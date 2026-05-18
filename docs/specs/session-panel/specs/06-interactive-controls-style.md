# Interactive Controls Style

**Parent:** [`_index.md`](../_index.md)
**Tech Spec:** skipped

## Why

DifficultyButton, SongRow, AccompanimentToggle, VolumeSlider, InstrumentToggleButton이 단색 직사각형이어서 선택/비선택 상태 구분이 약하고 시각적으로 투박하다. 글래스모피즘 기반 패널(sub-spec 05) 위에서 이 요소들을 새 디자인 언어로 통일해야 실서비스 품질에 도달한다.

## What

- DifficultyButton, SongRow 행 배경, AccompanimentToggle, InstrumentToggleButton의 모서리를 둥근 직사각형으로 변경한다.
- 각 컨트롤의 상태별 배경 색상을 정의·적용한다:
  - 기본(Normal): 반투명 흰색
  - 선택됨(Selected/Active): 액센트색 (#4A9EFF) 기반
  - 비활성(Disabled): 낮은 불투명도 회색
- 버튼 선택 시 짧은 scale-up + fade 전환 애니메이션을 적용한다.
- VolumeSlider의 채워진 영역과 핸들에 액센트색 (#4A9EFF)을 적용한다.
- 위 모든 인터랙티브 요소가 동일한 모서리 반경·상태 색 팔레트를 공유한다.

## Behavior

- **Given** 버튼이 기본(Normal) 상태임
  **When** 사용자가 버튼을 봄
  **Then** 반투명 흰색 배경 + 둥근 모서리로 표시된다.

- **Given** 사용자가 버튼을 선택함
  **When** 선택 상태가 활성화됨
  **Then** 배경이 #4A9EFF 기반 액센트 색으로 전환되며, 짧은 scale-up 애니메이션이 재생된다.

- **Given** 버튼이 비활성(Disabled) 상태임
  **When** 사용자가 버튼을 봄
  **Then** 낮은 불투명도의 흐릿한 회색으로 표시되어 선택 불가임을 시각적으로 알 수 있다.

- **Given** VolumeSlider가 표시됨
  **When** 사용자가 슬라이더를 조작함
  **Then** 채워진 영역과 핸들이 #4A9EFF 색으로 표시된다.

## Out of Scope

- 패널 컨테이너 배경 스타일 — sub-spec 05에서 다룬다.
- 컨트롤의 크기·위치·레이아웃 변경.
- Hover(포인터 오버) 상태 (VR에서 버튼은 물리적 누름 또는 핀치로 발화).
- Particle/VFX 피드백 효과.
- 기존 인터랙션 로직(클릭 이벤트, 슬라이더 값 바인딩) 변경.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
