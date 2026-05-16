# docker-compose 로컬 통합 스택 (spring + mariadb + dedicated-server)

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Done`

## Goal

로컬 개발 머신에서 `docker compose up` 한 명령으로 Spring 백엔드, MariaDB, Photon Fusion Dedicated Server 세 서비스를 함께 기동하고, 인증 → 룸 생성·합류·정리 시나리오를 컨테이너 환경에서 통합 검증한다. 본 plan이 만들어내는 compose 정의는 다음 AWS 배포 plan에서 동일한 형태로 재사용된다.

## Context

- 선행 plan [`2026-05-01-namae1128-dedicated-server-build-pipeline.md`](2026-05-01-namae1128-dedicated-server-build-pipeline.md)이 Linux Dedicated Server 산출물과 단독 Dockerfile을 만들어 둔다.
- Spring 백엔드(`backend/`)는 현재 호스트에서 `gradlew bootRun` 또는 `verify-local-auth.ps1`로 기동하고, MariaDB는 호스트에 직설치 또는 별개 docker로 띄우는 형태다.
- 기존 Windows 자동화 하네스(`tools/run-room-lifecycle-automation.ps1`)는 본 plan에서 **컨테이너 환경 기반 통합 smoke**로 대체된다.
- `02-user-persistence`의 영속화는 MariaDB 컨테이너의 named volume으로 대응한다. 재시작 후 동일 `playerId` 유지가 자동 검증되어야 한다.
- Photon Fusion은 Photon Cloud의 매치메이킹/Relay를 사용하므로 Dedicated Server 컨테이너에 인바운드 포트 매핑은 필수가 아니다. 백엔드만 호스트에 8080 포트를 노출한다.

## Decisions

- Compose 파일은 리포지토리 루트의 `docker-compose.yml`에 둔다. 운영용 오버레이는 별도 plan에서 `docker-compose.prod.yml`로 분리한다.
- Spring 백엔드 이미지는 `backend/Dockerfile`에서 multi-stage(Gradle build → JRE 21 runtime)로 빌드한다.
- MariaDB는 공식 이미지 `mariadb:11.4`(LTS) 사용. 데이터는 named volume `mariadb-data`에 보관.
- 환경변수는 `.env` 파일과 `docker-compose.yml`의 `environment` 블록 조합으로 관리한다. 비밀값(JWT secret 등)은 `.env`에만 두고 `.env.example`을 커밋한다.
- `app.security.meta.verifier-mode`는 mock으로 둔다(real verifier는 별도 후속 plan).

## Approach

1. **Spring Dockerfile** — `backend/Dockerfile`(multi-stage). build stage에서 `./gradlew bootJar`, runtime stage는 `eclipse-temurin:21-jre`에 jar 복사. health check는 `/api/v1/health`(없으면 가벼운 endpoint 추가) 또는 actuator로 둔다.
2. **`.dockerignore`** — `backend/.dockerignore` (Gradle 캐시·`build/`·IDE 설정 제외).
3. **MariaDB 설정** — compose에서 `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD` 환경변수, named volume `mariadb-data`, healthcheck(`mysqladmin ping`).
4. **Dedicated Server 컨테이너** — 선행 plan의 `docker/dedicated-server/Dockerfile`을 그대로 compose 서비스로 등록. Photon AppId/배경 환경변수는 `.env`에서 주입.
5. **`docker-compose.yml`** — 세 서비스 정의. depends_on으로 spring → mariadb 순서 보장. 네트워크는 기본 bridge.
6. **환경변수 분리** — `.env.example`에 placeholder 값, `.env`는 `.gitignore`에 추가.
7. **통합 smoke 절차** — `docker compose up -d` → spring health 확인 → `meta-login` mock 호출 → Editor `RoomClientSmokeTest`에서 Photon Cloud 경유 합류 → 서버 로그에 입장 기록 확인 → `docker compose down` 후 `docker compose up`으로 MariaDB 영속성 확인.
8. **e2e 자동화 재구성** — `tools/run-room-lifecycle-automation.ps1`을 폐기하거나 deprecated 표기하고, 같은 5개 시나리오를 docker-compose 환경에서 실행하는 `tools/run-stack-smoke.ps1`(또는 bash 버전 `tools/run-stack-smoke.sh`)을 새로 작성한다. 자동화 클라이언트는 컨테이너화의 그래픽 의존성 문제를 피해 호스트에서 실행하는 **혼합 모드**를 채택한다(서버·DB는 컨테이너, 헤드리스 클라이언트는 호스트 Linux Server 산출 또는 Editor batch).

## Deliverables

- `docker-compose.yml`
- `.env.example`
- `.gitignore` 갱신 (`.env` 추가)
- `backend/Dockerfile`
- `backend/.dockerignore`
- `docs/dev/local-stack.md` — 로컬 stack 기동·정지·검증 절차 짧은 가이드
- (선행 plan 산출인) `docker/dedicated-server/Dockerfile` 재사용

> `tools/run-stack-smoke` 자동화 스크립트는 본 plan에서 분리되어 후속 plan [`2026-05-08-namae1128-stack-smoke-automation.md`](2026-05-08-namae1128-stack-smoke-automation.md)에서 다룬다.

## Acceptance Criteria

- [x] `[auto-hard]` `docker compose config`가 에러 없이 통과한다.
- [x] `[auto-hard]` `docker compose build`가 spring/dedicated-server 두 이미지에 대해 성공한다.
- [x] `[manual-hard]` `docker compose up -d` 직후 spring `/api/v1/users/me`(또는 health)가 200을 반환할 수 있는 상태가 된다.
- [x] `[manual-hard]` `meta-login` mock 호출이 200을 반환하고 MariaDB에 user 레코드가 적재된다.
- [x] `[manual-hard]` `docker compose down` 후 `docker compose up`을 다시 실행해도 같은 `metaAccountId` 로그인 시 동일한 `playerId`가 유지된다.
- [x] `[manual-hard]` Editor의 `RoomClientSmokeTest.unity`가 Photon Cloud 경유로 컨테이너 Dedicated Server에 합류해 서버 로그에 입장이 기록된다.

## Out of Scope

- AWS EC2 배포 (다음 plan)
- HTTPS/TLS, 도메인, 리버스 프록시 구성 (다음 plan)
- 실 Meta SDK verifier 구현
- Flyway/Liquibase 마이그레이션 도구 도입
- 다중 Dedicated Server 인스턴스 오케스트레이션
- Photon AppId 환경 분리(dev/prod)

## Notes

- 헤드리스 클라이언트의 컨테이너화는 OpenGL/그래픽 의존성 때문에 까다로워 본 plan에서는 시도하지 않는다. e2e 자동화의 클라이언트 측은 호스트에서 Linux Server 빌드 또는 Editor batch로 실행한다.
- Photon AppId와 JWT secret은 `.env`에만 두고 `.env.example`에는 placeholder만 남긴다. 절대 커밋되지 않도록 `.gitignore` 점검이 필요하다.
- Dedicated Server 컨테이너는 stateless하지 않다(룸 상태 보유). 단일 인스턴스를 가정하며, 동시 다수 룸은 단일 프로세스 내에서 처리된다.
- 2026-05-08: `docker compose config --quiet` exit 0으로 AC #1 pass.
- 2026-05-08: `docker compose build`(AC #2)는 사용자 WSL2 + Docker Desktop 환경에서 spring/dedicated-server 두 이미지 모두 통과. 검증 중 발견된 사실 두 가지를 산출에 반영했다 — (1) `backend/gradlew`가 Windows git core.autocrlf로 CRLF 저장되어 `/bin/sh: ./gradlew: not found`로 실패, Dockerfile build stage에 `sed -i 's/\r$//' gradlew && chmod +x gradlew` 라인 추가로 해결. (2) MariaDB healthcheck를 `mysqladmin ping ...`에서 mariadb:11.x 표준인 `healthcheck.sh --connect --innodb_initialized`로 교체 + `start_period: 15s` 추가.
- 2026-05-08: AC #3~#6 manual-hard 통과. 검증 흐름은 `docker compose up -d` → mariadb (healthy) + spring Up → `meta-login` mock 호출(요청 키 `metaIdToken`, `nickname`) → 200 응답 + ULID `playerId` + access/refresh 토큰 + MariaDB `users` 레코드 적재 → `docker compose down` 후 재기동 시 동일 `playerId` 유지 → Unity Editor `RoomClientSmokeTest` Play → Photon Cloud kr 리전 → 컨테이너 Dedicated Server에 `adding player [Player:2]` 기록.
- 2026-05-08: 사용자 결정으로 원래 AC #7(`tools/run-stack-smoke` 5시나리오 자동화)는 본 plan에서 분리하고 별도 후속 plan [`2026-05-08-namae1128-stack-smoke-automation.md`](2026-05-08-namae1128-stack-smoke-automation.md)으로 옮겼다. 본 plan은 docker-compose 통합 + 통합 smoke까지로 닫는다.
- 2026-05-08: 사용자 환경(WSL2 + Docker Desktop)에서 호스트 8080 포트가 점유되는 사례가 발생할 수 있으므로, 충돌 시 `docker-compose.yml`의 spring `ports` 매핑을 `"8081:8080"`처럼 변경하면 된다(컨테이너 내부 포트 8080은 그대로 유지).

## Handoff

다음 plan(`AWS dev 토폴로지 (EC2 Spring + MariaDB, ECS Fargate room-server)`, `tools/run-stack-smoke 자동화`)이 의존하는 산출과 제약:

- **`docker-compose.yml` 3서비스 정의**: `spring`(build: backend/Dockerfile, ports 8080:8080), `mariadb`(image: mariadb:11.4, named volume `mariadb-data`, healthcheck: `healthcheck.sh --connect --innodb_initialized` + `start_period: 15s`), `dedicated-server`(build: docker/dedicated-server/Dockerfile). depends_on은 spring → mariadb(condition: service_healthy)로 직렬화.
- **환경변수 키 (`.env.example`)**: `MARIADB_DATABASE`, `MARIADB_USER`, `MARIADB_PASSWORD`, `MARIADB_ROOT_PASSWORD`, `JWT_SECRET`, `SPRING_PROFILES_ACTIVE`(기본 `dev`), `MURANG_META_MOCK_PREFIX`(기본 `mock-meta:`). 운영에서는 동일 키를 secrets manager 또는 CI 환경변수로 주입.
- **`backend/Dockerfile`**: multi-stage(`gradle:8-jdk21-jammy` build → `eclipse-temurin:21-jre` runtime). build stage에 `sed -i 's/\r$//' gradlew && chmod +x gradlew` 라인 포함 — Windows git CRLF 변환된 wrapper를 컨테이너에서 그대로 받기 위함. 운영에서도 그대로 사용.
- **MariaDB 영속성**: named volume `mariadb-data`가 `docker compose down`에서 살아남음. `docker compose down -v`만 데이터 초기화에 사용. 운영에서는 host bind mount 또는 RDS로 교체 가능.
- **dedicated-server 빌드 사전 조건**: `Builds/RoomAutomation/LinuxServer/` 산출이 build context(워크트리/리포 루트)에 있어야 함(선행 plan).
- **호스트 포트 매핑**: 8080 충돌 시 `docker-compose.yml`의 spring `ports`만 변경(컨테이너 내부 8080 고정). Editor `MultiplayerAuthConfig.editorBackendBaseUrl`도 같이 변경 필요.
- **Photon AppId**: 현재 `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`에 동봉되어 dedicated-server 이미지에 포함됨. 운영 분리(dev/prod AppId)는 추가 후속 plan.
- **메인 인증 흐름**: `POST /api/v1/auth/meta-login` 요청 키는 `metaIdToken`(NotBlank, 12~4096자) + `nickname`(2~32자, `^[\p{L}\p{N} ]+$`). mock 모드에서는 `mock-meta:` prefix가 붙은 토큰만 통과.
- **자동화 회귀 분리**: `tools/run-stack-smoke` 5시나리오 검증은 후속 plan [`2026-05-08-namae1128-stack-smoke-automation.md`](2026-05-08-namae1128-stack-smoke-automation.md)에서 본 plan의 docker-compose 환경 위에 작성.
