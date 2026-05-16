# AWS dev 토폴로지

[`docs/specs/multiplayer-network/specs/05-room-server-manager.md`](../specs/multiplayer-network/specs/05-room-server-manager.md)의 단일 AWS dev 환경 구성. local 검증용 `docker-compose.yml`(spring + mariadb + dedicated-server)과는 분리한다.

## 전체 그림

```
VPC (단일)
├── Public Subnet
│   ├── EC2 t3.small (Ubuntu 24.04 LTS)
│   │   ├── docker-compose.ec2-dev.yml
│   │   │   ├── spring container (port 8080 -> host 8080)
│   │   │   │   ├── Client-facing API
│   │   │   │   ├── Auth/User Service
│   │   │   │   └── RoomServerManager (ECS RunTask/DescribeTasks/StopTask)
│   │   │   └── mariadb container (host port 비공개, volume /var/lib/mysql)
│   │   └── IAM Instance Profile -> AWS SDK 자격증명
│   │
│   └── ECS Fargate room-server task (on-demand, RoomServerManager 가 RunTask)
│       ├── public IP (assignPublicIp=ENABLED)
│       └── outbound -> EC2 8080 의 /internal/rooms/{id}/ready,heartbeat
│
└── Internet Gateway
```

## 컴포넌트 책임

### EC2 (단일 인스턴스, t3.small)

- Spring Boot App 컨테이너: `Client-facing API`, `Auth/User Service`, `RoomServerManager`
- MariaDB 컨테이너: `users`, `rooms`, `room_server_instances` 영속
- 둘 다 `docker-compose.ec2-dev.yml` 한 파일로 기동
- `~/.env.aws-dev` 가 환경변수 단일 소스 (`docker compose --env-file` 로 주입)
- IAM Instance Profile 에 ECS RunTask/StopTask/DescribeTasks, CloudWatch Logs PutLogEvents 권한 부여 (AWS SDK 가 instance metadata 로 자동 획득)

### MariaDB

- 같은 EC2 의 별도 컨테이너
- host port 비공개 (`ports:` 없음 — Spring 컨테이너만 internal docker network 로 접근)
- volume `mariadb-data` 로 EBS 영속
- `/var/backups/mariadb` 마운트 — cron `mysqldump` 백업 위치

### ECS Fargate room-server task

- 이미지: ECR `<account>.dkr.ecr.ap-northeast-2.amazonaws.com/murang-room-server:<tag>`
- task definition family: `murang-room-server` (revision 자동 최신 사용)
- Launch type: `FARGATE`
- Network: 같은 VPC 의 public subnet, `assignPublicIp=ENABLED`
- 환경변수 (RoomServerManager 의 RunTask container override 로 주입):
  - `ROOM_ID`
  - `PHOTON_SESSION_NAME`
  - `MAX_PLAYERS`
  - `ROOM_RUNTIME_VERSION`
  - `ROOM_READY_CALLBACK_URL` — EC2 public DNS 의 `/internal/rooms/{id}/ready`
  - `ROOM_HEARTBEAT_CALLBACK_URL` — EC2 public DNS 의 `/internal/rooms/{id}/heartbeat`
- 외부 클라이언트는 `public_ip:game_port/udp` 로 합류 (Photon Cloud 가 라우팅)

### Internet Gateway

- Public subnet 의 outbound + inbound 게이트웨이
- EC2 의 8080 inbound 와 Fargate task 의 game_port 인바운드 모두 IG 경유

## 네트워크 / 보안 그룹

### EC2 보안 그룹 (sg-ec2)

| Direction | Port | Source | 목적 |
|---|---|---|---|
| Inbound | 22 | 운영자 IP (관리) | SSH (선택, AWS Systems Manager Session Manager 권장) |
| Inbound | 8080 | 0.0.0.0/0 | Spring public API + room-server 콜백 |
| Outbound | * | 0.0.0.0/0 | AWS API, ECR pull, Meta Graph 호출 |

### Fargate room-server 보안 그룹 (sg-room-server)

| Direction | Port | Source | 목적 |
|---|---|---|---|
| Inbound | game_port/UDP | 0.0.0.0/0 | 클라이언트 합류 (Photon Cloud 외부 라우팅 시 필요) |
| Outbound | 8080/TCP | sg-ec2 | ready/heartbeat 콜백 |
| Outbound | * | 0.0.0.0/0 | Photon Cloud relay, AWS API |

## DB 스키마 (현재 적용 마이그레이션)

- `V1__create_users.sql` — `users` 테이블
- `V2__add_player_id_to_users.sql` — ULID 기반 `player_id`
- `V3__create_rooms.sql` — `rooms (room_id, owner_user_id, photon_session_name, max_players, created_at, closed_at)`
- `V4__create_room_server_instances.sql` — `room_server_instances (id, room_id, status, ecs_cluster_arn, ecs_task_arn, task_public_ip, game_port, created_at, ready_at, last_heartbeat_at, terminated_at)`

## 의도적으로 빠진 것

- 다중 EC2 / 오토스케일 그룹 / RDS
- AWS Secrets Manager / Parameter Store (`~/.env.aws-dev` 사용)
- ECS task 의 별도 IAM role (`taskRoleArn`) 세부 정책 — 일단 비워두고 outbound 만 사용
- S3 백업 동기화 (`/var/backups/mariadb` 까지만)
- GitHub Actions / CI 자동 배포

후속 plan 에서 우선순위 정해서 채운다.
