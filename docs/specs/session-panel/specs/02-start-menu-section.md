# Start Menu Section

**Parent:** [`_index.md`](../_index.md)

## Why

리듬게임 진입 흐름은 [`rhythm-game/specs/session-flow.md`](../../rhythm-game/specs/session-flow.md)에서 상태 전이로만 정의되어 있고, "메뉴 UI의 시각·상호작용 디자인"은 그 sub-spec에서 명시적으로 Out of Scope로 빠져 있다. 사용자가 곡·난이도·자동 반주로 발화될 트랙을 실제로 선택해 세션을 시작하려면, 그 선택을 받아낼 책임을 가진 별도의 sub-spec이 필요하다.

## What

세션 패널의 한 섹션. 사용자가 악기를 잡은 상태에서만 노출되며, 곡 목록·난이도 목록·차트 반주 트랙 on·off·시작 액션을 사용자에게 제공한다. 시작이 확정되면 결정된 곡/난이도/반주 트랙 구성을 rhythm-game `session-flow`로 전달해 세션을 시작시킨다. 사용자가 잡은 악기에 해당하는 트랙은 자동으로 채점 대상이 되며, 반주 트랙 후보에서 제외된다.

## Behavior

- **Given** 사용자가 한 악기를 잡고 패널이 노출됨
  **When** 시작 메뉴 섹션을 봄
  **Then** 전체 곡이 곡 목록으로 노출되며, 잡은 악기 트랙이 없는 곡은 선택 불가능한 비활성 상태로 표시된다.

- **Given** 곡 목록에서 한 곡이 선택됨
  **When** 난이도 컨트롤을 봄
  **Then** 그 곡의 차트에 정의된 난이도 목록이 노출된다.

- **Given** 곡과 난이도가 선택됨
  **When** 반주 트랙 컨트롤을 봄
  **Then** 잡은 악기의 트랙을 제외한 나머지 트랙들이 on/off 토글로 노출되며, 기본값은 모두 on이다.

- **Given** 곡·난이도·반주 트랙 구성이 결정됨
  **When** 사용자가 패널 내 '시작' 버튼을 손으로 눌러/핀치하여 시작 액션을 발화함
  **Then** 잡은 악기 트랙을 채점 대상으로, on 상태인 반주 트랙을 자동 반주로 하는 세션이 시작된다.

- **Given** 곡 또는 난이도가 선택된 상태
  **When** 사용자가 잡고 있던 악기를 놓거나 다른 악기로 바꿈
  **Then** 곡·난이도·반주 트랙 선택이 초기화된다.

- **Given** 패널이 핀치 호출(악기 미잡음)로 떠 있음
  **Then** 시작 메뉴 섹션은 노출되지 않는다.

## Out of Scope

- 패널의 표시 트리거·위치·컨텍스트별 섹션 가시성 — [`01-anchoring.md`](01-anchoring.md).
- 볼륨 섹션 — [`03-volume-section.md`](03-volume-section.md).
- 세션 진행 중의 노트 발화·채점·자동 반주 동작 자체 — `rhythm-game` 피처의 [`session-flow`](../../rhythm-game/specs/session-flow.md), [`accompaniment`](../../rhythm-game/specs/accompaniment.md), `judgment`(archived).
- 결과 화면 / 점수 표시.
- 곡 데이터의 출처·저장 위치(차트 카탈로그) 자체의 설계.
- 곡 목록의 정렬·필터·검색 컨트롤 — 별도 후속 sub-spec(예: `song-list-controls`)에서 정의.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
