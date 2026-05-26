# 멀티플레이어 룸 세션

**Parent:** [`_index.md`](../_index.md)

## Why

여러 유저가 같은 VR 공간을 공유하려면 유저들을 하나의 세션으로 묶는 룸 개념이 필요하다. 룸이 없으면 각자 독립된 공간에서만 존재하게 되어 협주가 불가능하다.

## What

Photon Fusion을 통해 룸을 생성하거나 기존 룸에 입장·퇴장할 수 있는 세션 관리 기능을 제공한다. 여러 유저가 같은 룸에 동시 접속해 공유 공간을 형성한다. 룸 권위는 전용 서버 인스턴스가 가지며, 클라이언트는 백엔드를 경유해 룸을 생성하고 Photon Cloud가 매칭한 룸 세션으로 직접 연결해 입장한다. 룸 생성 시 비밀번호 설정 여부를 선택할 수 있으며, 모든 룸은 룸 목록에 노출되고 비밀번호가 설정된 룸은 잠금 상태로 표시된다.

모든 룸은 동일한 default 씬을 사용한다. 씬에는 악기·오브젝트가 미리 배치되어 있고, 클라이언트는 그 단일 씬 안에서만 상호작용한다. 룸 내부에서 오브젝트를 추가·이동·삭제하는 흐름은 없으며, 유저별 룸 상태(악기 배치·오브젝트 설정 등)는 영속화 대상이 아니다. default 씬의 실제 Unity 씬 매핑은 [`../decisions/01-default-scene.md`](../decisions/01-default-scene.md) 가 단일 source of truth.

룸 서버 생명주기는 Spring 서버 내부 모듈인 `RoomServerManager`가 관리한다. Spring 백엔드는 room instance를 직접 실행하지 않고, 요청 유저의 `playerId`를 식별한 뒤 룸 이름, 비밀번호, 정원, 필요 런타임 버전을 묶어 같은 프로세스 안의 `RoomServerManager`를 호출해 프로비저닝을 시작한다. AWS dev/prototype 환경에서는 Spring Server와 MariaDB가 EC2에 상주하고, `RoomServerManager`는 AWS API를 통해 ECS Fargate의 Unity Headless Dedicated Server task를 실행·조회·종료한다. 향후 대규모 트래픽이나 복잡한 스케줄링 요구가 생기면 `RoomServerManager` 내부의 runtime provider 구현을 Kubernetes 기반으로 교체할 수 있다. 로컬 Docker 개발 환경도 같은 계약과 상태 흐름을 따르는 대체 구현을 둘 수 있다.

룸 생성 상태 흐름은 인프라 구현체와 무관하게 `PROVISIONING → SERVER_STARTING → READY → ACTIVE → UNHEALTHY → TERMINATING → TERMINATED`를 공통으로 사용한다. 실패 경로는 `PROVISIONING → FAILED`, `SERVER_STARTING → FAILED`다. `READY`는 컨테이너가 단순 실행 중이라는 의미가 아니라, Unity Dedicated Server가 default 씬을 로드하고 Photon Fusion 세션을 열어 등록을 마친 뒤 app-level ready callback을 `RoomServerManager`에 보낸 상태를 뜻한다. 이 callback이 오기 전까지 룸은 목록에 노출되거나 입장 가능 상태가 되지 않는다. `ACTIVE`는 admission이 열리고 첫 join ticket이 소모되어 실제 룸 세션이 진행 중인 상태다. `UNHEALTHY`는 ready 이후 heartbeat이 정해진 임계 안에 도달하지 않거나 런타임이 정상 입장 처리를 보장하지 못한다고 판단된 상태이며, 즉시 admission을 중지하고 `TERMINATING → TERMINATED`로 수습한다.

비밀번호가 설정된 방을 포함한 모든 입장 요청은 백엔드 승인 단계를 거친다. 백엔드는 `JWT 검증 → 비밀번호 검증 → 정원/상태 검증`을 완료한 뒤, 짧은 TTL의 1회용 `join ticket` 또는 `reservation`을 발급한다. 클라이언트는 발급된 ticket과 룸 식별자(예: Photon session name)를 사용해 Photon Cloud가 라우팅하는 동일한 Fusion 세션에 합류하고, 룸 서버는 ticket을 서버 측에서 검증·1회 소모한 뒤에만 입장을 허용한다. 유효한 ticket 없이 룸 세션에 직접 연결하려는 시도는 거부한다.

룸 서버는 single-use room instance로 취급한다. 마지막 유저가 퇴장하거나 room 종료가 선언되면 해당 인스턴스는 재사용을 위해 리셋되지 않고 종료되며, 점유하던 자원만 `RoomServerManager`가 관리하는 capacity pool(ECS task 슬롯, Docker host, 향후 K8s cluster capacity 등)로 환원된다. 단, **persistent 플래그가 설정된 시연/데모 전용 룸은 본 정책의 예외**다. persistent 룸은 0명 상태에서도 종료되지 않으며 heartbeat reconciliation 의 timeout 청소 대상에서도 제외된다. 동시에 살아 있는 persistent 룸은 1개로 제한되며, 생성 권한은 운영자에게만 부여된다 (일반 룸 생성 API 의 옵션으로 노출되지 않음). 일반 룸 목록 노출·입장 admission·정원·비밀번호 정책은 일반 룸과 동일. 상세 정책은 [`decisions/05-persistent-demo-room-policy.md`](../decisions/05-persistent-demo-room-policy.md) 가 단일 진실원.

유저가 보는 룸 목록의 클라이언트 진입점은 Spring 백엔드이지만, active room의 런타임 진실원은 Spring 내부 `RoomServerManager`가 가진다. Spring은 룸 목록 API를 제공하는 client-facing facade 역할을 맡고, `READY` 또는 `ACTIVE`이며 admission이 열려 있다고 `RoomServerManager`가 확인한 룸만 목록에 노출한다. Spring이 별도 캐시나 projection을 유지하더라도 이는 파생된 읽기 모델일 뿐 authoritative source가 아니다.

세션 내부 네트워크 식별자인 `PlayerRef`는 룸 실행 중에만 유효한 전송 식별자로 취급한다. 애플리케이션 레벨의 영구 유저 식별은 백엔드 `playerId`를 기준으로 유지하고, Photon Custom Auth의 `UserId`에도 이 값을 전달한다. `metaAccountId`는 Meta 같은 로그인 제공자 식별자, `nickname`은 표시용 값으로만 다룬다.

## Behavior

- **Given** 로그인된 유저가
  **When** 새 룸 생성을 요청하면
  **Then** Spring은 룸 이름·비밀번호·정원·필요 런타임 버전을 내부 `RoomServerManager`에 전달해 default 씬 기반의 room instance 프로비저닝을 시작하고, ready callback이 올 때까지 룸은 입장 불가 상태로 유지된다.

- **Given** `RoomServerManager`가 room instance를 `READY`로 보고했을 때
  **When** 백엔드가 룸 메타데이터를 공개하면
  **Then** 해당 룸은 목록에 노출되고 입장 승인 대상이 된다.

- **Given** Spring이 룸 목록 API를 제공할 때
  **When** 룸 목록을 조회하면
  **Then** Spring은 `RoomServerManager`가 현재 admission 가능하다고 확인한 active room 집합만 반환한다.

- **Given** 로그인된 유저가
  **When** 기존 룸에 입장을 요청하면
  **Then** 백엔드는 JWT·비밀번호·정원·상태를 검증한 뒤 짧은 TTL의 1회용 join ticket을 발급하고, 룸 서버가 그 ticket을 검증·소모한 경우에만 같은 룸 세션에 합류시킨다.

- **Given** 유저가 유효한 join ticket 없이 룸 세션에 직접 연결하려고 할 때
  **When** 룸 서버가 admission을 검사하면
  **Then** 입장이 거부된다.

- **Given** 룸에 접속 중인 유저가
  **When** 퇴장하면
  **Then** 해당 유저는 세션에서 제거되고 다른 유저에게 반영된다.

- **Given** 룸의 마지막 유저가
  **When** 퇴장하면
  **Then** 룸은 종료 절차에 들어가고 room instance는 `TERMINATING → TERMINATED`를 거쳐 정리되며 점유 자원은 capacity pool로 환원된다.

- **Given** persistent 룸의 마지막 유저가
  **When** 퇴장하면
  **Then** 룸은 종료되지 않고 0명 상태로 계속 유지되며 heartbeat reconciliation 의 timeout 청소 대상에서도 제외된다.

- **Given** 이미 살아 있는 persistent 룸이 있을 때
  **When** 두 번째 persistent 룸 생성을 시도하면
  **Then** 거절되고 충돌 사유가 전달된다.

- **Given** 룸에 이미 8명이 접속해 있을 때
  **When** 추가 유저가 입장을 요청하면
  **Then** 입장이 거부되고 거부 사유가 클라이언트에 전달된다.

- **Given** 로그인된 유저가 룸 생성 시 비밀번호 옵션을 설정했을 때
  **When** 룸 생성을 요청하면
  **Then** 비밀번호가 설정된 룸이 만들어지고 룸 목록에 잠금 상태로 표시된다.

- **Given** 비밀번호 룸에 입장하려는 유저가
  **When** 올바른 비밀번호를 제시하면
  **Then** 룸에 합류한다.

- **Given** 비밀번호 룸에 입장하려는 유저가
  **When** 잘못된 비밀번호를 제시하면
  **Then** 입장이 거부되고 비밀번호 불일치 사유가 클라이언트에 전달된다.

- **Given** `READY` 또는 `ACTIVE` 상태의 룸 서버가
  **When** heartbeat을 제때 보내지 못하거나 런타임이 비정상 상태로 판단되면
  **Then** 룸은 `UNHEALTHY`로 전이되고 즉시 입장이 중지된 뒤 `TERMINATING → TERMINATED`로 수습된다.

## Out of Scope

- 아바타·손 동작 실시간 동기화
- 악기 연주 동기화
- 음성 채팅
- 초대 링크
- 초대 전용 룸 (룸 코드 외 별도 초대 메커니즘)
- 룸 비밀번호 변경·재설정 (룸 생성 시점에만 설정 가능)
- 호스트(클라이언트) 권위 마이그레이션
- `RoomServerManager` 내부의 구현체 세부(ECS / local Docker / Kubernetes)와 와이어 프로토콜 정의 ([`05-room-server-manager.md`](05-room-server-manager.md) 책임)
- 유저별 룸 상태(악기·오브젝트 배치 등) 영속화·복원·snapshot 직렬화 (default 씬 + 기배치 오브젝트 모델이므로 본 피처 전체 Out of Scope)
- 룸 내부 오브젝트 추가·이동·삭제 흐름
- 일반 유저가 클라이언트 UI 로 persistent 룸을 생성하는 흐름 (운영자 수동 채널만 지원)
- 동시 persistent 룸 N개 운영 (1개 한정)
- persistent 룸의 콘텐츠·배치 차이 (default 씬 + 기배치 오브젝트 그대로 — 일반 룸과 동일)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-28 | Photon Fusion SDK 패키지 준비 | `Done` | [2026-04-28-namae1128-photon-fusion-import.md](../../_archive/multiplayer-network/plans/2026-04-28-namae1128-photon-fusion-import.md) |
| 2026-05-01 | 룸 세션 라이프사이클 (비밀번호 옵션 포함) | `Done` | [2026-05-01-namae1128-room-session-lifecycle.md](../../_archive/multiplayer-network/plans/2026-05-01-namae1128-room-session-lifecycle.md) |
| 2026-05-01 | Linux Dedicated Server 산출 + 단독 Dockerfile | `Done` | [2026-05-01-namae1128-dedicated-server-build-pipeline.md](../../_archive/multiplayer-network/plans/2026-05-01-namae1128-dedicated-server-build-pipeline.md) |
| 2026-05-01 | 공개 룸 목록 조회 (잠금 표시 포함) | `Done` | [2026-05-01-namae1128-room-list-query.md](../../_archive/multiplayer-network/plans/2026-05-01-namae1128-room-list-query.md) |
| 2026-05-07 | docker-compose 로컬 통합 스택 (spring + mariadb + dedicated-server) | `Done` | [2026-05-07-namae1128-docker-compose-local-stack.md](../../_archive/multiplayer-network/plans/2026-05-07-namae1128-docker-compose-local-stack.md) |
| 2026-05-08 | tools/run-stack-smoke 5시나리오 자동화 (docker-compose 기반) | `Abandoned` (2026-05-25 — superseded by `RoomAuthorityValidateJoinTests` + `quest-onsite-integration-verification`. dev 무게중심이 EC2+ECS 로 이동, 짝 산출물 `tools/run-room-lifecycle-automation.ps1` 도 같은 사이클에 삭제) | [2026-05-08-namae1128-stack-smoke-automation.md](../../_archive/multiplayer-network/plans/2026-05-08-namae1128-stack-smoke-automation.md) |
| 2026-05-09 | dedicated-server 빌드 시 Standalone OpenXR loader 임시 토글 | `Done` | [2026-05-09-namae1128-dedicated-server-build-openxr-toggle.md](../../_archive/multiplayer-network/plans/2026-05-09-namae1128-dedicated-server-build-openxr-toggle.md) |
| 2026-05-17 | Ghost room reconciliation (heartbeat 만료 + DS terminate 콜백) | `Ready` | [2026-05-17-namae1128-ghost-room-reconciliation.md](../plans/2026-05-17-namae1128-ghost-room-reconciliation.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용.

## Open Questions

- _없음_
