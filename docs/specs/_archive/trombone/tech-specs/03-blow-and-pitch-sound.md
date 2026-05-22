# Blow & Continuous Pitch Sound — Tech Spec

**Sub-Spec:** [`03-blow-and-pitch-sound.md`](../specs/03-blow-and-pitch-sound.md)
**Status:** `Draft`
**Date:** 2026-05-17

## Components

- **Trombone (스크립트)** (신규) — `InstrumentBase` 상속. `TryResolveNoteOn` 구현. 왼손 Grip on/off 를 NoteOn / NoteOff MidiEvent 로 변환해 `TriggerMidi` 호출. Slide.localPosition.x → AudioSource.pitch 갱신을 매 프레임 운전.
- **InstrumentBase** (기존) — `TriggerMidi` / `MidiTriggered` 이벤트 / instrumentId / instanceVolume 표면 제공.
- **InstrumentAudioOutput** (기존) — Voice pool. 본 sub-spec 은 active voice 의 AudioSource.pitch 를 외부에서 갱신할 수 있는 진입점이 필요 (구체 API 는 plan 단계).
- **Slide (GameObject)** (기존, 02 에서 운전됨) — pitch 계산 source.
- **Trombone Sound clip** (신규 asset, 사용자 공급) — `Assets/Instruments/Trombone/Sound/C4.wav` 1개. AudioBank 로 로드.
- **Ghost left wrist input** (기존) — 왼손 Grip 누름/뗌 이벤트 source.

## Data / Control Flow

- 왼손 Grip 누름 → Trombone.OnLeftGripPressed → `TriggerMidi(new MidiEvent(MidiNote=60(C4), Velocity=1.0, NoteOn))`.
- `InstrumentBase.TriggerMidi` → `TryResolveNoteOn` (자식 구현) → `NotePlayback(clip=C4.wav, pitch=현재슬라이드pitch비율, velocity=1.0)` 반환 → `InstrumentAudioOutput.PlayNote` 가 voice 할당 + 재생.
- 매 LateUpdate 동안 (왼손 Grip 홀드 + 활성 voice 존재) Trombone 이 InstrumentAudioOutput 의 active voice 의 AudioSource.pitch 를 새 슬라이드 비율로 갱신.
- 왼손 Grip 뗌 → `TriggerMidi(MidiEvent(60, 0, NoteOff))` → `InstrumentAudioOutput.StopNote` (release fade).
- TromboneAnchor.attach == false 면 Trombone 자체가 disable → 왼손 Grip 입력이 발음을 트리거하지 않음.

### Slide x → pitch 계산

- t = Mathf.InverseLerp(slideMinX, slideMaxX, Slide.localPosition.x)
- semitones = Mathf.Lerp(0, -6, t)  // min=0반음, max=-6반음
- pitchRatio = Mathf.Pow(2, semitones / 12) — InstrumentAudioOutput active voice 의 AudioSource.pitch 에 적용.
- baseTone 파일이 C4 (261.63 Hz) 임을 컨벤션으로 박제. NotePlayback.pitch 가 1.0 이면 C4, 0.707 이면 F#3.

## Boundaries

- **건드린다**: Trombone 스크립트 신규 작성, Trombone prefab 에 Trombone 컴포넌트 + InstrumentAudioOutput 컴포넌트 부착, Sound 폴더 신규 (`Assets/Instruments/Trombone/Sound/`), `InstrumentLaneConfig` SO 1개 (lane 매핑은 빈 채로 시작 가능, 표면만 노출), InstrumentBase 시그니처 변경 없이 자식이 active voice pitch 를 갱신할 수 있는 진입점 추가 (plan 단계에서 InstrumentAudioOutput public API 1개 추가 또는 callback 패턴).
- **건드리지 않는다**: 왼손 Grip 입력 외 다른 입력, Piano / Drum 의 사운드 파이프라인, RhythmGame 의 실제 lane 채우기.

## Invariants

- 동시에 1개의 voice 만 활성 (왼손 Grip 1개 = NoteOn 1개). Grip 떼기 전에 다시 Grip 을 눌러도 새 NoteOn 발급하지 않거나, 직전 voice 를 StopNoteImmediate 후 새 NoteOn — 둘 중 plan 에서 결정. 본 Tech Spec 은 "동시 1 voice" 만 박제.
- AudioSource.pitch 는 항상 [pitch(slideMin), pitch(slideMax)] = [1.0, ~0.707] 안.
- TromboneAnchor.attach == false 동안 어떤 입력에도 새 NoteOn 발급되지 않는다.
- baseTone 클립의 실제 음정이 C4 라는 가정이 깨지면 매핑 전체가 어긋난다 — ARD 03 의 consequence.

## Assumptions

- 사용자가 `Assets/Instruments/Trombone/Sound/C4.wav` 한 개의 베이스 톤 wav 를 제공한다. 파일명이 곧 노트 라벨 (Piano 의 `Sound/A0.wav~Ds7.wav` 동일 규약). 출처: 본 인터뷰 ARD 03.
- `InstrumentBase` / `InstrumentAudioOutput` 의 NoteOn/Off 파이프라인은 Piano 의 사용 사례에서 검증됨. 출처: `Read Assets/Instruments/Piano/Scripts/Piano.cs (2026-05-17)` + `Read Assets/Instruments/CLAUDE.md (2026-05-17)`.
- Slide.localPosition.x 가 02 sub-spec 에 의해 [slideMinX, slideMaxX] 범위로 제한된다. 본 Tech Spec 은 그 범위 외 값을 받지 않는다.

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `Assets/Instruments/Piano/Scripts/Piano.cs` | `Trombone` (스크립트) | Piano 는 88건반 sensor 가 fingertip claim 기반으로 NoteOn 발급 + 가까운 옥타브 샘플을 pitch shift. trombone 은 왼손 Grip 1개로 NoteOn 발급 + 매 프레임 active voice pitch 를 직접 갱신 (Piano 는 NoteOn 시점에만 pitch 결정). |
| `Assets/Instruments/Piano/Sound/` | `Assets/Instruments/Trombone/Sound/C4.wav` | Piano 는 옥타브당 2개 샘플(A, Ds). trombone 은 단일 베이스 톤 1개. |

## Open Tech Decisions

- [x] Slide x → pitch 적용 경로 → [decisions/02-pitch-resolve-strategy.md](../decisions/02-pitch-resolve-strategy.md)
- [x] 베이스 톤 wav 입력 규약 → [decisions/03-base-tone-asset-convention.md](../decisions/03-base-tone-asset-convention.md)
