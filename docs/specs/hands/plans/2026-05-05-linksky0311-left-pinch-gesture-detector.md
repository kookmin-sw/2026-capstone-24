# Left Pinch Gesture Detector

**Linked Spec:** [`03-left-pinch-gesture.md`](../specs/03-left-pinch-gesture.md)
**Status:** `Ready`

## Goal

왼손 hand-tracking에서 thumb·index 핀치를 검출해 핀치 시작/해제 C# 이벤트를 발화하는 컴포넌트를 만든다. 시야 가시성 + 손바닥 법선 + 좌측 손 악기 미잡음 + hand-tracking 활성 4가지 게이트를 모두 만족할 때만 발화한다. anchoring plan이 만든 `SessionPanelController`가 컨트롤러 InputAction과 본 컴포넌트 이벤트를 둘 다 구독해 같은 토글 핸들러로 OR.

## Context

hands 피처 `03-left-pinch-gesture` sub-spec의 첫 plan이다. spec resolve(2026-05-05) 결정 4건이 본 plan Approach의 기둥:

- **"마주보는 방향" 정의** = 왼손이 사용자 시야 안 + 손바닥 법선이 사용자 쪽을 향함 (AND).
- **거리 임계 + 히스테리시스** = Schmitt trigger (시작 임계 < 해제 임계) + 최소 유지 시간으로 채터링 방지.
- **컨트롤러 동등 입력 채널** = 좌측 컨트롤러 메뉴 버튼 (anchoring plan stub과 동일). 본 plan은 InputAction 자산을 건드리지 않고 hand-tracking 채널만 추가.
- **더블 핀치는 본 sub-spec 범위 외**. 본 plan은 single 핀치 시작/해제 이벤트만.

본 plan이 의존하는 외부 산출물 (선행 적용 가정):

- **anchoring plan(`docs/specs/session-panel/plans/2026-05-05-linksky0311-session-panel-anchoring.md`)**: `Assets/Settings/Input/InputSystem_Actions.inputactions`의 `Player` map에 `PanelToggle` Action 추가(컨트롤러 메뉴/secondary 버튼 stub) + `Assets/SessionPanel/Scripts/SessionPanelController.cs`. 본 plan은 SessionPanelController에 `leftPinchDetector` SerializeField 1개 + 구독 1줄 추가.
- **XR Hands 1.7.3 패키지** (이미 설치, sample 코드 `Assets/Samples/XR Hands/1.7.3/HandVisualizer/Scripts/HandProcessor.cs` 활용 가능).

소비측 통합 디자인: spec body의 "소비측은 입력 채널을 구분하지 않는다"는 의도를 만족하는 가장 단순한 디자인은 **SessionPanelController가 두 채널을 모두 구독해 같은 핸들러로 OR**다. 코드상 두 구독이지만 의미는 동일. 별도 어댑터·virtual InputDevice 도입은 본 plan 책임 밖.

## Verified Structural Assumptions

- 메인 카메라 경로: `VR Player/Camera Offset/Main Camera` (Camera + TrackedPoseDriver 부착). 본 plan은 `Transform.forward`를 시야 방향 reference로 사용. — `unity-scene-reader 보고 (2026-05-05)`
- 왼손 hand-tracking root: `VR Player/Camera Offset/Hands/Left/LeftHandTrackingHandRoot` (`XRInteractionGroup` 부착). 자식 `LeftHandTrackingGhostHand`에 `XRHandTrackingEvents` + `XRHandSkeletonDriver` + `XRHandMeshController` 부착. — `unity-scene-reader 보고 (2026-05-05)`
- `XRHandSkeletonDriver.jointTransformReferences[]`에 `L_Palm`, `L_ThumbTip`, `L_IndexTip` joint transform이 명시 등록되어 있다. 본 plan은 이 transform들을 inspector로 직접 wire해서 핀치 거리(`Distance(thumbTip, indexTip)`) + 손바닥 법선(palm joint의 axis 한 방향)을 read한다. — `unity-scene-reader 보고 (2026-05-05)`
- 왼손 grab 판정: `LeftControllerHandRoot`와 `LeftHandTrackingHandRoot` 양쪽에 `XRInteractionGroup` 부착. 본 plan은 `IXRSelectInteractor.hasSelection` polling으로 "왼손에 어떤 악기도 잡혀 있지 않음"을 판정한다. — `unity-scene-reader 보고 (2026-05-05)`
- 컨트롤러 root와 hand-tracking root는 SampleScene 인스턴스에서 동시 active 상태다. 본 plan은 `XRHandTrackingEvents.handIsTracked`(또는 `XRHand.isTracked`) polling으로 hand-tracking 모드일 때만 검출하도록 게이트한다. 컨트롤러 모드에서 hand 추적 데이터가 stale/invalid이면 `handIsTracked == false`이므로 false positive가 자연 차단된다. — `unity-scene-reader 보고 (2026-05-05)`
- `Assets/Settings/Input/InputSystem_Actions.inputactions`의 `Player` map에는 본 plan 작성 시점 기준 PanelToggle Action 미존재(anchoring plan이 신규 추가 예정). 본 plan은 InputAction 자산을 수정하지 않으므로 anchoring plan과의 충돌 없음. — `unity-scene-reader 보고 (2026-05-05)`
- XR Hands sample(`Assets/Samples/XR Hands/1.7.3/HandVisualizer/Scripts/HandProcessor.cs` / `HandVisualizer.cs`)에 thumb·index tip joint 접근 패턴이 있어 본 plan의 검출 로직 구현 시 참고 가능. palm joint normal axis(palm joint transform의 어느 축이 손바닥 법선인지)는 sample 코드와 비교해 plan-implementer가 박제. — `Read Assets/Samples/XR Hands/1.7.3/HandVisualizer/Scripts/HandProcessor.cs (2026-05-05)`

## Approach

1. **`LeftPinchDetector` 컴포넌트 신설** — `Assets/Hands/Scripts/LeftPinchDetector.cs`. MonoBehaviour. 노출:

   ```csharp
   [SerializeField] XRHandTrackingEvents leftHandEvents;     // LeftHandTrackingGhostHand의 컴포넌트
   [SerializeField] Transform palmTransform;                 // L_Palm joint
   [SerializeField] Transform thumbTipTransform;             // L_ThumbTip joint
   [SerializeField] Transform indexTipTransform;             // L_IndexTip joint
   [SerializeField] Camera headCamera;                       // Main Camera (null이면 Camera.main fallback)
   [SerializeField] XRInteractionGroup leftHandInteractionGroup;  // 왼손 grab 판정
   [SerializeField, Tooltip("팜 normal로 사용할 axis (palm joint local)")]
   PalmNormalAxis palmNormalAxis = PalmNormalAxis.Up;       // enum: Up/Down/Right/Left/Forward/Back

   [Header("Thresholds")]
   [SerializeField, Min(0f)] float pinchStartDistance   = 0.025f;
   [SerializeField, Min(0f)] float pinchReleaseDistance = 0.04f;
   [SerializeField, Range(0f, 1f)] float palmFacingDot  = 0.5f;  // cos(60°) 정도
   [SerializeField, Min(0f)] float minHoldSeconds       = 0.05f;

   public event System.Action PinchStarted;
   public event System.Action PinchEnded;

   bool isPinched;
   float candidateStartedAt = -1f;
   ```

2. **검출 로직 (`Update` 또는 `XRHandTrackingEvents.poseUpdated` 구독)** — 게이트 4개 모두 통과해야 발화 후보:
   - **g1 hand-tracking active**: `leftHandEvents.handIsTracked == true`.
   - **g2 시야 안**: `Camera.WorldToViewportPoint(palmTransform.position)`의 x/y가 `[0,1]` 안 + z > 0.
   - **g3 손바닥 법선**: `palmNormalAxis`로 선택한 palm joint local axis를 world로 변환한 벡터를 `palmNormal`로, `Vector3.Dot(palmNormal, -headCamera.transform.forward) >= palmFacingDot`.
   - **g4 왼손 미잡음**: `leftHandInteractionGroup.hasSelection == false` (`IXRSelectInteractor` 인터페이스를 통한 polling. group이 null이면 게이트 통과로 간주).

   거리: `float d = Vector3.Distance(thumbTipTransform.position, indexTipTransform.position)`.

   상태 전이 (Schmitt trigger + 최소 유지):

   - `!isPinched && d <= pinchStartDistance && allGatesPass`:
     - 후보 시작 시점이 미기록이면 `candidateStartedAt = Time.time`.
     - `Time.time - candidateStartedAt >= minHoldSeconds`이면 `isPinched = true; PinchStarted?.Invoke()`.
   - `!isPinched && (d > pinchStartDistance || !allGatesPass)`: `candidateStartedAt = -1f` (후보 리셋).
   - `isPinched && (d >= pinchReleaseDistance || !allGatesPass)`: `isPinched = false; candidateStartedAt = -1f; PinchEnded?.Invoke()`.

3. **palm normal axis 박제** — XR Hands sample의 `HandProcessor.cs` 또는 `HandVisualizer.cs`에서 palm joint transform의 어느 axis가 손바닥 normal인지 확인. 일반적으로 `palm.up`이지만 SDK·헤드셋에 따라 다를 수 있어 `PalmNormalAxis` enum SerializeField로 두어 inspector에서 조정 가능하게 한다. plan-implementer는 sample 패턴과 일치하는 default를 코드에 둔다.

4. **`SessionPanelController` 구독 추가** — `Assets/SessionPanel/Scripts/SessionPanelController.cs` (anchoring plan 산출물) 수정:

   ```csharp
   [SerializeField] LeftPinchDetector leftPinchDetector; // 추가

   void OnEnable() {
       panelToggleAction.action.performed += OnToggle;
       if (leftPinchDetector != null)
           leftPinchDetector.PinchStarted += OnHandTrackingPinch;
   }
   void OnDisable() {
       panelToggleAction.action.performed -= OnToggle;
       if (leftPinchDetector != null)
           leftPinchDetector.PinchStarted -= OnHandTrackingPinch;
   }
   void OnHandTrackingPinch() => HandlePanelToggle();
   void OnToggle(InputAction.CallbackContext _) => HandlePanelToggle();
   ```

   기존 `OnToggle` 본문을 `HandlePanelToggle()`로 추출하고, hand-tracking 핀치도 같은 메서드를 호출. PinchEnded는 본 plan에선 토글 동작에 영향 없음.

5. **scene 와이어링** — SampleScene의 VR Player 인스턴스 또는 anchoring plan의 `SessionPanelManager` GameObject(둘 중 자연스러운 위치)에 `LeftPinchDetector` 컴포넌트 1개 부착. inspector wire:
   - `leftHandEvents` ← `Camera Offset/Hands/Left/LeftHandTrackingHandRoot/LeftHandTrackingGhostHand`의 `XRHandTrackingEvents`
   - `palmTransform` / `thumbTipTransform` / `indexTipTransform` ← `XRHandSkeletonDriver.jointTransformReferences[]`의 `L_Palm` / `L_ThumbTip` / `L_IndexTip`
   - `headCamera` ← `Camera Offset/Main Camera`
   - `leftHandInteractionGroup` ← `LeftControllerHandRoot`의 `XRInteractionGroup` (또는 `LeftHandTrackingHandRoot`의 같은 컴포넌트 — 둘 중 좌측 손 grab 신호가 더 정확하게 흘러들어오는 쪽을 wire)
   - `SessionPanelController.leftPinchDetector` ← 같은 LeftPinchDetector 인스턴스

## Deliverables

- `Assets/Hands/Scripts/LeftPinchDetector.cs` — 신설.
- `Assets/SessionPanel/Scripts/SessionPanelController.cs` (anchoring plan 산출물) — 수정. `leftPinchDetector` SerializeField + OnEnable/Disable 구독 + `HandlePanelToggle()` 추출.
- `Assets/Scenes/SampleScene.unity` — VR Player 인스턴스 산하(또는 SessionPanelManager) `LeftPinchDetector` 컴포넌트 부착 + 6개 SerializeField inspector wire + `SessionPanelController.leftPinchDetector` wire.

## Acceptance Criteria

- [ ] `[auto-hard]` `LeftPinchDetector.cs`와 수정된 `SessionPanelController.cs` 컴파일 통과 (`read_console` 컴파일 에러 0).
- [ ] `[auto-hard]` `LeftPinchDetector` 컴포넌트가 `event System.Action PinchStarted`와 `event System.Action PinchEnded`를 정확히 노출 (grep 단일 매치).
- [ ] `[auto-hard]` `LeftPinchDetector`의 SerializeField 6개(`leftHandEvents`, `palmTransform`, `thumbTipTransform`, `indexTipTransform`, `headCamera`, `leftHandInteractionGroup`) + 임계 4개(`pinchStartDistance`, `pinchReleaseDistance`, `palmFacingDot`, `minHoldSeconds`) + axis enum SerializeField가 코드 grep으로 모두 확인됨.
- [ ] `[auto-hard]` SampleScene에 `LeftPinchDetector` 컴포넌트가 정확히 1개 존재 + 6개 reference SerializeField가 모두 non-null로 직렬화됨 (`{fileID: 0}` 0건).
- [ ] `[auto-hard]` SampleScene의 `SessionPanelController`에 `leftPinchDetector` 직렬화 reference가 위 LeftPinchDetector 인스턴스를 가리킴 (단일 매치).
- [ ] `[auto-hard]` SampleScene Editor 로드 시 콘솔 에러·예외 0건 (직렬화 sanity).
- [ ] `[manual-hard]` Editor Play (anchoring plan 적용 후 가정) + Quest hand-tracking 모드 + 왼손에 어떤 악기도 잡지 않은 상태 + 왼손이 시야 안 + 손바닥이 카메라 쪽 → thumb·index 핀치 시 `PinchStarted`가 1회 발화되고 SessionPanel이 호출됨 (anchoring 동작과 일치).
- [ ] `[manual-hard]` 위 시나리오에서 손가락을 다시 띄움. 0.04m(release 임계)에 도달하기 전엔 `PinchEnded` 미발화, 도달 시 1회 발화 (Schmitt trigger 검증).
- [ ] `[manual-hard]` 왼손에 drum stick을 잡은 상태에서 thumb·index 핀치 모양 → `PinchStarted` 발화 0건 (g4 게이트).
- [ ] `[manual-hard]` 왼손이 시야 밖이거나 손바닥이 카메라 반대 방향일 때 thumb·index 핀치 모양 → `PinchStarted` 발화 0건 (g2/g3 게이트).
- [ ] `[manual-hard]` 컨트롤러 모드(hand-tracking 비활성) 진입 후 컨트롤러 메뉴 버튼만 누름 → 패널 정상 호출(anchoring stub 동작 유지). 같은 시나리오에서 hand-tracking에 의한 false positive 발화 0건 (g1 게이트).
- [ ] `[manual-hard]` 채터링 테스트: 임계 거리 근방(0.03m 부근)에서 손가락을 빠르게 떨어도 `PinchStarted` / `PinchEnded`가 노이즈처럼 반복 발화되지 않음 (Schmitt + minHoldSeconds 효과 검증).

## Out of Scope

- 오른손 핀치, 양손 핀치, 더블 핀치 — spec Out of Scope.
- 핀치를 통한 일반 UI 포인팅·선택 — spec Out of Scope.
- 핸드 트래킹·컨트롤러 입력 채널의 추상화 — spec Out of Scope.
- 임계값 정밀 튜닝 (사용자별 손가락 크기 / 헤드셋별 추적 정밀도 차이). 본 plan은 표준값으로 시작 + 후속 튜닝 plan에서 조정.
- XR Hands 패키지가 아닌 다른 hand-tracking provider (Meta XR All-In-One SDK, Leap Motion 등) 통합.
- `XRInputModalityManager` 기반 정밀 모드 분기. 본 plan은 `handIsTracked` 게이트로 충분하다고 본다 — 필요해지면 후속 plan에서 도입.
- InputAction 자산 수정. 본 plan은 hand-tracking 채널을 InputAction에 binding으로 추가하지 않고 C# event로만 노출.

## Notes

- palm joint의 어느 axis가 손바닥 normal인지는 XR Hands sample(`HandProcessor.cs` / `HandVisualizer.cs`)에서 확인 후 default로 박는다. 헤드셋·SDK 버전에 따라 `up` / `right` / `forward`가 다를 수 있어 `PalmNormalAxis` enum SerializeField로 inspector 조정 가능하게 둔다.
- `LeftPinchDetector.PinchEnded`는 본 plan의 SessionPanelController 통합에선 사용되지 않지만, 후속 plan(예: 더블 핀치 sub-spec, 패널 외 소비처)이 쓸 수 있도록 함께 노출한다.
- `XRInteractionGroup.hasSelection` polling은 매 frame 일어나도 비용이 작다. group 단위로 보면 `Direct` / `Ray` 등 하부 interactor 타입 차이를 흡수할 수 있다.
- 본 plan 적용 시점에 anchoring plan의 SessionPanelController는 컨트롤러 InputAction을 이미 구독 중이다. `leftPinchDetector` 필드가 null이어도 컨트롤러 채널은 그대로 동작한다 — 본 plan은 SerializeField를 추가만 하고 null 안전 분기.

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신 -->
