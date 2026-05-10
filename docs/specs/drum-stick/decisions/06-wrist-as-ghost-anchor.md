# 06 — GhostAnchor 셋업: 기존 wrist transform 재사용

**Sub-Spec:** [`02-stick-no-penetration.md`](../specs/02-stick-no-penetration.md)
**From Tech Spec:** [`02-stick-no-penetration.md`](../tech-specs/02-stick-no-penetration.md) §Open Tech Decisions #2
**Status:** `Accepted`
**Date:** 2026-05-10

## Context

Tech Spec(`tech-specs/02-stick-no-penetration.md`)은 driver의 추종 기준점을 "GhostAnchor"라는 이름으로 박제하고, 그 stick-local pose를 어떻게 캐시할지를 분기 지점으로 남겼다. 검토 도중 다음이 드러났다:

- 현재 코드 [`AnchoredStickGhostFollower.Bind`](../../../../Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs)는 이미 stick prefab 자식 `GripPoseHand/L_Wrist`(또는 `R_Wrist`) transform을 source로 받아 `m_WristLocalToRoot = transform.worldToLocalMatrix × wrist.localToWorldMatrix`로 stick-local 변환을 캐시한다.
- 즉 ARD 02(stick-hand coupling)로 prefab에 박제된 wrist transform이 사실상 driver의 추종 기준점 역할을 이미 수행하고 있다.

따라서 분기는 단순한 "캐시 시점"이 아니라 *"GhostAnchor를 wrist와 별도로 둘 강한 이유가 있는가"* 로 환원된다.

## Options Considered

- **A. 별도 빈 GameObject 추가 (prefab 직렬화)** — stick prefab에 GhostAnchor 빈 GameObject를 새로 추가. wrist transform과 분리된 독립 anchor. 의미 분리(wrist=PlayHand source, GhostAnchor=ghost 추종 기준점). prefab 변경·코드 source 인자 변경 비용.
- **B. 기존 wrist transform 재사용 (prefab 직렬화)** — 별도 GameObject 없음. 현재 `Bind`의 wrist source 인자와 `m_WristLocalToRoot` 캐시 패턴을 그대로 유지. wrist는 ARD 02에 의해 prefab-fixed pose라 동등한 prefab 직렬화 anchor로 기능.
- **C. Bind 시점 ghost wrist sample 동적 캐시** — wrist도 GhostAnchor도 사용하지 않고, Bind 호출 시점에 ghost wrist의 world pose를 stick-local로 변환해 단발 캐시. prefab에 anchor 박제 없음. 디버깅 어려움(stick prefab만 보고는 anchor 위치를 알 수 없음).

## Decision

**B — 기존 wrist transform 재사용.**

선택 사유:

1. **현재 코드와 호환.** `AnchoredStickGhostFollower.Bind`가 이미 wrist transform을 source로 받아 `m_WristLocalToRoot`를 캐시하는 패턴이 박혀 있다. driver 메서드만 transform 직접 덮어쓰기 → velocity 할당으로 갱신하면 되며, anchor 셋업 부분은 그대로 두어 변경 표면이 최소화된다.
2. **prefab 변경 최소.** A는 모든 stick prefab(L/R, Sanyo/비-Sanyo 변형)에 빈 GameObject를 일괄 추가해야 하지만, B는 prefab을 건드리지 않는다.
3. **의미 분리 비용이 본 sub-spec 범위에 없음.** wrist transform이 변경될 시나리오는 ARD 02(stick-hand pose 고정)에 의해 본 sub-spec 범위에서 발생하지 않는다. 향후 wrist 변경 시나리오가 발생하면 그때 본 결정을 재평가한다.
4. **C 대비 디버깅 우위.** B는 prefab을 보면 anchor 위치를 즉시 확인할 수 있어 stale-state 사고가 일어나지 않는다. C는 anchor 위치가 코드 실행 중에만 존재해 진단이 어렵다.

## Spec What Coverage

본 sub-spec의 `## What` 4개 항목과 세 옵션의 매핑:

| What | A (별도 GhostAnchor) | B (wrist 재사용) | C (동적 sample) |
|---|---|---|---|
| 스틱이 드럼킷 부품 안쪽으로 들어가지 않는다 | 만족 | 만족 | 만족 |
| 표면 위에서 멈춰 보인다 | 만족 | 만족 | 만족 |
| 표면을 떠나면 다시 따라간다 | 만족 | 만족 | 만족 |
| 입력 위치와 시각 위치 어긋남 허용 | 만족 | 만족 | 만족 |

세 옵션 모두 What 4개를 fully 만족. 본 결정은 What 만족도가 아니라 *셋업 비용·코드 호환·디버깅 명료성*을 기준으로 B를 선택한다.

## Consequences

- plan-drafter는 driver의 추종 기준점을 별도 빈 GameObject로 박지 않는다. stick prefab의 기존 wrist transform(`GripPoseHand/L_Wrist` 또는 `R_Wrist`)을 source로 사용하는 현재 `AnchoredStickGhostFollower.Bind` 인자 형태를 유지한다.
- stick prefab들에 빈 GameObject를 새로 추가하지 않는다. prefab 변경은 Rigidbody 모드(`isKinematic`, `useGravity`)·기타 driver 갱신에 한한다.
- Tech Spec Components 섹션에서 "GhostAnchor (신규)" 표기는 본 결정에 따라 "wrist transform 재사용"으로 갱신된다.
- 본 결정은 ARD 02의 "stick-hand의 wrist pose가 prefab-fixed" 가정에 의존한다. ARD 02가 변경되어 wrist pose가 동적으로 바뀌게 되면 본 결정을 재평가한다.
- driver 매트릭스 캐시는 현재 `m_WristLocalToRoot` 1개를 그대로 사용한다. 별도 `m_GhostAnchorLocalToRoot` 같은 두 번째 캐시를 도입하지 않는다.
- 향후 stick prefab 변경(예: Rigidbody 직렬값 갱신, 신규 컴포넌트 부착)이 필요해지면 변경 대상은 `_Sanyo` 접미사 변형(`drum_stick_L_Sanyo`, `drum_stick_R_Sanyo`, `drum_stick_Sanyo`)으로 한정한다. 비-Sanyo 변형(`drum_stick_L`, `drum_stick_R`, `drum_stick`)은 본 sub-spec의 plan에서 건드리지 않는다.
