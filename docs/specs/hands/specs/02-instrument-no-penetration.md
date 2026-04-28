# Instrument No-Penetration

**Parent:** [`_index.md`](../_index.md)

## Why

현재 Physics 핸드가 Kinematic으로 동작하기 때문에 피아노 건반·드럼 패드 같은 악기 표면을 손이 그대로 통과한다. 이 통과는 두 가지 비용을 만든다.

1. "어디까지가 표면이고 어디부터 누른 것인가"의 시각·물리 단서를 없애 연주의 정확도를 떨어뜨린다.
2. VR 공간에서 가상 객체의 실재감을 깨뜨린다.

단순히 충돌만 켠다고 해결되지 않는다. 충돌이 켜져 손이 표면에 막히는 동안에도 입력은 계속 들어오기 때문에, 잘못 처리하면 손이 입력 위치에서 크게 벗어나거나 떨림이 발생하거나 연주 속도를 따라가지 못해 박자가 늦는다. locomotion(Move/Turn) 같은 텔레포트성 이동이 발생하는 순간에도 손이 환경을 부적절하게 밀거나 튕길 수 있다.

## What

- 피아노·드럼 같은 악기 표면을 손이 통과하지 않는다.
- 비통과 동작을 유지하면서도 연주에 지장을 줄 만한 레이턴시·떨림이 발생하지 않는다.
- 손이 표면에 막혀 있는 동안에도, 사용자가 입력으로 의도한 위치는 다른 시각 단서(Ghost 등)로 계속 관찰 가능하다.
- locomotion 시작·종료 이벤트를 신호 삼아 Physics 핸드 동작을 일시 중단·복원하고, 그 동안 손이 가상 환경을 의도치 않게 밀거나 튕기지 않는다.
- 양손이 동시에 같은 악기 표면 또는 서로 인접한 표면에 닿아도 비통과 동작이 두 손 모두에 동등하게 보장된다.

## Behavior

- **Given** 사용자의 손이 피아노 건반 또는 드럼 표면에 접근한다.
  **When** 입력 위치가 표면 안쪽으로 더 들어가려 한다.
  **Then** 손은 표면에서 멈춘다. 입력은 계속 진행되어도 손이 객체를 통과하지 않는다.

- **Given** 손이 표면을 누르고 있다.
  **When** 사용자가 입력 손을 빠르게 좌우/상하로 움직여 다른 건반·패드를 친다.
  **Then** 연주에 지장을 줄 만한 레이턴시·떨림 없이 다음 타격이 인식된다.

- **Given** 사용자가 Move/Turn 같은 locomotion으로 위치를 크게 바꾼다.
  **When** 그 짧은 순간 동안 손은 새 위치로 점프해야 한다.
  **Then** 손이 가상 환경의 객체를 부적절하게 밀거나 튕기지 않는다.

- **Given** 손이 표면에 막혀 있어 입력 위치와 실제 손 위치가 어긋나 있다.
  **When** 사용자가 자기 손이 어디로 가려 했는지 확인하고 싶다.
  **Then** 별도의 시각 단서(Ghost 등)로 의도한 위치를 관찰할 수 있다.

- **Given** 두 손이 동시에 같은 악기 표면 또는 인접한 표면에 접근한다.
  **When** 두 입력이 모두 표면 안쪽으로 더 들어가려 한다.
  **Then** 양손 모두 표면에서 멈추며 서로의 동작을 방해하지 않는다.

본 sub-spec의 합격 여부는 정성적으로 판정한다 — 정상 연주 속도에서 사용자가 떨림이나 박자 어긋남을 의식적으로 인지하지 않으면 통과로 본다.

## Out of Scope

- 3-hand 구조 자체와 그 검증은 다루지 않는다. → [`01-three-hand-architecture-validation.md`](01-three-hand-architecture-validation.md)에서 다룸.
- 악기별 입력 인식 로직(어떤 깊이/속도에서 노트가 트리거되는가)은 다루지 않는다.
- 컨트롤러 햅틱 피드백 정책은 다루지 않는다.
- 손이 객체를 **잡는** 상호작용(grab/grip)은 다루지 않는다. grab과 비통과가 같은 객체에서 만나는 시나리오의 우선순위 처리도 본 sub-spec의 책임이 아니다.
- 악기 입력 표면이 아닌 환경 객체(테이블·벽·바닥 등)에 대한 비통과는 본 sub-spec의 책임이 아니다.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-28 | Physics 핸드 비통과 동작 전환 (non-kinematic 콘택트 추종) | Ready | [2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md](../plans/2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md) |
| 2026-04-28 | HandTracking 입력 떨림 흡수 (OneEuro 사전 스무딩) | Ready | [2026-04-28-sanyoentertain-handtracking-oneeuro-smoothing.md](../plans/2026-04-28-sanyoentertain-handtracking-oneeuro-smoothing.md) |
| 2026-04-28 | Locomotion 일시 중단·복원 + 양손 안전성 검증 | Ready | [2026-04-28-sanyoentertain-locomotion-pause-and-bilateral-safety.md](../plans/2026-04-28-sanyoentertain-locomotion-pause-and-bilateral-safety.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
