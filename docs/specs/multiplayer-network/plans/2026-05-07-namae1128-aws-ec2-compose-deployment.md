# AWS EC2 docker-compose 배포

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Ready`

## Goal

선행 plan에서 만든 `docker-compose.yml`을 단일 AWS EC2 인스턴스에 배포해, 실 Quest 클라이언트가 EC2 백엔드를 통해 인증하고 EC2의 Dedicated Server 컨테이너에 룸으로 합류할 수 있게 만든다.

## Context

- 선행 plan [`2026-05-07-namae1128-docker-compose-local-stack.md`](2026-05-07-namae1128-docker-compose-local-stack.md)에서 spring/mariadb/dedicated-server 세 서비스가 compose로 함께 동작함이 검증된다.
- 본 plan은 그 compose 파일을 운영 환경에 그대로 옮기는 단계다. 1인 개발자/동아리 단계의 가장 일반적인 형태인 **EC2 1대 + docker-compose**를 채택한다.
- 본 plan은 Single-instance 배포만 다룬다. 오토스케일·다중 Dedicated Server·k8s/Agones는 모두 후속 단계로 미룬다.
- Photon Fusion은 Photon Cloud Relay를 outbound로 사용하므로 Dedicated Server 컨테이너에 인바운드 포트 개방은 필요하지 않다. 보안그룹은 백엔드 HTTPS 포트와 SSH 포트만 인바운드로 열면 된다.

## Decisions

- **인스턴스 타입**: t3.small (vCPU 2, 메모리 2GB)부터 시작. Unity Dedicated Server + JVM + MariaDB 동시 기동을 위한 최소치.
- **OS**: Ubuntu 24.04 LTS.
- **TLS**: Caddy reverse proxy로 백엔드 8080을 443에 노출하고 자동 Let's Encrypt 발급.
- **Secrets**: `.env`는 EC2의 `/etc/murang/.env`에 직접 배치(rsync/scp). AWS Secrets Manager 도입은 후속 단계.
- **DB 백업**: 일 1회 cron으로 `mysqldump` 결과를 EC2 EBS의 별도 디렉터리에 보관. S3 동기화는 후속.
- **배포 방식**: 수동 `git pull && docker compose pull && docker compose up -d` 스크립트. CI/CD 자동화는 후속.

## Approach

1. **`docker-compose.prod.yml` 오버레이** — 로컬 compose에서 변경되는 부분만 분리.
   - 모든 서비스에 `restart: unless-stopped`.
   - Spring 컨테이너의 SPRING_PROFILES_ACTIVE를 `prod`로.
   - 외부 노출은 Caddy만 80/443, 그 외는 internal network.
2. **Caddy 서비스 추가** — `docker-compose.prod.yml`에 `caddy:2` 컨테이너 정의. `Caddyfile`에서 `api.<도메인>` → `spring:8080` 리버스 프록시 1줄.
3. **EC2 프로비저닝 절차** — `docs/ops/aws-ec2-bootstrap.md`에 한 페이지로 정리:
   - EC2 인스턴스 생성·보안그룹(인바운드 22/80/443) 설정.
   - Docker Engine + docker compose plugin 설치.
   - 도메인 A 레코드 → EC2 EIP.
   - 리포지토리 clone, `/etc/murang/.env` 배치, 이미지 pull/up.
4. **`backend/src/main/resources/application-prod.yml`** — DB URL을 compose 내부 hostname `mariadb:3306` 기준으로 두고, JWT secret과 Photon AppId는 환경변수에서 주입.
5. **운영 smoke 절차** — Quest 빌드의 `MultiplayerAuthConfig.deviceBackendBaseUrl`을 EC2 도메인으로 가리킨 채 `meta-login` mock → `/users/me` 200 → `RoomClientSmokeTest`(또는 실 빌드) Photon 합류까지 수동 확인.
6. **롤백 전략** — `docker compose down && git checkout <prev>` 후 `docker compose up -d`. 이미지 버전 태그(`:YYYYMMDD-<sha>`)를 도입할지 여부는 본 plan 범위에서 결정.

## Deliverables

- `docker-compose.prod.yml`
- `Caddyfile`
- `backend/src/main/resources/application-prod.yml`
- `docs/ops/aws-ec2-bootstrap.md`
- `docs/ops/deploy-runbook.md` — 배포·롤백 절차
- `tools/deploy-ec2.ps1` 또는 `tools/deploy-ec2.sh` (옵션, 수동 절차 한 줄짜리 래퍼)

## Acceptance Criteria

- [ ] `[auto-hard]` `docker compose -f docker-compose.yml -f docker-compose.prod.yml config`가 에러 없이 통과한다.
- [ ] `[manual-hard]` 부트스트랩 가이드를 따라 새 EC2 인스턴스에서 `docker compose up -d`까지 도달한다.
- [ ] `[manual-hard]` `https://api.<도메인>/api/v1/auth/meta-login` 호출이 200을 반환한다.
- [ ] `[manual-hard]` 동일 `metaAccountId`로 두 번째 로그인 시 같은 `playerId`가 반환된다 (DB 영속성 확인).
- [ ] `[manual-hard]` `docker compose restart` 또는 EC2 재부팅 후 모든 서비스가 자동 복구된다.
- [ ] `[manual-hard]` 일 1회 `mysqldump` 백업이 cron으로 실행되고 결과 파일이 생성된다.

## Out of Scope

- 다중 EC2/오토스케일 그룹
- ECS/Fargate, EKS/Agones 마이그레이션
- AWS Secrets Manager·Parameter Store 통합
- S3 백업·복구 자동화
- CI/CD 파이프라인(GitHub Actions 등)
- 다중 Dedicated Server 인스턴스 또는 매치메이커 분리
- 실 Meta SDK verifier 활성화
- Photon AppId의 dev/prod 환경 분리
- **Quest USB 실기기 빌드(Android)에서의 EC2 Dedicated Server 룸 합류 검증** — Quest 실기기 manual-hard 1건은 후속 plan [`2026-05-11-namae1128-quest-onsite-integration-verification.md`](../plans/2026-05-11-namae1128-quest-onsite-integration-verification.md)으로 책임 이관 (cross sub-spec: 본 plan은 `03-room-session`, 후속 plan은 `01-user-auth` 소관). auth-gate·real-meta-verifier의 Quest 시나리오와 함께 한 빌드/사이드로드 사이클로 일괄 검증한다. 본 plan은 EC2 배포·인프라 동작 검증까지로 책임 범위 한정.

## Notes

- t3.small은 CPU 크레딧 기반이라 부하가 길어지면 throttling이 발생할 수 있다. 실 유저 로드 테스트가 시작되면 t3.medium 또는 c6g.large 같은 burstable이 아닌 타입으로의 전환을 검토한다.
- Photon AppId가 dev/prod에 동일하면 dev 클라이언트가 prod 룸 목록에 노출될 수 있다. 운영 시점에 AppId 분리가 필수가 되며, 이는 별도 후속 plan에서 다룬다.
- DB 백업 파일이 EC2 로컬에만 있으면 인스턴스 손실 시 데이터 회복이 불가능하다. 운영 안정화 단계에서 S3 동기화를 추가해야 한다.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. 후속 작업이 의존하는 운영 산출:
- `https://api.<도메인>/api/v1/...` 엔드포인트
- `docker-compose.prod.yml` 의 서비스/볼륨/네트워크 정의
- `docs/ops/deploy-runbook.md` 의 배포·롤백 절차
-->
