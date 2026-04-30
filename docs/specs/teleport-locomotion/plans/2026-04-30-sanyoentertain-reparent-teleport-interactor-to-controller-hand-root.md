# Teleport Interactor를 LeftControllerHandRoot로 재배치

**Linked Spec:** [`01-base-teleport.md`](../specs/01-base-teleport.md)
**Caused By:** [`2026-04-30-sanyoentertain-base-teleport.md`](./2026-04-30-sanyoentertain-base-teleport.md)
**Status:** `In Progress`

## Goal

선행 plan에서 `Camera Offset/Hands/Left`(정적 컨테이너)의 직속 자식으로 부착됐던 `Teleport Interactor`를 controller pose가 실제로 driving되는 `Camera Offset/Hands/Left/LeftControllerHandRoot`(TrackedPoseDriver 보유)의 자식으로 재배치한다. 이로써 라인이 사용자의 왼손 컨트롤러 위치에서 시각적으로 출발해 thumbstick만으로 텔레포트가 성립한다.

## Context

> **선행 plan 검증 실패에서 파생됨.** 선행: `2026-04-30-sanyoentertain-base-teleport.md`.
> 실패한 Acceptance Criteria:
> - `[manual-hard]` Play 모드 + VR(또는 XR Device Simulator)에서 **왼손 thumbstick만**을 앞으로 밀면 (트리거·그립·기타 어떤 버튼도 누르지 않은 상태에서) 왼손 위치에서 라인이 즉시 표시된다. — 사용자 검증: 라인이 시각적으로 안 뜸. 가설은 "Left가 아니라 Left Ghost Hand에 둬야 한다"였으나 분석 결과 정확한 타겟은 controller pose를 받는 `LeftControllerHandRoot`였음.
> - `[manual-hard]` 라인이 `Plane` 위 한 지점을 가리키는 동안 **thumbstick을 놓는 순간** ... 카메라 리그가 그 지점으로 이동하고 라인이 사라진다. — 선행 [1] fail로 검증 불가 (Hold).
> - `[manual-hard]` 라인 끝이 `Plane`을 벗어난 허공/Table/악기를 가리킬 때 thumbstick을 놓으면 이동이 일어나지 않고 라인만 사라진다. — 선행 [1] fail로 검증 불가 (Hold).
>
> 본 plan은 위 항목을 다시 통과 가능하게 만드는 부속 작업을 다룬다.

### 분석으로 확정한 원인

선행 plan은 XRI Starter Assets의 `Teleport Interactor.prefab` 인스턴스를 `Camera Offset/Hands/Left` GameObject(fileID `8914745029960616737`, Transform fileID `4884399879460611291`)의 직속 자식으로 부착했다. 그런데 이 `Left` GameObject는:

- `m_LocalPosition: 0, 0, 0`(부모인 `Hands`에 대한 정적 위치)
- 자체 컴포넌트는 `Transform`과 본 plan에서 새로 부착한 `ControllerInputActionManager`뿐
- TrackedPoseDriver/XRController 등 **controller pose를 받는 컴포넌트가 없음**

즉 `Left`는 양손 hand rig 인스턴스들을 묶는 **정적 컨테이너**다. 사용자의 왼손 컨트롤러 pose를 실제로 받는 GameObject는 `Left`의 자식 중 하나인 **`LeftControllerHandRoot`**([`Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab`](../../../Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab), GUID `9b9ac1cf446e2034e9b80d08d635e7ae`)다. 이 prefab은 다음을 보유한다:

- `Transform` (m_LocalPosition `(0, 1.1176, 0)` — 헤드셋 베이스라인 높이 보정)
- `XRInteractionGroup` (interactor swap 조정용, 본 plan에서는 사용하지 않음)
- `HapticImpulsePlayer` (XRI Default Input Actions의 `Haptic` 액션 reference)
- **`TrackedPoseDriver`** (script GUID `c2fadf230d1919748a9aa21d40f74619`) — `XRI Default Input Actions/XRI LeftHand`의 `Position`/`Rotation`/`Tracking State` 액션을 binding. **이 컴포넌트가 매 프레임 컨트롤러 pose를 이 GameObject의 transform에 적용한다.**
- 자식: `LeftControllerGhostHand`(시각용 ghost prefab)

결과적으로 선행 plan 적용 후 `ControllerInputActionManager`가 thumbstick performed에서 `Teleport Interactor.gameObject.SetActive(true)`를 호출하면 라인 visual은 켜지지만, 라인 origin이 controller가 아니라 `Hands/Left`의 정적 위치(대략 카메라 offset 원점)에서 출발하기 때문에 사용자 시야 밖에 그려지거나 바닥 아래로 가려져 "라인이 안 보임"으로 관측된다.

### 수정 방향

XRI 3.x 표준 hierarchy(`XR Origin/Camera Offset/{Left|Right} Controller/Teleport Interactor` — Controller GameObject는 항상 TrackedPoseDriver 보유)와 동일한 구조로 맞춘다 — Teleport Interactor의 부모를 `Left` → `LeftControllerHandRoot`로 변경. XRI Starter Assets 데모([`Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab`](../../../Assets/Samples/XR%20Interaction%20Toolkit/3.3.1/Starter%20Assets/Prefabs/XR%20Origin%20(XR%20Rig).prefab))에서도 확인된 패턴.

### 다른 가능성 점검 (이번 plan에서 다루지 않는 이유)

분석 과정에서 다음 항목들도 점검했고 모두 정상으로 확인되어 본 plan에서는 손대지 않는다.

- `m_IsActive: 0`(Teleport Interactor 인스턴스의 초기 비활성) — 의도된 설계. ControllerInputActionManager가 SetActive로 토글한다.
- `m_SelectInput.m_InputActionReferencePerformed`/`m_InputActionReferenceValue`(XRI Left Locomotion/Teleport Mode) override — 정확히 적용됨.
- `TeleportationArea.m_TeleportationProvider: {fileID: 0}` — 자동 탐색 모드. 씬에 Provider 1개뿐이라 정상 동작.
- `TeleportationProvider`/`LocomotionMediator`/`XRBodyTransformer` wiring — 인스펙터 자동 검색으로 정상 연결됨.
- `ControllerInputActionManager.m_TeleportInteractor`(fileID `7223456789012345005`) 참조 — fileID 기반이므로 Teleport Interactor의 transform 부모를 바꿔도 참조 자체는 그대로 유효. 따라서 ControllerInputActionManager는 `Left` GameObject에 그대로 둔다 (이동 불필요).

### Ghost Hand 대신 ControllerHandRoot로 정한 이유

`LeftControllerHandRoot`의 자식인 `LeftControllerGhostHand`(prefab GUID `a7f32f4296ccdfb42b0a66dfd8c4425e`)에 Teleport Interactor를 부착해도 부모(LCH)가 controller-tracked이므로 일단 동작은 한다. 그러나:

1. Ghost Hand는 `LeftPhysicsHand` 오케스트레이터(`Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab`)가 mode(controller/handTracking)에 따라 visible/invisible 토글하는 *시각용* 노드다. 텔레포트 라인은 mode 전환과 독립적으로 항상 controller pose를 따라야 한다.
2. Ghost root는 손바닥 정렬을 위해 `DrumStickSetup.AdjustGhostHandPalmOffsets`(`Assets/Hands/Editor/DrumStickSetup.cs`)가 Wrist→Palm 보정 offset을 적용한 위치다. 라인 origin이 손바닥 안쪽으로 들어가 있어 visual이 손에 묻혀 어색하게 보일 수 있다.
3. XRI canonical 위치는 TrackedPoseDriver 직속 — `LeftControllerHandRoot`가 그 자체.

이러한 이유로 Ghost가 아닌 LCH를 부모로 정한다.

## Approach

### 1. VR Player.prefab의 Teleport Interactor 인스턴스 재부모

`Assets/Characters/Prefabs/VR Player.prefab` 안에서 다음 수정을 한다.

1. **Teleport Interactor PrefabInstance(fileID `7223456789012345004`)의 `m_TransformParent`** 값을 변경:
   - 현재: `{fileID: 4884399879460611291}` (= `Left` GameObject의 Transform)
   - 변경: `{fileID: 8092876703916648065}` (= `LeftControllerHandRoot` GameObject의 stripped Transform reference, 이미 동 prefab 안에서 line 985~989에 노출됨 — `m_CorrespondingSourceObject: {fileID: 6391592564113756609, guid: 9b9ac1cf446e2034e9b80d08d635e7ae}`)

2. **`Left` GameObject Transform(fileID `4884399879460611291`)의 `m_Children`** 목록에서 Teleport Interactor의 stripped Transform fileID(`7223456789012345003`)를 제거. 자식 4개(`5629048054344987016`, `8092876703916648065`, `4993623736871699638`, `3685833779193035942`)만 남는다.

3. **`LeftControllerHandRoot` PrefabInstance(fileID `2953468454788347712`)** 가 nested prefab이므로, 새 자식 추가는 다음 둘 중 하나의 형태로 직렬화된다(둘 다 Unity가 자동으로 처리):
   - PrefabInstance의 `m_AddedGameObjects` 항목에 Teleport Interactor 추가, 또는
   - LeftControllerHandRoot Transform의 `m_Children` propertyPath modification으로 추가.

   본 plan은 **Unity Editor의 일반 drag-drop 또는 `manage_gameobject` MCP 호출**을 통해 재부모 작업을 수행하고, Unity가 자동으로 위 둘 중 한 형태를 선택하게 한다. 직접 YAML을 손으로 편집하지 않는다.

4. **m_LocalPosition는 그대로** `(0, 0, 0)` 유지. (XRI demo는 `(0, -0.02, -0.035)`로 라인 origin을 컨트롤러 그립 앞쪽으로 살짝 옮기는 polish를 적용하지만, 본 plan은 "라인이 보이는가"의 base 검증이 목표이므로 추가 offset은 후속 polish plan으로 미룬다.)

### 2. ControllerInputActionManager는 그대로 둔다

`Camera Offset/Hands/Left` GameObject에 부착된 `ControllerInputActionManager`(GUID `f9ac216f0eb04754b1d938aac6380b31`)는 이동하지 않는다. 그 `m_TeleportInteractor` 슬롯은 fileID `7223456789012345005`(Teleport Interactor 인스턴스의 XRRayInteractor stripped MonoBehaviour)를 가리키며, transform 부모 변경은 이 참조에 영향을 주지 않는다.

### 3. 검증

- 자동: VR Player.prefab 텍스트에서 `m_TransformParent` 변경, `Left` Transform `m_Children` 갱신, 컴파일 0건 확인.
- 수동: Play 모드(또는 XR Device Simulator)에서 선행 plan의 manual-hard 3건([1] 라인 표시 / [2] thumbstick release시 텔레포트 / [3] Plane 밖 release시 무이동) 재검증.

## Deliverables

- `Assets/Characters/Prefabs/VR Player.prefab` — Teleport Interactor PrefabInstance의 m_TransformParent 변경 + `Left` Transform `m_Children`에서 Teleport Interactor 제거 + `LeftControllerHandRoot` PrefabInstance에 Teleport Interactor 추가(Unity가 자동으로 m_AddedGameObjects 또는 m_Children modification으로 직렬화).

(새 C# 스크립트 없음. 입력 액션 자산·씬 미변경.)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Characters/Prefabs/VR Player.prefab`의 Teleport Interactor PrefabInstance(`!u!1001 &7223456789012345004`)에서 `m_TransformParent` 값이 `{fileID: 8092876703916648065}`(LeftControllerHandRoot Transform stripped ref)을 가리킨다 — 직접 Grep.
- [ ] `[auto-hard]` 같은 prefab의 `Left` GameObject Transform(`!u!4 &4884399879460611291`)의 `m_Children` 목록에 Teleport Interactor의 stripped Transform fileID `7223456789012345003`이 더 이상 등장하지 않는다 (자식 4개로 줄어듦).
- [ ] `[auto-hard]` 같은 prefab에서 Teleport Interactor 인스턴스의 m_SelectInput override 두 슬롯(`m_SelectInput.m_InputActionReferencePerformed`, `m_SelectInput.m_InputActionReferenceValue`)이 여전히 `XRI Default Input Actions` 자산(GUID `c348712bda248c246b8c49b3db54643f`)의 `XRI Left Locomotion/Teleport Mode` 액션 fileID `1263111715868034790`을 가리킨다 (재부모 작업이 select input 설정을 깨지 않는다).
- [ ] `[auto-hard]` Unity Editor가 컴파일 오류 없이 Domain Reload를 마친다 (`read_console` error/exception 0건).
- [ ] `[manual-hard]` 선행 plan `2026-04-30-sanyoentertain-base-teleport.md`의 실패 AC ("Play 모드 + VR(또는 XR Device Simulator)에서 **왼손 thumbstick만**을 앞으로 밀면 (트리거·그") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-04-30-sanyoentertain-base-teleport.md`의 실패 AC ("라인이 `Plane` 위 한 지점을 가리키는 동안 **thumbstick을 놓는 순간** (다른 어떤 버튼") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-04-30-sanyoentertain-base-teleport.md`의 실패 AC ("라인 끝이 `Plane`을 벗어난 허공/Table/악기를 가리킬 때 thumbstick을 놓으면 이동이") 가 이 plan 적용 후 재검증에서 통과한다.

## Out of Scope

- 라인 origin offset polish(XRI demo의 `(0, -0.02, -0.035)` 같은 그립 정렬 미세 조정) — 시각적 마감 단계가 필요해지면 별도 plan.
- Teleport Interactor를 `LeftControllerHandRoot`의 `XRInteractionGroup`에 starting member로 등록 — 본 plan에서는 다른 interactor와 select 충돌이 없으므로 group 등록 불필요. 추후 같은 손에 NearFar/Ray Interactor를 더 추가하면 그때 group 등록 plan으로 분리.
- 오른손 텔레포트 — 본 피처는 spec(`01-base-teleport.md`) 기준 왼손 단일.
- Hand tracking 모드(`LeftHandTrackingHandRoot`)에서의 텔레포트 — 컨트롤러 사용 가정. 후속 sub-spec/plan으로 미룸.

## Notes

- 본 plan은 단일 prefab 단일 노드의 부모 변경이므로 가능한 한 작은 범위 변경. atomic commit 단위로 자연스럽게 묶인다.
- Unity가 nested prefab의 자식 추가를 직렬화하는 방식은 Unity 버전·상황에 따라 `m_AddedGameObjects` 또는 부모 Transform의 `m_Children` modification 둘 중 하나로 갈리므로, AC #1·#2는 둘 다 만족시킬 수 있는 형태(부모 transform 변경 + Left에서 제거)로 작성됐다. AC가 prefab의 자식 추가 직렬화 방식을 한 가지로 강제하지 않는다.
- **2026-04-30 검증 실패 — VR Player prefab hierarchy 붕괴.** 사용자 manual-hard 보고: "VR Player가 엄청나게 잘못됐고 화면이 아예 보이지 않는다." 분석 결과:
  - VR Player.prefab top-level에 위치한 Teleport Interactor PrefabInstance(`&7223456789012345004`)의 `m_TransformParent`가 nested prefab `LeftControllerHandRoot` PrefabInstance(`&2953468454788347712`)의 stripped Transform(`8092876703916648065`)을 가리키도록 변경됨.
  - 그러나 LCH PrefabInstance의 `m_AddedGameObjects` 배열은 비어 있음(라인 966). Unity의 nested prefab 직렬화 규칙상, top-level prefab이 nested prefab의 자식으로 새 GameObject를 추가하려면 nested prefab PrefabInstance의 `m_AddedGameObjects`에 명시 항목이 있어야 한다 — 그게 없으면 비정합으로 간주된다.
  - 결과: Unity가 SampleScene 안의 VR Player PrefabInstance를 인스턴스화할 때 hierarchy 전체가 붕괴. 씬 안 VR Player의 자식 GameObject는 0개로 줄고, stripped Transform만 남음. Main Camera/AudioListener/XROrigin/Hands/Teleport Interactor 모두 인스턴스화되지 않음.
  - 콘솔 시그널: `"There are no audio listeners in the scene"` 다수, `"Reference frame of the curve not set and XROrigin is not found"`, 컴파일 에러는 0건 — hierarchy/직렬화 문제임을 확정.
  - 본 plan의 Approach 3에 적힌 "Unity가 둘 중 하나(`m_AddedGameObjects` 또는 LCH Transform `m_Children` modification)로 자동 처리한다"는 가정이 실제로는 깨졌다 — 직렬화 시점에 어느 쪽도 채워지지 않은 채 m_TransformParent만 stripped reference를 가리킨 비정합 상태로 디스크에 기록됨.
  - 즉시 복구안: working tree의 VR Player.prefab에서 본 plan의 변경만 되돌린다 (Teleport Interactor PrefabInstance의 `m_TransformParent`를 `{fileID: 4884399879460611291}`(Left Transform)으로 복원, `Left` Transform `m_Children`에 stripped Transform `7223456789012345003`을 다시 추가). 선행 base-teleport plan의 변경(DynamicMoveProvider→TeleportationProvider 교체, ControllerInputActionManager 부착, m_SelectInput override, SampleScene Plane TeleportationArea)은 그대로 둔다.
  - 후속 접근 후보(별도 plan에서 결정 필요): (a) `LeftControllerHandRoot.prefab` 자산 자체를 편집해 그 안에 Teleport Interactor 자식을 넣고 VR Player에서 별도 인스턴스화 — 같은 텔레포트 리그를 우/양손 분기로 확장할 때 자연스러운 구조; (b) 런타임 코드로 `ControllerInputActionManager.Awake/Start`에서 자식 reparent — prefab YAML 충돌 회피, 그러나 새 코드 도입; (c) 정적 컨테이너 Left에 Teleport Interactor를 둔 채 실시간 위치를 LCH 위치에서 따라가게 하는 follower MonoBehaviour 도입.

## Handoff
