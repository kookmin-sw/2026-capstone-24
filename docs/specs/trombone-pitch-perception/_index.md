# Trombone Pitch Perception

## Why

트롬본은 z회전(상하 각도)으로 5개 파셜(배음)이 결정되는데, 사용자는 본인이 지금 5개
파셜 중 어느 영역에 있는지, 어디로 더 움직여야 다음 파셜로 가는지를 시각적으로 인지하기
어렵다. 동시에 리듬게임 노트 UI 5줄의 시야상 수직 간격이 실제 파셜 anchor 각도 폭과
매칭되지 않아, 어떤 줄을 봐야 그 파셜이 발화되는지 직관적으로 알 수 없다. 양쪽 모두
"지금 어느 파셜에 있는지 · 어디를 봐야 하는지"라는 사용자 인지를 차단해 트롬본 학습·연주
난이도를 크게 높인다.

## What

트롬본을 잡은 동안 본체 mesh의 표시 회전이 5단계 anchor 각도로 hard step 스냅되고,
리듬게임 노트 UI 5개 파셜 패널이 사용자 시점 pitch -30°/-15°/0°/+15°/+30°에 1:1
매핑되어 수직 적층 배치된다.

- 트롬본 본체(Rig/Body) mesh가 5개 anchor 각도(-30/-15/0/+15/+30°) 중 현재
  PartialIndex에 대응하는 각도로 표시된다 (입력 각도는 그대로, 표시만 보정).
- 리듬게임 5개 파셜 패널이 사용자 시점 pitch -30/-15/0/+15/+30°에 1:1 적층된다
  (시야상 패널 i의 pitch = 파셜 i의 anchor 각도와 동일).
- 두 변화 모두 트롬본 픽업 동안 항상 적용된다 (자유 연주에서도 시각 스냅 ON).
- 5개 anchor 각도 값(현재 `anglePerPartial=15°`, `centerPartialIndex=2`)은 그대로
  유지하며, 본 피처는 표시·layout 보정만 책임진다.

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| Trombone Body Visual Snap | Done | [`specs/01-view-snap.md`](specs/01-view-snap.md) |
| Note Vertical Layout (Pitch-Aligned 5-Panel) | Done | [`specs/02-note-vertical-layout.md`](specs/02-note-vertical-layout.md) |

> 상태 값: `Draft` / `Active` / `Done` / `Abandoned`

## Open Questions

_현재 열린 질문 없음._

## Status

`Done`
