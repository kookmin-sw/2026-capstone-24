# No Teleport Zone via Blocking Collider

**Linked Spec:** [`02-no-teleport-zones.md`](../specs/02-no-teleport-zones.md)
**Status:** `Done`

## Goal

`Plane`(`TeleportationArea` 부착) 위에 살짝 띄워 깔리는 별도 차단 콜라이더 GameObject(`NoTeleportZone`)를 도입해, 라인이 그 위를 가리키는 동안 XRI 기본 동작에 의해 자동으로 invalid color로 표시되고 release 시 텔레포트가 발동되지 않게 한다. 씬 작업자가 끌어다 놓고 크기·위치만 조정하면 끝나도록 재사용 prefab 1개도 같이 만든다. 새 C# 코드 0줄.

## Context

본 plan은 `teleport-locomotion` 피처의 `02-no-teleport-zones.md`를 직접 푼다. spec의 What 4가지: ① 가상 공간 일부를 노 텔레포트 존으로 지정, ② 라인이 그 위에 있으면 즉시 invalid 표현, ③ 그 위에서 텔레포트 확정해도 이동 X, ④ 노 텔레포트 존의 정의·배치 방식이 씬 작업자 친화적.

### 선행 plan으로 이미 갖춰진 사실

- `Assets/Scenes/SampleScene.unity`의 `Plane` GameObject에 `TeleportationArea` 부착 (`m_TeleportTrigger: 0` = `OnSelectExited`, release 시 발동) — `01-base-teleport.md` + `fix-push-immediate-teleport-trigger.md`로 확정.
- `Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab`에 XRI Starter Assets `Teleport Interactor.prefab` 자식 + `ControllerInputActionManager` root 부착, `m_SelectInput` 두 슬롯이 모두 `XRI Left Locomotion/Teleport Mode`를 가리킴. 라인 표시·release 시 텔레포트 발동까지 정상.
- `VR Player.prefab` root에 `TeleportationProvider`/`SnapTurnProvider` 부착, `DynamicMoveProvider` 미부착.

### 핵심 설계 (왜 차단 콜라이더 패턴인가)

XRI의 `XRInteractorLineVisual`은 hit한 객체가 `ITeleportationInteractable`(=`TeleportationArea`/`TeleportationAnchor`)인지 여부로 valid/invalid color를 자동 분기한다. 즉 일반 GameObject의 콜라이더가 ray에 hit되어 멈추면 LineVisual은 invalid color(red)를 그린다. 선행 plan에서 `Table`(BoxCollider, TeleportationArea 미부착) 위에 라인을 두면 release 시 텔레포트가 발동하지 않음을 이미 검증한 것과 같은 메커니즘.

이 자연 동작을 그대로 활용해, 노 텔레포트 존 = "Plane 위로 살짝 띄운 BoxCollider만 가진 GameObject"로 정의한다.

- 라인이 `Plane`보다 가까운 NoTeleportZone 콜라이더에서 hit → `XRRayInteractor`가 거기서 멈춤 → `ITeleportationInteractable` 아님 → LineVisual = invalid color, release 시 `BaseTeleportationInteractable.SendTeleportRequest` 호출 분기 자체가 없음.
- 라인이 NoTeleportZone을 벗어나 `Plane`을 가리키면 다시 valid color → 정상 텔레포트.

부수 코드(마커 컴포넌트, raycastMask 분기 등) 없이 spec의 What 4가지를 모두 충족한다.

### 결정

- **마커 컴포넌트는 두지 않는다.** 씬 작업자는 `NoTeleportZone.prefab` 인스턴스로 식별. 빈 `MonoBehaviour`는 CLAUDE.md "진단/디버깅 로직 추가 금지" 규칙과도 충돌하므로 회피.
- **메시 렌더러도 두지 않는다.** Editor에서 BoxCollider 와이어프레임으로 식별 가능하고 Play 모드에서는 비가시. spec이 "노 텔레포트 존 자체의 시각 표현"을 요구하지 않음.
- **Layer는 Default(0)**. `XRRayInteractor.raycastMask`가 이미 Default를 hit함이 확인됨(아래 `## Verified Structural Assumptions` 참조). 별도 Layer 신설하면 raycastMask도 같이 손대야 해 변경 범위만 늘어남.
- **prefab 위치**: `Assets/Locomotion/Prefabs/NoTeleportZone.prefab` (도메인 폴더 신설). 기존 `Assets/Hands/`, `Assets/Characters/` 패턴과 동일한 도메인-별 prefab 배치 규칙.
- **SampleScene 데모 zone 개수**: 1개. Plane 위 명확한 영역에 두어 "라인을 zone 안팎으로 통과시키며 invalid/valid 즉시 전환"을 사람이 쉽게 검증할 수 있게.

### 제약

- 새 C# 스크립트 0개. 입력 액션 자산 미수정. LCH.prefab/VR Player.prefab 미수정.
- `Plane`의 `TeleportationArea` 설정(`m_TeleportTrigger: 0`, `interactionLayers: -1`)을 보존.
- NoTeleportZone 안에 *이미 들어가 있는 사용자*를 강제로 밖으로 밀어내는 동작은 본 plan 책임 아님 (spec Out of Scope).

## Verified Structural Assumptions

- SampleScene `Plane` GameObject: world position [0, 0, 0], rotation [0, 0, 0], scale [1, 1, 1]. `MeshCollider` 부착(unity default Plane 메시, bounds size [10, ~0, 10]). Layer = Default(0) — `unity-scene-reader 보고 (2026-04-30, manage_scene get_hierarchy / manage_components)`.
- SampleScene 정적 GameObject `Table`: position [1.549, 0.193, 1.985], scale [1.7515, 1.067, 1], `BoxCollider` 부착(center [1.549, 0.193, 1.985], size [1.752, 1.067, 1]). 본 plan의 NoTeleportZone 데모 인스턴스는 Table 콜라이더와 위치 충돌하지 않는 영역에 배치 — `unity-scene-reader 보고 (2026-04-30, manage_scene get_hierarchy)`.
- SampleScene 안 인스턴스화된 VR Player의 자식 `Teleport Interactor`의 `XRRayInteractor.raycastMask`는 Default Layer(0)를 hit한다(직렬화 raw value `-2147483615`, Default 비트 포함). `interactionLayers`는 `-2147483648`(Nothing) — Plane의 `TeleportationArea.interactionLayers: -1`과 매치(XRI는 Nothing↔Everything 매치 규칙 아니지만, 선행 plan 검증에서 라인 표시·텔레포트 발동이 정상이었으므로 현 셋업이 Default Layer raycast hit 흐름은 문제없음을 입증) — `unity-scene-reader 보고 (2026-04-30, manage_components)`.
- Plane의 `TeleportationArea.interactionLayers` = `-1`(Everything). NoTeleportZone에는 `TeleportationArea`를 부착하지 않으므로 `interactionLayers` 자체가 존재하지 않음 — `unity-scene-reader 보고 (2026-04-30, manage_components)`.
- XRI의 invalid color 자동 분기 메커니즘: `XRInteractorLineVisual`이 hit 객체가 `ITeleportationInteractable`이 아니면 invalid color를 사용. 본 plan에서는 NoTeleportZone에 `TeleportationArea`를 부착하지 않아 자동 분기에 위임 — `선행 plan(base-teleport-on-controller-hand-root.md, fix-push-immediate-teleport-trigger.md)에서 Table(TeleportationArea 미부착, BoxCollider만)에서 release 시 텔레포트 미발동·라인 invalid 표현이 사용자 검증으로 확인된 결과를 그대로 활용`.

## Approach

### 1. `NoTeleportZone.prefab` 자산 생성

`Assets/Locomotion/Prefabs/NoTeleportZone.prefab`을 `manage_prefabs create`로 신규 생성한다. 도메인 폴더 `Assets/Locomotion/`과 그 하위 `Prefabs/`가 없으면 `manage_asset` MCP의 `create_folder`로 같이 만든다.

prefab 구성:

- root GameObject 이름: `NoTeleportZone`.
- 부착 컴포넌트: `BoxCollider` 1개만. (Transform은 GameObject 기본 부착이므로 제외 표기.)
  - `m_Center`: [0, 0.025, 0] — 인스턴스 transform.y=0이어도 자동으로 Plane(y=0) 위에 떠서 ray가 Plane보다 먼저 hit되도록.
  - `m_Size`: [1, 0.05, 1] — Y는 ray 차단을 위한 최소 두께. X·Z는 인스턴스 단계에서 transform.scale로 조정.
  - `m_IsTrigger`: false (raycast hit 받기 위해 isTrigger=false 유지. XRI raycast가 trigger도 hit할 수 있으나, 물리 일관성 위해 solid).
- 메시 렌더러·MeshFilter·기타 컴포넌트 부착 안 함.
- Layer = Default(0). prefab GameObject layer 그대로.
- Tag = Untagged.

### 2. SampleScene 데모 zone 인스턴스 배치

`Assets/Scenes/SampleScene.unity`에 step 1의 `NoTeleportZone.prefab` 인스턴스 1개를 추가한다(`manage_gameobject` MCP의 prefab 인스턴스화 모드 또는 `manage_scene` 활용).

- 인스턴스 이름: `NoTeleportZone (Demo)` (씬 작업자가 추후 다른 zone 추가 시 식별하기 좋게).
- world position [0, 0, 2.5] — Plane 가운데 z=+2.5 영역. Table([1.549, _, 1.985])과 X/Z 모두 충분히 떨어져 위치 충돌 없음.
- world rotation [0, 0, 0].
- world scale [2, 1, 1.5] — X 2m, Z 1.5m 범위 차단. (BoxCollider local size [1,0.05,1] × scale → 월드 size [2, 0.05, 1.5].)
- parent: scene root (특정 그룹 없이).

### 3. 검증

각 Acceptance Criteria 수행. 자동: prefab 생성 + 컴포넌트 부착 + scene 인스턴스 직렬화 + 컴파일 + 인스턴스화 sanity. 수동: VR(또는 XR Device Simulator) Play 모드에서 라인을 zone 안팎으로 이동시키며 invalid/valid 즉시 전환과 release 시 차단을 확인.

## Deliverables

- `Assets/Locomotion/Prefabs/NoTeleportZone.prefab` — root에 `BoxCollider`(center [0, 0.025, 0], size [1, 0.05, 1], isTrigger false) 1개만 부착. 메시 렌더러 없음. Layer Default. **신규.**
- `Assets/Locomotion/Prefabs/.meta` 등 도메인 폴더 신설에 따른 .meta 파일들 — Unity가 자동 생성. **신규.**
- `Assets/Scenes/SampleScene.unity` — `NoTeleportZone (Demo)` GameObject 인스턴스 1개 추가(prefab 참조, position [0, 0, 2.5], scale [2, 1, 1.5]). **수정.**

(새 C# 스크립트 없음. LCH.prefab / VR Player.prefab / 입력 액션 자산 / Plane TeleportationArea 미수정.)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Locomotion/Prefabs/NoTeleportZone.prefab` 자산이 존재하고 root GameObject에 정확히 `Transform` + `BoxCollider` 두 개의 컴포넌트만 부착되어 있다(메시 렌더러·MeshFilter·기타 컴포넌트 0개) — `manage_prefabs get_hierarchy` 또는 텍스트 검사.
- [ ] `[auto-hard]` 같은 prefab의 `BoxCollider` 직렬화 값: `m_Center: {x:0, y:0.025, z:0}`, `m_Size: {x:1, y:0.05, z:1}`, `m_IsTrigger: 0` (false).
- [ ] `[auto-hard]` `Assets/Scenes/SampleScene.unity`에 `NoTeleportZone (Demo)`라는 이름의 GameObject 인스턴스가 정확히 1개 존재하고, 그 GameObject가 step 1의 `NoTeleportZone.prefab`의 prefab 인스턴스다(PrefabInstance source가 해당 prefab의 GUID를 가리킴).
- [ ] `[auto-hard]` `NoTeleportZone (Demo)` 인스턴스의 world transform이 position [0, 0, 2.5], rotation [0, 0, 0], scale [2, 1, 1.5]에 일치한다(부동소수점 허용 오차 ±0.001).
- [ ] `[auto-hard]` `NoTeleportZone (Demo)` 인스턴스에 `TeleportationArea`/`TeleportationAnchor`/`ITeleportationInteractable` 구현 컴포넌트가 단 하나도 부착되어 있지 않다.
- [ ] `[auto-hard]` SampleScene `Plane` GameObject의 `TeleportationArea` 직렬화가 그대로 보존된다(`m_TeleportTrigger: 0`, `interactionLayers: -1`, 부착 상태 유지) — 선행 plan 결과 미회귀.
- [ ] `[auto-hard]` `Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab`과 `Assets/Characters/Prefabs/VR Player.prefab`의 선행 plan 결과(Teleport Interactor 자식·`ControllerInputActionManager` 부착·`m_SelectInput` 두 슬롯 hookup·`TeleportationProvider` 부착·`DynamicMoveProvider` 미부착)가 그대로 보존된다.
- [ ] `[auto-hard]` Unity Editor가 컴파일 오류 없이 Domain Reload를 마친다 — `read_console` error/exception 0건.
- [ ] `[manual-hard]` Play 모드 + VR(또는 XR Device Simulator)에서 왼손 thumbstick으로 텔레포트 라인을 띄우고 라인 끝을 `NoTeleportZone (Demo)` 영역 위로 가져가면 라인의 색이 즉시 invalid color(빨간색)로 전환된다.
- [ ] `[manual-hard]` 같은 상태에서 라인 끝을 `NoTeleportZone (Demo)` 영역 밖의 `Plane` 위로 이동시키면 라인 색이 즉시 valid color로 복구된다.
- [ ] `[manual-hard]` 라인이 `NoTeleportZone (Demo)` 위를 가리키는 상태에서 thumbstick을 release하면 카메라 리그가 이동하지 않고 라인만 사라진다.
- [ ] `[manual-hard]` 라인을 `NoTeleportZone (Demo)` 위로 진입·이탈시키며 통과시킬 때마다 invalid/valid 색 전환이 한 프레임 안에 즉각적으로 일어난다(눈에 띄는 지연 없음).
- [ ] `[manual-hard]` 본 plan 적용 후에도 `Plane` 위 zone 바깥 한 지점을 가리키는 동안 thumbstick을 release하면 카메라 리그가 그 지점으로 정확히 이동한다(선행 plan 동작 미회귀).
- [ ] `[manual-hard]` 오른손 thumbstick Snap Turn 동작이 본 plan 적용 전과 동일하다(회전 동작 미회귀).

## Out of Scope

- 노 텔레포트 존 안에 *이미* 들어가 있는 사용자를 강제로 밖으로 밀어내는 동작 — spec Out of Scope, 본 plan은 라인 진입 차단만 책임.
- 노 텔레포트 존 자체의 시각 표현(색 quad, 경계선 등) — spec이 요구하지 않음. 필요해지면 별도 plan/sub-spec.
- 악기 anchor 반경에서 라인이 "구별되는 표현"으로 바뀌는 동작 — `03-instrument-anchors.md`.
- 페이드 인/아웃 같은 시각 전환 효과.
- 운영 씬 외 다른 씬(`VirtualMusicStudio.unity`, `asad.unity` 등)의 노 텔레포트 존 동기화. SampleScene 단일.
- `NoTeleportZone` 마커 `MonoBehaviour` 도입 — 본 plan 결정으로 명시 회피.

## Notes

- 차단 콜라이더 패턴이 동작하는 핵심 가정: XRI `XRRayInteractor`의 raycast가 가장 가까운 hit 1개를 잡는 표준 동작. NoTeleportZone(y≈0.025)이 Plane(y=0)보다 ray origin에 더 가까워 ray가 NoTeleportZone에서 멈춘다. 이 가정이 깨지는 케이스(예: ray가 NoTeleportZone 측면이 아니라 Plane을 통해 zone 아래로 빠져 들어갈 수 있는 매우 낮은 발사각)는 본 plan의 검증 시나리오에서 발생하지 않으나 후속 plan에서 다른 형태(아주 얇은 zone, 비스듬한 zone)를 도입하면 재검증 필요.
- `interactionLayers`(=`-2147483648`/Nothing)와 `Plane.TeleportationArea.interactionLayers`(=`-1`/Everything)의 매치 여부는 본 plan 변경 대상 아님. 선행 plan에서 release 시 정상 텔레포트가 검증되었으므로 현 매치 규칙으로 충분.
- 도메인 폴더 신설 `Assets/Locomotion/`은 향후 anchor 관련 자산(`03-instrument-anchors.md`)도 같이 들어갈 수 있는 자연 위치. 미리 만들어두면 후속 plan 자산 배치 결정이 단순해진다.

## Handoff

- **`Assets/Locomotion/Prefabs/NoTeleportZone.prefab`**: root에 `BoxCollider`(center [0, 0.025, 0], size [1, 0.05, 1], isTrigger=false)만 부착. 메시 렌더러 없음. Layer Default. GUID `f0269c3394cc4cabbf13d40950c197fd`. 추가 노 텔레포트 존이 필요하면 이 prefab을 끌어다 놓고 transform.position(X/Z)와 transform.scale(X/Z)로 영역 조정. Y축 transform·scale은 보통 그대로 둔다(차단 두께만 결정).
- **SampleScene `NoTeleportZone (Demo)` 인스턴스**: position [0, 0, 2.5], scale [2, 1, 1.5]. PrefabInstance fileID `3141592653589793238`. 씬 root에 배치. 후속 plan에서 zone 추가/이동이 필요하면 이 인스턴스를 참고로 같은 prefab의 다른 인스턴스를 만들면 된다.
- **`Plane`의 `TeleportationArea` 보존**: `m_TeleportTrigger: 0`, `m_InteractionLayers.m_Bits: 4294967295`(= `-1`/Everything). 본 plan에서 손대지 않음. 후속 plan에서도 release 트리거 시점·layer 매치 모두 그대로 신뢰 가능.
- **자동 invalid color 분기 메커니즘**: `XRInteractorLineVisual`이 hit 객체의 `ITeleportationInteractable` 구현 여부로 분기. NoTeleportZone에는 `TeleportationArea` 미부착 → hit 시 자동 invalid. 후속에서 "anchor 반경에서 라인 시각이 anchor용으로 구별 표현"을 추가할 때, 그 표현이 invalid color(red)와 충돌하지 않도록 별도 색 슬롯/우선순위를 도입해야 함을 잊지 말 것 — `02-no-teleport-zones.md` Out of Scope 노트와 일치.
- **Layer 결정**: NoTeleportZone은 Default Layer 사용. 후속에서 노 텔레포트 존 전용 Layer를 도입해 raycast 분기를 정밀화하고 싶다면 `XRRayInteractor.raycastMask` 변경도 같이 들어가야 함을 명시.
