# Note Panel Y Position Fix

**Parent:** [`_index.md`](../_index.md)

## Why

피아노와 드럼 모두 리듬게임 노트 UI 패널이 Y축으로 너무 높게 생성돼, 사용자가
노트가 어떤 위치에 낙하하는지 시각적으로 파악하기 어렵다. 피아노는 prefab에
박힌 Transform Y가 부적절하고, 드럼은 각 히트존마다 패널 위치를 런타임에
동적으로 계산하므로 한 번 조정한 값이 재생성 시 초기화된다.

## What

- 피아노 노트 UI 패널은 악기 prefab 내부의 패널 Transform Y를 직접 편집해
  원하는 높이에 고정한다.
- 드럼 노트 UI 패널은 세계 좌표 Y(m)를 직접 지정하는 Inspector 필드로
  생성 위치를 결정한다.
- 동일한 악기 인스턴스가 재생성될 때도 설정된 Y값이 유지된다.

## Behavior

- **Given** 피아노로 리듬게임 세션을 시작할 때
  **When** 노트 UI 패널이 화면에 나타날 때
  **Then** 패널의 Y 위치가 prefab에 저장된 고정값과 동일하다

- **Given** 드럼의 DrumNoteDisplayAdapter에 worldYOverride 값이 지정된 상태에서
  **When** 리듬게임 세션이 시작되어 각 파츠 위에 패널이 생성될 때
  **Then** 모든 드럼 파츠 패널의 world Y가 worldYOverride 값과 같다

- **Given** 동일한 DrumKit prefab에서 새 인스턴스를 생성하고 세션을 재시작할 때
  **When** 패널이 다시 생성될 때
  **Then** worldYOverride 값이 적용되어 이전과 동일한 높이에 패널이 나타난다

## Out of Scope

- X, Z 위치 조정
- 패널 크기·스케일 변경
- 드럼 이외 악기의 런타임 패널 생성 로직 변경
- 사용자가 UI에서 Y값을 실시간 변경하는 기능

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

## Open Questions

_현재 열린 질문 없음._
