# Drum Session Panel Position Fix

**Parent:** [`_index.md`](../_index.md)

## Why

드럼 연주 위치에서 리듬게임 선택 UI(SessionPanel)를 열면 패널이 사용자가
손으로 조작하기에 너무 먼 위치에 표시된다. SessionPanel은 활성 악기의
_panelAnchor 위치를 기준으로 배치되므로, DrumKit prefab의 _panelAnchor Y·Z를
더 적합한 위치로 조정하면 드럼에서 패널이 편하게 조작 가능한 거리에 표시된다.

## What

- DrumKit prefab의 _panelAnchor Transform Y·Z 위치를 조정해 드럼 연주 자리에서
  SessionPanel이 사용자가 편하게 조작할 수 있는 거리와 높이에 표시된다.
- 조정된 위치값이 prefab에 고정되므로 DrumKit 인스턴스가 재생성될 때도
  동일한 위치에 패널이 뜬다.

## Behavior

- **Given** DrumKit가 씬에 배치된 상태에서
  **When** 사용자가 리듬게임 선택 UI를 열 때
  **Then** SessionPanel이 드럼 연주 자세에서 손으로 조작 가능한 거리에 표시된다

- **Given** 수정된 _panelAnchor가 적용된 DrumKit prefab에서
  **When** 동일한 prefab의 새 인스턴스를 생성한 후 SessionPanel을 열 때
  **Then** 새 인스턴스에서도 동일한 위치에 SessionPanel이 뜬다

## Out of Scope

- 피아노 및 다른 악기의 SessionPanel 위치 변경
- SessionPanel 회전(orientation) 동작 수정
- _panelAnchor X 위치 조정

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-20 | _panelAnchor Y·Z 거리 검증 및 미세 조정 | Done | [`2026-05-20-linksky0311-drum-session-panel-position-fix.md`](../plans/2026-05-20-linksky0311-drum-session-panel-position-fix.md) |

## Open Questions

_현재 열린 질문 없음._
