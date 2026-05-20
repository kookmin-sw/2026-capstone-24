# Tech Spec — DSP Pitch Crossfade

**Sub-Spec:** [`04-dsp-pitch-crossfade.md`](../specs/04-dsp-pitch-crossfade.md)
**Status:** `Draft`
**Date:** 2026-05-20

## Components

- **`Trombone`** (기존) — 슬라이드·Partial 변경 핸들러에서 Choke→NoteOn 대신 pitch 갱신 요청 경로로 전환
- **`InstrumentAudioOutput`** (기존) — 미사용이던 `TrySetActiveVoicePitch()` 를 실제 호출 경로로 연결; Voice 구조에 DSP 컴포넌트 참조 추가
- **`TrombonePitchDsp`** (신규) — Voice의 AudioSource GameObject에 부착하는 MonoBehaviour; `OnAudioFilterRead`로 볼륨 fade 엔벨로프를 적용해 pitch 전환 시 클릭 억제

## Data / Control Flow

슬라이드 변경 시:
- `TromboneSlideController.SlideIndexChanged` → `Trombone.OnSlideChanged()` → 새 effectiveMidi 계산 → `InstrumentAudioOutput.TrySetActiveVoicePitch(note, newPitch)` → `TrombonePitchDsp.RequestPitchChange(newPitch)` (fade-out 시작 신호)

Partial 변경 시:
- `Trombone` 내 Partial 인덱스 변경 감지 (frame loop) → 위와 동일 경로

DSP-gated pitch 교체 시:
- audio thread: fade-out 완료 → `volatile bool`로 main thread 신호
- main thread (Update): `AudioSource.pitch` 갱신 → audio thread에 fade-in 시작 신호

Grip 뗌 / Anchor 이탈:
- 기존 StopNote / Choke 경로 유지; DSP 내부 fade 상태 즉시 리셋

## Boundaries

- **건드린다**: `Trombone.cs` 슬라이드·Partial 변경 핸들러, `InstrumentAudioOutput.cs` Voice 구조, 신규 `TrombonePitchDsp.cs`, Trombone prefab의 Voice AudioSource GameObject
- **건드리지 않는다**: `InstrumentBase`, `TromboneSlideController`(이벤트 발행 측), `TrombonePartialController`(값 제공 측), `InstrumentAudioOutput`의 NoteOn/NoteOff/Choke 경로, 다른 악기(Piano, DrumKit) 오디오 파이프라인

## Invariants

- `AudioSource` API(`pitch`, `Play`, `Stop`)는 반드시 Unity main thread에서만 호출한다. `OnAudioFilterRead` 콜백(audio thread) 내에서 AudioSource 멤버를 직접 수정하지 않는다.
- main thread ↔ audio thread 공유 상태는 `volatile` 기본형(int, float, bool)으로만 교환한다.
- fade 엔벨로프 중 `AudioSource.pitch` 변경은 fade-out 완료 후 main thread가 처리하며, 그 전까지 audio thread는 음량만 제어한다.
- `TrombonePitchDsp`가 부착되지 않은 Voice는 기존 동작(즉시 pitch 갱신, 클릭 억제 없음)을 유지한다.
- Choke / NoteOff 경로가 호출되면 DSP 내부 fade 상태를 즉시 리셋한다.

## Assumptions

- `InstrumentAudioOutput`의 Voice pool은 각 Voice가 별도 GameObject + AudioSource로 구성된다 — 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-20)`
- `TromboneSlideController.SlideIndexChanged`는 `Trombone`에 구독된 C# event다 — 출처: `Read Assets/Instruments/Trombone/Scripts/Trombone.cs (2026-05-20)`
- `Trombone`의 frame loop(`Update` 또는 `LateUpdate`)에서 Partial 인덱스 변경을 감지 가능하다 — 출처: `Read Assets/Instruments/Trombone/Scripts/Trombone.cs (2026-05-20)`
- Unity audio sample rate는 44100 Hz — planner가 구현 시 `AudioSettings.outputSampleRate`로 런타임 확인 권장

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `docs/specs/trombone/tech-specs/03-blow-and-pitch-sound.md` | `04-dsp-pitch-crossfade.md` | 03은 최초 발음 라이프사이클(NoteOn/Off) 설계; 04는 발음 중 pitch 전환 클릭 억제용 DSP 레이어 추가 |

## Open Tech Decisions

- [ ] pitch 변경 시 main/audio thread 시퀀싱 전략 — DSP-gated vs pitch-first → `decisions/04-pitch-change-thread-sync.md`
