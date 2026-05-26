# Pitch 변경 시 main/audio thread 시퀀싱 전략

**Sub-Spec:** [`04-dsp-pitch-crossfade.md`](../specs/04-dsp-pitch-crossfade.md)
**From Tech Spec:** [`tech-specs/04-dsp-pitch-crossfade.md`](../tech-specs/04-dsp-pitch-crossfade.md) §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-20

## Context

`OnAudioFilterRead`(audio thread)에서 볼륨 엔벨로프를 적용해 클릭을 억제할 때, `AudioSource.pitch` 변경(main thread 전용 Unity API)과 엔벨로프 타이밍을 어떻게 동기화할지 선택해야 한다. 두 스레드가 독립 실행되므로 pitch 교체 시점과 엔벨로프 경계가 어긋나면 클릭이 잔류하거나 thread-unsafe 코드가 된다.

## Options Considered

- **DSP-gated** — audio thread가 fade-out(~5ms) 완료 시 `volatile bool`로 main thread에 신호 → main thread(Update)가 `AudioSource.pitch` 갱신 후 fade-in 신호. 완전 thread-safe, 클릭 0; 단 pitch 교체까지 최대 1프레임(~16ms) 지연.
- **Pitch-first** — main thread가 pitch를 즉시 변경하면서 동시에 DSP에 fade 시작 신호를 보냄. 구현 단순; 단 버퍼 중간에 pitch 변경이 일어날 경우 단일 버퍼 내 불연속점에서 미세 클릭 잔류 가능.

## Decision

**DSP-gated** — fade-out 중 볼륨이 거의 0이므로 최대 1프레임 지연이 사용자에게 인지되지 않는다. 클릭 0 보장이 본 spec의 핵심 요건이므로 안전한 쪽을 채택한다.

## Consequences

- planner는 `TrombonePitchDsp`에 `volatile` 기본형으로 main↔audio thread 단방향 신호 필드를 설계해야 한다.
- main thread Update 루프에서 DSP 신호 폴링 후 `AudioSource.pitch` 교체와 fade-in 신호를 수행해야 한다.
- 슬라이드 반응 속도 불만 피드백이 생기면 Pitch-first 전략으로 전환을 재검토한다.
