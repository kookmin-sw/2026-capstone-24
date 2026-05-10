# 05 — Stick Velocity Driver 적용 메서드

**Sub-Spec:** [`02-stick-no-penetration.md`](../specs/02-stick-no-penetration.md)
**From Tech Spec:** [`02-stick-no-penetration.md`](../tech-specs/02-stick-no-penetration.md) §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-10

## Context

Tech Spec(`tech-specs/02-stick-no-penetration.md`)은 attach 중인 stick을 non-kinematic Rigidbody(`useGravity=false`)로 두고, 매 FixedUpdate에 GhostAnchor가 ghost wrist world pose를 따라가도록 driver를 적용한다고 박제한다. driver를 어떤 Rigidbody API로 적용할지에 따라 충돌 시 거동·jitter 패턴·VR follower의 자연스러움이 달라지므로 분기 결정이 필요하다.

## Options Considered

- **A. `Rigidbody.linearVelocity` / `angularVelocity` 직접 할당** — `rb.linearVelocity = dPos / Time.fixedDeltaTime`, `rb.angularVelocity = ToAngular(dRot, dt)`. 충돌 시 물리 엔진이 자연스럽게 velocity를 0에 수렴시켜 표면에서 멈춤. VR follower에서 일반적인 패턴. 빠른 입력 시 jitter·미세 진동 가능 (plan 단계 max-velocity clamp / smoothing으로 완화).
- **B. `Rigidbody.MovePosition` / `MoveRotation`** — `rb.MovePosition(target)`. 물리 sweep으로 한 frame 내 충돌 검사 보장. 본래 kinematic용 API라 non-kinematic에서는 내부적으로 큰 velocity를 부여한 뒤 다음 step에서 정리하는 구조 → 충돌 후 튕김·velocity 잔여 발생. velocity 기반 follower 사상과 어긋남.

## Decision

**A — `Rigidbody.linearVelocity` / `angularVelocity` 직접 할당.**

선택 사유:

1. Tech Spec Components·Data/Control Flow가 "velocity 기반 driver"로 사상을 잡고 있어, A가 그 사상의 자연스러운 구현이다. B는 sweep 보장이라는 다른 사상이라 Tech Spec과 결합이 헐겁다.
2. sub-spec What #2("표면 위에서 멈춰 보인다")·#4("어긋남 허용")의 메커니즘이 *velocity가 충돌로 0에 수렴 → ghostWrist와 GhostAnchor가 분리*로 자연스럽게 표현된다. B는 MovePosition이 매번 target에 도달하려 해 충돌 시 튕김 / 잔여 velocity 잔재가 어긋남을 spike 형태로 만들 수 있다.
3. jitter는 plan 단계의 max-velocity clamp 또는 deadzone으로 흡수 가능한 plan 디테일이라 본 ARD에서 닫지 않아도 된다.

## Spec What Coverage

본 sub-spec의 `## What` 4개 항목과 두 옵션의 매핑:

| What | A (velocity 직접) | B (MovePosition) |
|---|---|---|
| 스틱이 드럼킷 부품 안쪽으로 들어가지 않는다 | 만족 | 만족 |
| 표면에 닿은 상태에서도 입력 끊기지 않고 표면 위에서 멈춰 보인다 | 만족 | ⚠️ 부분 (튕김 잔여로 "멈춤" 시각 불안정) |
| 표면을 떠나면 다시 손 위치를 자연스럽게 따라간다 | 만족 | 만족 |
| 입력 위치와 시각 위치 간 어긋남 허용 | 만족 (velocity 0 수렴 → 자연 어긋남) | ⚠️ 부분 (튕김 잔여로 어긋남이 spike 형태) |

A가 모든 What을 깔끔히 만족, B는 What #2·#4에서 부분 만족.

## Consequences

- plan-drafter는 driver 구현부에 `Rigidbody.linearVelocity` / `angularVelocity` 직접 할당 형태를 사용한다. `MovePosition` / `MoveRotation`을 사용하지 않는다.
- plan은 빠른 입력 시 jitter·과속을 완화하기 위한 파라미터(예: max-velocity clamp, deadzone, 회전 smoothing)를 plan 디테일로 노출할 수 있다 — 본 ARD는 그 값을 정하지 않는다.
- attach 중 `transform.position` / `rotation` 직접 변경은 금지(Tech Spec Invariants 그대로). velocity·angularVelocity 외 다른 driver를 섞지 않는다.
- 본 결정은 stick의 non-kinematic Rigidbody·useGravity=false 가정 위에서 성립한다. 이 가정이 깨지면(예: 향후 stick에 중력·gravity 추가) 본 결정을 재평가한다.
