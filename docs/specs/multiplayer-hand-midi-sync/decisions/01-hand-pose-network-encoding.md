# 손 pose 네트워크 인코딩 방식

**Sub-Spec:** [`01-remote-hand-visualization.md`](../specs/01-remote-hand-visualization.md)
**From Tech Spec:** [`tech-specs/01-remote-hand-visualization.md`](../tech-specs/01-remote-hand-visualization.md) §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-19

## Context

각 손 26개 본 × 양손 = 52개 quaternion 을 매 틱 송신해야 한다. Photon Fusion 2 는 [Networked] 필드에 대해 자동 보간·델타 압축을 제공하지만 본 단위로 쪼개면 wire payload 가 커진다. 한편 커스텀 압축 (smallest-three 등) 은 wire 비용을 낮추지만 직렬화 코드와 디버깅 부담이 따른다. 1차 출시는 "보이고 들리기" 가 핵심이고 타이밍 보정은 out of scope 이므로 단순성과 검증 가능성을 우선한다.

## Options Considered

- **Fusion [Networked] 본 단위 quaternion 직접** — 본 26개 × 2손의 quaternion 을 각각 [Networked] 필드로 선언. 구현 단순. Fusion 기본 보간·델타 압축에 의존.
- **압축 pose snapshot (smallest-three 등)** — 본 회전을 smallest-three 등으로 압축해서 한 번에 송신. 대역폭 작아짐. 추가 직렬화 비용 + 압축 마진의 visual artifact 디버깅 필요.

## Decision

**Fusion [Networked] 본 단위 quaternion 직접** — 1차 출시의 핵심은 "보인다" 의 성립이지 대역폭 최적화가 아니다. Fusion 기본 보간이 late-join snapshot·보간 보강을 무료로 처리하고, 향후 대역폭 측정 결과로 압축이 필요하면 본 결정을 재평가한다.

## Spec What Coverage

| Spec What 항목 | 만족 여부 |
|---|---|
| 같은 룸의 다른 플레이어의 양손이 자기 화면에 실시간 동기화된다. | 만족 |
| 손가락 본 전부 동기화. | 만족 |
| Late join 시 입장 즉시 손 보임. | 만족 — Fusion [Networked] join snapshot 자동 전달. |

## Consequences

- planner 는 NetworkedHandPose 의 [Networked] 필드를 본 단위 quaternion 으로 선언한다.
- plan 단계에서 룸 정원 가정 대역폭 측정 AC 를 1건 둔다. 임계치 초과 시 본 ARD 를 재평가.
- 향후 본 단위 quaternion 의 visual jitter 가 인지 가능 수준이면 보간 강도 또는 본 압축으로 재검토한다.
