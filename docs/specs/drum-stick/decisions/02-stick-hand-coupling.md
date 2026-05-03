# 02 — Anchor 부착 상태에서 Stick↔손 결합 방식

**Linked Spec:** [`../specs/01-anchor-auto-attach-detach.md`](../specs/01-anchor-auto-attach-detach.md)
**Date:** 2026-05-02
**Status:** Resolved

## 결정

Stick에 신규 컴포넌트를 부착해 다음 동작을 구현한다.

1. Stick이 spawn될 때 그 컴포넌트가 **Ghost Hand를 그대로 따라간다**. Physics Hand가 Ghost를 추종하는 패턴([`PhysicsHandGhostFollower.cs`](../../../../Assets/Hands/Scripts/PhysicsHandGhostFollower.cs))과 동일하게, stick 자체가 ghost wrist transform을 source로 frame 단위 추종한다.
2. Stick prefab 자체에 **stick-잡기 포즈로 fix된 hand prefab**(이하 "stick-hand")이 child로 들어 있다.
3. Stick이 spawn된 뒤 `PlayHand`는 그 stick-hand의 pose를 source로 [`PlayHandPoseDriver.PushSourceOverride`](../../../../Assets/Hands/Scripts/PlayHandPoseDriver.cs#L52)에 전달받아 **stick-hand의 pose에 완전히 고정**된다. 즉 PlayHand의 grip override 우선순위 슬롯을 stick-hand가 점유한다.
4. Stick에 붙어 있는 stick-hand 자체는 **렌더링되지 않는다**. Physics Hand가 Play 모드에서 보이지 않는 것과 같은 패턴([`HidePhysicsHandInPlayMode.cs`](../../../../Assets/Hands/Scripts/HidePhysicsHandInPlayMode.cs))을 재사용한다.
5. Stick의 기존 `XRGrabInteractable` 경로는 anchor 부착 상태에서는 사용하지 않는다 (사용자의 grip/trigger 입력이 stick을 떼어낼 수 없어야 한다는 sub-spec 요구).

채택하지 않은 안:

- "XRGrabInteractable + 강제 select"는 XRI 강제 select API 표면이 미묘하고 grip/trigger와 select 입력 분리가 복잡하다.
- "Reparent + Rigidbody disable"는 GripPoseProvider 우회로 새 코드 경로가 추가되며, 사용자가 의도한 "Physics Hand 같은" 패턴이 아니다.

## Why

- 이미 프로젝트에는 Ghost/Physics/Play의 3-hand 구조와 [`PlayHandPoseDriver`](../../../../Assets/Hands/Scripts/PlayHandPoseDriver.cs)의 priority 체인 `grip override > physics > ghost fallback`이 박제돼 있다. Stick이 *하나의 source*로 PlayHand를 점유하는 모델은 이 체인의 grip override 슬롯과 정확히 같은 추상화다.
- Ghost Hand는 컨트롤러/핸드 트래킹 입력을 그대로 받는 "사용자 의도" 좌표다. Stick이 Ghost를 따라가면 컨트롤러 입력 → ghost → stick → playhand 체인이 끝까지 동일 좌표로 이어져, 사용자가 손을 움직였을 때 stick과 PlayHand가 한 덩어리로 움직인다.
- Stick에 hand prefab을 미리 붙여 두면 "어떤 pose로 잡혔는가"가 stick prefab 자체의 직렬화된 사실이 된다. plan-implementer가 anchor 도착 시점에 별도 pose 결정 로직을 갖지 않아도 된다.
- Stick-hand의 렌더링을 끄는 것은 Physics Hand 패턴을 그대로 답습하므로 신규 코드 경로가 거의 없다.

## How to apply

- plan에서 stick prefab에 다음을 신설한다.
  - 신규 컴포넌트(예: `AttachedStickGhostFollower` — 이름은 plan에서 확정): Ghost wrist transform을 source로 stick의 transform을 매 frame 추종.
  - child로 stick-hand prefab(좌/우 한 쌍 또는 stick variant별 1개): stick-잡기 pose로 고정된 hand. 렌더링 OFF (Physics Hand의 hide 패턴 재사용).
- PlayHand의 source override는 anchor 부착 시점에 한 번 `PushSourceOverride(stickHandRoot, stickHandWristRoot)`로 push, detach 시 `PopSourceOverride`로 pop한다. push 호출 주체는 sub-spec 01의 anchor-자체-컴포넌트(decision 01)와 stick 신규 컴포넌트가 협조해 결정한다 — 구체 책임 분배는 plan에서 확정.
- Stick의 기존 `XRGrabInteractable`은 prefab 직렬화에서 enabled=false로 두거나 anchor 부착 상태에서만 일시 비활성화한다. 어느 쪽이 sub-spec "grip/trigger 입력으로 떼어낼 수 없다"를 더 단순히 만드는지는 plan에서 점검.
- 본 결정은 sub-spec `01-anchor-auto-attach-detach.md`의 "동시 attach / 손 포즈 유지 / grip·trigger로 떼어지지 않음" Behavior를 모두 만족시키는 단일 메커니즘이다.
