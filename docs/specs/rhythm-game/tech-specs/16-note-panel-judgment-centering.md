# Note Panel Judgment Line Centering — Tech Spec

**Sub-Spec:** [`16-note-panel-judgment-centering.md`](../specs/16-note-panel-judgment-centering.md)
**Status:** `Draft`
**Date:** 2026-05-27

## Components

- **TromboneNoteDisplayAdapter** (기존, 필드 추가) — `judgmentLineOffset` Inspector 필드를
  추가하고 `ComputePanelWorldPos`에서 `panelPos += yawRight * judgmentLineOffset` 오프셋 반영.

## Data / Control Flow

- `Begin()`: `ComputePanelWorldPos(anchor, p)` → `panelPos += yawRight * judgmentLineOffset` → 패널 배치
- `LateUpdate()`: 동일 오프셋으로 매 프레임 패널 위치 갱신

## Boundaries

- **건드린다**: `TromboneNoteDisplayAdapter.cs` (`judgmentLineOffset` 필드 + `ComputePanelWorldPos` 수정), `Trombone.prefab` `TromboneNoteDisplay` 컴포넌트 (`judgmentLineOffset = 0.75f` 설정)
- **건드리지 않는다**: `NoteDisplayPanel` 캔버스 레이아웃(판정선 Y 위치), `RhythmJudge` 판정 타이밍, 노트 스크롤 속도, 다른 악기 어댑터(`DrumNoteDisplayAdapter` 등)

## Invariants

- 오프셋 적용 후에도 `ComputePanelRotation`(anchor를 향해 회전)은 변경 없이 유효하다.
- `judgmentLineOffset = 0`이면 기존 동작과 동일하다.

## Assumptions

**Prefab Hierarchy** (Unity MCP `manage_prefabs.get_hierarchy`, 2026-05-27):

```
Trombone (root)
  ├── Rig
  │   └── PanelAnchor          ← TromboneNoteDisplayAdapter.centerAnchor 참조 대상
  └── TromboneNoteDisplay      ← TromboneNoteDisplayAdapter 부착
```

- `GetYawDirections`: `yawRight = Cross(Vector3.up, yawForward)` = 플레이어 left 방향 — Read `TromboneNoteDisplayAdapter.cs` (2026-05-27)
- 판정선 = `NoteDisplayPanel` 캔버스 `Y = rect.y` (bottom). 세계 공간: 패널 중심 + yawLeft × (scrollLength/2) — Read `NoteDisplayPanel.cs` (2026-05-27)
- `TromboneNoteDisplayAdapter.panelScrollLengthMeters` 기본값 1.5f → 오프셋 목표값 0.75f — Read `TromboneNoteDisplayAdapter.cs` (2026-05-27)

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `DrumNoteDisplayAdapter.cs` | `TromboneNoteDisplayAdapter` | 드럼은 고정 worldPosition; 트롬본은 PanelAnchor 기준 pitch + 이번 yaw 오프셋 |

## Open Tech Decisions

→ decisions/04-panel-offset-mode.md
