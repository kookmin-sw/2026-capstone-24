# Trombone

## Why

VR 음악 스튜디오에 트럼본 연주 경험을 추가한다. 드럼·피아노에 이어 관악기 카테고리를 채우며,
슬라이드 조작이라는 독특한 인터랙션으로 연주자가 실제 트럼본과 유사한 물리적 감각을 느끼게 한다.

## What

- 플레이어가 트럼본 앵커에 텔레포트하면 트럼본이 플레이어 입 위치로 자동 이동하고
  이후 플레이어 Mouth Transform을 실시간으로 추적한다.
- 왼손은 트럼본 바디 고정 위치에 GripPose 모델로 교체되고,
  오른손은 슬라이드에 GripPose 모델로 자동 연결된다.
- 오른손의 트럼본 로컬 X 이동이 슬라이드를 제어하며, 7개 구간이 각각 다른 MIDI 노트를 발음한다.
- 앵커 이탈 시 연주 상태가 해제되고 양손·트럼본이 원상 복귀된다.

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| 01 앵커·그립 | Draft | [specs/01-anchor-and-grip.md](specs/01-anchor-and-grip.md) |
| 02 슬라이드·MIDI | Draft | [specs/02-slide-midi.md](specs/02-slide-midi.md) |

> 상태 값: `Draft` / `Active` / `Done` / `Abandoned`

## Open Questions

_현재 열린 질문 없음._

## Status

`Draft`
