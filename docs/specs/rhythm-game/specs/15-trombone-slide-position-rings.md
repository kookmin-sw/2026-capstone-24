# Trombone Slide Position Rings

**Parent:** [`_index.md`](../_index.md)

## Why

트롬본 리듬게임에서 각 노트의 색은 슬라이드 포지션을 나타내지만,
실제 트롬본 위에서 어느 물리적 위치가 어느 색에 대응하는지
즉각적으로 파악하기 어렵다. 각 슬라이드 포지션에 고유 색 링을
배치해, 플레이어가 노트 색을 보는 순간 손을 어디로 움직여야
할지 직관적으로 인지할 수 있도록 한다. 오른손 Grip이 잡힌 동안
현재 포지션의 링이 더 밝게 빛나, 자신이 지금 어디에 있는지
즉시 시각적으로 확인할 수 있게 한다.

## What

트롬본을 잡는 순간 슬라이드의 7개 포지션에 색 링이 나타나고,
트롬본을 놓으면 사라진다. 오른손 Grip을 잡는 동안 현재 슬라이드
인덱스의 링이 HDR 밝기로 강조된다.

- 트롬본을 잡는 순간 슬라이드 포지션 1~7에 대응하는 링 7개가 나타난다.
- 각 링의 색은 기존 노트 색 매핑(spec 13)과 동일하다.
- 링은 슬라이드 움직임과 무관하게 고정 위치에 존재한다.
- 오른손 Grip이 잡힌 상태에서 현재 슬라이드 포지션에 해당하는 링이
  HDR 밝기로 강조된다; 나머지 링은 기본 밝기로 표시된다.
- Grip이 풀리면 모든 링이 기본 밝기로 돌아간다.
- 트롬본을 놓으면 링 7개가 모두 사라진다.
- 이 동작은 자유 연주와 리듬게임 세션 모두에서 적용된다.

## Behavior

- **Given** 플레이어가 트롬본을 잡을 때
  **When** 그립 이벤트가 발생할 때
  **Then** 슬라이드 포지션 1~7에 색 링 7개가 나타난다

- **Given** 링이 표시된 상태에서
  **When** 슬라이드를 이동시킬 때
  **Then** 링 위치는 변하지 않는다

- **Given** 트롬본을 잡은 상태에서
  **When** 오른손 Grip을 누를 때
  **Then** 현재 슬라이드 포지션의 링이 HDR 밝기로 강조되고 나머지 링은 기본 밝기로 표시된다

- **Given** 오른손 Grip이 잡힌 상태에서
  **When** 슬라이드 포지션이 변경될 때
  **Then** 새 포지션의 링이 강조되고 이전 포지션의 링은 기본 밝기로 돌아간다

- **Given** 오른손 Grip이 잡힌 상태에서
  **When** Grip을 놓을 때
  **Then** 모든 링이 기본 밝기로 돌아간다

- **Given** 플레이어가 트롬본을 놓을 때
  **When** 그립 해제 이벤트가 발생할 때
  **Then** 링 7개가 모두 사라진다

## Out of Scope

- 링 크기/두께 조정 UI
- 자유 연주 중 판정 피드백
- 링 표시/숨김 토글 설정
- 노트 접근 시 링 강조 효과 (슬라이드 포지션 기반 강조만)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-27 | Trombone 슬라이드 포지션 7링 — attach 게이팅 + HDR Grip 강조 | Done | [`2026-05-27-claude-trombone-slide-rings.md`](../plans/2026-05-27-claude-trombone-slide-rings.md) |
| 2026-05-27 | Trombone 슬라이드 링 — HDR 강조 대신 비활성 링 dim 처리 | Done | [`2026-05-27-claude-trombone-slide-rings-dim.md`](../plans/2026-05-27-claude-trombone-slide-rings-dim.md) |
| 2026-05-27 | Trombone 슬라이드 링 — grip OFF 상태 기본 dim | Done | [`2026-05-27-claude-trombone-slide-rings-dim2.md`](../plans/2026-05-27-claude-trombone-slide-rings-dim2.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
