# TromboneBodyVisualSnap — Rig/Body localRotation.z 스냅 + 100ms ease-out

**Linked Spec:** [`01-view-snap.md`](../specs/01-view-snap.md)
**Status:** `Done`

## Goal

신규 `Instruments.TromboneBodyVisualSnap` 컴포넌트를 Trombone.prefab의 Rig/Body에 부착해, 트롬본을 잡은 동안(IsAttached=true) Rig/Body의 표시 z회전이 `TrombonePartialController.PartialIndex`에 대응하는 5개 anchor 각도(-30°/-15°/0°/+15°/+30°) 중 하나로 100ms exponential ease-out 곡선을 그리며 수렴하고, 트롬본을 놓으면(IsAttached=false) Body localRotation을 원복해 입력 회전을 그대로 보이게 한다.

## Context

`docs/specs/trombone-pitch-perception/specs/01-view-snap.md`(Trombone Body Visual Snap)는 "Rig/Body 표시 z회전만 5개 anchor 각도로 hard step 스냅. 입력 측 PartialController는 변경 금지. 짧은 ease로 시각 점프 완화. IsAttached=false 시 원복"을 What으로 요구한다.

근거 결정:
- ARD `decisions/01-visual-rotation-target.md` (Accepted, 2026-05-27): **Rig/Body 그룹만** 회전. Rig/Body Transform.localRotation의 z만 수정하고 x/y는 원본 유지. Slide/MouthPiece/SlidePositionMarkers/NoteDisplay는 Rig의 형제이거나 별도 자식이므로 영향받지 않음.
- ARD `decisions/02-hard-step-easing.md` (Accepted, 2026-05-27): **100ms exponential ease-out**을 default로 박제하되 `easeDurationSeconds`(default 0.1f)와 ease 곡선 표현(AnimationCurve 또는 exponential factor)을 SerializeField로 노출. PartialIndex 변경 시점부터 ease 시작. 이전 ease 진행 중 PartialIndex가 또 바뀌면 현재 표시각에서 새 target으로 재출발(jitter 방지).
- Tech Spec `tech-specs/01-view-snap.md`:
  - **Data Flow**: 사용자 입력 → Trombone root localEulerAngles.z → `TrombonePartialController` (LateUpdate, ExecOrder 10005) → PartialIndex → **TromboneBodyVisualSnap** (LateUpdate, ExecOrder ≥ 10006) → anchor 각도 = `(PartialIndex - centerPartialIndex) × anglePerPartial` → Rig/Body.localRotation.z를 ease로 보간.
  - **Boundaries**: 건드림 = Trombone.prefab의 Rig/Body Transform.localRotation(z만) + 신규 컴포넌트 부착. 건드리지 않음 = TrombonePartialController의 입력 처리·hysteresis·PartialIndex 산출, Rig/Slide·Rig/MouthPiece·SlidePositionMarkers·NoteDisplay, Trombone root 회전, Hands 도메인 grip pose 처리.
  - **Invariants**: IsAttached=true 동안 표시 z회전은 항상 PartialIndex에 대응하는 anchor 각도로 수렴(ease 진행 중 제외). x/y는 변경 금지. 단방향 input → display. IsAttached=false → 원복.
- Sibling 참조: `TromboneSlideController.cs`가 동일한 분리 패턴(입력 → 보정 → quantize → 자기 Transform만 수정 + IsAttached=false 시 baseline 리셋)을 이미 구현. 본 컴포넌트는 회전축(z) + ease 보간 형태로 답습.

## Verified Structural Assumptions

- **Trombone prefab 계층**: `Trombone (root, Instruments.Trombone + Instruments.TrombonePartialController + Instruments.InstrumentAudioOutput) → Rig (Transform only) → {Body (Transform + MeshFilter + MeshRenderer), Slide (TromboneSlideController), MouthPiece, NoteDisplay (Canvas), SlidePositionMarkers}`. Body가 트롬본 본체 mesh를 단독 보유. — `Unity MCP (manage_prefabs.get_hierarchy) on Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-27, 76 objects)`
- **Body 자식 = GripPoseHand (왼손)**: `Trombone/Rig/Body/GripPoseHand`에 `GripPoseHandPreview` + `L_Wrist` 본 계층 + `HandMeshPreview`(SkinnedMeshRenderer)가 들려 있음. Body를 z회전하면 본 왼손 자식은 함께 회전한다 — 본 plan은 Body의 z회전만 보정하므로 왼손 grip pose 정합 미세 어긋남이 발생할 수 있으나 spec Out of Scope (Hands 도메인 책임으로 이관). Slide(=오른손 grip)는 Body의 형제이므로 영향 없음. — `Unity MCP (manage_prefabs.get_hierarchy) (2026-05-27)`
- **TromboneAnchor.IsAttached API**: `public bool IsAttached => m_IsAttached;` (한 줄, public readonly property). attach/detach 시점은 `AttachTromboneToMouth()`/`Detach()`에서 m_IsAttached가 true/false로 토글되며, `Detach()`는 양손 PlayHandPoseDriver pop → `tromboneRoot.SetPositionAndRotation(m_CachedRestorePosition, m_CachedRestoreRotation)` → m_IsAttached=false 순서로 실행. Detach 후 다음 LateUpdate에서 본 컴포넌트가 원복을 한 번만 수행해야 함(매 프레임 원복은 input 회전 표시 차단). — `Read Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs (2026-05-27, public IsAttached + Detach 동작 라인 35, 205-218)`
- **TrombonePartialController API**:
  - `public int PartialIndex => m_PartialIndex;` (LateUpdate ExecOrder 10005에서 갱신)
  - `public float AnglePerPartial => anglePerPartial;` (SerializeField, default 15f)
  - `public int PartialCount => partialOffsetsSemitones.Length;`
  - `centerPartialIndex` (SerializeField, default 2)는 public 노출되지 않음 — 본 plan은 anchor 각도 계산식 `(PartialIndex - centerPartialIndex) × anglePerPartial`을 쓰려면 **본 컴포넌트가 같은 `centerPartialIndex`를 SerializeField로 보유**해 inspector에서 동일 값으로 페어링하거나, PartialController에 `public int CenterPartialIndex => centerPartialIndex;`를 추가해야 함. 본 plan은 후자(API 노출)를 선택해 단일 진실원 유지. PartialController.cs의 `ThresholdFor(int i) => (i - centerPartialIndex - 0.5f) * anglePerPartial` 식과 anchor 각도식 `(i - centerPartialIndex) * anglePerPartial`이 동일 `centerPartialIndex`를 공유하는 게 spec What과 부합. — `Read Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs (2026-05-27, public API 라인 27-29, centerPartialIndex 라인 23, anchor 각도식 도출 라인 68)`
- **TrombonePartialController side effect**: `OnEnable`에서 `m_PartialIndex = ClampInitial(initialPartialIndex);`로 초기화. LateUpdate ExecOrder 10005에서 매 프레임 `IsAttached==false`면 `m_PartialIndex = ClampInitial(initialPartialIndex);` 리셋 후 즉시 return — 즉 detach 직후에도 PartialIndex는 유효 인덱스를 보유함(0 또는 initialPartialIndex). 본 plan은 IsAttached=false 시 PartialIndex 값을 무시하고 Body 원복 경로로 분기해야 함. — `Read Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs (2026-05-27, OnEnable 라인 41-44, LateUpdate detach 분기 라인 46-52)`
- **TromboneSlideController 패턴 답습 가능 영역**: Slide는 자기 Transform(`slide` SerializeField)의 localPosition.x만 수정하고 `IsAttached==false` 분기에서 `m_IsGripHeld = false; ApplyQuantizedPosition();` 후 return하는 구조. 본 plan도 `body` SerializeField(Rig/Body Transform) + IsAttached=false 분기에서 원복을 수행. `[DefaultExecutionOrder(10005)]`로 박제됐으나 본 plan은 PartialController(10005) 다음에 실행되어야 하므로 **`[DefaultExecutionOrder(10006)]`로 박제**. — `Read Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs (2026-05-27, attach 분기 라인 56-85, ExecOrder 라인 13)`
- **asmdef reference**: 신규 파일은 `Assets/Instruments/Trombone/Scripts/TromboneBodyVisualSnap.cs`로 작성하며, 이 폴더는 `Assets/Instruments/Instruments.asmdef`(name=`Instruments`, references=[`Unity.InputSystem`, `Hands`, `Unity.XR.Interaction.Toolkit`])에 포함된다. 본 컴포넌트는 `using UnityEngine;` + 같은 namespace `Instruments`의 `TromboneAnchor`/`TrombonePartialController`만 import하므로 신규 reference 추가 불필요. — `Read Assets/Instruments/Instruments.asmdef (2026-05-27)`

## Approach

1. **`TrombonePartialController.cs`에 public getter 1줄 추가** — `public int CenterPartialIndex => centerPartialIndex;`. 본 plan의 anchor 각도 계산식 `(i - centerPartialIndex) × anglePerPartial`이 PartialController의 hysteresis 식 `(i - centerPartialIndex - 0.5f) * anglePerPartial`과 동일 single source of truth를 쓰도록 노출. SerializeField·private 필드·입력 처리 로직은 무변경 (Tech Spec Boundaries 준수).
2. **신규 컴포넌트 작성** `Assets/Instruments/Trombone/Scripts/TromboneBodyVisualSnap.cs`:
   - namespace `Instruments`, `[DefaultExecutionOrder(10006)]`, `[DisallowMultipleComponent]`, `sealed class`.
   - SerializeField:
     - `TromboneAnchor tromboneAnchor` (IsAttached 판정)
     - `TrombonePartialController partialController` (PartialIndex / AnglePerPartial / CenterPartialIndex 소스)
     - `Transform body` (Rig/Body Transform — Reset에서 `body = transform`로 self-bind, inspector에서 Rig/Body GameObject에 부착해 자동 wiring)
     - `[Min(0f)] float easeDurationSeconds = 0.1f` (ARD 02 박제 default)
     - 곡선 표현은 **exponential ease-out factor** 채택 — `[Range(1f, 10f)] float easeExponent = 4f` (k 값. ARD에서 "AnimationCurve 또는 exponential factor" 옵션 중 후자가 더 간단·결정론적). 진행률 `t ∈ [0,1]`에 대해 `easedT = 1 - exp(-easeExponent × t)` 사용. duration=0이면 instant snap (옵션 가능).
   - 런타임 상태: `float m_StartAngleZ`, `float m_TargetAngleZ`, `float m_ElapsedSeconds`, `int m_LastPartialIndex = -1`, `bool m_IsBoosting`(=ease 진행 중), `Vector3 m_OriginalLocalEulerAnglesAtAttach`(원복용 — IsAttached false→true 전이 시 캐시).
   - **Awake/Reset**: `body = transform;` (self-bind), 그 외 SerializeField 자동 wiring은 inspector에서.
   - **OnEnable**: `m_LastPartialIndex = -1; m_IsBoosting = false;` (초기 상태). `m_OriginalLocalEulerAnglesAtAttach`은 attach 직전에 캐시되므로 OnEnable에선 미초기화.
   - **LateUpdate** (ExecOrder 10006이므로 PartialController(10005) 다음 실행):
     - guard: `if (body == null || tromboneAnchor == null || partialController == null) return;`
     - **IsAttached=false 분기**: `if (!tromboneAnchor.IsAttached) { ApplyOriginalRotation(); m_LastPartialIndex = -1; m_IsBoosting = false; return; }` — 원복을 매 프레임 호출해도 idempotent (cache된 원본 값을 다시 쓰는 형태). 단, attach 직전에 m_OriginalLocalEulerAnglesAtAttach가 미캐시일 수 있으므로 캐시 여부 플래그 `m_HasOriginalCache`를 두고 false면 원복 skip.
     - **attach 전이 감지 + 캐시**: `if (m_LastPartialIndex == -1) { m_OriginalLocalEulerAnglesAtAttach = body.localEulerAngles; m_HasOriginalCache = true; /* 초기 target = 현재 PartialIndex anchor, 즉시 점프 */ }` — 첫 LateUpdate에서 ease 없이 anchor에 점프하면 attach 직후 깜빡임이 줄어든다. 사용자 피드백에서 attach 직후 ease가 필요하다고 보고되면 ARD 02 재평가.
     - **target anchor 계산**: `int idx = partialController.PartialIndex; float targetZ = (idx - partialController.CenterPartialIndex) * partialController.AnglePerPartial;`
     - **PartialIndex 변경 시 ease 재시작**: `if (idx != m_LastPartialIndex) { m_StartAngleZ = CurrentDisplayAngleZ(); m_TargetAngleZ = targetZ; m_ElapsedSeconds = 0f; m_IsBoosting = true; m_LastPartialIndex = idx; }` — 이전 ease 진행 중이라도 `CurrentDisplayAngleZ()`(=현재 body.localEulerAngles.z를 [-180,180] 정규화)에서 새 target으로 재출발 (ARD 02 jitter 방지 조항).
     - **ease 진행**:
       ```
       if (m_IsBoosting) {
           m_ElapsedSeconds += Time.deltaTime;
           float t = (easeDurationSeconds > 0f) ? Mathf.Clamp01(m_ElapsedSeconds / easeDurationSeconds) : 1f;
           float easedT = (easeExponent > 0f) ? 1f - Mathf.Exp(-easeExponent * t) : t;
           // duration 끝 보정: t=1이면 정확히 target 적용
           if (t >= 1f) { easedT = 1f; m_IsBoosting = false; }
           float z = Mathf.LerpAngle(m_StartAngleZ, m_TargetAngleZ, easedT);
           ApplyZ(z);
       } else {
           // ease 종료 상태 — target 그대로 유지 (입력 변화로 PartialIndex가 유지되는 한 표시각 정지)
           ApplyZ(m_TargetAngleZ);
       }
       ```
     - `ApplyZ(z)`: `var e = m_OriginalLocalEulerAnglesAtAttach; e.z = z; body.localEulerAngles = e;` — x/y는 attach 시점 원본 값을 유지(Invariant: x/y 변경 금지).
     - `ApplyOriginalRotation()`: `if (m_HasOriginalCache) { body.localEulerAngles = m_OriginalLocalEulerAnglesAtAttach; }` — Detach 후 매 프레임 호출 시 cached 원본으로 멱등 복원. PartialController가 Trombone root를 회전시키는 게 아니라 본 컴포넌트가 Body만 회전시켰으므로, 원복 = Body localEulerAngles를 cached 값으로 되돌리는 것이 곧 "입력 그대로 보임" (Body가 Rig 자식이고 Rig는 Trombone root를 따라가므로).
     - `CurrentDisplayAngleZ()`: `float z = body.localEulerAngles.z; if (z > 180f) z -= 360f; return z;` — Mathf.LerpAngle은 360° wrap을 처리하지만 시작 z를 [-180,180]로 정규화해두면 디버깅 가독성 향상.
3. **Trombone.prefab 편집** — Rig/Body GameObject에 신규 `TromboneBodyVisualSnap` 컴포넌트 부착 후 SerializeField wiring:
   - `tromboneAnchor` ← `Trombone/TromboneAnchor` (같은 prefab 내 instance reference)
   - `partialController` ← `Trombone` root의 `TrombonePartialController` 컴포넌트
   - `body` ← self (Reset에서 자동 — 부착 직후 자동 wiring 확인)
   - `easeDurationSeconds = 0.1f`, `easeExponent = 4f` default 유지
4. **수동 회귀 점검** — Unity Editor에서 prefab 변경 직렬화 확인 + Play Mode 진입해 (a) 트롬본 잡기 전 Body가 원래 회전 그대로인지 (b) 잡은 후 Rig/Body가 5개 anchor 중 하나로 수렴하는지 (c) PartialIndex 변경 시 100ms 정도 ease 곡선 보이는지 (d) 놓은 후 입력 회전 그대로 보이는지 시각 확인.

## Deliverables

- `Assets/Instruments/Trombone/Scripts/TromboneBodyVisualSnap.cs` (신규) — Rig/Body localRotation.z를 PartialIndex anchor 각도로 100ms exponential ease-out 보간하는 컴포넌트
- `Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs` (수정 — 1줄 추가) — `public int CenterPartialIndex => centerPartialIndex;` getter 노출
- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` (수정) — Rig/Body GameObject에 `TromboneBodyVisualSnap` 컴포넌트 부착 + SerializeField wiring

## Acceptance Criteria

- [ ] `[auto-hard]` `TromboneBodyVisualSnap.cs`가 컴파일에 성공하고 namespace `Instruments` 하의 `sealed class`로 정의되며 `[DefaultExecutionOrder(10006)]` + `[DisallowMultipleComponent]` 어트리뷰트가 부여된다.
  **검증:** `Read Assets/Instruments/Trombone/Scripts/TromboneBodyVisualSnap.cs` 후 `namespace Instruments` / `sealed class TromboneBodyVisualSnap` / `[DefaultExecutionOrder(10006)]` 라인 grep 확인 + Unity MCP `read_console types=["error"]`로 컴파일 에러 0건.
- [ ] `[auto-hard]` `TrombonePartialController.cs`에 `public int CenterPartialIndex => centerPartialIndex;` 한 줄이 추가되며, 기존 `m_PartialIndex` / `anglePerPartial` / `partialOffsetsSemitones` / `hysteresisAngle` / `LateUpdate` 본문 / `ThresholdFor` 식은 무변경.
  **검증:** `git diff Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs`가 단 1줄 추가(public getter)만 보이고 LateUpdate·ThresholdFor 라인에 변경 없음 (Tech Spec Boundaries: "TrombonePartialController의 입력 처리·hysteresis·PartialIndex 산출 건드리지 않음" 준수 확인).
- [ ] `[auto-hard]` Trombone.prefab의 `Trombone/Rig/Body` GameObject에 `Instruments.TromboneBodyVisualSnap` 컴포넌트가 부착되고, SerializeField (`tromboneAnchor`, `partialController`, `body`, `easeDurationSeconds`, `easeExponent`)가 wiring 또는 default 값으로 직렬화된다.
  **검증:** Unity MCP `manage_prefabs.get_hierarchy prefab_path="Assets/Instruments/Trombone/Prefabs/Trombone.prefab"` 결과의 `Trombone/Rig/Body` item의 `componentTypes` 배열에 `"Instruments.TromboneBodyVisualSnap"`이 포함됨 + prefab YAML diff에서 `easeDurationSeconds: 0.1`/`easeExponent: 4` 직렬화 확인.
- [ ] `[auto-hard]` 본 컴포넌트가 Body의 x/y 회전을 변경하지 않는다 (Invariant 박제).
  **검증:** `Read Assets/Instruments/Trombone/Scripts/TromboneBodyVisualSnap.cs`에서 `body.localEulerAngles` 또는 `body.localRotation`에 값을 대입하는 모든 라인을 grep해, x/y는 항상 `m_OriginalLocalEulerAnglesAtAttach.x` / `.y`(또는 attach 시점 캐시값)로만 셋되고 새 입력에 의해 변경되지 않는지 코드 검토.
- [ ] `[auto-soft]` 컴파일 후 Unity Editor 콘솔에 본 컴포넌트 관련 NullReferenceException / MissingComponent 에러가 0건이다.
  **검증:** Unity MCP `read_console types=["error","warning"] filter_text="TromboneBodyVisualSnap"` → 0건.
- [ ] `[manual-hard]` Editor Play Mode에서 트롬본을 anchor에서 잡지 않은 상태(IsAttached=false)일 때 Rig/Body의 표시 회전이 prefab 원본 그대로 보인다.
  **검증:** Play Mode 진입 → 트롬본 anchor에 진입하지 않은 채 Scene View에서 Trombone/Rig/Body의 Transform inspector를 열고 localEulerAngles가 prefab 원본 값과 일치하는지 시각 확인.
- [ ] `[manual-hard]` 트롬본을 잡은 후(IsAttached=true) trombone root z회전을 -25° 근처로 두면 Rig/Body의 표시 z회전이 약 100ms 안에 -15°(파셜 1 anchor)로 수렴하고, -8°↔+8° 범위로 천천히 흔들 때 Rig/Body 표시 z회전은 0°(파셜 2 anchor)에 정지된 채 유지된다 (입력 변화에도 표시 정지).
  **검증:** Play Mode + VR rig(또는 mock controller)로 트롬본 잡은 상태에서 Scene View Transform inspector의 Trombone/Rig/Body.localEulerAngles.z 값을 라이브 확인 — 파셜 경계 통과 시 ease 곡선으로 다음 anchor로 이동, 같은 파셜 안에서는 정지.
- [ ] `[manual-hard]` 트롬본을 놓으면(anchor 외부 이동, IsAttached=false) Rig/Body 표시 회전이 원래 prefab 값으로 복원되어 입력 회전을 그대로 따른다.
  **검증:** Play Mode에서 트롬본 attach → detach 시 Scene View Transform inspector의 Trombone/Rig/Body.localEulerAngles가 prefab 원본 값으로 되돌아가고, 이후 trombone root를 직접 움직여도 Body z회전이 root와 동일하게 따라가는지 시각 확인.

## Out of Scope

- `TromboneNoteDisplayAdapter`/`TromboneNoteHud`의 노트 UI 5개 패널 layout (sub-spec 02-note-vertical-layout 책임)
- 왼손 GripPoseHand가 Body 회전을 따라가면서 발생할 수 있는 grip pose 미세 어긋남 보정 (spec Out of Scope; Hands 도메인 책임)
- Slide/MouthPiece/SlidePositionMarkers/NoteDisplay의 회전 보정 (ARD 01에서 명시적 제외)
- `anglePerPartial=15°` / `centerPartialIndex=2` 등 PartialController 파라미터 값 변경
- VR 카메라/시점 강제 제어 (멀미 위험으로 명시적 제외)
- TrombonePartialController의 입력 처리·hysteresis·PartialIndex 산출 로직 변경 (Tech Spec Boundaries 준수)

## Notes

- `centerPartialIndex` 노출은 SSOT를 위해 PartialController에 1줄 getter 추가로 해결. 만약 후속 리뷰에서 "PartialController 표면 확장 금지"가 결정되면 본 컴포넌트가 자체 `[SerializeField] int centerPartialIndex = 2;`를 두고 prefab inspector에서 페어링하는 방향으로 ARD 1건 추가 후 본 plan 수정 필요.
- attach 직후 첫 LateUpdate에서 ease 없이 anchor로 instant 점프하는 선택은 "attach 시점 깜빡임 최소화"를 위해 본 plan에서 결정. ARD에 박제되지 않은 결정이므로 사용자 피드백에서 "attach 직후 점프가 거칠다"가 보고되면 별도 ARD 작성 권장.
- exponential ease-out factor `k=4` default는 `1 - exp(-4) ≈ 0.982`로 100ms 종료 시점에 target의 98% 이상 도달 → 시각상 정확히 anchor에 수렴한 것으로 보이며 마지막 2% 미만 잔여는 t=1 보정에서 0으로 정리됨. AnimationCurve 대비 결정론·디버깅 용이성·SerializeField 단순성 이점.
- **Gimbal lock 표시 부작용 (manual-hard 검증 중 발견)**: Trombone.prefab의 Body 원본 Euler X=270°(=-90°)는 정확히 gimbal lock 경계다. `ApplyZ`가 의도한 `(orig.x, orig.y, targetZ)` Euler를 set하면 Unity Quaternion 정규화 후 Euler readback이 `(270, 360-targetZ, 0)` 형태로 표시된다. **시각 회전 자체는 plan 의도와 완전 일치** (Quaternion diff 0.000°, 원본 대비 정확히 `|targetZ|°` z축 회전). Euler-string 기반 자동 검증은 misleading하므로 후속 회귀 테스트는 `Quaternion.Angle(actual, Quaternion.Euler(orig.x, orig.y, targetZ))` 기준으로 비교한다.
- **수동 검증 시 PartialController 상호작용 주의**: PartialController의 `tromboneRoot` SerializeField는 `Trombone/Rig`(root 자체가 아님)를 가리키고 `angleSignMultiplier=-1`이다. 즉 `Rig.localEulerAngles.z`가 양수면 사용자 시점에서 head-down이며 PartialIndex가 증가한다. 사양 Behavior의 "root z회전 -25°" 표현은 PartialController 입력 모델 기준이며, 실제 입력 transform은 Rig다. 본 plan은 PartialController의 PartialIndex만 읽으므로 영향 없음 — 단 후속 retro 테스트나 manual 검증 시나리오 작성 시 transform/sign 모델을 정확히 기재할 것.

## Handoff

- **신규 public API**: `Instruments.TromboneBodyVisualSnap` (`Assets/Instruments/Trombone/Scripts/TromboneBodyVisualSnap.cs`, `[DefaultExecutionOrder(10006)]`, `sealed`). Trombone.prefab의 `Trombone/Rig/Body`에 부착. SerializeField: `tromboneAnchor`, `partialController`, `body`, `easeDurationSeconds=0.1f`, `easeExponent=4f`.
- **TrombonePartialController 표면 확장**: `public int CenterPartialIndex => centerPartialIndex;` 1줄 추가 (라인 30). hysteresis·LateUpdate·ThresholdFor 등 입력 처리 로직 무변경.
- **동작 경계**: Rig/Body Transform.localRotation의 z 자유도만 변경 (Quaternion 공간). x/y는 attach 시점 캐시값 유지. Slide·MouthPiece·NoteDisplay·Trombone root 무변경.
- **자동 검증 결과**: EditMode 133/133 pass, PlayMode 4/4 pass, console error 0건. reviewer pass. auto-hard 4 + auto-soft 1 직접 검증 pass.
- **Manual-hard 자동 검증 결과** (2026-05-27, sub-agent execute_code via PlayMode):
  - AC6 (IsAttached=false 베이스라인): Body localEuler `(270.02, 0.00, 0.00)` = prefab 원본 그대로. **PASS**
  - AC7 (IsAttached=true + PartialIndex=1 강제 → anchor=-15°): visualSnap 적용 회전 = `Q(270.02, 0, -15)`와 Quaternion diff **0.000°**, 원본 대비 정확히 **15.000°** z축 회전. **PASS** (Euler readback (270, 345.01, 0)는 gimbal lock 표시 부작용일 뿐, 시각 회전은 정확)
  - AC8 (IsAttached=false 강제 detach): `m_LastPartialIndex=-1`, `m_IsBoosting=false`, Body Q diff `0.0000°` 원복. **PASS**
- **후속 sub-spec(02-note-vertical-layout)에 넘길 컨텍스트**: Trombone prefab에 `PanelAnchor` child를 추가할 때 `Trombone/Rig` 형제 또는 `Trombone` root 직속에 두면 본 컴포넌트(Body 자식)의 회전 보정과 독립적으로 유지된다. NoteDisplay는 현재 `Trombone/Rig/NoteDisplay`(Canvas)이며 본 plan에서 무변경.
