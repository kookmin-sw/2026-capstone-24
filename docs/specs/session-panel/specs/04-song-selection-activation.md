# Song Selection Activation

**Parent:** [`_index.md`](../_index.md)

## Why

세션 패널 시작 메뉴 섹션의 구현체가 악기를 잡은 상태에서도 곡 목록을 전부 비활성(회색) 상태로 표시하는 버그가 있다. 사용자가 리듬게임을 시작할 수 없으므로 수정이 필요하다.

## What

악기를 잡은 상태에서 패널이 열릴 때, 해당 악기 트랙이 존재하는 곡은 클릭 가능한 활성 상태로 표시된다.

## Behavior

- **Given** 사용자가 한 악기를 잡고 세션 패널이 열림
  **When** 시작 메뉴 섹션의 곡 목록을 봄
  **Then** 해당 악기 트랙이 차트에 존재하는 곡은 활성(클릭 가능), 그 외는 비활성으로 표시된다.

- **Given** 악기를 잡지 않은 상태에서 패널이 핀치 호출로 열림
  **When** 시작 메뉴 섹션을 봄
  **Then** 시작 메뉴 섹션은 노출되지 않는다.

## Out of Scope

- 곡 데이터의 출처·스캔 방식 — `rhythm-game/specs/05-song-catalog.md`
- 패널 노출/위치 트리거 — `01-anchoring.md`
- NoteDisplayPanel 위치 조정 — 사용자가 프리팹 직접 수정으로 관리
- 볼륨 섹션 — `03-volume-section.md`

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-17 | Song Selection Activation — 진단·수정 | Done | [`2026-05-17-linksky0311-song-selection-activation-fix.md`](../plans/2026-05-17-linksky0311-song-selection-activation-fix.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
