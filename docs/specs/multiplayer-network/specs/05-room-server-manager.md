# 룸 서버 매니저

**Parent:** [`_index.md`](../_index.md)

## Why

[`03-room-session.md`](03-room-session.md)는 룸 생성과 입장 흐름을 정의하지만, 실제 room instance를 어떤 인프라 위에서 어떻게 띄우고 종료할지의 책임은 Spring 내부에서 별도 모듈로 분리되어야 한다. 그렇지 않으면 컨트롤러/서비스 전반에 ECS task, 로컬 Docker, 향후 Kubernetes pod 같은 인프라 세부사항이 퍼져 구현체 교체가 어려워지고 장애 복구 정책도 흩어진다.

## What

`RoomServerManager`는 Spring 서버 내부 모듈이며, room instance 생명주기와 runtime provider 호출을 관리한다. AWS dev/prototype 환경에서는 EC2에 상주한 Spring 서버 내부에서 동작하며, AWS API를 통해 ECS Fargate의 Unity Headless Dedicated Server task를 실행·조회·중지한다. 향후 대규모 트래픽이나 복잡한 스케줄링 요구가 생기면 `RoomServerManager` 내부의 runtime provider 구현을 Kubernetes 기반으로 교체할 수 있다. 로컬 개발 환경에는 같은 상태 계약을 따르는 Docker 기반 대체 구현을 둘 수 있다.

- room create 요청을 받아 capacity pool 위에 single-use Unity Headless Dedicated Server instance를 프로비저닝한다.
- room instance의 공통 상태 머신을 `PROVISIONING → SERVER_STARTING → READY → ACTIVE → UNHEALTHY → TERMINATING → TERMINATED`로 운영한다.
- 실패 경로는 `PROVISIONING → FAILED`, `SERVER_STARTING → FAILED`로 다룬다.
- `READY`는 인프라 런타임이 올라온 것만이 아니라, Unity Dedicated Server가 default 씬 로드와 Photon Fusion 세션 등록을 마치고 app-level ready callback을 보낸 상태를 뜻한다.
- `ACTIVE`는 admission이 열린 뒤 실제 join ticket이 소모되어 룸 세션이 진행 중인 상태를 뜻한다.
- `UNHEALTHY`는 `READY` 또는 `ACTIVE` 이후 heartbeat 상실, 런타임 비정상, 세션 진행 불가 등으로 더 이상 정상 admission/진행을 보장하지 못하는 상태를 뜻한다.
- `TERMINATING`은 `RoomServerManager`가 room instance 정리 절차를 수행 중인 상태이며, 정리가 끝나면 `TERMINATED`가 된다.
- heartbeat과 ready callback은 모두 app-level 신호로 취급한다. 단순 컨테이너 프로세스 생존 여부만으로 룸 준비 완료나 정상 상태를 판단하지 않는다.
- 룸 종료 후 room instance는 재사용하지 않고 종료하며, 점유 자원만 capacity pool로 환원한다.
- `RoomServerManager`는 인프라 식별자(ECS task ARN, Docker container ID, 향후 Pod UID 등)를 내부적으로 관리하되, Spring의 다른 모듈에는 room 상태와 입장 가능 여부 중심의 추상화된 결과만 노출한다.
- active room의 런타임 진실원은 Spring 내부 `RoomServerManager`가 가진다. Spring/DB에 있는 room list 캐시나 projection은 client-facing 읽기 모델일 뿐이며, authoritative source가 아니다.
- `RoomServerManager`는 Spring이 룸 목록 projection을 만들 수 있도록 room lifecycle query 또는 event stream을 제공한다. Spring은 이를 통해 유저에게 보여줄 room list를 제공하되, stale projection만으로 active room을 광고해서는 안 된다.
- `RoomServerManager`는 자신의 내부 상태와 실제 인프라 런타임(ECS task, Docker container, 향후 Kubernetes pod)을 주기적으로 대조하는 liveness reconciliation 책임을 가진다. 이벤트 유실, callback 누락, 프로세스 강제 종료가 발생해도 ghost room과 orphan instance를 정리할 수 있어야 한다.
- `RoomServerManager`는 capacity와 lifecycle에 대한 telemetry를 수집한다. 최소한 active room 수, provisioning/ready latency, unhealthy termination 수, capacity 사용량, admission rejection 사유를 관측 가능하게 해야 한다.

## Behavior

- **Given** Spring이 내부 `RoomServerManager`에 새 룸 생성을 요청할 때
  **When** `RoomServerManager`가 capacity를 확보하면
  **Then** room instance는 `PROVISIONING → SERVER_STARTING`으로 전이되고 Unity Headless Dedicated Server 기동 절차가 시작된다.

- **Given** room instance가 default 씬 로드와 Photon Fusion 세션 등록을 마쳤을 때
  **When** app-level ready callback을 보내면
  **Then** `RoomServerManager`는 해당 instance를 `READY`로 표시하고 Spring은 그 이후에만 룸 목록 공개와 join ticket 발급을 시작한다.

- **Given** `READY` 상태의 룸이 있을 때
  **When** 첫 join ticket이 유효하게 소모되면
  **Then** room instance는 `ACTIVE`로 전이되고 heartbeat 감시가 시작된다.

- **Given** Spring이 유저에게 room list를 제공해야 할 때
  **When** active room 집합을 조회하면
  **Then** Spring은 내부 `RoomServerManager`의 authoritative runtime state 또는 그로부터 갱신된 freshness 보장 projection을 사용한다.

- **Given** `READY` 또는 `ACTIVE` 상태의 room instance가
  **When** heartbeat을 임계 시간 안에 보내지 못하거나 런타임이 비정상 상태로 판정되면
  **Then** instance는 `UNHEALTHY`로 전이되고 즉시 admission이 중지되며 `TERMINATING → TERMINATED`로 정리된다.

- **Given** 내부 상태에는 room이 남아 있지만 실제 ECS task 또는 container가 사라졌을 때
  **When** reconciliation 주기가 돌면
  **Then** `RoomServerManager`는 해당 room을 stale 또는 dead로 판정하고 `UNHEALTHY → TERMINATING → TERMINATED` 수순으로 정리하며 Spring projection에도 반영한다.

- **Given** room instance가 마지막 유저 퇴장 또는 명시적 종료 요청을 받았을 때
  **When** 종료 절차가 시작되면
  **Then** instance는 `TERMINATING → TERMINATED`를 거쳐 정리되고 점유 자원은 capacity pool로 환원된다.

- **Given** provision 단계 또는 server start 단계에서 오류가 발생했을 때
  **When** room instance가 정상 기동에 실패하면
  **Then** 상태는 `FAILED`가 되고 백엔드는 룸 생성 실패를 클라이언트에 전달한다.

## Out of Scope

- 클라이언트 향 서비스 API 본체 (룸 생성/목록/입장 등 — 백엔드 책임)
- 룸 서버 자체의 게임 로직·세션 동기화·판정 (룸 서버 책임)
- 유저별 룸 상태 영속화·snapshot 직렬화·복원 (default 씬 모델이므로 본 피처 전체 Out of Scope)
- 빌드 산출물 생성과 아티팩트 배포 ([`06-build-targets.md`](06-build-targets.md) 책임)
- ECS/Kubernetes의 세부 리소스 정의 파일, Helm chart, Terraform 등 구체 IaC 산출물
- 다중 리전 스케줄링과 글로벌 매치메이킹

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-07 | AWS dev 토폴로지 (EC2 Spring + MariaDB, ECS Fargate room-server) | `Ready` | [2026-05-07-namae1128-aws-dev-topology-ec2-fargate.md](../plans/2026-05-07-namae1128-aws-dev-topology-ec2-fargate.md) |
| 2026-05-16 | AWS dev HTTPS 종단 (Caddy reverse proxy + Let's Encrypt + DuckDNS) | `Ready` | [2026-05-16-namae1128-aws-dev-https-caddy.md](../plans/2026-05-16-namae1128-aws-dev-https-caddy.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

- [ ] ready callback / heartbeat 전송 채널 (HTTP / gRPC / queue 등)
- [ ] heartbeat 주기와 `UNHEALTHY` 판정 임계
- [ ] capacity 선택 정책 (binpack / spread / 고정 우선순위)
