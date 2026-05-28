# Hard Step Easing — 표시각 전환 곡선

**Sub-Spec:** [`../specs/01-view-snap.md`](../specs/01-view-snap.md)
**From Tech Spec:** [`../tech-specs/01-view-snap.md`](../tech-specs/01-view-snap.md) §Open Tech Decisions #2
**Status:** `Accepted`
**Date:** 2026-05-27

## Context

표시각이 5단계 anchor 사이로 전환될 때, instant snap이면 시각 점프가 거칠고 너무 긴
ease는 5단계 고정감을 약화시킨다. 사용자 인지 가능한 stair-step 느낌은 유지하되 시각
점프는 완화해야 한다.

## Options Considered

- **Instant (0ms)** — 가장 강한 계단감. 파셜 경계에서 시각 점프 거칠음 ⚠️.
- **짧은 easing (80~120ms exponential)** — 점프 완화 + 계단감 유지. SerializeField로 inspector tuning 가능. default 100ms exponential ease-out.
- **매개변수화만 (default 100ms, 결정 유보)** — 구체값을 spec에 박지 않고 inspector 튜닝에 위임. spec 의도 약화 ⚠️.

## Decision

**짧은 easing (80~120ms exponential, default 100ms ease-out)** — 5단계 고정감과 시각 부드러움의 균형. duration·곡선을 SerializeField로 노출하되 default를 100ms exponential ease-out으로 spec에 박제해 의도를 명확히 한다.

## Spec What Coverage

- Rig/Body 표시 회전이 5개 anchor 각도로 수렴: **만족** (ease 종료 후 정확히 anchor 값)
- Hard step "고정감": **만족** (100ms 이내 수렴, 인지 가능한 stair-step 유지)
- 짧은 transition: **만족** (ease 진행 중에만 anchor 외 값 허용)

## Consequences

- TromboneBodyVisualSnap에 `easeDurationSeconds` (default 0.1f) + ease 곡선 표현(AnimationCurve 또는 exponential factor) SerializeField 노출.
- PartialIndex 변경 시점부터 ease 시작. 이전 ease가 진행 중인 상태에서 PartialIndex가 또 바뀌면 현재 표시각에서 새 target으로 재출발 (jitter 방지).
- 변경 시 재평가 trigger: 사용자 피드백에서 "점프가 거칠다" 또는 "느려서 답답하다" 패턴이 보고될 경우.
