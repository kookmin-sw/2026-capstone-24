<!--
06-build-targets 의 clientCompatibilityVersion 호환성 검증을 어디서 강제할지 박제한다.
이 결정은 03-room-session 의 admission 모델과 06-build-targets 의 What 섹션이 함께 참조한다.
-->

# clientCompatibilityVersion 호환성 검증 시점 = 런타임 admission

**Sub-Spec:** [`06-build-targets.md`](../specs/06-build-targets.md)
**Status:** `Accepted`
**Date:** 2026-05-17

## Context

[`06-build-targets.md`](../specs/06-build-targets.md) 는 클라이언트와 room server 가 `clientCompatibilityVersion` 을 공유해야 한다고 명시한다. 그 값은 prefab 식별자, NetworkBehaviour/RPC 시그니처, 룸 입장 계약 등 네트워크 상호운용성을 대표한다. 호환되지 않는 클라이언트와 서버가 같은 룸 세션에 합류하면 RPC 미스매치·prefab GUID 충돌·NetworkObject deserialization 실패 등이 즉시 또는 잠재적으로 발생한다.

이 호환성을 어디서 강제할지 결정해야 한다 — 빌드 단계 / CI / 런타임 가드 셋 중 하나로.

## Options Considered

- **build-time-hash** — Editor 빌드 스크립트가 클라이언트·서버 양쪽 산출물의 prefab GUID + NetworkBehaviour signature 해시를 비교해 mismatch 시 빌드 실패. 산출 단계에서 차단되므로 가장 이른 시점이지만, 양 빌드를 동일 commit 에서 빌드해야 강제력이 있고, 두 빌드를 다른 시점·다른 머신에서 만드는 워크플로우(local dev vs CI vs 운영 deploy)와 충돌한다.
- **ci-comparison-job** — 두 빌드 산출물을 모두 만들고 비교하는 별도 검증 잡을 CI 파이프라인에 둠. workflow 는 명확하지만, 현재 GitHub Actions 등 CI 자동 빌드가 구성되지 않은 상태이고 ([`2026-05-01-namae1128-dedicated-server-build-pipeline.md`](../../_archive/multiplayer-network/plans/2026-05-01-namae1128-dedicated-server-build-pipeline.md) Out of Scope), 우선 인프라가 없다. 또한 dev 환경에서 local 빌드만으로 테스트할 때 가드가 부재.
- **runtime-admission-gate** — room server 가 admission 단계에서 클라이언트의 `clientCompatibilityVersion` 을 자신의 값과 비교하고, mismatch 시 입장을 거부한다. 이미 [`03-room-session.md`](../specs/03-room-session.md) 의 `roomRuntimeVersion` ready callback / join ticket admission 모델과 같은 게이트를 공유한다. 빌드 단계의 가드는 아니지만 호환 안 되는 페어가 같은 세션에 합류하는 것은 100% 차단된다.

## Decision

**runtime-admission-gate** — `clientCompatibilityVersion` 호환성 검증은 room server 의 admission 단계에서 강제한다. join ticket 검증 직후, 룸 세션 합류 직전에 클라이언트가 보고한 `clientCompatibilityVersion` 을 room server 의 값과 비교하고, 호환되지 않으면 admission 을 거부하고 거부 사유를 클라이언트에 전달한다.

호환성 규칙은 1차 단계에서 **strict-equal** (문자열 동일성) 을 채택한다. semantic versioning 의 minor / patch 호환 같은 더 느슨한 규칙은 도입하지 않는다.

선택 사유:

- [`03-room-session.md`](../specs/03-room-session.md) 의 ready callback / join ticket / admission 모델과 자연스럽게 결합. 새 검증 인프라가 필요 없다.
- 모든 환경(local dev, AWS dev, prod)에 동일 가드. CI 의존 없음.
- 빌드 단계 / CI 가드는 미래에 보강 가드로 추가 가능 (option-additive). 본 결정은 그것들을 배제하지 않는다.

## Consequences

- room server 는 ready callback 에 `clientCompatibilityVersion` 값을 포함해 `RoomServerManager` 에 보고한다.
- 클라이언트는 룸 합류 요청 또는 합류 직후 첫 RPC 에서 자신의 `clientCompatibilityVersion` 을 room server 에 전달한다 (전달 채널은 plan 에서 박제).
- room server 는 mismatch 발생 시 admission 을 거부하고 거부 사유(`COMPATIBILITY_MISMATCH`) 를 클라이언트에 전송한다. `RoomServerManager` 는 이 거부를 `UNHEALTHY` 시그널로 취급하지 않는다 (정상 거부 흐름).
- `clientCompatibilityVersion` 값의 source 는 1차 컨벤션으로 Unity Player Settings 의 Version 문자열(`Application.version`)을 그대로 사용한다 ([`06-build-targets.md`](../specs/06-build-targets.md) What 섹션 박제).
- 빌드 단계 / CI 단계 가드는 향후 인프라가 갖춰지면 보강으로 추가. 본 결정은 그것들을 막지 않는다.
- 본 결정이 다시 평가되어야 하는 trigger: (a) `Application.version` 만으로 식별이 불충분한 변경(예: NetworkBehaviour signature 만 바뀌고 버전 문자열은 그대로) 이 반복적으로 누락 검출되는 사례 발생, (b) CI 자동 빌드 인프라 도입.
