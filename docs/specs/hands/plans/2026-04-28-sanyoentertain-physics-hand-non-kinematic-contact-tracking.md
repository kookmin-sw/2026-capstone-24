# Physics 핸드 비통과 동작 전환 (non-kinematic 콘택트 추종)

**Linked Spec:** [`02-instrument-no-penetration.md`](../specs/02-instrument-no-penetration.md)
**Status:** `Ready`

## Goal

Physics 핸드를 non-kinematic Rigidbody로 전환하고 ghost 입력을 velocity 기반으로 추종하게 만들어, 피아노 건반·드럼 패드 같은 악기 표면을 손이 통과하지 않으면서도 정상 연주 속도에서 떨림·박자 늦음이 발생하지 않게 한다. 본 plan은 단일·양손 동시 비통과 동작을 모두 포함하며, locomotion 처리는 후속 plan으로 분리한다.

## Context

- 부모 spec [`hands/_index.md`](../_index.md)는 입력 추적 / 물리 충돌 / 시각 표현을 Ghost / Physics / Play 세 핸드로 분리한 구조를 명시한다. Linked Spec은 그중 Physics 핸드가 악기 표면에서 비통과 동작을 보이도록 요구한다.
- 선행 sub-spec(Three-Hand Architecture Validation, 아카이브됨)이 끝나, Play 핸드는 Physics GameObject `activeInHierarchy`에 따라 Physics ↔ Ghost로 자동 폴백한다. 본 plan에서는 Physics를 끄지 않으므로(Play 1에서는 토글 안 함, plan 2에서 locomotion 처리) Play는 Physics를 계속 따라가고, 손이 막혀 있는 동안에도 Ghost(투명 파랑)가 입력 raw 위치를 보여주어 Linked Spec의 "막혀 있어도 입력 위치는 Ghost로 관찰" 요구가 충족된다.
- **현재 코드/자산 사실 (2026-04-28 기준)**
  - `Assets/Hands/Prefabs/Physics/{Left,Right}PhysicsHand.prefab` — Rigidbody `m_IsKinematic: 1`, `m_UseGravity: 0`, `m_Interpolate: 1`(Interpolate), `m_CollisionDetection: 2`(ContinuousSpeculative), Layer `8`(`HandPhysics`).
  - `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` — `LateUpdate` + `OnBeforeRender`에서 `transform.SetPositionAndRotation`으로 root를 텔레포트하고, 손가락 본은 `targetJoint.localPosition/localRotation`을 활성 ghost(handTracking 또는 controller)로부터 직접 복사한다. kinematic이라 충돌해도 표면을 통과한다. 활성 ghost 자동 선택(`ResolveSourceMode`)은 `handTrackingRoot`/`controllerRoot`의 `activeInHierarchy`를 본다.
  - 손가락 본 자식 `__Collider_*` 들에 `CapsuleCollider`(IsTrigger=0)가 부착되어 있고, Layer `HandPhysics`. 콜라이더 자체에는 본 plan에서 손대지 않는다.
  - `Assets/Hands/Scripts/HidePhysicsHandInPlayMode.cs` — Play 모드에서 SkinnedMeshRenderer만 끈다(시각만). Rigidbody/Collider 동작에는 영향 없다.
  - `Assets/Hands/Scripts/PlayHandPoseDriver.cs` — `sourceRoot.gameObject.activeInHierarchy` 우선, 비활성 시 `fallbackSourceRoot`(Ghost)로 폴백. 본 plan은 Physics를 끄지 않으므로 Play는 충돌 후에도 Physics 본을 따라가 손이 표면 안쪽에 멈춰 보임(spec 의도).
  - VR Player에 `LocomotionMediator` + `DynamicMoveProvider`가 있으나 본 plan은 그 신호를 사용하지 않는다(plan 2에서 처리).
- **결정 근거**
  - root만 Rigidbody로 추종, 손가락 본은 transform copy 유지: 본 단위 articulated physics는 짝힘이 누적되어 떨림이 커지고 본 plan의 Acceptance(연주 속도에서 떨림 없음)를 만족시키기 어렵다. root가 표면에 막혀 본 chain 전체가 함께 멈추면 손가락 콜라이더도 자연히 더 깊이 들어가지 못한다.
  - velocity 기반 추종 (`Rigidbody.linearVelocity = (target - current) / Time.fixedDeltaTime`, ClampMagnitude 한도 적용) — `MovePosition`은 동작상 kinematic 전용 의미가 강해 colliding 표면을 강제로 밀고 들어갈 수 있다. velocity 갱신은 `FixedUpdate`에서.

## Approach

### 1. Physics 프리팹 Rigidbody 설정 변경

`Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab`, `RightPhysicsHand.prefab`:

- Rigidbody `m_IsKinematic: 1 → 0`
- `m_UseGravity: 0` 유지
- `m_Interpolate: 1` 유지
- `m_CollisionDetection: 2`(ContinuousSpeculative) 유지
- `m_Constraints: 0` 유지 (회전은 추종 코드가 angularVelocity로 직접 풂)

직렬화 자산 편집 직전 `unity-asset-edit` 스킬 절차(범위 고정 = prefab asset, 헤더 보존, 좌·우 동일 변경 확인)를 따른다. PrefabInstance override가 아닌 prefab asset 본체를 수정한다.

### 2. `PhysicsHandGhostFollower`를 velocity 기반 root 추종 + 손가락 본 transform copy로 분리

`Assets/Hands/Scripts/PhysicsHandGhostFollower.cs`:

- 기존 두 ghost(handTracking/controller) 자동 선택 로직(`ResolveSourceMode`, `HasRequiredReferences`, `BuildJointMap`, `__Collider_*`/`{Left,Right}Hand` 무시 규칙)은 그대로 유지.
- 직렬화 필드 추가:
  - `[SerializeField] float maxLinearSpeed = 20f;` — 정상 손목 속도(빠른 트릴) 한계 위로 잡되, 텔레포트 같은 큰 점프는 흡수하지 않게 finite로 둔다(텔레포트 안전성은 plan 2 책임).
  - `[SerializeField] float maxAngularSpeed = 50f;` (rad/s)
- 추종을 두 단계로 분리:
  - **root 추종 (rigidbody)** — `FixedUpdate`에서:
    1. 활성 source 결정(`ResolveSourceMode` 재사용).
    2. `Vector3 deltaPos = sourceRoot.position - rb.position;`
       `rb.linearVelocity = Vector3.ClampMagnitude(deltaPos / Time.fixedDeltaTime, maxLinearSpeed);`
    3. 회전: `Quaternion deltaRot = sourceRoot.rotation * Quaternion.Inverse(rb.rotation);`
       `deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);`
       `if (angleDeg > 180f) angleDeg -= 360f;`
       `Vector3 angularVel = axis.normalized * (angleDeg * Mathf.Deg2Rad / Time.fixedDeltaTime);`
       `rb.angularVelocity = Vector3.ClampMagnitude(angularVel, maxAngularSpeed);`
  - **손가락 본 추종 (transform copy)** — 기존 `LateUpdate` + `OnBeforeRender` 경로를 유지하되 root transform은 더 이상 직접 set하지 않는다(rigidbody가 관리). 손가락 본의 `localPosition/localRotation`만 ghost로부터 복사한다. 즉 기존 `SyncFromActiveSource`에서 `transform.SetPositionAndRotation(sourceRoot.position, sourceRoot.rotation);` 한 줄을 제거하고 jointMap loop만 남긴다.
- `Physics.SyncTransforms()` 호출은 손가락 본을 갱신한 직후로 옮겨도 OK(rigidbody 위치는 Physics 시스템이 이미 보유). 기존 `LateUpdate`에서 호출하던 위치 그대로 두어도 된다.
- `OnEnable`에서 첫 프레임 점프 흡수: `rb.position = sourceRoot.position; rb.rotation = sourceRoot.rotation;` 한 번 호출(activeSource 없으면 skip). Plan 2에서 SetActive 토글로 재활성될 때도 같은 경로를 탄다.

### 3. 컴파일·콘솔 검증

Unity MCP `read_console` (또는 사용자가 Editor에서 직접)로 본 변경으로 인한 신규 컴파일 에러·경고가 없는지 확인. domain reload 완료까지 대기.

### 4. Play 모드 검증 (사용자 직접)

SampleScene에서 다음 시나리오를 확인:

- **A** — 자유 공간: Physics 핸드가 ghost 입력을 시각적 지연 없이 따라간다(Play도 동일).
- **B** — 피아노 흰 건반 누르기: 손가락이 건반 표면에서 멈춘다. ghost는 표면을 통과해 raw 입력을 보여준다. 빠른 좌우 주행에서 떨림·박자 늦음이 의식되지 않는다.
- **C** — 드럼 패드 위 손바닥 누름: Physics 손바닥이 패드 표면에서 멈추고 통과하지 않는다.
- **D** — 양손이 동시에 양 옆 건반을 누름: 양손 모두 비통과가 동등하게 보장.
- **E** — Physics 디버그 머터리얼(투명 빨강)이 의도와 달리 켜져 있지는 않은지 확인(정상 상태에서는 보이지 않아야 함).

## Deliverables

- 수정: `Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab` (Rigidbody `m_IsKinematic: 1 → 0`)
- 수정: `Assets/Hands/Prefabs/Physics/RightPhysicsHand.prefab` (Rigidbody `m_IsKinematic: 1 → 0`)
- 수정: `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` (FixedUpdate root velocity 추종 + 손가락 본 transform copy 분리, `maxLinearSpeed` / `maxAngularSpeed` 직렬화 필드 추가, OnEnable rb 위치 동기화)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab`과 `RightPhysicsHand.prefab` 두 파일 모두 Rigidbody의 `m_IsKinematic: 0`이 grep으로 확인된다.
- [ ] `[auto-hard]` Unity 컴파일이 에러 없이 통과한다 (`read_console`에서 본 변경으로 인한 신규 컴파일 에러 0건).
- [ ] `[auto-soft]` `read_console`에서 본 변경으로 인한 신규 런타임 경고·예외가 없다.
- [ ] `[manual-hard]` Play 모드에서 양손 모두 피아노 흰 건반·드럼 패드 표면을 통과하지 않는다 (시나리오 B/C/D).
- [ ] `[manual-hard]` 정상 연주 속도(예: 빠른 트릴, 양손 16분음표 연타)에서 떨림·박자 늦음이 의식적으로 인지되지 않는다.
- [ ] `[manual-hard]` 손이 표면에 막힌 상태에서도 활성 Ghost(투명 파랑)가 입력의 raw 위치를 계속 보여주어 spec의 "막혀 있어도 입력 위치는 Ghost로 관찰" 요구가 충족된다.
- [ ] `[manual-hard]` 자유 공간에서 손이 시각적 지연 없이 입력을 따라간다 (시나리오 A).

## Out of Scope

- locomotion(Move/Turn/Teleport) 시작·종료 시 Physics 일시 중단·복원 — 후속 plan(`2026-04-28-sanyoentertain-locomotion-pause-and-bilateral-safety.md`)에서 다룸.
- 환경 객체(테이블/벽/바닥)에 대한 비통과 — Linked Spec의 Out of Scope.
- grab/grip 상호작용과의 우선순위 — Linked Spec의 Out of Scope.
- 손가락 본 단위 articulated physics 시뮬레이션 — Approach 결정에 따라 본 plan과 후속 plan 모두에서 다루지 않음.
- `maxLinearSpeed`/`maxAngularSpeed` 자동 튜닝 — 수동 값으로 시작, 측정·튜닝은 검증 결과를 본 뒤 후속 과제로 판단.
- 손가락 본 콜라이더 형상·layer 변경.

## Notes

- 본 plan에서 root teleport 경로를 제거하므로, `OnEnable` 시점에 rigidbody 위치가 ghost와 어긋나 있으면 첫 FixedUpdate에서 큰 velocity가 발생할 수 있다. Approach 2의 OnEnable rb 위치 동기화로 흡수.
- Acceptance가 정성적·manual 비중이 크므로 plan 2 시작 전에 본 plan의 [manual-hard] 항목들이 모두 통과되었음을 확인한다.

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신. 비워둠. -->
