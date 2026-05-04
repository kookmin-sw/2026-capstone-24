# Session Panel Volume Section

**Linked Spec:** [`03-volume-section.md`](../specs/03-volume-section.md)
**Status:** `Ready`

## Goal

세션 패널의 볼륨 섹션을 채운다. 마스터 볼륨 + 활성 악기 인스턴스 볼륨 슬라이더를 만들고, AudioMixer 신설로 마스터 라우팅을, `InstrumentBase`에 인스턴스 볼륨 필드 추가로 인스턴스 단위 적용을 구현하며, 두 값 모두 PlayerPrefs로 persist한다.

## Context

session-panel `03-volume-section` sub-spec의 첫 plan이다. spec [`03-volume-section.md`](../specs/03-volume-section.md)이 정의한 핵심:

- 볼륨 0~1 선형, 기본 0.5, 마지막 값 사용자 단위 persist.
- 마스터 볼륨은 항상 노출, 인스턴스 볼륨은 활성 악기가 있을 때만 노출.
- 인스턴스 볼륨은 잡힌 특정 악기 인스턴스에만 적용 (동일 종류 다른 인스턴스에 무영향).
- 멀티플레이 타 사용자 슬롯은 What 박제만, 본 plan에서 다루지 않음.

본 plan은 다음 두 가지에 의존하며 plan-implementer가 새 세션에서 받았을 때 그 산출물이 이미 존재한다고 가정한다:

- **anchoring plan(`2026-05-05-linksky0311-session-panel-anchoring`) 선행 적용.** SessionPanel.prefab의 `VolumeSectionContainer` placeholder + `IActiveInstrumentProvider` / `IActiveInstrument` 추상 인터페이스를 사용한다. spec-implement orchestrator는 sub-spec 표 순서(01 → 03)대로 자연스럽게 위상정렬한다.
- **drum/piano 잡기 wiring은 다른 작업자 plan에서 진행 중.** 본 plan의 manual 검증은 anchoring plan과 마찬가지로 dummy stub `IActiveInstrumentProvider`를 inspector로 임시 주입해서 진행한다.

코드베이스 사실 (메인 세션이 직접 grep으로 박제):

- 프로젝트에 AudioMixer 자산 0개. 모든 사운드는 `InstrumentAudioOutput`이 voice별 `AudioSource.volume` 직접 제어. 본 plan이 mixer를 신규 도입한다.
- 코드에 PlayerPrefs 호출 0건. persist 패턴을 본 plan이 도입한다.
- `InstrumentAudioOutput` 의 voice `AudioSource`는 `EnsureVoicePool()`에서 런타임 동적 `AddComponent<AudioSource>()`로 생성. `outputAudioMixerGroup` 설정도 그 시점에 적용해야 한다.
- `InstrumentBase`는 abstract MonoBehaviour. `Piano`와 `DrumKit`이 상속. `[SerializeField] protected InstrumentAudioOutput audioOutput;` 자식으로 wire된 상태.
- `InstrumentBase.GetAudioSourceSettings()`는 `virtual` — 서브클래스가 오버라이드.

## Verified Structural Assumptions

- `Assets/Instruments/Piano/Prefabs/Piano.prefab` 루트 = `Piano` GameObject (`Piano.cs : InstrumentBase`). 자식 `AudioOutput`에 `InstrumentAudioOutput` 단독 부착. `Piano.audioOutput` 필드가 이 자식의 `InstrumentAudioOutput`을 가리킴. — `unity-scene-reader 보고 (2026-05-05)`
- `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` 루트 = `DrumKit` GameObject (`DrumKit.cs : InstrumentBase`). 자식 `AudioOutput`에 `InstrumentAudioOutput` 단독 부착. 8개 piece nested prefab(BassDrum, Snare, HighTom, MidTom, FloorTom, HiHat, CrashCymbal, RideCymbal) 각각은 `DrumPiece : MonoBehaviour`로 InstrumentBase 미상속. **본 plan은 인스턴스 볼륨 슬라이더 단위를 DrumKit 1개로 통일한다 (piece별 분리는 spec Out of Scope).** — `unity-scene-reader 보고 (2026-05-05)`
- `Assets/Scenes/SampleScene.unity`의 InstrumentBase 인스턴스 수: Piano 1개 + DrumKit 1개. 같은 prefab의 인스턴스 다중화 가능성은 본 plan 범위 밖이므로 PlayerPrefs key는 prefab 이름 단독으로 한다 (`SessionPanel.Volume.Piano`, `SessionPanel.Volume.DrumKit`). — `unity-scene-reader 보고 (2026-05-05)`
- AudioMixer 자산: 코드베이스에 `.mixer` 파일 0개. 본 plan에서 `Assets/SessionPanel/Audio/SessionMixer.mixer`를 신설하며 `Master` 그룹 1개 + 외부 노출 파라미터 `MasterVolume_dB` 1개를 둔다. — `Read Assets (.mixer 파일 grep) (2026-05-05)`
- PlayerPrefs 사용 사례: 코드베이스 grep 결과 0건. 본 plan이 첫 도입. 키 prefix는 `SessionPanel.Volume.` 통일. — `Read Assets (PlayerPrefs grep) (2026-05-05)`
- `InstrumentAudioOutput.EnsureVoicePool()`(`Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs:206-227`)이 voice 별 AudioSource를 동적 AddComponent로 생성. `outputAudioMixerGroup` 설정은 voice 생성 직후 같은 위치에서 일괄 적용한다. — `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-05)`

## Approach

1. **AudioMixer 자산 신설** — `Assets/SessionPanel/Audio/SessionMixer.mixer`. 그룹 구성: `Master` 1개. 외부 노출 파라미터 `MasterVolume_dB`로 SetFloat 가능. [`unity-asset-edit`](../../../../.claude/skills/unity-asset-edit/SKILL.md) skill 절차로 MCP `manage_asset`(또는 mixer 신설 메뉴) 사용.

2. **`InstrumentAudioOutput`에 voice mixer group SerializeField 추가** — 새 필드 `[SerializeField] AudioMixerGroup voiceMixerGroup;`. `EnsureVoicePool()`에서 `AddComponent<AudioSource>()` 직후 `source.outputAudioMixerGroup = voiceMixerGroup` 설정. Piano/DrumKit prefab의 `AudioOutput` 자식에 inspector로 `SessionMixer/Master` 그룹 wire.

3. **`InstrumentBase`에 인스턴스 볼륨 + InstrumentId 추가** — 코드 변경:

   ```csharp
   [SerializeField] string instrumentId = "";
   [SerializeField, Range(0f, 1f)] float instanceVolume = 0.5f;
   public string InstrumentId => instrumentId;
   public float InstanceVolume {
       get => instanceVolume;
       set { instanceVolume = Mathf.Clamp01(value); SessionVolume.PersistInstance(instrumentId, instanceVolume); }
   }
   ```

   - `Awake`에서 PlayerPrefs 복원: `instanceVolume = SessionVolume.LoadInstance(instrumentId, defaultValue: 0.5f)`. instrumentId가 빈 문자열이면 복원 skip.
   - 기존 `TriggerMidi` / `TryResolveNoteOn` 파이프라인에서 `playback.Volume *= instanceVolume`을 한 번 곱한다 (가장 단순한 hook 위치는 NotePlayback 생성 직후).

4. **Piano/DrumKit prefab에 InstrumentId 직렬화** — Piano.prefab의 Piano 컴포넌트 `instrumentId = "Piano"`. DrumKit.prefab의 DrumKit 컴포넌트 `instrumentId = "DrumKit"`. `unity-asset-edit` skill 절차로 MCP `manage_components` 사용 (단일 propertyPath 스칼라 변경 케이스).

5. **`SessionVolume` static API 신설** — `Assets/SessionPanel/Scripts/SessionVolume.cs`. 책임: PlayerPrefs persist + AudioMixer 마스터 라우팅.

   ```csharp
   public static class SessionVolume {
       const string MasterKey = "SessionPanel.Volume.Master";
       static string InstanceKey(string id) => $"SessionPanel.Volume.{id}";
       static AudioMixer s_Mixer;
       public static void Bind(AudioMixer mixer);
       public static float Master { get; set; }                     // setter: persist + SetFloat
       public static float LoadInstance(string id, float defaultValue);
       public static void PersistInstance(string id, float value);  // PlayerPrefs.SetFloat
       static float LinearToDb(float v) => Mathf.Log10(Mathf.Max(v, 0.0001f)) * 20f;
   }
   ```

   - Master setter는 `s_Mixer.SetFloat("MasterVolume_dB", LinearToDb(value))` + `PlayerPrefs.SetFloat`.
   - `Bind`는 booting 시 1회 호출 + 첫 호출에서 PlayerPrefs 마스터 값을 mixer에 즉시 반영.

6. **`SessionVolumeBootstrap` MonoBehaviour 신설** — `Assets/SessionPanel/Scripts/SessionVolumeBootstrap.cs`. SerializeField로 `AudioMixer sessionMixer`. `Awake`에서 `SessionVolume.Bind(sessionMixer)` 호출. SampleScene의 `SessionPanelManager` GameObject(anchoring plan 산출물)에 부착해 inspector로 mixer wire.

7. **VolumeSlider UI prefab 신설** — `Assets/SessionPanel/UI/VolumeSlider.prefab`. 표준 UGUI `Slider` (Min 0, Max 1, Whole Numbers off) + 라벨 TextMeshPro 1개. World-space Canvas 안에 들어가는 자식 RectTransform.

8. **`VolumeSectionController` 스크립트 신설** — `Assets/SessionPanel/Scripts/VolumeSectionController.cs`. 책임:
   - SerializeField: `volumeSliderPrefab`, `sliderParent` (VolumeSectionContainer 자식 또는 자기 자신), `activeInstrumentProviderObject` (UnityEngine.Object → 인터페이스 캐스팅, anchoring plan 패턴 그대로).
   - `Awake`: master 슬라이더 1개 인스턴스화 + 라벨 "Master" + 초기 value = `SessionVolume.Master`. `OnValueChanged` → `SessionVolume.Master = v`.
   - `IActiveInstrumentProvider.ActiveInstrumentChanged` 구독:
     - non-null로 바뀜 → instance 슬라이더 1개 lazy 인스턴스화 + 라벨 = `current.InstrumentId` + 초기 value = 그 악기의 `InstanceVolume`. `OnValueChanged` → `current.InstanceVolume = v` (InstrumentBase 프로퍼티가 PlayerPrefs persist까지 처리).
     - null로 바뀜 → instance 슬라이더 SetActive(false).
     - 다른 악기로 swap → instance 슬라이더의 라벨/value를 새 악기 기준으로 갱신.
   - 인스턴스 슬라이더는 1개를 재사용한다 (라벨·value만 갱신).

9. **VolumeSectionContainer wiring** — anchoring plan이 만든 SessionPanel.prefab의 `VolumeSectionContainer` 자식에 `VolumeSectionController` 컴포넌트 부착 + 슬라이더 prefab + parent transform inspector wire. `unity-asset-edit` skill 절차.

10. **수동 sanity 검증을 위한 dummy IActiveInstrument 컴포넌트** — `Assets/SessionPanel/Editor/DummyActiveInstrument.cs` (또는 Tests 영역). production 코드 아님. inspector로 Current를 Piano/DrumKit InstanceBase로 직접 set 가능한 임시 컴포넌트. anchoring plan 검증용 dummy provider와 같은 결의 임시 도구. 후속 plan(잡기 wiring)이 production 구현체로 교체할 때 본 dummy를 제거.

## Deliverables

- `Assets/SessionPanel/Audio/SessionMixer.mixer` — 신설. Master 그룹 + `MasterVolume_dB` exposed parameter.
- `Assets/SessionPanel/UI/VolumeSlider.prefab` — 신설. UGUI Slider + 라벨.
- `Assets/SessionPanel/Scripts/SessionVolume.cs` — 신설. static volume API + persist + mixer 라우팅.
- `Assets/SessionPanel/Scripts/SessionVolumeBootstrap.cs` — 신설. SessionVolume.Bind 부팅 hook.
- `Assets/SessionPanel/Scripts/VolumeSectionController.cs` — 신설. UI ↔ SessionVolume ↔ IActiveInstrumentProvider.
- `Assets/SessionPanel/Editor/DummyActiveInstrument.cs` — 신설. manual 검증용 임시 stub provider.
- `Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs` — 수정. `voiceMixerGroup` SerializeField + `EnsureVoicePool` 적용.
- `Assets/Instruments/_Core/Scripts/InstrumentBase.cs` — 수정. `instrumentId` / `instanceVolume` SerializeField + `InstanceVolume` 프로퍼티 + `Awake` 복원 + NoteOn volume 곱하기.
- `Assets/Instruments/Piano/Prefabs/Piano.prefab` — 수정. AudioOutput 자식 `voiceMixerGroup` ← SessionMixer/Master, Piano.instrumentId = "Piano".
- `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` — 수정. AudioOutput 자식 `voiceMixerGroup` ← SessionMixer/Master, DrumKit.instrumentId = "DrumKit".
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab` (anchoring plan 산출물) — 수정. VolumeSectionContainer에 `VolumeSectionController` 부착 + slider prefab/parent wire.
- `Assets/Scenes/SampleScene.unity` — `SessionPanelManager` GameObject(anchoring plan 산출물)에 `SessionVolumeBootstrap` 부착 + `SessionMixer` wire + DummyActiveInstrument 임시 부착.

## Acceptance Criteria

- [ ] `[auto-hard]` `SessionMixer.mixer` 자산이 `Assets/SessionPanel/Audio/`에 존재 + `MasterVolume_dB` exposed parameter가 grep으로 단일 매치.
- [ ] `[auto-hard]` `Piano.prefab` AudioOutput 자식 InstrumentAudioOutput의 `voiceMixerGroup` 직렬화 reference가 SessionMixer/Master를 가리킴 (단일 매치).
- [ ] `[auto-hard]` `DrumKit.prefab` AudioOutput 자식 InstrumentAudioOutput의 `voiceMixerGroup` 직렬화 reference가 SessionMixer/Master를 가리킴.
- [ ] `[auto-hard]` `Piano.prefab`의 Piano 컴포넌트 `instrumentId` 직렬화 값이 정확히 `"Piano"` (grep 단일 매치).
- [ ] `[auto-hard]` `DrumKit.prefab`의 DrumKit 컴포넌트 `instrumentId` 직렬화 값이 정확히 `"DrumKit"`.
- [ ] `[auto-hard]` `Piano.prefab` 인스턴스화 시 콘솔 에러·예외 0건 (직렬화 sanity).
- [ ] `[auto-hard]` `DrumKit.prefab` 인스턴스화 시 콘솔 에러·예외 0건.
- [ ] `[auto-hard]` `SessionVolume.cs`, `SessionVolumeBootstrap.cs`, `VolumeSectionController.cs`, 수정된 `InstrumentBase.cs`, `InstrumentAudioOutput.cs`가 모두 컴파일 통과 (`read_console` 컴파일 에러 0).
- [ ] `[auto-hard]` `VolumeSlider.prefab` 인스턴스화 후 자식 트리에 `UnityEngine.UI.Slider` 1개 + 라벨 텍스트 컴포넌트 1개 존재.
- [ ] `[manual-hard]` SampleScene Editor Play (anchoring plan 산출물 적용 후) + dummy provider Current = null로 패널 호출 → 패널 안 마스터 슬라이더 1개만 노출, 초기값 = 직전 실행 persist 값(첫 실행이면 0.5).
- [ ] `[manual-hard]` 마스터 슬라이더를 0으로 내리면 Piano·DrumKit 발화 시 무음, 1로 올리면 정상 출력 (AudioMixer dB 라우팅 확인).
- [ ] `[manual-hard]` dummy provider Current를 Piano로 set → 인스턴스 슬라이더 1개 추가 노출 (라벨 "Piano"). 슬라이더 변경 시 Piano 출력 음량만 변하고 DrumKit는 무영향.
- [ ] `[manual-hard]` dummy provider Current를 DrumKit으로 swap → 인스턴스 슬라이더의 라벨/value가 DrumKit 기준으로 갱신. 슬라이더 변경 시 DrumKit 출력만 변함.
- [ ] `[manual-hard]` Editor 종료 후 재진입 → 마스터·Piano·DrumKit 슬라이더 값이 모두 직전 종료 시점 값으로 복원 (PlayerPrefs persist 확인).
- [ ] `[manual-hard]` dummy provider Current = null 상태로 패널 노출 시 인스턴스 슬라이더가 노출되지 않음 (spec Behavior).
- [ ] `[manual-hard]` SampleScene Play 진입 시 SessionVolume·VolumeSectionController 관련 콘솔 에러·예외 0건.

## Out of Scope

- 멀티플레이 타 사용자 악기/마이크 슬롯의 Behavior — spec에서 What 박제만, 후속 멀티플레이 spec.
- 사운드 파이프라인(MIDI 발화, 믹싱 효과 체인) 자체 — 본 plan은 mixer 자산 신설 + 마스터 라우팅까지만 한다.
- Piece별 개별 볼륨 (BassDrum, Snare, …) — DrumKit 단위로 통일. piece 단위가 필요해지면 별도 spec/plan.
- 슬라이더의 시각·청각 피드백 디자인 (틱 사운드, 색상 hover 등).
- 활성 악기 신호의 production wiring (drum/piano 잡기) — 다른 작업자가 진행 중인 잡기 시스템 plan에서. 본 plan의 manual 검증은 dummy stub으로.
- 왼손 핀치 호출 자체 — anchoring plan에 컨트롤러 stub 입력으로 들어와 있고, 핀치 swap은 hands `03-left-pinch-gesture` plan에서.

## Notes

- AudioMixer SetFloat는 dB 단위, 슬라이더는 0~1 선형. 변환 식은 `dB = log10(max(v, 0.0001)) * 20`. 0일 때 약 -80 dB(거의 무음). 본 plan은 `SessionVolume.LinearToDb`에 한 번만 박는다.
- `instrumentId`를 InstrumentBase에 두는 이유: PlayerPrefs key + `IActiveInstrument.InstrumentId`(anchoring 인터페이스 정의)에 같은 값을 노출해, 후속 plan(잡기 wiring)에서 IActiveInstrument 구현체가 InstrumentBase.InstrumentId를 그대로 흘려보내면 자연스럽다.
- 인스턴스 슬라이더는 1개를 재사용(라벨·value swap)한다. spec이 "잡힌 악기의 슬롯이 추가된다"고 했으므로 동시 노출 슬롯 수는 1(자기 악기) + 1(마스터)이고, 멀티플레이 슬롯은 본 plan 범위 밖이라 동적 복제 없이 단일 인스턴스로 충분하다.

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신 -->
