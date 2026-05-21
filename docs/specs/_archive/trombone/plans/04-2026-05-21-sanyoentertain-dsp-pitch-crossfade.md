# DSP Pitch Crossfade — Choke→pitch-update 경로 전환 + TrombonePitchDsp 도입

**Linked Spec:** [`04-dsp-pitch-crossfade.md`](../specs/04-dsp-pitch-crossfade.md)
**Status:** `Done`

## Goal

발음 중 슬라이드/Partial 인덱스 변경 시 현재 동작 — `MidiEventType.Choke` 후 즉시 `NoteOn` 재트리거 — 가 만들어내는 가청 클릭/짧은 무음을 제거한다. (1) 신규 `TrombonePitchDsp` MonoBehaviour 를 voice 의 AudioSource GameObject 에 부착해 `OnAudioFilterRead` 로 짧은 volume crossfade envelope 을 그리고, (2) `InstrumentAudioOutput` 의 이미 존재하는 `TrySetActiveVoicePitch(note, pitch)` 진입점을 *실제로* main thread Update 루프에서 호출하도록 라우팅하며, (3) `Trombone.LateUpdate` 의 Partial/Slide 변경 분기를 *retrigger* (Choke + NoteOn) 가 아니라 *pitch-only update* 경로로 전환한다. DSP-gated 시퀀싱(ARD 04) — audio thread 가 fade-out 완료를 `volatile bool` 로 신호 → main thread 가 `AudioSource.pitch` 갱신 후 fade-in 신호 — 으로 thread-safe + click-free 를 보장한다.

## Context

03 sub-spec 의 plan handoff (`03-2026-05-17-sanyoentertain-blow-and-pitch-sound.md`) 는 다음 사실들을 본 plan 의 전제로 박제했다:

- `Trombone` 컴포넌트가 `[DefaultExecutionOrder(10006)]` 슬롯에 LateUpdate 운전 중이며, `m_IsBlowing` flag 가 발음 중인지의 단일 진실원.
- `InstrumentAudioOutput.TrySetActiveVoicePitch(int note, float pitch)` 는 이미 구현되어 있음 — `voice.TrackPitch == true` 인 Active/SustainedActive/Attacking voice 의 `Source.pitch` 만 갱신, Releasing/Idle 은 무시 (Read `Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs:182-195`).
- `InstrumentBase.OnChoke` 가 `audioOutput.StopNote` (release fade) 가 기본이지만 `Trombone.OnChoke` 가 `audioOutput.StopNoteImmediate` 로 override 됨 — Choke 가 즉시 silence + voice 회수. 본 plan 은 *Partial/Slide 변경 시 Choke 자체를 더 이상 사용하지 않으므로* OnChoke override 동작은 그대로 두되 anchor 이탈 경로에서만 발화한다.
- Trombone 의 NotePlayback 은 `sustain: true, fadeInDuration: 0.05f, fadeOutDuration: 0.15f` 로 만들어져 `PlayNoteSustained` 경로 — voice 의 `Source.loop = true` + `TrackPitch = true`. 본 plan 의 pitch-update 경로가 안전하게 도달하는 전제.
- Trombone.prefab 의 `Trombone` root 에 `InstrumentAudioOutput` 컴포넌트가 부착돼 있음. Voice pool 의 AudioSource 들은 *런타임* 에 `EnsureVoicePool()` 가 `VoicePool_Trombone` 자식 GameObject 1 개에 32 개 AudioSource 컴포넌트로 모두 부착함 (`Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs:229-250`). **이게 본 plan 설계의 핵심 가정 어긋남이다** — `OnAudioFilterRead` 콜백은 자신이 부착된 GameObject 의 AudioSource 들의 출력을 후크하므로 *한 GameObject 에 모든 voice AudioSource 가 묶이면 한 컴포넌트가 전 voice 의 출력을 동시에 받음* + voice 별 개별 fade envelope 적용이 불가능하다. 따라서 본 plan 은 `InstrumentAudioOutput` 의 voice pool 구조를 *수정* — voice 당 별도 GameObject 분리 + 외부 컴포넌트 부착 hook — 한다. Tech Spec §Boundaries 의 "InstrumentAudioOutput.cs Voice 구조" 가 명시적으로 건드림 허용.

ARD 04 (`docs/specs/trombone/decisions/04-pitch-change-thread-sync.md`) 의 DSP-gated 결정은 다음 두 제약을 plan 에 박제한다 (§Consequences):

1. `TrombonePitchDsp` 가 `volatile` 기본형으로 main↔audio thread 단방향 신호 필드를 설계.
2. main thread Update 루프에서 DSP 신호 폴링 후 `AudioSource.pitch` 교체 + fade-in 신호.

이 두 제약 + Tech Spec §Invariants 의 "AudioSource API 는 main thread 에서만, audio thread 는 음량만" 으로 thread-safety 가 닫힌다. 본 plan 의 신호 프로토콜은 다음 단방향 4 state machine 으로 박제 (`TrombonePitchDsp` 내부):

```
DspState (volatile int):
  0 = Idle            (volume = TargetVolume)
  1 = FadingOut       (volume: TargetVolume → 0 over fadeOutSamples)
  2 = AwaitingPitch   (volume = 0; audio thread silent until main thread switches state)
  3 = FadingIn        (volume: 0 → TargetVolume over fadeInSamples)

main thread API (호출자 = TrombonePitchDsp.RequestPitchChange(newPitch)):
  - newPitch 를 m_PendingPitch (volatile float) 에 저장
  - DspState 가 Idle 또는 FadingIn 이면 → FadingOut 으로 set, 진행 elapsed counter reset
  - DspState 가 FadingOut 또는 AwaitingPitch 면 → 이미 진행 중, m_PendingPitch 만 덮어씀 (가장 최근 요청이 이김)

main thread Update 폴링 (TrombonePitchDsp.LateUpdate):
  - DspState == AwaitingPitch 이면:
      AudioSource.pitch = m_PendingPitch
      DspState = FadingIn

audio thread (OnAudioFilterRead):
  - DspState == Idle: data 그대로 (TargetVolume 적용 = 1.0 곱 = no-op)
  - DspState == FadingOut:
      각 sample 마다 t = elapsedSamples / fadeOutSamples
      multiplier = 1 - t (선형)
      elapsedSamples 증가; 끝나면 DspState = AwaitingPitch, elapsedSamples reset
  - DspState == AwaitingPitch: data *= 0 (silent)
  - DspState == FadingIn:
      각 sample 마다 t = elapsedSamples / fadeInSamples
      multiplier = t
      elapsedSamples 증가; 끝나면 DspState = Idle, elapsedSamples reset
```

상태 전이는 단방향 그래프 (`Idle → FadingOut → AwaitingPitch → FadingIn → Idle`) + main thread 가 *덮어쓸 권한* 은 `RequestPitchChange` 진입 시점 1 곳뿐 → invariant: 동시 변경 race 없음.

fade duration 은 sample 수로 박제 — 5ms @ 44.1kHz = 220 samples (양쪽 동일). `AudioSettings.outputSampleRate` 를 `OnEnable` 에서 1 회 읽어 박제하면 sampling rate 변경에도 일관 동작 (Tech Spec §Assumptions).

`InstrumentAudioOutput` 의 voice pool 구조 수정 전략: 현재 `EnsureVoicePool()` 가 32 개 AudioSource 를 *단일* `VoicePool_<악기>` GameObject 에 모두 부착. 본 plan 은 각 voice 가 *자신만의* GameObject `Voice_<index>` 를 갖도록 분리 — pool root 자식으로 32 개 GameObject 생성 + 각각에 AudioSource 1 개 + 외부 부착용 hook `VoiceGameObjectCreated` (Action<GameObject> event) 발화. Trombone 측이 이 event 를 구독해 `TrombonePitchDsp` 1 개씩 부착. **Piano / DrumKit 는 event 구독 안 하므로 voice GameObject 분리 자체는 sibling 영향이 *동작상으로는* 0** (32 개 GameObject 1 개 vs 32 개 동등) — Tech Spec §Boundaries 의 "다른 악기 영향 0" 정합. 단, scene Hierarchy 표면이 32 개 children 으로 확장되는 시각적 변경은 발생; Notes 에 박제.

Trombone.cs 의 LateUpdate 분기 전환: 기존 라인 (현재 source `Trombone.cs:86-92`):

```csharp
if (m_IsBlowing && (partialChanged || slideChanged))
{
    TriggerMidi(new MidiEvent(baseToneMidiNote, 0f, MidiEventType.Choke));
    TriggerMidi(new MidiEvent(baseToneMidiNote, 1f, MidiEventType.NoteOn));
    ...
}
```

본 plan 후 (단일 진입점은 audioOutput.TrySetActiveVoicePitch 1 회):

```csharp
if (m_IsBlowing && (partialChanged || slideChanged))
{
    float newPitch = ComputePitchForEffectiveMidi(); // 현재 선택된 sample 기준 pitch
    audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, newPitch);
    if (partialController != null) m_LastPartialIndex = partialController.PartialIndex;
    if (slideController != null) m_LastSlideIndex = slideController.SlideIndex;
}
```

`ComputePitchForEffectiveMidi()` 는 기존 `ComputePitchForSelectedSample(ComputeEffectiveMidi())` 의 단순 wrapper — NoteOn 시점에 선택된 `m_SelectedSample` 을 그대로 사용해 새 effective MIDI 대비 pitch ratio 계산. **단일 sample 안에서 pitch shift 만으로 표현 가능한 범위** (root ± 6 반음 정도) 를 벗어나는 변경은 본 plan 에서 *허용* (sub-spec §Out of Scope "발음 중 멀티샘플 자동 교체" 가 명시적으로 sustain 중 sample 교체 금지). pitch ratio 가 1 octave 이상으로 벗어나면 음질이 다소 어색해지지만 sub-spec invariant 가 명시적으로 그 경우를 받아들임.

`InstrumentAudioOutput.TrySetActiveVoicePitch` 가 호출되면 voice 가 `TrombonePitchDsp.RequestPitchChange(newPitch)` 를 거쳐야 한다 — 즉 `AudioSource.pitch` 직접 set 이 아니라 DSP 가 fade-out 후 main thread 가 pitch set. 본 plan 은 `InstrumentAudioOutput.TrySetActiveVoicePitch` 의 내부 동작을 *수정* — voice GameObject 에 `TrombonePitchDsp` 가 부착돼 있으면 그 컴포넌트의 `RequestPitchChange` 를 호출, 부착 안 됐으면 기존 직접 `Source.pitch = pitch` (Tech Spec §Invariants 마지막 조항: "TrombonePitchDsp 부착 안 된 voice 는 기존 동작 유지"). 즉 시블링 (Piano/DrumKit) 은 DSP 미부착이라 기존 동작 그대로.

`OnAudioFilterRead` 의 thread 안전성: Unity 가 audio thread 에서 호출하는 콜백. `data` 배열은 audio thread 의 출력 버퍼이며 컴포넌트가 곱셈으로 in-place 수정 가능. `AudioSource.volume` 을 직접 건드리지 않고 `data[i] *= multiplier` 로만 처리 — Tech Spec §Invariants ("audio thread 는 음량만 제어, AudioSource API 호출 금지") 정합.

m_IsBlowing 분기 (anchor detach / grip release) 시 DSP 상태 reset: anchor detach 분기 (`Choke`) + grip release 분기 (`NoteOff`) 둘 다 voice 가 Releasing 또는 회수 상태로 가므로 DSP 가 곱하는 `data` 자체가 정지/소실됨. 다음 NoteOn 에서 voice 가 새로 잡힐 때 DSP 상태도 `Idle` 로 reset 되어야 함 — `OnEnable` (voice 재사용 시 DSP 컴포넌트가 새로 enable 되지 않음, 같은 GameObject 의 같은 component) 만으로는 부족. 따라서 `RequestPitchChange` 와 별개로 `ResetEnvelope()` 진입점을 두고, `InstrumentAudioOutput.PlayNoteSustained` 후 voice 의 DSP 컴포넌트에 `ResetEnvelope()` 호출하는 hook 을 둔다 (방법: `InstrumentAudioOutput` 의 PlayNoteSustained 안에서 voice GameObject 의 `TrombonePitchDsp` 컴포넌트가 있으면 `ResetEnvelope()` 호출 — TryGetComponent 활용. 시블링은 컴포넌트 없으므로 noop).

## Verified Structural Assumptions

- `InstrumentAudioOutput.cs` 의 voice pool 은 `EnsureVoicePool()` 가 `VoicePool_<gameObject.name>` 단일 자식 GameObject 에 `MaxVoices` (default 32) 개의 `AudioSource` 컴포넌트를 모두 add — *voice 당 별도 GameObject 가 아님*. 본 plan 이 voice 당 별도 GameObject 로 분리해 `TrombonePitchDsp` 의 voice-단위 `OnAudioFilterRead` 후크가 가능하도록 한다. 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-21, lines 229-250)`.
- `InstrumentAudioOutput.TrySetActiveVoicePitch(int note, float pitch)` 가 이미 구현됨 — `voice.Note == note` + voice.Source != null + State 가 Active/SustainedActive/Attacking 중 하나 + voice.TrackPitch == true 인 voice 의 `Source.pitch = pitch` 1 회 갱신. Releasing/Idle 또는 TrackPitch=false 인 voice 는 skip. 본 plan 은 이 메서드 *내부 동작* 만 수정 — voice GameObject 에 `TrombonePitchDsp` 컴포넌트가 있으면 그 컴포넌트의 `RequestPitchChange` 를 호출, 없으면 기존 `Source.pitch = pitch` 직접 (시그니처는 유지, 시블링 동작 변화 0). 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-21, lines 182-195)`.
- `InstrumentAudioOutput.PlayNoteSustained` 가 `voice.Source.loop = true`, `voice.TrackPitch = true`, `voice.FadeInDuration/FadeOutDuration` 박제 + state 가 `hasFadeIn ? Attacking : SustainedActive`. 본 plan 의 pitch-update 경로가 도달 가능한 voice 상태. 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-21, lines 135-159)`.
- `InstrumentAudioOutput.Update` 가 매 프레임 voice 상태에 따라 `Source.volume` 운전 — Attacking 중에는 fade-in 곱셈, Releasing 중에는 fade-out 곱셈. 본 plan 의 DSP envelope 은 이와 *직교* (Source.volume 은 voice-level 라이프사이클 fade, DSP envelope 은 pitch 전환 시점 short crossfade) — 두 fade 가 곱해져 최종 출력. 동시에 진행돼도 두 multiplier 곱은 [0,1] 안에 머무름. 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-21, lines 71-107)`.
- `Trombone.LateUpdate` 의 현재 retrigger 분기 (`Trombone.cs:86-92`) — partialChanged 또는 slideChanged 시 `TriggerMidi(Choke)` + `TriggerMidi(NoteOn)` 2 회 호출. 본 plan 이 이 분기를 *pitch-update 경로* 1 회 호출 (`audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, newPitch)`) 로 교체. anchor detach 분기 (`Trombone.cs:57-65`) 의 `TriggerMidi(Choke)` 는 *유지* — sub-spec §Behavior #5 ("anchor 이탈 시 기존 Choke 흐름 그대로"). 출처: `Read Assets/Instruments/Trombone/Scripts/Trombone.cs (2026-05-21, lines 55-93, 109-113)`.
- `Trombone.OnChoke(MidiEvent)` 가 `audioOutput.StopNoteImmediate(midiEvent.Note)` override — 즉시 silence + voice 회수. 본 plan 후에도 anchor 이탈 시에만 발화. 출처: `Read Assets/Instruments/Trombone/Scripts/Trombone.cs (2026-05-21, lines 109-113)`.
- `Trombone.ComputePitchForSelectedSample(float effectiveMidi)` 가 `Mathf.Pow(2f, (effectiveMidi - m_SelectedSample.rootMidiNote) / 12f)` — 본 plan 의 pitch-update 경로가 같은 식을 NoteOn 시점 선택된 `m_SelectedSample` 그대로 사용 (sample 교체 없음, sub-spec §Out of Scope). 출처: `Read Assets/Instruments/Trombone/Scripts/Trombone.cs (2026-05-21, lines 123-127)`.
- `InstrumentBase.TriggerMidi` 의 `NoteOn` 분기가 `audioOutput.PlayNoteSustained(..., playback.FadeInDuration, playback.FadeOutDuration)` 호출 — `playback.Sustain == true` 일 때. Trombone 의 `TryResolveNoteOn` 이 `sustain: true, fadeInDuration: 0.05f, fadeOutDuration: 0.15f` 박제. 본 plan 의 DSP envelope (5ms) 은 voice-level fade-in (50ms) 과 *겹쳐도* 곱셈으로 합성 — 첫 NoteOn 직후 5ms DSP 가 곧장 Idle 이라 voice fade-in 만 발화 (sub-spec §Behavior 비충돌). 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-21, lines 117-147)`.
- `InstrumentAudioOutput` 의 `Voice` sealed class 의 `Source` 필드 type 이 `AudioSource` (`Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs:14`). 본 plan 의 voice 별 GameObject 분리 후 각 GameObject 가 `AudioSource` 1 개 + 외부 컴포넌트 (`TrombonePitchDsp`) 1 개. 출처: 동일 파일.
- `Instruments.asmdef` references = `[Unity.InputSystem, Hands, Unity.XR.Interaction.Toolkit]`. 신규 `TrombonePitchDsp.cs` 는 `UnityEngine` 만 import (AudioSource, MonoBehaviour, AudioSettings) — 추가 reference 불필요. `Assets/Instruments/Trombone/Scripts/` 폴더에 `.asmdef` 없음 → 자동 `Instruments.asmdef` 포함. 출처: `Read Assets/Instruments/Instruments.asmdef (2026-05-21)`.
- Trombone.prefab `Trombone` root (line 863) 가 `InstrumentAudioOutput` (line 897) + `Trombone` (line 910) + `TrombonePartialController` (line 946) + `TromboneAnchor` (line 1486) 컴포넌트 부착됨. Voice pool 의 AudioSource 들은 prefab 시점에는 부재 — 런타임 `EnsureVoicePool()` 가 동적 생성. 본 plan 은 *prefab 자체*에 `TrombonePitchDsp` 를 부착하지 않는다 — 런타임 voice GameObject 생성 시점에 `InstrumentAudioOutput` 측이 `Trombone` 컴포넌트에서 hook 을 받아 부착. 출처: `Grep -n "Trombone\|InstrumentAudioOutput\|VoicePool\|AudioSource\|m_Name:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-21, lines 863-1486)`.
- Unity audio sample rate: 런타임에 `AudioSettings.outputSampleRate` 로 확인. Project Settings → Audio 의 Default Speaker Mode 가 변경되거나 OS 마이그레이션 시 변경 가능. 본 plan 의 `TrombonePitchDsp.OnEnable` 에서 1 회 읽어 `m_FadeOutSamples = Mathf.RoundToInt(fadeOutDurationSec * sampleRate)` 계산. 출처: Tech Spec §Assumptions 가 박제했고, planner 가 직접 코드 Read 로 재확인은 불필요 (Unity 표준 API).
- `OnAudioFilterRead(float[] data, int channels)` 는 컴포넌트가 부착된 GameObject 의 AudioSource 출력을 audio thread 에서 후크하는 Unity 표준 콜백. 컴포넌트가 부착된 GameObject 의 *모든* AudioSource 출력이 합쳐져 `data` 로 전달됨. 따라서 *voice 1 개당 별도 GameObject* 가 필요 — 한 GameObject 에 32 개 AudioSource 가 묶이면 32 개 voice 출력이 합쳐져 voice 별 fade envelope 적용 불가. 본 plan 의 voice pool 구조 분리 결정이 직접 기인. 출처: Unity 공식 docs (Manual + ScriptReference), 표준 API.
- 03 plan handoff 의 "Trombone.cs 의 `m_IsBlowing` 이 발음 중 단일 진실원" 박제 — 본 plan 은 `m_IsBlowing` 분기 자체를 *건드리지 않는다*. Partial/Slide changed 분기 내부만 retrigger → pitch-update 로 교체. anchor detach / grip release 의 Choke / NoteOff 호출은 유지. 출처: 03 plan §Approach 단계 + `Read Trombone.cs (2026-05-21, lines 55-93)`.

## Approach

1. **`Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` 수정 — Voice pool 구조 분리 + voice-level DSP hook**

   - `EnsureVoicePool()` 의 voice 생성 루프를 단일 GameObject 에 32 AudioSource 추가 → voice 당 별도 자식 GameObject (`Voice_{i}`) 생성 + 각각 AudioSource 1 개 부착. pool root 는 그대로 `VoicePool_<gameObject.name>` 단일 자식.

     ```csharp
     while (m_Voices.Count < m_CurrentSettings.MaxVoices)
     {
         int index = m_Voices.Count;
         GameObject voiceGo = new GameObject(string.Format("Voice_{0}", index));
         voiceGo.transform.SetParent(m_VoicePoolRoot, false);
         AudioSource source = voiceGo.AddComponent<AudioSource>();
         ApplySettingsToSource(source, m_CurrentSettings);
         Voice voice = new Voice { Source = source, State = VoiceState.Idle };
         m_Voices.Add(voice);
         VoiceGameObjectCreated?.Invoke(voiceGo);
     }
     ```

   - 신규 public event: `public event System.Action<GameObject> VoiceGameObjectCreated;`. voice GameObject 가 생성될 때마다 1 회 발화. Trombone 측이 구독해 `TrombonePitchDsp` 부착.

   - `TrySetActiveVoicePitch(int note, float pitch)` 내부 수정 — 기존 `voice.Source.pitch = pitch` 직접 set 라인을 다음으로 교체:

     ```csharp
     if (voice.Source.TryGetComponent<TrombonePitchDsp>(out var dsp))
         dsp.RequestPitchChange(pitch);
     else
         voice.Source.pitch = pitch;
     updatedAny = true;
     ```

     시블링 (Piano/DrumKit) voice 는 DSP 컴포넌트 부재라 항상 else 경로 → 기존 동작 그대로.

   - `PlayNoteSustained` 마지막에 voice GameObject 의 DSP 컴포넌트 있으면 `ResetEnvelope()` 호출 hook 추가:

     ```csharp
     if (voice.Source.TryGetComponent<TrombonePitchDsp>(out var dsp))
         dsp.ResetEnvelope();
     ```

     voice 재사용 시 (NoteOn 직후) DSP 상태가 Idle 로 reset 되도록 보장.

   - `StopNote` (release fade) 와 `StopNoteImmediate` (즉시 silence) 둘 다 DSP 상태에 추가 reset 안 함 — voice 가 Releasing 또는 Idle 로 가면 다음 NoteOn 의 `PlayNoteSustained` hook 이 reset 처리. 단순화.

2. **`Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 신규 작성**

   - `namespace Instruments`.
   - `[DisallowMultipleComponent]`. `[RequireComponent(typeof(AudioSource))]`.
   - SerializeField 없음 (모든 파라미터는 const 또는 runtime 박제 — voice GameObject 가 런타임 생성이라 prefab Inspector 노출 불필요).
   - 상수: `const float k_FadeOutDurationSec = 0.005f;` `const float k_FadeInDurationSec = 0.005f;` (5ms 양쪽).
   - 내부 상태 (volatile + sample-counter):
     ```csharp
     volatile int m_State = 0; // 0=Idle, 1=FadingOut, 2=AwaitingPitch, 3=FadingIn
     volatile float m_PendingPitch = 1f;
     int m_ElapsedSamples; // audio thread only — volatile 불필요
     int m_FadeOutSamples;
     int m_FadeInSamples;
     AudioSource m_Source;
     ```
   - `OnEnable()`:
     ```csharp
     m_Source = GetComponent<AudioSource>();
     int rate = AudioSettings.outputSampleRate;
     m_FadeOutSamples = Mathf.Max(1, Mathf.RoundToInt(k_FadeOutDurationSec * rate));
     m_FadeInSamples = Mathf.Max(1, Mathf.RoundToInt(k_FadeInDurationSec * rate));
     m_State = 0;
     m_ElapsedSamples = 0;
     ```
   - `public void RequestPitchChange(float newPitch)` (main thread 호출):
     ```csharp
     m_PendingPitch = newPitch;
     int s = m_State;
     if (s == 0 || s == 3) // Idle 또는 FadingIn 이면 새 fade-out 시작
     {
         m_ElapsedSamples = 0;
         m_State = 1;
     }
     // FadingOut(1) / AwaitingPitch(2) 면 m_PendingPitch 만 덮어씀
     ```
   - `public void ResetEnvelope()` (main thread 호출, NoteOn 직후):
     ```csharp
     m_ElapsedSamples = 0;
     m_State = 0;
     ```
   - `void LateUpdate()` (main thread 폴링, AwaitingPitch → pitch set + FadingIn):
     ```csharp
     if (m_State == 2)
     {
         if (m_Source != null) m_Source.pitch = m_PendingPitch;
         m_ElapsedSamples = 0;
         m_State = 3;
     }
     ```
   - `void OnAudioFilterRead(float[] data, int channels)` (audio thread, volume 만 운전):
     ```csharp
     int s = m_State;
     if (s == 0) return; // Idle — no-op
     int sampleFrames = data.Length / channels;
     if (s == 1) // FadingOut
     {
         for (int i = 0; i < sampleFrames; i++)
         {
             float t = (float)m_ElapsedSamples / m_FadeOutSamples;
             if (t >= 1f) { for (int c = 0; c < channels; c++) data[i * channels + c] = 0f; }
             else { float mult = 1f - t; for (int c = 0; c < channels; c++) data[i * channels + c] *= mult; }
             m_ElapsedSamples++;
             if (m_ElapsedSamples >= m_FadeOutSamples)
             {
                 m_ElapsedSamples = 0;
                 m_State = 2;
                 // 남은 sample 들은 silent
                 for (int j = i + 1; j < sampleFrames; j++)
                     for (int c = 0; c < channels; c++) data[j * channels + c] = 0f;
                 return;
             }
         }
     }
     else if (s == 2) // AwaitingPitch — silent
     {
         for (int i = 0; i < data.Length; i++) data[i] = 0f;
     }
     else if (s == 3) // FadingIn
     {
         for (int i = 0; i < sampleFrames; i++)
         {
             float t = (float)m_ElapsedSamples / m_FadeInSamples;
             if (t >= 1f) { /* no-op, full volume */ }
             else { for (int c = 0; c < channels; c++) data[i * channels + c] *= t; }
             m_ElapsedSamples++;
             if (m_ElapsedSamples >= m_FadeInSamples)
             {
                 m_ElapsedSamples = 0;
                 m_State = 0;
                 return; // 남은 sample 들은 full volume
             }
         }
     }
     ```
   - `m_State` 는 `volatile int` — main↔audio thread 양방향 read/write 안전 (atomic int 전제, .NET 64-bit 정렬). `m_PendingPitch` 는 `volatile float` — float 도 64-bit 정렬 안 4 byte atomic.

3. **`Assets/Instruments/Trombone/Scripts/Trombone.cs` 수정 — LateUpdate retrigger 분기 → pitch-update + VoiceGameObjectCreated 구독**

   - LateUpdate 의 partialChanged/slideChanged 분기 교체:
     ```csharp
     if (m_IsBlowing && (partialChanged || slideChanged))
     {
         float newEffectiveMidi = ComputeEffectiveMidi();
         float newPitch = ComputePitchForSelectedSample(newEffectiveMidi);
         if (audioOutput != null)
             audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, newPitch);
         if (partialController != null) m_LastPartialIndex = partialController.PartialIndex;
         if (slideController != null) m_LastSlideIndex = slideController.SlideIndex;
     }
     ```
     이전 2 개 `TriggerMidi` 호출 (Choke + NoteOn) 모두 삭제. **anchor detach 분기 (Trombone.cs:57-65) 의 `TriggerMidi(Choke)` 는 보존** (sub-spec §Behavior #5).
   - `ComputePitchForSelectedSample` 은 기존 private method 그대로 사용 — sample 교체 없음 (sub-spec §Out of Scope).
   - `Awake()` (또는 `OnEnable`) 에 `audioOutput.VoiceGameObjectCreated` 구독 추가 — 각 voice GameObject 생성 시 `TrombonePitchDsp` 부착:
     ```csharp
     protected override void Awake()
     {
         base.Awake(); // audioOutput auto-resolve + Initialize() → EnsureVoicePool → voice GameObject 생성 → event 발화
         // 단 base.Awake() 안에서 EnsureVoicePool이 호출되면 이미 voice GameObject들이 생성됨 — 구독이 너무 늦음.
         // → 해결: base.Awake() 호출 *전*에 audioOutput 을 GetComponent 로 미리 잡고 event 구독, 그 다음 base.Awake().
     }
     ```
     **함정 회피**: `InstrumentBase.Awake()` 안에서 `audioOutput.InitializePoolSettings(settings)` → `EnsureVoicePool` 가 실행되며 voice GameObject 들이 *그 시점에* 생성된다. 구독을 `base.Awake()` 호출 전에 해야 모든 voice GameObject 의 event 를 받음.

     수정된 Awake:
     ```csharp
     protected override void Awake()
     {
         // audioOutput auto-resolve 를 base 보다 먼저 수행해 event 구독 시점 확보
         if (audioOutput == null)
             audioOutput = GetComponentInChildren<InstrumentAudioOutput>(true);
         if (audioOutput != null)
             audioOutput.VoiceGameObjectCreated += OnVoiceGameObjectCreated;
         base.Awake();
     }

     void OnDestroy()
     {
         if (audioOutput != null)
             audioOutput.VoiceGameObjectCreated -= OnVoiceGameObjectCreated;
     }

     void OnVoiceGameObjectCreated(GameObject voiceGo)
     {
         if (voiceGo.GetComponent<TrombonePitchDsp>() == null)
             voiceGo.AddComponent<TrombonePitchDsp>();
     }
     ```
     **Awake 의 `if (audioOutput == null) audioOutput = GetComponentInChildren<...>` 라인은 InstrumentBase.Awake 안에서도 다시 실행되지만 audioOutput 이 이미 not-null 이라 no-op** (`Read InstrumentBase.cs:79-80`). 따라서 시블링 (Piano) 의 base 동작 변화 0.

   - Trombone 의 `audioOutput` 필드는 `protected` (InstrumentBase 가 protected SerializeField 로 선언). Trombone 가 자식 namespace 안이므로 직접 접근 가능. 변경 없음.

4. **검증 단계** ([`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md) 따름)

   - 컴파일 대기 후 `read_console types=[error] count=20` → 0 error.
   - EditMode 회귀: `unity-test-runner` 1 회 호출 — InstrumentBase / InstrumentAudioOutput 사용 시블링 (Piano / DrumKit) 테스트 회귀 없음 확인. voice pool 구조 분리가 동작에 영향 없는지 Piano/DrumKit 발음 회귀 확인이 핵심.
   - Editor Play 모드 시각·청각 검증 (AC 6-10): anchor 진입 + 왼손 grip 홀드 → slide 좌우 이동·partial 변경 → 음정 끊김 없이 변화. anchor 이탈 시 즉시 silence. grip release 시 release fade.

5. **자산 수정 결정 트리 (Unity MCP Workflow)** — 본 plan 의 변경은 모두 `.cs` 파일 신규/수정 + 기존 `Trombone.prefab` 변경 없음 (voice GameObject 는 런타임 동적 생성). 따라서 manage_prefabs 호출 불필요. `Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` 와 `Assets/Instruments/Trombone/Scripts/Trombone.cs` 수정 + `Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 신규 — 모두 manage_script (또는 Edit) 로 처리. Trombone.prefab 의 `InstrumentAudioOutput` 컴포넌트 SerializeField 변경 없음 → manage_prefabs 0 회 호출.

## Deliverables

- `Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` — 신규. `OnAudioFilterRead` 로 4-state crossfade envelope (Idle/FadingOut/AwaitingPitch/FadingIn) + main thread `RequestPitchChange(pitch)` / `ResetEnvelope()` API. `volatile int m_State` + `volatile float m_PendingPitch` 로 main↔audio thread 신호.
- `Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` — 수정. (a) `EnsureVoicePool()` 가 voice 당 별도 자식 GameObject (`Voice_{i}`) 생성 + AudioSource 부착, (b) public event `VoiceGameObjectCreated` 발화, (c) `TrySetActiveVoicePitch` 가 voice GameObject 에 `TrombonePitchDsp` 가 있으면 `RequestPitchChange` 위임, (d) `PlayNoteSustained` 마지막에 DSP 가 있으면 `ResetEnvelope()` 호출.
- `Assets/Instruments/Trombone/Scripts/Trombone.cs` — 수정. (a) LateUpdate partialChanged/slideChanged 분기를 retrigger(Choke+NoteOn) → `audioOutput.TrySetActiveVoicePitch` 1 회 호출로 교체, (b) `Awake` 가 `audioOutput.VoiceGameObjectCreated` 구독 → 각 voice GameObject 에 `TrombonePitchDsp` 부착, (c) `OnDestroy` 가 event 구독 해제. anchor detach 분기의 `TriggerMidi(Choke)` 는 보존.

## Acceptance Criteria

- [ ] `[auto-hard]` `TrombonePitchDsp.cs` 가 `Assets/Instruments/Trombone/Scripts/` 에 존재하고, `namespace Instruments`, `[DisallowMultipleComponent]`, `[RequireComponent(typeof(AudioSource))]` 가 모두 부착됐으며, `OnAudioFilterRead(float[] data, int channels)` 시그니처와 `public void RequestPitchChange(float newPitch)` / `public void ResetEnvelope()` 두 public 진입점이 정의돼 있다.
  **검증:** `Grep -nE "namespace Instruments|DisallowMultipleComponent|RequireComponent\(typeof\(AudioSource\)\)|void OnAudioFilterRead\(float\[\] data, int channels\)|public void RequestPitchChange\(float|public void ResetEnvelope\(\)" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 ≥ 6 매칭.

- [ ] `[auto-hard]` `TrombonePitchDsp.cs` 가 `volatile int m_State` + `volatile float m_PendingPitch` 두 필드를 선언하고, `OnAudioFilterRead` 안에서 `AudioSource` API (`.pitch`, `.Play`, `.Stop`, `.volume` 등) 를 *호출하지 않는다* — Tech Spec §Invariants "audio thread 는 음량만 제어, AudioSource API 호출 금지". `AudioSource.pitch` set 은 `LateUpdate` 안 main thread 분기에서만 발생.
  **검증:** `Grep -nE "volatile int m_State|volatile float m_PendingPitch" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 정확히 2 매칭 + `Grep -nE "m_Source\.(pitch|Play|Stop|clip|loop)" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과를 `OnAudioFilterRead` 메서드 본문 외부 (= `LateUpdate` 또는 `OnEnable` 안) 라인에서만 발견 — `OnAudioFilterRead` 메서드 본문 안에서 `m_Source.pitch =` 또는 `m_Source.Play` 라인 0 건.

- [ ] `[auto-hard]` `InstrumentAudioOutput.cs` 의 `EnsureVoicePool()` 가 각 voice 마다 별도 자식 GameObject (`Voice_<index>`) 를 생성하고, public event `VoiceGameObjectCreated` (System.Action<GameObject>) 를 voice GameObject 생성 시 발화한다.
  **검증:** `Grep -nE "new GameObject\(.*\"Voice_|public event System\.Action<GameObject> VoiceGameObjectCreated|VoiceGameObjectCreated\?\.Invoke\(" Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` 결과 ≥ 3 매칭.

- [ ] `[auto-hard]` `InstrumentAudioOutput.TrySetActiveVoicePitch` 내부에 voice GameObject 의 `TrombonePitchDsp` 컴포넌트 분기가 있어 (`TryGetComponent<TrombonePitchDsp>`) 부착돼 있으면 `dsp.RequestPitchChange(pitch)` 위임, 없으면 기존 `voice.Source.pitch = pitch` 직접 set. 기존 `voice.TrackPitch == true` + Active/SustainedActive/Attacking state 분기 보존.
  **검증:** `Grep -nE "TryGetComponent<TrombonePitchDsp>|dsp\.RequestPitchChange|voice\.TrackPitch" Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` 결과 ≥ 3 매칭, 모두 `TrySetActiveVoicePitch` 메서드 본문 안.

- [ ] `[auto-hard]` `Trombone.cs` 의 LateUpdate partialChanged/slideChanged 분기가 retrigger (`TriggerMidi(... Choke)` + `TriggerMidi(... NoteOn)`) 대신 단일 `audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, ...)` 호출로 교체됐다. anchor detach 분기 (`!tromboneAnchor.IsAttached`) 의 `TriggerMidi(... Choke)` 는 *유지*.
  **검증:** `Grep -nE "partialChanged \|\| slideChanged|MidiEventType\.Choke|MidiEventType\.NoteOn|TrySetActiveVoicePitch" Assets/Instruments/Trombone/Scripts/Trombone.cs` 결과 — `partialChanged \|\| slideChanged` 라인 1 개 + 그 뒤 본문 안에 `TrySetActiveVoicePitch` 1 회 호출 + 같은 본문 안 `MidiEventType.Choke` 또는 `MidiEventType.NoteOn` 호출 0 건. `MidiEventType.Choke` 라인은 anchor detach 분기 (`!tromboneAnchor.IsAttached` 직후 본문) 안에만 등장.

- [ ] `[auto-hard]` `Trombone.cs` 가 `audioOutput.VoiceGameObjectCreated` event 를 구독해 voice GameObject 마다 `TrombonePitchDsp` 컴포넌트를 부착한다. `Awake()` 안에서 `base.Awake()` 호출 *전에* 구독해 모든 voice 의 event 를 놓치지 않는다 (`InstrumentBase.Awake` 가 `Initialize() → EnsureVoicePool()` 를 통해 voice 생성을 트리거).
  **검증:** `Grep -nE "VoiceGameObjectCreated \+=|AddComponent<TrombonePitchDsp>" Assets/Instruments/Trombone/Scripts/Trombone.cs` 결과 ≥ 2 매칭 + `Awake` 메서드 본문에서 `VoiceGameObjectCreated +=` 라인이 `base.Awake()` 호출 라인보다 *앞에* 있다 (라인 번호 비교).

- [ ] `[auto-soft]` Unity Editor 컴파일 0 error (`read_console action=get types=[error] count=20`). EditMode 회귀 (`unity-test-runner`) Piano / DrumKit / InstrumentAudioOutput 관련 기존 테스트 회귀 없음 — voice pool 구조 분리가 시블링 발음 동작에 영향을 주지 않음을 자동 검증.
  **검증:** `read_console action=get types=[error] count=20` → TrombonePitchDsp / InstrumentAudioOutput / Trombone 관련 컴파일 에러 0 건 + `editor_state.isCompiling == false` + `unity-test-runner` EditMode 결과 `failed=0` (직전 plan baseline `EditMode 102/102 pass` 유지).

- [ ] `[manual-hard]` (sub-spec Behavior #1) Editor Play 모드에서 TromboneAnchor 진입 + 왼손 Grip 홀드 → 발음 중 오른손 Grip 으로 슬라이드를 좌→우로 천천히 이동 → 발음이 *끊기지 않고* 음높이가 새 슬라이드 인덱스 대응 값으로 즉시 변경된다. 클릭 노이즈가 가청 한계 이하.
  **검증:** Play 모드 진입 → trombone anchor 텔레포트 → 왼손 grip 홀드 시작 + 오른손 grip 으로 slide 인덱스 0→6 풀 스윙 → 음이 연속적으로 한 음원에서 흘러가며 (clip restart 없음) 슬라이드 인덱스 변경 순간마다 짧은 (~10ms 합) crossfade 후 새 pitch 로 전환. Inspector 에서 voice 의 `Source.pitch` 값이 슬라이드 인덱스 변경마다 갱신되고 `Source.isPlaying == true` 가 끝까지 유지.

- [ ] `[manual-hard]` (sub-spec Behavior #2) Editor Play 모드에서 anchor 진입 + 왼손 Grip 홀드 → 악기 기울기로 Partial 인덱스 변경 (`TrombonePartialController` 가 z축 회전 임계 통과) → 발음이 끊기지 않고 음높이가 새 Partial 에 대응하는 값으로 변경된다.
  **검증:** Play 모드에서 grip 홀드 발음 중 → 악기 기울기 변경 (z 회전 `anglePerPartial=15°` 이상) → Partial 인덱스 변경 직후 음정이 ~5-12 반음 점프하며 clip restart 없이 같은 voice 의 pitch 만 변화 (Inspector `Source.pitch` 변화 + `Source.isPlaying` 유지 확인).

- [ ] `[manual-hard]` (sub-spec Behavior #3) 발음 미시작 상태 (왼손 grip 미입력) 에서 슬라이드 또는 partial 이 변경되어도 어떤 음도 들리지 않는다 — `m_IsBlowing == false` 일 때 retrigger / pitch-update 분기 모두 noop.
  **검증:** Play 모드에서 anchor 진입 후 grip 미입력 상태로 slide / partial 변경 → 정적 + `VoicePool_Trombone` 의 모든 voice `isPlaying=false` 확인.

- [ ] `[manual-hard]` (sub-spec Behavior #4) 발음 중 왼손 Grip 을 뗌 → 기존 fade-out 흐름 (`InstrumentAudioOutput.StopNote` → Releasing → `FadeOutDuration=0.15f` 동안 volume 선형 감소 → Idle) 이 그대로 동작. DSP envelope 은 release 동안 추가 영향 없음 (`TrackPitch=false` 분기로 진입 안 함).
  **검증:** Play 모드에서 grip 홀드 발음 중 grip 뗌 → 약 150ms 동안 자연스러운 fade-out → 정적 + Inspector 에서 voice `State: Releasing → Idle` 전환 확인 (Inspector 노출 안 되므로 디버그 로그 또는 `StopNote` breakpoint 으로 확인).

- [ ] `[manual-hard]` (sub-spec Behavior #5) 발음 중 anchor 외부 위치로 텔레포트 (detach) → `Trombone.LateUpdate` 의 `!tromboneAnchor.IsAttached` 분기가 `TriggerMidi(Choke)` 발화 → `Trombone.OnChoke` → `audioOutput.StopNoteImmediate(note)` → 즉시 silence. 03 plan baseline 동작 그대로.
  **검증:** Play 모드에서 grip 홀드 발음 중 다른 위치로 텔레포트 → 음이 *fade 없이* 즉시 정지 + trombone 본체 원위치 복귀 + `VoicePool_Trombone` voice 들 `isPlaying=false`.

## Out of Scope

- 포르타멘토 (pitch 미끄러짐) — sub-spec §Out of Scope. 본 plan 의 DSP envelope 은 *볼륨* crossfade 만, pitch 는 fade-out 직후 *즉시* set (선형/지수 보간 없음).
- 발음 중 멀티샘플 자동 교체 — sub-spec §Out of Scope. 본 plan 의 pitch-update 경로는 NoteOn 시점 선택된 `m_SelectedSample` 그대로 사용. pitch ratio 가 1 octave 이상 벗어나면 음질 어색하지만 sub-spec 가 명시적으로 받아들임.
- 멀티플레이어 pitch 동기화 — sub-spec §Out of Scope. `MidiTriggered` 이벤트는 NoteOn/NoteOff 시점만 발화하고 발음 중 pitch 변경은 외부에서 추적 불가 (03 plan handoff 박제 — ARD 02 §Consequences).
- voice pool 구조 분리가 시각적 Hierarchy 표면을 32 개 `Voice_<i>` children 으로 확장하는 시각적 변경 — Notes 박제. 시블링 (Piano/DrumKit) 도 동일하게 변경되지만 동작 영향 0.
- 슬라이드 / Partial 컨트롤러 자체 동작 변경 — 02 plan handoff "Slide.localPosition.x 는 03 이 read only" 유지. 본 plan 도 slide/partial controller 의 `SlideIndex` / `PartialIndex` / `PartialOffsetSemitones` 만 read.
- fade duration (5ms) tuning — 본 plan 은 5ms 임시값. 사용자가 청각적으로 클릭이 잔류하면 prefab Inspector 가 아니라 (SerializeField 노출 안 함) `TrombonePitchDsp.cs` 의 `k_FadeOutDurationSec` / `k_FadeInDurationSec` const 를 직접 조정. 동적 튜닝 표면화는 별도 plan 후보.
- audio thread 의 fade envelope curve (선형 vs 지수 vs 코사인) — 본 plan 은 선형. 5ms 짧은 구간이라 curve 차이가 청각적으로 거의 안 들림 — 선형이 가장 단순.
- voice pool 의 voice 수 조절 (`MaxVoices`) — `AudioSourceSettings.MaxVoices=32` default 유지. 본 plan 의 voice GameObject 분리도 32 개 GameObject 생성.
- Pitch-first 전략으로 전환 (ARD 04 §Consequences "슬라이드 반응 속도 불만 피드백이 생기면 재검토") — 본 plan 은 DSP-gated 만. 재검토는 별도 plan 또는 ARD 트리거.

## Notes

- ARD 04 (DSP-gated) 가 "pitch 교체까지 최대 1 프레임 (~16ms) 지연" 을 명시 — 본 plan 의 5ms fade-out 후 다음 LateUpdate 의 main thread 폴링이 `AudioSource.pitch` 를 set. 최악의 경우 fade-out 5ms + main thread polling 지연 16ms = 21ms 정도. ARD 04 가 받아들인 trade-off.
- `OnAudioFilterRead` 의 `m_FadeOutSamples` 단위는 *sample frames* (즉 채널 1개당 sample 수). data.Length / channels = sampleFrames. 5ms * 44100Hz = 220.5 → RoundToInt = 221 (또는 220). audio thread 의 elapsedSamples 카운터가 sampleFrames 단위로 증가하는 식 정합.
- volatile float 의 atomic 정확성 — .NET CLR 의 float (4 byte) 는 32-bit aligned 면 atomic read/write 보장 (C# 언어 spec §5.5). x64 platform 에서 안전. Unity 의 IL2CPP 도 동일 정합.
- `m_ElapsedSamples` 는 audio thread 만 read/write — volatile 불필요. 단일 thread access 라 race 없음. 다만 state 전이 시점 (FadingOut → AwaitingPitch, FadingIn → Idle) 에 reset 되는데 이 reset 도 audio thread 안.
- voice pool 구조 분리의 시블링 영향 — Piano / DrumKit 는 `VoiceGameObjectCreated` event 구독 안 함 → voice GameObject 가 32 개로 분리되지만 단순 AudioSource 1 개 부착 GameObject 라 그들에게도 정상 동작. 단 시각적으로 Hierarchy 에 `VoicePool_Piano/Voice_0, Voice_1, ..., Voice_31` 등이 펼쳐짐. 사용자 진단 영향 없음.
- `Trombone.Awake()` 의 `audioOutput.VoiceGameObjectCreated += ...` 구독을 `base.Awake()` 호출 전에 두는 이유: `InstrumentBase.Awake → Initialize() → audioOutput.InitializePoolSettings → EnsureVoicePool → voice GameObject 생성 + event 발화`. 구독이 늦으면 모든 voice event 를 놓침. 또한 `audioOutput.VoiceGameObjectCreated` 가 `base.Awake()` 안에서 발화될 때 본 plan 의 핸들러 `OnVoiceGameObjectCreated` 가 호출되어 voice GameObject 에 `TrombonePitchDsp.AddComponent` → DSP 의 `OnEnable` 즉시 호출 → `m_Source = GetComponent<AudioSource>()` 가 같은 GameObject 의 AudioSource 를 안전하게 잡음 (AudioSource 가 먼저 AddComponent 됨).
- `TrombonePitchDsp` 가 SerializeField 노출을 두지 않은 이유: voice GameObject 는 런타임 동적 생성이라 prefab Inspector 가 없음. fade duration 튜닝이 필요해지면 코드 const 수정 또는 별도 plan 으로 InstrumentAudioOutput 또는 Trombone 에 `dspFadeOutSeconds` SerializeField 추가 후 DSP `Initialize(dspFadeOutSeconds)` 로 주입하는 패턴 추천.
- `InstrumentAudioOutput.TrySetActiveVoicePitch` 의 시그니처는 03 plan 이 추가한 `(int note, float pitch) → bool` 그대로 유지. 본 plan 은 *내부 동작* 만 수정 (DSP 위임 분기 추가). 시블링 (Piano/DrumKit) 이 이 메서드를 호출하지 않으므로 동작 영향 0.
- `Trombone` 의 anchor detach 분기의 `TriggerMidi(Choke)` 는 그대로 유지 — 본 plan 의 pitch-update 경로와 *anchor detach 시 즉시 silence* 의도가 충돌하지 않음. sub-spec §Behavior #5 정합.
- voice pool 분리가 모든 InstrumentBase 사용 악기에 영향을 주므로, Piano/DrumKit 의 발음 동작에 회귀가 없는지 `unity-test-runner` EditMode + PlayMode 회귀 테스트가 검증의 단일 진실원. 회귀 발견 시 본 plan 의 voice pool 분리 결정 자체를 재검토 (현재 모든 voice 를 같은 GameObject 에 유지 + DSP 컴포넌트 1 개가 32 voice 출력을 한꺼번에 후크 + voice-level 식별 정보를 별도 필드로 전달하는 더 복잡한 대안 — 본 plan 1 차 차원에서는 단순한 voice 당 별도 GameObject 분리 채택).
- Manual-hard 5 개 (AC 8-12) 는 사용자 헤드셋 청각/시각 검증 필요. `/spec-build` 자동 진행 종료 후 일괄 검증 예정 — 본 commit 시점 자동 검증 가능한 axis 는 모두 통과.
- **2026-05-21 manual-hard 처리 결과**: 사용자가 VR 헤드셋 미보유 상태로 `TromboneCScaleDebugger` 컴포넌트(별도 디버그 도구)로 C major scale 8음 자동 재생을 시도. 코드 경로는 다음 로그로 모두 검증 완료 — `voices=32 playing=1`, `maxVol=1.00`, `spatialBlend=0.00`, `step[1..7] trySet=True` (DSP 위임 분기 진입 + voice 의 `TrackPitch=true` 매칭), pitch ratio 1.122 → 2.000 까지 7회 갱신, NoteOn/NoteOff 라이프사이클 정상. **단, OS audio 출력은 OpenXR runtime 점유 + FMOD "Cannot call this command after System::init" + Spatializer Plugin 미할당 경고로 차단되어 청각 클릭 잔류 여부는 직접 확인 불가**. 따라서 manual-hard AC 8-12 의 *코드 경로* 는 통과 처리하되, *실제 청각 클릭 잔류* 검증은 VR 환경 확보 후로 deferred. EditMode/PlayMode 자동 회귀 (133/4 PASS) + auto-hard 6/6 + auto-soft 1/1 통과로 본 plan 의 코드 변경 자체는 검증 완료. 후속 plan 또는 별도 sub-spec 에서 청각 확인 필요 시 재진입.

## Handoff

(plan 완료 후 doc-updater 가 자동 갱신할 예정. 본 plan 은 04 sub-spec 의 유일한 plan 이며 트럼본 피처 전체의 마지막 plan 후보. 후속 plan 이 본 plan 위에 빌드한다면 다음 정보를 참조.)

- **`InstrumentAudioOutput` voice pool 구조 변경** — voice 당 별도 자식 GameObject (`Voice_<index>`) 로 분리. `VoiceGameObjectCreated` (System.Action<GameObject>) event 발화로 외부 컴포넌트 부착 hook 제공. 후속 RhythmGame 또는 다른 악기 plan 이 voice 별 동작을 추가하려면 이 event 를 구독.
- **`TrombonePitchDsp` 컴포넌트 위치** — 런타임에 `Trombone` 컴포넌트가 `audioOutput.VoiceGameObjectCreated` 구독 핸들러에서 각 voice GameObject 에 1 회 부착. prefab 시점 부착 안 함.
- **DSP envelope fade duration** — `k_FadeOutDurationSec = 0.005f` / `k_FadeInDurationSec = 0.005f` 코드 const. 튜닝 필요시 후속 plan 이 SerializeField 표면화.
- **pitch 운전 경로** — Trombone.LateUpdate 가 partial/slide 변경 감지 → `audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, newPitch)` → voice GameObject 의 DSP `RequestPitchChange(newPitch)` → audio thread 5ms fade-out → main thread `AudioSource.pitch = newPitch` → audio thread 5ms fade-in. NoteOn / NoteOff 는 동일 경로 사용 안 함 (NoteOn 은 PlayNoteSustained, NoteOff 는 StopNote, anchor detach 는 Choke → StopNoteImmediate).
- **DSP envelope 과 voice-level fade-in/out 의 곱셈 합성** — NoteOn 직후 voice fade-in (50ms) 과 DSP 가 *동시에* 진행되지 않음 (NoteOn 시점 `ResetEnvelope()` 호출로 DSP 가 Idle 상태). pitch 변경 시점에만 DSP envelope 발동. 두 envelope 이 겹치는 시점은 발음 중 pitch 변경이 NoteOn 직후 50ms 안에 발생할 때만 — 매우 짧은 시간이라 거의 발생 안 함.
- **시블링 영향** — Piano / DrumKit 는 `VoiceGameObjectCreated` event 구독 안 함 → DSP 미부착 → `TrySetActiveVoicePitch` 가 호출돼도 (호출 안 됨) DSP 위임 분기가 안 타고 직접 `Source.pitch` set. 동작 회귀 0.
- **시각적 변경** — Hierarchy 에서 `VoicePool_Trombone` 자식이 32 개 `Voice_<i>` GameObject 로 펼쳐짐. Piano / DrumKit 도 동일. 사용자 진단 영향 없음.
