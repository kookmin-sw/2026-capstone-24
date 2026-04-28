# Locomotion 일시 중단·복원 + 양손 안전성 검증

**Linked Spec:** [`02-instrument-no-penetration.md`](../specs/02-instrument-no-penetration.md)
**Status:** `Ready`

## Goal

VR locomotion(Continuous Move / Snap·Continuous Turn / Teleport)이 발생하는 짧은 순간 동안 Physics 핸드를 일시 중단해 가상 환경 객체를 부적절하게 밀거나 튕기지 않도록 하고, 종료와 함께 안전하게 복원한다. 양손이 동시에 같은 또는 인접한 표면에 닿은 상태에서 locomotion이 일어나도 회귀가 없음을 확인한다.

## Context

- 본 plan은 선행 plan `2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md`이 완료된 상태(Physics 핸드 = non-kinematic Rigidbody + velocity 기반 root 추종, OnEnable rb 위치 동기화)를 전제한다.
- Linked Spec [`02-instrument-no-penetration.md`](../specs/02-instrument-no-penetration.md)의 **Behavior 3**: locomotion으로 위치를 크게 바꾸는 짧은 순간 동안 손이 가상 환경 객체를 부적절하게 밀거나 튕기지 않아야 한다. 비통과가 켜진 채로 카메라 rig가 큰 거리를 텔레포트하면 Physics 핸드는 한 프레임 안에 그 거리를 추종해야 하는데, plan 1의 `maxLinearSpeed`로 클램프된 velocity로는 따라잡지 못해 환경 객체와 콜리전을 일으키거나 끌고 가는 위험이 생긴다.
- Linked Spec의 **Behavior 5**: 양손이 같은/인접 표면에 닿아도 비통과 동작이 두 손 모두 보장. plan 1에서 자유 공간 양손 동작은 검증되지만, locomotion 도중·직후 양손 시나리오는 본 plan에서 별도로 회귀 확인한다.
- **현재 씬 사실 (2026-04-28 기준)**
  - `Assets/Characters/Prefabs/VR Player.prefab` — `LocomotionMediator`(Unity.XR.Interaction.Toolkit, namespace `UnityEngine.XR.Interaction.Toolkit.Locomotion`) + `DynamicMoveProvider`(Starter Assets) 부착. mediator는 `locomotionStarted` / `locomotionEnded` (UnityEvent<LocomotionProvider>) 이벤트를 가진다. 텔레포트(`TeleportationProvider`)·연속/스냅 회전(`ContinuousTurnProvider`/`SnapTurnProvider`)도 같은 mediator를 통해 통합 신호된다.
  - SampleScene의 VR Player rig는 단일 인스턴스. `LocomotionMediator`도 한 개.
- **결정 근거 — 일시 중단 메커니즘**
  - (a) `Rigidbody.isKinematic = true` 임시 복귀 — 콜라이더는 살아 있어 piano sensor 등 OnTriggerStay 기반 기재가 텔레포트 도중 잘못된 입력을 만들 위험이 있음.
  - (b) Physics 핸드 GameObject `SetActive(false)` — 콜라이더·rigidbody·sensor trigger 모두 사라져 텔레포트 노이즈를 깨끗하게 차단. plan 1 결과로 Play는 자동으로 활성 Ghost로 폴백해 손이 시각적으로 사라지지 않음. 다음 활성 시 콘택트 캡처가 1프레임 늦게 시작되는 단점은 있으나, 정상 사용자 흐름(이동 후 새 위치에서 연주 시작)에서는 무시 가능.
  - 본 plan은 (b)를 채택. 결정 근거: piano sensor의 OnTriggerStay 노이즈 차단이 정성적 안전성 가장 큰 위험.

## Approach

### 1. Locomotion 인지 컴포넌트 신규 작성

`Assets/Hands/Scripts/LocomotionAwarePhysicsHand.cs`:

- 단순 `MonoBehaviour`. `[DisallowMultipleComponent]`.
- 직렬화 필드:
  - `[SerializeField] UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionMediator locomotionMediator;`
  - `[SerializeField] GameObject physicsHandRoot;` (좌·우 PhysicsHand GameObject 직접 참조)
- 내부 상태: `int m_PauseDepth;` (중첩 신호 보호)
- `OnEnable`에서 `locomotionMediator.locomotionStarted.AddListener(OnLocomotionStarted);` 와 `locomotionEnded.AddListener(OnLocomotionEnded);`
- `OnDisable`에서 listener 해제.
- `OnLocomotionStarted(LocomotionProvider _)`:
  - `m_PauseDepth++;`
  - `m_PauseDepth == 1 && physicsHandRoot != null` → `physicsHandRoot.SetActive(false);`
- `OnLocomotionEnded(LocomotionProvider _)`:
  - `m_PauseDepth = Mathf.Max(0, m_PauseDepth - 1);`
  - `m_PauseDepth == 0 && physicsHandRoot != null && !physicsHandRoot.activeSelf` → `physicsHandRoot.SetActive(true);`
- 좌·우 손 각각 한 인스턴스. 동일 mediator를 공유.

### 2. Editor 와이어링 메뉴 신규 추가

`Assets/Hands/Editor/PlayHandPoseDriverSceneWiring.cs` 또는 동일 폴더의 새 에디터 스크립트(권장: 같은 파일에 메뉴 추가):

- `Tools/Hands/Wire Locomotion Aware Physics Hands` 메뉴 추가.
- 동작:
  1. 활성 씬에서 `LocomotionMediator` 1개 인스턴스를 찾는다(`Object.FindFirstObjectByType<LocomotionMediator>()`). 0개이거나 2개 이상이면 명확한 에러 다이얼로그 후 종료.
  2. 좌·우 PhysicsHand GameObject를 찾는다(기존 wiring 메뉴의 path 규약 재사용 — `VR Player/Camera Offset/Hands/{Left,Right}/...PhysicsHand`).
  3. 좌·우 각각의 hand 컨테이너(예: `VR Player/Camera Offset/Hands/Left`, `.../Right`)에 `LocomotionAwarePhysicsHand` 컴포넌트가 없으면 추가하고, `locomotionMediator` / `physicsHandRoot` 필드를 와이어링한다. 이미 있으면 필드만 갱신.
  4. `EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveOpenScenes();`로 저장.
- 기존 `Wire Play Hand Pose Drivers` 메뉴는 그대로 둔다.

### 3. 컴파일·콘솔 검증

Unity MCP `read_console`로 컴파일 에러·신규 경고 부재 확인. domain reload 완료까지 대기.

### 4. Play 모드 검증 (사용자 직접)

SampleScene에서 다음 시나리오를 확인:

- **A** — Continuous Move(좌측 스틱) 짧게 이동: 이동 중·직후 손이 환경 객체를 밀거나 튕기지 않는다. 종료 후 ghost 추종이 정상 복원.
- **B** — Snap Turn 또는 Continuous Turn으로 큰 회전: 회전 중·직후 손이 환경 객체와 부적절한 콜리전을 일으키지 않는다.
- **C** — 텔레포트로 큰 거리 점프: 도착 후 손이 가상 객체에 끼어 있지 않으며, ghost 추종이 즉시 복원되어 다음 연주 입력이 정상 인식.
- **D** — 양손이 동시에 피아노 흰 건반 두 개를 누르고 있는 상태에서 짧은 Move/Turn 발생: 이동 후 양손 모두 비통과 동작이 동등하게 복원되고, 잘못된 잔여 NoteOn/Off가 발생하지 않는다.
- **E** — locomotion 비활성 동안 plan 1의 시나리오 A~E가 회귀 없이 동작한다.

## Deliverables

- 신규: `Assets/Hands/Scripts/LocomotionAwarePhysicsHand.cs` (LocomotionMediator 이벤트로 PhysicsHand SetActive 토글, 중첩 신호 카운터)
- 수정(또는 신규 에디터): `Assets/Hands/Editor/PlayHandPoseDriverSceneWiring.cs` — `Tools/Hands/Wire Locomotion Aware Physics Hands` 메뉴 추가
- 수정: `Assets/Scenes/SampleScene.unity` (좌·우 `LocomotionAwarePhysicsHand` 인스턴스 + 와이어링 PrefabInstance override)

## Acceptance Criteria

- [ ] `[auto-hard]` Unity 컴파일이 에러 없이 통과한다 (`read_console`에서 신규 컴파일 에러 0건).
- [ ] `[auto-hard]` `Assets/Scenes/SampleScene.unity`에서 좌·우 두 개의 `LocomotionAwarePhysicsHand` MonoBehaviour 인스턴스가 직렬화되어 있고, 각각의 `locomotionMediator`(fileID) / `physicsHandRoot`(fileID) 참조가 0이 아니다 (씬 YAML grep으로 확인).
- [ ] `[auto-soft]` `read_console`에서 본 변경으로 인한 신규 런타임 경고·예외가 없다.
- [ ] `[manual-hard]` Play 모드에서 Continuous Move / Snap Turn / Continuous Turn / Teleport 각각에 대해 이동 중·직후 손이 가상 환경 객체를 부적절하게 밀거나 튕기지 않는다 (시나리오 A/B/C).
- [ ] `[manual-hard]` 양손이 동시에 인접 건반/패드를 누른 상태에서 짧은 Move/Turn 직후 양손 모두 비통과 동작이 동등하게 복원되고, 잘못된 NoteOn/Off가 트리거되지 않는다 (시나리오 D).
- [ ] `[manual-hard]` Plan 1의 시나리오(자유 공간 추종, 단일·양손 비통과, 떨림 부재)가 본 plan 변경 이후에도 회귀 없이 동작한다 (시나리오 E).

## Out of Scope

- 손이 객체를 잡는 grab/grip 상호작용 — Linked Spec의 Out of Scope.
- 환경 객체(테이블/벽/바닥)에 대한 비통과 — Linked Spec의 Out of Scope.
- locomotion 중 햅틱·시각 페이드 등 사용자 피드백 — 본 plan은 안전성에만 집중.
- locomotion 외 이유(Editor pause, 시나리오 컷씬 등)로 인한 일시 중단 정책 — 필요 시 후속 과제.
- `LocomotionMediator`가 0개이거나 2개 이상인 비표준 rig 구성 처리 — 와이어링 메뉴는 에러로 종료.

## Notes

- `LocomotionMediator.locomotionStarted/Ended`는 등록된 모든 provider(텔레포트·이동·회전)에 대해 통합 발화하므로 provider별 분기를 두지 않고 하나의 토글로 충분.
- `SetActive(false)` → `SetActive(true)` 토글 후 첫 프레임 점프는 plan 1의 OnEnable rb 위치 동기화로 흡수된다.
- 양손 동시 검증은 SampleScene의 피아노/드럼 배치에서 한 사용자가 양손을 동시에 사용할 수 있는 상황을 가정한다.

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신. 비워둠. -->
