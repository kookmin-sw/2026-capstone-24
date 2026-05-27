# Note Panel Judgment Line Centering

**Parent:** [`_index.md`](../_index.md)

## Why

트롬본 노트 패널 5개가 VR 공간에서 플레이어 시야 기준 오른쪽으로 치우쳐 있어,
판정선이 플레이어의 전방 중앙이 아닌 오른쪽에 위치한다. 판정선이 카메라 전방
(플레이어 정면)에 오도록 패널을 수평 이동하여 노트를 더 자연스럽게 연주할 수 있게 한다.

## What

`TromboneNoteDisplayAdapter`의 판정선 위치가 `PanelAnchor`의 카메라 yaw-forward 방향에
정렬되도록 패널을 수평 오프셋한다. 오프셋 값은 Inspector에서 조정 가능하다.

- 각 파셜 패널의 판정선이 `PanelAnchor` 기준 카메라 yaw-forward 방향에 위치한다.
- 노트는 판정선 왼쪽(플레이어 기준)에서 스폰되어 판정선을 향해 이동한다.
- 오프셋 값은 `TromboneNoteDisplayAdapter` Inspector 필드에서 조정 가능하다.
- 기존 노트 스크롤 속도·판정 타이밍 로직은 변경되지 않는다.

## Behavior

- **Given** 트롬본 세션이 시작될 때
  **When** 5개 파셜 패널이 배치될 때
  **Then** 각 패널의 판정선이 `PanelAnchor` 전방(카메라 yaw-forward) 방향에 정렬되어 있다

- **Given** 패널이 배치된 상태에서
  **When** 플레이어가 트롬본을 정면으로 바라볼 때
  **Then** 판정선이 시야 중앙에 보이고 노트가 왼쪽에서 오른쪽으로 판정선을 향해 이동한다

## Out of Scope

- 패널 scrollLength 변경
- 판정 타이밍 윈도우 변경
- 피아노·드럼 패널 위치 변경

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
