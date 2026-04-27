# Locomotion·Snap Turn 중 Physics Hand 일시 비활성화

**Linked Spec:** [`hand-physics-interaction.md`](../specs/hand-physics-interaction.md)
**Status:** `Ready`

## Goal

플레이어가 이동·스냅 턴을 시작하면 Physics Hand를 즉시 비활성화해 Play Hand가 Ghost를 따라가도록 만들고, 이동·턴이 끝나는 시점에 Physics Hand를 새 위치(=현재 ghost wrist 위치)로 텔레포트시켜 즉시 복귀시킨다. 이동·턴 중·종료 직후 어색한 늘어남이나 1프레임 jump가 시각적으로 보이지 않아야 한다.

## Context

Hands 시스템 구조와 Physics Hand 비-Kinematic 전환 배경은 parent root-spec(`docs/specs/hands/_index.md`)과 sub-spec(`hand-physics-interaction.md`) 참고. 본 plan은 spec의 5가지 What 중 "이동/스냅 턴 중 Physics Hand 일시 비활성화" 한 가지를 담당.

전제(plan 006 완료):
- Physics Hand prefab은 비-Kinematic Rigidbody. velocity 기반으로 ghost wrist를 추적.
- `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs`에 `TeleportToSourceImmediately()` 공개 메서드가 있고, 호출 시 Rigidbody pose를 ghost 위치로 즉시 동기화하고 다음 1 프레임 velocity 추적을 건너뛴다.
- `Assets/Hands/Scripts/PlayHandFollowSwitcher.cs`가 PhysicsHand.activeInHierarchy를 보고 source를 Physics ↔ Ghost로 자동 전환한다. 즉 Physics Hand를 SetActive(false)만 해도 Play Hand는 자동으로 Ghost를 따라간다.
- Play Hand의 source 전환은 onBeforeRender hook까지 동일 프레임에 적용된다.

해결해야 할 문제:
- Physics Hand가 비-Kinematic 상태에서 Locomotion(스냅 턴, 연속 이동, 텔레포트)으로 XR Origin 자체가 워프하면 Physics Hand가 큰 velocity로 추적하려고 폭주하거나, 막혀 있던 외부 콜라이더에 끼인 채로 따라가 어색한 모양이 나온다.
- Physics Hand가 활성화된 상태로 워프되면 1프레임 jump가 시각적으로 두드러진다 → 이동·턴 중에는 Physics를 끄고 Ghost로 보여주다, 종료 직후 Physics를 새 위치에서 깨끗하게 복귀.

XR Interaction Toolkit 3.x의 LocomotionMediator가 모든 Locomotion provider를 통합해 `locomotionStarted`/`locomotionEnded` 이벤트를 발생시킨다. SampleScene의 XR Origin에 LocomotionMediator가 붙어 있을 가능성이 높지만, 작업 시작 시 직접 확인 필요. 현재 코드 베이스(`Assets/`) 검색에서는 LocomotionProvider/SnapTurnProvider 사용자 정의 스크립트가 없으므로 패키지 기본 컴포넌트에 의존한다고 가정.

spec Out of Scope에 "텔레포트, 부드러운 이동 등 다른 XR locomotion 방식에 대한 별도 처리"가 있어 모든 locomotion에 동일하게 반응하면 된다(분기 정책 없음).

## Approach

1. **PhysicsHandPauseController 신규 컴포넌트**
   - 위치: `Assets/Hands/Scripts/PhysicsHandPauseController.cs`. SampleScene에 단일 인스턴스로 존재(XR Origin 또는 Hands rig 어디든 가능). MonoBehaviour.
   - 직렬화 필드: `LocomotionMediator locomotionMediator`(주 트리거), `LocomotionProvider[] fallbackProviders`(LocomotionMediator가 null일 때 직접 구독), `GameObject leftPhysicsHand`, `GameObject rightPhysicsHand`, `PhysicsHandGhostFollower leftFollower`, `PhysicsHandGhostFollower rightFollower`.
   - OnEnable에서 LocomotionMediator의 `locomotionStarted`/`locomotionEnded`(또는 Mediator의 실제 이벤트 시그니처에 맞춰)를 구독. 없으면 fallbackProviders 각각의 `beginLocomotion`/`endLocomotion`(혹은 3.x의 동등 이벤트)에 구독.
   - OnDisable에서 모두 해제.

2. **이동·턴 시작 처리**
   - 이벤트 핸들러: `OnLocomotionStarted` → `leftPhysicsHand.SetActive(false)`, `rightPhysicsHand.SetActive(false)`. PlayHandFollowSwitcher가 자동으로 Ghost로 source 전환.
   - 중첩 호출 안전성: 이미 비활성 상태면 no-op. counter로 중첩 카운트도 가능하지만 단순 활성/비활성으로 충분하다고 본다(LocomotionMediator는 한 번에 하나의 locomotion만 활성).

3. **이동·턴 종료 처리**
   - 이벤트 핸들러: `OnLocomotionEnded` → 각 손에 대해 1) `follower.TeleportToSourceImmediately()` 호출(Rigidbody pose를 ghost wrist 위치로 동기화 + 다음 velocity 추적 1프레임 skip), 2) `physicsHand.SetActive(true)`.
   - 활성화 순서: TeleportToSourceImmediately가 SetActive(false) 상태에서 호출되면 컴포넌트가 disable이라 동작하지 않을 수 있다 → 순서를 SetActive(true) 먼저, 같은 프레임에 TeleportToSourceImmediately 호출로 진행. 활성화 직후 PhysicsHandGhostFollower의 OnEnable에서 `m_TeleportOnNextSync = true` 자동 세팅하는 형태로 PhysicsHandGhostFollower 쪽에서 흡수하는 것이 더 안전 — plan 006의 deliverable에 이미 활성화 직후 1프레임 텔레포트 동기화 로직이 들어 있으므로 본 plan에서는 추가 호출 없이 SetActive(true)만 호출해도 된다. 단 활성화 직후 1프레임 동안 PhysicsHandGhostFollower의 첫 FixedUpdate가 아직 실행되지 않았는데 다른 코드가 Rigidbody에 접근하는 경우를 대비해, SetActive(true) 직후 명시적으로 `Physics.SyncTransforms()` 한 번 호출.

4. **plan 006의 PlayHandFollowSwitcher와 통합 검증**
   - PhysicsHand.activeInHierarchy가 false → Switcher가 Ghost로 source 전환 (자동).
   - PhysicsHand.activeInHierarchy가 true → Switcher가 Physics로 source 복귀 (자동).
   - 추가 코드 없음. 단 Switcher가 매 Update에서 활성 상태를 polling하는 구조라면 SetActive(true) 후 1 프레임 안에 source 전환이 일어나도록 Update 시점이 PhysicsHandPauseController.OnLocomotionEnded 이후인지 확인.

5. **검증·튜닝**
   - 헤드셋에서 스냅 턴 / 연속 이동 / (있다면) 텔레포트 모두 시도.
   - 이동 중 Play Hand가 Ghost를 따라가는지 (자세가 늘어나거나 어긋나지 않는지).
   - 종료 직후 Physics Hand가 새 위치에서 즉시 정상 추적으로 복귀하는지 (튕김·jitter 없음).
   - 콘솔에 NullReference, 매 프레임 경고 없음.
   - 1프레임 jump가 보이면 PhysicsHandGhostFollower의 활성화 직후 N프레임(예: 2~3) 동안 velocity 클램프를 더 강하게 적용하는 옵션 추가 검토.

## Deliverables

- `Assets/Hands/Scripts/PhysicsHandPauseController.cs` — 신규. Locomotion 이벤트 구독 + Physics Hand 활성/비활성 토글
- `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` — (필요 시) OnEnable에서 다음 1프레임 텔레포트 동기화 자동 세팅 보강
- `Assets/Scenes/SampleScene.unity` — XR Origin(또는 Hands rig) 아래에 PhysicsHandPauseController 인스턴스 추가, LocomotionMediator/Physics Hand 참조 배선

## Acceptance Criteria

- [ ] 헤드셋에서 스냅 턴 시 Physics Hand가 비활성화되고 Play Hand가 Ghost를 따라가, 새 회전 위치에서 어색한 늘어남·꺾임 없이 보인다.
- [ ] 헤드셋에서 연속 이동 중 Play Hand가 Ghost를 따라가 사용자 손과 일치한 위치로 이동한다 (Physics Hand가 외부 콜라이더에 끼여 늘어나는 현상 없음).
- [ ] 이동·턴 종료 직후(다음 프레임) Physics Hand가 ghost wrist 위치로 텔레포트되어 활성화되고, 외부 콜라이더와의 충돌 반응이 plan 006과 동일하게 다시 동작한다.
- [ ] 이동·턴 종료 직후 Physics Hand가 큰 velocity로 튕겨나가지 않는다 (콘솔 경고 없음, 시각적 jitter 없음).
- [ ] LocomotionMediator가 SampleScene에 없으면 fallbackProviders 경로로 동일 동작이 보장된다 (Editor에서 일부러 mediator 참조를 비워보고 검증).
- [ ] 이동·턴이 중단·재시작되는 빠른 입력에서도 Physics Hand 활성 상태가 일관성 있게 토글된다 (붙어 있는 Physics Hand가 영영 비활성으로 남거나 하는 상태 없음).

## Out of Scope

- Locomotion 시스템 자체의 신규 구현·튜닝.
- 텔레포트 / 부드러운 이동 / 스냅 턴 각각을 별개 정책으로 다르게 처리하는 것.
- 핸드 트래킹 인식 실패·복구 시 레이어 동기화.
- Drumstick 등 도구를 잡은 상태에서 이동하면 도구는 어떻게 따라오는지(잡힌 도구는 XRGrabInteractable이 알아서 따라온다고 가정).

## Notes

- spec Open Question 1 결정안: locomotion 트리거 출처는 LocomotionMediator의 통합 이벤트를 우선 사용하고, 없으면 각 LocomotionProvider의 begin/end 이벤트로 fallback. 본 plan 구현 시 SampleScene 상태 확인 후 어느 경로를 실제로 쓰는지 코드/씬에 명확히 남길 것.
- spec Open Question 2(이동·턴 종료 후 1프레임 jump 보간 필요 여부)는 본 plan의 Acceptance Criteria 4번에서 직접 검증한다. 검증 결과에 따라 plan 종료 시 sub-spec의 Open Question을 닫고 결정 사항을 spec에 반영.
- LocomotionMediator의 정확한 이벤트 이름은 XR Interaction Toolkit 버전에 따라 다를 수 있다(`locomotionStarted`/`locomotionEnded` vs `locomotionEntered`/`locomotionExited` 등). 작업 시작 시 패키지 버전 확인 후 실제 이벤트 시그니처에 맞춰 구현.
