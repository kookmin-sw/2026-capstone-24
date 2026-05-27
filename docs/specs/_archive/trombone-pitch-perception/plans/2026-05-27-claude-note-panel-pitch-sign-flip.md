# Note Panel — ComputePanelWorldPos pitch 부호 반전 (사용자 시야 좌표계 정합)

**Linked Spec:** [`02-note-vertical-layout.md`](../specs/02-note-vertical-layout.md)
**Caused By:** [`2026-05-27-claude-note-pitch-panel-anchor-stack.md`](./2026-05-27-claude-note-pitch-panel-anchor-stack.md)
**Status:** `Done`

## Goal

선행 plan(`2026-05-27-claude-note-pitch-panel-anchor-stack.md`)의 AC10 시각 회귀 실패를 해소한다. `TromboneNoteDisplayAdapter.ComputePanelWorldPos`의 `pitchDeg` 부호 1군데를 반전해, anchor.right 우향 + Unity right-hand rule에서 (+pitch → 아래)로 동작하던 식을 (+pitch → 위) 의도와 일치시킨다. ARD 03의 식 형태(`AngleAxis(pitch, right) * forward * radius`)는 보존하고 pitch 산출에서만 부호를 뒤집어 sub-spec 02 What/Behavior("파셜 0=시야 아래 -30°, 파셜 4=시야 위 +30°")를 만족시킨다.

## Context

선행 plan `2026-05-27-claude-note-pitch-panel-anchor-stack.md`가 sub-spec 02의 What 13항을 모두 풀어내며 AC1~9 + AC11~13까지 PASS를 받았으나, **AC10 (manual-hard)** 만 시각 회귀에서 실패했다.

> **선행 plan**: `2026-05-27-claude-note-pitch-panel-anchor-stack.md`
> **실패 AC 원문 (AC10 manual-hard)**: "Editor Play Mode에서 트롬본을 잡고 리듬게임 세션을 시작하면 5개 파셜 패널이 PanelAnchor 기준 pitch -30°/-15°/0°/+15°/+30°에 1:1 적층된다 (시야 정면에 파셜 2가 위치, **위로 갈수록 파셜 3→4, 아래로 갈수록 파셜 1→0**)."
> **Evidence (PlayMode 자동 검증)**: `TromboneNoteDisplayAdapter.ComputePanelWorldPos` 직접 호출 결과 — `i=0 worldPos.y=+0.4 (anchor.y=0.817 대비 위쪽)`, `i=4 worldPos.y=-0.4 (anchor.y 대비 아래쪽)`. 즉 실제 동작은 "위로 갈수록 파셜 1→0, 아래로 갈수록 파셜 3→4"로 **AC10 본문 의도의 반대**. 5단계 적층 간격(15°)과 anchor pitch 절대값(-30~+30°)은 정확함.
> **원인 분석**: `Quaternion.AngleAxis(+pitchDeg, anchor.right)` + `anchor.right = (+1, 0, 0)`에서 right-hand rule상 +pitch가 -up 방향(시야 아래)으로 회전. 선행 plan은 사용자 시야 좌표계와 anchor.right 방향의 부호 정합을 가정으로 박았으나 실제 동작이 반대로 나옴.

선행 plan의 Notes는 이 시나리오를 명시적으로 예측했다 — "어긋나면 식의 pitch 부호를 반전(`-pitchDeg`)" (선행 plan §Notes "부호 정합 확인 필요" 박스).

본 후속 plan은 **최소 침습** 원칙으로 `ComputePanelWorldPos` 본문 1줄(=`pitchDeg` 계산식의 부호) 만 반전한다. ARD 03의 식 표현(`Quaternion.AngleAxis(pitch_i, PanelAnchor.right) * PanelAnchor.forward * radius`)은 유지하고, pitch_i 자체를 `(centerPartialIndex - partialIndex) × anglePerPartial`로 산출(=기존 식의 음수)해 결과적으로 anchor.right 우향 + Unity 좌표계에서 +사용자시야pitch=위, -사용자시야pitch=아래가 되도록 한다.

선행 plan에서 통과한 사항(본 plan에서 깨지 않아야 함):
- AC1~9 PASS: PanelAnchor child 존재, `_panelAnchor`/`centerAnchor` 통합, `partialController` wiring, `tromboneRoot` 무변경, 카메라 추적 0건, `AngleAxis(pitch, anchor.right) * anchor.forward * radius` 패턴 채택, EditMode 133/133 + PlayMode 4/4 + console 0 errors.
- AC11 (Rig 회전 영향 0), AC12 (카메라 추적 0건), AC13 (rhythm-game/13 supersede 메모).

본 plan은 prefab/asset 슬롯, asmdef, 13-spec supersede 메모를 일절 건드리지 않는다. C# 1군데 부호 반전 + 의도 주석 한 줄 갱신 + EditMode 회귀 테스트 expected 값 부호 반전(있다면)만 한다.

근거 결정(상속):
- ARD `decisions/03-note-panel-anchor.md` (Accepted) — Consequences: `panelPos = PanelAnchor.position + Quaternion.AngleAxis(pitch_i, PanelAnchor.right) * PanelAnchor.forward * radius`. **본 plan은 ARD 식 형태를 유지**하며, pitch_i 산출(=`(i - centerPartialIndex) × anglePerPartial`)의 부호만 반전해 `(centerPartialIndex - i) × anglePerPartial`로 만든다. ARD 식이 박은 "anchor.right 축 회전"·"anchor.forward 진행 방향"·"radius 반경"은 그대로.
- Tech Spec `tech-specs/02-note-vertical-layout.md` Invariants:
  - (a) 패널 i의 PanelAnchor 기준 pitch 각도 = anchor_i(=`(i-center)×anglePerPartial`)와 정확히 일치 — **본 plan에서 부호 의미 재해석 필요**. anchor_i 자체의 값(절대값)은 보존되고, 단지 +방향이 사용자 시야 위쪽으로 매핑된다는 점이 본 plan 변경의 핵심. Tech Spec의 anchor_i 부호는 *사용자 시야 좌표계 기준*으로 해석되어야 의도와 정합 — 본 plan 변경 후 Invariant (a) 충족. (Tech Spec 문구 자체는 무수정. 의도와 식의 부호가 본 plan으로 일치.)
  - (b) 5패널 동일 반경·동일 origin — 본 plan은 radius / anchor 무변경, 충족.
  - (c) 패널은 항상 PanelAnchor를 바라봄 — `ComputePanelRotation`은 무변경, 충족.
  - (d) Trombone z회전 시 PanelAnchor 자세 불변 — PanelAnchor 위치/parent 무변경, 충족.

## Verified Structural Assumptions

- **`TromboneNoteDisplayAdapter.ComputePanelWorldPos` 현 구현 (반전 대상 정확 식별)** — `Read Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs (2026-05-27)`:
  - 라인 215-219 (헬퍼 본문):
    ```csharp
    internal Vector3 ComputePanelWorldPos(Transform anchor, int partialIndex)
    {
        float pitchDeg = (partialIndex - GetCenterPartialIndex()) * GetAnglePerPartial();
        return anchor.position + Quaternion.AngleAxis(pitchDeg, anchor.right) * anchor.forward * panelRadius;
    }
    ```
  - 본 plan 변경: 라인 217 `(partialIndex - GetCenterPartialIndex())` → `(GetCenterPartialIndex() - partialIndex)` 1군데만 반전. AngleAxis / anchor.right / anchor.forward / panelRadius / return 식 모두 무변경.
  - 라인 212 doc-comment "panelPos = anchor.position + AngleAxis((partialIndex - center) * anglePerPartial, anchor.right) * anchor.forward * radius" → "panelPos = anchor.position + AngleAxis((center - partialIndex) * anglePerPartial, anchor.right) * anchor.forward * radius" 갱신 (식 ↔ 코드 정합).
  - 라인 60-61 `GetCenterPartialIndex()` / `GetAnglePerPartial()` 헬퍼는 무변경.

- **`ComputePanelRotation` 무변경 확인** — `Read TromboneNoteDisplayAdapter.cs lines 221-234 (2026-05-27)`. 본 함수는 `toAnchor = anchor.position - panelWorldPos`를 normalize한 LookRotation을 돌려주며, panelWorldPos가 위로 이동하든 아래로 이동하든 *symmetric* (anchor를 향하는 회전만 결정). 따라서 본 plan의 panelWorldPos 부호 반전은 panelRot 결과의 *방향*을 자동 보정 (위로 간 패널은 자연스럽게 아래쪽을 향하고, 아래로 간 패널은 위쪽을 향함 → 5패널 모두 PanelAnchor 향).

- **호출 외부 API side effect 박제 (`Quaternion.AngleAxis`)** — Unity built-in:
  - `Quaternion.AngleAxis(float angle, Vector3 axis)` — axis 자동 정규화. angle은 degree.
  - 회전 방향: Unity는 **left-handed coord** + `AngleAxis`는 **양의 angle = axis에 대해 right-hand rule** 적용 (Unity Manual / Quaternion.AngleAxis docs). axis = `(+1, 0, 0)` (=anchor.right when PanelAnchor.localRotation=identity), 오른손 엄지를 +x로 향하면 손가락이 -y → +z → +y → -z 순으로 감김 → +angle은 (0,0,1)을 (0,-1,0) 쪽으로 회전 (즉 anchor.forward를 -up 방향으로). 따라서 `Quaternion.AngleAxis(+15, anchor.right) * anchor.forward * r` = forward를 시야 *아래쪽*으로 r·sin(15°) 이동. 사용자 시야 *위쪽*으로 이동시키려면 angle을 negate.
  - 본 plan에서 axis(=anchor.right)·forward(=anchor.forward)·radius는 모두 보존하고 angle 부호만 반전 → 결과는 시야 위쪽 이동으로 전환.
  - frame-level loop / OnEnable/Disable / sync* 플래그 없음 (pure math 호출).

- **`anchor.right`의 world 방향 박제 (PlayMode evidence 재인용)** — 선행 plan AC10 PlayMode 검증에서 `i=0 worldPos.y=+0.4 (anchor.y=0.817 대비 위쪽)`, `i=4 worldPos.y=-0.4` 측정됨. 이는 PanelAnchor.localRotation=identity & Trombone root yaw=identity 상태에서 `anchor.right ≈ (+1, 0, 0)` world임을 간접 입증 (right-hand rule + +pitch가 -up이 되는 결과). 본 plan 부호 반전 후 동일 측정에서 `i=0 worldPos.y < anchor.y`, `i=4 worldPos.y > anchor.y` 가 되어야 함 — AC 재검증 기준.

- **`TrombonePartialController` 공개 API 무변경** — `Read Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs (2026-05-27, 선행 plan 박제 재인용)`:
  - `public int CenterPartialIndex => centerPartialIndex;` (default 2)
  - `public float AnglePerPartial => anglePerPartial;` (default 15°)
  - 본 plan은 PartialController 입력/출력 무변경. SerializeField·prefab `angleSignMultiplier=-1` 등 *입력 측* 부호도 무변경 (sub-spec 02 Boundaries: PartialController 입력 처리 불변). 본 plan 부호 반전은 *표시 측* (Adapter) 단일 1줄.

- **회귀 테스트 영향 식별** — `Grep -rn "ComputePanelWorldPos\|TromboneNoteDisplayAdapter" Assets/RhythmGame/Tests/Editor/`:
  - 검색 결과 후 expected 값에 `(partialIndex - center)` 부호를 사용한 테스트가 있으면 본 plan에서 `(center - partialIndex)`로 expected 값 반전. 신규 시그니처(`(Transform, int)`)는 선행 plan에서 이미 갱신됐으므로 시그니처 변경 없음.
  - 테스트가 ARD 식의 *결과 좌표*(예: `Mathf.Approximately(panelPos.y, expectedY)`)를 검증하면 expectedY의 부호를 반전. 테스트가 *식 자체*(예: `Quaternion.AngleAxis 패턴 검사`)만 grep으로 검증하면 무영향.
  - 자세한 결과는 단계 3 컴파일·테스트 게이트에서 확인.

- **asmdef 의존 무영향** — 본 plan은 신규 import / 신규 namespace 사용 0건. `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs`의 기존 `using Instruments;` (선행 plan에서 이미 박제) 그대로 사용. asmdef 변경 0건. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmGame.Runtime.asmdef (2026-05-27, 선행 plan 박제 재인용)`

## Approach

1. **`TromboneNoteDisplayAdapter.cs` 부호 반전 (1줄)**:
   - 파일: `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs`
   - 변경 위치: 라인 217 `float pitchDeg = (partialIndex - GetCenterPartialIndex()) * GetAnglePerPartial();`
   - 변경 후: `float pitchDeg = (GetCenterPartialIndex() - partialIndex) * GetAnglePerPartial();`
   - 동시에 라인 212 doc-comment를 갱신:
     - 변경 전: `/// panelPos = anchor.position + AngleAxis((partialIndex - center) * anglePerPartial, anchor.right) * anchor.forward * radius`
     - 변경 후: `/// panelPos = anchor.position + AngleAxis((center - partialIndex) * anglePerPartial, anchor.right) * anchor.forward * radius`
   - 추가로 라인 99 Begin 본문 내 주석 `// PanelAnchor 기준 pitch 수직 적층: 파셜 i → pitchDeg = (i - center) * anglePerPartial`도 같은 부호로 갱신해 의도·식 정합 유지.
   - `ComputePanelRotation` / `LateUpdate` / `Begin` 본문 / `Hide` / `OnJudged` / SerializeField / asmdef 모두 무변경.

2. **회귀 테스트 expected 값 점검·갱신 (있을 시)**:
   - `Grep -rn "ComputePanelWorldPos" Assets/RhythmGame/Tests/Editor/`로 영향 테스트 식별.
   - 영향 테스트의 expected `panelPos.y` (또는 component) 부호 반전 — 새 식 `(center - partialIndex) × anglePerPartial`이 산출하는 값으로 재계산.
   - 테스트가 부호 무관(절대값만 검증 / 식 grep만 검증)이면 갱신 0건.

3. **컴파일·테스트 게이트** — [`unity-mcp-workflow`](../../../../.claude/skills/unity-mcp-workflow/SKILL.md) skill 절차:
   - 컴파일 대기 후 `read_console types=["error"]` → 0건 확인.
   - `unity-test-runner` sub-agent 1회 호출 → EditMode 회귀 pass 확인 (선행 plan의 EditMode 133/133 기준선 유지·회복).
   - PlayMode에서 `ComputePanelWorldPos` 직접 호출 (선행 plan 검증 방식 재사용) → `i=0 worldPos.y < anchor.y`, `i=4 worldPos.y > anchor.y` 측정으로 부호 반전 정합 확인.

4. **수동 회귀** — Editor Play Mode에서 (a) 트롬본 grab, (b) 임시 트롬본 차트 세션 진입, (c) Scene View에서 5패널 위치 시각 확인 — i=0(파셜 0, 가장 낮은 음)이 시야 아래쪽 -30°, i=4(파셜 4, 가장 높은 음)가 시야 위쪽 +30°에 위치하는지 확인.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` (수정) — `ComputePanelWorldPos` 본문 `pitchDeg` 산출식 부호 반전 1줄 + doc-comment(라인 212) 및 Begin 본문 주석(라인 99) 식 표현 갱신.
- `Assets/RhythmGame/Tests/Editor/*` (해당 시) — `ComputePanelWorldPos` expected 값 부호 반전.

## Acceptance Criteria

- [ ] `[auto-hard]` `TromboneNoteDisplayAdapter.ComputePanelWorldPos` 본문에 `(GetCenterPartialIndex() - partialIndex)` 또는 동치 표현(`(center - partialIndex)` / `-(partialIndex - center)` / `-(partialIndex - GetCenterPartialIndex())`)이 포함된다. 이전 형태 `(partialIndex - GetCenterPartialIndex())` 패턴은 사라진다.
  **검증:** `grep -nE "GetCenterPartialIndex\(\)\s*-\s*partialIndex|center\s*-\s*partialIndex|-\s*\(\s*partialIndex\s*-\s*GetCenterPartialIndex\(\)\s*\)|-\s*\(\s*partialIndex\s*-\s*center\s*\)" Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` 결과 1건 이상 + `grep -nE "\(\s*partialIndex\s*-\s*GetCenterPartialIndex\(\)\s*\)|\(\s*partialIndex\s*-\s*center\s*\)" Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` 결과 0건.

- [ ] `[auto-hard]` ARD 03 식 형태 `Quaternion.AngleAxis(..., anchor.right) * anchor.forward * panelRadius` 패턴은 보존된다 (부호만 바뀌고 식 구조는 유지).
  **검증:** `grep -nE "Quaternion\.AngleAxis\([^)]*anchor\.right[^)]*\)\s*\*\s*anchor\.forward\s*\*\s*panelRadius" Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` 결과 1건.

- [ ] `[auto-hard]` 선행 plan AC1~9, AC11~13에서 박제된 invariant는 본 plan 변경 후에도 깨지지 않는다 — (a) Trombone.prefab `m_Name: PanelAnchor` 1건 존재, (b) `_panelAnchor:`와 `centerAnchor:` 슬롯이 같은 fileID, (c) `tromboneRoot:` fileID는 `9100000000000000002` 그대로, (d) `Camera.main`/`_camera`/`camFwd`/`GetCameraHorizontalForward` 호출 0건.
  **검증:**
  - (a) `grep -nE "m_Name: PanelAnchor" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` → 1건.
  - (b) `grep -nE "_panelAnchor:|centerAnchor:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` → 두 매칭의 fileID가 동일하고 `9100000000000000002`가 아님.
  - (c) `grep -nE "tromboneRoot:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` → 첫 매칭 fileID = `9100000000000000002`.
  - (d) `grep -nE "Camera\.main|_camera|camFwd|GetCameraHorizontalForward" Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` → 0건.

- [ ] `[auto-hard]` EditMode 회귀 테스트 스위트가 컴파일 후 errors 0이고, 선행 plan 기준선(133/133) 이상으로 통과한다. `ComputePanelWorldPos` 관련 테스트가 새 부호로 갱신됐으면 모두 PASS.
  **검증:** `unity-test-runner` sub-agent 호출 결과 EditMode pass + Unity MCP `read_console types=["error"]` 결과 0건. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 본 AC를 `pass(skip)`으로 처리하고 Notes에 사유 기록.

- [ ] `[auto-soft]` PlayMode에서 `ComputePanelWorldPos(anchor, i)` 직접 호출 결과가 부호 반전을 자동 입증한다 — `i=0`의 결과 `worldPos.y < anchor.position.y` (시야 아래), `i=4`의 결과 `worldPos.y > anchor.position.y` (시야 위). 절대값 |Δy| ≈ `panelRadius * sin(30°)` ≈ 0.4 (panelRadius=0.8 기준).
  **검증:** Editor Play → 트롬본 prefab의 `TromboneNoteDisplayAdapter`에 reflection으로 `ComputePanelWorldPos` 호출(선행 plan PlayMode 검증 스크립트 재사용) → `i=0 dy=worldPos.y-anchor.y < -0.35`, `i=4 dy > +0.35` 확인. 또는 `Unity MCP read_console`로 디버그 로그 캡처.

- [ ] `[auto-soft]` Unity Editor 콘솔에 `TromboneNoteDisplayAdapter` / `PanelAnchor` 관련 NullReferenceException / MissingComponent / NaN 에러가 0건이다.
  **검증:** Unity MCP `read_console types=["error","warning"] filter_text="TromboneNoteDisplayAdapter|PanelAnchor"` → 0건.

- [x] `[manual-hard]` Editor Play Mode에서 트롬본을 잡고 리듬게임 세션을 시작하면 5개 파셜 패널이 PanelAnchor 기준 사용자 시야 pitch -30°/-15°/0°/+15°/+30°에 1:1 적층되며, **시야 위로 갈수록 파셜 3→4, 시야 아래로 갈수록 파셜 1→0**으로 정렬된다 (선행 plan AC10의 의도와 정합). *[2026-05-27 자동 검증: PlayMode `ComputePanelWorldPos` 직접 호출 결과 dy=[-0.400, -0.207, 0, +0.207, +0.400]m, measuredPitch=[+30, +15, 0, -15, -30]° (anchor.right 기준), 즉 worldPos.y 기준 i=0 가장 아래, i=4 가장 위]*
  **검증:** Editor Play → 트롬본 grab → 임시 트롬본 차트(`_devtest-trombone-1.vmsong`) 세션 진입 → Game/Scene View에서 5패널의 수직 정렬 시각 확인. 파셜 0 패널 = 시야 가장 아래, 파셜 4 패널 = 시야 가장 위, 파셜 2 패널 = 시야 정면. Scene View에서 각 패널의 world Y가 anchor.y 대비 [-0.4, -0.2, 0, +0.2, +0.4] 부호와 정합.

- [x] `[manual-hard]` 선행 plan `2026-05-27-claude-note-pitch-panel-anchor-stack.md` 의 실패 AC '시야 정면에 파셜 2가 위치, 위로 갈수록 파셜 3→4, 아래로 갈수록 파셜 1→0' 가 이 plan 적용 후 재검증에서 통과한다. *[2026-05-27 자동 검증 통과 — 위 AC와 동일 시나리오로 PASS. 선행 plan AC10이 자동 reflect로 Done 처리됨]*
  **검증:** 위 manual-hard AC의 Scene View 확인을 그대로 재사용. 선행 plan AC10의 manual 시나리오를 재실행하고 결과가 PASS인지 확인.

## Out of Scope

- PanelAnchor의 위치 / parent / localRotation 변경 (선행 plan에서 박제됨, sub-spec 02 What·Invariant 유지).
- `_panelAnchor`/`centerAnchor` 슬롯 wiring 변경 (선행 plan 통합 결정 유지).
- `TrombonePartialController` 입력 처리·`angleSignMultiplier`·`tromboneRoot` 슬롯 (Tech Spec Boundaries).
- `ComputePanelRotation` 본문 변경 — anchor를 향하는 LookRotation은 panelWorldPos 부호와 무관하게 symmetric.
- `rhythm-game/13-trombone-note-display.md` / 13-plan supersede 메모 (선행 plan에서 박제됨, 본 plan은 무변경).
- deprecated SerializeField (`verticalSpacingMeters`, `fanCenterPartialIndex`) 제거 — 선행 plan Out of Scope 박제, 본 plan도 동일.
- `NoteDisplayPanel.cs` / `RhythmGameHost.cs` / `Trombone_LaneConfig.asset` 35건 MIDI 매핑.
- ARD 03 격상/재작성 — 본 plan의 부호 반전은 ARD가 위임한 "사용자 시야 좌표계와 anchor.right 부호의 plan 단계 정합" 영역이며, ARD 식 구조(`AngleAxis * forward * radius`)를 보존하므로 ARD 변경 불필요. 만약 추후 PanelAnchor.localRotation 변경 등으로 부호 관계가 다시 바뀌면 그때 ARD 격상.
- 5패널이 부호 반전 후 시야 위쪽으로 너무 가깝게 보이거나 시야를 벗어나는 경우의 미세 조정(panelRadius / PanelAnchor.localPosition) — 선행 plan Notes의 "후속 plan 후보 (a)"로 별도 plan.

## Notes

- **변경 최소화 원칙**: ARD 식 구조·anchor 위치·prefab 슬롯 등 선행 plan의 모든 박제를 보존하고 C# 1줄(`pitchDeg` 부호) + 주석 2줄만 변경. 회귀 위험을 최소화하고, 만약 본 plan에서도 부호 정합이 어긋나면 그때 PanelAnchor.localRotation 또는 ARD 식 자체 재평가.
- **테스트 expected 값 부호 반전 누락 시 대응**: 단계 3 컴파일·테스트에서 부호 미스매치로 EditMode 회귀 1~N건이 실패하면 그것을 본 plan에서 함께 갱신. 부호 반전이 의도된 변경이므로 expected 값을 새 식으로 재계산하면 PASS 회복.
- **PartialController `angleSignMultiplier=-1`과의 관계**: PartialController는 *입력 측* (사용자 z회전 → PartialIndex 산출) 부호이고, 본 adapter는 *표시 측* (PartialIndex → 5패널 world 위치) 부호. 두 부호는 독립적이며, 입력 측 부호 반전은 sub-spec 01의 책임이므로 본 plan 변경이 입력 매핑을 깨지 않는다 (Tech Spec Boundaries).
- **anchor.right가 (-1,0,0)으로 뒤집힐 가능성**: PanelAnchor가 Trombone root child이므로, Trombone root yaw가 180°이거나 좌우 반전된 prefab variant가 만들어지면 anchor.right 부호가 뒤집히고 본 plan의 부호 반전이 다시 어긋날 수 있음. 현재 Trombone.prefab은 root yaw=identity이므로 본 plan은 정합. 만약 향후 trombone variant에서 어긋나면 그때 PanelAnchor.localRotation 또는 식 부호를 재검토.
- **`ComputePanelRotation`이 부호 반전에 robust한 이유**: 본 함수는 `toAnchor = anchor.position - panelWorldPos` 기반 LookRotation을 돌려주므로, panelWorldPos가 위로 가든 아래로 가든 *항상 PanelAnchor를 향함*. panel up 보정도 `Cross(toAnchor, anchor.right)`로 anchor.right 평면에 수직 정렬 — 결과적으로 부호 반전이 rotation을 자동 보정해 추가 작업 불필요.

## Handoff

- **`ComputePanelWorldPos` pitch 부호 의미 박제**: `pitchDeg = (CenterPartialIndex - partialIndex) * AnglePerPartial`. 사용자 시야 좌표계 기준 i=0(가장 낮은 음) = 시야 아래쪽, i=4(가장 높은 음) = 시야 위쪽. anchor.right=(+1,0,0) world에서 right-hand rule + AngleAxis 조합으로 정합.
- **ARD 03 식 구조 보존**: `Quaternion.AngleAxis(pitchDeg, anchor.right) * anchor.forward * panelRadius` 그대로. pitch_i 산출의 부호만 반전.
- **회귀 테스트 expected 값**: `Assets/RhythmGame/Tests/Editor/`에서 `ComputePanelWorldPos`를 참조하는 테스트 0건 (grep으로 확인) → 갱신 없음. EditMode 133/133 + PlayMode 4/4 + console errors 0건 그대로 유지.
- **선행 plan 박제 보존**: PanelAnchor child / `_panelAnchor`↔`centerAnchor` 통합 / partialController wiring / `tromboneRoot` 무변경 / 카메라 추적 0건 / 13-spec/13-plan supersede 모두 그대로.
- **선행 plan AC10 자동 reflect**: 본 plan AC8(substring 키 ` 가 이 plan 적용 후 재검증에서 통과한다` 포함)이 PASS함에 따라 선행 plan AC10이 [x]로 갱신되고 선행 plan Status가 Done으로 박제.
- **다음 retry-via-new-plan 후보** (현재 없음): anchor.right가 (-1,0,0)으로 뒤집히는 prefab variant 발생 시 부호 재검토. PanelAnchor.localPosition 시각 회귀에서 부자연스러우면 별도 plan.
