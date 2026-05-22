# MIDI wire 의 악기 식별자 타입

**Sub-Spec:** [`02-remote-midi-audio.md`](../specs/02-remote-midi-audio.md)
**From Tech Spec:** [`tech-specs/02-remote-midi-audio.md`](../tech-specs/02-remote-midi-audio.md) §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-19

## Context

원격 MIDI RPC 는 어떤 악기에서 발생한 이벤트인지 식별해야 자기 클라이언트가 같은 악기를 찾아 재생할 수 있다. `MidiEvent` struct 에는 이미 `InstrumentId` (ushort) 필드가 "멀티플레이 브로드캐스트 / 리플레이 디스패치용 예약 필드" 로 박제되어 있고, 한편 `InstrumentBase.instrumentId` 는 string 으로 IActiveInstrument 식별자 책임을 진다. 둘 중 무엇을 wire 에 실을지 결정한다.

## Options Considered

- **MidiEvent.InstrumentId (ushort)** — wire 비용 2byte. MidiEvent struct 의 예약 필드 활성화. ushort ↔ string 매핑 테이블을 InstrumentIdRegistry 가 보관. 매핑은 씬 로드 시 1회.
- **InstrumentBase.instrumentId (string)** — wire 에 string 직접. 매핑 테이블 불필요. wire 비용 매 노트마다 string 길이만큼 추가.

## Decision

**MidiEvent.InstrumentId (ushort)** — `MidiEvent` struct 의 예약 필드가 정확히 이 목적이라고 박제되어 있고, NoteOn / NoteOff 가 빈번한 데 비해 wire 비용이 일관되게 작다. 매핑 비용은 씬 로드 1회로 흡수.

## Spec What Coverage

| Spec What 항목 | 만족 여부 |
|---|---|
| 다른 플레이어가 악기를 연주하면 그 소리가 자기 헤드폰에서 들린다. | 만족 |
| 자기 클라이언트의 InstanceVolume 이 원격 노트에 적용된다. | 만족 — 매핑된 자기 InstrumentBase 가 자기 InstanceVolume 으로 재생. |

## Consequences

- planner 는 InstrumentIdRegistry 컴포넌트를 1개 만들어 씬 로드 시점에 모든 InstrumentBase 가 자기 ushort InstrumentId 를 등록하게 한다.
- 각 악기 InstrumentBase 인스펙터에 ushort InstrumentId 필드를 노출하고 default 씬의 Piano · Drum · Trombone 에 고유 값을 박제한다.
- string instrumentId 와 ushort InstrumentId 의 불일치 가능성을 차단하기 위해 plan 에 정합성 검증 AC 를 1건 둔다.
