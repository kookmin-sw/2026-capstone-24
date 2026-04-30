# Instrument Anchors via TeleportationAnchor

**Linked Spec:** [`03-instrument-anchors.md`](../specs/03-instrument-anchors.md)
**Status:** `Ready`

## Goal

XRI 기본 컴포넌트인 `TeleportationAnchor`를 부착한 재사용 prefab(`InstrumentAnchor.prefab`)을 도입하고, SampleScene의 `Piano`/`DrumKit` 앞에 각 1개씩 anchor 인스턴스를 두어, 라인이 anchor 영역 위에 들어오는 즉시 끝점이 anchor 자리로 snap되고 release 시 사용자가 anchor의 위치·각도로 정확히 텔레포트되게 한다. 새 C# 코드 0줄.

## Context

본 plan은 `teleport-locomotion` 피처의 `03-instrument-anchors.md`를 직접 푼다. spec의 What 4가지: ① 악기당 단일 anchor(위치 + 향하는 각도), ② 라인 끝이 anchor 반경 안에 들어오면 시각 표현이 일반 텔레포트와 구별되는 형태로 전환, ③ 그 상태에서 확정 시 anchor 위치·각도로 정확히 텔레포트, ④ 반경 밖이면 [`01-base-teleport.md`](../../_archive/teleport-locomotion/specs/01-base-teleport.md)의 기본 동작 그대로.

### 선행 plan으로 이미 갖춰진 사실

- `Assets/Scenes/SampleScene.unity`의 `Plane` GameObject에 `TeleportationArea` 부착 (`m_TeleportTrigger: 0` = `OnSelectExited`, `m_InteractionLayers.value: -1` = Everything) — `01-base-teleport.md` + `fix-push-immediate-teleport-trigger.md`로 확정.
- `Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab`에 XRI Starter Assets `Teleport Interactor.prefab` 자식 + `ControllerInputActionManager` root 부착 — 라인 표시·release 시 텔레포트 발동까지 정상.
- `VR Player.prefab` root에 `TeleportationProvider`/`SnapTurnProvider` 부착, `DynamicMoveProvider` 미부착.
- `Assets/Locomotion/Prefabs/NoTeleportZone.prefab` 존재. SampleScene에 `NoTeleportZone (Demo)` 인스턴스(pos [0, 0, 2.5], scale [2, 1, 1.5]) 1개 — `02-no-teleport-zones.md` plan으로 확정.
- SampleScene에 `Piano`(pos [0.091, -0.084, 1.016], rotation Y=180°)와 `DrumKit`(pos [-0.857, ~0, -1.625], rotation 0, scale 1.5×) GameObject가 이미 존재.

### 핵심 설계 (왜 XRI `TeleportationAnchor`를 그대로 쓰는가)

XRI의 `TeleportationAnchor`는 spec What 1·3·4를 정확히 제공한다.

- 콜라이더 hit 시 `BaseTeleportationInteractable.SendTeleportRequest` → `GenerateTeleportRequest`가 anchor `Transform`의 world pose로 destinationPosition/destinationRotation을 채움. `m_MatchOrientation = TargetUpAndForward`(=2)일 때 `XRInteractorLineVisual`/`reticle` 갱신 시 reticle이 anchor의 transform.up·transform.forward로 정렬되어 끝점이 anchor 자리·각도로 snap된다(라인 시각이 일반 area의 hit-point 끝점과 즉시 구별됨).
- 라인이 anchor collider를 hit하지 않는 동안에는 ray가 그 아래 `Plane.TeleportationArea`에 그대로 닿아 기본 텔레포트가 동작.

What 2의 "anchor용으로 구별되는 시각 표현"은 본 plan에선 XRI 기본 endpoint snap(=라인 끝점이 anchor.transform 자리·각도로 고정 표시)으로 1차 충족한다. 사용자가 manual 검증에서 "구별되지 않는다"고 판정하면 후속 plan에서 색 분기/마커 prefab을 추가하면 된다(spec OQ "anchor 반경의 기본 크기·시각 표현"은 본 plan의 결정 + 후속 plan 여지로 닫는다).

`02-no-teleport-zones`와 동일한 "Plane 위 살짝 띄운 collider" 패턴을 그대로 차용한다. ray는 Plane(y=0)보다 anchor collider(y≈0.025)에서 먼저 멈춰 자동 분기에 위임 → 부수 코드 0줄.

### 결정

- **anchor 반경 기본 크기**: BoxCollider local center [0, 0.025, 0], size [1, 0.05, 1]. 즉 1m × 1m × 0.05m(차단 두께). 인스턴스 단계에서 transform.scale로 영역 가로/세로 조정. (NoTeleportZone과 동일 패턴 → 씬 작업자 인지 비용 최소.)
- **anchor 시각 표현**: XRI 기본만 사용. `MatchOrientation = TargetUpAndForward`(2)로 reticle 끝점이 anchor 자리·forward로 snap. 추가 색·마커 prefab은 spec OQ에 따라 후속 plan 여지로 남김.
- **prefab 위치**: `Assets/Locomotion/Prefabs/InstrumentAnchor.prefab`. NoTeleportZone과 같은 도메인 폴더.
- **데모 인스턴스 개수**: 2개(piano 앞, drumkit 앞). spec의 마지막 Behavior(두 악기 anchor 반경이 겹치지 않는 케이스에서 모호함 없이 선택)를 사람이 검증하려면 둘 이상의 anchor가 필요.
- **anchor parent**: scene root. Piano/DrumKit 자식으로 두지 **않는다** — 이유: Piano(rotation Y=180°)·DrumKit(scale 1.5×)의 transform이 자식 anchor의 world transform을 추적하기 까다롭고, anchor의 world position·rotation이 spec의 "위치 + 향하는 각도"의 단일 진실원이어야 검증이 단순.
- **anchor의 destination transform**: anchor GameObject 자체의 root transform(`TeleportationAnchor.m_TeleportAnchorTransform`이 자기 자신을 가리킴). 별도 자식 transform을 두지 않음 → prefab 구성·검증 단순.
- **`m_MatchDirectionalInput` = false**: true이면 사용자 attach transform forward로 destination rotation을 덮어쓰므로 anchor의 각도가 무시된다. spec의 "anchor의 각도로 정확히" 요구와 충돌.
- **`m_FilterSelectionByHitNormal` = false**: anchor collider(BoxCollider) 윗면 normal과 anchor.transform.up 일치가 보장되지만, 본 plan에선 normal 필터링이 필요 없으므로 기본 false.
- **메시 렌더러·Layer**: NoTeleportZone과 동일하게 메시 없음, Layer Default(0). 시각 표현은 Editor에서 BoxCollider 와이어프레임 + `TeleportationAnchor.OnDrawGizmos`의 blue wire cube로 식별.
- **Tag**: Untagged.
- **Piano anchor 위치/각도 산출**: Piano world pos (0.091, -0.084, 1.016), rotation Y=180° → Piano forward = -Z. 사용자가 Piano를 마주 보고 연주하려면 anchor를 Piano forward 방향에 두고 사용자가 Piano 쪽(=+Z)을 향하게 해야 함. 거리 d=0.7m. anchor pos = (0.091, 0, 1.016 - 0.7) = **[0.091, 0, 0.316]**, rotation Y=**0°**. NoTeleportZone (Demo)(z 범위 [1.75, 3.25])과 충돌 없음.
- **DrumKit anchor 위치/각도 산출**: DrumKit world pos (-0.857, 0, -1.625), rotation 0 → DrumKit forward = +Z. 사용자가 DrumKit을 마주 보고 연주하려면 anchor를 DrumKit forward 방향(+Z)에 두고 사용자가 -Z를 향하게 해야 함. 거리 d=0.6m. anchor pos = (-0.857, 0, -1.625 + 0.6) = **[-0.857, 0, -1.025]**, rotation Y=**180°**. Plane 영역 안, NoTeleportZone/Table/PianoAnchor 모두와 충돌 없음.
- **두 anchor 영역 비겹침 검증**: PianoAnchor X 범위 [-0.409, 0.591], Z 범위 [-0.184, 0.816]. DrumKitAnchor X 범위 [-1.357, -0.357], Z 범위 [-1.525, -0.525]. 두 영역의 X/Z 모두 겹치지 않음. spec 마지막 Behavior 가정 만족.

### 제약

- 새 C# 스크립트 0개. 입력 액션 자산 미수정. LCH.prefab/VR Player.prefab 미수정. Piano·DrumKit 자체 미수정(anchor를 자식으로 부착하지 않음).
- `Plane`의 `TeleportationArea` 설정 보존. NoTeleportZone (Demo) 인스턴스 보존.
- anchor 반경 안에 들어가지 않는 자유 영역에서는 Plane 텔레포트가 그대로 동작해야 한다(선행 plan 미회귀).
- spec Out of Scope: 이미 anchor 반경 안에 들어가 있는 사용자에게 손 자세 보정·악기와 자동 정렬 등 추가 동작은 본 plan 책임 아님. 본 plan은 사용자(=리그) 위치·각도까지만.

## Verified Structural Assumptions

- SampleScene 한 단계 깊이 GameObject: `Plane`(origin), `Table`(pos [1.549, 0.193, 1.985], scale [1.75, 1.07, 1.0], BoxCollider center 0/size [1,1,1]), `Piano`(pos [0.091, -0.084, 1.016], rotation Y=180°), `DrumKit`(pos [-0.857, 0, -1.625], rotation 0, scale 1.5×), `NoTeleportZone (Demo)`(pos [0, 0, 2.5], scale [2, 1, 1.5], BoxCollider 부착), `VR Player`(root에 `TeleportationProvider` 부착), Lights·`XRInteractionManager`·`ChartAutoPlayer` 등. — `unity-scene-reader 보고 (2026-04-30, manage_scene get_hierarchy + find_gameobjects + 컴포넌트 조회)`.
- `Plane.TeleportationArea`: `m_TeleportTrigger: 0`(OnSelectExited), `m_InteractionLayers.value: -1`(Everything) — 선행 plan(`fix-push-immediate-teleport-trigger`, `no-teleport-zone-blocking-collider`) 결과 그대로 보존됨 — `unity-scene-reader 보고 (2026-04-30)`.
- `Piano`/`DrumKit` 모두 root에 직접 콜라이더 없음. Piano는 자식 `Piano/PianoInteraction/KeyboardBed`에 BoxCollider, DrumKit은 자식 드럼 피스(BassDrum/Snare 등)에 BoxCollider. 본 plan은 두 악기의 자식 콜라이더를 손대지 않으며, anchor 인스턴스는 두 악기와 별도의 scene root 자식으로 둠 — `unity-scene-reader 보고 (2026-04-30)`.
- `Assets/Locomotion/Prefabs/`는 `NoTeleportZone.prefab`만 존재. `InstrumentAnchor.prefab`은 미생성. 본 plan에서 신규 생성 — `unity-scene-reader 보고 (2026-04-30, manage_asset)`.
- `BaseTeleportationInteractable.TeleportTrigger` enum: `OnSelectExited=0` / `OnSelectEntered=1` / `OnActivated=2` / `OnDeactivated=3`. 본 plan 의도 값 = `0`(release 시 발동, NoTeleportZone과 동일 트리거 시점) — `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/Teleportation/BaseTeleportationInteractable.cs (2026-04-30)`.
- `MatchOrientation` enum: `WorldSpaceUp=0` / `TargetUp=1` / `TargetUpAndForward=2` / `None=3`. 본 plan 의도 값 = `2`(TargetUpAndForward — anchor.transform의 up·forward를 모두 매치해야 spec의 "위치 + 향하는 각도"가 충족됨) — `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/Teleportation/BaseTeleportationInteractable.cs (2026-04-30)`.
- `BaseTeleportationInteractable.m_MatchDirectionalInput`(bool): true이면 사용자 attach transform forward로 destination rotation을 덮어쓴다. 본 plan 의도 값 = `0`(false). false여야 anchor의 각도가 그대로 적용됨 — `Read 동일 .cs (2026-04-30)`.
- `TeleportationAnchor.m_TeleportAnchorTransform`(Transform 참조): null이면 `OnValidate`/`Reset`이 자기 자신의 transform으로 채움. 본 plan은 prefab의 root transform이 destination이 되도록 별도 자식을 두지 않음 — `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/Teleportation/TeleportationAnchor.cs (2026-04-30)`.

## Approach

### 1. `InstrumentAnchor.prefab` 자산 생성

`Assets/Locomotion/Prefabs/InstrumentAnchor.prefab`을 `manage_prefabs create`로 신규 생성한다. 기존 `Assets/Locomotion/Prefabs/` 폴더는 선행 plan에서 이미 만들어져 있어 추가 폴더 신설 없음.

prefab 구성:

- root GameObject 이름: `InstrumentAnchor`. Layer = Default(0). Tag = Untagged.
- 부착 컴포넌트(Transform 제외):
  - `BoxCollider` 1개:
    - `m_Center`: [0, 0.025, 0] — 인스턴스 transform.y=0이어도 자동으로 Plane(y=0)보다 ray origin에 가까워 ray가 anchor에서 멈춤.
    - `m_Size`: [1, 0.05, 1] — Y는 차단 두께. X·Z는 인스턴스 transform.scale로 조정.
    - `m_IsTrigger`: false.
  - `TeleportationAnchor` 1개:
    - `m_TeleportAnchorTransform`: 비워둠 → `OnValidate`가 자기 자신의 transform으로 자동 채움. (필요 시 prefab 직렬화에서 anchor GameObject 자기 자신 fileID로 명시.)
    - `m_TeleportTrigger`: 0 (OnSelectExited).
    - `m_MatchOrientation`: 2 (TargetUpAndForward).
    - `m_MatchDirectionalInput`: 0 (false).
    - `m_FilterSelectionByHitNormal`: 0 (false).
    - `m_UpNormalToleranceDegrees`: 30 (기본).
    - `m_TeleportationProvider`: 비워둠 → Awake에서 `ComponentLocatorUtility<TeleportationProvider>.TryFindComponent`가 VR Player root의 provider를 자동 찾음.
    - `m_InteractionLayers.value`: -1 (Everything) — `Plane.TeleportationArea`와 동일 매치 규칙.
- 메시 렌더러·MeshFilter·기타 컴포넌트 부착 안 함.

### 2. SampleScene Piano anchor 인스턴스 배치

`Assets/Scenes/SampleScene.unity`에 `InstrumentAnchor.prefab` 인스턴스 1개를 추가:

- 인스턴스 이름: `PianoAnchor`.
- world position: [0.091, 0, 0.316] — Piano(pos.x=0.091, pos.z=1.016, rotY=180° → forward=-Z) 앞 0.7m.
- world rotation: Euler [0, 0, 0] — 사용자가 Piano 방향(+Z)을 보도록.
- world scale: [1, 1, 1] — 1m × 1m anchor 영역(BoxCollider local size × scale).
- parent: scene root.

### 3. SampleScene DrumKit anchor 인스턴스 배치

같은 prefab 인스턴스 1개를 추가:

- 인스턴스 이름: `DrumKitAnchor`.
- world position: [-0.857, 0, -1.025] — DrumKit(pos.x=-0.857, pos.z=-1.625, rotY=0 → forward=+Z) 앞 0.6m.
- world rotation: Euler [0, 180, 0] — 사용자가 DrumKit 방향(-Z)을 보도록.
- world scale: [1, 1, 1].
- parent: scene root.

### 4. 검증

각 Acceptance Criteria 수행. 자동: prefab 생성·컴포넌트 부착·직렬화 grep·scene 인스턴스화·컴파일·인스턴스화 sanity. 수동: VR(또는 XR Device Simulator) Play 모드에서 라인을 두 anchor 안팎으로 이동시키며 끝점 snap 동작·release 시 정확한 위치/각도 도착·기본 Plane 텔레포트 미회귀·NoTeleportZone invalid 미회귀·Snap Turn 미회귀를 확인.

## Deliverables

- `Assets/Locomotion/Prefabs/InstrumentAnchor.prefab` — root에 `BoxCollider`(center [0,0.025,0], size [1,0.05,1], isTrigger false) + `TeleportationAnchor`(`m_TeleportTrigger:0`, `m_MatchOrientation:2`, `m_MatchDirectionalInput:0`, `m_FilterSelectionByHitNormal:0`, `m_TeleportAnchorTransform:` self) 부착. 메시 렌더러 없음. Layer Default. **신규.**
- `Assets/Scenes/SampleScene.unity` — `PianoAnchor`(pos [0.091, 0, 0.316], rot [0, 0, 0], scale [1,1,1]) + `DrumKitAnchor`(pos [-0.857, 0, -1.025], rot [0, 180, 0], scale [1,1,1]) 인스턴스 2개 추가(둘 다 `InstrumentAnchor.prefab` 인스턴스). **수정.**

(새 C# 스크립트 없음. LCH.prefab / VR Player.prefab / 입력 액션 자산 / Plane TeleportationArea / NoTeleportZone.prefab·인스턴스 / Piano / DrumKit 미수정.)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Locomotion/Prefabs/InstrumentAnchor.prefab` 자산이 존재하고 root GameObject에 정확히 `Transform` + `BoxCollider` + `TeleportationAnchor` 세 개의 컴포넌트만 부착되어 있다(메시 렌더러·MeshFilter·기타 컴포넌트 0개) — `manage_prefabs get_hierarchy` 또는 텍스트 검사.
- [ ] `[auto-hard]` 같은 prefab의 `BoxCollider` 직렬화 값: `m_Center: {x:0, y:0.025, z:0}`, `m_Size: {x:1, y:0.05, z:1}`, `m_IsTrigger: 0`.
- [ ] `[auto-hard]` 같은 prefab의 `TeleportationAnchor` 직렬화 값을 grep으로 단일 매치한다: `m_TeleportTrigger: 0`(OnSelectExited), `m_MatchOrientation: 2`(TargetUpAndForward), `m_MatchDirectionalInput: 0`, `m_FilterSelectionByHitNormal: 0`. 인덱스가 인스펙터 표기와 어긋나는 케이스를 잡기 위해 4개 필드 모두 인덱스 값으로 직접 검증.
- [ ] `[auto-hard]` 같은 prefab의 `TeleportationAnchor.m_TeleportAnchorTransform`이 anchor GameObject 자기 자신의 transform fileID를 가리키며 null이 아니다.
- [ ] `[auto-hard]` `Assets/Scenes/SampleScene.unity`에 `PianoAnchor`라는 이름의 GameObject 인스턴스가 정확히 1개 존재하고, 그 GameObject의 PrefabInstance source가 `InstrumentAnchor.prefab`의 GUID를 가리킨다.
- [ ] `[auto-hard]` `PianoAnchor` 인스턴스의 world transform이 position [0.091, 0, 0.316], rotation Euler [0, 0, 0], scale [1, 1, 1]에 일치한다(부동소수점 허용 오차 ±0.001).
- [ ] `[auto-hard]` `Assets/Scenes/SampleScene.unity`에 `DrumKitAnchor`라는 이름의 GameObject 인스턴스가 정확히 1개 존재하고, PrefabInstance source가 `InstrumentAnchor.prefab`의 GUID를 가리킨다.
- [ ] `[auto-hard]` `DrumKitAnchor` 인스턴스의 world transform이 position [-0.857, 0, -1.025], rotation Euler [0, 180, 0], scale [1, 1, 1]에 일치한다(부동소수점 허용 오차 ±0.001).
- [ ] `[auto-hard]` `PianoAnchor` 영역(X [-0.409, 0.591], Z [-0.184, 0.816])과 `DrumKitAnchor` 영역(X [-1.357, -0.357], Z [-1.525, -0.525])이 X/Z 모두 겹치지 않는다 — spec 마지막 Behavior 가정 검증.
- [ ] `[auto-hard]` SampleScene `Plane` GameObject의 `TeleportationArea` 직렬화가 그대로 보존된다(`m_TeleportTrigger: 0`, `m_InteractionLayers.value: -1`, 부착 상태 유지) — 선행 plan 결과 미회귀.
- [ ] `[auto-hard]` SampleScene `NoTeleportZone (Demo)` 인스턴스가 그대로 존재하고 transform/PrefabInstance source가 보존된다(pos [0, 0, 2.5], scale [2, 1, 1.5]) — 선행 plan 결과 미회귀.
- [ ] `[auto-hard]` `Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab`과 `Assets/Characters/Prefabs/VR Player.prefab`의 선행 plan 결과(Teleport Interactor 자식·`ControllerInputActionManager` 부착·`m_SelectInput` 두 슬롯 hookup·`TeleportationProvider` 부착·`DynamicMoveProvider` 미부착)가 그대로 보존된다.
- [ ] `[auto-hard]` SampleScene의 `Piano`/`DrumKit` GameObject 자체 transform·자식 콜라이더가 본 plan 적용 전과 동일하다(악기 본체 미수정).
- [ ] `[auto-hard]` Unity Editor가 컴파일 오류 없이 Domain Reload를 마친다 — `read_console` error/exception 0건.
- [ ] `[manual-hard]` Play 모드 + VR(또는 XR Device Simulator)에서 텔레포트 라인을 `PianoAnchor` 영역 위로 가져가면 라인 끝점이 `PianoAnchor` 자리(piano 앞 [0.091, 0, 0.316])로 즉시 snap되고 일반 Plane hit-point 끝점과 시각적으로 구별된다.
- [ ] `[manual-hard]` 같은 상태에서 release하면 카메라 리그가 `PianoAnchor`의 위치([0.091, 0, 0.316])와 각도(Y=0°, Piano를 정면으로)로 정확히 텔레포트된다.
- [ ] `[manual-hard]` Play 모드에서 텔레포트 라인을 `DrumKitAnchor` 영역 위로 가져가고 release하면 카메라 리그가 anchor의 위치([-0.857, 0, -1.025])와 각도(Y=180°, DrumKit을 정면으로)로 정확히 텔레포트된다.
- [ ] `[manual-hard]` 라인 끝을 두 anchor 영역 어느 쪽에도 들어가지 않는 Plane의 빈 지점에 두고 release하면 그 hit-point 좌표로 텔레포트되는 기본 동작이 그대로 유지된다(선행 plan 미회귀).
- [ ] `[manual-hard]` 라인이 `NoTeleportZone (Demo)` 위를 가리킬 때 invalid color 표현·release 시 미이동 동작이 본 plan 적용 전과 동일하다(선행 plan 미회귀).
- [ ] `[manual-hard]` 오른손 thumbstick Snap Turn 동작이 본 plan 적용 전과 동일하다(회전 동작 미회귀).
- [ ] `[manual-hard]` `PianoAnchor` 영역을 벗어나 라인 끝이 다시 일반 Plane 위로 이동하면 라인 끝점이 anchor 표현에서 즉시 일반 hit-point 표현으로 복구된다(spec Behavior 두 번째 케이스).

## Out of Scope

- anchor 영역에서 라인 색을 invalid/일반 valid와 또 다른 *별도 색*으로 구분하는 추가 시각 처리. 본 plan은 endpoint snap만으로 spec "구별되는 형태"를 1차 충족. 부족하다고 사람이 판정하면 후속 plan으로 분기.
- 한 악기에 여러 anchor를 두거나 후보 anchor 중 사용자가 선택하는 형태(spec Out of Scope, OQ).
- 이동형 / 휴대형 악기 anchor.
- anchor 도착 후 사용자 손 자세를 악기 연주에 맞춰 추가 보정(spec Out of Scope).
- 운영 씬 외 다른 씬(`VirtualMusicStudio.unity`, `asad.unity` 등)의 anchor 동기화. SampleScene 단일.
- Piano·DrumKit 자체에 콜라이더 추가/anchor를 자식으로 부착하는 형태. 본 plan은 scene root 자식으로 분리 배치.
- anchor 반경 가시화(메시 렌더러·gizmo 외 시각). 씬 작업자는 Editor의 BoxCollider 와이어프레임 + `TeleportationAnchor.OnDrawGizmos`의 blue wire cube로 식별.

## Notes

- `MatchOrientation = TargetUpAndForward`일 때 `BaseTeleportationInteractable.GetReticleDirection`이 reticle.up = anchor.transform.up, reticle.forward = anchor.transform.forward로 채운다(소스 라인 485-488). 즉 라인 끝의 reticle이 anchor 자리에 anchor의 forward 방향으로 정렬되어 표시 — 본 plan에서 spec What 2의 "구별되는 형태"가 자동 충족되는 메커니즘.
- 씬 작업자가 추가 악기 anchor를 두려면 `InstrumentAnchor.prefab`을 끌어다 놓고 transform.position(악기 정면 일정 거리)·transform.rotation(사용자가 악기를 보도록 Y rotation)·transform.scale(영역 크기)을 조정. anchor의 `m_TeleportAnchorTransform`은 자기 자신을 가리키므로 prefab 인스턴스 transform이 곧 텔레포트 destination이다.
- 두 anchor 영역의 X/Z 비겹침 검증을 본 plan AC에 포함한 것은 spec 마지막 Behavior("두 악기의 anchor 반경이 사용 시점에 겹치지 않도록 씬이 구성됨")의 자동 회귀 방지 게이트 역할. 추후 anchor 추가 시 같은 검증 패턴을 plan AC에 그대로 복제 가능.
- spec OQ "anchor 반경 기본 크기·시각 표현"은 본 plan의 결정(1m × 1m BoxCollider + XRI 기본 reticle snap)으로 1차 닫는다. 사람이 manual-hard 검증에서 시각 표현이 부족하다고 판정하면 후속 plan(예: anchor 색 분기 LineVisual 커스텀, anchor용 reticle prefab 도입)으로 분기.

## Handoff

- **`Assets/Locomotion/Prefabs/InstrumentAnchor.prefab`**: root에 `BoxCollider`(center [0, 0.025, 0], size [1, 0.05, 1], isTrigger false) + `TeleportationAnchor`(`m_TeleportTrigger:0`, `m_MatchOrientation:2`, `m_MatchDirectionalInput:0`, `m_FilterSelectionByHitNormal:0`, `m_TeleportAnchorTransform:` self) 부착. 메시 렌더러 없음. Layer Default. 추가 악기 anchor가 필요하면 이 prefab 인스턴스를 만들고 transform.position·rotation·scale만 조정.
- **SampleScene anchor 인스턴스**: `PianoAnchor`(pos [0.091, 0, 0.316], rot Y=0°), `DrumKitAnchor`(pos [-0.857, 0, -1.025], rot Y=180°). 둘 다 scene root 자식. 후속 plan에서 anchor 추가/이동이 필요하면 이 둘을 참고로 같은 prefab의 다른 인스턴스를 만들면 된다.
- **시각 구분 메커니즘**: `MatchOrientation=TargetUpAndForward`로 reticle이 anchor 자리·forward로 snap. 후속에서 색 분기/마커가 필요해지면 `XRInteractorLineVisual`의 valid color slot 또는 reticle prefab에 anchor 전용 변형을 두고 `XRRayInteractor`가 hit한 interactable 타입에 따라 분기시키는 패턴이 자연스럽다.
- **Plane TeleportationArea 보존**: `m_TeleportTrigger: 0`, `m_InteractionLayers.m_Bits: 4294967295`(=Everything). 본 plan에서 손대지 않음.
- **두 anchor 영역 X/Z 비겹침**: PianoAnchor X [-0.409, 0.591]/Z [-0.184, 0.816], DrumKitAnchor X [-1.357, -0.357]/Z [-1.525, -0.525]. spec 마지막 Behavior 가정 만족. 추후 anchor 추가 시 모든 anchor 페어에 대해 같은 비겹침이 유지돼야 spec 가정이 깨지지 않음.
- **Layer 결정**: anchor는 Default Layer. 후속에서 anchor 전용 Layer를 도입해 raycast 분기를 정밀화하고 싶다면 `XRRayInteractor.raycastMask`도 같이 수정.
