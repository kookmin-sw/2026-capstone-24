# Note Panel Horizontal Offset Mode

**Sub-Spec:** [`16-note-panel-judgment-centering.md`](../specs/16-note-panel-judgment-centering.md)
**Status:** `Accepted`
**Date:** 2026-05-27
**From Tech Spec:** `tech-specs/16-note-panel-judgment-centering.md §Open Tech Decisions`

## Context

`ComputePanelWorldPos`에 가로 오프셋을 주어 판정선을 전방 중앙에 정렬할 때,
오프셋을 코드에 하드코딩할지 Inspector 필드로 노출할지 결정이 필요하다.

## Options Considered

- **Inspector 파라미터 노출** — `judgmentLineOffset` float 필드 추가, 기본값 0. 씬 조정 없이 Inspector에서 미세 튜닝 가능.
- **코드 내 하드코딩** — `panelScrollLengthMeters / 2`를 직접 주입. 단순하지만 나중에 조정 시 코드 수정 필요.

## Decision

**Inspector 파라미터 노출** — 기본값 0으로 기존 동작을 유지하면서 씬 배포 후에도 Inspector에서 조정 가능.

## Consequences

- planner는 `TromboneNoteDisplayAdapter`에 `[SerializeField] float judgmentLineOffset = 0f` 추가.
- planner는 `ComputePanelWorldPos` 반환값에 `+ yawRight * judgmentLineOffset` 적용.
- planner는 `Trombone.prefab`의 `TromboneNoteDisplay` 컴포넌트의 `judgmentLineOffset`을 `panelScrollLengthMeters / 2 = 0.75f`로 설정해야 한다.
- `panelScrollLengthMeters` 변경 시 이 오프셋도 재검토 필요.
