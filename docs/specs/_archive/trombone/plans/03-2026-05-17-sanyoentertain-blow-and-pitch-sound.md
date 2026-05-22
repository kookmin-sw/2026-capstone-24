# Trombone(InstrumentBase) — 왼손 Grip 발음 + 연속 pitch 운전

**Linked Spec:** [`03-blow-and-pitch-sound.md`](../specs/03-blow-and-pitch-sound.md)
**Status:** `Done`

## Goal

`Assets/Instruments/Trombone/Scripts/Trombone.cs` 신규 컴포넌트를 `InstrumentBase` 상속으로 작성해 Trombone.prefab `Trombone` root에 부착하고, 동일 root에 `InstrumentAudioOutput`을 부착한다. 왼손 Grip rising edge → `TriggerMidi(NoteOn, note=60, vel=1.0)`로 C4 발음 시작, falling edge → `TriggerMidi(NoteOff)`로 정지. LateUpdate에서 `Slide.localPosition.x → t → semitones=Lerp(0,-6,t) → pitchRatio=2^(semitones/12)`을 계산해 활성 voice의 `AudioSource.pitch`를 매 프레임 갱신해 글리산도가 들리도록 한다. `InstrumentAudioOutput`에 "활성 voice pitch 갱신" 진입점 1개를 추가하고, `TromboneAnchor.IsAttached == false`이면 새 NoteOn을 발급하지 않는다. C4.wav는 `Assets/Instruments/Trombone/Sound/C4.wav` (사용자가 이미 제공, GUID `f26d0d954cda956439e8fca5f0a2daa9`)를 `soundClips`에 wire한다.

## Context

본 plan은 트럼본 피처의 마지막 sub-spec (03 — Blow & Continuous Pitch Sound) 을 구현한다. 01·02 plan handoff가 박제한 사실은 다음과 같다.

- `TromboneAnchor.IsAttached` (read-only property)가 외부 노출돼 있어 03 sub-spec이 attach 신호로 사용 가능 (02 handoff).
- `TromboneSlideController`가 매 LateUpdate에 `Slide.localPosition.x`를 write 운전 — 03은 *read only*. 충돌 회피 (02 handoff).
- 오른손 `XRI Right Interaction/Select Value`는 02가 점유. 03은 대칭 `XRI Left Interaction/Select Value` 사용 (02 handoff).
- `TromboneSlideController`가 `[DefaultExecutionOrder(10005)]` 슬롯 점유. 03 컴포넌트는 *Slide.x를 read만 하므로 같은 슬롯 또는 더 늦은 슬롯 어느 쪽이든 안전*하지만, write 후 read가 같은 프레임에 보장되려면 slide write보다 *늦게* 실행돼야 한다 — 본 plan은 `[DefaultExecutionOrder(10006)]` 부착 (slide(10005) < trombone(10006) < PlayHandPoseDriver(10010)).

ARD 02 (`docs/specs/trombone/decisions/02-pitch-resolve-strategy.md`) 결정은 NoteOn 시 clip 재생 + 매 프레임 `AudioSource.pitch` 직접 운전. ARD 02 §Consequences가 "InstrumentAudioOutput에 active voice의 AudioSource.pitch를 외부에서 갱신할 수 있는 진입점 1개 추가"를 박제했다. 본 plan은 `InstrumentAudioOutput.cs`에 `public bool TrySetActiveVoicePitch(int note, float pitch)` public method 1개를 추가한다 (callback 패턴보다 단순 + Piano 같은 시블링은 호출 안 함 → 무영향). 새 메서드는 `GetOldestVoiceForNote(note)`로 voice를 찾아 `voice.State == VoiceState.Active`인 경우에만 `voice.Source.pitch`를 set + true 반환. Releasing 또는 Idle 상태이면 false 반환 (NoteOff 후 fade-out 동안에는 pitch가 더 이상 변하지 않음 — 사용자가 grip을 떼면 그 시점 pitch로 fade).

ARD 03 (`docs/specs/trombone/decisions/03-base-tone-asset-convention.md`) 결정은 `Sound/<NoteName>.wav` 파일명=노트명 컨벤션. 본 plan은 `TryResolveNoteOn` 안에서 `baseToneClip` (SerializeField AudioClip 1개, prefab에 C4.wav wire) 을 직접 사용해 단일 클립 발음. Piano처럼 옥타브 분기는 *없음* (베이스 톤 1개 + pitch shift만). `baseToneMidiNote` SerializeField (default 60=C4)도 함께 박제 — 추후 사용자가 wav 파일명을 바꾸면 이 필드만 일치시키면 매핑이 따라간다 (ARD 03 §Consequences).

발음 라이프사이클은 다음 4 분기 (sub-spec §Behavior 4건 + invariant 4건):

```
LateUpdate:
  // 1. attach 안 되어 있으면 active voice가 떠 있으면 강제 NoteOff + early return
  if (!tromboneAnchor.IsAttached):
    if (m_IsBlowing): SendNoteOff(); m_IsBlowing = false
    return

  // 2. 왼손 Grip 폴링
  bool grip = leftGripAction.action.ReadValue<float>() >= gripThreshold

  // 3. rising edge → NoteOn
  if (grip && !m_IsBlowing):
    float pitch = ComputePitchFromSlide()
    TriggerMidi(new MidiEvent(baseToneMidiNote, 1.0f, NoteOn))
    m_IsBlowing = true

  // 4. falling edge → NoteOff
  else if (!grip && m_IsBlowing):
    TriggerMidi(new MidiEvent(baseToneMidiNote, 0f, NoteOff))
    m_IsBlowing = false

  // 5. holding → 매 프레임 active voice pitch 갱신
  if (m_IsBlowing):
    audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, ComputePitchFromSlide())
```

`ComputePitchFromSlide()`는 다음 식 (Tech Spec §Data/Control Flow):

```
t = Mathf.InverseLerp(slideMinX, slideMaxX, slide.localPosition.x)
semitones = Mathf.Lerp(0f, -6f, t)
return Mathf.Pow(2f, semitones / 12f)
```

Slide의 min/max는 02 plan이 prefab에 박제한 `slideMinX=-0.7f, slideMaxX=-0.3f`와 동일 값을 본 컴포넌트도 SerializeField로 받아 동일 prefab값으로 wire한다 (02 컴포넌트 필드를 *참조*하지 않고 *자체 SerializeField 동일 값* — 두 컴포넌트가 서로 read-only로 같은 prefab값을 본다). 02가 prefab에서 min/max를 조정해도 사용자가 본 컴포넌트의 동일 필드도 같이 조정해야 함 — Notes에 박제.

## Verified Structural Assumptions

- Trombone.prefab `Trombone` root (GameObject fileID `3300842808265667475`, Transform fileID `6792978486195072924`) — 현재 component는 Transform 1개만. `InstrumentBase` 상속 컴포넌트 부재, `InstrumentAudioOutput` 부재. 본 plan이 두 컴포넌트를 모두 root에 부착. 출처: `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-17, lines 771-804)`.
- Trombone.prefab `Slide` GameObject (Transform fileID `7663274629528727135`, localPosition.x=`-0.58953875`, localScale (`116.5, 97.8, 115.9`)) — 02 plan이 `TromboneSlideController` 부착 + min/max 박제 (`-0.7/-0.3`). 본 plan은 같은 Transform을 *read only* (slide.localPosition.x). 출처: `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-17, lines 2035-2069, 2138-2146)`.
- `Assets/Instruments/Trombone/Sound/C4.wav` 자산 존재 + GUID `f26d0d954cda956439e8fca5f0a2daa9`. 본 plan이 `soundClips[0]`에 wire. 출처: `Read Assets/Instruments/Trombone/Sound/C4.wav.meta (2026-05-17)`.
- `InstrumentBase.TriggerMidi(MidiEvent)` 동작 요약 (호출 외부 API 박제, *동작 전체*):
  - `audioOutput == null`이면 early return.
  - `NoteOn`: `TryResolveNoteOn(midiEvent, out playback)` → success면 `audioOutput.PlayNote(midiEvent.Note, playback.Clip, playback.Pitch, playback.Volume * instanceVolume)`.
  - `NoteOff`: `OnNoteOff(midiEvent)` (virtual hook) → `audioOutput.StopNote(midiEvent.Note)`.
  - `Choke`: `OnChoke(midiEvent)` → `audioOutput.StopNoteImmediate(midiEvent.Note)` (default).
  - 분기 후 항상 `MidiTriggered?.Invoke(midiEvent)` (NoteOn/Off 둘 다 외부 구독자에 전파).
  - `Awake`: `instrumentId` 비어있지 않으면 `InstanceVolumeStore.Active.Load`로 instanceVolume 복원 + `audioOutput`이 null이면 `GetComponentInChildren<InstrumentAudioOutput>(true)` 자동 탐색 + `Initialize()` 호출.
  - `Initialize`: `audioOutput == null`이면 `Debug.LogError` 후 `enabled=false`. 본 plan은 audioOutput을 *같은 root*에 부착하므로 GetComponentInChildren이 자기 자신을 포함해 탐색 → resolve 성공.
  - `OnDisable`: `audioOutput.StopAllVoices()` — anchor detach 시 컴포넌트를 disable하면 모든 voice가 멈춤. 본 plan은 컴포넌트 disable 패턴 대신 LateUpdate 분기에서 강제 NoteOff (위 §Context 분기 1).
  - 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-17, lines 1-186)`.
- `InstrumentAudioOutput.PlayNote / StopNote / GetOldestVoiceForNote` 동작 요약 (호출 외부 API 박제, *동작 전체*):
  - `PlayNote(note, clip, pitch, volume)`: `GetBestVoice()` (Idle 우선, 없으면 oldest) → `StopVoice(voice)` 강제 stop → `Source.clip/pitch/volume/loop=false/Play()` + voice.Note=note, State=Active, StartedAt=Time.time.
  - `StopNote(note)`: `GetOldestVoiceForNote(note)`로 Active|Releasing 중 oldest 1개 찾아 release fade 시작 (Update 루프가 fade 후 `StopVoice`).
  - `Update`: Releasing 상태 voice 전부 fade 진행 (`Source.volume = ReleaseStartVolume * (1 - elapsed/ReleaseDuration)`). 즉 NoteOff 후 본 plan의 pitch 갱신은 Releasing voice에는 영향 없도록 (Active 상태일 때만 갱신) 처리.
  - `GetOldestVoiceForNote(note)`: voice.Note == note + Source != null + State != Idle 조건 — Active와 Releasing 둘 다 후보. 본 plan의 `TrySetActiveVoicePitch`는 추가 분기로 `State == Active`만 허용.
  - `OnDisable`: `StopAllVoices()` — 컴포넌트 비활성화 시 즉시 모든 voice 정지.
  - 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-17, lines 1-216)`.
- XRI Default Input Actions의 `XRI Left Interaction/Select Value` 액션 (Value/Axis float, `<XRController>{LeftHand}/{Grip}`) — 본 plan의 `leftGripAction` InputActionReference 대상. asset GUID `c348712bda248c246b8c49b3db54643f`, ActionMap 이름 `XRI Left Interaction`, Action 이름 `Select Value`. 출처: `Read Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/XRI Default Input Actions.inputactions (2026-05-17, lines 868-888)` + `Read .../XRI Default Input Actions.inputactions.meta (2026-05-17)`.
- `Instruments.asmdef` references = `[Unity.InputSystem, Hands, Unity.XR.Interaction.Toolkit]`. 본 plan의 신규 `Trombone.cs`가 `using UnityEngine.InputSystem;` (InputActionReference) 만 추가 import. `Hands` reference는 직접 사용 안 하지만 02의 `TromboneSlideController`가 이미 점유 — 충돌 없음. `Assets/Instruments/Trombone/Scripts/`에 별도 `.asmdef`가 없어 가장 가까운 상위 `Assets/Instruments/Instruments.asmdef`에 자동 포함. 신규 reference 추가 불필요. 출처: `Read Assets/Instruments/Instruments.asmdef (2026-05-17)`.
- `MidiEvent` 생성자 시그니처: `new MidiEvent(int Note, float Velocity, MidiEventType Type)`. Piano.cs line 62·70에서 동일 사용 사례 — 본 plan도 동일 형태로 호출. 출처: `Read Assets/Instruments/Piano/Scripts/Piano.cs (2026-05-17, lines 62, 70)`.
- 02 plan handoff의 "TromboneSlideController가 매 LateUpdate Slide.localPosition.x를 write" + `[DefaultExecutionOrder(10005)]` 점유 — 본 plan은 `[DefaultExecutionOrder(10006)]`로 1 슬롯 늦게 실행해 같은 프레임 안에서 02가 write한 결과를 read 보장. 출처: 02 plan §Handoff + `Read Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs (2026-05-17, line 11)`.
- Piano.prefab의 `Piano` root에서 InstrumentBase 상속 컴포넌트가 `audioOutput` SerializeField로 같은 root의 `InstrumentAudioOutput`을 가리키는 패턴 + `soundClips` (AudioClip[]) + `laneConfig: {fileID: 0}` (null 허용) + `instrumentId: "Piano"` + `instanceVolume: 0.5` + `_panelAnchor` (자식 Transform) wiring 박제. 본 plan은 동일 패턴 답습 (Trombone root에 둘 다 부착, soundClips=[C4.wav], laneConfig=null, instrumentId="Trombone", instanceVolume=0.5, _panelAnchor=null). 출처: `Read Assets/Instruments/Piano/Prefabs/Piano.prefab (2026-05-17, lines 16699-16732)`.

## Approach

1. **`Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` 수정 — 활성 voice pitch 갱신 진입점 추가**
   - `public bool TrySetActiveVoicePitch(int note, float pitch)` 메서드 신규. 본문:
     ```csharp
     public bool TrySetActiveVoicePitch(int note, float pitch)
     {
         Voice voice = GetOldestVoiceForNote(note);
         if (voice == null || voice.Source == null) return false;
         if (voice.State != VoiceState.Active) return false;
         voice.Source.pitch = pitch;
         return true;
     }
     ```
   - 위치: `StopNoteImmediate` 와 `StopAllVoices` 사이 (논리적으로 "active voice 상태 조작" 그룹).
   - 시그니처 변경 없음 — Piano/DrumKit 등 시블링 영향 0. ARD 02 §Consequences가 직접 박제한 변경.

2. **`Assets/Instruments/Trombone/Scripts/Trombone.cs` 신규 작성**
   - `namespace Instruments`.
   - `[DefaultExecutionOrder(10006)]` (slide 10005 다음, PlayHandPoseDriver 10010 이전).
   - `[DisallowMultipleComponent]`.
   - `public sealed class Trombone : InstrumentBase`.
   - SerializeField:
     - `TromboneAnchor tromboneAnchor` — attach 신호 source.
     - `Transform slide` — slide.localPosition.x read source.
     - `AudioClip baseToneClip` — 단일 베이스 톤 (C4.wav).
     - `int baseToneMidiNote = 60` — C4 MIDI 노트 번호. 사용자가 다른 음정 wav로 바꾸면 이 필드만 일치시키면 됨 (ARD 03 §Consequences).
     - `InputActionReference leftGripAction` — `XRI Left Interaction/Select Value`.
     - `float slideMinX = -0.7f` — 02 prefab default와 동일.
     - `float slideMaxX = -0.3f` — 02 prefab default와 동일.
     - `float gripThreshold = 0.5f` — XRI default press point.
   - 내부 상태: `bool m_IsBlowing`.
   - `protected override void Awake()`:
     - `base.Awake()` 호출 (InstrumentBase가 audioOutput auto-resolve + Initialize).
   - `void OnEnable()`: `leftGripAction?.action?.Enable();`
   - `void OnDisable()`: `leftGripAction?.action?.Disable();` 그리고 `base.OnDisable()` 가 `audioOutput.StopAllVoices()` 호출하므로 m_IsBlowing 잔존 상태 reset.
   - `void LateUpdate()`: 위 §Context 분기 5 단계 그대로 구현.
   - `float ComputePitchFromSlide()`: `if (slide == null) return 1f; t = InverseLerp(slideMinX, slideMaxX, slide.localPosition.x); semitones = Lerp(0, -6, t); return Pow(2, semitones/12);`. `slideMinX > slideMaxX` 또는 둘 같음이면 InverseLerp가 0 반환 → semitones=0, pitch=1.
   - `protected override bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback)`:
     ```csharp
     playback = default;
     if (baseToneClip == null) return false;
     // NoteOn 발급 시점 pitch는 슬라이드 현재 위치로 계산.
     float pitch = ComputePitchFromSlide();
     playback = new NotePlayback(baseToneClip, pitch, midiEvent.Velocity);
     return true;
     ```
     - Piano와 달리 AudioBank (`TryGetAudioBank`) 를 사용하지 *않는다* — `baseToneClip` SerializeField에 직접 wire한 단일 클립을 직접 사용. ARD 03이 "단일 클립 + 파일명=노트명" 컨벤션으로 결정했고 베이스 클립이 1개뿐이라 bank dictionary가 과잉. (단, `soundClips` SerializeField는 InstrumentBase의 base 필드 그대로 두고 사용자가 Inspector에서 `[C4.wav]`를 같이 wire — 추후 다른 plan이 AudioBank로 전환할 여지를 남김.)
   - Public API는 추가하지 않음 (Piano의 `NoteOn(keyIndex, velocity)` public 메서드 대응은 트럼본에는 불필요 — 외부 입력 source가 본 컴포넌트의 SerializeField로 wire된 left grip 만이라 LateUpdate가 단일 진입점).

3. **Trombone.prefab `Trombone` root에 컴포넌트 부착** (manage_gameobject MCP, `precondition_sha256` 보호)
   - `InstrumentAudioOutput` AddComponent. SerializeField:
     - `voiceMixerGroup`: null (사용자 추후 wire 또는 Piano.prefab와 동일한 mixer group 사용 — 본 plan 범위 외).
   - `Trombone` AddComponent. SerializeField:
     - `audioOutput` (InstrumentBase 상속 필드): 같은 root의 `InstrumentAudioOutput` reference.
     - `soundClips`: `[C4.wav]` (GUID `f26d0d954cda956439e8fca5f0a2daa9`).
     - `laneConfig`: null (Tech Spec §Boundaries — laneConfig SO 1개 표면 노출은 본 plan에서 선택적, 빈 SO 1개 부착도 가능하나 null도 InstrumentLaneConfig.cs 안 안전 동작 — sub-spec §Out of Scope의 "실제 lane 채우기는 후속 spec"에 의해 빈 채로 출발).
     - `instrumentId`: `"Trombone"`.
     - `instanceVolume`: `0.5`.
     - `_panelAnchor`: null (본 plan은 패널 UI 다루지 않음).
     - `tromboneAnchor`: prefab 단계 null (scene 단계에서 wire).
     - `slide`: prefab의 Slide Transform (fileID 7663274629528727135).
     - `baseToneClip`: C4.wav AudioClip reference (GUID `f26d0d954cda956439e8fca5f0a2daa9`).
     - `baseToneMidiNote`: 60.
     - `leftGripAction`: XRI Default Input Actions의 `XRI Left Interaction/Select Value` InputActionReference (asset GUID `c348712bda248c246b8c49b3db54643f`, action ID 발견 라인 number 881 (`Select Value`) — 정확한 InputActionReference fileID는 prefab 박제 단계에서 MCP가 asset에서 발급. 02 plan의 `rightGripAction` wiring 형식 답습: `{fileID: <neg-long>, guid: c348712bda248c246b8c49b3db54643f, type: 3}`).
     - `slideMinX`, `slideMaxX`: `-0.7`, `-0.3` (02와 동일).
     - `gripThreshold`: `0.5`.

4. **TestSceneSanyo `Trombone` PrefabInstance에 `tromboneAnchor` wiring 보완**
   - 02 plan이 이미 `TromboneSlideController.tromboneAnchor` modification으로 scene `TromboneAnchor` GameObject를 wire한 패턴 답습.
   - 본 plan은 `Trombone` 컴포넌트의 `tromboneAnchor` 필드를 같은 scene `TromboneAnchor` reference로 wire (m_Modifications 항목 추가, manage_gameobject MCP 또는 Tech Spec §Boundaries가 허용한 직접 텍스트 Edit).
   - drum-kit / 02 plan의 wiring 형식 답습 — `target: {fileID: <component-fileID>, guid: <Trombone.prefab GUID>, type: 3}, propertyPath: tromboneAnchor, value: , objectReference: {fileID: <scene TromboneAnchor 컴포넌트 fileID>}`.

5. **검증 단계** (Unity MCP workflow는 [`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md) 따름)
   - 컴파일 대기 후 `read_console types=[error] count=20` → 0 error.
   - EditMode 회귀: `unity-test-runner` 1회 호출 — InstrumentBase / InstrumentAudioOutput 사용 시블링 (Piano / DrumKit) 테스트 회귀 없음 확인.
   - Editor Play 모드 시각 검증 (AC 8-13).

## Deliverables

- `Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` — `public bool TrySetActiveVoicePitch(int note, float pitch)` 메서드 1개 추가.
- `Assets/Instruments/Trombone/Scripts/Trombone.cs` — 신규. `InstrumentBase` 상속, 왼손 Grip rising/falling edge → NoteOn/NoteOff, LateUpdate에서 활성 voice pitch 갱신, `TryResolveNoteOn` 구현.
- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` — `Trombone` root에 `InstrumentAudioOutput` + `Trombone` AddComponent + SerializeField wiring (soundClips=[C4.wav], baseToneClip=C4.wav, baseToneMidiNote=60, leftGripAction=XRI Left Interaction/Select Value, slide=Slide Transform, slideMinX/Max/gripThreshold, instrumentId="Trombone", instanceVolume=0.5).
- `Assets/Scenes/TestSceneSanyo.unity` — Trombone PrefabInstance의 m_Modifications에 `tromboneAnchor` (scene TromboneAnchor 컴포넌트 reference) 1개 항목 추가.

## Acceptance Criteria

- [x] `[auto-hard]` `Trombone.cs`가 `Assets/Instruments/Trombone/Scripts/`에 존재하고 `namespace Instruments`, `public sealed class Trombone : InstrumentBase`, `[DefaultExecutionOrder(10006)]`, `[DisallowMultipleComponent]`가 모두 부착돼 있다.
  **검증:** `Grep -n "namespace Instruments|class Trombone : InstrumentBase|DefaultExecutionOrder\(10006\)|DisallowMultipleComponent" Assets/Instruments/Trombone/Scripts/Trombone.cs` 결과 4 매칭.
- [x] `[auto-hard]` `Trombone.cs`가 `TryResolveNoteOn` override를 구현하고, NoteOn 시점에 `ComputePitchFromSlide()` 결과를 `NotePlayback`의 pitch로 전달한다.
  **검증:** `Grep -n "override bool TryResolveNoteOn|new NotePlayback\(baseToneClip,\s*ComputePitchFromSlide\(\)|new NotePlayback\(baseToneClip,\s*pitch" Assets/Instruments/Trombone/Scripts/Trombone.cs` 결과 ≥ 2 매칭 (override 시그니처 + NotePlayback 생성 라인).
- [x] `[auto-hard]` `Trombone.cs`의 `ComputePitchFromSlide()`가 `Mathf.InverseLerp(slideMinX, slideMaxX, slide.localPosition.x)` + `Mathf.Lerp(0`, `-6`, t)` + `Mathf.Pow(2`, semitones/12)` 3 식을 모두 단일 메서드 안에 포함한다 — Tech Spec §Data/Control Flow 식이 정확히 박제됐는지 검증.
  **검증:** `Grep -nE "InverseLerp.*slideMinX.*slideMaxX|Mathf\.Lerp\(0f?,\s*-6f?,|Mathf\.Pow\(2f?," Assets/Instruments/Trombone/Scripts/Trombone.cs` 결과 정확히 3 매칭 + 모두 같은 메서드 본문(`ComputePitchFromSlide`) 안.
- [x] `[auto-hard]` `Trombone.cs`가 LateUpdate에서 (a) `tromboneAnchor.IsAttached == false`이면 active blowing을 NoteOff로 강제 종료하고 early return, (b) rising edge에 `TriggerMidi(NoteOn)` 1회 발급, (c) falling edge에 `TriggerMidi(NoteOff)` 1회 발급, (d) blowing 동안 `audioOutput.TrySetActiveVoicePitch(baseToneMidiNote, ComputePitchFromSlide())` 갱신을 매 프레임 수행한다.
  **검증:** `Grep -nE "!tromboneAnchor.IsAttached|MidiEventType\.NoteOn|MidiEventType\.NoteOff|TrySetActiveVoicePitch" Assets/Instruments/Trombone/Scripts/Trombone.cs` 결과 ≥ 4 매칭 + 각각 LateUpdate 또는 호출 메서드 안.
- [x] `[auto-hard]` `InstrumentAudioOutput.cs`에 `public bool TrySetActiveVoicePitch(int note, float pitch)` 메서드가 추가됐고, 본문이 `voice.State == VoiceState.Active`일 때만 `Source.pitch`를 set한다 (Releasing/Idle voice는 갱신 안 함).
  **검증:** `Grep -nE "public bool TrySetActiveVoicePitch\(int note, float pitch\)|voice\.State != VoiceState\.Active|voice\.Source\.pitch = pitch" Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` 결과 ≥ 3 매칭.
- [x] `[auto-hard]` Trombone.prefab `Trombone` root GameObject (fileID `3300842808265667475`)가 `InstrumentAudioOutput` + `Trombone` 두 컴포넌트를 포함하고, `Trombone`의 SerializeField `soundClips`, `baseToneClip`, `baseToneMidiNote`, `leftGripAction`, `slide`, `slideMinX`, `slideMaxX`, `gripThreshold`, `instrumentId`, `instanceVolume`이 모두 직렬화돼 있다.
  **검증:** `Grep -nE "m_EditorClassIdentifier:.*InstrumentAudioOutput|m_EditorClassIdentifier:.*Trombone$|baseToneClip:|baseToneMidiNote:|leftGripAction:|instrumentId: Trombone|soundClips:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과 ≥ 7 매칭. + `baseToneClip:` 라인 다음에 `guid: f26d0d954cda956439e8fca5f0a2daa9` (C4.wav GUID) 등장.
- [x] `[auto-soft]` Unity Editor 컴파일 0 error (`read_console action=get types=[error] count=20`). EditMode 회귀 (`unity-test-runner`) Piano / DrumKit / InstrumentAudioOutput 관련 기존 테스트 회귀 없음.
  **검증:** `read_console action=get types=[error] count=20` → Trombone/InstrumentAudioOutput 관련 컴파일 에러 0 건 + `editor_state.isCompiling == false` + `unity-test-runner` EditMode 결과 `failed=0` (직전 02 plan 기준 `EditMode 102/102 pass`).
- [ ] `[manual-hard]` Editor Play 모드에서 TromboneAnchor로 텔레포트 → anchor 진입 + 왼손 Grip 누름 → C4 (또는 슬라이드 위치에 따른 음정)이 즉시 들리기 시작.
  **검증:** Play 모드 진입 → trombone anchor 텔레포트 → 슬라이드를 최소 위치(`slideMinX` 근처)에 두고 왼손 grip 누름 → 약 261.6 Hz (C4) 음이 들리는지 청각 확인 + Inspector에서 `InstrumentAudioOutput` 자식 `VoicePool_Trombone`의 AudioSource 중 1개가 `isPlaying=true`인지 확인.
- [ ] `[manual-hard]` 왼손 Grip 홀드 동안 오른손 Grip으로 슬라이드를 좌→우로 천천히 이동 → 음정이 *끊김 없이* 글리산도로 변화 (clip이 멈췄다가 다시 재생되는 일 없음). 슬라이드 끝(`slideMaxX`)에서 F#3 (~185 Hz, C4 - 6반음)이 들린다.
  **검증:** Play 모드에서 anchor 진입 + 왼손 grip 홀드 + 오른손 grip으로 slide 풀 스윙 → 음이 한 음원으로 연속 변화 (clip restart 없음) + 끝 위치에서 약 185 Hz (F#3) 청각 확인 + Inspector에서 voice 1개의 `pitch` 필드가 `~0.707`로 변하는지 확인.
- [ ] `[manual-hard]` 왼손 Grip을 떼는 순간 음이 release fade로 정지 (즉시 cut 아님 — `ReleaseDuration=0.1f`).
  **검증:** Play 모드에서 grip 홀드 발음 중 grip 뗌 → 약 100ms fade-out 후 정적 + Inspector에서 voice의 `State`가 `Releasing` → `Idle`로 전환되는지 확인.
- [ ] `[manual-hard]` anchor 외부 위치 (detach 상태)에서 왼손 Grip을 눌러도 어떤 음도 들리지 않는다.
  **검증:** Play 모드에서 detach 상태 (anchor 외부 텔레포트) → 왼손 grip 누름 → 정적 + `VoicePool_Trombone`의 모든 voice `isPlaying=false`인지 확인.
- [ ] `[manual-hard]` anchor 진입 + 왼손 Grip 홀드 발음 중인 상태에서 다른 anchor 외부로 텔레포트 (detach) → 음이 즉시 NoteOff fade로 종료 (anchor 안 입력 잠금 분기가 active blowing도 같이 종료).
  **검증:** Play 모드에서 grip 홀드 발음 중 다른 위치로 텔레포트 → trombone이 원위치 복귀 + 음이 release fade로 종료 + Inspector voice State가 `Releasing` → `Idle` 전환 확인.
- [ ] `[manual-hard]` `Trombone` 컴포넌트의 `MidiTriggered` 이벤트가 NoteOn/NoteOff 발급 시 정상 발화한다 (RhythmGame 연동 표면 검증).
  **검증:** Play 모드 진입 전 임시 디버그 스크립트 1개로 `FindObjectOfType<Trombone>().MidiTriggered += ev => Debug.Log(...)` 구독 → grip 누름/뗌마다 `NoteOn note=60` / `NoteOff note=60` Console 로그 1쌍 등장 (디버그 스크립트는 검증 후 제거).

## Out of Scope

- `slideMinX` / `slideMaxX` 정확한 수치 튜닝 — 02와 동일 임시값 답습. 사용자가 헤드셋 시각 검증 후 prefab Inspector로 조정 (02와 *별도* 필드이므로 사용자가 두 컴포넌트의 동일 값을 함께 조정해야 함 — Notes에 박제).
- 다른 음정 베이스 톤 wav 지원 (A3.wav 등 사용자가 다른 파일로 교체) — `baseToneMidiNote` SerializeField로 표면은 노출되지만, 실제 다른 파일 매핑 검증은 사용자가 wav 파일을 교체하고 `baseToneMidiNote` 값을 수정한 후 확인. 본 plan은 default C4 (=60) 만 검증.
- `InstrumentLaneConfig` SO 실제 lane 매핑 (sub-spec §Out of Scope — laneConfig는 빈 또는 null 채로 표면만 노출).
- RhythmGame 연동의 실제 판정/lane 운전 (sub-spec §Out of Scope, MidiTriggered 발화만 검증).
- ControlChange 파이프라인을 통한 pitch 변화 전파 — ARD 02가 직접 운전 모델로 결정, 추후 RhythmGame 멀티플레이어에서 재평가.
- 다양한 표현 (velocity 변화, 입술 압력 시뮬레이션, 비브라토) — sub-spec §Out of Scope.
- 슬라이드 7 포지션 snap 또는 반음 quantize — sub-spec §Out of Scope.
- 슬라이드 운전 컴포넌트(`TromboneSlideController`)의 동작 변경 — 02 plan handoff "Slide.localPosition.x는 03이 read만" 답습.
- 다른 트럼본 인스턴스의 멀티플레이어 동기화.

## Notes

- `slideMinX/slideMaxX`를 02 컴포넌트(`TromboneSlideController`)에서 *참조*하지 않고 *동일 값 별도 SerializeField*로 박제한 이유: 두 컴포넌트가 한 컴포넌트의 직렬화 슬롯을 공유하면 invariant 위반 시 한 곳 수정으로 양쪽이 깨지지만, 한쪽이 prefab Inspector에서 보이지 않으면 사용자가 의도 위반을 모른 채 *한 쪽만* 조정하면 매핑이 어긋난다. 02·03 두 SerializeField가 *동일 default 값*으로 prefab에 박제되면 Inspector에서 두 값이 한 화면에 보여 동시 조정이 시각화. 향후 SO 하나로 통합하는 plan은 본 plan 범위 외.
- `[DefaultExecutionOrder(10006)]`는 02의 10005 다음 1 슬롯 늦게 — 같은 프레임의 slide write → trombone read 순서 보장. PlayHandPoseDriver의 10010보다는 여전히 빠르므로 PlayHand 시각 정합에는 영향 없음. 02 plan §Notes의 "같은 슬롯 안에서 Unity가 deterministic 순서를 보장하지 않음" 함정 회피.
- `TryResolveNoteOn`이 InstrumentBase의 AudioBank (`TryGetAudioBank`)를 사용하지 않고 SerializeField `baseToneClip`을 직접 사용한 이유: 단일 베이스 톤이라 dictionary lookup이 과잉이고, AudioBank는 clip.name → AudioClip mapping이라 wav 파일명이 `C4`가 아닌 경우 (사용자가 `Trombone_BaseTone.wav`로 이름 변경) 매핑이 깨질 위험이 있다. SerializeField 직접 참조는 파일명 무관하게 동작. ARD 03의 "파일명=노트명" 컨벤션은 사용자가 *어떤 노트인지 즉시 파악*할 수 있게 하는 *사용자 인지용*이고, 런타임 매핑은 `baseToneMidiNote` SerializeField로 분리.
- `soundClips` SerializeField는 InstrumentBase의 base 필드 그대로 두고 사용자가 `[C4.wav]`를 함께 wire — Trombone 컴포넌트가 직접 쓰지는 않지만 추후 plan이 AudioBank로 전환할 때 무수정 동작. `instrumentId="Trombone"`은 RhythmGame 연동 + InstanceVolume 영속화 키로 사용 (Piano와 동일 패턴).
- `OnNoteOff` virtual hook은 override하지 않음 — InstrumentBase default 동작 (`audioOutput.StopNote`)이 정확히 본 plan이 원하는 release fade 동작. virtual hook이 호출되기 전에 활성 voice가 종료 처리되므로 `TrySetActiveVoicePitch`가 NoteOff 직후 Releasing voice에 호출돼도 (위 InstrumentAudioOutput 동작 박제) `voice.State != Active` 분기로 false 반환 — 안전.
- 사용자가 다른 음정 wav를 제공해 `baseToneMidiNote`를 변경하면 슬라이드 max 위치의 음정도 따라 이동 (예: A3 wav + `baseToneMidiNote=57` → 슬라이드 min=A3, slide max=Eb3). sub-spec의 "C4↔F#3 매핑"은 default wav 가정한 *결과 표면*이고, 본 plan의 식 (`pitch=Pow(2, Lerp(0,-6,t)/12)`)은 *항상 6반음 하행*이라 wav 변경에 일관 동작.
- 컨텍스트 절약을 위해 02 plan의 시각 검증 manual-hard 항목은 본 plan에서 재검증하지 않음 (02 plan handoff에 검증 결과 박제됨). 03의 manual-hard는 03 신규 영역만 다룸.
- 자동 검증 결과:
  - 자동 AC 6 (auto-hard 5 + auto-soft 1 — 또는 plan 작성 시 6개로 분류, 결과적으로 모두 PASS): Grep evidence 6 종 + InstrumentBase 시그니처 (`MidiEvent` 3-arg constructor, `OnDisable` virtual override) 정합 확인.
  - `unity-test-runner`: EditMode 102/102 pass, PlayMode 4/4 pass, Console errors 0. Piano / DrumKit 시블링 회귀 없음.
- Manual-hard 6 개 (AC 8-13) 는 사용자 헤드셋 청각/시각 검증 필요. `/spec-build` 자동 진행 종료 후 사용자가 헤드셋에서 일괄 검증 예정.

## Handoff

(plan 완료 후 `/spec-build`의 doc-updater가 자동 갱신할 예정. 본 plan은 트럼본 피처의 *마지막* sub-spec이므로 다음 sub-spec은 없으나, 후속 RhythmGame 연동 plan이 다음 정보를 참조할 수 있다.)

- **Trombone 컴포넌트 위치** — `Trombone.prefab` root에 `Trombone` (InstrumentBase 상속) + `InstrumentAudioOutput` 두 컴포넌트 부착. `instrumentId="Trombone"`.
- **MidiTriggered 이벤트 발화 시점** — 왼손 Grip rising/falling edge 각 1회. NoteOn (note=60, vel=1.0) / NoteOff (note=60, vel=0). `m_IsBlowing` 상태에 의해 edge 이벤트 1회만 발급 보장.
- **pitch 운전 모델** — NoteOn 시 clip 재생 후 매 프레임 `InstrumentAudioOutput.TrySetActiveVoicePitch(60, pitch)` 직접 운전. MidiTriggered는 NoteOn/NoteOff만 발급 — 발음 중 pitch 변화는 외부에서 추적 불가 (ARD 02 §Consequences).
- **새 InstrumentAudioOutput API** — `public bool TrySetActiveVoicePitch(int note, float pitch)` 추가. Active voice만 갱신 (Releasing/Idle은 false 반환). Piano/DrumKit는 호출하지 않으므로 시블링 영향 0.
- **baseToneMidiNote SerializeField** — default 60 (C4). 사용자가 wav 파일을 다른 음정으로 바꿀 때 이 필드만 일치시키면 매핑 자동 follow. 슬라이드 max는 항상 6반음 하행.
- **slideMinX/Max 박제값** — `-0.7 / -0.3` (02와 동일 default). 02·03 각각의 SerializeField로 별도 박제 — 사용자가 prefab Inspector에서 *두 컴포넌트 모두* 같은 값으로 조정해야 매핑 정합. 향후 통합 SO plan 후보.
- **LaneConfig** — 본 plan은 null로 둠. RhythmGame 연동 plan은 `Trombone` 인스턴스에 `InstrumentLaneConfig` SO 1개를 wire (예: laneIndex=0 → midiNote=60) 하면 Note Display 패널 연결.
