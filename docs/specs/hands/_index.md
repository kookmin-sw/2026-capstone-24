# Hands

## Why

VR에서 손은 사용자의 가장 기본적인 입출력 채널이다. 컨트롤러나 핸드 트래킹의 raw 입력을 그대로 보여주면 가상 객체와 충돌해도 통과해버려 몰입이 깨지고, 특히 피아노/드럼 같은 악기 연주에서는 손이 표면을 그대로 통과해 "어디까지가 표면이고 어디부터 누른 것인가"의 시각·물리 단서가 사라진다. 반대로 충돌 위주의 단일 핸드만 두면 추적과 충돌이 갈등해 손이 입력 위치에서 멀어지거나 떨림이 발생해 연주 속도를 따라가지 못한다.

이 피처는 **입력 추적 / 물리 충돌 / 시각 표현**이라는 세 가지 책임을 하나의 손에 몰지 않고 분리해, 각각 독립적으로 튜닝하면서도 사용자에게는 일관된 손 하나로 보이게 만드는 구조를 제공한다.

## What

- VR 컨트롤러 또는 핸드 트래킹 입력을 받아 가상 손을 표시하고, 가상 객체와 상호작용한다.
- 손은 세 개의 역할로 분리된 표현을 가진다.
  - **Ghost**: 입력의 raw 목표 위치/포즈. 충돌하지 않고 항상 입력을 따라간다.
  - **Physics**: Ghost를 추적하지만 가상 환경과 충돌해 통과를 막는 역할을 맡는다.
  - **Play**: 사용자에게 실제로 보이는 시각 표현. 상황에 따라 Ghost 또는 Physics를 따라간다.
- 피아노·드럼 같은 악기 표면을 손이 통과하지 않는다.
- 비통과 동작을 유지하면서도 연주에 지장을 줄 만한 레이턴시·떨림을 피한다.

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| Three-Hand Architecture Validation | Draft | [specs/three-hand-architecture-validation.md](specs/three-hand-architecture-validation.md) |
| Instrument No-Penetration | Draft | [specs/instrument-no-penetration.md](specs/instrument-no-penetration.md) |

> 상태 값: `Draft` / `Active` / `Done` / `Abandoned`

## Open Questions

_현재 열린 질문 없음._

## Status

`Draft`
