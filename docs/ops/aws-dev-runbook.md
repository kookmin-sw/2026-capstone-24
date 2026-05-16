# AWS dev 런북

EC2 control-plane + ECS Fargate room-server 토폴로지([`aws-dev-topology.md`](aws-dev-topology.md))를 처음 부트스트랩하고 일상 운영하는 절차.

본 plan 의 acceptance criteria 9건(`[manual-hard]`)이 모두 본 런북 따라가면 통과한다고 가정한다.

---

## 0. 사전 준비

- AWS 계정 + IAM 사용자 (Admin 또는 최소 권한: ECS, ECR, EC2, IAM, CloudWatch, VPC)
- `aws` CLI v2 인증 완료 (`aws sts get-caller-identity` 통과)
- 로컬 Docker 이미지 빌드 환경
- `~/.env.aws-dev.example` → 값 채워 둔 `.env.aws-dev` (아직 EC2 안 올라간 상태)

---

## 1. VPC / Subnet / Security Group

콘솔 또는 CLI 로 한 번만 수동 생성.

1. **VPC** — `10.10.0.0/16` (또는 기본 VPC 재사용)
2. **Public Subnet** — `10.10.1.0/24`, Auto-assign public IPv4 ON
3. **Internet Gateway** — VPC 에 attach, public subnet route table 에 `0.0.0.0/0 -> IGW` 추가
4. **Security Groups**
   - `sg-ec2`: inbound 22 (운영자 IP), 8080 (0.0.0.0/0); outbound all
   - `sg-room-server`: inbound `game_port/UDP` 0.0.0.0/0, outbound TCP 8080 → `sg-ec2`, outbound all
   - 보안 그룹 ID 두 개를 `.env.aws-dev` 의 `MURANG_ROOM_RUNTIME_ECS_SECURITY_GROUP_IDS` 에 채울 것

---

## 2. ECR 리포지토리 + Fargate task definition

```bash
# 2-1. ECR repo 생성 (한 번만)
aws ecr create-repository --repository-name murang-room-server --region ap-northeast-2

# 2-2. 로그인
aws ecr get-login-password --region ap-northeast-2 | \
  docker login --username AWS --password-stdin <account>.dkr.ecr.ap-northeast-2.amazonaws.com

# 2-3. 이미지 빌드 + push (room-server 변경 시 매번)
tools/push-room-server-image.sh <tag>     # Linux/WSL2
tools/push-room-server-image.ps1 -Tag <tag>  # Windows
```

Task definition 은 [`tools/task-definition-room-server.json`](../../tools/task-definition-room-server.json) 템플릿을 substitute 후 등록한다.

```bash
# <ACCOUNT_ID>, <REGION>, <IMAGE_TAG> 치환 후 register
sed \
  -e "s/<ACCOUNT_ID>/777459856497/g" \
  -e "s/<REGION>/ap-northeast-2/g" \
  -e "s/<IMAGE_TAG>/dev-002/g" \
  tools/task-definition-room-server.json \
  > /tmp/task-definition-room-server.rendered.json

aws ecs register-task-definition \
  --cli-input-json file:///tmp/task-definition-room-server.rendered.json \
  --region ap-northeast-2
```

핵심 명세:

- family: `murang-room-server`
- requiresCompatibilities: `["FARGATE"]`
- networkMode: `awsvpc`
- cpu/memory: 1024 / 2048 (조정 가능)
- container:
  - name: `room-server` (반드시 `MURANG_ROOM_RUNTIME_ECS_CONTAINER_NAME` 과 일치)
  - image: `<account>.dkr.ecr.<region>.amazonaws.com/murang-room-server:<tag>`
  - portMappings: `7777/UDP`
  - logConfiguration: `awslogs` driver → `/ecs/murang-room-server` log group

> 환경별 task definition 차이(예: 다른 이미지 tag, cpu/memory)는 본 템플릿을 복사해 별도 파일로 관리한다.

---

## 3. EC2 인스턴스 부트스트랩

1. **AMI**: Ubuntu 24.04 LTS
2. **Instance type**: `t3.small`
3. **Security group**: `sg-ec2`
4. **IAM Instance Profile** — 새로 만들거나 기존 역할에 다음 정책 attach
   - `AmazonECS_FullAccess` (또는 RunTask/StopTask/DescribeTasks 만 허용하는 최소 정책)
   - `CloudWatchLogsFullAccess`
5. **User data** (선택) 또는 SSH 접속 후 수동:
   ```bash
   sudo apt-get update -y
   sudo apt-get install -y docker.io docker-compose-v2 git mariadb-client cron
   sudo usermod -aG docker ubuntu
   ```

리포지토리 clone:

```bash
git clone https://github.com/kookmin-sw/2026-capstone-24.git
cd 2026-capstone-24
git checkout feat/multiplayer-network   # 또는 운영 브랜치
cp .env.aws-dev.example ~/.env.aws-dev
# ~/.env.aws-dev 값 채우기
```

---

## 4. control-plane 기동

```bash
# 빌드 + up
tools/deploy-ec2-control-plane.sh up      # Linux
# 또는 수동
docker compose -f docker-compose.ec2-dev.yml --env-file ~/.env.aws-dev build spring
docker compose -f docker-compose.ec2-dev.yml --env-file ~/.env.aws-dev up -d
```

확인:

```bash
docker compose -f docker-compose.ec2-dev.yml ps         # mariadb healthy, spring Up
curl -s http://localhost:8080/actuator/health | jq .    # status=UP
```

EC2 외부에서:

```bash
curl -s http://<ec2-public-dns>:8080/actuator/health | jq .
```

> spring 서비스는 `awslogs` Docker 로그 드라이버를 사용하므로 `docker compose logs spring` / `docker logs <id>` 는 동작하지 않는다. 부팅 로그·런타임 로그는 CloudWatch Logs 의 `/ec2/murang-spring` (또는 `MURANG_SPRING_LOG_GROUP` 으로 override) 에서 확인한다.
>
> ```bash
> aws logs tail /ec2/murang-spring --follow --since 5m --region ap-northeast-2
> aws logs tail /ec2/murang-spring --filter-pattern "ERROR" --since 1h --region ap-northeast-2
> ```

---

## 5. Smoke — 룸 생성/입장/종료

1. Quest 빌드(또는 PC 빌드 client 듀얼)에서 EC2 endpoint(`http://<ec2-public-dns>:8080`) 로 인증.
2. 룸 생성 요청 — Spring 로그에 `RoomServerManager` 가 ECS RunTask 호출하는 흐름 확인.
3. MariaDB 에서 `rooms`, `room_server_instances` row 생성/갱신 확인:
   ```bash
   docker compose -f docker-compose.ec2-dev.yml exec mariadb \
     mariadb -u murang_app -p"$MARIADB_PASSWORD" murang -e \
     "SELECT room_id, owner_user_id, photon_session_name, created_at, closed_at FROM rooms;"
   docker compose -f docker-compose.ec2-dev.yml exec mariadb \
     mariadb -u murang_app -p"$MARIADB_PASSWORD" murang -e \
     "SELECT id, room_id, status, ecs_task_arn, task_public_ip, ready_at FROM room_server_instances;"
   ```
4. ECS 콘솔에서 task RUNNING 확인 → task 가 EC2 의 `/internal/rooms/{id}/ready` POST → Spring 로그에 `mark ready` → DB `status=READY`, `ready_at` 갱신 확인.
5. 클라이언트가 `task_public_ip:game_port/udp` 합류 → 서버 로그에 입장 기록.
6. 마지막 유저 leave → Spring 이 ECS StopTask → `status=TERMINATED`, `terminated_at`, `rooms.closed_at` 갱신.

---

## 6. MariaDB 일일 백업 (cron)

백업 명령 자체는 [`tools/mysqldump-cron.sh`](../../tools/mysqldump-cron.sh) wrapper 가 캡슐화한다 (`--single-transaction --quick --routines --triggers` + gzip + retention rotation). cron 은 wrapper 만 호출.

### 1. 수동 1회 실행 → wrapper 정상 동작 확인

```bash
sudo /home/ubuntu/2026-capstone-24/tools/mysqldump-cron.sh
ls -lh /var/backups/mariadb/                                 # murang-YYYYMMDD-HHMMSS.sql.gz 존재
sudo zcat /var/backups/mariadb/murang-*.sql.gz | head -20    # CREATE TABLE / INSERT 보임
```

### 2. cron 등록

```bash
sudo tee /etc/cron.d/murang-mariadb-dump > /dev/null <<'EOF'
SHELL=/bin/bash
PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
30 4 * * * root /home/ubuntu/2026-capstone-24/tools/mysqldump-cron.sh >> /var/log/mariadb-dump.log 2>&1
EOF
sudo chmod 644 /etc/cron.d/murang-mariadb-dump
sudo systemctl status cron --no-pager                        # active (running)
sudo run-parts --test /etc/cron.d 2>/dev/null || true
```

### 3. 다음날 결과 검증

```bash
ls -lh /var/backups/mariadb/                                 # 새 .sql.gz 추가됨
sudo tail -20 /var/log/mariadb-dump.log                      # "mariadb backup -> ... bytes" 로그
```

> retention: 기본 14일 (RETENTION_DAYS 환경변수로 조정). S3 동기화는 후속 plan. 현재는 EC2 EBS 로컬 보관만.

---

## 7. 정지 / 재기동

```bash
# 정지 (볼륨 보존)
docker compose -f docker-compose.ec2-dev.yml --env-file ~/.env.aws-dev down

# 재기동
docker compose -f docker-compose.ec2-dev.yml --env-file ~/.env.aws-dev up -d

# 코드 업데이트 후 재배포
tools/deploy-ec2-control-plane.sh redeploy
```

---

## 8. 트러블슈팅

| 증상 | 점검 |
|---|---|
| `aws-dev` 프로파일에서 Spring 부팅 실패 | `~/.env.aws-dev` 의 필수 값(`DB_*`, `JWT_SECRET`, `MURANG_ROOM_RUNTIME_ECS_*`) 누락 여부 |
| `ECS RunTask AccessDenied` | EC2 Instance Profile 의 ECS 정책 누락. `aws sts get-caller-identity --instance-id` 로 IAM role 확인 |
| Fargate task `READY` 까지 못 감 | task log 확인 (`/ecs/murang-room-server`), ready callback URL 의 도달성 확인 — Fargate SG outbound 8080 -> sg-ec2 inbound 8080 |
| `room_server_instances.ecs_task_arn` 은 채워졌는데 `ready_at` 이 null | task 부팅 시간 (Fargate cold start + Unity headless 부팅) 또는 callback URL 도달성 문제 |
| `mysqldump` cron 결과 파일 없음 | `cron` 데몬 동작 여부 (`sudo systemctl status cron`), `/etc/cron.d/murang-mariadb-dump` 권한 (644 권장) |

---

## 9. 본 plan AC 매핑

| AC | 검증 위치 |
|---|---|
| `[auto-hard]` `docker compose config` 통과 | 청크 4 커밋 시 자동 확인 |
| `[manual-hard]` EC2 control plane 외부 접근 | §4 `curl <ec2-public-dns>:8080/actuator/health` |
| `[manual-hard]` 동일 metaAccountId 재로그인 시 같은 playerId | Quest 빌드 두 번 로그인 후 backend `players_*` 확인 |
| `[manual-hard]` `rooms`/`room_server_instances` row + RunTask ARN 저장 | §5 step 3 |
| `[manual-hard]` ready callback → status=READY | §5 step 4 |
| `[manual-hard]` Quest 빌드 → Fargate room-server 합류 | §5 step 5 |
| `[manual-hard]` 종료 → StopTask + terminated_at + rooms.closed_at | §5 step 6 |
| `[manual-hard]` CloudWatch 로그 + 1개 이상 custom metric | [`cloudwatch-minimum-observability.md`](cloudwatch-minimum-observability.md) |
| `[manual-hard]` MariaDB 외부 비공개 | docker-compose.ec2-dev.yml 에 `ports:` 없음 + sg-ec2 inbound 3306 차단 |
| `[manual-hard]` 일 1회 mysqldump cron | §6 |
