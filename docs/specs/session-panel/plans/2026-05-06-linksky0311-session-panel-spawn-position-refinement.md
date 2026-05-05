# Session Panel Spawn Position Refinement

**Linked Spec:** [`01-anchoring.md`](../specs/01-anchoring.md)
**Status:** `Ready`

## Goal

세션 패널의 핀치 호출 spawn 위치를 (a) ghost hand `L_Wrist` 정밀 wire, (b) VR HMD 미추적 시 Main Camera fallback, (c) `pinchSpawnLocalOffset` 사용성 기준 재조정 — 세 가지로 정돈한다.

## Context

선행 plan [`2026-05-05-linksky0311-session-panel-anchoring.md`](../../_archive/session-panel/plans/2026-05-05-linksky0311-session-panel-anchoring.md)이 패널 토글·anchor·`IActiveInstrumentProvider` 인터페이스 골격을 박제했고, 그 plan의 manual 검증에서 다음이 확인됐다:

- **VR 미연결 Editor에서 `LeftHandTrackingHandRoot`가 default position(0,0,0 근처)에 머물러** 패널이 바닥에 노출되어 사용자가 볼 수 없는 양상이 manual #9에서 관찰됨. 코드 로직은 정상이지만 Editor 검증이 막힘.
- 선행 plan은 `leftHandSpawnTransform`을 `LeftHandTrackingHandRoot` 자체에 임시 wire했으나, 실제 손바닥 anchor는 자식 ghost hand의 `L_Wrist`임이 본 plan의 unity-scene-reader 보고로 박제됐다.
- `pinchSpawnLocalOffset` default `(0, 0.05, 0.15)`도 wrist 후보 변경에 맞춰 재조정 필요.

선행 plan은 이 3건을 의도적으로 Out of Scope로 미뤘고(`핀치 호출 시 패널 위치의 정밀 튜닝. 본 plan의 manual 검증은 "왼손 손바닥 근처에 합리적으로 보임" 정도까지만 확인하고, 실제 사용성 튜닝은 후속 plan(또는 spec resolve)에서 진행`), 본 plan이 그 후속이다.

본 plan은 `SessionPanelController.cs` 코드 변경(분기 로직 추가) + `SampleScene.unity`의 SerializeField 재wire로 끝난다. `IActiveInstrumentProvider` production wire-up, 핀치 binding swap, dummy stub 제거 등은 본 plan 범위 밖이다.

## Verified Structural Assumptions

- VR Player의 왼손 손바닥 anchor: `Assets/Characters/Prefabs/VR Player.prefab`의 `Camera Offset/Hands/Left/LeftHandTrackingHandRoot/LeftHandTrackingGhostHand/L_Wrist`. ghost hand에 `XRHandTrackingEvents`·`XRHandSkeletonDriver`·`XRHandMeshController` 부착. `L_Wrist` 자체는 정적 anchor + 자식 `L_Palm`(metacarpal 5개) 보유. — `unity-scene-reader 보고 (2026-05-06)`
- HMD fallback anchor: `VR Player/Camera Offset/Main Camera` (MainCamera tag). 컴포넌트 `Transform`/`Camera`/`AudioListener`/**`TrackedPoseDriver`**/`UniversalAdditionalCameraData`. — `unity-scene-reader 보고 (2026-05-06)`
- `TrackedPoseDriver`는 Main Camera에만 부착돼 있고 `LeftHandTrackingHandRoot`에는 없음. hand tracking은 `XRHandTrackingEvents`/`XRHandSkeletonDriver`가 담당. — `unity-scene-reader 보고 (2026-05-06)`
- `UnityEngine.InputSystem.XR.TrackedPoseDriver` public API: `bool trackingType`/`PoseDataSource` 외에, runtime에서 추적 활성 여부 detect 신호로 활용 가능한 후보는 (a) Main Camera Transform의 `localPosition` 변동(추적 활성 시 0이 아니게 됨), (b) `XR Origin`의 active state 등이며, 본 plan은 단순 (a) 신호 — Main Camera localPosition `sqrMagnitude < epsilon` 이면 미추적으로 판정 — 으로 처리한다. — `unity-scene-reader 보고 (2026-05-06)` + 본 plan 결정
- 5개 `L_Wrist` 분포: tracking ghost(본 plan target) / controller ghost / physics hand / play hand / drum_stick — wire 시 path로 정확히 구분해야 함. — `unity-scene-reader 보고 (2026-05-06)`

## Approach

1. **`SessionPanelController.cs` SerializeField 추가**
   - `[SerializeField] private Transform headFallbackTransform;` — Main Camera Transform. VR 미추적 시 spawn anchor.
   - `[SerializeField] private Vector3 fallbackSpawnLocalOffset = new Vector3(0f, 0f, 0.5f);` — Main Camera 정면 0.5m 기본.
   - `[SerializeField] private float trackingEpsilon = 0.001f;` — Main Camera localPosition sqrMagnitude 임계.
   - 기존 `pinchSpawnLocalOffset` default를 `(0, 0.08, 0.1)`로 재조정 — `L_Wrist` 기준 손바닥 위·앞.

2. **`ResolveSpawnContext` 헬퍼 메서드 신설**

   ```csharp
   private (Transform anchor, Vector3 offset) ResolveSpawnContext()
   {
       if (headFallbackTransform != null
           && headFallbackTransform.localPosition.sqrMagnitude < trackingEpsilon
           && leftHandSpawnTransform != null)
       {
           // VR 미추적 — Main Camera fallback
           return (headFallbackTransform, fallbackSpawnLocalOffset);
       }
       if (leftHandSpawnTransform != null)
           return (leftHandSpawnTransform, pinchSpawnLocalOffset);
       if (headFallbackTransform != null)
           return (headFallbackTransform, fallbackSpawnLocalOffset);
       return (transform, Vector3.zero);
   }
   ```

   tracking 신호: Main Camera `localPosition` (Camera Offset 기준)이 epsilon 미만이면 미추적. VR HMD 추적이 시작되면 Main Camera localPosition이 head pose에 따라 갱신되어 0이 아닌 값이 됨 — 직관적 신호.

3. **`PositionAtWrist` 분기 적용**

   ```csharp
   private void PositionAtWrist()
   {
       var (anchor, offset) = ResolveSpawnContext();
       _panelInstance.transform.position = anchor.TransformPoint(offset);
       if (_mainCamera != null)
       {
           Vector3 camForward = _mainCamera.transform.forward;
           camForward.y = 0f;
           if (camForward.sqrMagnitude > 0.001f)
               _panelInstance.transform.rotation = Quaternion.LookRotation(camForward);
       }
   }
   ```

4. **SampleScene wire 갱신** — `unity-asset-edit` skill 절차로 `manage_components`를 사용해 다음 SerializeField를 SessionPanelManager의 SessionPanelController에 wire:
   - `leftHandSpawnTransform` ← `VR Player/Camera Offset/Hands/Left/LeftHandTrackingHandRoot/LeftHandTrackingGhostHand/L_Wrist` Transform (현재 `LeftHandTrackingHandRoot`에서 변경)
   - `headFallbackTransform` ← `VR Player/Camera Offset/Main Camera` Transform (신규 wire)

5. **이름 정리(선택)** — `pinchSpawnLocalOffset`/`fallbackSpawnLocalOffset` 두 SerializeField가 inspector에 모두 노출되며, plan 본문 deliverables 단계에서 inspector 라벨이 자명하도록 SerializeField 이름 그대로 유지(rename 안 함).

## Deliverables

- `Assets/SessionPanel/Scripts/SessionPanelController.cs` — 수정. `headFallbackTransform`/`fallbackSpawnLocalOffset`/`trackingEpsilon` 신규 SerializeField + `ResolveSpawnContext` 헬퍼 메서드 + `PositionAtWrist` 분기 적용 + `pinchSpawnLocalOffset` default `(0, 0.08, 0.1)`로 재조정.
- `Assets/Scenes/SampleScene.unity` — `SessionPanelManager.SessionPanelController` SerializeField 재wire: `leftHandSpawnTransform` → ghost hand `L_Wrist`, `headFallbackTransform` → Main Camera Transform 신규.

## Acceptance Criteria

- [ ] `[auto-hard]` `SessionPanelController.cs` 컴파일 통과 (Editor 도메인 reload 후 `read_console` 컴파일 에러 0).
- [ ] `[auto-hard]` `SampleScene.unity`의 `SessionPanelManager.SessionPanelController.leftHandSpawnTransform` fileID가 `LeftHandTrackingGhostHand/L_Wrist` Transform을 가리킴 (grep으로 fileID 확인 + path 매칭).
- [ ] `[auto-hard]` `SampleScene.unity`의 `SessionPanelManager.SessionPanelController.headFallbackTransform` fileID가 Main Camera Transform을 가리킴.
- [ ] `[auto-hard]` `pinchSpawnLocalOffset` 직렬화 default 값이 `(0, 0.08, 0.1)`로 변경됨 (grep으로 SampleScene 또는 prefab 직렬화 값 확인 — scene wire가 default를 덮어쓴다면 SerializeField default가 코드에서 변경됐는지 grep).
- [ ] `[manual-hard]` SampleScene Editor Play에서 VR HMD 미연결 상태로 `PanelToggle` 발화 → 패널이 **Main Camera 정면 0.5m fallback 위치**에 노출 (바닥 아님). 화면에서 회색 backdrop + 청록(Volume) 패널이 카메라 시야 안에 정상 표시.
- [ ] `[manual-hard]` VR HMD 연결 상태(또는 XR Device Simulator로 head·hand 추적 시뮬)로 `PanelToggle` 발화 → 패널이 **`L_Wrist` 위쪽 손바닥 근처**에 노출. wrist를 따라 자연스럽게 위치 갱신.
- [ ] `[manual-hard]` Inspector에서 `pinchSpawnLocalOffset` 또는 `fallbackSpawnLocalOffset`을 임의로 조정 → 다음 PanelToggle 발화 시 그 offset 변경이 즉시 반영(SerializeField가 inspector에 노출되고 사용자가 직관적으로 튜닝 가능).

## Out of Scope

- `IActiveInstrumentProvider` production 구현/wire-up — 다른 작업자가 진행 중이며 별도 plan.
- 왼손 핀치 입력으로 `PanelToggle` binding swap — [`hands/specs/03-left-pinch-gesture.md`](../../hands/specs/03-left-pinch-gesture.md) plan에서.
- `DummyActiveInstrumentProvider`/`DummyActiveInstrument`/`TestInstrumentTarget`/`SessionPanel.Tests` asmdef 제거 — production wire-up plan이 일괄 처리.
- 시작 메뉴/볼륨 섹션 컨텐츠 — [`02-start-menu-section.md`](../specs/02-start-menu-section.md) / [`03-volume-section.md`](../specs/03-volume-section.md) plan.
- `Piano/PanelAnchor`/`DrumKit/PanelAnchor` local offset 정밀 튜닝 — 별도 후속 plan 또는 spec resolve.
- 패널 시각 디자인(컨텐츠/색·폰트/애니메이션). 본 plan은 placeholder Image 그대로 둔다.

## Notes

- `Main Camera localPosition.sqrMagnitude < trackingEpsilon`을 미추적 신호로 사용하는 휴리스틱은 Camera Offset이 (0,0,0)에 정렬돼 있고 head 추적이 안 되면 Main Camera가 default position에 머문다는 가정에 의존한다. 사용자 환경에서 Camera Offset이 다르게 설정돼 있으면 이 휴리스틱이 어긋날 수 있음 — 그런 경우 후속 plan에서 `XROrigin.HasTrackingOriginFlags` 또는 `InputDevice.TryGetFeatureValue(CommonUsages.devicePosition, ...)` 같은 명시 신호로 교체 검토.
- `trackingEpsilon` default는 0.001f. localPosition sqrMagnitude는 m² 단위라 약 ~3cm 미만이면 미추적 판정. 사용자가 inspector에서 자유 조정 가능.

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신 -->
