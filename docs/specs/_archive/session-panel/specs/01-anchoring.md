# Anchoring

**Parent:** [`_index.md`](../_index.md)

## Why

패널의 호출 트리거와 위치가 정의되지 않으면 두 진입 경로(악기 잡기 / 왼손 핀치)가 어떻게 충돌·전이되는지가 모호해지고, 사용자는 패널이 언제 어디에 뜨는지 예측할 수 없게 된다. 또한 어떤 컨텍스트에서 어떤 섹션이 노출되는지도 패널의 표시 조건과 함께 결정되어야 한다.

## What

이 sub-spec은 세션 패널이 노출되는 트리거 조건과 월드 내 위치, 그리고 컨텍스트별 노출 섹션 목록을 정의한다. 각 섹션의 내부 컨텐츠와 동작은 다른 sub-spec이 담당하며, 본 sub-spec은 "언제·어디에 패널이 보이고, 어떤 섹션이 켜지는가"만을 책임진다.

## Behavior

- **Given** 사용자가 어떤 악기도 잡지 않은 상태
  **When** 사용자가 왼손 핀치 제스쳐로 패널 호출 입력을 발생시킴
  **Then** 패널이 노출되며 볼륨 섹션만 표시된다.

- **Given** 사용자가 한 악기를 잡고 있음
  **When** 잡기가 확정됨
  **Then** 그 악기에 월드 고정된 오프셋 위치에 패널이 노출되며, 시작 메뉴 섹션과 볼륨 섹션이 모두 표시된다.

- **Given** 핀치 호출로 패널이 떠 있는 상태(악기 미잡음)
  **When** 사용자가 한 악기를 잡음
  **Then** 패널은 그 악기의 고정 오프셋 위치로 이동하며, 시작 메뉴 섹션이 추가로 노출된다.

- **Given** 잡힌 상태에서 패널이 떠 있음
  **When** 사용자가 잡고 있던 악기를 놓음
  **Then** 패널이 닫힌다.

- **Given** 패널이 노출되어 있음
  **When** 사용자가 패널 호출 입력(왼손 핀치 또는 동등 매핑된 컨트롤러 입력)을 다시 발화함
  **Then** 패널이 닫힌다 (토글).

## Out of Scope

- 왼손 핀치 제스쳐 자체의 정의·검출 — [`hands/specs/03-left-pinch-gesture.md`](../../hands/specs/03-left-pinch-gesture.md).
- 시작 메뉴 섹션의 내부 컨텐츠 — [`02-start-menu-section.md`](02-start-menu-section.md).
- 볼륨 섹션의 내부 컨텐츠 — [`03-volume-section.md`](03-volume-section.md).
- 패널의 시각 디자인(텍스처, 폰트, 색상 등).
- "악기 잡기"의 판정 자체(어떤 손/조건으로 잡힌 것으로 간주하는가).

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-05 | Session Panel Anchoring | Done | [2026-05-05-linksky0311-session-panel-anchoring.md](../../_archive/session-panel/plans/2026-05-05-linksky0311-session-panel-anchoring.md) |
| 2026-05-06 | Session Panel Spawn Position Refinement | Done | [2026-05-06-linksky0311-session-panel-spawn-position-refinement.md](../../_archive/session-panel/plans/2026-05-06-linksky0311-session-panel-spawn-position-refinement.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
