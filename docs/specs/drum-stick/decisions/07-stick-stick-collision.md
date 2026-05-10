# 07 — Stick-Stick 충돌 회피 메커니즘

**Sub-Spec:** [`02-stick-no-penetration.md`](../specs/02-stick-no-penetration.md)
**Status:** `Accepted`
**Date:** 2026-05-10

## Context

sub-spec `02-stick-no-penetration.md` `## Behavior` 4번은 "양손 스틱이 같은 영역에서 만나도 스틱끼리는 서로 통과한다"를 박제한다. 그러나 Tech Spec(`tech-specs/02-stick-no-penetration.md`)·ARD 05·06 어디에도 이 거동을 만족시키는 메커니즘이 박제돼 있지 않다.

결정적 사실:

- `drum_stick_L_Sanyo` / `drum_stick_R_Sanyo` 두 prefab 모두 Layer 0 (Default) + 단일 BoxCollider + Rigidbody.
- ARD 05에 의해 attach 중 두 stick 모두 `isKinematic=false` non-kinematic Rigidbody.
- Unity 기본 Physics matrix에서 같은 layer + 양쪽 non-kinematic + 광범위 통과 방지 정책 → 두 stick collider는 서로 충돌해 막힌다.

Behavior 4번을 만족시키려면 두 stick 사이의 collider 충돌만 정확히 비활성화해야 한다.

## Options Considered

- **A. Bind 시점 `Physics.IgnoreCollision(stickL, stickR)` 런타임 호출** — `DrumKitStickAnchor.AttachSticks` 단일 진입점에서 양쪽 stick 인스턴스가 동시 알려진 시점 1회 호출. prefab/Physics matrix 무변경.
- **B. Stick 전용 layer 신규 + Physics matrix Stick↔Stick OFF** — 신규 layer 1개 + matrix 1셀 토글 + `_Sanyo` stick prefab 3개 layer 변경. Tech Spec §Boundaries "layer / Physics matrix 무변경" 박제와 직접 충돌.
- **C. 한쪽 stick collider trigger 모드 토글** — 코드 1줄. 다만 trigger collider는 drum piece solid collider와도 통과해 sub-spec What 1·2항(통과 방지)을 직접 위반. 채택 불가.

## Decision

**A — `Physics.IgnoreCollision(L.collider, R.collider, true)` 런타임 호출.**

선택 사유:

1. **Tech Spec §Boundaries 박제와 일치.** Tech Spec은 "drum piece prefab들의 collider 구조 / layer 무변경" + "드럼킷 외 환경 객체의 layer / Physics matrix 무변경"을 박제했다. A는 prefab/matrix 무변경으로 이 박제와 정면 일치한다. B는 박제 위반으로 Tech Spec 재합의가 필요하다.
2. **호출 시점이 결정적.** ARD 03(Instantiate/Destroy) + Tech Spec Data/Control Flow가 `DrumKitStickAnchor.AttachSticks`를 stick spawn 단일 진입점으로 박제했다. 양쪽 stick 인스턴스가 동시 알려진 시점 1회 호출이라 누락 위험이 낮다.
3. **Spec What 모두 만족.** A는 두 stick collider 쌍만 정확히 비활성화하므로 drum piece solid collider와의 충돌(통과 방지)에는 무영향이다.

## Spec What & Behavior Coverage

본 sub-spec의 `## What` 4개 항목 + `## Behavior` 4번과 세 옵션의 매핑:

| 항목 | A (IgnoreCollision) | B (layer 신설) | C (trigger 토글) |
|---|---|---|---|
| What1: drumkit 부품 안쪽으로 안 들어감 | 만족 | 만족 | ⚠️ 위반 |
| What2: 표면에서 멈춰 보임 | 만족 | 만족 | ⚠️ 위반 |
| What3: 표면 떠나면 따라감 | 만족 | 만족 | 만족 |
| What4: 어긋남 허용 | 만족 | 만족 | 만족 |
| Behavior4: 두 스틱 서로 통과 | 만족 | 만족 | 만족 |

A·B 모두 fully 만족하나 Tech Spec §Boundaries 박제 정합성으로 A 선택.

## Consequences

- plan-drafter는 stick 양쪽이 동시 알려진 시점(Tech Spec Data/Control Flow의 `DrumKitStickAnchor.AttachSticks` 흐름) 한 곳에서 `Physics.IgnoreCollision(stickL.collider, stickR.collider, true)`을 1회 호출한다. Detach 시 두 stick이 모두 Destroy되므로 복원 호출은 불요(Unity의 IgnoreCollision pair는 collider 객체 수명에 묶인다).
- stick prefab의 Layer / Physics matrix는 변경하지 않는다 (Tech Spec §Boundaries 유지).
- 호출 위치 결정 단위는 plan 디테일 — 본 ARD는 단일 진입점이라는 사실만 박는다.
- 본 결정은 attach 중 stick이 정확히 양쪽 1개씩(L, R) 존재한다는 ARD 03 가정에 의존한다. stick 인스턴스 수가 변경되는 시나리오(예: 한쪽만 spawn) 발생 시 본 결정을 재평가한다.
- Stick과 PhysicsHand 간 collider 관계는 본 ARD 범위 밖이다 (Tech Spec §Boundaries에서 hands/02-instrument-no-penetration sub-spec과 분리).
