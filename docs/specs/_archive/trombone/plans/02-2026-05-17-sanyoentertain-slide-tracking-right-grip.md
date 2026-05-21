# TromboneSlideController — 오른손 Grip baseline 기반 1DoF Slide 운전

**Linked Spec:** [`02-slide-tracking.md`](../specs/02-slide-tracking.md)
**Status:** `Done`

## Goal

`Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs` 신규 컴포넌트를 Trombone.prefab 의 Slide GameObject 에 부착해, 오른손 Grip 홀드 동안만 사용자의 오른손 trombone-local x 변화량을 `Slide.localPosition.x` 에 1:1 로 더하고 `[slideMinX, slideMaxX]` 로 clamp 한다. anchor 진입 (`TromboneAnchor.IsAttached == true`) 인 동안에만 동작하고, 진입 밖에서는 어떤 입력에도 슬라이드가 움직이지 않는다. Grip 떼는 순간 baseline 만 해제하고 슬라이드는 마지막 위치를 유지한다 (자동 복귀 없음).

## Context

01 sub-spec 의 `TromboneAnchor` 가 anchor 진입 라이프사이클 (selectExited + locomotionStarted pending window, 본체 정렬, 양손 GripPose Push/Pop) 까지 박제했고, attach 상태에서 trombone root 가 매 LateUpdate 입을 추종한다. 02 sub-spec 은 그 attach 가 활성인 동안의 *서브-라이프사이클* — 오른손 Grip 만 운전 입력 — 을 책임진다. Tech Spec §Data/Control Flow + §Invariants 가 핵심 식과 단일 clamp 지점을 박제했고, Open Tech Decisions 가 비어 있어 입력 모달리티/구독 방식은 본 plan 에서 자체 채택한다.

운전 식은 단순하다 (Tech Spec §Data/Control Flow):
```
매 LateUpdate (Grip 홀드 중):
  currentLocalX  = tromboneRoot.InverseTransformPoint(ghostRightWristSource.position).x
  Slide.localPosition.x = Mathf.Clamp(baselineSlideX + (currentLocalX - baselineHandX), slideMinX, slideMaxX)
```
baseline 은 Grip 누르는 순간 정확히 1회 캐시 (`baselineHandX = currentLocalX`, `baselineSlideX = Slide.localPosition.x`) → Grip 떼면 둘 다 무효화 후 *Slide.localPosition.x 는 그대로 유지*. Tech Spec §Invariants 의 "baseline 은 Grip 홀드 중 갱신되지 않음" 그대로.

오른손 Grip 입력 source 는 XRI Default Input Actions 의 `XRI Right/Select Value` (path `<XRController>{RightHand}/{Grip}`, Value/Axis float) 를 InputActionReference 로 받아 polling. 단순 holding 판정 (`> 0.5f` 임계) 이면 충분 — drum-stick 처럼 *순간 event* 가 아니라 *연속 hold* 가 핵심이므로 `phase == Performed` 같은 event-driven 도 가능하지만 LateUpdate 단계 read 가 baseline 캐시 시점·해제 시점을 둘 다 같은 프레임에 처리해 단순. 0.5 임계는 XRI Default 의 grip press 임계와 일치 (DrumKit/Piano sibling 에서 동일 임계 사용 사례는 없으나 XRI default `pressPoint = 0.5` 답습).

오른손 위치 source 는 01 plan 의 `DrumKitStickAnchor.rightGhostWristSource` 와 동일한 ghost right wrist Transform (TestSceneSanyo fileID 256669989 — `Camera Offset/Hands/Right/...` 하위 R_Wrist). SerializeField 로 외부 주입.

`Slide/GripPoseHand` 는 Slide 자식이므로 Slide.localPosition.x 변화 시 자동 따라가 PlayHand 도 함께 운전된다 (PlayHandPoseDriver 의 `syncRootTransform=true` 가 매 LateUpdate + onBeforeRender 에서 GripPoseHand world pose 를 source 로 읽어 PlayHand world pose 를 묶기 때문). 본 plan 은 GripPoseHand 를 건드리지 않고 Slide 의 localPosition.x 만 변경하면 시각적 정합이 자동 성립.

실행 순서: 본 컴포넌트 LateUpdate → PlayHandPoseDriver LateUpdate. 본 컴포넌트는 `[DefaultExecutionOrder(10005)]` (01 plan 의 `TromboneAnchor` 와 동일 순서) — PlayHandPoseDriver 의 `[DefaultExecutionOrder(10010)]` 보다 먼저 실행되어 Slide.localPosition.x 가 같은 프레임의 PlayHand 갱신 직전에 결정. 01 plan §Notes 가 박제한 순서 제약 (`TromboneAnchor < PlayHandPoseDriver`) 을 그대로 만족하고, 본 plan 의 컴포넌트도 같은 10005 슬롯에 둠으로써 01 의 align 식과 02 의 slide 식이 같은 프레임 안에 결정 → PlayHand 가 두 변화를 동시 반영.

`TromboneAnchor.m_IsAttached` 는 현재 private (01 handoff 박제). 02 plan 이 *간섭 없이* enable 신호를 받으려면 read-only property 1 개 (`public bool IsAttached => m_IsAttached;`) 를 `TromboneAnchor.cs` 에 추가한다. event 노출은 매 프레임 polling 보다 비싸고 본 컴포넌트가 LateUpdate 에서 어차피 read 하므로 property 가 단순. 변경은 1 줄 — 01 plan §Boundaries 의 "TromboneAnchor.cs 수정 금지" 조항은 없음 (Tech Spec 02 §Boundaries 도 PlayHandPoseDriver / Slide mesh / 왼손 / 사운드만 건드리지 않음으로 박제).

## Verified Structural Assumptions

- `TromboneAnchor.cs` (01 plan 산출물) 의 attach 상태는 `bool m_IsAttached` private 필드로 관리되며 현재 외부 노출 표면 없음. 본 plan 이 read-only property `IsAttached` 1 개를 추가한다. 출처: `Read Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs (2026-05-17)` line 32 `bool m_IsAttached;`.
- Trombone.prefab `Slide` GameObject 의 현재 Transform: localPosition `(-0.58953875, -0.06634706, 0.023253594)`, localRotation `(-0.7071068, 0, -0, 0.7071067)`, localScale `(116.53447, 97.7706, 115.93064)`. baseline 시점 `slideMinX` ≤ -0.58953875 ≤ `slideMaxX` 가 되도록 prefab 박제 단계에서 min/max 가 설정돼야 한다. 출처: `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-17, lines 2053-2068)`.
- Trombone.prefab `Slide` 자식 = `GripPoseHand` (01 plan 에서 추가됨, R_Wrist + PreviewMesh). 본 plan 은 GripPoseHand 를 건드리지 않고 Slide.localPosition.x 만 운전 → GripPoseHand 가 Slide 자식이라 자동 추종. 출처: `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-17, line 2065-2066 m_Children: fileID 2928685886231064698)`.
- TestSceneSanyo 안 ghost right wrist Transform = fileID 256669989 (stripped prefab Transform, `m_CorrespondingSourceObject` 는 VR Player.prefab 의 R_Wrist fileID 811324780445496628). 본 plan 의 `rightGhostWristSource` SerializeField wiring 대상. drum-kit anchor 의 동일 필드와 같은 Transform 을 wire (drum-kit `rightGhostWristSource` 도 fileID 256669989). 출처: `Grep rightGhostWristSource in TestSceneSanyo.unity (2026-05-17, line 2961-2963)` + `Grep &256669989 in TestSceneSanyo.unity (2026-05-17, line 231-234)`.
- 오른손 Grip 입력 source 채택: XRI Default Input Actions 의 `XRI Right/Select Value` 액션 (Value/Axis float, binding `<XRController>{RightHand}/{Grip}`). InputActionReference 로 본 컴포넌트에 주입. 임계값 `0.5f` 이상이면 hold 로 판정. 출처: `Grep Grip in XRI Default Input Actions.inputactions (2026-05-17, lines 1974, 2143-2147)`.
- `PlayHandPoseDriver.syncRootTransform` 동작 (호출 외부 API 박제, *동작 전체*):
  - `[DefaultExecutionOrder(10010)]` 부착 — LateUpdate 가 다른 컴포넌트보다 늦게 실행됨.
  - `LateUpdate` + `Application.onBeforeRender` 두 곳에서 `SyncDesiredSourceIfChanged()` → `TryEnsureInitialized()` → `SyncPose()` 실행. `SyncPose` 는 `syncRootTransform == true` 이면 `transform.SetPositionAndRotation(m_ActiveSourceRoot.position, m_ActiveSourceRoot.rotation)` 으로 PlayHand 의 root transform 을 source root (= GripPoseHand) world pose 에 묶는다.
  - 즉 본 plan 이 `Slide.localPosition.x` 를 LateUpdate(10005) 에서 변경하면 같은 프레임의 PlayHandPoseDriver.LateUpdate(10010) 가 그 Slide 자식 GripPoseHand 의 world pose 를 읽어 PlayHand 를 묶음 → 한 프레임 안에 슬라이드·시각적 오른손이 동시 이동.
  - `onBeforeRender` 단계는 LateUpdate 이후이므로 본 plan 의 LateUpdate 변경이 onBeforeRender 보다 먼저. 한 프레임 정합 보장.
  - 출처: `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-17, lines 5, 113-134, 182-195)`.
- `Instruments.asmdef` references = `[Unity.InputSystem, Hands, Unity.XR.Interaction.Toolkit]`. 본 plan 의 신규 `TromboneSlideController.cs` 가 `using UnityEngine.InputSystem;` 으로 `InputActionReference` import 가능. `Assets/Instruments/Trombone/Scripts/` 폴더에는 `.asmdef` 가 없으므로 가장 가까운 상위 `Assets/Instruments/Instruments.asmdef` 에 자동 포함. 신규 reference 추가 불필요. 출처: `Read Assets/Instruments/Instruments.asmdef (2026-05-17)`.
- DrumKitStickAnchor.cs 의 `rightGhostWristSource` 패턴 동작 요약 (호출 외부 API 박제):
  - `[SerializeField] Transform rightGhostWristSource;` 로 ghost right wrist 를 외부 주입.
  - drum-stick 에서는 `Instantiate` 직후 `stick.transform.SetPositionAndRotation(rightGhostWristSource.position, rightGhostWristSource.rotation)` 으로 초기 정렬 + `AnchoredStickGhostFollower.Bind(rightGhostWristSource, ...)` 로 매 프레임 추종 source 위임. 본 plan 은 ghost wrist 의 world position 을 `tromboneRoot.InverseTransformPoint(...)` 로 trombone-local 좌표로 변환해 .x 성분만 사용.
  - 출처: `Read Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs (2026-05-17, lines 25, 109-115, 124-145)`.

## Approach

1. **`TromboneAnchor.cs` 에 read-only property 추가** — 단일 줄 추가: `public bool IsAttached => m_IsAttached;` (`m_IsAttached` 필드 선언 바로 아래). 외부 노출 표면 변경이지만 setter 없음 → invariant 깨질 가능성 0. 01 plan 의 attach/detach 로직은 그대로.
2. **`Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs` 신규** (manage_script MCP 또는 직접 Write):
   - namespace `Instruments`.
   - `[DefaultExecutionOrder(10005)]`, `[DisallowMultipleComponent]`.
   - SerializeField:
     - `TromboneAnchor tromboneAnchor` — enable 신호 source. null 이면 본 컴포넌트 noop (warn 없음 — 사용자 진단 요청 없음).
     - `Transform tromboneRoot` — trombone-local 변환 기준. 일반적으로 Slide 의 부모 (`Trombone` GameObject) — Inspector 에서 wire.
     - `Transform slide` — 운전 대상. 기본은 `transform` (본 컴포넌트가 Slide 에 부착되므로). `[SerializeField]` 로 외부 주입 허용해 향후 분리 시 유연.
     - `Transform rightGhostWristSource` — 오른손 위치 source. TestSceneSanyo fileID 256669989 (drum-kit 과 동일 wire).
     - `InputActionReference rightGripAction` — `XRI Right/Select Value` 액션 reference.
     - `float slideMinX = -0.6f` (default, Inspector 에서 prefab 시각 조정).
     - `float slideMaxX = -0.3f` (default, Inspector 에서 prefab 시각 조정).
     - `float gripThreshold = 0.5f` (XRI default press point 답습).
   - 내부 상태: `bool m_IsGripHeld`, `float m_BaselineHandX`, `float m_BaselineSlideX`.
3. **입력 enable/disable** — `OnEnable` 에서 `rightGripAction.action?.Enable()`, `OnDisable` 에서 `rightGripAction.action?.Disable()`. XRI starter assets 의 InputActionManager 가 이미 enable 했을 가능성 있어도 idempotent — 안전.
4. **LateUpdate** — 다음 단계 순차:
   - `if (tromboneAnchor == null || !tromboneAnchor.IsAttached) { m_IsGripHeld = false; return; }` — detach 동안 baseline 잔존 제거 + 아무 운전 안 함.
   - `var action = rightGripAction?.action;`
   - `bool grip = action != null && action.ReadValue<float>() >= gripThreshold;`
   - `if (grip && !m_IsGripHeld)` — Grip 누르는 순간 (rising edge):
     - `m_BaselineHandX = ComputeHandLocalX();`
     - `m_BaselineSlideX = slide.localPosition.x;`
     - `m_IsGripHeld = true;`
   - `else if (!grip && m_IsGripHeld)` — Grip 떼는 순간 (falling edge):
     - `m_IsGripHeld = false;`
     - baseline 변수는 그대로 둠 (다음 누름까지 미사용 — 단순 flag 로 invariant 보장).
   - `if (m_IsGripHeld) ApplySlide();`
5. **`ComputeHandLocalX()`** — `var localPos = tromboneRoot.InverseTransformPoint(rightGhostWristSource.position); return localPos.x;`. ghost wrist 가 null 인 ref 면 0 반환 + early return (안전망).
6. **`ApplySlide()`** — `var current = ComputeHandLocalX(); var newX = Mathf.Clamp(m_BaselineSlideX + (current - m_BaselineHandX), slideMinX, slideMaxX); var p = slide.localPosition; p.x = newX; slide.localPosition = p;`. y/z 불변 보장 (Tech Spec §Invariants).
7. **Trombone.prefab 의 Slide 에 컴포넌트 부착** (manage_gameobject MCP, `precondition_sha256` 보호) — `Slide` GameObject 에 `TromboneSlideController` AddComponent. SerializeField wiring:
   - `tromboneAnchor` — prefab 단계에서는 null (scene 단계에서 wire). prefab 인스턴스 wiring 은 scene 측 PrefabInstance 의 m_Modifications 로.
   - `tromboneRoot` — prefab 내 root (`Trombone` Transform).
   - `slide` — `transform` (자기 자신).
   - `rightGhostWristSource` — prefab 단계 null (scene 단계 wire).
   - `rightGripAction` — XRI Default Input Actions 의 `XRI Right/Select Value` InputActionReference (asset reference 이므로 prefab 에 직접 wire 가능).
   - `slideMinX = -0.7f`, `slideMaxX = -0.3f` (defaults — 사용자가 헤드셋 시각 검증 후 prefab 시각 조정. baseline -0.58953875 가 범위 안에 들도록 임시 박제).
8. **TestSceneSanyo 의 Trombone PrefabInstance 에 wiring 보완** (manage_gameobject MCP / 직접 텍스트 Edit) — Trombone PrefabInstance 의 m_Modifications 에 `tromboneAnchor` (scene `TromboneAnchor` GameObject reference) 와 `rightGhostWristSource` (fileID 256669989) 항목 추가. drum-kit PrefabInstance 의 동일 필드 wiring 형식 답습.
9. **검증** — 컴파일 후 read_console 0 error. Editor Play 모드 진입 → anchor 진입 → 오른손 Grip 누른 채 좌우 이동 → Slide.localPosition.x 변화. Grip 떼면 마지막 위치 유지. anchor 외부에서 Grip 눌러도 무동작. Slide.localPosition.(y, z) 불변.

## Deliverables

- `Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs` — `public bool IsAttached => m_IsAttached;` property 1 줄 추가.
- `Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs` — 신규. 오른손 Grip 홀드 동안 baseline+delta 식으로 Slide.localPosition.x 운전, anchor enable 신호 연동.
- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` — Slide GameObject 에 `TromboneSlideController` AddComponent + SerializeField wiring (tromboneRoot, slide, rightGripAction, slideMinX/Max, gripThreshold).
- `Assets/Scenes/TestSceneSanyo.unity` — Trombone PrefabInstance 의 m_Modifications 에 `tromboneAnchor` + `rightGhostWristSource` 2 개 wiring 항목 추가.

## Acceptance Criteria

- [x] `[auto-hard]` `TromboneSlideController.cs` 가 `Assets/Instruments/Trombone/Scripts/` 에 존재하고, `namespace Instruments`, `[DefaultExecutionOrder(10005)]`, `[DisallowMultipleComponent]` 가 모두 부착돼 있다.
  **검증:** `Grep -n "namespace Instruments|DefaultExecutionOrder\(10005\)|DisallowMultipleComponent" Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs` 결과 3 줄 매칭.
- [x] `[auto-hard]` `TromboneSlideController` 가 baseline 박제식 (`baselineHandX`, `baselineSlideX`) 과 매 LateUpdate clamp 식 (`Mathf.Clamp(baselineSlideX + (currentLocalX - baselineHandX), slideMinX, slideMaxX)`) 을 정확히 1 곳에서 수행한다 — Tech Spec §Invariants "clamp 가 한 곳에서만 발생".
  **검증:** `Grep -n "Mathf.Clamp" Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs` 결과 정확히 1 매칭 + 그 라인이 `baselineSlideX + (.*?- m_BaselineHandX)` 패턴 포함.
- [x] `[auto-hard]` `TromboneAnchor.cs` 에 `public bool IsAttached` read-only property 가 추가돼 있고 setter 가 없다 (auto-property 또는 expression-body).
  **검증:** `Grep -n "public bool IsAttached" Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs` 결과 1 매칭 + 같은 라인에 `=>` 또는 `{ get;` 만 있고 `set` 토큰 부재.
- [x] `[auto-hard]` `TromboneSlideController` 가 `TromboneAnchor.IsAttached == false` 일 때 baseline 을 reset 하고 (`m_IsGripHeld = false`) Slide.localPosition 변경을 수행하지 않는다 — anchor 밖 입력이 슬라이드를 움직이면 Tech Spec §Invariants 마지막 조항 위반.
  **검증:** `Grep -n "IsAttached" Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs` 결과 ≥ 1 매칭 + 해당 분기 안에서 `m_IsGripHeld = false` 또는 동등 reset + early return 패턴 확인.
- [x] `[auto-hard]` Trombone.prefab 의 `Slide` GameObject 가 `TromboneSlideController` 컴포넌트를 포함하고, SerializeField `tromboneRoot` / `slide` / `rightGripAction` / `slideMinX` / `slideMaxX` 가 모두 직렬화돼 있다.
  **검증:** `Grep -n "TromboneSlideController|tromboneRoot:|slideMinX:|slideMaxX:|rightGripAction:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과 ≥ 5 매칭.
- [x] `[auto-soft]` Unity Editor 컴파일 0 error (`read_console types=[error]`). `TromboneSlideController.cs` 가 `Instruments` asmdef 에 자동 포함돼 `UnityEngine.InputSystem.InputActionReference` 가 resolve.
  **검증:** `read_console action=get types=[error] count=20` 결과 TromboneSlideController 관련 컴파일 에러 0 건 + `editor_state.isCompiling == false`.
- [ ] `[manual-hard]` Editor Play 모드에서 TromboneAnchor 로 텔레포트 진입 → 오른손 Grip 을 누르지 않고 손을 좌우로 움직임 → Slide 가 *전혀* 움직이지 않는다 (시각적 오른손은 GripPoseHand 위치 그대로 유지).
  **검증:** Play 모드 진입 → anchor 텔레포트 → Grip 미입력 상태에서 오른손 좌우 흔들기 → Slide.localPosition.x 가 진입 시점 값에서 변하지 않음 (Inspector 또는 Scene 뷰 시각 확인).
- [ ] `[manual-hard]` Editor Play 모드에서 anchor 진입 상태, Grip 을 *누르는 순간* 의 오른손 위치/슬라이드 위치가 baseline 으로 박제되고, 그 이후 오른손이 -x 방향 5cm 이동 시 슬라이드가 정확히 -x 방향 5cm 이동 (1:1 비율 + slideMin clamp 안일 때).
  **검증:** Play 모드 진입 → anchor 텔레포트 → 오른손을 임의 위치에 두고 Grip 누름 → 천천히 -x 방향으로 손 이동하며 Slide 가 손과 같은 양만큼 따라 움직이는지 시각·Inspector 확인.
- [ ] `[manual-hard]` Grip 홀드 상태에서 손을 `slideMinX` 보다 더 안쪽으로 (또는 `slideMaxX` 너머로) 이동 → Slide 가 min/max 에서 clamp 되고 더 이상 움직이지 않으며, 손을 다시 범위 안으로 복귀시키면 슬라이드도 다시 따라온다.
  **검증:** Play 모드에서 anchor 진입 + Grip 홀드 후 손을 끝까지 -x → Slide 가 slideMinX 에서 정지 + Inspector 에서 `Slide.localPosition.x == slideMinX` 확인. 손을 다시 +x 로 가져오면 즉시 추종 재개.
- [ ] `[manual-hard]` Grip 을 떼는 순간 Slide 는 마지막 위치 그대로 유지하고, Grip 을 다시 누르면 그 시점 새 baseline 으로 운전이 재개된다 (자동 복귀 없음, baseline 갱신 발생).
  **검증:** Play 모드에서 Grip 누르고 손을 이동 → Grip 뗌 (슬라이드 정지 확인) → 손을 다른 위치로 이동 (Slide 불변 확인) → Grip 재누름 → 손을 움직였을 때 슬라이드가 *새 baseline* 부터 추종.
- [ ] `[manual-hard]` anchor 외부 위치로 텔레포트해서 detach 된 상태에서 오른손 Grip 을 누르고 손을 움직여도 Slide 위치가 변하지 않는다.
  **검증:** Play 모드에서 detach 후 임의 위치에서 Grip 홀드 + 손 이동 → Trombone scene 인스턴스의 Slide.localPosition.x 가 detach 시점 값 그대로 유지 (Inspector 확인).
- [ ] `[manual-hard]` 시각적 오른손이 슬라이드와 같은 프레임에 함께 이동한다 (한 프레임 뒤처지지 않음). PlayHand 가 Slide/GripPoseHand 의 world pose 를 따르므로 Slide.localPosition.x 변경 시 즉시 같은 프레임에 PlayHand 가 그 위치에 보인다.
  **검증:** Play 모드에서 Grip 홀드 + 손 좌우 빠른 이동 → 슬라이드와 오른손 메쉬가 시각적으로 함께 움직이는지 (한쪽이 한 프레임 뒤처지지 않는지) 시각 확인.

## Out of Scope

- `slideMinX` / `slideMaxX` 의 *정확한* 수치 튜닝 — 본 plan 은 baseline -0.58953875 가 범위 안에 들도록 임시값 `-0.7f / -0.3f` 만 박제. 실제 트럼본 가동 범위 매칭은 사용자가 헤드셋 시각 검증 단계에서 prefab Inspector 로 조정.
- 슬라이드 위치 → 음높이 매핑 / 사운드 출력 — 03 sub-spec.
- 왼손 Grip 입력 (호흡 / 발음) — 03 sub-spec.
- 슬라이드의 물리 시뮬레이션 (관성, 충돌, 진동) — Tech Spec §Out of Scope (Tech Spec 의 sub-spec §Out of Scope 와 동일).
- Grip 입력 외 별도 모드 전환 (예: 트리거로 슬라이드 운전) — sub-spec §Out of Scope.
- PlayHand override 슬롯 변경 — 01 plan handoff "PlayHand override 는 본 plan 의 점유 그대로 유지" 답습.
- 슬라이드 운전을 멀티플레이어로 동기화 — 트럼본 피처 전체 범위 외.

## Notes

- baseline 변수 `m_BaselineHandX` / `m_BaselineSlideX` 는 Grip 떼는 순간 의도적으로 0 으로 리셋하지 *않는다*. Grip 떼는 순간 `m_IsGripHeld = false` 만 변경하고 baseline 값은 그대로 — 다음 누름까지 사용되지 않으므로 invariant 깨질 가능성 0 이지만, *왜 reset 하지 않는가* 박제: reset 자체가 추가 코드라 (불필요 + 미세 race 여지) 단순 flag 토글로 끝낸다.
- gripThreshold default `0.5f` 는 XRI Default Input Actions 의 grip press point 와 일치. Quest / Index 컨트롤러 둘 다 동일 임계 — 디바이스별 분기 불필요.
- LateUpdate read 단계에서 `rightGripAction?.action?.ReadValue<float>()` 는 disabled action 에 대해 0 반환 (Unity InputSystem 표준) — null 안전. `Enable()` 호출은 OnEnable 에 두지만, 다른 곳에서 이미 enabled 면 idempotent.
- `TromboneAnchor.IsAttached` 추가는 01 plan §Handoff 의 "공개 표면 필요시 read-only property 또는 event 추가로 결정" 위임 조항을 *property* 로 결정. 본 plan 은 그 결정을 박제. event 노출은 02 가 LateUpdate 폴링하는 모델이라 매 프레임 read 가 더 단순.
- 03 sub-spec 이 추가로 detach 시점에 슬라이드를 원위치 복귀시킬지 결정이 필요해지면 (현재 sub-spec §Behavior 는 "마지막 위치 유지") 본 컴포넌트 LateUpdate 의 `!tromboneAnchor.IsAttached` 분기에서 reset 로직을 *추가* 할 수 있다 — 현재는 추가 안 함.
- 01 plan §Notes 의 LateUpdate 실행 순서 박제 (10005 < 10010) 와 동일 슬롯에 본 plan 컴포넌트를 둠. 같은 슬롯 안에서도 Unity 는 *deterministic 순서를 보장하지 않으나*, 본 plan 의 Slide.localPosition 변경과 01 plan 의 trombone root 변경은 서로 독립적 (Slide 는 root 의 자식이지만 Slide.localPosition.x 변경은 root 의 world transform 에 의존하지 않음). 따라서 같은 슬롯 안 비결정 순서도 안전.
- XRI Default Input Actions 안에서 발견된 InputActionReference 이름은 plan 작성 시점 가정한 `XRI Right/Select Value` 가 아니라 `XRI Right Interaction/Select Value` 였다 (현재 XRI 3.3.1 Starter Assets 의 네이밍). plan §Verified Structural Assumptions 의 가정이 어긋났지만 동일한 grip binding 을 가리키므로 의도된 결과. fallback 매칭 (`Contains("Right")` + `Contains("Select Value")`) 으로 정확히 매칭됨.
- 구현 후 자동 검증 결과:
  - 자동 AC 6 (auto-hard 5 + auto-soft 1) 전부 PASS — Grep evidence 5 종 + read_console 0 error.
  - unity-test-runner: EditMode 102/102 pass, PlayMode 4/4 pass, Console errors 0. 회귀 없음.
- Manual-hard 6 개 (AC 7-12) 는 사용자 헤드셋 시각 검증 필요. sub-spec 03 종료 후 일괄 검증 예정 — 본 commit 시점은 `pending`.

## Handoff

- **TromboneAnchor.IsAttached** — `public bool IsAttached` read-only property 노출됨. 03 sub-spec 의 왼손 Grip 운전 컴포넌트가 동일 enable 신호로 사용 가능.
- **Slide.localPosition.x 운전 슬롯 점유** — `TromboneSlideController` 가 매 LateUpdate Slide.localPosition.x 를 (Grip 홀드 동안) 운전. 03 sub-spec 은 Slide.localPosition.x 를 *read* 만 (음높이 매핑 source) 하고 *write* 하지 않는다. 두 컴포넌트가 같은 필드에 동시 write 하면 비결정 결과.
- **오른손 Grip 입력 슬롯 점유** — `XRI Right/Select Value` InputActionReference 가 본 컴포넌트의 단일 소비자로 wire. 03 sub-spec 의 왼손은 `XRI Left/Select Value` (대칭 binding) 를 사용 — 좌/우 분리.
- **TromboneSlideController 컴포넌트 부착 위치** — Trombone.prefab 의 Slide GameObject. 03 sub-spec 이 Slide 의 추가 컴포넌트를 부착할 때 `[DefaultExecutionOrder]` 충돌 주의 (현재 본 컴포넌트가 10005 슬롯 점유).
- **slideMinX / slideMaxX 박제값** — prefab default `-0.7f / -0.3f` (임시). 03 sub-spec 의 음높이 매핑은 이 범위를 `[slideMinX, slideMaxX] → [최저 음, 최고 음]` 로 선형 매핑 가능. 정확한 매핑 식은 03 sub-spec 의 ARD 후보.
- **TromboneSlideController가 Slide GameObject에 부착돼 있음** — 03 sub-spec의 Trombone(InstrumentBase) 컴포넌트는 trombone root에 부착하고 Slide.localPosition.x를 read-only로 참조한다 (write 금지).
- **InputActionReference 실제 이름** — `XRI Right Interaction/Select Value` (XRI 3.3.1 Starter Assets). 03 sub-spec 왼손은 대칭으로 `XRI Left Interaction/Select Value` 사용 예정.
