# Base Teleport: 단일 이동 수단 전환

**Linked Spec:** [`01-base-teleport.md`](../specs/01-base-teleport.md)
**Status:** `Abandoned`

> **Abandoned 2026-04-30.** Approach 2가 Teleport Interactor를 `Camera Offset/Hands/Left`(정적 컨테이너)에 부착하도록 작성됐으나, 실제 controller pose는 그 자식 `LeftControllerHandRoot`(TrackedPoseDriver 보유)가 받는다는 사실을 plan 작성 시점에 검증하지 않음. manual-hard 검증에서 라인 visual이 사용자 시야 밖에 그려지는 형태로 fail. 후속 reparent plan도 같은 시점의 다른 사고로 abandoned 처리 — base 자체를 폐기하고 spec(`01-base-teleport.md`)에서 처음부터 다시 plan을 작성한다.

## Goal

`SampleScene` 안에서 사용자가 왼손 thumbstick으로 텔레포트 라인을 띄우고 가리킨 자리로 이동하도록 만든다. 동시에 기존의 자유 이동(`DynamicMoveProvider`)을 입력으로 발동될 수 없게 제거하여, 위치 이동 수단을 텔레포트 하나로 단일화한다. Snap Turn은 그대로 유지한다.

## Context

본 plan은 `teleport-locomotion` 피처의 base sub-spec(`01-base-teleport.md`)을 직접 푼다. Linked Spec의 What 핵심: ① 왼손 입력으로만 텔레포트 라인을 띄운다, ② 위치 이동만 담당하고 회전(Snap Turn)은 보존, ③ 자유 이동 같은 다른 이동 수단은 입력으로도 발동되지 않게 제거, ④ 텔레포트 가능 surface와 그렇지 않은 영역의 구분을 도입(노 텔레포트 존 자체는 후속 sub-spec).

### 현재 코드 상태 (확인 결과)

- 운영 씬: `Assets/Scenes/SampleScene.unity`. VR Player·Piano·DrumKit·Plane(바닥)·Table 인스턴스가 들어 있음.
- VR Player 프리팹(`Assets/Characters/Prefabs/VR Player.prefab`)에 XRI 3.3.1 Locomotion 시스템이 이미 구성되어 있다:
  - `LocomotionMediator` + `XRBodyTransformer` (Locomotion 인프라)
  - `DynamicMoveProvider` — 자유 이동 담당. 입력 액션은 Left/Right Hand Move (thumbstick).
  - `SnapTurnProvider` — 좌/우 Snap Turn 담당. 본 plan에서 **변경 금지**.
  - `TeleportationProvider` — **현재 없음**. 신규 부착 필요.
- VR Player 하위 트리: `Camera Offset/Hands/{Left, Right}` 그리고 각 Left/Right 아래에 PhysicsHand·PlayHand 등 hand 프리팹 인스턴스가 자식으로 들어 있다. 텔레포트 라인은 `Camera Offset/Hands/Left` 아래에 자식 GameObject로 부착한다.
- 입력 액션(`Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/XRI Default Input Actions.inputactions`)에 표준 `XRI Left Locomotion` 액션맵이 이미 정의되어 있다:
  - `Teleport Mode` (Vector2, thumbstick push) — 라인 발동.
  - `Teleport Mode Cancel` (Button) — 취소.
  - `Move` (Vector2) — 자유 이동에 쓰이던 액션. 본 plan 적용 후에도 액션 정의 자체는 남지만 `DynamicMoveProvider`가 사라지므로 어떤 결과도 만들지 않는다.
- XRI Starter Assets에 `Teleport Interactor.prefab`, `Teleport Area.prefab`/`Blocking Teleport Reticle.prefab`이 포함되어 있어 자산은 그대로 재사용한다. 단, 텔레포트 트리거 흐름은 두 컴포넌트의 협업으로 성립한다(아래 "표준 텔레포트 트리거 흐름" 참조).
- XRI Starter Assets에 `ControllerInputActionManager.cs`가 들어 있다 (`Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Scripts/ControllerInputActionManager.cs`).

### 표준 텔레포트 트리거 흐름 (XRI Starter Assets, thumbstick 단일 입력)

XRI 데모 씬 표준 셋업(`XR Origin (XR Rig).prefab`의 Left/Right Controller)을 기준으로 분석한 결과, **thumbstick만으로 텔레포트가 성립하려면 두 개의 셋업이 동시에 필요**하다 — 한쪽만 해서는 동작하지 않는다.

1. **`ControllerInputActionManager`** — 손(controller) GameObject에 부착. 인스펙터에서 `m_TeleportInteractor`(자식의 XRRayInteractor 참조), `m_TeleportMode`(`XRI Left Locomotion/Teleport Mode` 액션), `m_TeleportModeCancel` (같은 액션맵의 Cancel)을 채워 둔다. 동작:
   - `Teleport Mode` 액션 `performed` → `m_TeleportInteractor.gameObject.SetActive(true)` — 라인 visual 표시.
   - `Teleport Mode` 액션 `canceled` → 다음 Update에서 `SetActive(false)` — 라인 visual 숨김.
   - **이 컴포넌트는 select 자체를 발동시키지 않는다. 라인 visual의 on/off만 책임진다.**

2. **Teleport Interactor 인스턴스의 `m_SelectInput` override** — `Teleport Interactor.prefab` 본 자산의 `m_SelectInput.m_InputActionReferencePerformed`와 `m_InputActionReferenceValue`는 둘 다 비어 있다(`fileID: 0`). 이 상태로는 select가 절대 발동되지 않는다. 데모 씬은 인스턴스 단계에서 두 슬롯 모두를 **`XRI Left Locomotion/Teleport Mode` 액션 자체로 직접 override**한다 (`XR Origin (XR Rig).prefab` 1893/1905줄 modification 항목 확인). Vector2 액션을 button으로 해석할 때 magnitude > 0 → performed. 결과:
   - thumbstick을 밀면 → select 시작 → 라인이 가리키는 지점이 hover/valid이면 select가 유지됨.
   - thumbstick을 놓으면 → select 종료 → `XRRayInteractor`가 마지막 hover 위치로 `TeleportationProvider`에 텔레포트 요청을 발행 → 카메라 리그 이동.

요지: select 트리거 입력과 라인 visual 토글 입력이 **동일한 thumbstick 액션**을 가리키도록 양쪽 모두 셋업해야, 사용자가 트리거/그립 같은 별도 버튼을 누르지 않고 **오직 왼쪽 thumbstick만으로** 텔레포트가 성립한다. 한쪽만 셋업하면:
- (1)만 → 라인은 보이지만 thumbstick을 놓아도 텔레포트가 안 일어남.
- (2)만 → thumbstick을 미는 순간 즉시 텔레포트가 발동되지만 라인 시각 표시가 없음 (Interactor GameObject가 active이긴 하지만 visual on/off가 그 흐름을 따라가지 않음).

### 결정 (Linked Spec의 Open Questions 해소)

- **왼손 입력**: XRI 표준 `Teleport Mode` 액션(왼쪽 thumbstick push). 별도 키 매핑을 새로 만들지 않는다 — 기존 자유 이동에 쓰이던 thumbstick을 그대로 텔레포트 트리거로 전환한다.
- **시각 전환 효과**: 본 base plan에서는 즉시 이동(페이드 없음). 페이드 정책은 후속 plan/sub-spec.

### 제약·의사결정 근거

- **새 C# 코드 없이** XRI 표준 컴포넌트(`TeleportationProvider`, `XRRayInteractor`, `TeleportationArea`, `ControllerInputActionManager`)와 Starter Assets 프리팹만 조합한다. Linked Spec의 Why를 만족하기 위해 자체 구현은 불필요하다.
- 자유 이동은 "보이지도, 입력으로 발동되지도 않는 상태"여야 하므로 단순 비활성이 아니라 **`DynamicMoveProvider` 컴포넌트 자체를 VR Player에서 제거**한다. (Snap Turn 동등성을 깨지 않는지 확인 필요.)
- `Teleport Area`는 텔레포트 가능 floor를 *명시적으로* 지정하는 컴포넌트다. 본 plan에서는 씬의 `Plane`(바닥)에만 부여한다 — Linked Spec이 말하는 "텔레포트 가능 surface와 그렇지 않은 영역의 구분"의 최소 구현. (특정 영역을 능동적으로 차단하는 노 텔레포트 존은 `02-no-teleport-zones.md`)

## Approach

### 1. VR Player 프리팹: Locomotion 컴포넌트 정리

`Assets/Characters/Prefabs/VR Player.prefab`의 root GameObject(`VR Player`)에서:

1. `DynamicMoveProvider` 컴포넌트를 **제거**한다. 입력 액션 참조(`Left Hand Move`, `Right Hand Move`)도 함께 사라진다.
2. `TeleportationProvider` 컴포넌트를 **추가**한다. 인스펙터의 `Mediator` 슬롯은 자동으로 같은 GameObject의 `LocomotionMediator`를 잡는다(자동 검색되지 않으면 명시 할당).
3. `SnapTurnProvider`·`LocomotionMediator`·`XRBodyTransformer`는 그대로 둔다.

### 2. 왼손에 Teleport Interactor 부착 + select input override

`Camera Offset/Hands/Left` GameObject 아래에 자식으로 XRI Starter Assets의 `Teleport Interactor.prefab`을 인스턴스로 추가한다.

- Source: `Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/Interactors/Teleport Interactor.prefab` (GUID `c1800acf6366418a9b5f610249000331`).
- 기본 위치/회전: Local zero. (왼손 베이스 위치에서 라인이 출발)
- 라인 visual 색·길이 등 시각 파라미터는 default 유지.

**핵심 override** — 인스턴스 단계에서 `XRRayInteractor`(`m_Script` GUID `6803edce0201f574f923fd9d10e5b30a`)의 select input 두 슬롯을 모두 `XRI Left Locomotion/Teleport Mode` 액션으로 명시 할당한다. (이 override가 없으면 thumbstick을 놓아도 텔레포트가 발동되지 않는다 — Context의 "표준 텔레포트 트리거 흐름" 참조.)

- `m_SelectInput.m_InputActionReferencePerformed` → `XRI Default Input Actions.inputactions`의 `XRI Left Locomotion/Teleport Mode` 액션 reference.
- `m_SelectInput.m_InputActionReferenceValue` → 같은 액션 reference.

(왼손이므로 `XRI Right Locomotion`이 아닌 **`XRI Left Locomotion`** 맵의 액션을 잡는다. 데모 씬 right-hand override는 `XRI Right Locomotion`의 fileID `-8061240218431744966`을 쓰지만, 본 plan에서는 left-hand 대응 액션 reference를 사용한다.)

### 3. 왼손 GameObject에 ControllerInputActionManager 부착 + 액션 hookup

라인 visual의 on/off는 `ControllerInputActionManager` 컴포넌트가 담당한다. `Camera Offset/Hands/Left` GameObject(=Teleport Interactor의 부모)에 컴포넌트를 추가한다.

- Script: `Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Scripts/ControllerInputActionManager.cs`.
- 인스펙터 슬롯:
  - `m_TeleportInteractor` → 같은 GameObject 자식인 `Teleport Interactor`의 `XRRayInteractor` 컴포넌트.
  - `m_TeleportMode` → `XRI Default Input Actions/XRI Left Locomotion/Teleport Mode` action reference.
  - `m_TeleportModeCancel` → `XRI Default Input Actions/XRI Left Locomotion/Teleport Mode Cancel` action reference.
  - `m_RayInteractor`/`m_NearFarInteractor` → 비워 둔다(왼손에 다른 ray interactor를 두지 않으므로 swap 동작이 필요 없다).
  - `m_SmoothMotionEnabled` → false (텔레포트 단일화를 강제). `m_NearFarEnableTeleportDuringNearInteraction` → default(true) 유지.

(2)와 (3)은 동일한 `Teleport Mode` 액션을 가리킨다 — select 발동 입력과 라인 visual 토글 입력이 같은 thumbstick 액션이라는 것이 사용자 요구사항("thumbstick만으로")의 핵심.

### 4. 씬: 텔레포트 가능 surface 지정

`Assets/Scenes/SampleScene.unity`의 `Plane` GameObject에 `TeleportationArea` 컴포넌트를 추가한다.

- `Teleportation Provider` 슬롯: 씬에 인스턴스화된 `VR Player`의 `TeleportationProvider`를 명시 참조 (또는 자동 탐색 모드가 있다면 그걸 사용).
- `Match Orientation` 등 회전 관련 옵션은 default 유지 — 회전은 Snap Turn이 책임지므로 텔레포트가 회전을 덮어쓰지 않게 한다.
- 다른 GameObject(`Table`·악기 등)에는 본 plan에서 `TeleportationArea`/`Anchor`를 부여하지 않는다 — 즉 그 위로는 텔레포트가 발동되지 않아 Linked Spec의 "텔레포트 가능 surface 구분" 최소 형태가 성립한다.

### 5. 검증

각 Acceptance Criteria 수행. 수동 검증은 Unity Editor의 Play 모드 + VR 헤드셋 또는 XR Device Simulator를 통해 진행한다.

## Deliverables

- `Assets/Characters/Prefabs/VR Player.prefab` — 다음 4건의 변경:
  1. root GameObject(`VR Player`)에서 `DynamicMoveProvider` 컴포넌트 제거.
  2. 같은 root에 `TeleportationProvider` 컴포넌트 추가.
  3. `Camera Offset/Hands/Left` GameObject 자식으로 `Teleport Interactor` 인스턴스 추가, 인스턴스에서 `m_SelectInput.m_InputActionReferencePerformed`/`m_InputActionReferenceValue`를 `XRI Left Locomotion/Teleport Mode`로 override.
  4. `Camera Offset/Hands/Left` GameObject에 `ControllerInputActionManager` 컴포넌트 추가, `m_TeleportInteractor`·`m_TeleportMode`·`m_TeleportModeCancel` 슬롯 채우기.
- `Assets/Scenes/SampleScene.unity` — `Plane` GameObject에 `TeleportationArea` 컴포넌트 추가 + 위 프리팹 변경의 인스턴스 반영.

(새 C# 스크립트 없음. 입력 액션 자산은 이미 표준 `XRI Default Input Actions`를 참조하므로 수정하지 않는다.)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Characters/Prefabs/VR Player.prefab`의 root GameObject 컴포넌트 목록에 `DynamicMoveProvider` 스크립트 GUID(`9b1e8c997df241c1a67045eeac79b41b`)가 더 이상 등장하지 않는다 — `Grep`으로 확인.
- [ ] `[auto-hard]` 같은 prefab의 root GameObject에 `TeleportationProvider` 컴포넌트(스크립트 GUID는 `Library/PackageCache/com.unity.xr.interaction.toolkit/.../Locomotion/Teleportation/TeleportationProvider.cs.meta`로 해석되는 값)가 추가되어 있고, `LocomotionMediator`/`XRBodyTransformer`/`SnapTurnProvider` 3종은 그대로 남아 있다.
- [ ] `[auto-hard]` `Assets/Characters/Prefabs/VR Player.prefab`의 `Camera Offset/Hands/Left` 트랜스폼 자식 목록에 XRI Starter Assets의 `Teleport Interactor.prefab`(GUID `c1800acf6366418a9b5f610249000331`) 인스턴스가 정확히 한 개 존재한다.
- [ ] `[auto-hard]` 위 Teleport Interactor 인스턴스의 PrefabInstance modification 목록에 `m_SelectInput.m_InputActionReferencePerformed`와 `m_SelectInput.m_InputActionReferenceValue` 두 propertyPath 모두가 등장하고, 각 항목의 objectReference가 `XRI Default Input Actions` 자산(GUID `c348712bda248c246b8c49b3db54643f`)의 `XRI Left Locomotion/Teleport Mode` 액션 fileID를 가리킨다.
- [ ] `[auto-hard]` `Assets/Characters/Prefabs/VR Player.prefab`의 `Camera Offset/Hands/Left` GameObject에 `ControllerInputActionManager` 컴포넌트(스크립트 경로 `Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Scripts/ControllerInputActionManager.cs`)가 부착되어 있고, 직렬화 데이터에서 `m_TeleportInteractor`·`m_TeleportMode`·`m_TeleportModeCancel` 세 필드 모두 비어 있지 않다(`fileID: 0`이 아님).
- [ ] `[auto-hard]` `Assets/Scenes/SampleScene.unity`의 `Plane` GameObject에 `TeleportationArea` 컴포넌트가 부착되어 있다 — 씬 YAML에서 해당 컴포넌트 스크립트 GUID 등장 확인.
- [ ] `[auto-hard]` Unity Editor가 컴파일 오류 없이 Domain Reload를 마친다 (`read_console` error/exception 0건).
- [ ] `[manual-hard]` Play 모드 + VR(또는 XR Device Simulator)에서 **왼손 thumbstick만**을 앞으로 밀면 (트리거·그립·기타 어떤 버튼도 누르지 않은 상태에서) 왼손 위치에서 라인이 즉시 표시된다.
- [ ] `[manual-hard]` 라인이 `Plane` 위 한 지점을 가리키는 동안 **thumbstick을 놓는 순간** (다른 어떤 버튼도 누르지 않은 채로) 카메라 리그가 그 지점으로 이동하고 라인이 사라진다.
- [ ] `[manual-hard]` 라인 끝이 `Plane`을 벗어난 허공/Table/악기를 가리킬 때 thumbstick을 놓으면 이동이 일어나지 않고 라인만 사라진다.
- [ ] `[manual-hard]` 오른손 thumbstick으로 Snap Turn 입력을 주면 본 plan 적용 전과 동일한 각도/방향으로 회전한다 (회전 동작이 변하지 않는다).
- [ ] `[manual-hard]` 오른손 thumbstick(또는 기존 자유 이동에 쓰이던 입력)을 임의 방향으로 밀어도 카메라 리그가 평행이동하지 않는다 — 자유 이동이 입력으로 발동되지 않는다.

## Out of Scope

- 라인이 노 텔레포트 존을 가리켰을 때의 invalid 시각 표시 — `02-no-teleport-zones.md`.
- 악기 근처에서 라인이 anchor로 스냅·구별 표시되는 동작 — `03-instrument-anchors.md`.
- 텔레포트 시 페이드 인/아웃 같은 시각 전환 효과의 도입.
- Snap Turn 자체의 조정·회전 각도 변경 — 본 plan은 보존만 책임진다.
- 운영 씬 외 다른 씬(`VirtualMusicStudio.unity`, `asad.unity`)의 동기화 — 운영 씬이 `SampleScene`이라는 가정 위에 작업한다. 다른 씬도 운영에 쓴다면 별도 plan으로 처리.

## Notes

- `Move` 액션 자체는 입력 액션 자산에 그대로 남는다. `DynamicMoveProvider`가 사라졌으므로 어떤 위치 이동도 만들지 않지만, 입력 액션 자산을 손대지 않은 이유는 같은 액션맵을 다른 곳에서 참조할 수 있기 때문이다 — 본 plan은 자유 이동의 *발동 경로*만 끊는다.
- `Teleport Interactor.prefab` 본 자산은 `m_SelectInput.m_InputActionReferencePerformed`/`m_InputActionReferenceValue`가 `fileID: 0`(빈 값)으로 박혀 있다 — 즉 인스턴스 단계에서 명시 override가 필수다. 이 사실을 모른 채 "프리팹 부착하면 동작한다"고 가정하면 thumbstick은 작동하지만 텔레포트가 발동되지 않는 무성공 상태로 빠지므로, Approach 2의 override 단계를 반드시 실행해야 한다. (참고: `XR Origin (XR Rig).prefab`의 1893/1905줄이 데모 씬 표준 override 패턴.)
- `ControllerInputActionManager`가 라인 visual on/off만 책임지고 select 자체는 Teleport Interactor의 select input이 발동시킨다는 사실은 `ControllerInputActionManager.cs`의 `OnStartTeleport`가 `m_TeleportInteractor.gameObject.SetActive(true)`만 호출하고 select trigger를 직접 부르지 않는 점에서 확인된다 — 본 plan에서 두 셋업 모두를 강제하는 이유.
- 2026-04-30: 검증 실패에서 파생된 후속 plan `2026-04-30-sanyoentertain-reparent-teleport-interactor-to-controller-hand-root.md` 추가. 완료 후 본 plan의 `[manual-hard]` "Play 모드 + VR(또는 XR Device Simulator)에서 **왼손 thumbstick만**을 앞으로 밀면 (트리거·그" 항목 재검증 필요.
- 2026-04-30: 같은 후속 plan에서 `[manual-hard]` "라인이 `Plane` 위 한 지점을 가리키는 동안 **thumbstick을 놓는 순간** (다른 어떤 버튼" 항목 재검증 필요.
- 2026-04-30: 같은 후속 plan에서 `[manual-hard]` "라인 끝이 `Plane`을 벗어난 허공/Table/악기를 가리킬 때 thumbstick을 놓으면 이동이" 항목 재검증 필요.

## Handoff
