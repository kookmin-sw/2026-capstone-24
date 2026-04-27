# Physics Hand 충돌·반작용 + Play Hand 동적 follow + Ghost 시각화

**Linked Spec:** [`hand-physics-interaction.md`](../specs/hand-physics-interaction.md)
**Status:** `Ready`

## Goal

Physics Hand를 비-Kinematic Rigidbody로 전환해 외부 콜라이더와 실제 충돌·반작용하도록 만들고, Play Hand가 Physics 활성/비활성에 따라 source를 동적으로 전환하는 기반을 구축한다. 동시에 Ghost Hand를 파란색 투명 머티리얼로 교체해 디버깅 시 다른 레이어와 시각적으로 즉시 구분되게 한다.

## Context

Hands 시스템은 Ghost(입력 원본) / Physics(물리 세계) / Play(시각) 세 레이어로 구성된다. parent root-spec(`docs/specs/hands/_index.md`) 참고. 세 레이어는 항상 동시에 존재하지만 어떤 레이어가 다른 레이어를 따라가는지는 상황에 따라 동적으로 바뀐다.

현재 코드 상태:
- `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs:56-71` — 매 LateUpdate / onBeforeRender에서 ghost wrist 위치를 `transform.SetPositionAndRotation`으로 직접 복사한다. Rigidbody가 Kinematic이라는 전제이며, 외부 콜라이더와의 충돌은 무시된다.
- `Assets/Hands/Scripts/PlayHandPoseDriver.cs:21-32, 46-59` — 직렬화 필드 `sourceRoot/sourceWristRoot`(prefab 인스턴스에서 Physics Hand를 가리킴)를 추적하고, `PushSourceOverride/PopSourceOverride`로 외부에서 source를 동적으로 교체할 수 있는 구조가 이미 존재한다. Refresh 시점에 동일 프레임에 source가 교체되며 onBeforeRender hook까지 적용된다.
- `Assets/Hands/Prefabs/Physics/{Left,Right}PhysicsHand.prefab` — 현재 Rigidbody.isKinematic = true 추정 (작업 시작 시 Unity MCP로 재확인).
- `Assets/Hands/Prefabs/Play/{Left,Right}PlayHand.prefab` — PlayHandPoseDriver.sourceRoot/Wrist가 Physics Hand의 wrist를 가리키도록 배선되어 있을 것 (작업 시작 시 재확인).
- `Assets/Hands/Prefabs/Ghost/` 하위 4개 prefab(LeftHandTrackingGhostHand, RightHandTrackingGhostHand, LeftControllerGhostHand, RightControllerGhostHand) — SkinnedMeshRenderer가 일반 손 머티리얼을 쓰고 있어 Play와 시각적으로 구분되지 않는다.

이 plan은 spec의 5가지 What 중 Physics Hand 비-Kinematic 전환, Play Hand 동적 follow, Ghost Hand 시각화 변경 3가지를 한 묶음으로 처리한다. 이 셋은 같은 prefab/씬 라운드에 함께 손대는 것이 안전하고(Play Hand sourceRoot 재배선이 Physics 전환과 맞물리므로), 한 세션에 검증까지 끝낼 수 있는 분량이다. 이동/턴 비활성화는 plan 007, Drumstick grip 스냅은 plan 008로 분리.

## Approach

1. **Physics Hand prefab Kinematic 해제 (Unity 직렬화 자산 규칙 적용)**
   - 대상: `Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab`, `RightPhysicsHand.prefab`. 수정 범위는 prefab asset.
   - Rigidbody: `isKinematic = false`, `useGravity = false`(추적 손이라 중력 OFF), `interpolation = Interpolate`, `collisionDetection = ContinuousDynamic`(드럼처럼 얇은 콜라이더 통과 방지), `linearDamping = 0`, `angularDamping = 0.05`.
   - Unity MCP / 에디터로 수정. 텍스트 직접 편집은 마지막 수단.

2. **PhysicsHandGhostFollower를 velocity 기반 손목 추적으로 전환**
   - 손목 root는 비-Kinematic이 되었으므로 `transform.SetPositionAndRotation`으로 강제 이동하면 충돌이 무시된다. 손목만 velocity-based 추적으로 교체.
   - FixedUpdate에서 ghost wrist target과 Rigidbody의 현재 pose 차이를 구해
     - `linearVelocity = (targetPos - rb.position) / fixedDeltaTime`
     - `angularVelocity = axis * angle / fixedDeltaTime` (회전 차이를 axis-angle로)
     클램프: `maxLinearVelocity ≈ 20 m/s`, `maxAngularVelocity ≈ 30 rad/s`. 큰 워프(예: plan 007의 텔레포트 직후) 폭주 방지.
   - 손가락 joint들은 외부 물체와 충돌할 필요가 없으므로 기존처럼 손목 root local space에서 transform 직접 복사를 유지(LateUpdate / onBeforeRender). 이러면 손목이 외부에 막혀 있어도 손가락 모양은 ghost를 그대로 따라간다.
   - 활성화 직후 1프레임은 velocity 추적을 건너뛰고 transform을 ghost 위치로 즉시 동기화하는 1회 플래그(`m_TeleportOnNextSync`) 추가. plan 007의 텔레포트-복귀 시점에 외부에서 호출할 수 있도록 `public void TeleportToSourceImmediately()` 형태로 노출.

3. **PlayHandFollowSwitcher 신규 컴포넌트 작성**
   - 책임: PhysicsHand 활성 상태에 따라 PlayHandPoseDriver의 source를 Physics ↔ Ghost로 동적으로 PushSourceOverride.
   - `Assets/Hands/Scripts/PlayHandFollowSwitcher.cs`. RequireComponent(PlayHandPoseDriver).
   - 직렬화 필드: `physicsHandRoot`(GameObject — activeInHierarchy 판정용), `physicsSourceRoot`/`physicsSourceWristRoot`, `ghostSourceRoot`/`ghostSourceWristRoot`.
   - Update에서: 현재 활성 모드(Physics 또는 Ghost) 결정 → 직전 프레임과 다르면 PlayHandPoseDriver.PushSourceOverride 호출. 같은 source면 no-op.
   - PushSourceOverride만 사용. PopSourceOverride는 GripPoseProvider 같은 외부 코드가 호출. 외부 grip override가 활성화된 동안에도 Switcher는 "기본 source"를 갱신하기만 한다 — PlayHandPoseDriver 측에 grip override가 활성이면 Switcher의 Push는 무시되도록 우선순위 정리 필요.
   - 우선순위 처리: PlayHandPoseDriver에 grip override 전용 채널을 분리하거나, 가장 단순하게는 Switcher가 자기 책임의 base source를 별도 인터페이스(`SetBaseSource`)로 전달하고 PlayHandPoseDriver는 base와 grip override 중 grip override 우선으로 RefreshActiveSource. 본 plan에서 PlayHandPoseDriver에 `SetBaseSource(Transform root, Transform wrist)`를 추가하고 기존 `sourceRoot/sourceWristRoot` 직렬화 필드는 base의 초기값으로 유지. PushSourceOverride는 base 위에 얹히는 override로 동작.

4. **Play Hand prefab 재배선**
   - 대상: `Assets/Hands/Prefabs/Play/LeftPlayHand.prefab`, `RightPlayHand.prefab`. 수정 범위는 prefab asset.
   - PlayHandFollowSwitcher 추가. physics/ghost 양쪽 wrist root 참조 부여.
     - Physics: 같은 hand-root 아래의 LeftPhysicsHand/RightPhysicsHand prefab의 wrist root.
     - Ghost: HandTracking ghost와 Controller ghost가 둘 다 있어 어느 쪽을 쓸지 결정 필요. PhysicsHandGhostFollower의 `ResolveSourceMode` 로직(HandTracking 우선, 없으면 Controller)을 그대로 따라가도록 Switcher도 두 ghost 후보를 모두 받고 동일한 선택 로직 사용. 또는 더 단순하게 "Physics가 비활성일 때 따라갈 ghost는 Physics가 마지막에 따라가던 ghost와 동일"하므로 PhysicsHandGhostFollower에서 현재 활성 ghost를 외부에 노출하는 메서드 추가하고 Switcher가 그것을 참조.
     - 결정: 구조적 단순성을 위해 Switcher도 직접 두 ghost 후보를 받고 동일한 우선순위(HandTracking 우선)로 결정. PhysicsHandGhostFollower 의존 추가하지 않음.
   - 기존 PlayHandPoseDriver.sourceRoot/Wrist는 ghost(HandTracking 우선) 초기값으로 재설정. Switcher가 Start/Update에서 곧바로 PushSourceOverride로 덮어씀.

5. **Ghost Hand 머티리얼 교체**
   - 신규: `Assets/Hands/Materials/GhostHand_Blue.mat`. URP/Lit 또는 URP/Unlit Transparent. Base color #2E66FF, Alpha ≈ 0.4. Render Queue 3000 (Transparent).
   - 4개 Ghost prefab의 SkinnedMeshRenderer.sharedMaterials를 새 머티리얼 단일 슬롯으로 교체. 수정 범위는 prefab asset. 재배선 후 prefab을 다시 열어 머티리얼이 실제로 반영됐는지 확인(직렬화 자산 규칙).

## Deliverables

- `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` — 손목 추적을 velocity 기반으로 변경, `TeleportToSourceImmediately()` 공개 메서드 추가
- `Assets/Hands/Scripts/PlayHandPoseDriver.cs` — `SetBaseSource` 인터페이스 추가, base ↔ grip override 우선순위 정리
- `Assets/Hands/Scripts/PlayHandFollowSwitcher.cs` — 신규. Physics/Ghost source 동적 전환
- `Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab` — Rigidbody 비-Kinematic 변경
- `Assets/Hands/Prefabs/Physics/RightPhysicsHand.prefab` — 위와 동일
- `Assets/Hands/Prefabs/Play/LeftPlayHand.prefab` — PlayHandFollowSwitcher 추가, source 재배선
- `Assets/Hands/Prefabs/Play/RightPlayHand.prefab` — 위와 동일
- `Assets/Hands/Materials/GhostHand_Blue.mat` (+ `.meta`) — 신규 파란색 투명 머티리얼
- `Assets/Hands/Prefabs/Ghost/LeftHandTrackingGhostHand.prefab` — 머티리얼 교체
- `Assets/Hands/Prefabs/Ghost/RightHandTrackingGhostHand.prefab` — 위와 동일
- `Assets/Hands/Prefabs/Ghost/LeftControllerGhostHand.prefab` — 위와 동일
- `Assets/Hands/Prefabs/Ghost/RightControllerGhostHand.prefab` — 위와 동일

## Acceptance Criteria

- [ ] Physics Hand 손목이 SampleScene의 책상·드럼 같은 외부 콜라이더에 막혀 통과하지 않는다 (헤드셋에서 직접 손을 밀어 넣어 확인).
- [ ] Physics Hand가 외부에 막힌 동안 Play Hand는 Physics 위치(=막힌 위치)에 멈춰 있고, Ghost Hand는 입력 위치 그대로 진행해 두 손이 시각적으로 분리된다.
- [ ] Physics Hand GameObject를 런타임에 SetActive(false)로 끄면 Play Hand가 즉시 Ghost 위치로 전환되고, 다시 활성화하면 Physics 위치로 돌아온다 (수동 토글로 검증 가능).
- [ ] 4개 Ghost prefab 모두 파란색 반투명으로 렌더링되어 Play Hand(스킨 머티리얼)와 시각적으로 즉시 구분된다.
- [ ] 충돌이 없는 일반 동작에서 Physics Hand 손목과 ghost wrist의 위치 차이가 평상시 1 cm 이하로 유지된다 (Editor에서 Debug.DrawLine으로 육안 확인 수준 OK).
- [ ] 손가락 joint 포즈가 ghost와 동일하게 따라간다 (jitter, 누락된 joint, 잘못 회전된 joint 없음).
- [ ] 콘솔에 NullReference, 누락된 참조, 매 프레임 발생하는 경고가 없다.

## Out of Scope

- 이동/스냅 턴 시 Physics Hand 비활성화 (plan 007).
- Drumstick grip 즉시 스냅, 도구 잡기 시 충돌 처리 (plan 008).
- 양손 협조 동작.
- Physics Hand 손가락의 콜라이더 기반 충돌(현재 `__Collider_*` 노드 별도 처리는 그대로 둔다).
- Ghost Hand의 디버깅 외 시각화(시연용 강조 표시 등).

## Notes

- spec Open Question 2(이동/턴 종료 후 1프레임 jump)는 plan 007의 책임. 본 plan에서 `TeleportToSourceImmediately()` 훅을 미리 마련해두면 plan 007이 이걸 호출하기만 하면 된다.
- velocity 기반 추적이 떨림을 보이면 PD 게인 추가 또는 `Rigidbody.MovePosition/MoveRotation`으로 fallback 검토. 단 MovePosition은 Continuous 충돌 검출과 호환되지만 외력 응답이 약간 약해질 수 있어, 우선 velocity 직접 설정으로 시작.
- Ghost 머티리얼이 다른 Transparent 오브젝트(드럼 등)와 정렬 충돌이 보이면 RenderQueue를 살짝 높이거나 Z-write 설정을 조정.
