# anchor-attach-detach-trigger

**Sub-Spec:** [`01-anchor-mouth-attach-detach.md`](../specs/01-anchor-mouth-attach-detach.md)
**From Tech Spec:** [`tech-specs/01-anchor-mouth-attach-detach.md`](../tech-specs/01-anchor-mouth-attach-detach.md) §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-17

## Context

트럼본 anchor 진입/이탈 라이프사이클은 텔레포트 발화 시점에 맞춰 attach / detach 가 정확히 1회씩 발화해야 한다. XR Interaction Toolkit 의 TeleportationAnchor 는 selectExited 이벤트와 LocomotionProvider.locomotionStarted 이벤트가 따로 노출되며, cancelExit / silent fail / 같은 anchor 재텔레포트 같은 엣지 케이스가 존재한다. drum-stick (sub-spec 01) 에서 동일 문제를 selectExited + locomotionStarted pending window 페어로 안정화한 선례가 있다.

## Options Considered

- **drum-stick 동일 (selectExited + locomotionStarted pending window)** — DrumKitStickAnchor 에서 검증된 패턴. cancelExit 처리·SendTeleportRequest silent fail 방어·재텔레포트 no-op 모두 해결. `spec_what_coverage`: W1(본체 정렬) 만족, W2(양손 GripPose) 만족, W3(이탈 시 복귀) 만족.
- **selectEntered 단일 이벤트** — 단순화. 그러나 cancel / silent fail 엣지 미처리. `spec_what_coverage`: W1 만족, W2 만족, W3 만족 못함 ⚠️ (이탈 트리거 부재).
- **HMD trigger collider zone** — 텔레포트 무관 zone 진입 감지. 도메인 패턴과 이질. `spec_what_coverage`: W1 만족, W2 만족, W3 만족 (자유 이동 대응은 부수 효과).

## Decision

**drum-stick 동일 (selectExited + locomotionStarted pending window)** — 사용자가 권장 옵션 선택. 이미 안정화된 패턴을 답습해 엣지 케이스 처리 코드를 trombone 에서 재발명하지 않는다. drum-stick / trombone 두 anchor 가 동일 라이프사이클을 공유 → 도메인 일관성.

## Spec What Coverage

| What | 옵션 A (선택) | 옵션 B | 옵션 C |
|---|---|---|---|
| W1: 본체가 입 위치로 자동 정렬 | 만족 | 만족 | 만족 |
| W2: 양손에 자동 GripPose | 만족 | 만족 | 만족 |
| W3: 이탈 시 본체 원위치 복귀 + GripPose 해제 | 만족 | 만족 못함 ⚠️ | 만족 |

## Consequences

- TromboneAnchor 구현이 DrumKitStickAnchor 의 selectExited + locomotionStarted + pending window 구조를 재사용한다 (코드 복사 또는 공통 헬퍼 추출은 plan 에서 결정).
- 텔레포트 외 자유 이동(스틱 조이스틱 등)으로 anchor 영역 안팎을 넘나드는 케이스는 detach 가 발화하지 않는다 — 도메인 전체가 텔레포트 기반이라 의도된 동작.
- DrumKitStickAnchor 의 패턴 변경이 발생하면 본 결정도 재평가 대상 (선례가 깨지면 일관성 가치 감소).
