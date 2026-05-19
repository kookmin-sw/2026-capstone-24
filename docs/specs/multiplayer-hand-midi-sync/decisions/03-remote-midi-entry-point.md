# 원격 MIDI 클라이언트 재생 진입점

**Sub-Spec:** [`02-remote-midi-audio.md`](../specs/02-remote-midi-audio.md)
**From Tech Spec:** [`tech-specs/02-remote-midi-audio.md`](../tech-specs/02-remote-midi-audio.md) §Open Tech Decisions #2
**Status:** `Accepted`
**Date:** 2026-05-19

## Context

다른 플레이어의 MIDI RPC 가 자기 클라이언트에 도착했을 때 그 이벤트를 어디서 받아 dispatch 할지 결정한다. 자기 InstrumentBase 의 `TriggerMidi` 를 그대로 호출하면 MidiTriggered 이벤트가 재발행되어 RhythmGame 판정기 같은 기존 구독자가 원격 노트를 자기 입력으로 오인할 수 있다. 한편 InstrumentAudioOutput 의 PlayNote / StopNote 를 직접 호출하면 InstrumentBase 변경은 없지만 sustained · fade envelope · 자식 악기별 dispatch 로직 (TryResolveNoteOn) 을 호출 측에서 재구현해야 한다.

## Options Considered

- **InstrumentBase 에 `ApplyRemoteMidi` 진입점 1개 추가** — 내부적으로 TriggerMidi 와 같은 dispatch 로직을 재사용 (NoteOn / NoteOff / Choke, sustained · fade envelope) 하되 MidiTriggered 이벤트는 발행하지 않음.
- **InstrumentAudioOutput 공개 API 직접 호출** — PlayNote / StopNote / PlayNoteSustained / StopNoteImmediate 를 RemoteMidiApplier 가 직접 호출. InstrumentBase 변경 면적 = 0. sustained · fade envelope · TryResolveNoteOn dispatch 로직을 RemoteMidiApplier 가 자체 구현 → 코드 중복.

## Decision

**InstrumentBase 에 `ApplyRemoteMidi` 진입점 1개 추가** — InstrumentBase 는 이미 "TriggerMidi 단일 진입점" 이라는 계약을 도메인 표준으로 박제하고 있고 (`Assets/Instruments/CLAUDE.md §3`), 원격 진입점도 그 계약의 자연스러운 확장으로 두는 게 도메인 의도와 일치한다. dispatch 로직 중복을 피하고, sustained · fade envelope · TryResolveNoteOn 분기를 자식 악기 변경 없이 그대로 활용할 수 있다.

## Spec What Coverage

| Spec What 항목 | 만족 여부 |
|---|---|
| 다른 플레이어가 악기를 연주하면 그 소리가 자기 헤드폰에서 들린다. | 만족 |
| sustained note 의 자연스러운 release. | 만족 — 기존 fade envelope dispatch 재사용. |
| velocity 강약 구분. | 만족 — TryResolveNoteOn dispatch 재사용. |
| 자기 발신 echo 중복 없음. | 만족 — ApplyRemoteMidi 는 MidiTriggered 발행 안 함. |

## Consequences

- planner 는 InstrumentBase 에 `ApplyRemoteMidi(MidiEvent)` public 진입점을 추가한다.
- planner 는 InstrumentBase 내부 dispatch (NoteOn / NoteOff / Choke 분기, sustained · fade envelope 라우팅) 를 `TriggerMidi` 와 `ApplyRemoteMidi` 가 공유 헬퍼를 통해 재사용하도록 구조화한다.
- `ApplyRemoteMidi` 경로에서는 MidiTriggered 이벤트가 발행되지 않는다는 invariant 를 plan AC 로 검증한다.
- 자식 악기 (Piano / Drum / Trombone) 의 코드는 본 결정으로 변경되지 않는다.
