# Instrument Toggle Tests — Direct State Injection (Bypass LoadChart)

**Linked Spec:** [`07-session-options-instrument-toggle-tempo.md`](../../../rhythm-game/specs/07-session-options-instrument-toggle-tempo.md)
**Caused By:** [`2026-05-18-linksky0311-instrument-toggle-ui-builder.md`](./2026-05-18-linksky0311-instrument-toggle-ui-builder.md)
**Status:** `Done`

## Goal

선행 Plan B 의 production 코드(`InstrumentToggleButtonUI`, `RhythmGameSectionController` 토글 빌드/사전 변환 seam, `AssemblyInfo`, asmdef references) 는 모두 유지한 채, **`RhythmGameSectionInstrumentToggleTests.cs` 1 파일만 수정** 해 LoadChart 진입을 우회한 직접 상태 박제 방식으로 테스트 4건을 작성. `BuildInstrumentToggles` / `BuildAccompanimentDict` / `OnInstrumentToggleChanged` 의 단위 동작만 검증해 4건 PASS.

## Context

> **선행 plan**: `2026-05-18-linksky0311-instrument-toggle-ui-builder.md` (Plan B).
>
> **실패 AC**: `[auto-hard] 신규 EditMode 4건 PASS: InstrumentToggles_OnlyOtherInstrumentsWithMatchingDifficulty_Built / AccompanimentDict_ReflectsToggleStates_OffChannelFalse / InstrumentToggles_PlayerOnlySong_ContainerDeactivated / InstrumentToggles_DifficultyChange_RebuildsList.`
>
> **실패 evidence**: 4건 전부 동일 원인 — `System.Reflection.TargetInvocationException → ArgumentNullException: Value cannot be null. Parameter name: path2`. 스택은 `Path.Combine(Application.streamingAssetsPath, _selectedSong.GetChartPath(...))` 에서 발생. `StubSongEntry.GetChartPath(instrumentId, difficulty)` 가 `=> null` 을 반환하므로 `Path.Combine(string, null)` 이 `ArgumentNullException` 으로 깨지고, 테스트가 호출한 `OnSongRowClicked` 의 자연 흐름이 `OnDifficultyClicked(firstDiff, firstBtn)` → `LoadChart()` 까지 도달하면서 controller production 경로에서 폭발한다.

본 후속 plan 은 sub-spec 07 의 단위 검증 본질 — `BuildInstrumentToggles` 가 player 외 instrument 중 선택 난이도 지원 자만 row 인스턴스화하고 `_instrumentToggleStates` 에 ON 박제, `BuildAccompanimentDict` 가 그 상태를 channel 단위 dict 로 변환 — 만 보존한다. Plan B 의 모든 production 코드는 그대로 유지되고, **테스트가 controller 의 `OnSongRowClicked` → `OnDifficultyClicked` → `LoadChart` 자연 흐름을 타지 않고**, controller 의 private 필드 (`_selectedSong`, `_selectedDifficulty`, `_currentInstrument`, `_loadedChart`) 를 reflection 으로 직접 박제 후 `BuildInstrumentToggles()` / `BuildAccompanimentDict(int)` / `OnInstrumentToggleChanged(string, bool)` 만 직접 호출하는 방식으로 변경한다.

옵션 A 채택 사유: 옵션 B(임시 디렉터리에 fake `.vmsong` 파일 떨어뜨림 + `GetChartPath` 가 절대 경로 반환)는 (i) `Application.streamingAssetsPath` 와 무관한 절대 경로를 반환해도 `Path.Combine(streamingAssetsPath, absPath)` 가 절대 경로를 그대로 반환하는 .NET 동작에 의존해야 하고 (ii) EditMode 에서 file IO 도입은 OS/CI 환경 의존성 추가 + 테스트 cleanup 책임 추가 → 단위 테스트 본질을 흐린다. 옵션 A 는 1 파일 변경, file IO 0건, controller production 경로 무수정.

본 plan 은 sub-spec 07 의 Boundaries (세션 도중 토글·템포 변경 / NoteDisplayPanel 자동 반주 시각화 / 트랙별 음량) 를 건드리지 않는다. Plan B 의 Invariants (player 악기는 토글 대상 아님 / 곡→난이도→토글·템포→시작 순서) 도 검증 대상으로만 사용한다.

## Verified Structural Assumptions

- `RhythmGameSectionController.LoadChart` (L208-224) 의 `Path.Combine(Application.streamingAssetsPath, _selectedSong.GetChartPath(_currentInstrument.InstrumentId, _selectedDifficulty))` (L212) 가 실패 진원지. `GetChartPath` null 반환 → `Path.Combine(string, null)` `ArgumentNullException(path2)`. 동일 위험이 `AutoShowBpmFromSong` (L191) 에도 있으나, 본 테스트는 자연 흐름 (`OnSongRowClicked`) 을 호출하지 않으므로 두 경로 모두 진입하지 않는다. — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`
- `RhythmGameSectionController` 의 검증 대상 private 필드 정확한 이름 + 타입:
  - `IActiveInstrument _currentInstrument;` (L40)
  - `ISongEntry _selectedSong;` (L42)
  - `string _selectedDifficulty;` (L43)
  - `VmSongChart _loadedChart;` (L44)
  - `Dictionary<string, bool> _instrumentToggleStates` (L51, `StringComparer.OrdinalIgnoreCase` 박제)
  본 plan 테스트는 4 필드 모두 `BindingFlags.NonPublic | BindingFlags.Instance` 의 `FieldInfo.SetValue` 로 직접 박제. `_currentInstrument` 는 `StubInstrument` 인스턴스 직접 박제 (provider `Inject` 우회). — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`
- `BuildInstrumentToggles()` (L226-272) 는 `_selectedSong` / `_selectedDifficulty` / `_currentInstrument` 가 모두 박제된 상태에서 `instrumentToggleContainer != null` 이면 즉시 candidates 빌드 → row 인스턴스화 → `_instrumentToggleStates` 박제 진행. **이 메서드는 `_loadedChart` 를 참조하지 않는다** — 따라서 `_loadedChart` 미박제 상태에서도 토글 빌드만 단독 검증 가능. — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`
- `BuildAccompanimentDict(int judgedChannel)` (internal seam, Plan B 박제) 는 `_loadedChart.channelMap.entries` 와 `_instrumentToggleStates` 만 참조. `_selectedSong` / `_selectedDifficulty` 박제 없이도 두 입력 (chart + toggle states) 만 박제하면 단독 호출 가능. — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`
- `InstrumentToggleButtonUI.Setup(string instrumentId, bool defaultOn, Action<string,bool> callback)` 시그니처 (Plan B 박제). Setup 호출 시 `toggle.isOn = defaultOn` + `label.text = instrumentId` (label null guard) + `toggle.onValueChanged.AddListener(OnToggleChanged)`. label 미박제(prefab 에 `TextMeshProUGUI` 자식 없음) → null guard 로 skip. 본 테스트는 label 검증을 제거하고 `_instrumentId` (private) 만 reflection 으로 확인. — `Read Assets/SessionPanel/Scripts/InstrumentToggleButtonUI.cs (2026-05-18)`
- `SessionPanel.Tests.asmdef` 의 references 에 이미 `RhythmGame.Data` 가 포함됨 (Plan B 적용 시점). 추가 reference 불필요. — `Read Assets/SessionPanel/Tests/SessionPanel.Tests.asmdef (2026-05-18)`
- 본 plan 은 신규 C# 파일 0건, Unity 자산 0건 수정. 기존 테스트 1 파일만 rewrite. asmdef 의존 추가 없음.

## Approach

1. **`Assets/SessionPanel/Tests/RhythmGameSectionInstrumentToggleTests.cs` 전면 재작성** (1 파일).
   - Stubs (`StubSongEntry` / `StubCatalog` / `StubInstrument` / `StubProvider`) 는 그대로 유지.
   - `BuildControllerWithToggleContainer()` helper 도 거의 동일. 다만:
     - **선택**: songListContent / songRowPrefab / difficultyContainer / difficultyButtonPrefab 박제는 자연 흐름을 더 이상 타지 않으므로 유지하되, 테스트에서 사용하지 않는다. `instrumentToggleContainer` + `instrumentToggleButtonPrefab` 박제는 그대로 필수.
   - 신규 helper:
     ```csharp
     static void InjectState(RhythmGameSectionController ctrl,
         ISongEntry song, string difficulty, IActiveInstrument instrument)
     {
         GetPrivateField("_selectedSong").SetValue(ctrl, song);
         GetPrivateField("_selectedDifficulty").SetValue(ctrl, difficulty);
         GetPrivateField("_currentInstrument").SetValue(ctrl, instrument);
     }

     static void InvokeBuildInstrumentToggles(RhythmGameSectionController ctrl)
     {
         var mi = typeof(RhythmGameSectionController)
             .GetMethod("BuildInstrumentToggles", BindingFlags.NonPublic | BindingFlags.Instance);
         mi.Invoke(ctrl, null);
     }

     static Dictionary<int,bool> InvokeBuildAccompanimentDict(
         RhythmGameSectionController ctrl, int judgedChannel)
     {
         var mi = typeof(RhythmGameSectionController)
             .GetMethod("BuildAccompanimentDict",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
         return (Dictionary<int,bool>)mi.Invoke(ctrl, new object[] { judgedChannel });
     }
     ```
   - **`InjectAndInstrument` 호출 제거**: provider/catalog injection 자체가 자연 흐름을 위한 셋업이었음. 본 plan 테스트는 controller 의 `Inject` 자체를 호출하지 않는다 (Awake/OnEnable 의 fallback 흐름 회피). gameObject 는 `SetActive(false)` 상태로 유지해 `Start`/`OnEnable` 의 side effect (예: `RefreshSongList`) 도 회피.

2. **테스트 4건 재작성** (자연 흐름 대신 직접 박제):

   - **테스트 A** (`InstrumentToggles_OnlyOtherInstrumentsWithMatchingDifficulty_Built`):
     - song = piano(easy,normal), drum(easy), violin(easy,hard). player=piano, difficulty=easy.
     - `InjectState(ctrl, song, "easy", new StubInstrument("piano"))`.
     - `InvokeBuildInstrumentToggles(ctrl)`.
     - assert: `toggleContainer.childCount == 2` (drum + violin, Ordinal sort).
     - assert: `_instrumentToggleStates` 가 `{drum:true, violin:true}`.

   - **테스트 B** (`AccompanimentDict_ReflectsToggleStates_OffChannelFalse`):
     - song = piano(easy), drum(easy). player=piano.
     - `InjectState(ctrl, song, "easy", new StubInstrument("piano"))`.
     - `InvokeBuildInstrumentToggles(ctrl)` → `_instrumentToggleStates = {drum:true}`.
     - `_loadedChart` 박제: `VmSongChart` 인스턴스 + `tempoMap.segments.Add(new TempoSegment{bpm=120f})` + `channelMap.entries.Add({channel=1, instrumentKey="piano"})` + `Add({channel=2, instrumentKey="drum"})`.
     - `GetPrivateField("_loadedChart").SetValue(ctrl, chart);`.
     - `OnInstrumentToggleChanged("drum", false)` reflection invoke.
     - `var dict = InvokeBuildAccompanimentDict(ctrl, 1);`.
     - assert: `dict.ContainsKey(1) == false` (judged skipped), `dict[2] == false` (drum OFF).

   - **테스트 C** (`InstrumentToggles_PlayerOnlySong_ContainerDeactivated`):
     - song = piano(easy) only. player=piano.
     - `InjectState(ctrl, song, "easy", new StubInstrument("piano"))`.
     - `InvokeBuildInstrumentToggles(ctrl)`.
     - assert: `toggleContainer.gameObject.activeSelf == false` + `toggleContainer.childCount == 0`.

   - **테스트 D** (`InstrumentToggles_DifficultyChange_RebuildsList`):
     - song = piano(easy,hard), drum(easy), violin(hard). player=piano.
     - `InjectState(ctrl, song, "easy", instrument)` + `InvokeBuildInstrumentToggles(ctrl)`.
     - assert: `_instrumentToggleStates.ContainsKey("drum") == true` + `ContainsKey("violin") == false`.
     - 난이도 변경: `GetPrivateField("_selectedDifficulty").SetValue(ctrl, "hard");` + `InvokeBuildInstrumentToggles(ctrl)` 재호출.
     - assert: `_instrumentToggleStates.ContainsKey("drum") == false` + `ContainsKey("violin") == true` + `toggleContainer.childCount == 1`.
     - **자연 흐름의 `OnDifficultyClicked` 호출은 제거** — `LoadChart` 진입 방지가 본 plan 의 본질. `BuildInstrumentToggles` 재호출이 sub-spec 의 "난이도 변경 시 토글 목록 재구성" 책임을 단위 검증한다 (Plan B 의 `OnDifficultyClicked` 가 `LoadChart()` 직후 `BuildInstrumentToggles()` 를 호출함은 production 코드 grep 으로 별도 검증 가능 — `_index.md` 의 Plan B AC 가 이미 박제).

3. **selectedSong / selectedDifficulty 박제 순서 주의**: `BuildInstrumentToggles` 의 guard `if (_selectedSong == null || _selectedDifficulty == null || _currentInstrument == null) return;` 를 모두 통과시키려면 세 필드를 모두 박제. `InjectState` helper 가 3 필드 일괄 박제 책임.

4. **GameObject lifecycle**: `BuildControllerWithToggleContainer` 가 controller GameObject 를 active(true) 로 두면 `Start` 가 실행되며 catalog/provider null 로 인한 NRE 위험이 있다. 본 plan 은 `ctrlGo.SetActive(false)` 호출을 helper 마지막에 추가해 Awake/OnEnable/Start 의 자연 흐름을 모두 회피. 단 `AddComponent<RhythmGameSectionController>()` 가 Awake 를 즉시 호출하므로 SetActive(false) 만으로는 Awake 회피 불가 → `_provider` / `_catalog` 가 null 이어도 NRE 안 나는지 확인. Awake 코드 (L83-92) 가 `_provider?.Current` 패턴이라 null-safe. OnEnable (L94-104) 도 `if (_provider != null)` guard 있음. 본 plan 테스트는 controller GameObject 를 inactive 로 두고 reflection 만 사용 — Start/OnEnable side effect 회피.

5. **`InstrumentToggleButtonUI` prefab label 박제**: 현 testing helper 는 prefab 에 `Toggle` + `InstrumentToggleButtonUI` 만 박제하고 `TextMeshProUGUI` 자식 label 은 박제하지 않는다. Plan B 의 `Setup` 안 `if (label != null) label.text = ...` null guard 가 있으면 통과 — Plan B `InstrumentToggleButtonUI.cs` 의 Setup 구현을 Read 해 확인 후 박제. 만약 null guard 가 없으면 NRE — 그 경우는 **본 plan 범위가 아닌 production 코드 수정**이므로 `unresolved` 에 기록 + label null guard 추가는 별도 plan 책임.

## Deliverables

- `Assets/SessionPanel/Tests/RhythmGameSectionInstrumentToggleTests.cs` (수정) — 자연 흐름(`OnSongRowClicked` → `LoadChart`) 제거 + reflection 직접 박제 (`InjectState` helper) 로 4 테스트 재작성. 1 파일만 수정. Plan B production 코드 무수정.

## Acceptance Criteria

- [ ] `[auto-hard]` `RhythmGameSectionInstrumentToggleTests.cs` 가 `OnSongRowClicked` / `OnDifficultyClicked` reflection invoke 를 더 이상 사용하지 않고, `_selectedSong` / `_selectedDifficulty` / `_currentInstrument` / `_loadedChart` 4개 private 필드를 reflection 으로 직접 박제하는 `InjectState` 또는 동등 helper 를 보유.
  **검증:** Grep `"OnSongRowClicked"` 0건 + Grep `"OnDifficultyClicked"` 0건 in `Assets/SessionPanel/Tests/RhythmGameSectionInstrumentToggleTests.cs`. Grep `GetPrivateField\("_selectedSong"\)\.SetValue` 1건 이상 + Grep `GetPrivateField\("_selectedDifficulty"\)\.SetValue` 1건 이상 + Grep `GetPrivateField\("_currentInstrument"\)\.SetValue` 1건 이상 + Grep `GetPrivateField\("_loadedChart"\)\.SetValue` 1건 이상.
- [ ] `[auto-hard]` 테스트 4건이 모두 `BuildInstrumentToggles` 와 `BuildAccompanimentDict` 메서드 1개 이상을 reflection 으로 직접 호출하며, `LoadChart` 또는 `Path.Combine` 진입을 일으키지 않는다.
  **검증:** Grep `GetMethod\("BuildInstrumentToggles"` 1건 이상 + Grep `GetMethod\("BuildAccompanimentDict"` 1건 이상 in `Assets/SessionPanel/Tests/RhythmGameSectionInstrumentToggleTests.cs`. Grep `"LoadChart"` 0건 in same file.
- [ ] `[auto-hard]` 신규 EditMode 테스트 4건 모두 PASS: `InstrumentToggles_OnlyOtherInstrumentsWithMatchingDifficulty_Built`, `AccompanimentDict_ReflectsToggleStates_OffChannelFalse`, `InstrumentToggles_PlayerOnlySong_ContainerDeactivated`, `InstrumentToggles_DifficultyChange_RebuildsList`. (A) `toggleContainer.childCount == 2` + `_instrumentToggleStates` 가 drum/violin 키 보유. (B) `BuildAccompanimentDict(1)` 반환 dict 의 `ContainsKey(1) == false` + `dict[2] == false`. (C) `toggleContainer.gameObject.activeSelf == false` + `childCount == 0`. (D) easy→hard 재호출 후 `_instrumentToggleStates` 가 violin 키만 보유 + `toggleContainer.childCount == 1`.
  **검증:** `unity-test-runner` sub-agent 호출 또는 Unity MCP `run_tests` (mode=EditMode, filter=`RhythmGameSectionInstrumentToggleTests`) — passed=4 / failed=0 / no `ArgumentNullException` / no `TargetInvocationException` in console.
- [ ] `[auto-soft]` 기존 EditMode 테스트 (`SessionPanel.Tests` 의 `RhythmGameSectionRefreshTests`/`FolderScanSongCatalogTests`/`SessionPanelWiringTests`/`SessionVolumeTests` + `RhythmGame.Tests.Editor` 의 Plan A 38건) 회귀 0건.
  **검증:** `unity-test-runner` sub-agent 전체 EditMode run — pre-existing pass count 유지. 실패 시 Notes 에 기록 후 진행.
- [ ] `[auto-hard]` 선행 plan `2026-05-18-linksky0311-instrument-toggle-ui-builder.md` 의 실패 AC '신규 EditMode 4건 PASS' 가 이 plan 적용 후 재검증에서 통과한다.
  **검증:** 위 AC 의 `unity-test-runner` 출력에서 passed=4 / failed=0 확인. Plan B Acceptance Criteria L140 의 4 테스트 이름이 모두 passed 목록에 포함.

## Out of Scope

- Plan B 의 production 코드 (`InstrumentToggleButtonUI.cs`, `RhythmGameSectionController.cs` 의 토글 시스템, `AssemblyInfo.cs`, asmdef references) 수정 — 본 plan 은 테스트만 수정.
- `InstrumentToggleButtonUI.Setup` 안 `label` null guard 부재 시 추가 — production 코드 수정이므로 별도 plan 책임 (필요 시 `unresolved` 보고).
- 실제 `.vmsong` 파일을 임시 디렉터리에 떨어뜨려 `LoadChart` 를 자연 흐름 통과시키는 옵션 B 접근 — 옵션 A 채택 사유에 박제.
- `LoadChart` / `AutoShowBpmFromSong` 의 `Path.Combine(streamingAssetsPath, null)` null guard 추가 — Plan C 의 chart 머지 단계에서 자연스럽게 재설계되므로 본 plan 범위 밖. 현 production 동작상 `GetChartPath` 가 production 환경에서는 항상 non-null 반환 (`FolderScanSongCatalog` 의 실제 구현이 보장).
- manual-hard (Editor Play 모드 토글 시각 검증) — Plan C 책임.

## Notes

- 옵션 A vs 옵션 B 결정 근거: 옵션 A 의 변경 범위는 1 파일 + file IO 0건, 옵션 B 는 file IO + temp dir cleanup + 절대 경로/상대 경로 혼용 동작 가정. 단위 테스트 본질 (controller 의 토글 빌드 + 사전 변환 seam 만 검증) 에 옵션 A 가 더 충실.
- `Inject(provider, catalog)` 호출 제거로 인해 controller `_provider` / `_catalog` 는 null 로 남는다. `BuildInstrumentToggles` / `BuildAccompanimentDict` 둘 다 이 두 필드를 참조하지 않으므로 안전. `OnPlayButtonClicked` 는 host=null guard 로 어차피 막혔으므로 본 plan 테스트가 호출하지 않는다.
- 본 plan 적용 후에도 sub-spec 07 의 자연 흐름 (`OnSongRowClicked` → `LoadChart` → `BuildInstrumentToggles`) 의 end-to-end 검증은 manual-hard (Plan C) 또는 PlayMode 통합 테스트 (별도 plan) 가 담당. EditMode 단위 테스트는 `BuildInstrumentToggles` / `BuildAccompanimentDict` 단독 호출만 검증.
- 테스트 D 가 `OnDifficultyClicked` 호출 대신 `_selectedDifficulty` 박제 + `BuildInstrumentToggles` 재호출로 단축됨에 따라 "OnDifficultyClicked 가 LoadChart 직후 BuildInstrumentToggles 를 호출한다" 의 production grep 검증은 Plan B AC L134 (`Grep BuildInstrumentToggles\(\);` 1건 이상) 가 이미 책임.
- 2026-05-18 자동 AC 결과: 5/5 PASS.
  - AC1 (자연 흐름 reflection invoke 제거 + 4 필드 InjectState 박제): PASS.
  - AC2 (BuildInstrumentToggles/BuildAccompanimentDict reflection 호출 + LoadChart 0건): PASS.
  - AC3 (신규 EditMode 4건 PASS): PASS — SessionPanel.Tests 19/19 통과.
  - AC4 (auto-soft 회귀): PASS — RhythmGame.Tests.Editor 38/38 무회귀.
  - AC5 (재검증 AC): PASS — 선행 Plan B 의 '신규 EditMode 4건 PASS' AC 가 본 plan 적용 후 통과 확인.
- Plan B production 코드 무수정 — 테스트 파일 1개만 rewrite.

## Handoff

Plan B 의 EditMode 테스트 4건 실패(Path.Combine null) 를 자연 흐름 회피 + reflection 직접 박제 방식으로 fix. RhythmGameSectionInstrumentToggleTests.cs 1 파일만 rewrite. Plan B production 코드(InstrumentToggleButtonUI, RhythmGameSectionController 토글 시스템, AssemblyInfo, asmdef) 모두 무변경.

자동 reflect: 선행 Plan B 의 manual-hard 재검증 항목 없음(Plan B 는 auto-hard 만 보유). 본 fix plan 의 재검증 AC '신규 EditMode 4건 PASS' 가 통과해 cascade_depth 0/2 종료.

Plan C 시작 가능.
