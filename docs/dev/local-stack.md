# 로컬 통합 스택 기동 가이드

spring + mariadb + dedicated-server 세 서비스를 `docker compose up` 한 명령으로 기동하는 절차.

## 사전 조건

- **Docker Desktop** (Windows WSL2 Integration 활성화)
- Unity Editor에서 `RoomServerBuildMenu → Build Dedicated Server (Linux)` 실행 완료
  - 산출 경로: `Builds/RoomAutomation/LinuxServer/RoomServer.x86_64`
  - `docker compose build dedicated-server` 실행 전 산출물이 반드시 존재해야 한다.

## 1단계 — `.env` 만들기

```bash
cp .env.example .env
```

`.env`를 열어 다음 값을 채운다.

| 키 | 설명 |
|---|---|
| `MARIADB_PASSWORD` | 로컬용 임의 비밀번호 |
| `MARIADB_ROOT_PASSWORD` | MariaDB root 비밀번호 |
| `JWT_SECRET` | Base64 64바이트 이상 랜덤 문자열 (`openssl rand -base64 64`) |

`MURANG_META_MOCK_PREFIX`는 기본값 `mock-meta:`을 그대로 두면 된다.

## 2단계 — 이미지 빌드

```bash
# spring 이미지만 (MariaDB는 공식 이미지라 build 불필요)
docker compose build spring

# dedicated-server 이미지 (Builds/RoomAutomation/LinuxServer/ 산출 필요)
docker compose build dedicated-server

# 둘 다 한 번에
docker compose build
```

## 3단계 — 스택 기동

```bash
docker compose up -d
```

서비스 순서: mariadb healthy → spring 기동 → dedicated-server 기동.

## 4단계 — 상태 확인 및 검증

### 서비스 상태

```bash
docker compose ps
```

`spring`과 `mariadb`가 `healthy` / `running` 상태인지 확인한다.

### Spring 헬스

```bash
curl -s http://localhost:8080/actuator/health | python -m json.tool
```

`"status": "UP"` 응답이면 정상.

### meta-login mock 호출 (인증 확인)

```bash
curl -s -X POST http://localhost:8080/api/v1/auth/meta-login \
  -H "Content-Type: application/json" \
  -d '{"metaIdToken":"mock-meta:test-user-01","nickname":"테스터"}'
```

200 응답과 함께 `accessToken`이 반환되면 성공.  
MariaDB `users` 테이블에 레코드가 생성되었는지 확인:

```bash
docker compose exec mariadb mariadb -u murang_app -p"${MARIADB_PASSWORD}" murang \
  -e "SELECT id, meta_account_id, player_id FROM users;"
```

### 로그 확인

```bash
docker compose logs -f spring
docker compose logs -f dedicated-server
```

## 5단계 — 정지

```bash
# 컨테이너 정지 (MariaDB 볼륨 보존)
docker compose down

# 완전 초기화 (볼륨 포함 삭제 — 데이터 소멸 주의)
docker compose down -v
```

## 영속성 재확인

1. `docker compose down` 으로 스택 정지
2. `docker compose up -d` 로 재기동
3. 동일 `mock-meta:test-user-01` 토큰으로 meta-login 재호출
4. 반환된 `playerId`가 최초 호출과 동일한지 확인

## 다음 단계

로컬 스택 검증 완료 후 AWS EC2 배포는 후속 plan `2026-05-07-namae1128-aws-ec2-compose-deployment.md`를 참조.

## Real Meta Verifier 사용 절차

Quest 실기기에서 Oculus Graph API 실제 검증을 사용하려면 아래 절차를 따른다.

### 사전 조건

- Meta Developer Dashboard에서 **App ID**와 **App Secret** 발급 완료.
- Quest 빌드에 이미 App ID가 박혀 있는지(`OculusPlatformSettings`) 확인.

### 환경변수 설정

`.env` 파일에 다음 값을 추가/수정한다:

```
MURANG_META_VERIFIER_MODE=real
MURANG_META_APP_ID=<Meta Developer Dashboard에서 발급한 App ID>
MURANG_META_APP_SECRET=<Meta Developer Dashboard에서 발급한 App Secret>
```

경고: `MURANG_META_APP_SECRET`은 절대 git에 커밋하지 않는다.

### 스택 기동

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

### 검증

1. Quest 실기기에 Unity 빌드를 사이드로드한다.
2. `MultiplayerAuthGate` 버튼을 누른다.
3. Spring 컨테이너 로그에서 `RealMetaIdTokenVerifier`가 Graph API를 호출하는 로그와 `is_valid: true` 응답을 확인한다:
   ```bash
   docker compose logs -f spring
   ```
4. `statusLabel`에 `Multiplayer Active — <nickname>`이 표시되면 인증 성공.

### mock 모드 복귀

실기기 검증 후 개발 환경을 mock 모드로 되돌리려면 `.env`에서 `MURANG_META_VERIFIER_MODE=mock`으로 변경하고 스택을 재기동한다.
