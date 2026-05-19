# Panel Theme

**Parent:** [`_index.md`](../_index.md)
**Tech Spec:** skipped

## Why

SessionPanel 컨테이너 배경이 불투명 단색이어서 VR 공간 환경과 어울리지 않고 투박하게 보인다. 글래스모피즘 스타일(반투명 다크 배경 + 밝은 테두리)과 공통 색 팔레트를 확립해야 이후 하위 인터랙티브 요소(sub-spec 06)의 일관된 리스타일이 가능하다.

## What

- SessionPanel 컨테이너 배경을 반투명 다크 패널(어두운 계열 + alpha)로 교체한다.
- 패널 외곽 테두리를 얇고 밝은 반투명 선으로 적용한다.
- 공통 색 팔레트를 정의한다: 반투명 배경색, 액센트색 (#4A9EFF), 기본 텍스트 흰색, 보조 텍스트 반투명 흰색.
- 패널 모서리를 둥근 직사각형 형태로 통일한다.

## Behavior

- **Given** SessionPanel이 VR 공간에 표시됨
  **When** 사용자가 패널을 바라봄
  **Then** 패널 배경이 반투명해 뒤 환경이 어렴풋이 비치고, 얇고 밝은 테두리가 패널 경계를 구분한다.

- **Given** 패널이 표시됨
  **When** 사용자가 텍스트를 읽음
  **Then** 흰색·보조 반투명 텍스트가 어두운 반투명 배경 위에서 가독성 있게 보인다.

## Out of Scope

- 패널 내 버튼·슬라이더·토글 등 인터랙티브 요소 스타일 — sub-spec 06에서 다룬다.
- 패널의 위치·크기·앵커링 변경.
- 배경 Blur 효과 (반투명만 적용, blur 없음).
- 레이아웃(요소 배치·간격) 변경.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-18 | Panel Theme — Glassmorphism Background + Border | Done | [2026-05-18-linksky0311-panel-theme.md](../../_archive/session-panel/plans/2026-05-18-linksky0311-panel-theme.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
