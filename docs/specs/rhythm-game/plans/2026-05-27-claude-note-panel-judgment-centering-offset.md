# Trombone Note Panel — Judgment Line Offset 값 조정

**Linked Spec:** [`16-note-panel-judgment-centering.md`](../specs/16-note-panel-judgment-centering.md)
**Caused By:** [`2026-05-27-claude-note-panel-judgment-centering-dir.md`](./2026-05-27-claude-note-panel-judgment-centering-dir.md)
**Status:** `Done`

## Goal

방향 반전 후 다양한 offset 값을 시험해 최종적으로 `judgmentLineOffset = -0.1`로 판정선을 시야 중앙에 정렬.
코드 변경 없음, prefab 값만 교체.

## Approach

`Trombone.prefab` `TromboneNoteDisplayAdapter` 블록:

```yaml
# 최종
  judgmentLineOffset: -0.1
```

## Deliverables

- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` — `judgmentLineOffset: -0.1` 박제.

## Acceptance Criteria

- [ ] `[auto-hard]` `Trombone.prefab`에 `judgmentLineOffset: -0.1` 1건.
  **검증:** `Grep "judgmentLineOffset" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` → `-0.1` 포함 라인 1건.
- [ ] `[manual-hard]` 트롬본 세션 시작 시 판정선이 시야 중앙에 위치하고 노트가 왼쪽에서 오른쪽으로 흐른다.

## Notes

- 경험 시험 이력: 0(원본, 우측), 0.75(-dir, 좌측 과다), 0.15(-dir), 0.125(-dir), -0.1(-dir) → pass.
- Inspector에서 직접 미세 조정 가능.

## Handoff

`judgmentLineOffset = -0.1` prefab 박제로 판정선 시야 중앙 정렬 완료. manual-hard pass(2026-05-27).
