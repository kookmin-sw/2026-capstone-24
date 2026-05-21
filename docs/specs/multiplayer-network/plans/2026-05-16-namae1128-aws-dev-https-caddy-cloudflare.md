# AWS dev HTTPS 종단 (Elastic IP + Cloudflare DNS + Caddy reverse proxy + Let's Encrypt)

**Linked Spec:** [`05-room-server-manager.md`](../specs/05-room-server-manager.md)
**Status:** `Ready`

## Goal

EC2 control plane 앞에 Caddy reverse proxy 를 두고 Let's Encrypt 자동 인증서로 HTTPS 종단을 활성화한다. backend Spring 은 외부에 직접 노출하지 않고 Caddy 내부 네트워크로만 받는다. Quest 빌드는 cleartext 우회 토글(`Allow downloads over HTTP = Always allowed`)을 폐기하고 `https://<domain>` 로 정상적으로 호출한다. dedicated server 의 ready/heartbeat callback URL 도 같은 HTTPS 도메인 경유로 통일.

## Context

[`2026-05-07-namae1128-aws-dev-topology-ec2-fargate.md`](./2026-05-07-namae1128-aws-dev-topology-ec2-fargate.md) 가 EC2 + Spring + MariaDB + ECS Fargate room-server 토폴로지를 plain HTTP(8080) 로 세팅했다. dev/prototype 단계에서는 충분했지만 Android 9+ (Quest 포함) 가 기본적으로 cleartext HTTP 를 차단해 다음 우회책이 필요했다.

- Unity `Player Settings → Android → Other Settings → Allow downloads over HTTP*` = `Always allowed`
- (선택) `Assets/Plugins/Android/AndroidManifest.xml` 에 `android:usesCleartextTraffic="true"`

이건 dev only 우회이고 다음 문제를 동반한다.

1. 빌드 전체에 cleartext 가 허용돼 의도하지 않은 HTTP 호출도 통과 (보안 표면 ↑).
2. 새로운 클라이언트 환경 (예: iOS, 다른 안드로이드 기기) 으로 확장 시 같은 우회가 필요.
3. 운영 단계로 갈 때 결국 HTTPS 가 필요하므로 어차피 한 번은 마이그레이션 필요.

본 plan 은 그 마이그레이션을 dev 단계에서 미리 처리해 production-style 토폴로지를 유지한다. 도메인은 사용자가 이미 보유한 Cloudflare zone 의 서브도메인을 사용한다. Cloudflare 는 **DNS only (gray cloud)** 로 운영해 모든 HTTPS 종단·인증서 발급은 EC2 Caddy + Let's Encrypt 가 책임지고, Cloudflare 는 A 레코드 1개만 담당한다. EC2 Elastic IP 가 attached 인 한 IP 도 무료 (EIP 자체는 detached 시 월 ~$3.6).

### 왜 Caddy 인가

선택지를 비교 후 본 use case (단일 EC2 + 단일 upstream + dev) 에 Caddy 채택.

| 기준 | Caddy | Nginx + certbot |
|---|---|---|
| 설정 분량 | ~3~5 줄 Caddyfile | ~30~50 줄 nginx.conf + certbot cron + reload hook |
| Let's Encrypt | 자동 (발급/갱신/HTTPS 리다이렉트 기본 ON) | 수동 (certbot 별도 설치, 인증서 경로 명시, 90일 갱신 cron) |
| HTTP/2·HTTP/3 | 기본 ON | HTTP/2 명시 설정, HTTP/3 미지원 |
| 운영 시 인증서 ops 부담 | 0 | reload hook + cron + 실패 알람 모니터링 |
| 본 use case 적합도 | ★★★★★ | ★★★ (over-engineering) |

Lock-in 위험은 낮음 — 추후 nginx 로 swap 필요 시 docker-compose 의 service 한 개 교체 + 설정 재작성으로 1~2시간.

## Verified Structural Assumptions

- `docker-compose.ec2-dev.yml` 에 `spring`/`mariadb` 두 서비스가 정의돼 있고 `spring` 이 `ports: ["8080:8080"]` 으로 host 포트 노출 중 — `Read docker-compose.ec2-dev.yml (2026-05-16)`
- `application-aws-dev.yml` 의 `murang.room.internal-callback.base-url` 은 `MURANG_ROOM_INTERNAL_CALLBACK_BASE_URL` env 로 주입 — base URL 만 바꾸면 backend 가 ECS task env 에 그대로 전파 — `Read backend/src/main/resources/application-aws-dev.yml (2026-05-16)`
- ECS Fargate task 가 호출하는 ready/heartbeat URL 은 `RoomCallbackUrlBuilder` 가 `RoomInternalCallbackProperties.baseUrl` + `/internal/rooms/{id}/ready|heartbeat` 로 빌드 — base URL 한 곳만 바꾸면 두 경로 모두 HTTPS — `Read backend/src/main/java/com/murang/room/manager/RoomCallbackUrlBuilder.java (2026-05-16)`
- Unity `MultiplayerAuthConfig.asset` 의 `deviceBackendBaseUrl` 이 Quest 빌드 backend URL 단일 진실원 — `Read Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset (2026-05-16)`
- `RoomInternalCallbackController` 가 `/internal/rooms/{id}/ready|heartbeat` 경로 + `X-Internal-Token` 헤더 검증 (path/query 변경 없음) — `Read backend/src/main/java/com/murang/room/controller/RoomInternalCallbackController.java (2026-05-16)`
- 본 plan 의 acceptance 는 [`01-user-auth`](../specs/01-user-auth.md) 와 동일하게 **Quest 실기기 빌드** 기준 — Editor PlayMode 한정 동작은 acceptance 범위 밖 (참고: `cd80f2b`)

## Approach

본 plan 은 코드 변경 + 인프라 절차 갱신 + Unity 자산 변경이 섞여 있다. 분담:

- Claude 자율: docker-compose / Caddyfile / docs / .env.aws-dev.example / Unity asset (MCP)
- 사용자: AWS 콘솔 (EIP/SG), Cloudflare 대시보드 (A 레코드 + DNS only 토글), EC2 SSH 절차 적용, Quest cleartext 토글 OFF + 빌드

### 코드/문서 변경 (Claude)

1. **Caddy 서비스 추가** (`docker-compose.ec2-dev.yml`).
   - 새 서비스 `caddy`: image `caddy:2`, ports 80/443 외부 노출, volumes `caddy_data:/data` + `caddy_config:/config` + `./docker/caddy/Caddyfile:/etc/caddy/Caddyfile:ro`
   - `spring` 서비스의 host port mapping 제거 (`ports:` 라인 삭제) — Caddy 가 internal docker network 로만 접근
   - `mariadb` 그대로 (외부 비공개 정책 유지)
   - Healthcheck: caddy 에 `curl -sf http://localhost:2019/config/` (admin API) — 단, 외부 노출 X (admin 은 localhost only 가 기본)
2. **`docker/caddy/Caddyfile` 신규**.
   ```caddy
   {$MURANG_PUBLIC_HOSTNAME} {
       reverse_proxy spring:8080
       encode gzip
       log {
           output stdout
           format json
       }
   }
   ```
   - 도메인은 `MURANG_PUBLIC_HOSTNAME` env 로 주입 (예: `dev.<사용자 Cloudflare 도메인>`)
   - Caddy 가 자동으로 80 → 443 리다이렉트 + Let's Encrypt 발급/갱신 (HTTP-01 challenge)
3. **`.env.aws-dev.example` 갱신**.
   - 신규: `MURANG_PUBLIC_HOSTNAME=REPLACE_WITH_PUBLIC_DOMAIN` (사용자 Cloudflare zone 의 서브도메인)
   - 기존 `MURANG_ROOM_INTERNAL_CALLBACK_BASE_URL` 의 예시를 `https://${MURANG_PUBLIC_HOSTNAME}` 로 안내 (주석)
4. **`docs/ops/aws-dev-topology.md` 갱신**.
   - 아키텍처 도식에 Caddy 노드 추가 (HTTPS:443 → Caddy → http:8080 → Spring)
   - SG sg-ec2 inbound 표 갱신: 8080 → 80/443
   - 도메인/DNS 노드는 `Cloudflare (DNS only, gray cloud)` 로 명시 — proxy 모드 아님
5. **`docs/ops/aws-dev-runbook.md` 갱신**.
   - §1 (VPC/SG) 의 inbound 규칙 80/443 으로 교체
   - 신규 §1.1: Elastic IP 할당 + EC2 attach + Cloudflare zone 에 A 레코드 추가 (Proxy status = DNS only) 절차
   - 신규 §1.2: 첫 인증서 발급 절차 (Caddy 컨테이너 기동 시 자동, A 레코드 + SG 80 inbound + Proxy status = DNS only 가 필수 조건 — Proxied 상태면 HTTP-01 challenge 가 Cloudflare edge 에서 종단되어 발급 실패)
   - 트러블슈팅 표에 "ACME challenge 실패" / "도메인 미반영" / "Cloudflare Proxied 상태로 challenge 실패" 추가
6. **Unity `MultiplayerAuthConfig.asset` 갱신**.
   - `deviceBackendBaseUrl` 을 placeholder `https://dev.example.com` 로 (사용자가 실제 Cloudflare 도메인 확정 후 다시 갱신)
   - MCP `manage_asset` 또는 `UNITY_YAML_OVERRIDE=1` 우회 Edit (단순 propertyPath 변경이라 안전)
7. **(선택) `application-aws-dev.yml` 주석 추가**.
   - `internal-callback.base-url` 위에 "HTTPS 경유 권장" 코멘트만 한 줄

### 사용자 인프라 작업

A. **Elastic IP 할당**: AWS 콘솔 → EC2 → Elastic IPs → Allocate → 기존 인스턴스에 Associate. IP 한 개 확보 (attached 동안 무료, detached 시 월 ~$3.6). 기존에 random EC2 public IP 를 쓰고 있었다면 attach 와 동시에 IP 가 새 EIP 로 교체됨 (DNS 반영 + Photon Custom Auth 화이트리스트 등 다운스트림 영향 점검).

B. **Cloudflare A 레코드 추가**: Cloudflare 대시보드 → 사용자 zone 선택 → DNS → Records → Add record.
   - Type `A`, Name `dev` (또는 사용자 선택 서브도메인), IPv4 = 위 EIP, **Proxy status = DNS only (gray cloud)**, TTL `Auto`.
   - **Proxied (orange cloud) 로 두지 말 것** — Cloudflare edge 가 80/443 을 가로채 HTTP-01 challenge 가 Caddy 에 도달하지 못해 인증서 발급 실패.
   - 반영 확인: `dig +short dev.<zone>` 또는 `nslookup dev.<zone>` 가 EIP 1줄 반환 (Cloudflare anycast IP 가 반환되면 Proxied 상태이니 토글 재확인).

C. **SG sg-ec2 inbound 갱신**: 8080 제거, **80 + 443 추가** (둘 다 0.0.0.0/0). 80 은 Let's Encrypt ACME HTTP-01 challenge 에 필수.

D. **`~/.env.aws-dev` 갱신**:
   - 추가: `MURANG_PUBLIC_HOSTNAME=dev.<사용자 Cloudflare 도메인>` (예: `dev.example.com`)
   - 변경: `MURANG_ROOM_INTERNAL_CALLBACK_BASE_URL=https://dev.<사용자 Cloudflare 도메인>`

E. **EC2 redeploy**:
   ```bash
   cd ~/2026-capstone-24
   git pull --ff-only
   docker compose -f docker-compose.ec2-dev.yml --env-file ~/.env.aws-dev pull caddy
   docker compose -f docker-compose.ec2-dev.yml --env-file ~/.env.aws-dev up -d
   docker compose -f docker-compose.ec2-dev.yml --env-file ~/.env.aws-dev logs -f caddy
   # 첫 기동 시 Caddy 가 ACME challenge 진행. 10~30초 안에 "certificate obtained successfully" 로그.
   ```
   첫 인증서 발급 성공까지 80 inbound + 도메인 A 레코드 + EIP attach 가 다 갖춰져야 함.

F. **검증**:
   ```bash
   curl -fsS https://dev.<사용자 Cloudflare 도메인>/actuator/health
   # {"status":"UP"} 응답 + 인증서 Issuer: R3 또는 E1 (Let's Encrypt) 확인
   ```

G. **Quest 빌드 cleartext 토글 OFF**: `Player Settings → Android → Other Settings → Allow downloads over HTTP* = Not allowed`. 빌드 + 사이드로드 + 시연.

## Deliverables

- `docker-compose.ec2-dev.yml` 수정 (caddy 서비스 추가, spring 외부 포트 제거, caddy 볼륨 정의)
- `docker/caddy/Caddyfile` 신규
- `.env.aws-dev.example` 에 `MURANG_PUBLIC_HOSTNAME` 추가 + `MURANG_ROOM_INTERNAL_CALLBACK_BASE_URL` 예시 https 로 갱신
- `docs/ops/aws-dev-topology.md` 갱신 (Caddy 노드 + 인증서 경로 도식)
- `docs/ops/aws-dev-runbook.md` 갱신 (EIP / Cloudflare A 레코드 (DNS only) / Caddy 절차 추가, 트러블슈팅 표 보강)
- `Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset` `deviceBackendBaseUrl` HTTPS 로 갱신
- `ProjectSettings/ProjectSettings.asset` Android cleartext 토글 OFF (사용자 측 Editor 작업 후 commit)

## Acceptance Criteria

- [ ] `[auto-hard]` `docker compose -f docker-compose.ec2-dev.yml --env-file <test-env> config` 가 caddy 서비스 정의 포함 + syntax 0 error.
- [ ] `[auto-hard]` `docker run --rm -v $(pwd)/docker/caddy/Caddyfile:/etc/caddy/Caddyfile:ro caddy:2 caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile` 가 valid 응답.
- [ ] `[manual-hard]` EC2 deploy 후 `curl -fsSv https://<domain>/actuator/health` 가 200 + TLS handshake OK + 인증서 Issuer Let's Encrypt (`R3` 또는 `E1`).
- [ ] `[manual-hard]` Quest 빌드 (cleartext 토글 OFF) → 사이드로드 → AuthGate Activate → mock 또는 real Meta 모드에서 인증 통과 (Spring 로그에 `POST /api/v1/auth/meta-login 200`).
- [ ] `[manual-hard]` Quest 에서 룸 생성 → ECS Fargate task 부팅 → ready callback 이 `https://<domain>/internal/rooms/{id}/ready` 로 도달 (Spring 로그 + DB `room_server_instances.status = READY`).
- [ ] `[manual-hard]` SG sg-ec2 inbound 8080 제거된 상태에서도 위 시나리오 통과 (8080 외부 노출 없이 HTTPS 만으로 동작).

## Out of Scope

- 운영(prod) 도메인 + 다중 환경 (dev/staging/prod) 분리
- ACM/CloudFront/ALB 같은 AWS 매니지드 옵션 (별도 plan)
- Caddy access log 의 CloudWatch Logs 송출 (현재는 stdout JSON, Spring 과 같은 log driver 로 흡수)
- Cloudflare Proxied (orange cloud) 모드 + Cloudflare Origin Certificate / Authenticated Origin Pulls (DDoS 완화·edge caching 이점이 있으나 본 plan 의 Caddy + Let's Encrypt 구성과 직교. 운영 단계에서 별도 plan)
- ACME DNS-01 challenge + caddy-dns/cloudflare 플러그인 (port 80 노출 불필요 + wildcard 인증서 발급 가능. Caddy custom image 빌드 + Cloudflare API token 관리 비용이 dev 단계에는 과함. 후속 plan 후보)
- nginx + certbot 으로의 swap (필요 시 별도 plan)

## Notes

- Caddy 가 첫 인증서 발급 후 `/data/caddy/certificates/` 아래에 보관 — 볼륨 `caddy_data` 가 영속화. 컨테이너 재시작·재배포에도 인증서 유지.
- Let's Encrypt rate limit: 도메인당 주당 50건. `caddy_data` 볼륨을 함부로 날리면 재발급 폭주 위험 → 운영자가 의도적으로만 삭제.
- Cloudflare A 레코드 TTL `Auto` 는 보통 300초. EIP 변경 시 약 5분 내 반영 (TTL 을 1분으로 명시 가능). Proxy status 가 DNS only 가 아니면 `dig` 가 EIP 대신 Cloudflare anycast IP 를 반환하므로 항상 사전 확인.
- Caddy admin API (`localhost:2019`) 는 컨테이너 안에서만. 외부 노출 X — 보안 측면 자동 안전.
- Caddyfile 만 수정 후 `docker compose restart caddy` 또는 `docker compose exec caddy caddy reload --config /etc/caddy/Caddyfile` (zero-downtime reload).
- 본 plan 의 manual-hard 검증은 [`quest-onsite-integration-verification`](./2026-05-11-namae1128-quest-onsite-integration-verification.md) plan 시나리오와 같은 cycle 안에서 합쳐 검증 가능.
- Cloudflare API token 은 본 plan 에서는 사용하지 않는다 (HTTP-01 challenge + 수동 A 레코드 1개). 향후 DNS-01 로 전환하거나 ddns 자동화가 필요해지면 zone-scoped DNS Edit token 1개를 발급하는 후속 plan 으로 분리.

## Handoff

<!-- /spec-build 가 plan 완료 후 채움. -->
