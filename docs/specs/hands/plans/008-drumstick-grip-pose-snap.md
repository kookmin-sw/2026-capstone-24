# Drumstick grip 즉시 스냅 (보간 제거 + 충돌 무시)

**Linked Spec:** [`hand-physics-interaction.md`](../specs/hand-physics-interaction.md)
**Status:** `Ready`

## Goal

사용자가 Drumstick을 잡는 순간 Play Hand 포즈와 Drumstick 위치가 보간 없이 즉시 grip 포즈로 스냅되도록 만든다. 또한 잡힌 Drumstick이 Physics Hand 콜라이더와 충돌해 떨리거나 튕기지 않도록 잡기 동안 충돌을 무시하고, release 시 정상 복원한다.

## Context

Hands 시스템 구조는 parent root-spec(`docs/specs/hands/_index.md`) 참고. 본 plan은 sub-spec(`hand-physics-interaction.md`)의 5가지 What 중 "Drumstick grip 즉시 스냅" 한 가지를 담당. spec에는 "보간 없음"이 명시되어 있다.

현재 코드 상태 (이미 갖춰진 부분):
- `Assets/Hands/Scripts/GripPoseProvider.cs:23-52` — XRGrabInteractable의 `selectEntered`/`selectExited` 이벤트에 반응해 PlayHandPoseDriver의 `PushSourceOverride`/`PopSourceOverride`를 호출하고, `XRGrabInteractable.attachTransform`을 grip wrist로 교체.
- `Assets/Hands/Scripts/PlayHandPoseDriver.cs:46-73` — PushSourceOverride 호출 시 동일 프레임에 ResetInitialization + TryEnsureInitialized로 source가 교체되며, onBeforeRender hook이 렌더 직전에 적용되어 시각적 1프레임 보장.
- `Assets/Hands/Editor/DrumStickSetup.cs:78-101` — drum_stick 베이스 prefab 자동 설정. 현재 `m_AttachEaseInTime = 0.15f`로 잡기 시점에 도구가 손을 향해 0.15초 보간이 들어간다. `m_MovementType = 2`(Instantaneous)지만 attachEase는 별개 보간이라 손과 도구가 0.15초 동안 어긋나 보인다.
- `Assets/Instruments/Drum/Prefabs/drum_stick.prefab`(베이스), `drum_stick_L.prefab`/`drum_stick_R.prefab`(좌·우 variant). variant는 Play Hand별 grip pose가 다르게 설정되어 있다.

해결해야 할 문제 두 가지:
1. **보간이 남아 있다.** PlayHandPoseDriver 쪽 포즈 전환은 즉시지만 XRGrabInteractable의 attachEaseInTime 0.15초 때문에 Drumstick 자체가 손에 붙는 데 시간이 걸린다 → 사용자 시점에서 손 포즈는 즉시 grip이 되었는데 도구는 0.15초 동안 손 바깥에 있다가 빨려 들어오는 어색한 장면.
2. **Physics Hand와 잡힌 Drumstick의 충돌.** plan 006으로 Physics Hand가 비-Kinematic이 되면 잡힌 drumstick의 콜라이더가 손 콜라이더 안쪽에 들어와 떨림이 발생할 수 있다.

본 plan은 plan 006/007과 독립적으로 실행 가능. 단 Physics Hand 충돌 무시 단계는 plan 006이 완료되어 비-Kinematic이 된 이후에만 의미가 있다(이전이면 해당 단계 생략).

## Approach

1. **XRGrabInteractable.attachEaseInTime을 0으로**
   - `Assets/Hands/Editor/DrumStickSetup.cs:87`의 `SetIfExists(grabSO, "m_AttachEaseInTime", 0.15f);`를 `0f`로 변경.
   - 기존에 이미 생성·디스크에 저장된 `drum_stick.prefab`의 XRGrabInteractable 컴포넌트도 직접 0으로 수정. variant 두 개는 base를 상속하므로 자동 반영되지만 override가 잡혀 있으면 먼저 해제. Unity 직렬화 자산 규칙 적용 — Unity MCP / 에디터로 수정.
   - `m_MovementType`(Instantaneous = 2), `m_TrackPosition`(true), `m_TrackRotation`(true)는 그대로 유지.

2. **GripPoseProvider Push/Pop 즉시성 검증**
   - 변경 없이 현재 구조가 충분한지 헤드셋 검증.
   - PushSourceOverride가 호출된 시점부터 onBeforeRender hook이 렌더 직전에 새 source를 적용하므로 시각적으로는 동일 프레임이어야 한다.
   - 만약 1프레임 잔여가 보이면 GripPoseProvider.OnSelectEntered 마지막에 PlayHandPoseDriver의 강제 sync 메서드 호출(예: 신규 `ForceSyncNow()`) 추가 검토. 우선 검증 후 결정.

3. **Physics Hand와 잡힌 Drumstick 충돌 무시 (plan 006 완료 시 적용)**
   - GripPoseProvider에서 selectEntered 시 잡힌 손쪽 Physics Hand의 콜라이더 전부와 drumstick 자기 콜라이더 사이 `Physics.IgnoreCollision(stickCol, handCol, true)` 호출. selectExited 시 반대로 false.
   - 어느 쪽 손인지 식별: `GripPoseProvider.ResolvePlayHandPoseDriver`가 부모 hierarchy를 거슬러 "Left"/"Right" 노드를 찾는 로직을 그대로 재사용해 sibling에서 LeftPhysicsHand/RightPhysicsHand GameObject를 찾고 `GetComponentsInChildren<Collider>()`로 콜라이더 수집.
   - 무시 대상 컬렉션을 인스턴스 변수로 저장해 selectExited 시 동일 콜라이더에 대해 false로 복원. Physics Hand가 도중에 비활성화되면 (plan 007의 locomotion pause) Collider가 disable이지만 IgnoreCollision은 enabled 상태 무관하게 유지되므로 안전.

4. **검증 (헤드셋)**
   - 정지 상태에서 grab → 손과 스틱이 같은 프레임에 grip 포즈로 일치돼 보인다.
   - 빠르게 흔들면서 grab → 보간 없이 즉시 스냅. 도구가 손 바깥에서 빨려 들어오는 모션 없음.
   - release 후 Play Hand가 plan 006의 PlayHandFollowSwitcher가 관리하는 base source(Physics 또는 Ghost)로 즉시 복귀.
   - grab된 drumstick이 손에 들어 있는 상태에서 Physics Hand 콜라이더와 겹쳐도 떨림·튕김 없음.
   - release 후 다시 만지면 일반적인 충돌 반응이 정상 발생.

## Deliverables

- `Assets/Hands/Editor/DrumStickSetup.cs` — `m_AttachEaseInTime` 기본값 0으로 변경
- `Assets/Instruments/Drum/Prefabs/drum_stick.prefab` — XRGrabInteractable.attachEaseInTime = 0
- `Assets/Instruments/Drum/Prefabs/drum_stick_L.prefab` — base 상속 확인, override 있으면 해제
- `Assets/Instruments/Drum/Prefabs/drum_stick_R.prefab` — 위와 동일
- `Assets/Hands/Scripts/GripPoseProvider.cs` — Physics Hand 콜라이더와의 IgnoreCollision 처리 추가, Physics Hand 참조 해석 로직 보강

## Acceptance Criteria

- [ ] 헤드셋에서 drum_stick을 grab하는 순간 단일 프레임 안에 Play Hand 포즈가 grip 포즈로 전환되고 drum_stick이 손과 일치된 위치/회전으로 보인다 (보간 지연 없음).
- [ ] grab된 상태에서 손을 빠르게 흔들어도 Play Hand와 drum_stick이 시각적으로 어긋나지 않는다.
- [ ] release 시 PlayHandPoseDriver의 source가 base(plan 006의 PlayHandFollowSwitcher가 관리하는 Physics/Ghost 중 하나)로 즉시 복귀한다.
- [ ] grab된 drum_stick이 Physics Hand 콜라이더 안에 들어와도 떨림·튕김 없이 안정적으로 잡혀 있다 (plan 006 완료 환경 기준).
- [ ] release 후 drum_stick과 Physics Hand의 충돌이 정상 복원되어 비잡기 상태에서 손으로 다시 만지면 일반적인 충돌 반응이 발생한다.
- [ ] L/R 두 변형 모두 동일하게 작동한다.

## Out of Scope

- Drumstick 외 다른 도구(피아노 키 등)의 grip 포즈 정의·등록.
- 양손 협조 grab(한 손이 잡은 도구를 다른 손이 함께 잡는 경우).
- Throw/release physics 튜닝(ThrowVelocityScale, ThrowAngularVelocityScale 등은 spec 범위 밖).
- Drumstick의 grip 포즈 데이터(현재 DrumStickSetup이 자동 생성하는 GripPoseHand) 자체의 수정.

## Notes

- 본 plan은 plan 006/007과 독립적으로 실행 가능. 단독 실행 시 Approach 3(IgnoreCollision)는 의미 없으므로 생략 가능. plan 006이 먼저 완료되어 있으면 함께 적용.
- DrumStickSetup의 변경 후 base prefab이 갱신되도록 `Tools/DrumStick/1. Setup Base Prefab` 메뉴를 다시 실행하거나, 이미 있는 prefab을 직접 수정. 둘 중 어느 쪽이든 결과는 동일해야 함.
- attachEaseInTime을 줄였을 때 도구가 손에 너무 강하게 스냅되어 헤드셋에서 위화감이 있다면 plan 작업 중 사용자에게 보고하고 spec 결정 재확인.
