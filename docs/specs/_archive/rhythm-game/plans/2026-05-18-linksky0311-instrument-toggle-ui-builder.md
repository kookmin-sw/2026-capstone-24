# Instrument Toggle UI Builder — Per-Instrument On/Off Widget & Accompaniment Dict Wiring

**Linked Spec:** [`07-session-options-instrument-toggle-tempo.md`](../../../rhythm-game/specs/07-session-options-instrument-toggle-tempo.md)
**Status:** `Done`

## Goal

`InstrumentToggleButtonUI` 위젯 신규 도입 + `RhythmGameSectionController` 가 곡·난이도 선택 직후 player 악기 제외 + 해당 난이도 파일 존재 instrument 만 row 1개씩 인스턴스화 (기본 ON) 하고, `OnPlayButtonClicked` 의 accompaniment 사전이 그 토글 상태를 모든 channel 에 박도록 확장한다. UI prefab 실제 배치 / chart 머지 / effectiveBpm 일관성 / manual 시각 검증은 Plan C 책임.

## Context

sub-spec 07 분할판의 **Plan B** (2 of 3). Plan A (`2026-05-18-linksky0311-accompaniment-toggle-gate`) 가 `RhythmAccompaniment.Begin` 4-arg 시그니처 + `ShouldFire(int)` internal seam + `Fire` OFF skip 게이트를 완료해 채널 단위 게이트는 이미 동작한다. 단, 현 코드 `RhythmGameSectionController.OnPlayButtonClicked` L356-361 의 accompaniment 사전 빌드 로직은 player chart 의 `channelMap.entries` 중 judgedChannel 제외 모든 채널에 무조건 `true` 만 박는다 — UI 토글이 없으므로 OFF skip 게이트가 실제로 작동할 입력 자체가 없는 상태.

본 Plan B 는 그 입력 — instrument 단위 on/off 사용자 결정 — 을 controller 에 박제하고, `_loadedChart.channelMap.entries` 의 각 channel 이 어느 instrument 에 속하는지(`entry.instrumentKey`)를 보고 그 instrument 의 토글 상태를 channel 에 반영한다.

분할 결정에 따라 본 plan은 다음을 의도적으로 미룬다:
- 활성 instrument 의 추가 차트 파싱 (`_otherInstrumentCharts`) + `_loadedChart` 머지: Plan C.
- `effectiveBpm` 을 머지된 chart 의 모든 tempoMap segment 에 일관 적용: Plan C.
- `SessionPanel.prefab` 의 `instrumentToggleContainer` 자식 추가 + `InstrumentToggleButtonUI` prefab asset 직렬화 배치: Plan C 또는 사용자 인스펙터 작업.
- manual-hard (Editor Play 모드 토글 시각 검증): Plan C.

본 plan 의 controller 확장은 `instrumentToggleContainer == null` / `instrumentToggleButtonPrefab == null` 인 상태에서도 null-safe 하게 동작 (토글 빌드 단순 skip, 사전 빌드는 후방 호환으로 전체 true) — Plan A 의 EditMode 회귀를 깨지 않는다.

`_currentInstrument.InstrumentId` 비교 정책은 sub-spec 06 박제대로 OrdinalIgnoreCase. 토글 사전 키도 동일 정책 (`StringComparer.OrdinalIgnoreCase`).

## Verified Structural Assumptions

- `ISongEntry` 시그니처 (sub-spec 06 적용 + Plan A 시점 동일): `IReadOnlyCollection<string> GetDifficultiesFor(string instrumentId)` (빈 집합이면 미지원) + `string GetChartPath(string instrumentId, string difficulty)` (매칭 없으면 null) + 신규 호출자가 사용. `SupportedInstrumentIds` 는 OrdinalIgnoreCase HashSet (sub-spec 06 박제). — `Read Assets/SessionPanel/Scripts/ISongCatalog.cs (2026-05-18)` + `Read Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs (2026-05-18)`
- `RhythmGameSectionController.OnSongRowClicked → OnDifficultyClicked → LoadChart` 호출 흐름 (L139-221): `OnSongRowClicked` 가 `_selectedSong` 박제 후 player 악기 기준 `GetDifficultiesFor` 로 난이도 row 인스턴스화 → 첫 번째 row 를 `OnDifficultyClicked(firstDiff, firstBtn)` 로 자동 클릭 → `LoadChart()` 가 player 의 (instrument, difficulty) chart 만 파싱해 `_loadedChart` 박제 + `BuildBpmBar(_loadedChart)`. 본 plan의 `BuildInstrumentToggles()` 는 `OnSongRowClicked` 의 자동 첫-난이도 클릭 *이후* (또는 `OnDifficultyClicked` 안 `LoadChart` 호출 직후) 호출되어 player 외 instrument 각각에 대해 `GetDifficultiesFor(otherInstrumentId).Contains(_selectedDifficulty)` 인 instrument 만 row 인스턴스화. — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`
- `RhythmGameSectionController.OnPlayButtonClicked` L356-361 accompaniment 사전 빌드 현 코드: `var accompaniment = new Dictionary<int, bool>(); foreach (var entry in _loadedChart.channelMap.entries) if (entry.channel != judgedChannel) accompaniment[entry.channel] = true;`. 본 plan은 이 블록을 `_instrumentToggleStates[entry.instrumentKey]` lookup (OrdinalIgnoreCase) 로 교체. 키 부재 시 fallback=true. judgedChannel 은 사전에 박지 않는다 (현 동작 유지). — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`
- `ChannelInstrumentMap.Entry` 구조 (RhythmGame.Data): `public int channel; public string instrumentKey;`. `_loadedChart.channelMap.entries` 는 player chart 의 channel→instrument 매핑이므로 본 plan 범위에서 그 chart 에 등장하는 channel 만 사전에 박힌다. Plan C 의 chart 머지가 들어오기 전까지 OFF instrument 의 channel 이 player chart 에 등장하지 않으면 (다른 파일 소속) 사전에 박히지도 않는다 — 본 plan은 그 한계를 명시적으로 받아들인다(Out of Scope 참조). — `Read Assets/RhythmGame/Scripts/Data/ChannelInstrumentMap.cs (2026-05-18)`
- `AccompanimentToggleUI.cs` 기존 API: `public void Setup(int channel, string instrumentKey, bool defaultOn, Action<int, bool> callback)`. 채널 단위 + int callback. 본 plan은 **재사용하지 않고** 신규 `InstrumentToggleButtonUI` 를 instrument 단위 + string callback 으로 도입 — `AccompanimentToggleUI` 의 다른 호출자가 (호출자 없을 가능성 높으나) 깨지지 않게 본 파일은 건드리지 않는다. — `Read Assets/SessionPanel/Scripts/AccompanimentToggleUI.cs (2026-05-18)`
- `DifficultyButtonUI.cs` 의 동적 row 인스턴스화 패턴: `Setup(string difficulty, Action<string, DifficultyButtonUI> callback, Image bg = null)` + `Awake` 에서 `button.onClick.AddListener(OnClick)` 구독 + `OnClick` 이 `_callback?.Invoke(_difficulty, this)`. 본 plan의 `InstrumentToggleButtonUI` 는 같은 직렬화 + Setup 패턴을 따른다 (단 Toggle/Button 컴포넌트 선택은 후술). — `Read Assets/SessionPanel/Scripts/DifficultyButtonUI.cs (2026-05-18)`
- asmdef 의존: `Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef` references = [`Instruments`, `RhythmGame.Data`, `RhythmGame.Runtime`, `Unity.InputSystem`, `Unity.XR.Interaction.Toolkit`, `Unity.TextMeshPro`]. 본 plan의 신규 `InstrumentToggleButtonUI.cs` 는 `UnityEngine.UI.Toggle` + `TMPro.TextMeshProUGUI` 만 사용 — 추가 reference 불필요. 테스트 asmdef `SessionPanel.Tests.asmdef` references = [`SessionPanel.Runtime`, `Instruments`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`] — 본 plan의 신규 `RhythmGameSectionInstrumentToggleTests.cs` 는 `SerializedObject` + `RhythmGame.Data.VmSongChart` 를 사용한다. `RhythmGame.Data` 는 테스트 asmdef references 에 누락돼 있으므로 본 plan은 `SessionPanel.Tests.asmdef` 의 references 에 `"RhythmGame.Data"` 1개 추가가 필요. — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-18)` + `Read Assets/SessionPanel/Tests/SessionPanel.Tests.asmdef (2026-05-18)`

## Approach

1. **신규 파일**: `Assets/SessionPanel/Scripts/InstrumentToggleButtonUI.cs`.
   - namespace `SessionPanel`. `[AddComponentMenu("SessionPanel/Instrument Toggle Button UI")]` + `MonoBehaviour`.
   - 직렬화 필드: `[SerializeField] Toggle toggle; [SerializeField] TextMeshProUGUI label;`.
   - private state: `string _instrumentId; Action<string, bool> _callback;`.
   - public API: `public void Setup(string instrumentId, bool defaultOn, Action<string, bool> callback)` — `_instrumentId` 박제 + `label.text = instrumentId` + `toggle.isOn = defaultOn` + `toggle.onValueChanged.RemoveAllListeners()` + `AddListener(OnToggleChanged)`.
   - `void OnToggleChanged(bool isOn) => _callback?.Invoke(_instrumentId, isOn);`.
   - `AccompanimentToggleUI.cs` 와 동일한 idempotent 구독 패턴.

2. **`RhythmGameSectionController.cs` 수정** (한 파일 안에서 4 변경 묶음).
   - (a) 직렬화 필드 2개 신규 추가 (Header "Instrument Toggles" 아래):
     ```csharp
     [Header("Instrument Toggles")]
     [SerializeField] Transform instrumentToggleContainer;
     [SerializeField] GameObject instrumentToggleButtonPrefab;
     ```
   - (b) 인스턴스 필드 신규 추가 (`_selectedDiffBtn` 근처):
     ```csharp
     Dictionary<string, bool> _instrumentToggleStates =
         new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
     ```
     + `using System;` 추가 (현재 미사용).
   - (c) `OnDifficultyClicked` 의 `LoadChart()` 호출 *직후* 한 줄 추가:
     ```csharp
     BuildInstrumentToggles();
     ```
     → 곡 선택 직후 자동 첫-난이도 클릭 + 사용자 난이도 변경 양쪽에서 토글 목록이 재구성된다.
   - (d) `BuildInstrumentToggles()` 신규 메서드:
     - `_instrumentToggleStates.Clear();`
     - guard: `if (_selectedSong == null || _selectedDifficulty == null || _currentInstrument == null) return;`
     - container guard: `if (instrumentToggleContainer == null) return;` + `ClearChildren(instrumentToggleContainer);`
     - 후보 목록 빌드: catalog 가 노출하지 않는 "이 곡의 instrument 목록" 정보는 `_selectedSong.SupportedInstrumentIds` 를 그대로 사용. 그 중 player 악기 (`_currentInstrument.InstrumentId`) OrdinalIgnoreCase 제외 + `_selectedSong.GetDifficultiesFor(otherId).Contains(_selectedDifficulty)` (OrdinalIgnoreCase 비교는 GetDifficultiesFor 가 박제) 인 instrument 만 후보.
     - 후보 빈 목록 시: `instrumentToggleContainer.gameObject.SetActive(false);` + return.
     - 후보 있음 시: `instrumentToggleContainer.gameObject.SetActive(true);` + 후보 정렬(Ordinal 오름차순으로 결정적). 각 instrumentId 에 대해 `Instantiate(instrumentToggleButtonPrefab, instrumentToggleContainer)` → `LayoutElement.preferredHeight = 36f` → `GetComponent<InstrumentToggleButtonUI>().Setup(instrumentId, true, OnInstrumentToggleChanged)` → `_instrumentToggleStates[instrumentId] = true;` 기본 박제.
     - prefab null 가드: `instrumentToggleButtonPrefab == null` 이면 사전만 박제하고 위젯 인스턴스화 skip (EditMode 회귀 회피).
   - (e) `OnInstrumentToggleChanged(string instrumentId, bool isOn)` 신규 메서드:
     ```csharp
     _instrumentToggleStates[instrumentId] = isOn;
     ```
     단순 박제 — 시작 시까지 lazy.
   - (f) `OnPlayButtonClicked` 의 L356-361 accompaniment 사전 빌드 블록을 다음으로 교체:
     ```csharp
     var accompaniment = new Dictionary<int, bool>();
     foreach (var entry in _loadedChart.channelMap.entries)
     {
         if (entry.channel == judgedChannel) continue;
         bool on = true; // fallback when toggle not built (no UI yet, or instrument not in song)
         if (_instrumentToggleStates.TryGetValue(entry.instrumentKey ?? string.Empty, out var stored))
             on = stored;
         accompaniment[entry.channel] = on;
     }
     ```
     OFF 채널은 false 로 박힘 → Plan A 의 `ShouldFire(channel)` 게이트가 OFF skip 발동.
   - (g) `ResetSelection()` 에 `_instrumentToggleStates.Clear();` + `if (instrumentToggleContainer != null) { ClearChildren(instrumentToggleContainer); instrumentToggleContainer.gameObject.SetActive(false); }` 추가.

3. **테스트 asmdef 확장**: `Assets/SessionPanel/Tests/SessionPanel.Tests.asmdef` 의 `references` 에 `"RhythmGame.Data"` 추가.

4. **신규 EditMode 테스트**: `Assets/SessionPanel/Tests/RhythmGameSectionInstrumentToggleTests.cs`.
   - `RhythmGameSectionRefreshTests` 의 `StubSongEntry` / `StubCatalog` / `StubInstrument` / `StubProvider` 패턴 그대로 모방 (파일 안에 동일한 helper 클래스 다시 정의 — 두 테스트 파일 공유 helper 분리는 본 plan 범위 밖).
   - helper: `BuildControllerWithToggleContainer()` — `BuildController` 기본 셋업 + `instrumentToggleContainer` Transform GameObject + `instrumentToggleButtonPrefab` GameObject (RectTransform + Toggle (자식 Background/Label 없이 최소) + TextMeshProUGUI + `InstrumentToggleButtonUI`) 를 `SerializedObject` 로 박제. `_selectedSong` / `_selectedDifficulty` / `_loadedChart` 는 controller 가 내부 private 이므로 `SerializedObject` + reflection 으로 박제하거나, controller 의 `OnSongRowClicked` / `OnDifficultyClicked` 를 호출해 자연 흐름으로 박제. 후자가 더 결정적이므로 그렇게 진행.
   - StubSongEntry 확장: `GetDifficultiesFor(instrumentId)` 가 instrument 별 미리 박제한 List 반환 + `GetChartPath(instrumentId, diff)` 가 미리 박제한 in-memory 경로 반환. 테스트는 실제 file IO 를 피하기 위해 player chart 의 `LoadChart()` 가 `File.Exists` 실패 시 `_loadedChart` null 상태로 남는다 — 본 plan의 controller 코드를 약간 변형: **테스트는 LoadChart 까지 가지 않고 `BuildInstrumentToggles()` + `OnInstrumentToggleChanged` + accompaniment 사전 변환만 검증**. 검증 path:
     - 테스트 A (`InstrumentToggles_OnlyOtherInstrumentsWithMatchingDifficulty_Built`):
       - StubSong: piano(easy,normal), drum(easy), violin(easy,hard). player=piano, difficulty=easy.
       - 시나리오: `OnSongRowClicked(song)` (player=piano) — controller 가 자동으로 첫 difficulty=easy 를 `OnDifficultyClicked` 호출 → `LoadChart()` 가 file 없음으로 fail 하지만 `BuildInstrumentToggles()` 는 그 직후 무조건 호출됨.
       - assertion: `instrumentToggleContainer.childCount == 2` (drum, violin) + 각 child 의 `InstrumentToggleButtonUI` 의 라벨 텍스트가 "DrumKit" 또는 "Violin" (정렬은 Ordinal 오름차순이므로 결정적).
       - `_instrumentToggleStates` 가 `{drum:true, violin:true}` 박제됐는지: SerializedObject 또는 reflection 으로 접근. 또는 간접 검증: 둘 다 ON 상태에서 accompaniment 사전 변환 후 채널 값이 true.
     - 테스트 B (`AccompanimentDict_ReflectsToggleStates_OffChannelFalse`):
       - StubSong: piano(easy), drum(easy). player=piano, difficulty=easy.
       - `_loadedChart` 를 reflection 으로 박제 (in-memory VmSongChart 인스턴스: tempoMap segment 1개 + channelMap.entries = [{1,"piano"},{2,"drum"}]).
       - `OnSongRowClicked` 후 drum 토글을 OFF 로 토글 (`InstrumentToggleButtonUI._callback` 직접 invoke 또는 `Toggle.isOn = false` 시뮬레이션).
       - `OnPlayButtonClicked` 호출 — 단, host=null guard 가 작동해 StartSession 까지 안 가므로 accompaniment 사전 변환만 검증 가능한 형태로 controller 를 약간 리팩터링: accompaniment 사전 빌드를 private 메서드 `BuildAccompanimentDict(int judgedChannel)` 로 추출하고 internal 노출 (`SessionPanel.Runtime` 어셈블리에 `[InternalsVisibleTo("SessionPanel.Tests")]` 가 필요 — `Assets/SessionPanel/Scripts/AssemblyInfo.cs` 신규 1개 추가).
       - assertion: `dict[1]` 가 사전에 없거나 (judgedChannel) skip 됐고, `dict[2] == false` (drum OFF).
     - 테스트 C (`InstrumentToggles_PlayerOnlySong_ContainerDeactivated`):
       - StubSong: piano(easy) 만. player=piano.
       - `OnSongRowClicked` 후 `instrumentToggleContainer.gameObject.activeSelf == false` + `childCount == 0`.
     - 테스트 D (`InstrumentToggles_DifficultyChange_RebuildsList`):
       - StubSong: piano(easy,hard), drum(easy), violin(hard). player=piano.
       - difficulty=easy 선택 → drum 만 row, violin 없음.
       - difficulty=hard 로 변경 → violin 만 row, drum 없음. `_instrumentToggleStates` 가 새 instrument 만 박제(이전 drum 키는 Clear 됐는지 검증).
   - 총 4 테스트 (테스트 D 가 "난이도 변경 시 재구성" 책임 추가 — 본 plan 범위 안).

5. **신규 파일**: `Assets/SessionPanel/Scripts/AssemblyInfo.cs` — `[assembly: InternalsVisibleTo("SessionPanel.Tests")]`. `BuildAccompanimentDict` internal 노출용. 어셈블리 명은 `SessionPanel.Runtime.asmdef` 의 `name = "SessionPanel.Runtime"` 이지만 `InternalsVisibleTo` 는 *대상 어셈블리* (테스트) 명을 적는다 = `"SessionPanel.Tests"`.

## Deliverables

- `Assets/SessionPanel/Scripts/InstrumentToggleButtonUI.cs` (신규) — instrument 단위 on/off 위젯, `Setup(string, bool, Action<string,bool>)`.
- `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` (수정) — 직렬화 필드 2 + `_instrumentToggleStates` Dictionary + `BuildInstrumentToggles()` + `OnInstrumentToggleChanged` + `BuildAccompanimentDict(int)` internal + `OnPlayButtonClicked` 호출부 교체 + `ResetSelection` 정리.
- `Assets/SessionPanel/Scripts/AssemblyInfo.cs` (신규) — `[InternalsVisibleTo("SessionPanel.Tests")]`.
- `Assets/SessionPanel/Tests/SessionPanel.Tests.asmdef` (수정) — `references` 에 `"RhythmGame.Data"` 1개 추가.
- `Assets/SessionPanel/Tests/RhythmGameSectionInstrumentToggleTests.cs` (신규) — 4 EditMode 테스트 (토글 빌드, 사전 변환 OFF=false, player-only 컨테이너 비활성, 난이도 변경 재구성).

## Acceptance Criteria

- [ ] `[auto-hard]` 변경 후 Unity 에디터가 컴파일 에러 0건으로 도메인 리로드 완료.
  **검증:** Unity MCP `read_console` 호출 후 `types=[error]` 카운트 0건. 또는 `editor_state` resource 의 `isCompiling=false` & errors 빈 배열.
- [ ] `[auto-hard]` `InstrumentToggleButtonUI.cs` 가 `public void Setup(string instrumentId, bool defaultOn, Action<string, bool> callback)` 시그니처와 `_callback?.Invoke(_instrumentId, isOn)` 호출을 보유.
  **검증:** Grep `public void Setup\(string instrumentId, bool defaultOn, Action<string, bool> callback\)` 1건 + Grep `_callback\?\.Invoke\(_instrumentId, isOn\)` 1건 in `Assets/SessionPanel/Scripts/InstrumentToggleButtonUI.cs`.
- [ ] `[auto-hard]` `RhythmGameSectionController.cs` 가 `instrumentToggleContainer` SerializedField + `instrumentToggleButtonPrefab` SerializedField + `_instrumentToggleStates` Dictionary (OrdinalIgnoreCase) + `BuildInstrumentToggles()` + `OnInstrumentToggleChanged(string instrumentId, bool isOn)` + `internal Dictionary<int, bool> BuildAccompanimentDict(int judgedChannel)` 5개를 모두 보유, `OnDifficultyClicked` 안에 `BuildInstrumentToggles()` 1줄 호출.
  **검증:** Grep `\[SerializeField\] Transform instrumentToggleContainer` + Grep `\[SerializeField\] GameObject instrumentToggleButtonPrefab` + Grep `Dictionary<string, bool> _instrumentToggleStates` + Grep `void BuildInstrumentToggles\(\)` + Grep `void OnInstrumentToggleChanged\(string instrumentId, bool isOn\)` + Grep `internal Dictionary<int, bool> BuildAccompanimentDict\(int judgedChannel\)` + Grep `BuildInstrumentToggles\(\);` 각 1건 이상 match in `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs`. `StringComparer.OrdinalIgnoreCase` literal 1건 이상.
- [ ] `[auto-hard]` `OnPlayButtonClicked` 의 accompaniment 사전 빌드가 `_instrumentToggleStates[entry.instrumentKey]` lookup 기반으로 교체되어, 사전 키 부재 시 fallback=true 가 유지되며 OFF 토글이 false 로 박힌다.
  **검증:** Grep `_instrumentToggleStates\.TryGetValue\(entry\.instrumentKey` 1건 in `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs`. 기존 `accompaniment\[entry\.channel\] = true;` literal 의 unconditional 형태(`if (entry.channel != judgedChannel) accompaniment[entry.channel] = true;`) 가 OnPlayButtonClicked 안에서 잔존 0건 (`BuildAccompanimentDict` 안에서만 lookup 기반 빌드).
- [ ] `[auto-hard]` `Assets/SessionPanel/Scripts/AssemblyInfo.cs` 가 `[assembly: InternalsVisibleTo("SessionPanel.Tests")]` 1줄 보유, `SessionPanel.Tests.asmdef` 의 `references` 에 `"RhythmGame.Data"` 추가됨.
  **검증:** Grep `InternalsVisibleTo\("SessionPanel\.Tests"\)` 1건 in `Assets/SessionPanel/Scripts/AssemblyInfo.cs` + Grep `"RhythmGame\.Data"` 1건 in `Assets/SessionPanel/Tests/SessionPanel.Tests.asmdef`.
- [ ] `[auto-hard]` 신규 EditMode 테스트 4건 모두 PASS: `InstrumentToggles_OnlyOtherInstrumentsWithMatchingDifficulty_Built`, `AccompanimentDict_ReflectsToggleStates_OffChannelFalse`, `InstrumentToggles_PlayerOnlySong_ContainerDeactivated`, `InstrumentToggles_DifficultyChange_RebuildsList`. (A) 컨테이너 child = 2 (drum/violin), 라벨 정확. (B) `BuildAccompanimentDict(judgedChannel=1)` 반환 dict 가 `{2: false}` (drum OFF 후), key 1 부재. (C) player-only 곡 시 `instrumentToggleContainer.gameObject.activeSelf == false` + `childCount == 0`. (D) 난이도 easy→hard 변경 시 drum row 사라지고 violin row 등장, `_instrumentToggleStates` 가 violin 키만 보유.
  **검증:** `unity-test-runner` sub-agent 호출 또는 Unity MCP `run_tests` (mode=EditMode, filter=`RhythmGameSectionInstrumentToggleTests`) — passed=4 / failed=0.
- [ ] `[auto-soft]` 기존 EditMode 테스트 `SessionPanel.Tests` (RhythmGameSectionRefreshTests 등 15+) + `RhythmGame.Tests.Editor` (Plan A 의 38) 회귀 0건.
  **검증:** `unity-test-runner` sub-agent 전체 EditMode run — pre-existing pass count 유지 (회귀 0). 실패 시 Notes 에 기록 후 진행.

## Out of Scope

- `SessionPanel.prefab` 의 `instrumentToggleContainer` Transform 실제 자식 배치 + `InstrumentToggleButtonUI` prefab 직렬화 (Toggle 자식 그래픽/Label TextMeshPro 박제) — **Plan C** 또는 사용자 인스펙터 작업. 본 plan은 SerializedField 슬롯만 노출.
- 활성 instrument 의 추가 chart 파싱 (`_otherInstrumentCharts: Dictionary<string, VmSongChart>`) + `OnPlayButtonClicked` 안 chart 머지 (player chart 의 tracks + 활성 OFF-제외 instrument 들의 tracks 합치기, channelMap 합집합) — **Plan C** 책임. 본 plan은 player chart 의 channelMap 에 등장하지 않는 OFF instrument 의 channel 을 사전에 박지 *않는다*: 그 channel 은 player chart 머지 전이라 존재하지 않음. Plan C 가 머지를 도입한 후에야 그 사전 키가 false 로 박혀 Plan A 의 게이트가 발동한다.
- `effectiveBpm` 을 머지된 chart 의 모든 tempoMap segment 에 일관 적용 + `RhythmAccompaniment` 가 동일 BPM 사용 보장 — **Plan C** 책임.
- manual-hard (Editor Play 모드: drum/violin OFF 토글 → 시작 → 사운드 미발화 청각 검증, player-only 곡 시 토글 미노출 시각 검증) — **Plan C** 책임.
- `AccompanimentToggleUI.cs` 채널 단위 위젯의 deprecate / 삭제 — 호환성 위해 본 plan은 건드리지 않음. 다른 호출자 없음이 확인되면 후속 정리 plan 후보.
- `NoteDisplayPanel` 의 자동 반주 노트 시각화, 세션 도중 토글 변경, 트랙별 음량 — sub-spec Boundaries 에 의해 영구 out-of-scope.

## Notes

- 2026-05-18: 후속 plan `2026-05-18-linksky0311-instrument-toggle-tests-direct-injection.md` 추가. 신규 EditMode 4건이 `LoadChart` 의 `Path.Combine(streamingAssetsPath, null)` `ArgumentNullException` 으로 실패 → 테스트만 reflection 직접 박제로 재작성. 완료 후 본 plan 재검증 필요.
- 2026-05-18 자동 AC 결과 (fix plan 적용 후 재검증): 7/7 PASS.
  - AC1~AC5 (production 코드 Grep 매치): 4건 PASS — Plan B 원본 검증.
  - AC6 (신규 EditMode 4건 PASS): PASS — fix plan 후 SessionPanel.Tests 19/19 PASS (15 기존 + 4 신규).
  - AC7 (auto-soft 회귀): PASS — RhythmGame.Tests.Editor 38/38 무회귀.
- 2026-05-18 후속 fix plan 추가: `2026-05-18-linksky0311-instrument-toggle-tests-direct-injection.md` — 테스트 셋업이 자연 흐름 대신 reflection 직접 박제로 변경. Plan B production 코드 무변경 유지.
- `BuildAccompanimentDict` 를 internal 로 추출한 이유: `OnPlayButtonClicked` 내 inline 빌드를 EditMode 에서 외부 호출하려면 host=null guard 에 막혀 사전 빌드 자체가 실행되지 않는다. `_loadedChart` 박제 후 `BuildAccompanimentDict(judgedChannel)` 만 호출해 검증할 수 있도록 seam 분리. Plan C 의 chart 머지 적용 후에도 같은 seam 재사용 가능.
- 후보 정렬을 Ordinal 오름차순으로 한 이유: 결정적 row 순서 확보 (테스트 assertion 의 안정성).
- `instrumentToggleContainer` / `instrumentToggleButtonPrefab` 가 null 인 상태에서도 EditMode 회귀가 깨지지 않도록 모든 진입부에 null guard. 기존 `RhythmGameSectionRefreshTests` 는 두 필드 모두 박제하지 않으므로 `instrumentToggleContainer == null` → `BuildInstrumentToggles` 즉시 return, 사전은 빈 채로 남음 → `BuildAccompanimentDict` 가 lookup 실패 시 fallback=true 로 전체 ON (sub-spec 06 시점 회귀 동작과 동일).
- Plan A 의 `ShouldFire(channel)` 게이트 의미: 본 plan이 OFF 채널을 false 로 박지 않으면 게이트는 작동하지 않는다 — 이 plan의 사전 변환 책임이 Plan A 의 입력을 실제로 *공급*한다.
- 본 plan은 chart 머지를 미루므로 OFF instrument 의 channel 이 player chart 에 등장하지 않으면 사전에 박힐 키 자체가 없다 — 그러나 user 의 UI 토글 클릭은 `_instrumentToggleStates` 에 박제되므로 Plan C 의 chart 머지가 들어오는 순간 (그때 같은 channel 이 사전에 등장하기 시작) 자동으로 OFF 처리됨. UX 측면에서는 "Plan B 적용 직후에는 토글이 보이지만 실제 OFF 효과는 Plan C 적용 후 발현" — 이는 분할 정책의 의도된 trade-off.

## Handoff

Plan B + fix plan 완료. 핵심 산출:

- InstrumentToggleButtonUI 신규 위젯 (Setup(string, bool, Action<string,bool>)) + AccompanimentToggleUI(채널 단위) 와 독립.
- RhythmGameSectionController 확장: instrumentToggleContainer/instrumentToggleButtonPrefab SerializedField + _instrumentToggleStates Dictionary(OrdinalIgnoreCase) + BuildInstrumentToggles + OnInstrumentToggleChanged + internal BuildAccompanimentDict(int) seam.
- OnPlayButtonClicked accompaniment 사전이 _instrumentToggleStates 기반으로 빌드 (OFF=false, 키 부재 fallback=true).
- AssemblyInfo.cs InternalsVisibleTo("SessionPanel.Tests") + SessionPanel.Tests.asmdef 에 RhythmGame.Data, Unity.TextMeshPro reference 추가.
- EditMode 테스트 4건 (RhythmGameSectionInstrumentToggleTests): reflection 직접 박제 방식. SessionPanel.Tests 19/19 PASS.

후속 Plan C 시작 시점 박제:
- Plan A 의 ShouldFire(channel) 게이트는 player chart 의 channelMap.entries 에 등장하는 channel 만 게이팅. OFF instrument 의 channel 이 player chart 에 없으면 사전 키 자체 없음 — Plan C 의 chart 머지가 player chart 의 channelMap·tracks 에 활성 instrument 들의 channelMap·tracks 를 머지해야 OFF 효과 실제 발현.
- effectiveBpm 적용은 _loadedChart.tempoMap.segments[0].bpm 교체 (기존 코드 유지). 머지된 chart 의 tempoMap 도 동일 effectiveBpm 으로 일관 처리 필요 — Plan C 에서 머지 직후 적용.
- manual-hard (Editor Play 모드 토글 시각 검증) 는 Plan C 책임.
- SessionPanel.prefab 의 instrumentToggleContainer Transform + InstrumentToggleButtonUI prefab 자산 직렬화 배치 는 Plan C 또는 사용자 인스펙터 작업.
