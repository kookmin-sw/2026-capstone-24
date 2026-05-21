# 크로스페이드 중 RequestPitchChange() 중단 처리

**Sub-Spec:** [`05-audio-click-suppression.md`](../specs/05-audio-click-suppression.md)
**From Tech Spec:** `../tech-specs/05-audio-click-suppression.md` §Open Tech Decisions #3
**Status:** `Accepted`
**Date:** 2026-05-21

## Context

슬라이드를 빠르게 이동하면 FadingOut 또는 FadingIn 상태 도중 `RequestPitchChange()`가 재호출된다. 현재 구현이 볼륨 카운터를 초기화(1.0에서 다시 fade-out 시작)하면 볼륨 점프가 발생해 클릭이 된다.

## Options Considered

- **현재 볼륨에서 이어 FadeOut** — `_volumeMultiplier` 현재 값을 유지한 채 FadingOut 재진입. 볼륨 점프 없음. 항상 fade-out → pitch 교체 → fade-in 순서 보장.
- **pitch만 즉시 교체** — 크로스페이드 무시, pitch 값만 즉시 교체하고 현재 fade 계속 진행. 구현 단순하나 FadingIn 도중 파형 불연속 가능성 잔존.

## Decision

**현재 볼륨에서 이어 FadeOut** — 볼륨 점프를 완전히 방지하고 항상 fade-out → pitch 교체 → fade-in 순서를 보장해 클릭 억제 일관성 유지.

## Spec What Coverage

- "크로스페이드 진행 중 슬라이드 위치가 추가로 변경되어도 볼륨 불연속 없이 피치 전환이 완료된다" — 만족

## Consequences

- FadingOut 재진입 시 fade 카운터를 현재 `_volumeMultiplier`에 비례하는 값으로 초기화.
- FadingOut 도중 재진입이면 카운터 reset 없이 현재 볼륨에서 계속 하강.
- FadingIn 도중 재진입이면 `_volumeMultiplier`를 유지한 채 FadingOut으로 전환 → 완료 후 pitch 교체 → FadingIn.
