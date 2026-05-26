# Audio Click Suppression — 루프 경계 fade + Grip release DSP fade-out + 크로스페이드 중단 볼륨 점프 봉합

**Linked Spec:** [`05-audio-click-suppression.md`](../specs/05-audio-click-suppression.md)
**Status:** `Ready`

## Goal

sub-spec 04(DSP Pitch Crossfade) 이후 잔존하는 세 가지 가청 클릭 경로 — ① AudioSource.loop 경계 wrap, ② 왼손 Grip release 시 150ms voice-level fade-out으로는 못 막는 절단 클릭, ③ FadingIn 도중 RequestPitchChange가 들어와 `m_ElapsedSamples=0` reset 시 발생하는 볼륨 점프 — 을 `TrombonePitchDsp.cs` + `Trombone.cs` + `InstrumentAudioOutput.cs` 세 파일 수정만으로 봉합한다. ARD 05/06/07의 결정에 따라 (a) `OnAudioFilterRead` 안에서 `AudioSource.timeSamples` 폴링으로 wrap을 감지해 wrap 직후 `m_FadeInSamples` 동안 fade-in 적용, (b) `TrombonePitchDsp`에 state 4(`FadingOutToStop`) + state 5(`StopReady`)를 추가해 Grip release를 ≤10ms DSP fade-out으로 처리하고 완료 후 `InstrumentAudioOutput.StopVoice`로 voice를 회수, (c) `volatile float m_VolumeMultiplier`를 audio thread가 write / main thread가 read하도록 두고 `RequestPitchChange` 진입 시 `m_FadeOutFromVolume = m_VolumeMultiplier`를 main thread에서 캡처해 FadingOut을 현재 볼륨에서 이어가도록 만든다.

## Context

이 plan은 sub-spec 04의 plan handoff(`04-2026-05-21-sanyoentertain-dsp-pitch-crossfade.md` §Handoff)가 박제한 voice pool 분리 + voice별 `TrombonePitchDsp` 부착 구조 위에서 빌드한다. 04 plan의 4-state machine은 다음 시점에 클릭을 남긴다:

1. **루프 경계** — `AudioSource.loop=true` + clip 끝-시작 sample 비-zero-crossing → DSP가 Idle(0)이라 통과하는 동안 파형 불연속. 클립 자체를 zero-cross seamless로 편집하는 옵션은 sub-spec §Out of Scope("새 오디오 콘텐츠 제작") — DSP 레벨에서 봉합해야 함.
2. **Grip release** — 현재 `Trombone.LateUpdate`가 `TriggerMidi(... NoteOff)` → `InstrumentBase.OnNoteOff` → `audioOutput.StopNote(note)`로 voice의 `FadeOutDuration=0.15f`(150ms) release fade를 발동. 150ms 동안 voice는 *재생을 계속*하면서 `Source.volume`이 선형 감소하므로 사용자 입장에서 "Grip을 뗐는데도 소리가 매달려 있다"는 인지가 발생. sub-spec은 ≤10ms 짧은 fade-out을 요구.
3. **크로스페이드 중단** — `TrombonePitchDsp.RequestPitchChange`가 `state==3`(FadingIn) 검사에서 `m_ElapsedSamples=0` + `m_State=1`(FadingOut)로 즉시 전환. audio thread의 다음 FadingOut iteration에서 `t=0 → mult=1.0`에서 시작하므로, FadingIn 도중 (예: t=0.6일 때 volume=0.6) 갑자기 mult=1.0 → 0.4 점프가 발생 (0.6 → 1.0이 한 sample frame 안에 일어남). ARD 07이 "현재 볼륨에서 이어 FadeOut"을 결정.

세 경로 모두 *DSP 레벨에서* 봉합 가능하다. sub-spec의 What 3개와 ARD 05/06/07의 Decision이 모두 일치한다.

### 경로 ①: 루프 경계 wrap 감지 (ARD 05)

`AudioSource.timeSamples`는 main thread에서 안전하게 읽을 수 있고 audio playback의 *현재 sample 위치* (0 ~ clip.samples-1)를 반환한다 (loop=true면 wrap 시 0으로 점프). `LateUpdate`에서 매 프레임 폴링:

```
prev = m_PrevTimeSamples
curr = m_Source.timeSamples
if (curr < prev - clip.samples/2) → wrap 발생
  → volatile bool m_LoopWrapDetected = true 신호
  → m_LoopWrapCounter (audio thread만 read/write) reset 신호
m_PrevTimeSamples = curr
```

audio thread의 `OnAudioFilterRead`는 이 신호를 받으면 다음 sample frames 동안 fade-in 곱셈 multiplier를 적용. 단, 현재 4-state machine이 Idle(0)/FadingOut(1)/AwaitingPitch(2)/FadingIn(3) 4가지인데 *어느 상태에서도* loop wrap이 발생할 수 있다. 따라서 wrap fade는 *기존 state envelope과 곱셈으로 합성*한다 — 곱셈이므로 [0,1] 범위 유지. 다만 wrap 직후 multiplier가 [0,1]을 다시 곱하므로 FadingIn(3) 도중 wrap이 발생하면 두 fade-in이 곱해져 더 천천히 올라간다 — 음량 dip이지만 *점프는 없음* (sub-spec §Behavior #1의 "볼륨 불연속 없이").

wrap detection 임계로 `clip.samples/2`를 쓰는 이유는 pitch shift로 buffer 한 번에 큰 sample step이 일어날 수 있어 단순 `curr < prev`만으로는 부정확. clip 절반 이상을 한 frame에 건너뛸 가능성은 극히 낮다 (pitch=2.0이라도 60fps에서 한 frame=735 samples @ 44.1kHz → clip이 1초=44100 samples면 1.6%).

### 경로 ②: Grip release DSP fade-out (ARD 06)

기존 `Trombone.LateUpdate`의 grip release 라인 `TriggerMidi(new MidiEvent(baseToneMidiNote, 0f, MidiEventType.NoteOff))`을 신규 `audioOutput.RequestGripReleaseFadeOut(baseToneMidiNote)` 호출로 교체한다. 신규 `RequestGripReleaseFadeOut(int note)`은 해당 note의 voice를 찾아:

1. `voice.TrackPitch = false` (release 도중 slide pitch 추적 차단 — 기존 `StopNote`와 동일)
2. voice의 `TrombonePitchDsp.RequestFadeOut()` 호출 — DSP state 4(`FadingOutToStop`) 진입
3. voice.State를 신규 `VoiceState.FadingOutDsp`로 set — Update 루프가 voice의 DSP 상태를 폴링하면서 `DspState==5`(`StopReady`)일 때 `StopVoice(voice)` 호출

DSP state 4(`FadingOutToStop`)은 기존 FadingOut(1)과 envelope curve가 동일하지만 *완료 시 AwaitingPitch(2)가 아니라 StopReady(5)로 전이*한다. main thread `LateUpdate`의 폴링 분기에서 state==5이면 state=0(Idle) reset + `FadeOutCompleted` event 발화. `InstrumentAudioOutput.Update`가 voice.State==FadingOutDsp인 voice의 DSP를 매 프레임 폴링 → state==5 감지 → `StopVoice(voice)`.

**ARD 06이 명시한 "Choke와 RequestFadeOut는 동일 경로 공유하지 않는다"** 제약은 다음으로 만족: `OnChoke`/`StopNoteImmediate` 경로는 기존대로 즉시 `voice.Source.Stop` + `ResetVoice` (DSP에는 알리지 않음 — anchor 이탈 즉시 절단 의도). `RequestGripReleaseFadeOut`만 새 경로.

**ARD 06이 명시한 "fade 완료 후 voice를 안전하게 종료하는 콜백 또는 플래그 필요"** 제약은 voice.State==FadingOutDsp + DSP의 `bool IsStopReady => m_State==5` getter로 만족. 콜백(C# event) 대신 폴링이 단순 + audio thread → main thread 단방향 신호.

### 경로 ③: 크로스페이드 중단 볼륨 점프 (ARD 07)

`volatile float m_VolumeMultiplier`를 추가. audio thread가 `OnAudioFilterRead`에서 매 sample multiplier 적용 직후 `m_VolumeMultiplier = mult`로 박제. main thread는 read-only로 이 값을 읽어 `RequestPitchChange` 진입 시 `m_FadeOutFromVolume = m_VolumeMultiplier` 캡처:

```csharp
// RequestPitchChange (main thread):
m_PendingPitch = newPitch;
int s = m_State;
if (s == 0 || s == 3)  // Idle(=1.0) 또는 FadingIn(=현재 t)
{
    m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier;  // 현재 볼륨 캡처
    m_ElapsedSamples = 0;
    m_State = 1;  // FadingOut
}
// s == 1(FadingOut) / 2(AwaitingPitch): m_PendingPitch만 덮어씀, FadeOutFromVolume 유지
```

```csharp
// OnAudioFilterRead state==1 (FadingOut, audio thread):
float t = (float)m_ElapsedSamples / m_FadeOutSamples;
float mult = m_FadeOutFromVolume * (1f - t);  // 캡처한 시작 볼륨에서 0으로
for (int c = 0; c < channels; c++) data[i * channels + c] *= mult;
m_VolumeMultiplier = mult;  // 다음 RequestPitchChange가 캡처할 수 있도록 박제
m_ElapsedSamples++;
```

**FadingIn 도중 RequestPitchChange가 들어오면**: 현재 mult가 예컨대 0.6이고 캡처되면 `m_FadeOutFromVolume=0.6`. 다음 audio thread iteration의 첫 sample frame에서 `t=0 → mult=0.6*(1-0)=0.6` → 그 다음 sample에서 t 증가하며 mult 감소 → 점프 없음.

**이미 FadingOut(1) 도중 또 들어오면**: `m_PendingPitch`만 덮어씀, FadeOutFromVolume/ElapsedSamples 유지 → 현재 fade-out 그대로 계속, 새 pitch만 다음 AwaitingPitch에서 적용. 04 plan의 기존 동작과 동일.

**AwaitingPitch(2) 도중**: mult=0이라 `m_PendingPitch`만 덮어쓰는 게 정합. `m_VolumeMultiplier`는 0인 채.

**FadingIn(3) 도중**: 위 캡처 분기로 처리. 04 plan의 기존 `m_ElapsedSamples=0` reset이 *볼륨 점프*를 만든 원인 — 본 plan이 봉합.

`m_VolumeMultiplier`는 audio thread write / main thread read의 단방향 신호. .NET float (4 byte) 는 32-bit 정렬 atomic — `volatile` 키워드로 충분. 03 plan의 `volatile float m_PendingPitch`와 동일 패턴.

### `m_ElapsedSamples` thread 소유권 — 04 plan과의 차이

04 plan handoff에 박제된 사실: `m_ElapsedSamples`는 *audio thread 전용*이며 single-thread access. 본 plan이 main thread `RequestPitchChange` 안에서 `m_ElapsedSamples = 0`을 write하는 것은 04 plan 그대로 답습 — 그 시점에 audio thread는 *Idle(0) 또는 FadingIn(3)* 상태이며, FadingOut(1)/AwaitingPitch(2) 시점에는 main thread가 write 안 함 (s==0 || s==3 분기 안에서만). FadingIn(3) 시점 audio thread가 `m_ElapsedSamples++` 진행 중이지만 main thread가 동시에 `=0`을 쓰는 race는 *값이 어차피 0이 되는 의도된 reset*이라 결과적으로 안전. 04 plan이 이 race를 받아들였고 본 plan도 동일 패턴 유지.

본 plan의 신규 state 4(`FadingOutToStop`)에서도 audio thread만 `m_ElapsedSamples++` write. main thread `RequestFadeOut`은 `m_ElapsedSamples=0` set 1회 + state=4 set — Idle(0) 또는 다른 state 어느 시점에 호출되든 결과적으로 0으로 reset되는 의도된 동작.

### `Trombone.LateUpdate` 분기 — anchor detach vs grip release 분리

현재 `Trombone.LateUpdate`(`Trombone.cs:78-118`)는:

```
if (!anchor.IsAttached) { if (m_IsBlowing) Choke + m_IsBlowing=false; return; }
if (grip && !m_IsBlowing) { NoteOn; m_IsBlowing=true; ... }
else if (!grip && m_IsBlowing) { NoteOff; m_IsBlowing=false; }
if (m_IsBlowing && (partialChanged || slideChanged)) TrySetActiveVoicePitch(...);
```

본 plan 후:

```
if (!anchor.IsAttached) { if (m_IsBlowing) Choke + m_IsBlowing=false; return; }  // 유지
if (grip && !m_IsBlowing) { NoteOn; m_IsBlowing=true; ... }  // 유지
else if (!grip && m_IsBlowing) { audioOutput.RequestGripReleaseFadeOut(baseToneMidiNote); m_IsBlowing=false; }  // 교체
if (m_IsBlowing && (partialChanged || slideChanged)) TrySetActiveVoicePitch(...);  // 유지
```

anchor detach 분기의 `Choke`는 *그대로 유지* — sub-spec §Behavior #5 "anchor 이탈 시 기존 Choke 흐름 유지". `MidiEventType.NoteOff` 호출 0건 (Grip release가 DSP fade로 전환되므로).

`InstrumentBase.MidiTriggered` event 발화 측면: 기존 NoteOff 경로는 외부 구독자(RhythmGame 등)에게 `MidiEvent(note, 0f, NoteOff)`를 발행했다. 본 plan이 NoteOff 호출을 제거하므로 *외부 구독자가 NoteOff event를 받지 못한다*. sub-spec §Out of Scope에 "PlayMode 이외 주파수 연주(RhythmGame 연동)"가 명시돼 있고, 04 sub-spec의 invariant에서도 "MidiTriggered는 NoteOn/NoteOff만 발화"가 박제됐다. 본 plan은 NoteOff event 발화를 *직접* 발생시키지 않게 되는데, sub-spec §What 3개에 NoteOff event 발화 요구가 없고 §Behavior #4(앵커 이탈 Choke)도 별개 — **단, 외부 구독자가 발음 종료 시점을 알 필요가 있는 경우 본 plan은 그것을 깬다**. Notes에 박제 + Open Question으로 두지 않고 sub-spec §Out of Scope("RhythmGame 연동")의 보호 아래 진행.

### asmdef 영향

신규 코드는 모두 `Assets/Instruments/` 하위 — `Instruments.asmdef` 안에 자동 포함. references는 `Unity.InputSystem, Hands, Unity.XR.Interaction.Toolkit`이며 본 plan의 신규 코드는 `UnityEngine` (AudioSource, MonoBehaviour, AudioSettings) + `System` 만 사용 → 추가 reference 불필요. 04 plan과 동일 결론.

## Verified Structural Assumptions

- `TrombonePitchDsp.cs` 현재 4-state machine(`m_State`: 0=Idle / 1=FadingOut / 2=AwaitingPitch / 3=FadingIn)이 박제됨. `volatile int m_State`, `volatile float m_PendingPitch`, `int m_ElapsedSamples` (audio thread only), `m_FadeOutSamples` / `m_FadeInSamples` (OnEnable 박제). `OnEnable`이 `AudioSettings.outputSampleRate * 0.005f`를 RoundToInt로 박제. `LateUpdate`가 state==2(AwaitingPitch) 시 `m_Source.pitch = m_PendingPitch` + state=3 전환. `OnAudioFilterRead`가 state별 envelope 곱셈. `RequestPitchChange`가 state==0||state==3에서 `m_ElapsedSamples=0`+state=1, 그 외엔 m_PendingPitch만 덮어씀. `ResetEnvelope`이 state=0+elapsed=0. — 출처: `Read Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs (2026-05-21)`.
- `Trombone.cs` 현재 grip release 라인(`Trombone.cs:100-104`)이 `else if (!grip && m_IsBlowing) { TriggerMidi(new MidiEvent(baseToneMidiNote, 0f, MidiEventType.NoteOff)); m_IsBlowing = false; }`. anchor detach 분기(`Trombone.cs:80-88`)는 `TriggerMidi(... Choke)` 호출 후 return — `Trombone.OnChoke` override가 `audioOutput.StopNoteImmediate`로 즉시 silence. Partial/Slide 변경 분기(`Trombone.cs:107-117`)는 `audioOutput.TrySetActiveVoicePitch` 1회 호출. `Awake`가 `base.Awake()` 호출 전에 `audioOutput.VoiceGameObjectCreated += OnVoiceGameObjectCreated` 구독 + `OnVoiceGameObjectCreated`가 voice GameObject에 `TrombonePitchDsp.AddComponent`. — 출처: `Read Assets/Instruments/Trombone/Scripts/Trombone.cs (2026-05-21)`.
- `InstrumentAudioOutput.cs` 현재 `VoiceState { Idle, Attacking, Active, SustainedActive, Releasing }` 5-state. `StopNote(int note)`가 `voice.FadeOutDuration > 0` (Trombone은 0.15f) 일 때 `voice.State=Releasing` + `voice.ReleaseStartedAt=Time.time` + `voice.ReleaseStartVolume=voice.Source.volume` + `voice.TrackPitch=false`. `Update()`가 `voice.State==Releasing`일 때 `fadeOut=voice.FadeOutDuration||m_CurrentSettings.ReleaseDuration` 기준 선형 감소 후 `StopVoice`. `TrySetActiveVoicePitch`가 voice에 `TrombonePitchDsp`가 있으면 `dsp.RequestPitchChange(pitch)` 위임 (Active/SustainedActive/Attacking + TrackPitch=true만). `StopNoteImmediate(int note)`는 즉시 `StopVoice` (DSP 비통지). `PlayNoteSustained` 끝에 voice의 DSP가 있으면 `ResetEnvelope()` 호출. `EnsureVoicePool`이 voice당 별도 GameObject `Voice_<i>` + `VoiceGameObjectCreated?.Invoke(voiceGo)` 발화. `Voice` sealed class 필드: `Source` (AudioSource), `State`, `Note`, `StartedAt`, `ReleaseStartedAt`, `ReleaseStartVolume`, `TargetVolume`, `FadeInDuration`, `FadeOutDuration`, `TrackPitch`. — 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-21)`.
- `AudioSource.timeSamples` (Unity 표준 API)는 main thread에서 안전하게 read 가능, 현재 재생 sample 위치(0 ~ clip.samples-1)를 반환. `loop=true`에서 wrap 시 0으로 점프. `AudioSource.clip.samples`는 클립의 총 sample 수. 본 plan의 wrap 감지(`curr < prev - clip.samples/2`)가 의존. — 출처: Unity 공식 docs, 표준 API (planner 가정).
- `Instruments.asmdef` references = `["Unity.InputSystem", "Hands", "Unity.XR.Interaction.Toolkit"]`. 본 plan 신규 코드(`TrombonePitchDsp.cs` 수정, `InstrumentAudioOutput.cs` 수정, `Trombone.cs` 수정)는 `UnityEngine`만 import — 추가 reference 불필요. `Assets/Instruments/Trombone/Scripts/` 폴더에 `.asmdef` 없음 → 자동 `Instruments.asmdef` 포함. — 출처: `Read Assets/Instruments/Instruments.asmdef (2026-05-21)`.
- ARD 05/06/07의 Decision 박제: 05="OnAudioFilterRead wrap 감지 페이드", 06="TrombonePitchDsp.RequestFadeOut() 신규 추가" + "Choke와 RequestFadeOut 동일 경로 공유 금지", 07="현재 볼륨에서 이어 FadeOut". 본 plan Approach가 세 결정을 모두 답습. — 출처: `Read docs/specs/_archive/trombone/decisions/05-loop-boundary-fade.md, 06-grip-release-fadeout.md, 07-interrupted-crossfade.md (2026-05-21)`.
- Tech Spec §Invariants 박제: `AudioSource.pitch`는 main thread(LateUpdate)에서만 set, `OnAudioFilterRead`에서 GC 할당 금지(volatile 필드+sample 카운터만), 모든 fade ≤10ms, Choke 경로는 즉시 절단 유지, 발음 미시작 상태에선 오디오 처리 0. — 출처: `Read docs/specs/_archive/trombone/tech-specs/05-audio-click-suppression.md (2026-05-21)`.

## Approach

1. **`Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 수정 — state 4/5 추가 + 루프 wrap fade + m_VolumeMultiplier 박제**

   - 클래스 docstring 갱신: 6-state machine. `4 = FadingOutToStop` (volume 1→0 over fadeOutSamples, 완료 시 state=5), `5 = StopReady` (audio thread silent, main thread가 StopVoice 트리거 폴링용).
   - 신규 필드:
     ```csharp
     volatile float m_VolumeMultiplier = 1f;     // audio thread write, main thread read (RequestPitchChange가 캡처)
     volatile float m_FadeOutFromVolume = 1f;    // main thread write (RequestPitchChange 진입), audio thread read
     volatile bool m_LoopWrapSignal;             // main thread set true on wrap, audio thread read+clear
     // audio thread only:
     int m_LoopWrapFadeElapsedSamples = int.MaxValue;  // wrap fade 진행 sample 카운터. MaxValue = inactive.
     // main thread only:
     int m_PrevTimeSamples;
     ```
   - `OnEnable()`에 추가:
     ```csharp
     m_VolumeMultiplier = 1f;
     m_FadeOutFromVolume = 1f;
     m_LoopWrapSignal = false;
     m_LoopWrapFadeElapsedSamples = int.MaxValue;
     m_PrevTimeSamples = 0;
     ```
   - 신규 public API:
     ```csharp
     public void RequestFadeOut()  // main thread, Grip release용
     {
         int s = m_State;
         if (s == 4 || s == 5) return; // 이미 진행 중 또는 완료 대기
         m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier;  // 현재 볼륨 캡처
         m_ElapsedSamples = 0;
         m_State = 4;  // FadingOutToStop
     }
     public bool IsStopReady => m_State == 5;
     ```
   - `RequestPitchChange(float newPitch)` 수정 — `m_FadeOutFromVolume` 캡처 추가:
     ```csharp
     m_PendingPitch = newPitch;
     int s = m_State;
     if (s == 0 || s == 3)  // Idle 또는 FadingIn
     {
         m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier;  // 볼륨 점프 방지: 현재 mult 캡처
         m_ElapsedSamples = 0;
         m_State = 1;
     }
     // s == 1(FadingOut), s == 2(AwaitingPitch): m_PendingPitch만 덮어씀, FadeOutFromVolume 유지
     // s == 4/5는 RequestFadeOut 진행 중 — RequestPitchChange 무시(이미 grip release 흐름)
     ```
   - `LateUpdate()` 수정 — wrap 감지 + state==2 처리 + state==5 처리:
     ```csharp
     if (m_Source != null && m_Source.isPlaying && m_Source.loop && m_Source.clip != null)
     {
         int curr = m_Source.timeSamples;
         int prev = m_PrevTimeSamples;
         int half = m_Source.clip.samples / 2;
         if (curr < prev - half)  // wrap 발생
             m_LoopWrapSignal = true;
         m_PrevTimeSamples = curr;
     }
     if (m_State == 2)  // 기존 AwaitingPitch 분기 유지
     {
         if (m_Source != null) m_Source.pitch = m_PendingPitch;
         m_ElapsedSamples = 0;
         m_State = 3;
     }
     // state==5 (StopReady)는 InstrumentAudioOutput.Update가 IsStopReady 폴링해 StopVoice 호출.
     // DSP가 직접 state=0 reset 안 함 — StopVoice → ResetVoice → 다음 NoteOn의 ResetEnvelope() 가 처리.
     ```
   - `OnAudioFilterRead(float[] data, int channels)` 수정 — wrap fade 적용 + state 4 추가 + m_VolumeMultiplier 박제:
     ```csharp
     // ── wrap fade trigger ── (state 무관하게 wrap이 발생하면 fade-in 시작)
     if (m_LoopWrapSignal)
     {
         m_LoopWrapSignal = false;
         m_LoopWrapFadeElapsedSamples = 0;  // 활성화
     }

     int s = m_State;
     int sampleFrames = data.Length / channels;

     // ── state별 envelope multiplier 계산 + data 적용 ──
     for (int i = 0; i < sampleFrames; i++)
     {
         float mult;
         if (s == 0) mult = 1f;
         else if (s == 1)  // FadingOut: m_FadeOutFromVolume → 0
         {
             float t = (float)m_ElapsedSamples / m_FadeOutSamples;
             if (t >= 1f) mult = 0f;
             else mult = m_FadeOutFromVolume * (1f - t);
             m_ElapsedSamples++;
             if (m_ElapsedSamples >= m_FadeOutSamples)
             {
                 m_ElapsedSamples = 0;
                 m_State = 2;  // AwaitingPitch
                 s = 2;
             }
         }
         else if (s == 2) mult = 0f;
         else if (s == 3)  // FadingIn: 0 → 1
         {
             float t = (float)m_ElapsedSamples / m_FadeInSamples;
             mult = (t >= 1f) ? 1f : t;
             m_ElapsedSamples++;
             if (m_ElapsedSamples >= m_FadeInSamples)
             {
                 m_ElapsedSamples = 0;
                 m_State = 0;
                 s = 0;
             }
         }
         else if (s == 4)  // FadingOutToStop: m_FadeOutFromVolume → 0
         {
             float t = (float)m_ElapsedSamples / m_FadeOutSamples;
             if (t >= 1f) mult = 0f;
             else mult = m_FadeOutFromVolume * (1f - t);
             m_ElapsedSamples++;
             if (m_ElapsedSamples >= m_FadeOutSamples)
             {
                 m_ElapsedSamples = 0;
                 m_State = 5;  // StopReady — main thread 폴링 대기
                 s = 5;
             }
         }
         else mult = 0f;  // s == 5: silent until StopVoice

         // ── loop wrap fade-in 곱셈 합성 ──
         if (m_LoopWrapFadeElapsedSamples < m_FadeInSamples)
         {
             float wt = (float)m_LoopWrapFadeElapsedSamples / m_FadeInSamples;
             mult *= wt;
             m_LoopWrapFadeElapsedSamples++;
         }

         m_VolumeMultiplier = mult;  // main thread RequestPitchChange가 다음에 캡처할 값
         for (int c = 0; c < channels; c++) data[i * channels + c] *= mult;
     }
     ```
     **GC 할당 0**: data 배열은 in-place 수정, 신규 변수는 stack(float/int). Tech Spec §Invariants 정합.
   - 클래스 [DisallowMultipleComponent] / [RequireComponent(typeof(AudioSource))] / `namespace Instruments` 유지. SerializeField 없음(04 plan 유지).

2. **`Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` 수정 — VoiceState.FadingOutDsp + RequestGripReleaseFadeOut + Update 폴링**

   - `enum VoiceState`에 `FadingOutDsp` 항목 추가:
     ```csharp
     enum VoiceState { Idle, Attacking, Active, SustainedActive, Releasing, FadingOutDsp }
     ```
   - 신규 public method:
     ```csharp
     public void RequestGripReleaseFadeOut(int note)
     {
         Voice voice = GetOldestVoiceForNote(note);
         if (voice == null || voice.Source == null) return;
         if (voice.State == VoiceState.Idle || voice.State == VoiceState.Releasing || voice.State == VoiceState.FadingOutDsp) return;
         if (voice.Source.TryGetComponent<TrombonePitchDsp>(out var dsp))
         {
             dsp.RequestFadeOut();
             voice.State = VoiceState.FadingOutDsp;
             voice.TrackPitch = false;
         }
         else
         {
             // DSP 미부착 voice (시블링 호출 가정 없으나 안전망)
             StopNote(note);
         }
     }
     ```
   - `Update()`에 `FadingOutDsp` voice 폴링 분기 추가 (기존 Attacking/Releasing 분기 옆):
     ```csharp
     else if (voice.State == VoiceState.FadingOutDsp)
     {
         if (voice.Source.TryGetComponent<TrombonePitchDsp>(out var dsp))
         {
             if (dsp.IsStopReady)
                 StopVoice(voice);
         }
         else
         {
             StopVoice(voice);  // 안전망: DSP가 사라진 경우
         }
     }
     ```
   - `TrySetActiveVoicePitch` 의 state filter 갱신 — `FadingOutDsp` voice는 pitch 추적 차단(이미 release 중):
     ```csharp
     if (voice.State != VoiceState.Active && voice.State != VoiceState.SustainedActive && voice.State != VoiceState.Attacking) continue;
     // (FadingOutDsp / Releasing / Idle은 skip — 04 plan과 동일 패턴 + 신규 state 추가)
     ```
     기존 코드에 `FadingOutDsp`는 자동으로 skip됨 (3 state whitelist). 변경 불필요 — 확인용 주석만 추가.
   - `ResetVoice(Voice voice)`는 `voice.State = VoiceState.Idle` 그대로. `FadingOutDsp`도 `StopVoice → ResetVoice`로 회수.

3. **`Assets/Instruments/Trombone/Scripts/Trombone.cs` 수정 — Grip release 분기를 RequestGripReleaseFadeOut으로 교체**

   - `LateUpdate` 의 grip release 라인 교체:
     ```csharp
     else if (!grip && m_IsBlowing)
     {
         if (audioOutput != null)
             audioOutput.RequestGripReleaseFadeOut(baseToneMidiNote);
         m_IsBlowing = false;
     }
     ```
   - anchor detach 분기(`!tromboneAnchor.IsAttached`) 의 `TriggerMidi(... Choke)` 는 **유지** — sub-spec §Behavior #5.
   - Partial/Slide 변경 분기 유지 (04 plan 그대로).
   - Awake/OnDestroy의 VoiceGameObjectCreated 구독 유지 (04 plan 그대로).
   - `MidiEventType.NoteOff` 호출 라인이 사라지므로 외부 `MidiTriggered` 구독자가 Grip release 시점 NoteOff event를 더 이상 받지 못한다 — Notes 박제, sub-spec §Out of Scope "PlayMode 이외 주파수 연주" 의 보호 하에 진행.

4. **검증 단계** ([`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md) 따름)

   - 컴파일 대기 후 `read_console action=get types=[error] count=20` → 0 error.
   - EditMode 회귀: `unity-test-runner` 1 회 호출 — voice pool 동작 + InstrumentAudioOutput 사용 시블링 (Piano / DrumKit) 테스트 회귀 없음. 신규 `VoiceState.FadingOutDsp` 추가가 시블링 voice 라이프사이클에 영향 없음(시블링은 `RequestGripReleaseFadeOut` 호출 안 함, voice가 FadingOutDsp에 들어갈 경로 없음).
   - Editor Play 모드 시각·청각 검증 (manual-hard AC): anchor 진입 + grip 홀드 → 5초 이상 발음 유지(루프 wrap 발생) → 클릭 없음. grip release → ≤10ms fade-out 후 silence. 슬라이드 빠르게 좌우 흔들기(FadingIn 도중 RequestPitchChange 발생) → 볼륨 점프 없음.

5. **자산 수정 결정 트리 (Unity MCP Workflow)** — 본 plan의 변경은 모두 `.cs` 파일 수정만. `Trombone.prefab` 변경 없음 (voice GameObject + DSP는 04 plan 그대로 런타임 생성/부착). `manage_prefabs` / `manage_scene` 호출 0회. 컴파일 후 `read_console` + `unity-test-runner`만.

## Deliverables

- `Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` — 수정. (a) state 4(`FadingOutToStop`) + state 5(`StopReady`) 추가 → 6-state machine, (b) `volatile float m_VolumeMultiplier` (audio thread write/main thread read) + `volatile float m_FadeOutFromVolume` (main thread write/audio thread read) + `volatile bool m_LoopWrapSignal` 신규 필드, (c) `public void RequestFadeOut()` + `public bool IsStopReady` 신규 API, (d) `RequestPitchChange` 진입 시 `m_FadeOutFromVolume = m_VolumeMultiplier` 캡처(s==3 분기), (e) `LateUpdate` 가 `AudioSource.timeSamples` 폴링으로 wrap 감지 → `m_LoopWrapSignal=true`, (f) `OnAudioFilterRead` 가 state 4/5 처리 + wrap fade-in 곱셈 합성 + 매 sample 마다 `m_VolumeMultiplier` 박제.
- `Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` — 수정. (a) `enum VoiceState`에 `FadingOutDsp` 추가, (b) `public void RequestGripReleaseFadeOut(int note)` 신규 — DSP `RequestFadeOut()` 호출 + voice.State=FadingOutDsp + voice.TrackPitch=false, (c) `Update()` 가 voice.State==FadingOutDsp 인 voice 의 DSP.IsStopReady 폴링 → `StopVoice(voice)`.
- `Assets/Instruments/Trombone/Scripts/Trombone.cs` — 수정. `LateUpdate` 의 grip release 라인(`else if (!grip && m_IsBlowing)`) 의 `TriggerMidi(... NoteOff)` 호출을 `audioOutput.RequestGripReleaseFadeOut(baseToneMidiNote)` 호출로 교체. anchor detach 분기의 `TriggerMidi(... Choke)` 는 유지. Partial/Slide 변경 분기 + Awake/OnDestroy 구독 모두 유지.

## Acceptance Criteria

- [ ] `[auto-hard]` `TrombonePitchDsp.cs` 에 `volatile float m_VolumeMultiplier`, `volatile float m_FadeOutFromVolume`, `volatile bool m_LoopWrapSignal` 3개 필드가 선언돼 있고, `public void RequestFadeOut()` + `public bool IsStopReady` 2개 public 진입점이 추가됐다.
  **검증:** `Grep -nE "volatile float m_VolumeMultiplier|volatile float m_FadeOutFromVolume|volatile bool m_LoopWrapSignal|public void RequestFadeOut\(\)|public bool IsStopReady" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 ≥ 5 매칭.

- [ ] `[auto-hard]` `TrombonePitchDsp.OnAudioFilterRead` 가 state 4(`FadingOutToStop`) 처리 분기를 가지고 있고, 매 sample frame 마다 `m_VolumeMultiplier`를 박제하며(audio thread write), AudioSource API(`.pitch`/`.Play`/`.Stop`/`.clip`/`.loop`/`.volume`) 호출 0건이다. wrap fade는 `m_LoopWrapFadeElapsedSamples < m_FadeInSamples` 분기로 multiplier에 곱셈 합성.
  **검증:** `Grep -nE "m_State = 5|m_VolumeMultiplier = mult|m_LoopWrapFadeElapsedSamples" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 ≥ 3 매칭 + `Grep -nE "m_Source\.(pitch|Play|Stop|clip|loop|volume) *=" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과를 grep 후 line별로 확인해 모두 `LateUpdate` 메서드 본문 라인이며 `OnAudioFilterRead` 메서드 본문 안 라인 0건. (Grep 1차 수집 후 `awk`/육안으로 메서드 경계 확인.)

- [ ] `[auto-hard]` `TrombonePitchDsp.LateUpdate` 가 `AudioSource.timeSamples` + `AudioSource.clip.samples` 를 read 해 wrap을 감지(`curr < prev - half`) 후 `m_LoopWrapSignal = true` 박제한다. wrap 임계는 `clip.samples / 2`.
  **검증:** `Grep -nE "m_Source\.timeSamples|m_Source\.clip\.samples|m_LoopWrapSignal = true|m_PrevTimeSamples" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 ≥ 4 매칭, 모두 `LateUpdate` 메서드 본문 안.

- [ ] `[auto-hard]` `TrombonePitchDsp.RequestPitchChange` 가 s==3(FadingIn) 분기 진입 시 `m_FadeOutFromVolume = m_VolumeMultiplier` 캡처를 수행한다 (s==0 분기에서는 1f 캡처). s==1/s==2 분기에서는 캡처 안 함 — `m_PendingPitch` 만 덮어씀.
  **검증:** `Grep -nE "m_FadeOutFromVolume = " Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 — `RequestPitchChange` 메서드 본문 안 1 라인(`m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier;` 또는 동등) + `RequestFadeOut` 메서드 본문 안 1 라인 + `OnEnable` 초기화 1 라인 = 3 라인 (구체 라인 수는 구현에 따라 ±1 허용).

- [ ] `[auto-hard]` `InstrumentAudioOutput.cs` 의 `enum VoiceState` 에 `FadingOutDsp` 항목이 추가됐고, `public void RequestGripReleaseFadeOut(int note)` 메서드가 존재해 voice 의 `TrombonePitchDsp.RequestFadeOut()` 호출 + `voice.State = VoiceState.FadingOutDsp` + `voice.TrackPitch = false` 박제. `Update()` 에 `voice.State == VoiceState.FadingOutDsp` 분기가 있어 `dsp.IsStopReady` 폴링 후 `StopVoice(voice)` 호출.
  **검증:** `Grep -nE "FadingOutDsp|public void RequestGripReleaseFadeOut|dsp\.RequestFadeOut\(\)|dsp\.IsStopReady" Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` 결과 ≥ 5 매칭(enum 1 + state set 2회(RequestGripReleaseFadeOut 안 + Update 분기 조건) + 메서드 시그니처 1 + RequestFadeOut 호출 1 + IsStopReady 호출 1).

- [ ] `[auto-hard]` `Trombone.cs` 의 grip release 분기가 `TriggerMidi(... NoteOff)` 대신 `audioOutput.RequestGripReleaseFadeOut(baseToneMidiNote)` 호출로 교체됐다. anchor detach 분기의 `TriggerMidi(... Choke)` 는 유지. Partial/Slide 변경 분기의 `audioOutput.TrySetActiveVoicePitch` 유지.
  **검증:** `Grep -nE "MidiEventType\.NoteOff|RequestGripReleaseFadeOut|MidiEventType\.Choke|TrySetActiveVoicePitch" Assets/Instruments/Trombone/Scripts/Trombone.cs` 결과 — `MidiEventType.NoteOff` 라인 0건, `RequestGripReleaseFadeOut` 호출 1건(grip release `else if (!grip && m_IsBlowing)` 본문 안), `MidiEventType.Choke` 호출 1건(anchor detach `if (!tromboneAnchor.IsAttached)` 본문 안), `TrySetActiveVoicePitch` 호출 1건(Partial/Slide 변경 분기 본문 안).

- [ ] `[auto-soft]` Unity Editor 컴파일 0 error (`read_console action=get types=[error] count=20`). EditMode 회귀 (`unity-test-runner`) — Piano / DrumKit / InstrumentAudioOutput 시블링 테스트 회귀 없음 (`FadingOutDsp` 추가가 시블링 voice 라이프사이클에 영향 0 — 시블링은 `RequestGripReleaseFadeOut` 호출 안 함).
  **검증:** `read_console action=get types=[error] count=20` → TrombonePitchDsp / InstrumentAudioOutput / Trombone 관련 컴파일 에러 0 건 + `editor_state.isCompiling == false` + `unity-test-runner` EditMode 결과 `failed=0` (직전 plan baseline `EditMode 102/102 pass` 또는 04 plan 종료 시점 `133/4 PASS` 유지).

- [ ] `[manual-hard]` (sub-spec Behavior #1, ARD 05) Editor Play 모드에서 TromboneAnchor 진입 + 왼손 Grip 홀드 → 발음을 5초 이상(`Sound/A1.wav` 등 클립 길이 기준 최소 1회 이상의 루프 wrap 발생) 유지 → 가청 클릭이 들리지 않는다. 클릭이 있던 위치(클립 끝-시작 경계)가 부드럽게 연결된다.
  **검증:** Play 모드 진입 → trombone anchor 텔레포트 → 왼손 grip 5초 이상 홀드 → 청각으로 루프 경계 클릭 없음 확인 + Inspector 에서 voice 의 `Source.timeSamples` 가 wrap 시 0으로 점프하는 시점에 `TrombonePitchDsp.m_LoopWrapFadeElapsedSamples` 가 0부터 증가(디버그 로그 또는 inspector watch)로 wrap fade 작동 박제.

- [ ] `[manual-hard]` (sub-spec Behavior #2, ARD 06) Editor Play 모드에서 anchor 진입 + 왼손 Grip 홀드 → 발음 중 Grip 릴리즈 → 짧은 fade-out(≤10ms, 청각상 거의 즉시 — 5ms DSP fade) 후 silence. 절단 클릭이 없고, 기존 150ms voice-level fade-out("매달림") 도 없음.
  **검증:** Play 모드에서 grip 홀드 발음 중 grip 뗌 → 청각으로 짧고 깔끔한 종료 확인 + Inspector 또는 디버그 로그에서 voice 의 `State` 가 `FadingOutDsp → Idle` 로 ~5ms 안에 전이 + `Source.isPlaying` 이 즉시 false (5-10ms 내).

- [ ] `[manual-hard]` (sub-spec Behavior #3, ARD 07) Editor Play 모드에서 grip 홀드 발음 중 → 오른손 Grip + 슬라이드를 빠르게 좌우로 흔들기(FadingIn 도중 새 RequestPitchChange 가 들어오도록 의도적으로 빠른 변화) → 볼륨 점프 없이 부드럽게 새 피치로 전환.
  **검증:** Play 모드에서 grip 홀드 + 슬라이드 빠른 흔들기 → 청각으로 볼륨 점프(클릭) 없음 + 디버그 로그에서 `RequestPitchChange` 진입 시 `m_FadeOutFromVolume` 값이 `m_VolumeMultiplier` 현재 값(0~1 사이 임의값) 으로 캡처되는지 확인.

- [ ] `[manual-hard]` (sub-spec Behavior #4) 발음 미시작 상태(왼손 grip 미입력) 에서 슬라이드 / partial 변경 / 가만히 두기 → 어떤 오디오 처리도 발생하지 않는다(VoicePool_Trombone 모든 voice `isPlaying=false`).
  **검증:** Play 모드에서 anchor 진입 후 grip 미입력 상태로 slide / partial 변경 + 1분 대기 → 정적 + Inspector 에서 `VoicePool_Trombone` 의 모든 voice `Source.isPlaying=false` + 모든 voice 의 `State==Idle` + `TrombonePitchDsp.m_State==0`.

- [ ] `[manual-hard]` (sub-spec Behavior #5) 발음 중 anchor 외부 위치로 텔레포트(detach) → 기존 Choke 흐름 그대로 즉시 silence. fade-out 없음 (5ms DSP fade 도 적용 안 됨 — Choke 경로는 `RequestFadeOut` 안 호출, `StopNoteImmediate` 사용).
  **검증:** Play 모드에서 grip 홀드 발음 중 다른 위치로 텔레포트 → 음이 fade 없이 즉시 정지 + trombone 본체 원위치 복귀 + `VoicePool_Trombone` voice 들 `isPlaying=false` + voice.State==Idle (FadingOutDsp 경유 안 함).

## Out of Scope

- 포르타멘토(pitch 미끄러짐) — sub-spec §Out of Scope. 본 plan의 DSP envelope은 *볼륨* fade만, pitch는 main thread polling에서 *즉시* set.
- 멀티플레이어 pitch 동기화 — sub-spec §Out of Scope. NoteOff event 발화가 사라지므로 외부 구독자가 Grip release 시점을 못 받지만 sub-spec이 보호.
- 새 오디오 콘텐츠 제작/클립 편집 (zero-cross seamless loop) — sub-spec §Out of Scope. DSP wrap fade로 봉합.
- PlayMode 이외 주파수 연주(RhythmGame 연동) — sub-spec §Out of Scope. NoteOff event 발화 사라짐을 받아들임.
- fade duration tuning — 본 plan은 기존 04 plan의 5ms 상수(`k_FadeOutDurationSec` / `k_FadeInDurationSec`) 유지. Grip release fade도 동일 5ms 적용 (Tech Spec §Invariants "≤10ms" 만족).
- wrap fade curve(선형 vs 코사인 등) — 본 plan은 선형. 5ms 짧은 구간이라 청각 차이 거의 없음.
- voice pool 의 voice 수 조절 — 04 plan의 32 voice 유지.
- 시블링(Piano/DrumKit) 의 `RequestGripReleaseFadeOut` 사용 — 본 plan은 Trombone 전용. 시블링이 호출하면 DSP 미부착 voice라 `else` 분기로 `StopNote` fallback (안전망).
- `m_State==5`(StopReady) 진입 후 main thread 폴링 latency — Update 루프 1 프레임(~16ms@60fps) 안에 `StopVoice` 호출됨. fade가 끝난 voice 가 1 프레임 silent 유지(`OnAudioFilterRead` s==5 분기에서 mult=0) 후 회수 — 사용자 인지 없음.
- Choke 경로의 click 잔류 — sub-spec §Behavior #5가 "기존 Choke 흐름 유지"로 명시. Choke 시 클릭이 들리면 별도 sub-spec 후보.

## Notes

- **외부 구독자 NoteOff event 사라짐**: 기존 `Trombone.LateUpdate`의 grip release 라인이 `TriggerMidi(... NoteOff)` → `InstrumentBase.MidiTriggered?.Invoke(...)` 으로 외부 구독자에게 NoteOff event를 발행했다. 본 plan은 NoteOff 호출을 제거하므로 외부 구독자(RhythmGame 등)가 Grip release 시점을 알 수 없게 된다. sub-spec §Out of Scope("PlayMode 이외 주파수 연주(RhythmGame 연동)") 의 보호 아래 진행. RhythmGame 통합 시점에 별도 plan이 필요하면 `RequestGripReleaseFadeOut` 안에서 `MidiTriggered?.Invoke(new MidiEvent(note, 0f, NoteOff))` 발행 라인 추가 패턴 추천.
- **m_State==5(StopReady) 진입 후 audio thread 동작**: `OnAudioFilterRead` 의 `else mult = 0f` (s==5 분기) 가 voice 출력을 silent로 유지. 1 프레임 뒤 `InstrumentAudioOutput.Update` 폴링이 `StopVoice` 호출 → `voice.Source.Stop()` + `voice.Source.clip = null` + `ResetVoice` → 다음 NoteOn에서 같은 voice 재사용 시 `PlayNoteSustained` 끝의 `ResetEnvelope()` 호출이 `m_State=0` reset. 즉 state 5는 *임시 silent 대기 상태* 이며 voice 회수 후엔 자연스럽게 Idle로 복귀.
- **state 4와 state 1의 차이**: 둘 다 envelope curve 동일(`m_FadeOutFromVolume * (1-t)`) 이지만 완료 시 전이 대상이 다름. state 1 → state 2(AwaitingPitch, pitch 교체 대기). state 4 → state 5(StopReady, voice 회수 대기). 두 state를 코드 중복으로 분리한 이유는 다음 전이 분기가 깔끔 + `RequestFadeOut`이 state 1을 덮어쓸 가능성 차단(이미 pitch crossfade 중이면 grip release를 무시하는 게 sub-spec §Behavior #2와 충돌하지만, RequestFadeOut 진입에서 `s == 4 || s == 5` 만 early return하고 state 1/2/3은 새 fade-out으로 덮어씀 — 즉 grip release가 항상 우선).
- **wrap fade와 state envelope 곱셈 합성**: wrap이 FadingIn(3) 도중 발생하면 두 fade-in이 곱해져 천천히 올라간다. 청각상 음량 dip이지만 *볼륨 점프 없음* — sub-spec §Behavior #1의 "볼륨 불연속 없이"가 점프 없음을 의미. wrap이 FadingOut(1)/FadingOutToStop(4) 도중 발생해도 fade-in 곱셈이 더 작은 값을 만들어 mult를 더 빨리 0에 가깝게 만들 뿐 점프 없음.
- **m_FadeOutFromVolume 캡처 시점의 race**: main thread RequestPitchChange가 `m_FadeOutFromVolume = m_VolumeMultiplier` 캡처할 때 audio thread는 `m_VolumeMultiplier`를 계속 write 중. main이 캡처한 값은 audio thread의 *직전* sample frame 의 mult — 1 sample frame(~22µs @ 44.1kHz)의 차이라 청각상 무의미. volatile float atomic 보장으로 partial read 없음.
- **fade duration 5ms는 sub-spec §Invariants "≤10ms" 안**: Grip release fade 5ms, loop wrap fade 5ms (fadeIn 상수 재사용), pitch crossfade fade 5ms. 합쳐서 ≤10ms 한계 안.
- **state 5 진입 후 LateUpdate가 state=0 reset 안 함**: 원래 04 plan 패턴(state 2 → 3 전이는 LateUpdate가 처리)을 따른다면 state 5 → 0 전이도 LateUpdate에서 할 수 있지만, 본 plan은 그렇게 안 한다. 이유: state 5의 의도가 "voice 회수 대기"이므로 `StopVoice`가 호출되기 전까지 silent 유지가 필요. `StopVoice → ResetVoice` 후 다음 NoteOn의 `PlayNoteSustained → ResetEnvelope()` 호출이 state=0 reset을 담당.
- **Trombone.OnDestroy 의 unsubscribe 처리**: 04 plan이 박제. 본 plan은 변경 없음.
- **시블링 voice 영향**: Piano / DrumKit voice는 `TrombonePitchDsp` 미부착. 시블링이 `RequestGripReleaseFadeOut` 을 호출할 가능성 없음(Trombone만 호출) — `else` fallback(`StopNote`) 발화하지 않음. `Update()` 의 `FadingOutDsp` 분기도 시블링 voice 는 그 state로 들어가지 않으므로 통과. 시블링 voice 라이프사이클 영향 0.
- **Awake 의 구독 타이밍**: 04 plan 박제. base.Awake() 전에 audioOutput 잡고 event 구독. 본 plan은 변경 없음.
- **후속 plan**: manual-hard AC8(루프 경계) / AC9(Grip 릴리즈) 두 항목이 본 plan 적용 후에도 가청 클릭으로 실패. retry plan [`06-2026-05-21-sanyoentertain-audio-click-suppression-retry.md`](./06-2026-05-21-sanyoentertain-audio-click-suppression-retry.md) 이 (i) wrap fade 설계를 pre-boundary fade-OUT + post-wrap fade-IN(첫 sample wt=1/N) 으로 교체해 AC8 해결, (ii) `RequestFadeOut` / `RequestPitchChange` write 순서에 `Thread.MemoryBarrier()` 추가 + AC8 수정의 부수 효과로 AC9 가설 A(loop wrap 과 grip release 인지 혼동) 해결.

## Handoff

(plan 완료 후 doc-updater 가 자동 갱신. 본 plan은 05 sub-spec 의 유일한 plan 이며 트럼본 피처 전체의 마지막 plan 후보.)

- **`TrombonePitchDsp` 6-state machine**: 0=Idle / 1=FadingOut / 2=AwaitingPitch / 3=FadingIn / 4=FadingOutToStop / 5=StopReady. 신규 4/5는 Grip release 전용. 다른 악기/후속 plan 이 비슷한 voice-level fade-out-to-stop 패턴을 쓸 때 동일 state 추가.
- **`InstrumentAudioOutput.VoiceState.FadingOutDsp` 추가**: DSP가 voice 종료 신호를 main thread 폴링으로 전달하는 패턴. `RequestGripReleaseFadeOut(int note)` + `Update()` 의 `dsp.IsStopReady` 폴링 + `StopVoice`. 시블링은 호출 안 하므로 동작 영향 0.
- **`m_VolumeMultiplier` audio→main 단방향 박제 패턴**: audio thread가 매 sample 마다 write, main thread가 RequestPitchChange 진입 시 read. 후속 plan 이 fade 진행 중 외부 모니터링이나 다른 envelope 합성을 추가할 때 동일 패턴 활용 가능.
- **루프 wrap 감지 (`AudioSource.timeSamples` 폴링)**: `LateUpdate` 매 프레임 `curr < prev - clip.samples/2` 판정. wrap 임계 절반은 pitch shift 큰 step 안전 마진. 후속 plan 이 wrap-aware 처리(예: wrap 시 자동 클립 전환)를 추가하려면 `m_LoopWrapSignal` 신호 구독 패턴 활용.
- **NoteOff event 발화 소실**: Grip release 가 NoteOff 호출 안 함 → 외부 `MidiTriggered` 구독자가 NoteOff event 안 받음. RhythmGame 통합 시 `RequestGripReleaseFadeOut` 안에서 별도 event 발행 추가 필요.
- **anchor detach Choke 경로 유지**: 즉시 silence, fade 없음. DSP의 `RequestFadeOut` 미사용. ARD 06 분리 정합.
- **Fade duration**: 모든 fade(루프 wrap fade-in, Grip release fade-out, pitch crossfade fade-in/out) 5ms 통일. `k_FadeOutDurationSec` / `k_FadeInDurationSec` const. 튜닝 필요시 SerializeField 표면화 별도 plan 후보.
