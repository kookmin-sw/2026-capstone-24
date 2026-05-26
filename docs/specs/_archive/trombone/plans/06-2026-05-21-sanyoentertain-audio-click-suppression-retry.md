# Audio Click Suppression Retry — loop seam pre-fade + post-wrap fade-in 첫 샘플 wt=0 버그 제거 + Grip release race 봉합

**Linked Spec:** [`05-audio-click-suppression.md`](../specs/05-audio-click-suppression.md)
**Caused By:** [`05-2026-05-21-sanyoentertain-audio-click-suppression.md`](./05-2026-05-21-sanyoentertain-audio-click-suppression.md)
**Status:** `Ready`

## Goal

선행 plan 05 적용 후에도 manual-hard 검증에서 잔존한 두 가청 클릭 경로 — (i) AC8 루프 경계(5초 이상 발음 유지 시 ≥1회 loop wrap에서 클릭), (ii) AC9 왼손 Grip 릴리즈 직후 클릭 — 을 `TrombonePitchDsp.cs` 1 파일 수정만으로 봉합한다. AC8은 (a) 래핑 *전* fade-OUT 부재 + (b) 래핑 *후* fade-IN 첫 샘플 `wt = 0 / fadeInSamples = 0 → mult *= 0` 으로 인한 즉각 무음 점프 두 결함을 갖는 현재 wrap fade 설계를 `pre-boundary fade-OUT + post-wrap fade-IN(첫 샘플 wt=1/N)` 으로 교체해 해결한다. AC9는 가설A(loop wrap과 grip release 인지 혼동)를 AC8 수정으로 자동 해결, 가설B(`m_ElapsedSamples = 0` race로 인한 mult 점프)를 `RequestFadeOut` 진입 절차를 `m_FadeOutFromVolume` 캡처를 먼저 박제 → `m_ElapsedSamples = 0` 박제 → 마지막에 `m_State = 4` 박제 순서로 재배열 + `m_State` write 전까지 audio thread가 새 fade 분기를 보지 못하게 하는 release semantics 강제로 봉합한다.

## Context

### Caused By 인용 — 선행 plan 05 실패 증거

선행 plan `05-2026-05-21-sanyoentertain-audio-click-suppression.md` 가 6-state machine + `RequestGripReleaseFadeOut` + loop wrap fade 를 모두 구현·커밋했고 그 외 모든 AC(컴파일·grep·시블링 회귀 등 auto-hard 7건)는 통과했으나 manual-hard 5건 중 다음 2건이 실패했다:

> **AC8 (sub-spec Behavior #1, ARD 05) 실패** — 5초 이상 음 유지 시(클립 길이 0.5~2초 기준 ≥1회 loop wrap) 여전히 가청 클릭. wrap fade 가 *오히려* 첫 sample 의 `mult *= 0` 으로 추가 클릭을 만들고, 래핑 전 파형 불연속도 그대로 통과.
>
> **AC9 (sub-spec Behavior #2, ARD 06) 실패** — 왼손 Grip 릴리즈 시 여전히 가청 노이즈. 가설 A: 사용자가 grip release 테스트 중 loop wrap 가 겹쳐 AC8 의 wt=0 클릭을 grip release 클릭으로 인지. 가설 B: `RequestFadeOut()` 의 main thread write 가 `m_FadeOutFromVolume = m_VolumeMultiplier` → `m_ElapsedSamples = 0` → `m_State = 4` 3 write 사이에 audio thread 가 새 m_State(4) 만 본 채 옛 m_ElapsedSamples 잔여값을 사용하거나 그 반대 race 발생 → mult 점프.

선행 plan handoff (`§Handoff`) 가 박제한 6-state machine·`m_VolumeMultiplier` audio→main 단방향 박제 패턴·`VoiceState.FadingOutDsp` polling 패턴은 **유지**. 본 retry 는 그 위에서 wrap fade 수식과 `RequestFadeOut` write 순서만 수정.

### AC8 root cause 분석 — 현재 wrap fade 두 결함

현재 `TrombonePitchDsp.cs` (선행 plan 적용 후) 의 wrap fade 코드:

```csharp
// LateUpdate (main thread, 매 프레임):
if (m_Source != null && m_Source.isPlaying && m_Source.loop && m_Source.clip != null)
{
    int curr = m_Source.timeSamples;
    int half = m_Source.clip.samples / 2;
    if (curr < m_PrevTimeSamples - half) // wrap 발생
        m_LoopWrapSignal = true;
    m_PrevTimeSamples = curr;
}

// OnAudioFilterRead (audio thread):
if (m_LoopWrapSignal) {
    m_LoopWrapSignal = false;
    m_LoopWrapFadeElapsedSamples = 0;  // fade-IN 활성화 (post-wrap)
}
// per sample:
if (m_LoopWrapFadeElapsedSamples < m_FadeInSamples) {
    float wt = (float)m_LoopWrapFadeElapsedSamples / m_FadeInSamples;
    mult *= wt;     // ⚠️ 첫 샘플: wt = 0 / 220 = 0 → mult *= 0 → 즉각 무음
    m_LoopWrapFadeElapsedSamples++;
}
```

두 가지 결함:

1. **래핑 전 fade-OUT 0건**. `AudioSource.loop = true` + clip 의 끝 sample 과 시작 sample 이 non-zero-crossing 인 채로 wrap 이 발생하면 그 *불연속점* 자체가 DAC 출력에서 high-frequency 폭이 큰 spectral content 가 되어 클릭으로 들린다. DSP 는 main thread `LateUpdate` 가 wrap 을 *감지* 한 다음 프레임의 `OnAudioFilterRead` 에서 fade-in 을 시작하므로 *불연속점은 이미 DAC 를 통과한 뒤*. 후처리 fade-in 으로는 이미 발생한 클릭을 제거 못 함.

2. **post-wrap fade-IN 첫 샘플이 `wt = 0`**. wrap 직후 첫 sample frame 에서 `m_LoopWrapFadeElapsedSamples = 0` → `wt = 0 / m_FadeInSamples = 0` → `mult *= 0` → 출력 sample 이 0. 그 직전 sample frame 은 `state==0` 이라 `mult = 1`(또는 m_VolumeMultiplier 1.0 부근) → 출력이 정상. **1 → 0 → 1/N → 2/N → … 의 갑작스러운 1 → 0 점프 자체가 새로운 클릭**. wrap fade 가 클릭을 만든다.

올바른 설계:

- **pre-boundary fade-OUT** — `LateUpdate` 가 wrap *임박* (`clip.samples - timeSamples < approachThreshold`) 을 감지 → `m_LoopApproachSignal = true` → audio thread 가 다음 sample frame 부터 `m_LoopSeamFadeOutElapsedSamples = 0` 시작 → multiplier 가 1 → 0 으로 fadeOutSamples 동안 감소. 이때 mult 가 *0 부근* 에서 wrap 이 발생하므로 파형 불연속이 *무음 구간* 에 박혀 클릭 가청 한계 이하.
- **post-wrap fade-IN** — wrap 발생 (`curr < prev - half`) 감지 → `m_LoopWrapSignal = true` → audio thread 가 다음 sample frame 부터 `m_LoopSeamFadeInElapsedSamples = 0` 시작 → multiplier 가 0 → 1 로 fadeInSamples 동안 증가. **첫 샘플 wt = 1 / N (대략 0.0045 @ 5ms@44.1kHz 기준 220 samples → 1/220 ≈ 0.00455)** 으로 시작해 0 점프 없이 자연스럽게 진입. (`wt = elapsed / N` 이 아니라 `wt = (elapsed + 1) / N` 으로 박제하거나, 동등하게 `elapsed++` 를 곱셈 *전* 에 두면 같은 효과.)
- **approachThreshold 산정** — `m_FadeOutSamples` (= ~220 samples @ 5ms/44.1kHz) 만큼은 *반드시* fade-out 이 완료될 시간이 필요 + LateUpdate 가 60fps frame jitter (≈ 735 samples @ 44.1kHz, 한 frame 분) 을 흡수할 수 있어야 함. 보수적으로 `approachThreshold = m_FadeOutSamples + 2 * (sampleRate / 60)` ≈ 220 + 1470 = 1690 samples (≈38ms @ 44.1kHz). pitch shift 가 큰 경우 (pitch=2.0 → 한 frame 에 1470 samples 가 아니라 2940 samples 진행) 도 대비해 `2 * sampleRate / 60` 을 frame jitter margin 으로 둠.

### AC9 root cause 분석 — 두 가설

**가설 A** (선행 plan 미가설): 사용자가 grip release 테스트 중 클립 0.5~2 초 길이 기준 wrap 이 매우 자주 발생. wrap fade 가 만드는 AC8 클릭(1 → 0 → 1/N 점프)을 grip release 클릭으로 오인. AC8 수정이 자동 해결.

**가설 B**: `RequestFadeOut()` 의 main thread write 순서:

```csharp
public void RequestFadeOut()
{
    int s = m_State;
    if (s == 4 || s == 5) return;
    m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier; // write 1
    m_ElapsedSamples = 0;                                      // write 2
    m_State = 4;                                               // write 3 — 새 분기 active
}
```

선행 plan 04 의 race 주장 ("값이 어차피 0 이 되는 의도된 reset 이라 결과적으로 안전") 은 *FadingIn(3) 도중 RequestPitchChange* 에 한해 성립한다 — write 가 모두 `m_ElapsedSamples = 0` + `m_State = 1` 두 줄이고, audio thread 가 어느 write 를 먼저 보든 state==3 → 1 전이가 *fade 처음부터 다시* 가 의도이므로 elapsed=0 reset 도 의도. 그러나 본 plan 의 `RequestFadeOut` 은 **세 write** 이고 `m_FadeOutFromVolume` 이 *현재 mult 의 캡처값* 이므로 의미가 다르다:

- audio thread 가 write 3(`m_State = 4`) 을 먼저 본 sample frame 에서 옛 `m_FadeOutFromVolume`(write 1 이전 값, 예 = 1f from OnEnable) + 옛 `m_ElapsedSamples` (write 2 이전 값, 예 = 50 from 진행 중 FadingIn) 를 사용하면 `mult = 1f * (1 - 50/220) = 0.77`. 그 직전 sample 의 mult 가 예컨대 0.6 (FadingIn 진행 중 mult)였다면 0.6 → 0.77 점프(+) 발생.
- audio thread 가 write 1, 2 를 본 채 write 3 을 못 본 sample frame 에서는 여전히 state==3(FadingIn) 분기를 실행. 옛 `m_ElapsedSamples = 0` reset 이 audio thread 의 FadingIn 진행을 처음부터 다시 시작시킴 → mult 가 갑자기 0 으로 떨어짐 → 0.6 → 0 점프(-) 발생.

.NET CLR 의 일반 store 는 *strong release semantics* 가 보장되지 않으며 store 순서가 컴파일러/CPU reordering 으로 뒤바뀔 수 있다. `volatile` 키워드는 *각 access 의 acquire/release semantics* 는 부여하나, *서로 다른 volatile 필드 간 write 순서* 는 보장하지 않는다 (C# spec §15.5.4 / CLR memory model). 즉 audio thread 가 (m_State = 4, m_ElapsedSamples = old, m_FadeOutFromVolume = old) 조합을 잠시 볼 수 있다.

봉합:
1. write 순서를 명시적으로 박제: capture → elapsed reset → **state 마지막**. C# `volatile` 의 release semantics 는 *후속 write 에 대한* 순서 보장이 약하지만, audio thread 의 분기 진입 키가 `m_State` 1 개이므로 m_State 를 마지막에 박제하면 audio thread 가 새 분기를 보는 시점에 다른 두 write 도 *대부분 케이스에서* 보인다.
2. 잔여 race 를 *값이 의미상 안전한 조합* 으로 만든다: audio thread 가 state==4 분기에서 `m_ElapsedSamples`가 직전 비-0 값이면 mult 가 (1 - elapsed/N) 부근에서 *fade-out 곡선 안에 있는* 값이므로 점프 진폭이 작음(허용 가능). `m_FadeOutFromVolume` 도 *이전 RequestPitchChange* 가 박제한 값이라 의미상 [0, 1] 범위 안. 점프 진폭이 1 → 0 같은 극단이 안 되도록 **state==4 진입 직후 첫 sample 에 대해서만 mult 를 직전 sample 의 m_VolumeMultiplier 와 곱한 평균값으로 부드럽게 시작**하는 1-sample anti-jump guard 를 둘 수도 있으나, 가설 A 가 주범일 가능성이 더 크므로 **본 plan 은 write 순서 박제 + 가설 A 봉합** 에 집중. 가설 B 의 잔여 잡음이 발견되면 후속 plan 후보.

선행 plan 05 의 `m_State` write 순서가 사실상 마지막에 위치하긴 했으나(`m_FadeOutFromVolume = …; m_ElapsedSamples = 0; m_State = 4;`), C# compiler 가 의미상 동등한 reorder 를 했는지는 IL 단에서 보장이 약하다. 본 plan 은 *주석으로 write 순서 박제* + `System.Threading.Thread.MemoryBarrier()` 1 회 호출을 m_State write 직전에 삽입해 *명시적 store barrier* 를 강제한다. MemoryBarrier 는 GC 할당 0, x86/x64 에서는 lock add 1 회로 ~10ns — main thread 호출이므로 audio thread 성능 영향 없음. RequestPitchChange 에도 동일 패턴 적용.

### 현재 working tree 상태 (미커밋, 선행 plan 05 적용 후)

`Read Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs (2026-05-21)` 결과 220 lines, 6-state machine + wrap fade + RequestFadeOut + m_VolumeMultiplier 박제 모두 구현 완료. `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-21)` 결과 354 lines, `VoiceState.FadingOutDsp` enum 추가 + `RequestGripReleaseFadeOut(int)` + Update 폴링 분기 모두 구현 완료. `Read Assets/Instruments/Trombone/Scripts/Trombone.cs (2026-05-21)` 결과 179 lines, `LateUpdate` 의 grip release 라인 `audioOutput.RequestGripReleaseFadeOut(baseToneMidiNote)` 호출 박제 완료. **`InstrumentAudioOutput.cs` 와 `Trombone.cs` 는 본 retry 에서 추가 수정 없음** — 두 파일은 선행 plan 05 그대로 유지.

### 본 plan 의 변경 범위

`TrombonePitchDsp.cs` 1 파일만 수정. 다음 4개 영역:

(A) **wrap fade 설계 교체** — 기존 `m_LoopWrapSignal` 단일 신호 + post-wrap fade-in only 를 두 개 신호 (`m_LoopApproachSignal` + `m_LoopWrapSignal`) + pre-fade-out + post-fade-in 으로 교체. `m_LoopWrapFadeElapsedSamples` 1 변수를 `m_LoopSeamFadeOutElapsedSamples` + `m_LoopSeamFadeInElapsedSamples` 2 변수로 분리.

(B) **post-wrap fade-IN 첫 sample wt=0 버그 제거** — `wt = (float)m_LoopSeamFadeInElapsedSamples / m_FadeInSamples` 대신 `wt = (float)(m_LoopSeamFadeInElapsedSamples + 1) / m_FadeInSamples` 로 박제(첫 sample wt = 1/N 시작). 동등하게 `m_LoopSeamFadeInElapsedSamples++` 를 *곱셈 전* 에 두는 패턴.

(C) **LateUpdate wrap approach 감지 추가** — 기존 wrap 감지 (`curr < prev - half`) 옆에 approach 감지 (`clip.samples - curr < approachThreshold && !m_ApproachArmed`) 추가. `m_ApproachArmed` 는 한 wrap cycle 당 1 회 signal 발화를 보장(LateUpdate frame jitter 흡수).

(D) **RequestFadeOut / RequestPitchChange write 순서 박제 + MemoryBarrier** — `System.Threading.Thread.MemoryBarrier()` 호출을 `m_State = 4` (또는 `m_State = 1`) write 직전에 삽입. 주석으로 write 순서 박제: capture → elapsed reset → barrier → state.

`InstrumentAudioOutput.cs` 와 `Trombone.cs` 는 수정 안 함 — 선행 plan 05 의 `RequestGripReleaseFadeOut` 호출 / Update 폴링 / VoiceState.FadingOutDsp 가 모두 그대로 동작. asmdef 영향 없음(import `System.Threading` 1 줄 추가, `System` 은 이미 `Instruments.asmdef` 의 implicit `mscorlib` 에 포함).

### ARD / Tech Spec 정합

- **ARD 05** (`OnAudioFilterRead wrap 감지 페이드`) — 본 plan 의 (A)(B)(C) 가 충실히 답습. *Consequences* "fade 길이는 기존 5ms 크로스페이드와 통일 권장" 도 만족 (`m_FadeOutSamples`/`m_FadeInSamples` 재사용).
- **ARD 06** (`TrombonePitchDsp.RequestFadeOut() 신규 추가` + `Choke 와 RequestFadeOut 동일 경로 공유 금지`) — 본 plan 은 `RequestFadeOut()` 의 *호출 진입점은 그대로 유지* + write 순서/MemoryBarrier 만 강화. Choke 경로 (`StopNoteImmediate`) 와 분리 유지.
- **ARD 07** (`현재 볼륨에서 이어 FadeOut`) — 본 plan 은 `RequestPitchChange` 의 `m_FadeOutFromVolume = m_VolumeMultiplier` 캡처 유지 + write 순서 박제로 race 봉합 보강.
- **Tech Spec §Invariants** — `AudioSource.pitch` main thread only ✅, `OnAudioFilterRead` GC 할당 0 ✅, 모든 fade ≤ 10ms ✅ (5ms × 2 = 10ms 한계 안: pre fade-out 5ms + post fade-in 5ms 가 각각 ≤10ms 이고 두 fade 는 시간상 직렬 안 겹침), Choke 즉시 절단 유지 ✅, 발음 미시작 상태 오디오 처리 0 ✅ (`m_Source.isPlaying` guard).
- **Tech Spec §Boundaries** — 건드린다: `TrombonePitchDsp.cs` ✅. 건드리지 않는다: `TromboneSlideController.cs`, `TrombonePartialController.cs`, `InstrumentBase`, MIDI 이벤트 표면, 멀티플레이어, 클립 ✅.

### asmdef 영향

`Read Assets/Instruments/Instruments.asmdef (2026-05-21)` 결과: references = `["Unity.InputSystem", "Hands", "Unity.XR.Interaction.Toolkit"]`. 본 plan 은 `System.Threading.Thread.MemoryBarrier()` 호출 1 줄 추가 — `System.Threading` 은 .NET BCL/`mscorlib` 의 일부로 Unity 어셈블리의 implicit reference 라 별도 asmdef reference 추가 불필요. `using System.Threading;` 한 줄만 file top 에 추가.

## Verified Structural Assumptions

- `TrombonePitchDsp.cs` 현재 6-state machine + wrap fade + RequestFadeOut + m_VolumeMultiplier 박제 모두 구현 완료 (line 1-221). `m_LoopWrapSignal` (volatile bool, main set / audio read+clear), `m_LoopWrapFadeElapsedSamples` (int, audio thread only, MaxValue=inactive), `m_PrevTimeSamples` (int, main thread only) 박제. `OnEnable` 이 `m_FadeOutSamples`/`m_FadeInSamples` 를 `AudioSettings.outputSampleRate * 0.005f` 로 박제. `LateUpdate` 가 wrap 감지(`curr < m_PrevTimeSamples - half`) + state==2 처리. `OnAudioFilterRead` 가 state 0/1/2/3/4/5 envelope + wrap fade-in 곱셈 합성. `RequestPitchChange` 가 `m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier` 캡처 + `m_ElapsedSamples = 0` + `m_State = 1` (s == 0 || s == 3 분기). `RequestFadeOut` 이 동일 3 write 패턴 + state = 4. — 출처: `Read Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs (2026-05-21)`.
- `InstrumentAudioOutput.cs` 현재 `enum VoiceState { Idle, Attacking, Active, SustainedActive, Releasing, FadingOutDsp }` 6-state. `RequestGripReleaseFadeOut(int note)` 가 voice 의 `TrombonePitchDsp.RequestFadeOut()` 호출 + `voice.State = VoiceState.FadingOutDsp` + `voice.TrackPitch = false`. `Update()` 가 `voice.State == VoiceState.FadingOutDsp` 인 voice 의 `dsp.IsStopReady` 폴링 → `StopVoice(voice)`. **본 plan 에서 수정 안 함** — 선행 plan 05 그대로 유지. — 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-21)`.
- `Trombone.cs` 현재 `LateUpdate` 의 grip release 라인 `else if (!grip && m_IsBlowing) { if (audioOutput != null) audioOutput.RequestGripReleaseFadeOut(baseToneMidiNote); m_IsBlowing = false; }` 박제. anchor detach 분기 `TriggerMidi(... Choke)` 유지. Partial/Slide 변경 분기 `audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, newPitch)` 유지. Awake/OnDestroy 의 `VoiceGameObjectCreated` 구독·구독해제 박제. **본 plan 에서 수정 안 함** — 선행 plan 05 그대로 유지. — 출처: `Read Assets/Instruments/Trombone/Scripts/Trombone.cs (2026-05-21)`.
- `AudioSource.timeSamples`/`AudioSource.clip.samples`/`AudioSource.loop`/`AudioSource.isPlaying` 은 Unity 표준 API, main thread read 안전. `timeSamples` 는 현재 재생 sample 위치 (0 ~ clip.samples-1), loop=true 에서 wrap 시 0 으로 점프. `clip.samples` 는 클립의 총 sample 수. pitch shift 가 적용된 시간 진행도 `timeSamples` 에 반영(예: pitch=2.0 → frame 당 진행 sample 2 배). — 출처: Unity 공식 docs, 표준 API (planner 가정, 선행 plan 05 와 동일).
- `Instruments.asmdef` references = `["Unity.InputSystem", "Hands", "Unity.XR.Interaction.Toolkit"]`. `System.Threading` 은 implicit BCL reference (asmdef noEngineReferences=false + autoReferenced=true), 추가 reference 불필요. `Assets/Instruments/Trombone/Scripts/` 폴더에 `.asmdef` 없음 → 자동 `Instruments.asmdef` 포함. — 출처: `Read Assets/Instruments/Instruments.asmdef (2026-05-21)`.
- C# `volatile` 의 의미: 각 access 가 acquire/release semantics 를 갖지만 *서로 다른 volatile 필드 간 store 순서* 는 보장 안 됨. CLR 이 reorder 할 수 있음. `System.Threading.Thread.MemoryBarrier()` 가 양방향(load+store) full barrier 를 강제 — store 가 그 이후 코드로 reorder 안 됨. x86/x64 에서 lock 명령 1 회 (~10ns), main thread 호출이라 audio thread 성능 영향 0. — 출처: ECMA-335 (C# spec) §15.5.4 + Microsoft Docs `System.Threading.Thread.MemoryBarrier` (planner 가정).
- ARD 05/06/07 의 *Decision* 박제 (선행 plan 05 §Verified Structural Assumptions 와 동일): 05="OnAudioFilterRead wrap 감지 페이드" / 06="TrombonePitchDsp.RequestFadeOut() 신규 추가" + "Choke 와 RequestFadeOut 동일 경로 공유 금지" / 07="현재 볼륨에서 이어 FadeOut". 본 plan 은 세 결정을 모두 유지하면서 구현 결함만 봉합. — 출처: `Read docs/specs/_archive/trombone/decisions/05-loop-boundary-fade.md, 06-grip-release-fadeout.md, 07-interrupted-crossfade.md (2026-05-21)`.
- Tech Spec §Invariants (선행 plan 05 와 동일): `AudioSource.pitch` main thread only, OnAudioFilterRead GC 0, 모든 fade ≤ 10ms, Choke 즉시 절단 유지, 발음 미시작 상태 오디오 처리 0. — 출처: `Read docs/specs/_archive/trombone/tech-specs/05-audio-click-suppression.md (2026-05-21)`.

## Approach

1. **`Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` — wrap fade 설계 교체 + write 순서 박제 + MemoryBarrier**

   1-1. **using 추가** — file top 에 `using System.Threading;` 1 줄 추가.

   1-2. **신규/대체 필드** — 기존 `m_LoopWrapSignal` (volatile bool) + `m_LoopWrapFadeElapsedSamples` (int, audio thread) 2 개를 다음 4 개로 교체:
   ```csharp
   volatile bool m_LoopApproachSignal;  // main thread set true on approach (wrap 임박), audio thread read+clear
   volatile bool m_LoopWrapSignal;      // main thread set true on wrap (기존 유지)
   // audio thread only:
   int m_LoopSeamFadeOutElapsedSamples = int.MaxValue; // pre-boundary fade-out 진행 카운터. MaxValue = inactive.
   int m_LoopSeamFadeInElapsedSamples  = int.MaxValue; // post-wrap fade-in 진행 카운터. MaxValue = inactive.
   // main thread only:
   int m_PrevTimeSamples;           // 기존 유지
   bool m_ApproachArmed;            // 한 wrap cycle 당 approach signal 1 회 발화 guard
   int m_ApproachThresholdSamples;  // OnEnable 박제
   ```
   `m_LoopWrapFadeElapsedSamples` 는 제거(역할이 `m_LoopSeamFadeInElapsedSamples` 로 이동).

   1-3. **OnEnable 갱신**:
   ```csharp
   int rate = AudioSettings.outputSampleRate;
   m_FadeOutSamples = Mathf.Max(1, Mathf.RoundToInt(k_FadeOutDurationSec * rate));
   m_FadeInSamples  = Mathf.Max(1, Mathf.RoundToInt(k_FadeInDurationSec  * rate));
   // approachThreshold = fade-out 길이 + LateUpdate 60fps frame jitter margin(2 frame).
   // 5ms fade-out (220 samples @ 44.1kHz) + 2 * (44100 / 60) = 220 + 1470 = 1690 samples ≈ 38ms.
   // pitch shift 가 큰 경우(pitch=2.0)도 2 * sampleRate / 60 마진이 흡수.
   m_ApproachThresholdSamples = m_FadeOutSamples + 2 * (rate / 60);
   m_LoopApproachSignal             = false;
   m_LoopWrapSignal                 = false;
   m_LoopSeamFadeOutElapsedSamples  = int.MaxValue;
   m_LoopSeamFadeInElapsedSamples   = int.MaxValue;
   m_ApproachArmed                  = false;
   m_PrevTimeSamples                = 0;
   // (기존 m_State/m_ElapsedSamples/m_VolumeMultiplier/m_FadeOutFromVolume 초기화 유지)
   ```

   1-4. **LateUpdate wrap 감지 교체** — 기존 wrap 감지만 있는 분기를 approach + wrap 두 신호 발화 분기로 교체:
   ```csharp
   if (m_Source != null && m_Source.isPlaying && m_Source.loop && m_Source.clip != null)
   {
       int curr = m_Source.timeSamples;
       int totalSamples = m_Source.clip.samples;
       int half = totalSamples / 2;

       // ── wrap 감지 (기존 유지) ──
       if (curr < m_PrevTimeSamples - half) // wrap 발생
       {
           m_LoopWrapSignal = true;
           m_ApproachArmed = false; // 다음 wrap cycle 의 approach 재무장
       }

       // ── approach 감지 (신규) ──
       // wrap 임박: 남은 sample 수 < threshold 이고 아직 이번 cycle approach signal 발화 안 한 경우.
       int remaining = totalSamples - curr;
       if (!m_ApproachArmed && remaining < m_ApproachThresholdSamples)
       {
           m_LoopApproachSignal = true;
           m_ApproachArmed = true; // 같은 cycle 안에서 중복 발화 차단
       }

       m_PrevTimeSamples = curr;
   }

   // ── AwaitingPitch 처리 (기존 유지) ──
   if (m_State == 2)
   {
       if (m_Source != null) m_Source.pitch = m_PendingPitch;
       m_ElapsedSamples = 0;
       m_State = 3;
   }
   ```

   1-5. **OnAudioFilterRead 의 wrap fade 처리 교체** — 기존 single fade-in 곱셈을 pre fade-out + post fade-in 두 곱셈으로 교체:
   ```csharp
   // ── seam signal 처리: approach → fade-out 활성화, wrap → fade-in 활성화 ──
   if (m_LoopApproachSignal)
   {
       m_LoopApproachSignal = false;
       m_LoopSeamFadeOutElapsedSamples = 0; // pre-boundary fade-out 시작
   }
   if (m_LoopWrapSignal)
   {
       m_LoopWrapSignal = false;
       m_LoopSeamFadeOutElapsedSamples = int.MaxValue; // fade-out 종료(wrap 이미 발생, 이후 의미 없음)
       m_LoopSeamFadeInElapsedSamples  = 0;            // post-wrap fade-in 시작
   }

   int s = m_State;
   int sampleFrames = data.Length / channels;

   for (int i = 0; i < sampleFrames; i++)
   {
       float mult;
       // (기존 state 0/1/2/3/4/5 envelope 분기 그대로 유지 — 변경 없음)
       if (s == 0) mult = 1f;
       else if (s == 1) { /* 기존 FadingOut */ ... }
       else if (s == 2) mult = 0f;
       else if (s == 3) { /* 기존 FadingIn */ ... }
       else if (s == 4) { /* 기존 FadingOutToStop */ ... }
       else mult = 0f; // s == 5

       // ── pre-boundary fade-OUT 곱셈 합성 (신규) ──
       // 1 → 0 로 fadeOutSamples 동안 감소. wrap 직전에 mult 가 0 부근이 되도록.
       if (m_LoopSeamFadeOutElapsedSamples < m_FadeOutSamples)
       {
           // 첫 sample wt = 1 - 1/N (대략 219/220 = 0.9954), 마지막 sample wt ≈ 0.
           // wt = 1 - (elapsed + 1) / N 로 점프 방지(첫 sample 이 1.0 → 0.9954 자연 진입).
           float wtOut = 1f - (float)(m_LoopSeamFadeOutElapsedSamples + 1) / m_FadeOutSamples;
           if (wtOut < 0f) wtOut = 0f;
           mult *= wtOut;
           m_LoopSeamFadeOutElapsedSamples++;
       }
       // ── post-wrap fade-IN 곱셈 합성 (교체: wt=0 버그 제거) ──
       // 0 → 1 로 fadeInSamples 동안 증가. 첫 sample wt = 1/N (0 점프 방지).
       if (m_LoopSeamFadeInElapsedSamples < m_FadeInSamples)
       {
           float wtIn = (float)(m_LoopSeamFadeInElapsedSamples + 1) / m_FadeInSamples;
           if (wtIn > 1f) wtIn = 1f;
           mult *= wtIn;
           m_LoopSeamFadeInElapsedSamples++;
       }

       m_VolumeMultiplier = mult; // 기존 박제 유지

       for (int c = 0; c < channels; c++)
           data[i * channels + c] *= mult;
   }
   ```
   **GC 할당 0** 유지 (모든 신규 변수는 stack float/int, in-place data 수정). **AudioSource API 호출 0** 유지.

   1-6. **RequestPitchChange / RequestFadeOut 의 write 순서 박제 + MemoryBarrier** — 두 메서드 모두 동일 패턴:
   ```csharp
   public void RequestPitchChange(float newPitch)
   {
       m_PendingPitch = newPitch;
       int s = m_State;
       if (s == 4 || s == 5) return;
       if (s == 0 || s == 3)
       {
           // write 순서: capture → elapsed reset → MemoryBarrier → state.
           // C# volatile 은 서로 다른 필드 간 store 순서 보장 안 함.
           // MemoryBarrier 로 audio thread 가 state==1 을 보는 시점에 다른 두 write 도 보이도록 강제.
           m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier; // write 1: 현재 mult 캡처
           m_ElapsedSamples    = 0;                                   // write 2: fade 진행 리셋
           Thread.MemoryBarrier();                                    // store barrier
           m_State             = 1;                                   // write 3: 새 분기 active
       }
   }

   public void RequestFadeOut()
   {
       int s = m_State;
       if (s == 4 || s == 5) return;
       m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier; // write 1
       m_ElapsedSamples    = 0;                                   // write 2
       Thread.MemoryBarrier();                                    // store barrier (AC9 가설 B 봉합)
       m_State             = 4;                                   // write 3
   }
   ```
   MemoryBarrier 는 main thread 호출 빈도가 frame 단위 (≤ 60Hz × few) 라 성능 영향 무시 가능. audio thread (`OnAudioFilterRead`) 는 호출 안 함 — DSP 성능 영향 0.

2. **검증 단계** ([`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md) 따름)

   - 스크립트 수정 후 컴파일 대기 → `read_console action=get types=[error] count=20` → 0 error.
   - `editor_state.isCompiling == false` 확인.
   - EditMode 회귀: `unity-test-runner` 1 회 호출 — 선행 plan 05 의 baseline (`EditMode 102/102 pass` 또는 `133/4 PASS`) 유지. `TrombonePitchDsp` 의 wrap fade 변경은 시블링(Piano/DrumKit) 미부착이라 영향 0. `RequestFadeOut`/`RequestPitchChange` 의 MemoryBarrier 추가는 호출 시그니처 불변 → 호출 측 변경 0.
   - Editor Play 모드 시각·청각 검증 (AC8/AC9 재검증 + 선행 plan AC10~AC12 회귀):
     - AC8: trombone anchor 진입 + 왼손 grip 5 초 이상 홀드 → 루프 wrap 2 회 이상 발생 → 가청 클릭 없음 확인.
     - AC9: grip 홀드 발음 중 release → 5ms fade-out 후 깔끔 silence → 클릭 없음.
     - AC10: grip 홀드 + 슬라이드 빠른 흔들기(FadingIn 도중 새 RequestPitchChange) → 볼륨 점프 없음 (선행 plan 통과 회귀).
     - AC11: grip 미입력 정적 상태 → 오디오 처리 0 (선행 plan 통과 회귀).
     - AC12: grip 홀드 중 anchor 이탈 → 즉시 silence, fade 없음 (선행 plan 통과 회귀).

3. **자산 수정 결정 트리 (Unity MCP Workflow)** — 본 plan 변경은 `.cs` 1 파일 수정만. `Trombone.prefab` / 씬 / SO 변경 없음. `manage_prefabs` / `manage_scene` 호출 0 회. 컴파일 후 `read_console` + `unity-test-runner` 만.

## Deliverables

- `Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` — 수정.
  (a) `using System.Threading;` 추가.
  (b) `m_LoopWrapFadeElapsedSamples` 1 필드 제거, `m_LoopSeamFadeOutElapsedSamples` + `m_LoopSeamFadeInElapsedSamples` + `m_LoopApproachSignal` + `m_ApproachArmed` + `m_ApproachThresholdSamples` 5 필드 신규.
  (c) `OnEnable` 에 `m_ApproachThresholdSamples = m_FadeOutSamples + 2 * (rate / 60)` 박제 + 신규 필드 초기화.
  (d) `LateUpdate` 의 wrap 감지 분기에 approach 감지(`!m_ApproachArmed && remaining < m_ApproachThresholdSamples → m_LoopApproachSignal = true; m_ApproachArmed = true`) 추가 + wrap 발생 시 `m_ApproachArmed = false` 재무장.
  (e) `OnAudioFilterRead` 의 단일 wrap fade-in 곱셈을 pre-boundary fade-OUT (`m_LoopSeamFadeOutElapsedSamples` ramping 1→0) + post-wrap fade-IN (`m_LoopSeamFadeInElapsedSamples` ramping 1/N → 1) 두 곱셈으로 교체. 두 fade 모두 `(elapsed + 1) / N` 패턴으로 첫 sample 0 점프 제거.
  (f) `RequestPitchChange` / `RequestFadeOut` 의 `m_State` write 직전에 `Thread.MemoryBarrier()` 1 회 호출 추가 + write 순서 박제 주석.

- `Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` — **수정 없음** (선행 plan 05 그대로 유지).
- `Assets/Instruments/Trombone/Scripts/Trombone.cs` — **수정 없음** (선행 plan 05 그대로 유지).

## Acceptance Criteria

- [ ] `[auto-hard]` `TrombonePitchDsp.cs` 에 `using System.Threading;` 라인이 존재하고, `m_LoopApproachSignal` (volatile bool) + `m_LoopSeamFadeOutElapsedSamples` + `m_LoopSeamFadeInElapsedSamples` + `m_ApproachArmed` + `m_ApproachThresholdSamples` 5 필드가 선언돼 있다. 기존 `m_LoopWrapFadeElapsedSamples` 필드는 제거됐다.
  **검증:** `Grep -nE "using System\.Threading;|m_LoopApproachSignal|m_LoopSeamFadeOutElapsedSamples|m_LoopSeamFadeInElapsedSamples|m_ApproachArmed|m_ApproachThresholdSamples" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 ≥ 6 매칭(필드 선언 5 + using 1 + 사용처 추가) + `Grep -n "m_LoopWrapFadeElapsedSamples" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 0 매칭.

- [ ] `[auto-hard]` `TrombonePitchDsp.OnAudioFilterRead` 가 (a) `m_LoopApproachSignal` 감지 시 `m_LoopSeamFadeOutElapsedSamples = 0` 활성화 (b) `m_LoopWrapSignal` 감지 시 `m_LoopSeamFadeOutElapsedSamples = int.MaxValue` + `m_LoopSeamFadeInElapsedSamples = 0` 활성화 (c) 매 sample 마다 pre fade-out 곱셈 `mult *= 1f - (float)(m_LoopSeamFadeOutElapsedSamples + 1) / m_FadeOutSamples` (또는 동등) (d) 매 sample 마다 post fade-in 곱셈 `mult *= (float)(m_LoopSeamFadeInElapsedSamples + 1) / m_FadeInSamples` (또는 동등) 4 동작을 모두 수행하고, AudioSource API (`.pitch`/`.Play`/`.Stop`/`.clip`/`.loop`/`.volume`) 호출 0 건이다.
  **검증:** `Grep -nE "m_LoopApproachSignal = false|m_LoopSeamFadeOutElapsedSamples = 0|m_LoopSeamFadeOutElapsedSamples = int\.MaxValue|m_LoopSeamFadeInElapsedSamples = 0|m_LoopSeamFadeOutElapsedSamples \+ 1|m_LoopSeamFadeInElapsedSamples \+ 1" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 ≥ 6 매칭 + `Grep -nE "m_Source\.(pitch|Play|Stop|clip|loop|volume) *=" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과의 모든 라인이 `LateUpdate` 메서드 본문 안 (육안/awk 메서드 경계 확인).

- [ ] `[auto-hard]` `TrombonePitchDsp.LateUpdate` 가 approach 감지 (`!m_ApproachArmed && (clip.samples - timeSamples) < m_ApproachThresholdSamples` → `m_LoopApproachSignal = true; m_ApproachArmed = true`) 와 wrap 감지 (`curr < m_PrevTimeSamples - half` → `m_LoopWrapSignal = true; m_ApproachArmed = false`) 두 분기를 모두 가진다.
  **검증:** `Grep -nE "m_ApproachArmed = true|m_ApproachArmed = false|m_LoopApproachSignal = true|m_LoopWrapSignal = true|m_ApproachThresholdSamples" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 ≥ 5 매칭, 모두 `LateUpdate` 메서드 본문 안.

- [ ] `[auto-hard]` `TrombonePitchDsp.OnEnable` 이 `m_ApproachThresholdSamples = m_FadeOutSamples + 2 * (rate / 60)` (또는 동등 산식) 으로 박제하고, 신규 5 필드 (`m_LoopApproachSignal=false`, `m_LoopSeamFadeOutElapsedSamples=int.MaxValue`, `m_LoopSeamFadeInElapsedSamples=int.MaxValue`, `m_ApproachArmed=false`, `m_PrevTimeSamples=0`) 를 모두 초기화한다.
  **검증:** `Grep -nE "m_ApproachThresholdSamples = |m_LoopApproachSignal\s+= false|m_LoopSeamFadeOutElapsedSamples\s+= int\.MaxValue|m_LoopSeamFadeInElapsedSamples\s+= int\.MaxValue|m_ApproachArmed\s+= false" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 ≥ 5 매칭, 모두 `OnEnable` 메서드 본문 안.

- [ ] `[auto-hard]` `TrombonePitchDsp.RequestFadeOut` 와 `RequestPitchChange` 가 `m_State` write 직전에 `Thread.MemoryBarrier()` 1 회 호출을 포함한다. write 순서는 `m_FadeOutFromVolume = …; m_ElapsedSamples = 0; Thread.MemoryBarrier(); m_State = …;`.
  **검증:** `Grep -nE "Thread\.MemoryBarrier\(\)" Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` 결과 ≥ 2 매칭 (RequestFadeOut + RequestPitchChange 각 1 회). 두 매칭 모두 같은 메서드 본문 안에서 직전 라인이 `m_ElapsedSamples = 0;`, 직후 라인이 `m_State = …;` 형태.

- [ ] `[auto-soft]` Unity Editor 컴파일 0 error (`read_console action=get types=[error] count=20`). EditMode 회귀 (`unity-test-runner`) — 선행 plan 05 baseline 유지 (`failed=0`). 시블링 (Piano/DrumKit/InstrumentAudioOutput) 라이프사이클 영향 0 — 시블링 voice 는 `TrombonePitchDsp` 미부착이라 본 plan 변경 전체가 시블링에 noop.
  **검증:** `read_console action=get types=[error] count=20` → TrombonePitchDsp 컴파일 에러 0 건 + `editor_state.isCompiling == false` + `unity-test-runner` EditMode 결과 `failed=0`.

- [ ] `[manual-hard]` **AC8 재검증** (sub-spec Behavior #1, ARD 05) — Editor Play 모드에서 TromboneAnchor 진입 + 왼손 Grip 홀드 → 발음을 5 초 이상 유지(클립 1~2 초 기준 최소 2 회 이상 loop wrap 발생) → 가청 클릭이 *완전히* 들리지 않는다. 클립 끝-시작 경계가 부드럽게 연결됨(pre fade-out 으로 무음 진입 + post fade-in 으로 무음 복귀). 가 이 plan 적용 후 재검증에서 통과한다.
  **검증:** Play 모드 진입 → trombone anchor 텔레포트 → 왼손 grip 5 초 이상 홀드 → 청각으로 루프 경계 클릭 없음 확인 + Inspector / 디버그 로그에서 wrap 직전에 `TrombonePitchDsp.m_LoopSeamFadeOutElapsedSamples` 가 0 부터 증가하며 mult 가 1 → 0 으로 감소 + wrap 직후 `m_LoopSeamFadeInElapsedSamples` 가 0 부터 증가하며 mult 가 0 → 1 로 증가하는 sequence 박제.

- [ ] `[manual-hard]` **AC9 재검증** (sub-spec Behavior #2, ARD 06) — Editor Play 모드에서 anchor 진입 + 왼손 Grip 홀드 → 발음 중 Grip 릴리즈 → 짧은 fade-out(≤10ms, 청각상 거의 즉시) 후 silence. 절단 클릭 + 100ms 이상의 매달림 모두 없음. **20 회 이상 grip on/off 반복으로도 클릭이 일관되게 들리지 않음**(loop wrap 과 grip release 가 겹치는 timing 도 포함). 가 이 plan 적용 후 재검증에서 통과한다.
  **검증:** Play 모드에서 grip 홀드 발음 중 grip 뗌을 20 회 이상 다양한 timing(짧게 1 초 / 길게 5 초 / loop wrap 직전 / 직후) 으로 반복 → 청각으로 짧고 깔끔한 종료 + 클릭 0 회 + Inspector 에서 voice 의 `State` 가 `FadingOutDsp → Idle` 로 ~5-10ms 안에 전이 + `Source.isPlaying` 이 즉시 false.

- [ ] `[manual-hard]` **AC10 회귀** (sub-spec Behavior #3, ARD 07) — Editor Play 모드에서 grip 홀드 발음 중 → 오른손 Grip + 슬라이드를 빠르게 좌우로 흔들기 → 볼륨 점프 없이 부드러운 피치 전환. 선행 plan 05 에서 통과 + 본 plan 의 MemoryBarrier 추가가 회귀 안 만듦.
  **검증:** Play 모드에서 grip 홀드 + 슬라이드 빠른 흔들기 → 청각으로 볼륨 점프 없음 + 디버그 로그에서 `RequestPitchChange` 진입 시 `m_FadeOutFromVolume` 값이 `m_VolumeMultiplier` 현재 값(0~1 사이 임의값) 으로 캡처되는지 확인.

- [ ] `[manual-hard]` **AC11 회귀** (sub-spec Behavior #4) — 발음 미시작 상태에서 슬라이드/partial 변경/대기 → 어떤 오디오 처리도 발생 안 함. 선행 plan 05 통과 회귀.
  **검증:** Play 모드에서 anchor 진입 후 grip 미입력 상태로 slide/partial 변경 + 1 분 대기 → 정적 + Inspector 에서 `VoicePool_Trombone` 의 모든 voice `Source.isPlaying=false` + 모든 voice `State==Idle` + `TrombonePitchDsp.m_State==0`.

- [ ] `[manual-hard]` **AC12 회귀** (sub-spec Behavior #5) — 발음 중 anchor 이탈 → 기존 Choke 흐름 즉시 silence, fade 없음. 선행 plan 05 통과 회귀.
  **검증:** Play 모드에서 grip 홀드 발음 중 다른 위치로 텔레포트 → 음이 fade 없이 즉시 정지 + `VoicePool_Trombone` voice 들 `isPlaying=false` + `voice.State==Idle` (FadingOutDsp 경유 안 함).

## Out of Scope

- AC8 / AC9 외 다른 sub-spec AC 재구현 — AC6/AC7 / AC10~AC12 는 선행 plan 05 에서 통과했으므로 회귀 검증만.
- `m_FadeOutFromVolume` 캡처 시점의 가설 B 잔여 점프 — MemoryBarrier 로 *대부분* 봉합. 그래도 미세 클릭이 인지되면 후속 plan 에서 1-sample anti-jump guard (직전 sample mult 와 새 mult 의 평균값으로 첫 sample mult 박제) 추가 검토.
- 시블링(Piano/DrumKit) 의 loop seam click — `TrombonePitchDsp` 미부착이라 본 plan 변경 영향 없음. 시블링이 sustain loop click 을 겪으면 별도 sub-spec 후보.
- Grip release 시 외부 `MidiTriggered` 구독자 NoteOff event 발행 — 선행 plan 05 §Notes 박제 그대로. 본 plan 변경 안 함.
- fade duration / curve 튜닝 (선형 vs 코사인 등) — 선행 plan 05 의 5ms × 선형 유지.
- approach threshold 동적 조정 (pitch shift 큰 경우 더 큰 margin) — 본 plan 의 `2 * sampleRate / 60` 마진이 pitch=2.0 까지 흡수. 더 빠른 pitch shift 가 필요하면 후속 plan 후보.
- Choke 경로 클릭 — sub-spec §Behavior #5 의 "기존 Choke 흐름 유지" 정합. 별도 sub-spec 후보.

## Notes

- **wrap 1 회 cycle 의 fade timing**: clip 의 마지막 ~38ms 부터 pre fade-out 시작 → wrap 발생 직전 mult ≈ 0 → wrap 발생 (불연속점이 무음 안에 박힘) → wrap 직후 ~5ms 동안 post fade-in → mult 가 1 회복. 사용자 청각상 음량 dip 38+5 = 43ms ≈ 26Hz 주기로 인지되지만 mult 가 *부드럽게* 감소·증가하므로 클릭 대신 *vibrato 비슷한 약한 amplitude 변조* 로 인지됨. sub-spec §Behavior #1 의 "볼륨 불연속 없이"가 점프 없음을 의미하므로 정합. 만약 amplitude 변조가 거슬리면 후속 plan 에서 approachThreshold 를 줄이고 fade duration 도 줄이는 튜닝 검토.
- **`m_ApproachArmed` 재무장 timing**: wrap 발생 시 LateUpdate 가 `m_ApproachArmed = false` reset → 다음 cycle 의 첫 approach 감지에서 다시 발화. 이론상 wrap 이 LateUpdate 사이에 *없이* approach 만 발생할 수 있으나, approachThreshold 가 frame jitter margin 만큼 보수적이라 wrap 누락 위험 거의 0. 만약 wrap 누락이 발생하면 다음 wrap cycle 에서 approach 가 다시 발화돼 self-healing.
- **pre fade-out 도중 grip release 발생**: `m_LoopSeamFadeOutElapsedSamples < m_FadeOutSamples` 진행 중 `RequestFadeOut()` 호출 → state==4 진입 → state 4 envelope 의 mult 가 pre fade-out 곱셈 mult 와 합성돼 더 빠르게 0 으로 감. 점프 없음 (둘 다 fade-out 방향). 청각상 grip release fade 가 약간 빨라지는 정도, 인지 못 함.
- **pre fade-out 도중 RequestPitchChange 발생**: pre fade-out 진행 중 새 pitch 요청 → state 1(FadingOut) 진입 → mult 가 state envelope 과 pre fade-out 곱셈으로 더 빠르게 0 으로 감 → AwaitingPitch 진입 → pitch 교체 → FadingIn → 이때 pre fade-out 이 끝나 있고(이미 mult 0 부근에서 wrap 발생 후 post fade-in 시작) → FadingIn 곱셈 + post fade-in 곱셈으로 천천히 회복. 점프 없음 ((0 → t/N) × (0 → t/N) = 0 부근 점진 증가).
- **post fade-in 도중 grip release 발생**: `m_LoopSeamFadeInElapsedSamples < m_FadeInSamples` 진행 중 `RequestFadeOut()` 호출 → state==4 진입 + `m_FadeOutFromVolume = m_VolumeMultiplier` (현재 mult, post fade-in 진행값) 캡처 → state 4 envelope 의 mult 가 캡처값에서 0 으로 감 + post fade-in 곱셈이 더 큰 wt 가 돼서 mult 점진 증가 — 두 곱셈 합성으로 약간 복잡하나 *모두 부드러운 곡선* 이라 점프 없음.
- **MemoryBarrier 의 한계**: Thread.MemoryBarrier 가 store ordering 은 강제하나, audio thread 가 *언제* 다음 read 를 할지는 보장 안 함. 즉 main thread 의 3 write 가 *원자적으로 한 번에* 보이지 않을 수 있다 — 그러나 *순서대로* 보이게 됨. audio thread 가 m_State==4 를 본 시점에 m_FadeOutFromVolume, m_ElapsedSamples 도 새 값을 본다 (barrier 의미). 가설 B 의 race 잔여 시나리오는 audio thread 가 m_State==3 (old) 분기를 한 sample frame 더 실행 후 다음 frame 에 m_State==4 분기로 전환하는 *지연* 인데, 이는 점프 없음 (state 3 진행 mult 와 state 4 시작 mult 가 m_VolumeMultiplier 박제로 같음).
- **시블링 voice 영향 없음**: Piano/DrumKit voice 는 `TrombonePitchDsp` 미부착 → `OnAudioFilterRead` 가 호출 안 됨 → 본 plan 변경 전체가 시블링에 noop. `InstrumentAudioOutput.Update` 폴링도 시블링 voice 가 `VoiceState.FadingOutDsp` 에 안 들어가므로 영향 0.
- **fade duration 합산 ≤ 10ms 정합**: pre fade-out 5ms + post fade-in 5ms 두 fade 가 *시간상 직렬* (pre 가 wrap 이전, post 가 wrap 이후) 이라 *겹치지 않음*. 각 fade 가 ≤10ms (5ms) 이고 사용자 청각상 "fade 1 회 단위" 는 ≤10ms 정합. grip release fade 5ms 와 wrap fade 가 겹치는 시점에는 곱셈 합성으로 더 빠르게 fade 가 진행되므로 ≤10ms 정합.
- **`System.Threading.Thread.MemoryBarrier`** 는 .NET Standard 2.0 API, Unity 6 의 .NET 호환성 안. GC 할당 0, x86/x64 에서 lock add (~10ns), ARM 에서 dmb (~수십 ns). main thread 호출 빈도 (60Hz × few) 라 무시 가능.
- **선행 plan 05 의 `m_LoopWrapFadeElapsedSamples` 제거**: 역할이 `m_LoopSeamFadeInElapsedSamples` 로 이동. Grep 으로 0 매칭 보장(AC1 검증).

## Handoff

(plan 완료 후 doc-updater 가 자동 갱신. 본 plan 은 05 sub-spec 의 두 번째 plan 이며 트럼본 피처 전체의 마지막 plan 후보.)

- **루프 경계 seam fade 패턴**: `LateUpdate` 가 `clip.samples - timeSamples < approachThreshold` 감지 → audio thread 가 pre fade-out (1→0) 시작 → wrap 발생 → audio thread 가 post fade-in (1/N → 1) 시작. `approachThreshold = m_FadeOutSamples + 2 * (sampleRate / 60)` 으로 LateUpdate frame jitter 흡수. 후속 plan 이 비슷한 seam click 봉합을 다른 악기에 적용할 때 동일 패턴.
- **fade 첫 sample wt = 0 함정**: `wt = elapsed / N` 패턴은 elapsed=0 일 때 wt=0 → mult *= 0 → 1 → 0 → 1/N 점프. 항상 `wt = (elapsed + 1) / N` 또는 `elapsed++` 를 곱셈 *전* 에 두는 패턴 사용. 후속 plan 의 모든 envelope 곱셈에 동일 규칙 적용.
- **C# volatile + Thread.MemoryBarrier 패턴**: 서로 다른 volatile 필드 간 store 순서 보장은 MemoryBarrier 로만 가능. audio thread 와 main thread 가 여러 필드를 협력 read/write 할 때 main thread 의 *마지막 write 직전* 에 MemoryBarrier 1 회 호출 + 주석으로 write 순서 박제 패턴. 후속 plan 이 audio/main thread 협력 필드를 추가할 때 동일 패턴.
- **선행 plan 05 의 6-state machine / VoiceState.FadingOutDsp / RequestGripReleaseFadeOut 모두 유지**: 본 retry 가 그 위에서 wrap fade 설계와 write 순서만 수정. 시그니처/호출 측 변경 0.
- **잔여 가설 B (m_FadeOutFromVolume race) 후속 plan 후보**: MemoryBarrier 로 *대부분* 봉합. 미세 클릭이 manual 검증에서 잡히면 1-sample anti-jump guard (직전 sample mult 와 새 mult 의 평균값으로 첫 sample mult 박제) 추가 plan.
