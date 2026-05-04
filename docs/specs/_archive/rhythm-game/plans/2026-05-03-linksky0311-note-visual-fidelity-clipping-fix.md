# Note Visual Fidelity 클리핑 버그 수정

**Linked Spec:** [`../specs/02-note-visual-fidelity.md`](../specs/02-note-visual-fidelity.md)
**Caused By:** [`2026-05-03-linksky0311-note-visual-fidelity-height-and-skin.md`](./2026-05-03-linksky0311-note-visual-fidelity-height-and-skin.md)
**Status:** `Done`

## Goal

`SpawnNote()`의 노트 높이 클리핑 공식 버그를 수정하여 음표 길이에 비례한 높이가 실제로 표시되도록 한다.

## Context

> **선행 plan 검증 실패에서 파생됨.** 선행: `2026-05-03-linksky0311-note-visual-fidelity-height-and-skin.md`.
> 실패한 Acceptance Criteria:
> - `[manual-hard]` 세션 실행 시 C4 4분음표와 E4 2분음표 노트를 비교하면 E4 노트가 C4 노트의 약 2배 높이로 낙하한다. — 모든 노트 높이가 매우 짧아짐. clipping bug: `noteH = Mathf.Min(noteH, panelHeight - startY)` → ~0 at spawn
>
> 본 plan은 위 항목을 다시 통과 가능하게 만드는 부속 작업을 다룬다.

### 버그 원인 분석

선행 plan(`height-and-skin`)이 `SpawnNote()`에서 아래 클리핑 코드를 적용했다:

```csharp
float noteH = Mathf.Max(fallSpeed * pn.durationSec, 4f);
noteH = Mathf.Min(noteH, panelHeight - startY);  // ← 버그
```

`startY`는 노트 스폰 Y 위치(= `panelHeight` 부근, 패널 최상단)이므로, 스폰 시점에 `panelHeight - startY ≈ 0`이 되어 `noteH`가 거의 0으로 클리핑된다.

**올바른 클리핑:** 노트 높이가 패널 전체 높이를 초과하지 않도록 제한하는 것이 spec 의도("패널 상단에서 클리핑")이므로, 기준값은 `panelHeight - startY`가 아니라 `panelHeight`여야 한다.

```csharp
noteH = Mathf.Min(noteH, panelHeight);  // ← 수정 후
```

### 선행 plan 코드 상태

선행 plan이 working tree에 다음 변경을 이미 적용한 상태다:

- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs`
  - `whiteKeySkinPrefab` / `blackKeySkinPrefab` Inspector 필드 추가
  - `SpawnNote()` 높이 계산: `fallSpeed * pn.durationSec` (최소 4px)
  - 클리핑: `Mathf.Min(noteH, panelHeight - startY)` ← **이 줄만 수정 대상**
  - 수명 보정: `(startY + noteH) / fallSpeed + 0.5f`
  - 프리팹 2슬롯 선택 + 2D/3D 사이징 분기

본 plan은 위 변경 중 클리핑 한 줄만 수정한다. 나머지 변경은 그대로 유지한다.

## Verified Structural Assumptions

_해당 없음 — 순수 로직 변경_

## Approach

1. **`NoteDisplayPanel.cs` — 클리핑 공식 수정**
   - `SpawnNote()` 메서드에서 다음 한 줄을 교체한다:
     ```csharp
     // Before (버그)
     noteH = Mathf.Min(noteH, panelHeight - startY);

     // After (수정)
     noteH = Mathf.Min(noteH, panelHeight);
     ```
   - 파일 경로: `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs`

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` — 클리핑 공식 1줄 수정

## Acceptance Criteria

- [ ] `[auto-hard]` 변경 후 Unity 컴파일 에러가 없다.
- [ ] `[manual-hard]` 세션 실행 시 C4 4분음표와 E4 2분음표 노트를 비교하면 E4 노트가 C4 노트의 약 2배 높이로 낙하한다.
- [ ] `[manual-hard]` 매우 긴 노트(예: 온음표)가 있을 때, 노트 높이가 패널 상단을 넘지 않고 클리핑되어 표시된다.
- [ ] `[manual-hard]` `whiteKeySkinPrefab`에 커스텀 프리팹을 할당하면 흰 건반 노트 스폰 시 해당 프리팹이 사용된다.
- [ ] `[manual-hard]` 세션 중 `whiteKeySkinPrefab`을 교체해도 이미 낙하 중인 노트는 기존 스킨을 유지하고, 이후 스폰되는 노트부터 새 스킨이 적용된다.
- [ ] `[manual-hard]` 선행 plan `2026-05-03-linksky0311-note-visual-fidelity-height-and-skin.md`의 실패 AC ("세션 실행 시 C4 4분음표와 E4 2분음표 노트를 비교하면 E4 노트가 C4 노트의 약 2배 높이로 낙하한") 가 이 plan 적용 후 재검증에서 통과한다.

## Out of Scope

- 노트 가로 너비 변경.
- 드럼 트랙 노트 높이 비례.
- 스킨 선택 UI.
- 애니메이션·파티클 효과.

## Notes

- 자동 reflect 매칭 실패 — 선행 plan `2026-05-03-linksky0311-note-visual-fidelity-height-and-skin.md`의 per_plan_history 항목이 orchestrator-state.json에 없음 (검증 실패로 commit 전 중단됨). 해당 plan의 manual-hard AC 재검증은 사용자가 수동으로 처리 필요.
- AC 3, 4 (whiteKeySkinPrefab / 런타임 교체): skip-and-continue — 커스텀 prefab 없어 테스트 불가. 이후 prefab 생성 시 별도 검증 필요.

## Handoff
