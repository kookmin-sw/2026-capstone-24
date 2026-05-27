# Note Vertical Layout (Pitch-Aligned 5-Panel)

**Parent:** [`_index.md`](../_index.md)
**Tech Spec:** [`../tech-specs/02-note-vertical-layout.md`](../tech-specs/02-note-vertical-layout.md)

## Why

리듬게임 노트 UI 5개 파셜 패널이 트롬본 z회전 각도(상하)와 매칭되지 않게 배치되어
있어, 사용자는 어떤 패널을 봐야 어떤 파셜이 발화되는지 직관적으로 매칭할 수 없다. 패널
i의 시점 pitch가 파셜 i의 anchor 각도와 정확히 일치하도록 수직 적층하면 "이 패널을
보고 있다 = 이 파셜에 있다"가 시각적으로 보장된다.

## What

리듬게임 세션이 시작되면 5개 파셜 패널이 사용자 시점 pitch -30°/-15°/0°/+15°/+30°에
1:1 매핑되어 수직 적층 배치된다.

- 패널 i (파셜 i) 의 시점 pitch = (i - centerPartialIndex) × anglePerPartial.
- 5패널의 시점 origin은 트롬본 prefab의 신규 PanelAnchor child (eye 높이, trombone
  forward 대향으로 고정)다. Trombone이 z회전해도 PanelAnchor는 trombone forward에
  고정되어 패널 pitch 관계가 안정적으로 유지된다.
- 5패널은 동일 반경에 배치되고 각 패널은 PanelAnchor를 바라본다.
- 본 sub-spec은 `rhythm-game/13-trombone-note-display`의 yaw 반원형 layout을
  pitch 수직 layout으로 대체한다. 13-spec 본문 및 13-plan의 layout 가정은
  본 sub-spec의 후속 plan이 갱신한다.

## Behavior

- **Given** 사용자가 트롬본을 잡고 리듬게임 세션을 시작할 때
  **When** 세션 리드인이 시작될 때
  **Then** PanelAnchor 기준 pitch -30°/-15°/0°/+15°/+30°에 파셜 0~4 패널이 수직 적층되어 나타난다

- **Given** 5패널이 표시 중일 때
  **When** 사용자가 PartialIndex 2 (anchor 0°)에 머무르는 자세를 취할 때
  **Then** 사용자 시선 정면(0° pitch)에 파셜 2 패널이 정렬되어 보인다

- **Given** 차트가 파셜 3 노트를 스폰할 때
  **When** 노트가 떨어질 때
  **Then** 노트는 +15° pitch에 위치한 파셜 3 패널 위에서 판정선으로 이동한다

- **Given** 사용자가 PartialIndex 1→2로 자세를 바꿀 때
  **When** trombone이 z회전해도
  **Then** PanelAnchor가 trombone forward에 고정되어 5패널 pitch 관계는 변하지 않는다

- **Given** 사용자가 트롬본을 놓거나 세션이 끝날 때
  **When** Hide 사이클이 발생할 때
  **Then** 5개 패널이 일괄 소거된다

## Out of Scope

- 패널 내 슬라이드 X 오프셋 시각화 (rhythm-game/13의 후속 plan 또는 별도 sub-spec 책임)
- 다른 악기(피아노/드럼) 노트 디스플레이 layout 변경
- PartialController 동작 변경
- 판정 타이밍 로직 (rhythm-game의 judgment sub-spec)
- 5개 anchor 각도 값 변경 (`anglePerPartial=15°` 유지)
- SessionPanel(14-spec) 위치 변경 — PanelAnchor child를 SessionPanel과 공유할지는 plan 단계 결정

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-27 | Note Panel — PanelAnchor 신규 child + pitch 수직 적층 layout 전환 | Done | [`../plans/2026-05-27-claude-note-pitch-panel-anchor-stack.md`](../plans/2026-05-27-claude-note-pitch-panel-anchor-stack.md) |
| 2026-05-27 | Note Panel — ComputePanelWorldPos pitch 부호 반전 (사용자 시야 좌표계 정합) | Done | [`../plans/2026-05-27-claude-note-panel-pitch-sign-flip.md`](../plans/2026-05-27-claude-note-panel-pitch-sign-flip.md) |
| 2026-05-27 | Panel Anchor — Trombone root → Rig 자식으로 reparent (attach 시 사용자 mouth 추적) | Done | [`../plans/2026-05-27-claude-panel-anchor-rig-reparent.md`](../plans/2026-05-27-claude-panel-anchor-rig-reparent.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
