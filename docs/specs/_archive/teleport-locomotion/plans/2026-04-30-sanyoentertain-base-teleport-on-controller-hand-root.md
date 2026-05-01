# Base Teleport on Controller Hand Root

**Linked Spec:** [`01-base-teleport.md`](../specs/01-base-teleport.md)
**Status:** `Done`

## Goal

사용자가 왼손 thumbstick으로만 텔레포트 라인을 띄우고 가리킨 자리로 이동하도록, controller pose 권위자인 `LeftControllerHandRoot.prefab` 자산 안에 Teleport Interactor + 입력 매니저를 박아 라인 origin이 컨트롤러 위치를 따라가게 한다. 동시에 자유 이동(continuous move) 컴포넌트를 VR Player.prefab에서 제거해 위치 이동 수단을 텔레포트 하나로 단일화한다. 회전(Snap Turn)은 보존.

## Context

본 plan은 `teleport-locomotion` 피처의 base sub-spec(`01-base-teleport.md`)을 직접 푼다. spec의 What 핵심: ① 왼손 입력으로만 텔레포트 라인을 띄운다, ② 위치 이동만 담당하고 회전(Snap Turn)은 보존, ③ 자유 이동 같은 다른 이동 수단은 입력으로도 발동되지 않게 제거, ④ 텔레포트 가능 surface와 그렇지 않은 영역의 구분을 도입(노 텔레포트 존 자체는 후속 sub-spec).

### 현재 코드 상태 (검증된 사실)

VR Player.prefab(`Assets/Characters/Prefabs/VR Player.prefab`)의 root에는 XRI Locomotion 인프라(`LocomotionMediator`, `XRBodyTransformer`, `InputActionManager`, `XRInputModalityManager`)가 갖춰져 있고 자유 이동을 담당하는 `DynamicMoveProvider`와 회전을 담당하는 `SnapTurnProvider`가 부착되어 있다. `TeleportationProvider`는 미부착.

왼손 controller pose는 `Camera Offset/Hands/Left/LeftControllerHandRoot` GameObject에 driving된다 — 이 노드에 `UnityEngine.InputSystem.XR.TrackedPoseDriver`가 부착되어 있다. prefab source는 `Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab` (GUID `9b9ac1cf446e2034e9b80d08d635e7ae`). 반면 `Camera Offset/Hands/Left` 자체는 `Transform`만 가진 정적 컨테이너로, 손 모드(컨트롤러/핸드 트래킹/물리 핸드/플레이 핸드) 분기 자식 4개(`LeftHandTrackingHandRoot`, `LeftControllerHandRoot`, `LeftPhysicsHand`, `LeftPlayHand`)를 묶기만 한다 — 이 정적 컨테이너에 라인 origin이 부착되면 컨트롤러 pose를 따라가지 않아 사용자 시야 밖에 그려진다.

XRI Starter Assets에는 다음이 갖춰져 있다.

- `Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/Interactors/Teleport Interactor.prefab` — 라인 visual + `XRRayInteractor` + select input 슬롯.
- `Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Scripts/ControllerInputActionManager.cs` — `Teleport Mode` 액션 performed에서 Teleport Interactor의 GameObject `SetActive(true)`, canceled에서 `SetActive(false)`를 호출해 라인 visual을 토글한다.
- `Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/XRI Default Input Actions.inputactions` (GUID `c348712bda248c246b8c49b3db54643f`) — `XRI Left Locomotion` 액션맵 안에 `Teleport Mode`(Vector2, thumbstick), `Teleport Mode Cancel`(Button) 액션 정의.

SampleScene(`Assets/Scenes/SampleScene.unity`)의 텔레포트 가능 floor 후보는 `Plane`(MeshCollider). `Table`(BoxCollider)도 있지만 본 plan에서 `TeleportationArea`를 부여하지 않아 자연스러운 차단 영역으로 둔다. 둘 다 현재 `TeleportationArea`/`Anchor` 미부착.

### 결정 (spec의 Open Questions 해소)

- **왼손 입력**: `XRI Left Locomotion/Teleport Mode` 액션(왼쪽 thumbstick push). 별도 키 매핑 신설 없이 기존 자유 이동에 쓰이던 thumbstick을 그대로 텔레포트 트리거로 전환한다. (`Move` 액션 정의는 자산에 남지만 `DynamicMoveProvider`를 제거하므로 어떤 위치 이동도 만들지 않는다.)
- **시각 전환 효과**: 본 base plan은 즉시 이동(페이드 없음). 페이드 정책은 후속 plan/sub-spec.

### 표준 텔레포트 트리거 흐름

XRI Starter Assets의 표준 셋업에서, thumbstick 단일 입력으로 텔레포트가 성립하려면 두 셋업이 동시에 필요하다 — 한쪽만 해서는 동작하지 않는다.

1. **`ControllerInputActionManager`**가 `Teleport Mode` 액션 performed/canceled에서 Teleport Interactor의 GameObject `SetActive` 토글 → 라인 visual on/off. 이 컴포넌트는 select 자체는 발동시키지 않는다.
2. **Teleport Interactor 인스턴스의 `XRRayInteractor.m_SelectInput`**의 두 슬롯(`m_InputActionReferencePerformed`, `m_InputActionReferenceValue`)이 같은 `Teleport Mode` 액션을 가리킴 → thumbstick을 놓는 순간 select 종료 → `XRRayInteractor`가 마지막 hover 위치로 `TeleportationProvider`에 텔레포트 요청 발행 → 카메라 리그 이동.

원본 `Teleport Interactor.prefab`은 이 두 슬롯이 비어 있으므로 인스턴스 단계의 명시 override가 필수다. 한쪽만 셋업하면 라인은 보이지만 텔레포트가 안 일어나거나, 텔레포트는 발동되지만 라인 visual이 안 뜬다.

### 부착 자산 선택 근거

LCH.prefab은 Hands 도메인 자산이지만 controller pose 권위자라는 본질적 사실상 텔레포트 라인 origin이 그 안에 들어가는 것이 의미적으로 정합하다. `LeftControllerHandRoot.prefab`은 프로젝트 안에서 VR Player.prefab만 참조함을 grep으로 확인했으므로(영향 범위 = VR Player 단일), 이 자산을 직접 편집해도 다른 prefab/씬에 부수 영향은 없다.

대안으로 VR Player.prefab 안에서 LCH 인스턴스의 자식으로 Teleport Interactor를 추가하는 경로(=외부 prefab이 nested prefab 안쪽에 자식 추가)는 PrefabInstance의 `m_AddedGameObjects`/`m_Children` modification 직렬화에서 비정합 사고를 일으킬 수 있어 회피한다 — 본 plan은 LCH.prefab 자산 자체에 자식을 박는 형태로 진행해 인스턴스 단계의 modification 없이 자연스럽게 따라오게 한다.

### 제약

- 새 C# 코드 없이 XRI 표준 컴포넌트와 Starter Assets 자산만 조합한다.
- 자유 이동은 "보이지도, 입력으로 발동되지도 않는 상태"여야 하므로 `DynamicMoveProvider` 컴포넌트 자체를 VR Player.prefab root에서 제거한다.
- `TeleportationArea`는 SampleScene의 `Plane`에만 부여 — spec의 "텔레포트 가능 surface 구분"의 최소 구현. 노 텔레포트 존 자체는 `02-no-teleport-zones.md`.

## Verified Structural Assumptions

- VR Player.prefab(`Assets/Characters/Prefabs/VR Player.prefab`) root에 `DynamicMoveProvider`(자유 이동), `SnapTurnProvider`(회전), `LocomotionMediator`/`XRBodyTransformer`(인프라), `InputActionManager`/`XRInputModalityManager`가 부착되어 있고 `TeleportationProvider`는 미부착이다 — `unity-scene-reader 보고 (2026-04-30)`.
- VR Player.prefab의 `Camera Offset/Hands/Left` GameObject는 Transform만 가진 정적 컨테이너이고, 직속 자식 4개는 `LeftHandTrackingHandRoot` / `LeftControllerHandRoot` / `LeftPhysicsHand` / `LeftPlayHand`다 — `manage_prefabs get_hierarchy 직접 호출 (2026-04-30)`.
- 왼손 controller pose driving 노드는 `Camera Offset/Hands/Left/LeftControllerHandRoot`이며 그 GameObject에 `UnityEngine.InputSystem.XR.TrackedPoseDriver` + `XRInteractionGroup` + `HapticImpulsePlayer`가 부착되어 있다. prefab source는 `Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab` (GUID `9b9ac1cf446e2034e9b80d08d635e7ae`) — `manage_prefabs get_hierarchy 직접 호출 (2026-04-30)`.
- `LeftControllerHandRoot.prefab`은 프로젝트 안에서 VR Player.prefab만 참조한다 — `Grep "9b9ac1cf446e2034e9b80d08d635e7ae" 직접 호출 (2026-04-30)`. 이 자산을 편집해도 영향 범위는 VR Player 단일.
- SampleScene(`Assets/Scenes/SampleScene.unity`)의 텔레포트 가능 floor 후보는 `Plane`(MeshCollider). `Table`(BoxCollider)도 있지만 본 plan에서 `TeleportationArea`를 부여하지 않아 텔레포트 불가 영역으로 자연스럽게 둔다. 둘 다 현재 `TeleportationArea`/`Anchor` 미부착 — `unity-scene-reader 보고 (2026-04-30)`.
- XRI Starter Assets 자산: `Teleport Interactor.prefab`(`Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/Interactors/Teleport Interactor.prefab`), `ControllerInputActionManager.cs`(`Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Scripts/ControllerInputActionManager.cs`), `XRI Default Input Actions.inputactions`(`Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/XRI Default Input Actions.inputactions`, GUID `c348712bda248c246b8c49b3db54643f`). 그 안 `XRI Left Locomotion` 액션맵에 `Teleport Mode`(Vector2)·`Teleport Mode Cancel`(Button) 액션 존재 — `unity-scene-reader 보고 (2026-04-30)`.

## Approach

### 1. VR Player.prefab root: Locomotion 컴포넌트 정리

`Assets/Characters/Prefabs/VR Player.prefab`의 root GameObject(`VR Player`)를 `manage_prefabs` MCP의 `modify_contents`로 편집한다.

- `DynamicMoveProvider` 컴포넌트 제거.
- `TeleportationProvider` 컴포넌트 추가. `Mediator` 슬롯이 자동 검색되지 않으면 같은 GameObject의 `LocomotionMediator`를 명시 할당한다.
- `SnapTurnProvider`, `LocomotionMediator`, `XRBodyTransformer`, `InputActionManager`, `XRInputModalityManager`는 그대로 둔다.

### 2. LeftControllerHandRoot.prefab: Teleport Interactor 자식 추가 + select input override

`Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab` 자산을 `manage_prefabs modify_contents`로 편집한다. root GameObject(`LeftControllerHandRoot`)의 직속 자식으로 XRI Starter Assets의 `Teleport Interactor.prefab`을 인스턴스로 추가.

- Source: `Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/Interactors/Teleport Interactor.prefab`.
- Local position/rotation: zero. (LCH 자체가 controller pose를 받으므로 추가 offset 없이 라인 origin이 컨트롤러 위치에서 출발.)
- 라인 visual 색·길이 등 시각 파라미터는 default 유지.

인스턴스 단계에서 `XRRayInteractor.m_SelectInput`의 두 슬롯을 모두 `XRI Left Locomotion/Teleport Mode` 액션 reference로 명시 override한다.

- `m_SelectInput.m_InputActionReferencePerformed` → `XRI Default Input Actions/XRI Left Locomotion/Teleport Mode`.
- `m_SelectInput.m_InputActionReferenceValue` → 같은 액션.

### 3. LeftControllerHandRoot.prefab root: ControllerInputActionManager 부착

같은 LCH.prefab의 root GameObject(`LeftControllerHandRoot`)에 `ControllerInputActionManager` 컴포넌트를 추가한다(`manage_prefabs modify_contents` + `components_to_add` + `component_properties`).

- `m_TeleportInteractor` → 같은 prefab 안에 방금 추가한 Teleport Interactor의 `XRRayInteractor` 컴포넌트 참조.
- `m_TeleportMode` → `XRI Default Input Actions/XRI Left Locomotion/Teleport Mode` action reference.
- `m_TeleportModeCancel` → `XRI Default Input Actions/XRI Left Locomotion/Teleport Mode Cancel` action reference.
- `m_RayInteractor`, `m_NearFarInteractor` → 비워 둔다(왼손에 다른 ray interactor를 두지 않으므로 swap 동작이 필요 없다).
- `m_SmoothMotionEnabled` → false (텔레포트 단일화를 강제). 그 외 토글은 default 유지.

### 4. SampleScene: 텔레포트 가능 surface 지정

`Assets/Scenes/SampleScene.unity`의 `Plane` GameObject에 `TeleportationArea` 컴포넌트를 추가한다(`manage_components` MCP).

- `Teleportation Provider` 슬롯: 씬에 인스턴스화된 VR Player의 `TeleportationProvider`를 명시 참조 (자동 탐색 모드가 동작하면 비워둬도 무방).
- `Match Orientation` 등 회전 옵션은 default 유지 — 회전은 Snap Turn이 책임지므로 텔레포트가 회전을 덮어쓰지 않게 한다.
- `Table` 등 다른 GameObject에는 본 plan에서 `TeleportationArea`/`Anchor`를 부여하지 않는다.

### 5. 검증

각 Acceptance Criteria 수행. 자동: 직렬화 정합성 + 컴파일 + 인스턴스화 sanity. 수동: VR(또는 XR Device Simulator)로 thumbstick-only 텔레포트 흐름 검증.

## Deliverables

- `Assets/Characters/Prefabs/VR Player.prefab` — root에서 `DynamicMoveProvider` 제거, `TeleportationProvider` 추가.
- `Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab` — root 직속 자식으로 `Teleport Interactor` 인스턴스 추가(인스턴스 `m_SelectInput.m_InputActionReferencePerformed`/`m_InputActionReferenceValue` 둘 다 `XRI Left Locomotion/Teleport Mode`로 override), root에 `ControllerInputActionManager` 컴포넌트 추가(`m_TeleportInteractor`/`m_TeleportMode`/`m_TeleportModeCancel` 슬롯 hookup).
- `Assets/Scenes/SampleScene.unity` — `Plane` GameObject에 `TeleportationArea` 추가, VR Player 인스턴스에 위 prefab 변경 반영.

(새 C# 스크립트 없음. 입력 액션 자산 미수정.)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Characters/Prefabs/VR Player.prefab`의 root 컴포넌트 목록에 `DynamicMoveProvider`가 더 이상 등장하지 않고 `TeleportationProvider`가 부착되어 있다 — `manage_prefabs get_hierarchy` 또는 텍스트 grep으로 확인.
- [ ] `[auto-hard]` 같은 prefab의 root에 `SnapTurnProvider`, `LocomotionMediator`, `XRBodyTransformer`, `InputActionManager`, `XRInputModalityManager`가 그대로 부착되어 있다 (회전·인프라 보존).
- [ ] `[auto-hard]` `Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab`의 root GameObject 자식 목록에 XRI Starter Assets의 `Teleport Interactor.prefab` 인스턴스가 정확히 한 개 존재한다.
- [ ] `[auto-hard]` 같은 LCH.prefab의 Teleport Interactor 인스턴스에서 `XRRayInteractor.m_SelectInput.m_InputActionReferencePerformed`와 `m_InputActionReferenceValue` 두 슬롯이 모두 `XRI Default Input Actions`(GUID `c348712bda248c246b8c49b3db54643f`)의 `XRI Left Locomotion/Teleport Mode` 액션을 가리킨다 (어느 슬롯도 빈 값이거나 다른 액션을 가리키지 않는다).
- [ ] `[auto-hard]` 같은 LCH.prefab의 root GameObject에 `ControllerInputActionManager` 컴포넌트가 부착되어 있고, 직렬화에서 `m_TeleportInteractor`·`m_TeleportMode`·`m_TeleportModeCancel` 세 필드 모두 비어 있지 않다 (`fileID: 0` 또는 빈 reference 아님).
- [ ] `[auto-hard]` `Assets/Scenes/SampleScene.unity`의 `Plane` GameObject에 `TeleportationArea` 컴포넌트가 부착되어 있다.
- [ ] `[auto-hard]` Unity Editor가 컴파일 오류 없이 Domain Reload를 마친다 — `read_console` error/exception 0건.
- [ ] `[auto-hard]` 본 plan 적용 후 VR Player.prefab을 SampleScene에 인스턴스화한 상태에서 핵심 자식 노드가 모두 등장한다 — `Camera Offset/Hands/Left/LeftControllerHandRoot`, `Camera Offset/Hands/Left/LeftControllerHandRoot/Teleport Interactor`, `Camera Offset/Hands/Left/LeftPhysicsHand`, `Main Camera`가 `find_gameobjects` 또는 `manage_scene get_hierarchy`로 확인되고 인스턴스화 콘솔 에러 0건.
- [ ] `[manual-hard]` Play 모드 + VR(또는 XR Device Simulator)에서 **왼손 thumbstick만**(트리거·그립·기타 어떤 버튼도 누르지 않은 상태에서) 앞으로 밀면 왼손 컨트롤러 위치에서 즉시 텔레포트 라인이 표시된다.
- [ ] `[manual-hard]` 라인이 `Plane` 위 한 지점을 가리키는 동안 thumbstick을 놓는 순간(다른 어떤 버튼도 누르지 않은 채로) 카메라 리그가 그 지점으로 이동하고 라인이 사라진다.
- [ ] `[manual-hard]` 라인 끝이 `Plane`을 벗어난 허공/`Table`/악기를 가리킬 때 thumbstick을 놓으면 이동이 일어나지 않고 라인만 사라진다.
- [ ] `[manual-hard]` 오른손 thumbstick으로 Snap Turn 입력을 주면 본 plan 적용 전과 동일한 각도/방향으로 회전한다 (회전 동작이 변하지 않는다).
- [ ] `[manual-hard]` 왼손 thumbstick 외 다른 입력(오른손 thumbstick 평행이동, 기존 자유 이동에 매핑되던 입력 등)으로는 카메라 리그가 평행이동하지 않는다.

## Out of Scope

- 라인이 노 텔레포트 존을 가리켰을 때의 invalid 시각 표시 — `02-no-teleport-zones.md`.
- 악기 근처에서 라인이 anchor로 스냅·구별 표시되는 동작 — `03-instrument-anchors.md`.
- 텔레포트 시 페이드 인/아웃 같은 시각 전환 효과의 도입.
- Snap Turn 자체의 조정·회전 각도 변경 — 본 plan은 보존만 책임진다.
- 오른손 텔레포트, hand-tracking 모드(`LeftHandTrackingHandRoot`)에서의 텔레포트 — spec 기준 왼손·컨트롤러 모드 단일.
- 운영 씬 외 다른 씬(`VirtualMusicStudio.unity`, `asad.unity` 등)의 동기화. 운영 씬이 `SampleScene`이라는 가정 위에 작업한다.

## Notes

- LCH.prefab은 Hands 도메인 자산이지만 controller pose 권위자라는 본질적 사실상 텔레포트 라인 origin이 그 안에 들어가는 것이 의미적으로 정합하다. `Grep "9b9ac1cf446e2034e9b80d08d635e7ae"`로 LCH가 VR Player.prefab에서만 참조됨을 확인했으므로 다른 prefab/씬에 부수 영향은 없다.
- 외부 prefab(=VR Player.prefab)에서 nested prefab(=LCH 인스턴스)의 자식으로 직접 자식을 추가하는 경로는 PrefabInstance `m_AddedGameObjects`/`m_Children` modification 직렬화에서 비정합 사고를 일으킬 수 있어 회피한다 — 본 plan은 LCH.prefab 자산 자체에 자식을 박아 인스턴스 단계의 modification 없이 자연스럽게 따라오게 한다.
- `Move` 액션 자체는 입력 액션 자산에 그대로 남는다. `DynamicMoveProvider`가 사라져 어떤 위치 이동도 만들지 않지만, 액션 정의는 다른 곳에서 참조될 수 있으므로 자산을 손대지 않는다 — 본 plan은 자유 이동의 *발동 경로*만 끊는다.
- 2026-04-30: 검증 실패에서 파생된 후속 plan `2026-04-30-sanyoentertain-fix-push-immediate-teleport-trigger.md` 추가. 완료 후 본 plan의 `[manual-hard]` "Play 모드 + VR(또는 XR Device Simulator)에서 왼손 thumbstick만(트리거·" 항목 재검증 필요.
- 2026-04-30: 검증 실패에서 파생된 후속 plan `2026-04-30-sanyoentertain-fix-push-immediate-teleport-trigger.md` 추가. 완료 후 본 plan의 `[manual-hard]` "라인이 Plane 위 한 지점을 가리키는 동안 thumbstick을 놓는 순간(다른 어떤 버튼도 " 항목 재검증 필요.
- 2026-04-30: 검증 실패에서 파생된 후속 plan `2026-04-30-sanyoentertain-fix-push-immediate-teleport-trigger.md` 추가. 완료 후 본 plan의 `[manual-hard]` "라인 끝이 Plane을 벗어난 허공/Table/악기를 가리킬 때 thumbstick을 놓으면 이동이" 항목 재검증 필요.

## Handoff

- **VR Player.prefab root** (`Assets/Characters/Prefabs/VR Player.prefab`): `TeleportationProvider` 부착, `DynamicMoveProvider` 제거. `SnapTurnProvider`/`LocomotionMediator`/`XRBodyTransformer`/`InputActionManager`/`XRInputModalityManager` 그대로 보존. 위치 이동 수단은 텔레포트 단일.
- **LeftControllerHandRoot.prefab** (`Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab`): root 직속 자식으로 XRI Starter Assets `Teleport Interactor.prefab` 인스턴스 1개. PrefabInstance modification에서 `XRRayInteractor.m_SelectInput.m_InputActionReferencePerformed`·`m_InputActionReferenceValue` 두 슬롯 모두 `XRI Left Locomotion/Teleport Mode`(fileID `1263111715868034790`, GUID `c348712bda248c246b8c49b3db54643f`) override. `m_SelectActionTrigger: 1`(State) 추가. root에 `ControllerInputActionManager`(GUID `f9ac216f0eb04754b1d938aac6380b31`) 부착, `m_TeleportInteractor`/`m_TeleportMode`/`m_TeleportModeCancel` 세 슬롯 hookup.
- **SampleScene** (`Assets/Scenes/SampleScene.unity`): Plane GameObject에 `TeleportationArea`(GUID `38f6bf3d943ac7945842268c9ef1dca6`) 부착, `m_TeleportTrigger: 0`(OnSelectExited — release 시 발동). Table 등 다른 GameObject는 TeleportationArea 미부착으로 자연스러운 차단 영역.
- **입력 흐름**: 왼손 thumbstick(XRI Left Locomotion/Teleport Mode, Vector2 + Sector(directions=1) interaction) push → 라인 표시 + select active 유지. release → select_exited → Plane 위에서는 TeleportationArea가 release 시 SendTeleportRequest → 카메라 리그 이동. Plane 밖이면 release 시 라인만 사라지고 이동 X.
- **회전 보존**: `SnapTurnProvider`는 본 plan 변경 대상 아님 — 오른손 thumbstick Snap Turn 동작 동일.
- **후속 plan에서 주의**: 노 텔레포트 존(`02-no-teleport-zones.md`)을 추가할 때 추가 surface에 TeleportationArea를 부착할 경우 enum 매핑 함정으로 `m_TeleportTrigger`가 1(OnSelectEntered)로 들어가지 않도록 적용 직후 텍스트 grep으로 직렬화 결과 확인 권장 (`fix-push-immediate-teleport-trigger.md`의 진단 결과로 확정된 패턴).
