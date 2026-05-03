# Drum Stick

## Why

드럼킷 anchor에 텔레포트한 사용자의 의도는 "드럼 연주를 시작한다"이다. 그 사이에 양손에 스틱을 직접 집는 단계가 끼면 인지·시간 비용이 발생하고, anchor 정렬이 끝난 직후 빈 손인 상태는 사용자의 의도와 어긋난다.

또한 잡은 스틱이 드럼 표면을 그대로 통과하면 "어디서부터 표면이고 어디부터 친 것인가"의 시각·물리 단서가 사라져 타격 정확도와 박자감이 떨어진다.

이 피처는 드럼킷 anchor에서의 "스틱 자동 부착 → 잡힌 채로 연주 → anchor 이탈 시 자동 해제" 라이프사이클과 "스틱이 드럼킷 부품을 통과하지 않음"이라는 두 행동을 묶어, 사용자가 anchor에 도착하는 순간 즉시 연주 가능한 상태가 되도록 한다.

## What

- 드럼킷 anchor에 텔레포트로 도착하는 순간 양손에 자동으로 스틱이 들린 상태가 된다.
- 잡혀 있는 동안 사용자의 입력으로 스틱을 손에서 떼어낼 수 없다.
- 잡혀 있는 동안 손은 미리 정의된 스틱-잡기 포즈를 유지한다.
- 사용자가 드럼킷 anchor 외부로 텔레포트하는 순간 양손에서 스틱이 동시에 사라진다.
- 스틱은 드럼킷 부품(드럼 표면, 심벌, 림 등)을 통과하지 않고, 닿으면 표면에서 멈춰 보인다.

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| Anchor Auto Attach/Detach | Active | [specs/01-anchor-auto-attach-detach.md](specs/01-anchor-auto-attach-detach.md) |
| Stick No-Penetration | Draft | [specs/02-stick-no-penetration.md](specs/02-stick-no-penetration.md) |

> 상태 값: `Draft` / `Active` / `Done` / `Abandoned`

## Open Questions

_현재 열린 질문 없음._

## Status

`Draft`
