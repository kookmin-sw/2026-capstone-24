# pitch-resolve-strategy

**Sub-Spec:** [`03-blow-and-pitch-sound.md`](../specs/03-blow-and-pitch-sound.md)
**From Tech Spec:** [`tech-specs/03-blow-and-pitch-sound.md`](../tech-specs/03-blow-and-pitch-sound.md) §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-17

## Context

InstrumentBase 의 표준 MIDI 파이프라인은 NoteOn / NoteOff / Choke / ControlChange 4개 이벤트 타입을 가지며, NotePlayback.pitch 는 NoteOn 시점에 한 번 결정된다. 트럼본은 발음 중 슬라이드 위치 변화에 따라 pitch 가 연속 변화해야 하므로 NoteOn 시점에만 pitch 를 정하면 글리산도 표현이 불가능하다. 어떤 경로로 슬라이드 x 변화를 pitch 에 반영할지 결정해야 한다.

## Options Considered

- **NoteOn 시 clip 재생 + 매 프레임 AudioSource.pitch 직접 운전 + NoteOff 시 stop** — InstrumentAudioOutput active voice 의 AudioSource.pitch 를 트럼본 컴포넌트가 직접 갱신. InstrumentBase API 시그니처 변경 없이 InstrumentAudioOutput 에 active voice pitch setter 추가 정도로 끝. 구현 가볍고 continuous glissando 자연. `spec_what_coverage`: W1(Grip 홀드 발음) 만족, W2(연속 글리산도) 만족, W3(InstrumentBase 표면 노출) 만족.
- **NoteOn + ControlChange(pitch) + NoteOff 파이프라인 완전 통과** — 매 프레임 ControlChange 이벤트 발행. InstrumentBase 에 ControlChange handling 추가 필요. RhythmGame 의 멀티플레이어 동기화에는 이상적이나 인프라 투자 큼. `spec_what_coverage`: W1 만족, W2 만족, W3 만족.
- **매 프레임 반음 quantize → 이산 NoteOn/NoteOff 페어** — Slide x 가 반음 단위로 step 변할 때마다 새 음 발급. RhythmGame 매핑 자연. `spec_what_coverage`: W1 만족, W2 만족 못함 ⚠️ (continuous glissando 불가능), W3 만족.

## Decision

**NoteOn 시 clip 재생 + 매 프레임 AudioSource.pitch 직접 운전 + NoteOff 시 stop** — 사용자가 권장 옵션 선택. 본 sub-spec 의 핵심 What 인 "연속 음높이 변화" 를 가장 가볍게 충족시키며, InstrumentBase 표면 노출(NoteOn / NoteOff 발행) 은 그대로 유지된다. ControlChange 파이프라인은 RhythmGame 실제 연동 시 후속 spec 에서 재평가.

## Spec What Coverage

| What | 옵션 A (선택) | 옵션 B | 옵션 C |
|---|---|---|---|
| W1: 왼손 Grip 홀드 동안 발음 | 만족 | 만족 | 만족 |
| W2: 슬라이드 x 에 따른 연속 글리산도 | 만족 | 만족 | 만족 못함 ⚠️ |
| W3: InstrumentBase 표면 노출 (MidiTriggered 등) | 만족 | 만족 | 만족 |

## Consequences

- InstrumentAudioOutput 에 "active voice 의 AudioSource.pitch 를 외부에서 갱신할 수 있는 진입점" 1개를 추가해야 한다 (plan 단계에서 public method 또는 callback 패턴 결정).
- 트럼본의 MidiTriggered 이벤트는 NoteOn / NoteOff 만 발행 → RhythmGame 연동 시 발음 중 pitch 변화를 외부에서 추적할 수 없다. 추후 RhythmGame 멀티플레이어 또는 판정에 pitch 변화가 필요해지면 본 결정을 ControlChange 모델로 재평가.
- 동시에 1 voice 만 점유 (단일 NoteOn 이므로 active voice 식별이 단순) — 02 sub-spec 의 슬라이드 컨트롤과 동시 holding 일 때도 voice 교체 없이 같은 voice 의 pitch 만 갱신.
