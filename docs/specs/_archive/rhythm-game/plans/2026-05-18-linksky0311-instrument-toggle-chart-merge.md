# Instrument Toggle Chart Merge — Active Instruments Charts → Merged In-Memory Chart + effectiveBpm + Prefab Wiring (Plan C)

**Linked Spec:** [`07-session-options-instrument-toggle-tempo.md`](../../../rhythm-game/specs/07-session-options-instrument-toggle-tempo.md)
**Status:** `Done`

## Goal

sub-spec 07 의 **세 번째이자 최종 분할**. Plan A/B 산출(Toggle UI + Controller 토글 상태 + RhythmAccompaniment OFF skip 게이트 + `BuildAccompanimentDict` seam)을 실제 동작시키려면 (1) OFF 토글된 활성 instrument 의 채널·트랙·BPM 이 player chart 에 머지된 채로 `RhythmGameHost.StartSession` 에 들어가야 하고, (2) `SessionPanel.prefab` 에 `instrumentToggleContainer` Transform 자식 + `InstrumentToggleButtonUI` prefab asset 이 실제 와이어 되어야 한다. 본 plan 은 `RhythmGameSectionController.LoadChart` 가 player 외 활성 instrument 차트도 함께 파싱·보관하고, `OnPlayButtonClicked` 가 player chart 의 in-memory 복제본에 활성 instrument 의 `channelMap.entries` + `tracks` 를 머지한 뒤 머지 chart 의 `tempoMap.segments[0].bpm` 도 `effectiveBpm = _baseBpm + _bpmOffset` 으로 교체해 host 에 전달하도록 한다. 더해서 `SessionPanel.prefab` 의 song-detail 컨테이너 안에 toggle container Transform 을 신규 추가하고, 그 안에서 instance 화될 `InstrumentToggleButton.prefab` asset 을 신규 생성·MonoBehaviour 필드를 와이어한다.

## Context

> **이전 sub-spec handoff**
>
> - Plan A Done: `RhythmAccompaniment.Begin(VmSongChart, int, IRhythmClock, IReadOnlyDictionary<int, bool> enabled = null)` + `internal ShouldFire(int)` + `Fire` 가 `ShouldFire` 게이트로 OFF skip. (`RhythmAccompaniment.cs` L42-87, L132-137)
> - Plan B Done: `InstrumentToggleButtonUI`, Controller `_instrumentToggleStates Dictionary<string,bool>`, `BuildInstrumentToggles()`, `OnInstrumentToggleChanged(string,bool)`, internal `BuildAccompanimentDict(int judgedChannel) → Dictionary<int,bool>`. `OnPlayButtonClicked` 가 `BuildAccompanimentDict(judgedChannel)` 를 통해 host 에 accompaniment 사전 전달. (`RhythmGameSectionController.cs` L226-296, L426)
> - 직접 박제 테스트 4건 Done: `RhythmGameSectionInstrumentToggleTests.cs`.
>
> **현재 trade-off (Plan C 가 해결)**
>
> (1) player chart 의 `channelMap.entries` 에는 **player 악기 채널만** 등장(파일이 instrument 분리 vmsong이라 본인 chart 는 본인 channel 만 정의). 따라서:
> 1. `BuildAccompanimentDict` 가 player channel 만 보고 dict 빌드 → 다른 instrument 채널 키가 dict 에 등장하지 않아 Plan A 의 `ShouldFire` 게이트가 의미를 잃는다 (사전에 없으면 fallback ON, 또한 채널이 채널맵에 없으면 자동 반주 인덱싱 자체가 안 됨).
> 2. 활성 instrument 의 노트 트랙이 player chart 의 `tracks` 에 없으므로 `RhythmAccompaniment.BuildEvents` 가 발화할 노트 자체를 모름.
>
> (2) `SessionPanel.prefab` (L877-887) 의 `RhythmGameSectionController` MonoBehaviour 직렬화 블록에 `instrumentToggleContainer` / `instrumentToggleButtonPrefab` 키가 부재 → Plan B 의 `BuildInstrumentToggles()` 가 in-Editor 인스턴스화 대상 Transform/prefab 을 찾지 못해 토글 UI 가 화면에 나타나지 않는다.
>
> Plan C 는 (1) controller 가 player 외 활성 instrument 의 `.vmsong` 도 같은 난이도로 파싱·보관(`_otherInstrumentCharts`)하고, Play 클릭 시 player chart 의 in-memory 복제본에 `channelMap.entries` + `tracks` 를 머지해 host 에 넘긴다. effectiveBpm 도 머지 chart 의 tempoMap[0] 에 박제 → Plan A 의 RhythmAccompaniment 가 같은 chart 의 `tempoMap.TickToSeconds(...)` 로 자동 반주 타이밍을 계산하므로 자동 BPM 동기. 더해서 (2) `manage_prefabs` MCP 경유로 `SessionPanel.prefab` song-detail 컨테이너에 `instrumentToggleContainer` Transform 신규 추가 + `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 최소 구성 asset 신규 생성 + `RhythmGameSectionController` 직렬화 필드 와이어.

본 plan 은 sub-spec 07 의 What 중 (a) drum 토글 OFF → drum 자동 반주 무발화 (b) 80 BPM 조절 시 노트·자동 반주 모두 80 BPM 기준 발화 — 둘을 비로소 실제 발화 경로에서 만족시킨다. Tech Spec `Data/Control Flow` 의 `OnPlayButtonClicked → Build accompaniment dict + effectiveBpm → host.StartSession` 마지막 한 줄(`Apply effectiveBpm to chart.tempoMap`)에 **머지된 chart** 라는 보강이 본 plan 의 본질. Tech Spec `Prefab Hierarchy` 섹션이 박제한 "디테일 컨테이너의 difficultyContainer·bpmBar 패턴을 따라 instrument 토글 컨테이너 Transform 추가" 가 본 plan 의 prefab edit 단계로 만족된다.

본 plan 은 sub-spec 07 Boundaries (NoteDisplayPanel 자동 반주 시각화 / 세션 도중 변경 / 트랙별 음량) 와 Invariants (player 악기는 토글 대상 아님 / 곡→난이도→토글·템포→시작 순서) 를 건드리지 않는다. prefab 자식 배치도 Toggle 위젯 + label 만 추가하므로 Boundaries 침해 없음.

## Verified Structural Assumptions

- `RhythmGameSectionController.LoadChart` (L208-224) 의 현재 흐름은 player 차트만 파싱: `path = Path.Combine(streamingAssetsPath, _selectedSong.GetChartPath(_currentInstrument.InstrumentId, _selectedDifficulty))` → `VmSongParser.Parse(...)` → `_loadedChart = result.chart` → `BuildBpmBar(_loadedChart)`. player 외 instrument 차트는 파싱 진입조차 없다. 본 plan 은 이 메서드 끝에 player 외 활성 instrument(`_selectedSong.SupportedInstrumentIds.Where(id != _currentInstrument.InstrumentId)`) 각각에 대해 `GetChartPath(id, _selectedDifficulty)` non-null 파일을 `VmSongParser.Parse` 로 파싱해 신규 필드 `Dictionary<int, VmSongChart> _otherInstrumentCharts` 에 보관. 키는 그 chart 의 첫 channelMap.entries 의 channel int (사실상 instrument 채널). — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`
- `RhythmGameSectionController.OnPlayButtonClicked` (L390-430) 의 현재 흐름: `effectiveBpm = _baseBpm + _bpmOffset` → `_loadedChart.tempoMap.segments[0].bpm = effectiveBpm` (L403-409) → `judgedChannel = FindJudgedChannel(_loadedChart, ...)` → `accompaniment = BuildAccompanimentDict(judgedChannel)` → `host.StartSession(_loadedChart, ...)`. **player chart 자체를 직접 mutate 하는 점에 주의** — 본 plan 은 mutate 가능한 in-memory **복제본** 을 만들어 머지·BPM 박제를 그 복제본에 가하고 그것을 host 에 전달하도록 변경(player chart 원본 보존 = 난이도 재선택 시 재파싱 부담 회피). — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`
- `VmSongChart` 정의: `string title; string artist; string songId; TempoMap tempoMap = new(); ChannelInstrumentMap channelMap = new(); List<ChartTrack> tracks = new();` — `TempoMap.segments` 는 `List<TempoSegment>` (struct), `channelMap.entries` 는 `List<ChannelInstrumentMap.Entry>` (struct: `int channel; string instrumentKey;`), `ChartTrack` 은 sealed class: `int channel; List<ChartNote> notes = new();`. 머지 = `channelMap.entries.AddRange(...)` + `tracks.AddRange(...)` 의 얕은 복사로 충분 (struct entries / ref list ChartTrack 의 `.tracks` 자체는 외부 chart 의 list 이지만 본 in-memory 복제본은 결과 폐기 — RhythmAccompaniment 가 read-only 로만 소비). — `Read Assets/RhythmGame/Scripts/Data/VmSongChart.cs / ChannelInstrumentMap.cs / ChartTrack.cs / ChartNote.cs / TempoMap.cs / TempoSegment.cs (2026-05-18)`
- `RhythmAccompaniment.Begin` (L42-54) 가 chart 의 `channelMap` (BuildInstrumentMap 에서 channel↔InstrumentBase 매핑) + `tracks` (BuildEvents 에서 NoteOn/Off 이벤트 빌드) + `tempoMap.TickToSeconds` (이벤트 시각 계산) 셋 다 소비. 따라서 머지 chart 에 (i) 모든 활성 instrument 채널 entry, (ii) 활성 instrument tracks, (iii) effectiveBpm 박제된 tempoMap[0] 셋 다 있으면 자동 반주 타이밍이 effectiveBpm 기준으로 정확. `RhythmGameHost.StartSession` 시그니처는 `(VmSongChart chart, RhythmSong song, int judgedChannel, IReadOnlyDictionary<int,bool> accompanimentEnabled = null)` — accompaniment 게이트는 Plan A 가 이미 처리. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs (2026-05-18)`, `Grep RhythmGameHost.cs StartSession (2026-05-18)`
- `VmSongParser.Parse(text)` 가 `ParseResult { chart, errors, Success }` 를 반환. 성공 시 `chart.channelMap.entries` 가 그 파일의 [Channels] 항목들로, `chart.tracks` 가 그 파일의 [Track:N] 항목들로 채워진다. 활성 instrument vmsong 도 동일 구조 → 머지 안전. — `Read Assets/RhythmGame/Scripts/Data/Parsing/VmSongParser.cs (2026-05-18)`
- `_currentInstrument.InstrumentId` 비교는 `ISongEntry.SupportedInstrumentIds` 가 OrdinalIgnoreCase HashSet 으로 박제됨. 본 plan 은 player exclude 비교를 `StringComparer.OrdinalIgnoreCase.Equals(otherId, _currentInstrument.InstrumentId)` 로 통일. — `Read Assets/SessionPanel/Scripts/ISongCatalog.cs (2026-05-18)`
- `SessionPanel.Runtime.asmdef` references: `Instruments`, `RhythmGame.Data`, `RhythmGame.Runtime`, `Unity.InputSystem`, `Unity.XR.Interaction.Toolkit`, `Unity.TextMeshPro`. 본 plan 의 신규 import 는 `RhythmGame.Data` 의 `VmSongChart`/`ChartTrack`/`ChannelInstrumentMap.Entry`/`TempoSegment` 와 기존 `VmSongParser` 만 — 모두 `RhythmGame.Data` 안. **asmdef 추가 reference 불필요.** `SessionPanel.Tests.asmdef` references: `SessionPanel.Runtime, Instruments, RhythmGame.Data, Unity.TextMeshPro, UnityEngine.TestRunner, UnityEditor.TestRunner` — 신규 EditMode 테스트 import 도 동일 범위에 들어가므로 추가 reference 불필요. — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef / Assets/SessionPanel/Tests/SessionPanel.Tests.asmdef (2026-05-18)`
- **`SessionPanel.prefab` 현 직렬화 상태** (L877-887, `RhythmGameSectionController` MonoBehaviour 직렬화 블록): `songListContent`/`songRowPrefab`/`noSongPanel`/`songSelectedPanel`/`previewButton`/`bpmBar`/`difficultyContainer`/`difficultyButtonPrefab`/`playButton`/`activeInstrumentProviderObject`/`songCatalogObject` 키만 박제. **`instrumentToggleContainer` / `instrumentToggleButtonPrefab` 키가 누락** → Plan B 적용 후 prefab 재직렬화가 안 된 상태. 또한 `Assets/SessionPanel/Prefabs/` 디렉토리에는 `SessionPanel.prefab` 단일 파일만 존재해 `InstrumentToggleButton.prefab` 도 부재. — `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab L860-887 (2026-05-18)`, `Glob Assets/SessionPanel/Prefabs/*.prefab (2026-05-18)`
- **`difficultyContainer` 패턴 참조**: `Assets/SessionPanel/Prefabs/SessionPanel.prefab` L883 의 `difficultyContainer: {fileID: 3694353625739321471}` 와 L884 의 `difficultyButtonPrefab: {fileID: 443857913408226428, guid: 07db82e5475fa524693dbf0eec994866, type: 3}` 가 동적 row 자식 인스턴스화 패턴 (Tech Spec Prefab Hierarchy + Comparable Siblings `DifficultyButtonUI` 박제). 본 plan 의 `instrumentToggleContainer` Transform 도 같은 song-detail(`songSelectedPanel`) 자식 트리 안에 `difficultyContainer`/`bpmBar` 인접 위치에 추가하고, `InstrumentToggleButton.prefab` 도 동일한 방식의 dynamic-child prefab asset 으로 신규 생성. — `Read SessionPanel.prefab L880-885 (2026-05-18)`
- **`InstrumentToggleButtonUI.cs` 직렬 필드**: `[SerializeField] Toggle toggle; [SerializeField] TextMeshProUGUI label;`. 본 plan 의 신규 `InstrumentToggleButton.prefab` 은 root GameObject 에 RectTransform + InstrumentToggleButtonUI 컴포넌트 + UnityEngine.UI.Toggle 컴포넌트, 자식 GameObject 에 TextMeshProUGUI 1개. `Setup(instrumentId, defaultOn, callback)` 호출이 prefab instantiate 후 controller `BuildInstrumentToggles` 에서 일어나므로 prefab 시점에서 `instrumentId`/`callback` 박제는 불요. **prefab 시점에 박제할 것은 (a) `InstrumentToggleButtonUI._toggle` SerializedField → 자기 GameObject 의 Toggle 참조, (b) `InstrumentToggleButtonUI._label` SerializedField → 자식 TextMeshProUGUI 참조 두 가지뿐.** — `Read Assets/SessionPanel/Scripts/InstrumentToggleButtonUI.cs (2026-05-18)`
- **`manage_prefabs` MCP side effect**: 본 plan 의 prefab edit 은 `unity-asset-edit` skill 의 "직렬화 자산은 `manage_*` MCP 경유" 규칙에 종속. `manage_prefabs` 의 create/modify 액션은 GameObject hierarchy 추가, 컴포넌트 attach, SerializedField 와이어를 안전히 처리. **본 plan 은 prefab YAML 직접 Edit 을 금지** — 모든 prefab 자산 변경은 `manage_prefabs` + (필요 시) `manage_gameobject` 경유. 컴파일 대기·`read_console` 검증 절차는 [`unity-mcp-workflow`](../../../../.claude/skills/unity-mcp-workflow/SKILL.md) skill 이 단일 진실원이므로 plan 본문엔 재인용 생략. — `Read CLAUDE.md "직렬화 자산 수정 MCP 우선" 박스 (2026-05-18)`
- 본 plan 은 production 코드 1 파일(`RhythmGameSectionController.cs`) + 신규 EditMode 테스트 1 파일 + prefab edit 2 자산(`SessionPanel.prefab` 수정 + `InstrumentToggleButton.prefab` 신규). Plan A 의 `RhythmAccompaniment.cs` 와 Plan B 의 `InstrumentToggleButtonUI.cs` 는 코드 수정 없음.

## Approach

1. **`RhythmGameSectionController.cs` 신규 필드** (헤더 L44 `_loadedChart` 인근):
   ```csharp
   // (channel int → 그 instrument 의 파싱된 chart) — LoadChart 가 채움, OnPlayButtonClicked 가 머지에 소비
   Dictionary<int, VmSongChart> _otherInstrumentCharts = new Dictionary<int, VmSongChart>();
   ```
   `ResetSelection()` (L460-476) 에 `_otherInstrumentCharts.Clear();` 추가.

2. **`LoadChart()` (L208-224) 끝에 player 외 활성 instrument 차트 파싱 추가**:
   ```csharp
   _otherInstrumentCharts.Clear();
   foreach (var otherId in _selectedSong.SupportedInstrumentIds)
   {
       if (string.Equals(otherId, _currentInstrument.InstrumentId, StringComparison.OrdinalIgnoreCase)) continue;
       string otherRel = _selectedSong.GetChartPath(otherId, _selectedDifficulty);
       if (string.IsNullOrEmpty(otherRel)) continue;
       string otherPath = Path.Combine(Application.streamingAssetsPath, otherRel);
       if (!File.Exists(otherPath)) continue;
       var otherResult = VmSongParser.Parse(File.ReadAllText(otherPath));
       if (!otherResult.Success || otherResult.chart.channelMap.entries.Count == 0) continue;
       int firstChannel = otherResult.chart.channelMap.entries[0].channel;
       _otherInstrumentCharts[firstChannel] = otherResult.chart;
   }
   ```
   key 가 채널 int 인 이유: 머지 단계에서 중복 channel 충돌 감지 + dedupe 용이.

3. **신규 helper `BuildMergedChartForSession()` (internal, 머지 chart 반환)**:
   ```csharp
   internal VmSongChart BuildMergedChartForSession(float effectiveBpm)
   {
       if (_loadedChart == null) return null;
       // player chart 의 얕은 복제본 (in-memory)
       var merged = new VmSongChart {
           title    = _loadedChart.title,
           artist   = _loadedChart.artist,
           songId   = _loadedChart.songId,
       };
       merged.tempoMap.ticksPerQuarter = _loadedChart.tempoMap.ticksPerQuarter;
       foreach (var seg in _loadedChart.tempoMap.segments) merged.tempoMap.segments.Add(seg);
       foreach (var e   in _loadedChart.channelMap.entries) merged.channelMap.entries.Add(e);
       foreach (var t   in _loadedChart.tracks)             merged.tracks.Add(t);

       // 활성 instrument 차트 머지
       var seen = new HashSet<int>();
       foreach (var e in merged.channelMap.entries) seen.Add(e.channel);
       foreach (var kv in _otherInstrumentCharts)
       {
           foreach (var entry in kv.Value.channelMap.entries)
           {
               if (seen.Add(entry.channel)) merged.channelMap.entries.Add(entry);
           }
           foreach (var track in kv.Value.tracks) merged.tracks.Add(track);
       }

       // effectiveBpm 박제 (struct 이므로 인덱서 재대입)
       if (merged.tempoMap.segments.Count > 0)
       {
           var seg = merged.tempoMap.segments[0];
           seg.bpm = effectiveBpm;
           merged.tempoMap.segments[0] = seg;
       }
       return merged;
   }
   ```

4. **`OnPlayButtonClicked` (L390-430) 변경**: `_loadedChart.tempoMap.segments[0].bpm = effectiveBpm` 의 in-place mutate 를 `var merged = BuildMergedChartForSession(effectiveBpm)` 호출로 대체. `judgedChannel = FindJudgedChannel(merged, _currentInstrument.InstrumentId)` 로 머지 chart 기준 검색(어차피 player channel 이 머지 chart 의 channelMap 에 그대로 보존됨). Plan B 의 기존 `BuildAccompanimentDict(int judgedChannel)` 무인자 메서드는 reflection 테스트 호환 위해 무수정 유지하고, 신규 internal `BuildAccompanimentDictFromChart(int judgedChannel, VmSongChart sourceChart)` 메서드를 추가해 production 경로가 머지 chart 기반으로 dict 빌드:
   ```csharp
   internal Dictionary<int, bool> BuildAccompanimentDictFromChart(int judgedChannel, VmSongChart sourceChart)
   {
       var dict = new Dictionary<int, bool>();
       if (sourceChart == null) return dict;
       foreach (var entry in sourceChart.channelMap.entries)
       {
           if (entry.channel == judgedChannel) continue;
           bool on = true;
           if (_instrumentToggleStates.TryGetValue(entry.instrumentKey ?? string.Empty, out var stored))
               on = stored;
           dict[entry.channel] = on;
       }
       return dict;
   }
   ```
   `OnPlayButtonClicked` 호출은 `var accompaniment = BuildAccompanimentDictFromChart(judgedChannel, merged);` + `host.StartSession(merged, rhythmSong, judgedChannel, accompaniment);`.

5. **신규 EditMode 테스트 `RhythmGameSectionMergedChartTests.cs`** (1 파일):
   - `BuildMergedChartForSession_ChannelMapMerged_NoDuplicates` `[auto-hard]` — `_loadedChart` (channel 1=piano) + `_otherInstrumentCharts[2]` chart (channel 2=drum) 직접 박제 → reflection 으로 `BuildMergedChartForSession(80f)` 호출 → 결과 chart 의 `channelMap.entries.Count == 2`, channels = {1,2}, instrumentKeys = {"piano","drum"}, `tempoMap.segments[0].bpm == 80f`.
   - `BuildMergedChartForSession_TracksMerged_AllNotesPresent` `[auto-hard]` — player chart 의 piano track(channel 1, 1노트) + other chart 의 drum track(channel 2, 1노트) → 머지 chart `tracks.Count == 2`, 합집합 노트 수 검증.
   - `BuildAccompanimentDictFromChart_OffToggle_PropagatesToMergedChart` `[auto-hard]` — 동일 setup + `_instrumentToggleStates["drum"] = false` → `BuildAccompanimentDictFromChart(judgedChannel=1, merged) → {2: false}`.
   - `BuildMergedChartForSession_DoesNotMutatePlayerChart` `[auto-hard]` — `_loadedChart.tempoMap.segments[0].bpm = 100f` 박제 → `BuildMergedChartForSession(80f)` 호출 후 `_loadedChart.tempoMap.segments[0].bpm == 100f` 여전.

   Plan B 테스트와 동일 reflection helper 패턴 차용 — `BindingFlags.NonPublic | BindingFlags.Instance` + `FieldInfo.SetValue`.

6. **`SessionPanel.prefab` prefab edit (Unity MCP `manage_prefabs` 경유, 직접 YAML Edit 금지)**:
   - **새 prefab asset 생성**: `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab`. root GameObject(`InstrumentToggleButton`) 에 RectTransform + `UnityEngine.UI.Toggle` 컴포넌트 + `SessionPanel.InstrumentToggleButtonUI` 컴포넌트. 자식 GameObject(`Label`) 에 RectTransform + `TextMeshProUGUI` 컴포넌트(초기 text="instrument"). `InstrumentToggleButtonUI` 의 SerializedField `toggle` → root Toggle 컴포넌트 참조, `label` → 자식 TextMeshProUGUI 참조. 시각 디테일(배경 이미지/Layout 정렬/색상)은 사용자 인스펙터 후속 튜닝으로 위임 — 본 plan 은 직렬화 정합과 동작 확인까지만.
   - **`SessionPanel.prefab` 자식 추가**: `songSelectedPanel` 자식 트리 안 `difficultyContainer`/`bpmBar` 인접 위치(같은 부모) 에 신규 GameObject `InstrumentToggleContainer` 추가 (RectTransform + VerticalLayoutGroup 옵션, Layout Detail 은 사용자 후속 튜닝). 본 GameObject 의 Transform 을 `instrumentToggleContainer` 필드 후보로 사용.
   - **MonoBehaviour 직렬화 와이어**: `manage_prefabs` modify 액션으로 `SessionPanel.prefab` 의 `RhythmGameSectionController` 컴포넌트의 `instrumentToggleContainer` 필드 ← 신규 `InstrumentToggleContainer` Transform fileID, `instrumentToggleButtonPrefab` 필드 ← 신규 `InstrumentToggleButton.prefab` asset (guid + fileID type:3) 와이어.
   - 컴파일 대기·`read_console` 검증·screenshot 검증 절차는 [`unity-mcp-workflow`](../../../../.claude/skills/unity-mcp-workflow/SKILL.md) skill 이 단일 진실원 — 본 plan 적용 후 (a) `read_console` 으로 컴파일 에러 0 확인, (b) Editor 에서 prefab 인스턴스 열어 토글 row 가 컨테이너에 동적 추가됨을 시각 확인.

7. **Tech Spec Boundaries 준수 확인**: 본 plan 의 prefab 자식은 Toggle 위젯 + label 만 추가하므로 (i) NoteDisplayPanel 자동반주 시각화 추가 금지, (ii) 세션 도중 변경 금지(start 클릭 전 상태만), (iii) 음량/믹스 금지 3개 모두 침해 없음. 또한 Tech Spec Invariants 의 "곡 → 난이도 → 토글·템포 → 시작 순서" 가 그대로 유지(prefab edit 은 UI 노출만, 흐름 영향 없음), "player 악기는 토글 대상이 아니다" 도 controller `BuildInstrumentToggles` 가 player exclude 하므로 prefab 차원에서도 자연히 만족.

## Deliverables

- `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` (수정) — `_otherInstrumentCharts` 필드, `LoadChart` 끝에 활성 instrument 차트 파싱 루프, 신규 `BuildMergedChartForSession(float)` + `BuildAccompanimentDictFromChart(int, VmSongChart)`, `OnPlayButtonClicked` 의 mutate 를 머지 chart 경로로 치환, `ResetSelection` 에 `_otherInstrumentCharts.Clear()`.
- `Assets/SessionPanel/Tests/RhythmGameSectionMergedChartTests.cs` (신규) — EditMode 테스트 4건 (`BuildMergedChartForSession_ChannelMapMerged_NoDuplicates` / `BuildMergedChartForSession_TracksMerged_AllNotesPresent` / `BuildAccompanimentDictFromChart_OffToggle_PropagatesToMergedChart` / `BuildMergedChartForSession_DoesNotMutatePlayerChart`). reflection 으로 private 필드/메서드 직접 호출, file IO 0건.
- `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` (신규, `manage_prefabs` 경유) — root: RectTransform + UnityEngine.UI.Toggle + SessionPanel.InstrumentToggleButtonUI(toggle/label SerializedField 박제). 자식 `Label`: RectTransform + TextMeshProUGUI.
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab` (수정, `manage_prefabs` 경유) — `songSelectedPanel` 자식 트리에 `InstrumentToggleContainer` Transform 신규 추가(RectTransform + VerticalLayoutGroup). `RhythmGameSectionController` 컴포넌트의 `instrumentToggleContainer` 필드 ← 신규 Transform, `instrumentToggleButtonPrefab` 필드 ← 신규 `InstrumentToggleButton.prefab` 와이어.

## Acceptance Criteria

- [ ] `[auto-hard]` `RhythmGameSectionController.cs` 가 신규 필드 `Dictionary<int, VmSongChart> _otherInstrumentCharts` 와 신규 메서드 `BuildMergedChartForSession(float)`, `BuildAccompanimentDictFromChart(int, VmSongChart)` 를 가지며, `OnPlayButtonClicked` 가 `BuildMergedChartForSession(effectiveBpm)` 결과를 `host.StartSession(...)` 의 1번 인자로 전달한다.
  **검증:** `Grep -n "_otherInstrumentCharts|BuildMergedChartForSession|BuildAccompanimentDictFromChart" Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` + `Grep -n "host.StartSession\\(merged" Assets/SessionPanel/Scripts/RhythmGameSectionController.cs`.
- [ ] `[auto-hard]` `LoadChart()` 가 player 외 `_selectedSong.SupportedInstrumentIds` 각각에 대해 `GetChartPath(otherId, _selectedDifficulty)` non-null + 파일 존재 + `VmSongParser.Parse` 성공 + `channelMap.entries.Count > 0` 인 경우만 `_otherInstrumentCharts` 에 박제한다.
  **검증:** `Grep -n "_otherInstrumentCharts.Clear|_otherInstrumentCharts\\[" Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` + 그 블록 안 `string.IsNullOrEmpty(otherRel)` / `File.Exists(otherPath)` / `otherResult.Success` 가드 확인.
- [ ] `[auto-hard]` `ResetSelection()` 이 `_otherInstrumentCharts.Clear()` 를 호출해 곡·난이도 전환 시 잔류 chart 가 없다.
  **검증:** `Grep -n "_otherInstrumentCharts.Clear" Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` 결과에 `ResetSelection` 안 라인 1건 + `LoadChart` 안 라인 1건 = 총 2건.
- [ ] `[auto-hard]` 신규 EditMode 테스트 4건 PASS: `BuildMergedChartForSession_ChannelMapMerged_NoDuplicates` / `BuildMergedChartForSession_TracksMerged_AllNotesPresent` / `BuildAccompanimentDictFromChart_OffToggle_PropagatesToMergedChart` / `BuildMergedChartForSession_DoesNotMutatePlayerChart`.
  **검증:** `unity-test-runner` 서브에이전트 1회 호출 → SessionPanel.Tests EditMode 4건 PASS (기존 Plan B 4건 + Plan C 4건 = 총 8건 PASS).
- [ ] `[auto-hard]` `BuildMergedChartForSession(80f)` 결과 chart 의 `tempoMap.segments[0].bpm == 80f`, `channelMap.entries` 가 player + 활성 instrument 채널의 합집합(채널 중복 없음), `tracks` 가 player + 활성 instrument tracks 합집합.
  **검증:** 신규 EditMode 테스트 `BuildMergedChartForSession_ChannelMapMerged_NoDuplicates` + `BuildMergedChartForSession_TracksMerged_AllNotesPresent` 안 Assert 들이 본 조건 박제.
- [ ] `[auto-hard]` `_loadedChart` 원본은 `BuildMergedChartForSession(effectiveBpm != 박제값)` 호출 후에도 `tempoMap.segments[0].bpm` 이 원래 값 그대로(`effectiveBpm` 박제 영향 없음).
  **검증:** 신규 테스트 `BuildMergedChartForSession_DoesNotMutatePlayerChart` 의 Assert 가 `_loadedChart.tempoMap.segments[0].bpm == 100f` (호출 전 박제값) 박제.
- [ ] `[auto-hard]` `SessionPanel.prefab` 의 `RhythmGameSectionController` 직렬화 블록에 `instrumentToggleContainer` 와 `instrumentToggleButtonPrefab` 키가 모두 등장하며, 둘 다 `{fileID: 0}` 이 아닌 valid reference 로 와이어 되어 있다.
  **검증:** `Grep -n "instrumentToggleContainer:|instrumentToggleButtonPrefab:" Assets/SessionPanel/Prefabs/SessionPanel.prefab` 결과에 두 줄 모두 존재 + `instrumentToggleContainer: {fileID: 0}` 패턴 부재 (즉 non-zero fileID) + `instrumentToggleButtonPrefab` 는 guid 가 신규 prefab asset 의 .meta guid 와 일치.
- [ ] `[auto-hard]` `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 이 존재하며, 그 안에 `SessionPanel.InstrumentToggleButtonUI` MonoBehaviour 의 `toggle` SerializedField 와 `label` SerializedField 모두 `{fileID: 0}` 이 아닌 valid 내부 컴포넌트 참조로 와이어 되어 있다.
  **검증:** `Glob Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 결과 1건 + `Grep -n "Assembly-CSharp::SessionPanel.InstrumentToggleButtonUI" Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 매치 + 그 MonoBehaviour 블록 안 `toggle:` 와 `label:` 직렬 라인 모두 non-zero fileID.
- [ ] `[auto-soft]` Unity Editor 로드 직후 `read_console` 에 SessionPanel/Prefab 관련 컴파일 에러·MissingComponent 경고가 0건.
  **검증:** `mcp__UnityMCP__read_console` 으로 `types=["error","warning"]` + `filter_text="SessionPanel"` 또는 `filter_text="InstrumentToggle"` 1회 조회 후 0건 확인.
- [ ] `[auto-soft]` Plan A 의 `RhythmAccompaniment.ShouldFire(channel)` 게이트가 OFF 토글 채널에서 false 를 반환한다 (Plan A 기존 동작 회귀 없음). 머지 chart 의 entry 가 dict 키에 등장하므로 fallback ON 분기가 아닌 명시적 false 경로로 진입.
  **검증:** Plan A 의 기존 RhythmAccompaniment 단위 테스트가 있다면 회귀 PASS 확인, 없다면 본 plan 의 `BuildAccompanimentDictFromChart_OffToggle_PropagatesToMergedChart` 의 `dict[2] == false` Assert 가 ShouldFire 입력 정합성을 박제하는 것으로 갈음.
- [ ] `[manual-hard]` Unity Editor Play 모드에서 SessionPanel 을 열고: (1) 곡 row 클릭 → song-detail 패널이 노출되며 **difficultyContainer/bpmBar 인근에 `InstrumentToggleContainer` 가 보이고, 그 안에 player 외 활성 instrument(예: drum, violin) 각각의 토글 row 가 dynamic-instantiate 되어 시각 표시됨**, 라벨 텍스트가 instrument id 와 일치, 토글 위젯이 클릭 가능. (2) drum 토글 OFF → BPM 슬라이더로 effectiveBpm 변경(예: 80) → Play 버튼 → drum 자동 반주가 **시간 내 한 번도 발화하지 않음**(귀로 무음 확인 + drum 악기 시각 ping 없음), piano 노트가 80 BPM 기준 시각에 판정선 도달, violin 토글이 ON 인 상태라면 violin 자동 반주가 80 BPM 기준 시각에 발화. (3) 세션 종료 후 drum 토글 ON 으로 되돌려 재시작 시 drum 도 80 BPM 기준 발화.
  **검증:** Editor Play 모드 시뮬레이션 1회 — (1) UI 시각 (토글 row 표시 + 라벨 정합 + 클릭 동작) + (2) drum OFF 시 drum 무음 + piano 80BPM 판정 정상 + (violin 존재 시) violin 80BPM 발화 + (3) 토글 ON 복귀 시 drum 80BPM 발화 3단계 시각·청각 관찰.

## Out of Scope

- `InstrumentToggleButton.prefab` 의 시각 디테일(배경 이미지/Layout 정렬/색상/Toggle checkmark 그래픽). 본 plan 은 직렬화 정합과 동작 확인까지만 — 시각 마감은 사용자 인스펙터 후속 튜닝으로 위임.
- 트랙별 음량/믹스 (sub-spec 07 Boundaries).
- NoteDisplayPanel 에 자동 반주 노트 시각화 (sub-spec 07 Boundaries).
- 세션 도중 토글·BPM 변경 (sub-spec 07 Boundaries).
- 머지 chart 의 `tracks` 가 동일 channel 에 player + other 양쪽 노트를 가진 경우의 dedupe/병합 (현재 단계에선 instrument 분리 vmsong 구조상 channel 충돌은 발생하지 않음 — `BuildMergedChartForSession` 의 `seen` HashSet 이 channelMap 중복만 막고 tracks 자체는 그대로 append).
- player 외 instrument vmsong 의 `[Tempo]` 가 player 와 다른 경우의 충돌 정책 (sub-spec 07 Invariants — "본 sub-spec 은 player 선택 파일의 `[Tempo]` 만 초기값으로 사용"). 머지 chart 의 tempoMap 은 player 의 것을 그대로 쓰고 effectiveBpm 만 박제.

## Notes

- `BuildAccompanimentDict(int)` (Plan B 의 internal seam) 는 무수정 유지 — Plan B 직접 박제 테스트 4건의 reflection 호출이 그대로 PASS 되어야 하므로. 신규 `BuildAccompanimentDictFromChart(int, VmSongChart)` 가 본 plan 의 production 경로 + 신규 테스트 호출. Plan B 기존 테스트는 회귀 PASS.
- `_otherInstrumentCharts` 의 key 를 channel int 로 잡은 이유: instrument 별 chart 의 첫 channel(보통 그 instrument 의 단일 채널) 이 머지 dedupe 와 1:1 대응. instrument id 문자열 key 로 잡아도 무방하나 머지 시 다시 channel 추출이 필요해 1단 우회 → channel key 가 단순.
- 머지 chart 의 `tracks` 가 외부 chart 의 List<ChartNote> reference 를 그대로 공유한다(얕은 복사). RhythmAccompaniment 는 read-only 로만 노트를 소비하므로 안전. effectiveBpm 박제는 `tempoMap.segments[0]` struct 의 reassignment 뿐이라 외부 chart 영향 없음.
- prefab edit 은 `manage_prefabs` MCP 경유 강제. YAML 직접 Edit 은 CLAUDE.md 의 "직렬화 자산 수정 MCP 우선" 박스 위반이므로 본 plan 적용 시 금지. precondition_sha256·컴파일 대기 등 절차는 [`unity-mcp-workflow`](../../../../.claude/skills/unity-mcp-workflow/SKILL.md) skill 이 단일 진실원.
- 2026-05-18 자동 AC 결과: 10/10 PASS.
  - AC1~AC3 (코드 시그니처·LoadChart 가드·ResetSelection Clear): PASS — Grep 매치.
  - AC4 (신규 EditMode 4건): PASS — RhythmGameSectionMergedChartTests 4건 PASS. SessionPanel.Tests 23/23.
  - AC5~AC6 (머지 chart 정합·원본 보존): PASS — test sub-assert.
  - AC7~AC8 (SessionPanel.prefab 와이어·InstrumentToggleButton.prefab): PASS — manage_prefabs MCP 경유 prefab 직접 생성·와이어 (메인 세션 직접 수행, orchestrator 도구 미가용 보완).
  - AC9~AC10 (console 0건·Plan A 회귀): PASS.
- 2026-05-18 manual-hard 검증 (TestSceneSanyo, 사용자 직접 Play 모드):
  - Piano 잡고 drum 토글 ON → drum 자동 반주 발화 확인.
  - Piano 잡고 drum 토글 OFF → drum 무음 확인.
  - DrumKit 잡고 piano 토글 ON → piano 자동 반주 발화 확인.
  - DrumKit 잡고 piano 토글 OFF → piano 무음 확인.
  - sub-spec 07 의 What·Behavior 5조항 모두 충족.
- 부수 발견 + 보완:
  - Plan A/B/C 모두 production 코드 완성 후에도 drum 무음. 원인: Piano.prefab + DrumKit.prefab 의 RhythmGameHost 컴포넌트가 RhythmAccompaniment 컴포넌트를 자식·필드로 보유하지 않아 host.StartSession 의 accompaniment?.Begin(...) 호출이 null-check 로 skip 됐다. Plan A~C 모두 sub-spec 07 의 의존면(`RhythmGameHost.accompaniment` SerializedField가 실제 와이어돼 있음)을 가정했으나 실제 prefab 에는 부재.
  - 보완 작업: Piano.prefab + DrumKit.prefab 각각의 RhythmGameHost GameObject 에 RhythmAccompaniment 컴포넌트 신규 추가 + accompaniment SerializedField 와이어. manage_prefabs + manage_components MCP 경유.
- InstrumentToggleContainer 의 RectTransform 초기값(localScale 1000, anchoredPosition -220590,-249980)이 비정상이라 song-detail 영역을 가려 모든 버튼이 raycast 차단됐다. 정상화: anchorMin/Max (0,0), anchoredPosition (0,0), sizeDelta (100,100), localScale (1,1,1), localPosition (-110,-200).
- InstrumentToggleButton.prefab 시각 마감: Background Image(stretch, 짙은 파랑) + Checkmark Image(중앙 20×20, 연노랑) 자식 추가 + Toggle.targetGraphic/graphic 와이어 + isOn=true. plan Out of Scope 였으나 manual-hard 시각 검증 위해 보완.

## Handoff

Plan C 완료. sub-spec 07 전체(Plan A/B/fix/C) Done.

산출 (Plan C 단독):
- RhythmGameSectionController _otherInstrumentCharts + BuildMergedChartForSession(effectiveBpm) + BuildAccompanimentDictFromChart(int, VmSongChart) + OnPlayButtonClicked 가 머지 chart 를 host.StartSession 에 전달.
- LoadChart 가 player 외 활성 instrument 차트(GetChartPath non-null 파일)를 _otherInstrumentCharts 에 보관.
- ResetSelection 이 _otherInstrumentCharts.Clear() 호출.
- BuildMergedChartForSession 이 _loadedChart 원본 보존(in-memory 얕은 복제), 활성 instrument 채널·트랙 머지(channel HashSet dedupe), effectiveBpm 을 머지 chart 의 tempoMap.segments[0].bpm 으로 박제.
- RhythmGameSectionMergedChartTests 4 EditMode 테스트 PASS.

산출 (Plan C 안 prefab/시각 보완):
- InstrumentToggleButton.prefab 신규 생성 + Background/Checkmark 자식 시각 + Toggle.targetGraphic/graphic 와이어.
- SessionPanel.prefab 의 SongSelectedPanel 자식에 InstrumentToggleContainer Transform 추가 + RhythmGameSectionController.instrumentToggleContainer/instrumentToggleButtonPrefab 와이어.

산출 (Plan A/B/C 의존면 보완 — 부수 prerequisite):
- Piano.prefab + DrumKit.prefab 각각의 RhythmGameHost 컴포넌트에 RhythmAccompaniment 컴포넌트 추가 + accompaniment SerializedField 와이어. 본 와이어가 없으면 Plan A 의 RhythmAccompaniment.Begin 자체가 호출되지 않아 Plan B/C 전부 무효화됐다.

sub-spec 07 전체 manual-hard 사용자 직접 검증 완료. Plan A/B/fix/C 모두 _archive 이동.

후속 plan 후보 (현 시점 호출 없음):
- 세션 도중 토글·템포 변경 (sub-spec 07 Boundaries — 별도 sub-spec 후보).
- 자동 반주 트랙 음량/믹스 (session-panel/03-volume-section 또는 후속).
- 머지 chart 의 channel 충돌 정책 (sub-spec 06 의 instrument 분리 vmsong 가정상 발생 안 함, 향후 확장 시 정책 박제 필요).
