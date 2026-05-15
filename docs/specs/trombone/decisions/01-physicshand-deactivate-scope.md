# ARD 01 — PhysicsHand deactivate 책임 범위

**Sub-Spec:** [`../specs/01-anchor-and-grip.md`](../specs/01-anchor-and-grip.md)
**From Tech Spec:** [`../tech-specs/01-anchor-and-grip.md`](../tech-specs/01-anchor-and-grip.md) §Open Tech Decisions #1
**Date:** 2026-05-14
**Status:** Accepted

## 결정

트럼본 활성 동안 양손 `PhysicsHand` GameObject의 `SetActive(false)` / 이탈 시 `SetActive(true)` 토글은 **TromboneGripController가 단독으로 직접 수행**한다. `PlayHandPoseDriver` 본체에 PhysicsHand deactivate 책임을 끌어올리지 않는다.

## 컨텍스트

Tech Spec §Components와 §Invariants는 "트럼본 활성 동안 양손 PhysicsHand `activeInHierarchy == false`, 이탈 직후 `true`"를 강제한다. 동시에 §Boundaries는 두 가지 박제를 함께 들고 있다.

- "PlayHandPoseDriver 본체 시그니처·source priority 로직은 건드리지 않는다."
- "드럼/피아노 grip 흐름(`GripPoseProvider` 경유 `XRGrabInteractable.selectEntered` 체인)은 건드리지 않는다."

드럼 스틱 grab 흐름도 원래는 PhysicsHand deactivate가 의도였다는 신호가 있어 cross-cutting 일관성을 끌어올릴 후보였으나, 그 결정은 본 sub-spec 스코프를 초과한다.

## 선택지

| 선택 | spec What 4건 커버 | 비용 | 위험 |
|---|---|---|---|
| **TromboneGripController 한정** (선정) | 만족 | 트럼본 컴포넌트 한 곳에서 enter/exit 4-step 처리. PlayHandPoseDriver·드럼/피아노 흐름 무수정. | 드럼 스틱 grab은 deactivate 미구현 상태로 잔류 → 후속 cross-cutting ARD 필요. |
| PlayHandPoseDriver 본체로 승격 | 만족 (자동) | Driver 시그니처·내부 상태 신설. 드럼/피아노 GripPoseProvider 흐름 회귀 테스트 필요. | Tech Spec §Boundaries 2건 박제와 정면 충돌. Push/Pop 페어 깨질 시 PhysicsHand 영구 비활성 잔류. |

## 결과

- TromboneGripController는 enter 시 양손 `PhysicsHand.SetActive(false)`, exit 시 양손 `PhysicsHand.SetActive(true)`를 명시적으로 호출한다.
- `PlayHandPoseDriver.PushSourceOverride` / `PopSourceOverride`는 본 sub-spec에서 수정하지 않는다 (Tech Spec §Boundaries 유지).
- 드럼 스틱·피아노 grip 흐름은 본 ARD 범위 밖. 향후 cross-cutting 정책이 필요해지면 별도 ARD로 분리.

## Caused By

_없음._
