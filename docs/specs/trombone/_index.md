# Trombone

## Why

드럼·피아노는 anchor 진입 + 손가락/스틱 입력만으로 연주가 성립하지만 트럼본은 입(마우스피스)과 양손(슬라이드 운전)이 동시에 정렬되어야 비로소 연주 자세가 잡힌다. anchor 진입 직후 사용자가 본체를 직접 들어 입에 가져다 대거나 양손을 슬라이드에 갖다 붙이는 단계는 매번 발생하는 인지·시간 비용이고, "anchor에 도착했다 = 연주 시작" 의도와 어긋난다.

또한 슬라이드 관악기는 손가락 키 입력으로 음을 끊어 내는 타·건반 악기와 달리 슬라이드 위치가 연속적으로 음높이를 결정한다. 슬라이드 위치 ↔ 음높이가 시각·청각에서 동시에 자연스럽게 변화해야 트럼본 특유의 글리산도 표현이 성립한다.

이 피처는 "anchor 진입 → 입과 양손 자동 정렬 → 오른손 Grip 으로 슬라이드 운전 → 왼손 Grip 으로 호흡(발음) → anchor 이탈 시 원상복귀" 의 라이프사이클을 한 묶음으로 박제해, 사용자가 anchor 에 도착하는 순간 그대로 연주 가능한 트럼본 상태로 진입하도록 한다.

## What

- 사용자가 트럼본 anchor 로 텔레포트하면 트럼본 본체가 사용자의 입 위치로 자동 정렬되고 양손이 트럼본을 잡은 포즈로 즉시 전환된다.
- 사용자가 anchor 외부로 텔레포트하면 트럼본 본체는 원래 scene 배치 위치로 복귀하고 양손 포즈도 해제된다.
- 오른손 Grip 을 잡고 있는 동안만 사용자의 오른손 좌우 이동이 슬라이드의 좌우 위치에 반영된다. Grip 을 떼면 슬라이드는 마지막 위치를 유지한다.
- 왼손 Grip 을 누르고 있는 동안만 트럼본 음이 들리고, 그 동안 슬라이드 위치가 연속적으로 음높이를 결정한다 (글리산도 가능).
- 트럼본은 Piano 와 동일한 InstrumentBase 계약을 따라 MIDI 이벤트 표면을 노출한다 (RhythmGame 연동 여지 확보, 본 피처에서 실제 연동은 다루지 않음).

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| Anchor & Mouth Attach/Detach | Done | [specs/01-anchor-mouth-attach-detach.md](specs/01-anchor-mouth-attach-detach.md) |
| Slide Tracking by Right Grip | Draft | [specs/02-slide-tracking.md](specs/02-slide-tracking.md) |
| Blow & Continuous Pitch Sound | Draft | [specs/03-blow-and-pitch-sound.md](specs/03-blow-and-pitch-sound.md) |

> 상태 값: `Draft` / `Active` / `Done` / `Abandoned`

## Open Questions

_현재 열린 질문 없음._

## Status

`Draft`
