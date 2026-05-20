# Multi-File Song Catalog — 파일명 파싱·그루핑·인터페이스 확장

**Linked Spec:** [`06-multi-file-song-catalog.md`](../../../rhythm-game/specs/06-multi-file-song-catalog.md)
**Status:** `Done`

## Goal

`StreamingAssets/Songs/` 의 `.vmsong` 파일을 `<songname>-<instrument>-<difficulty>.vmsong` 명명 규약으로 파싱하고, 같은 `<songname>` 파일들을 하나의 곡 엔트리로 묶어 (instrument, difficulty) → 파일 경로를 노출하도록 `FolderScanSongCatalog` 와 `ISongEntry` 를 갱신한다. 후속 sub-spec 07(인스트루먼트 토글·템포)이 필요한 인터페이스 면을 본 plan에서 확정한다.

## Context

- sub-spec 05(Song Catalog)에서 도입된 `FolderScanSongCatalog` 는 한 `.vmsong` 파일 = 한 곡 엔트리 가정으로 동작하며, instrument 키는 본문 `[Channels]` 섹션에서 추출했다. 한 파일에 여러 악기 트랙(piano + DrumKit)이 들어 있는 `test.vmsong` 이 그 가정의 산출물.
- sub-spec 06은 그 가정을 뒤집는다: 한 파일 = 한 (instrument, difficulty) 트랙. 같은 `<songname>` 파일들이 한 곡으로 묶인다. 본 plan은 그 핵심을 `FolderScanSongCatalog.BuildEntriesFromFolder` 와 `ISongEntry` 에 박는다.
- 사용자 결정: 옛 단일 파일 다중-트랙 포맷(`test.vmsong`)은 **제거 + 신 포맷으로 마이그레이션**. 본 plan deliverables 에 `test.vmsong` 의 신 포맷 파일들로의 분할·교체를 포함한다 (자동 분할이 가능한 단순 구조 — `[Track:1]` → piano, `[Track:10]` → DrumKit).
- 씬 와이어링(`SampleScene`·`TestSceneSanyo` 의 `_songCatalogObject`)은 `FolderScanSongCatalog` 컴포넌트 인스턴스를 그대로 참조하므로 본 plan에서 씬 자산은 수정 불필요. 내부 구현·`ISongEntry` 구현만 갱신.
- `RhythmGameSectionController` 는 `song.GetChartPath(difficulty)`, `song.SupportedInstrumentIds.Contains(InstrumentId)`, `song.Difficulties` 를 호출한다. **`Difficulties` 는 곡별로 가변 집합이 되어야 한다 (07이 정식 도입하지만 본 plan에서 이미 데이터 모델은 곡 단위 변동 지원).**

## Verified Structural Assumptions

- `ISongCatalog` / `ISongEntry` 현재 시그니처: `Songs : IReadOnlyList<ISongEntry>`, `Changed : event Action`, `ISongEntry` 멤버 = `SongId`, `Title`, `Artist`, `Difficulties : IReadOnlyList<string>`, `SupportedInstrumentIds : IReadOnlyCollection<string>`, `GetChartPath(difficulty) : string` — `Read Assets/SessionPanel/Scripts/ISongCatalog.cs (2026-05-18)`.
- `FolderScanSongCatalog.BuildEntriesFromFolder(absoluteFolderPath, relativePrefix)` 는 static helper로 EditMode 테스트에서 직접 호출됨. 내부적으로 `VmSongParser.Parse(text)` → `parseResult.chart.channelMap.entries[*].instrumentKey` 를 `OrdinalIgnoreCase` HashSet 으로 수집해 `SongEntryImpl` 생성. 본 plan은 시그니처 호환 유지(외부 호출자 영향 없음). `_difficulties` 는 현재 `["Easy", "Normal", "Hard"]` 정적 배열로 하드코딩되어 모든 곡에 동일 반환 — `Read Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs (2026-05-18)`.
- `RhythmGameSectionController` 의 곡/난이도 UI 의존성: `RefreshSongList` 에서 `song.SupportedInstrumentIds.Contains(_currentInstrument.InstrumentId)` 로 row 활성/비활성, `OnSongRowClicked` 에서 `song.Difficulties` 를 iterate 해 난이도 버튼 생성, `LoadChart` / `AutoShowBpmFromSong` 에서 `song.GetChartPath(diff)` 로 `Application.streamingAssetsPath` 결합 파일 로드, `OnPlayButtonClicked` 에서 `_loadedChart.channelMap.entries` 로 judgedChannel/accompaniment 결정 — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`. 본 plan은 위 4 호출면을 그대로 만족시키도록 새 데이터 모델을 설계한다. **side effect:** `song.Difficulties` 가 곡별로 다른 집합을 반환하면 `OnSongRowClicked` 의 난이도 버튼 카운트도 곡별로 바뀌고 (06 What 만족), `GetChartPath(diff)` 는 그 곡 + 현재 선택된 instrument 의 파일을 반환해야 한다(controller 가 instrument 키를 별도 인자로 넘기지 않으므로). 따라서 **`SongEntryImpl` 은 현재 선택된 instrument 컨텍스트를 외부에서 알 수 없다 — 본 plan은 controller 가 `_currentInstrument.InstrumentId` 를 entry 에 명시적으로 전달할 수 있도록 `ISongEntry` 인터페이스에 신규 메서드를 추가(아래 항목 참조)하거나, `GetChartPath(difficulty)` 를 "현재 매핑된 instrument 한 개에 대한 파일 경로"로 정의한다.** 본 plan은 후속 07이 instrument 토글까지 요구할 것을 감안해 **`GetChartPath(instrumentId, difficulty)` overload 신규 추가**로 결정. controller 호출은 본 plan에서 `_currentInstrument.InstrumentId` 를 함께 넘기도록 같이 수정.
- `Assets/StreamingAssets/Songs/test.vmsong` 존재 (한 파일에 `[Track:1]` piano + `[Track:10]` DrumKit). sub-spec 06 Out of Scope = "옛 단일 파일 다중-트랙 포맷". 본 plan은 이를 신 포맷 2파일(`test_song-piano-easy.vmsong`, `test_song-drumkit-easy.vmsong`)로 분할 이동하고 `test.vmsong` 제거 — `Read Assets/StreamingAssets/Songs/test.vmsong (2026-05-18)`.
- asmdef: `Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef` references = `Instruments, RhythmGame.Data, RhythmGame.Runtime, Unity.InputSystem, Unity.XR.Interaction.Toolkit, Unity.TextMeshPro`. 본 plan 신규 import 0건 (System.IO / System.Linq / RhythmGame.Data / UnityEngine 모두 기존 reference 로 해결). 추가 신규 reference 없음 — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-18)`.
- 기존 EditMode 테스트 `SessionPanel.Tests.FolderScanSongCatalogTests` 2건은 (a) 두 단일-트랙 파일이 각자 piano/DrumKit 으로 등록, (b) parse 실패 파일 skip 을 검증. **본 plan은 두 테스트 모두 신 파일명 규약으로 교체하고 새 그루핑 테스트를 추가** (기존 테스트의 검증 의도는 유지 — instrument 키 흡수 / parse 실패 skip — 하지만 파일명 자체가 신 규약으로 바뀌므로 어차피 갱신 필요) — `Read Assets/SessionPanel/Tests/FolderScanSongCatalogTests.cs (2026-05-18)`.

## Approach

1. **`ISongEntry` 인터페이스 확장** — `Assets/SessionPanel/Scripts/ISongCatalog.cs`:
   - 기존 멤버 유지 (`SongId`, `Title`, `Artist`, `Difficulties`, `SupportedInstrumentIds`, `GetChartPath(difficulty)`).
   - **신규 메서드:** `IReadOnlyCollection<string> GetDifficultiesFor(string instrumentId)` — 그 곡 + 그 instrument 에 존재하는 difficulty 집합(`OrdinalIgnoreCase`). 빈 집합이면 그 instrument 는 이 곡에서 지원되지 않음.
   - **신규 메서드:** `string GetChartPath(string instrumentId, string difficulty)` — (instrument, difficulty) → `Songs/...vmsong` 상대 경로. 매칭 없으면 `null`.
   - 기존 `GetChartPath(string difficulty)` 는 backward-compat 으로 남기되, 내부적으로 "이 entry 의 첫 instrument" 에 대한 호출로 위임 (controller 가 점진 마이그레이션 가능하게).
   - `Difficulties` 는 "이 곡에서 어떤 instrument 든 한 번이라도 등장한 difficulty 의 합집합" 으로 의미를 재정의. 곡별로 가변 집합.

2. **`FolderScanSongCatalog.BuildEntriesFromFolder` 파일명 파서 도입** — `Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs`:
   - 파일별 처리 루프 안에서 `Path.GetFileNameWithoutExtension(path)` 결과를 마지막 두 하이픈으로 분리:
     - `LastIndexOf('-')` 로 `difficulty` 분리.
     - 남은 문자열에 다시 `LastIndexOf('-')` 로 `instrument` 분리.
     - 더 앞부분 전체 = `songname` (하이픈 포함 가능 — 예: `cool-song`).
     - 하이픈 2개 미만이면 명명 규약 위반 → skip(`continue`).
   - `<songname>` 키를 `OrdinalIgnoreCase` 로 정규화한 dictionary `Dictionary<string, GroupedSongBuilder>` 에 누적:
     - songId/title: 첫 만난 파일의 chart meta(`chart.songId`, `chart.title`)로 채움 (sub-spec 05 시멘틱 유지). 빈 값이면 파일명 기반 fallback.
     - 그 (instrument, difficulty) 키 → 상대 경로 매핑 한 건 추가 (`OrdinalIgnoreCase` 비교, 같은 키 중복이면 첫 항목 유지하고 console warning 없이 skip — CLAUDE.md "사용자가 진단 로직 요청하지 않음" 규칙 준수).
   - 파일 순회 끝나면 builder 들을 `ISongEntry` 구현체(`MultiFileSongEntry`)로 변환 후 반환. 순서는 `<songname>` 의 `OrdinalIgnoreCase` 정렬.

3. **`MultiFileSongEntry` 구현체 신설** — 같은 파일 안 sealed inner class:
   - 필드: `_id, _title, _artist, _instrumentIds : HashSet<string>(OrdinalIgnoreCase)`, `_filesByInstrumentDifficulty : Dictionary<(string instrument, string difficulty), string path>(OrdinalIgnoreCase tuple comparer)`, `_difficultiesByInstrument : Dictionary<string, List<string>>(OrdinalIgnoreCase)`.
   - `SupportedInstrumentIds` → `_instrumentIds` 그대로.
   - `Difficulties` → 모든 instrument 의 difficulty 합집합을 한 번 계산해 cache (정렬 순서: 우선순위 리스트 `["Easy", "Normal", "Hard"]` 에 따라 매핑되는 항목 먼저, 그 외 alphabetical — UI 표시 안정성 위해).
   - `GetDifficultiesFor(instrumentId)` → `_difficultiesByInstrument` lookup (없으면 빈 배열).
   - `GetChartPath(instrumentId, difficulty)` → `_filesByInstrumentDifficulty` lookup (없으면 null).
   - 기존 `GetChartPath(difficulty)` → 첫 instrument 의 매칭 파일을 반환(없으면 null).

4. **`RhythmGameSectionController` 호출면 조정** — `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs`:
   - `OnSongRowClicked(song)` 안 난이도 버튼 빌드 루프를 `foreach (var diff in song.Difficulties)` 대신 `foreach (var diff in song.GetDifficultiesFor(_currentInstrument.InstrumentId))` 로 교체. 이로써 sub-spec 06 What "그 instrument 의 piano-easy 파일만 있고 piano-hard 없으면 easy 만 노출" 충족.
   - `AutoShowBpmFromSong` / `LoadChart` 의 `song.GetChartPath(diff)` 호출을 `song.GetChartPath(_currentInstrument.InstrumentId, diff)` 로 교체. `RefreshSongList` 의 `Contains(_currentInstrument.InstrumentId)` 는 그대로 (`SupportedInstrumentIds` 의미 동일 유지).

5. **옛 `test.vmsong` 마이그레이션 — 자동 분할** — `Assets/StreamingAssets/Songs/`:
   - 기존 `test.vmsong` 의 `[Track:1]` 본문 + `[Meta]`/`[Resolution]`/`[Tempo]`/`[Channels]`(piano only)을 묶어 `test_song-piano-easy.vmsong` 으로 새 파일 작성. `[Channels]` 는 `channel=1 instrument=piano` 만.
   - 같은 방식으로 `[Track:10]` 본문 + meta(drumkit only) 를 `test_song-drumkit-easy.vmsong` 으로 작성. `[Channels]` 는 `channel=10 instrument=DrumKit` 만.
   - 기존 `test.vmsong` 삭제. `test.vmsong.meta` 같은 직렬화 부산물도 함께 삭제 (Unity 가 import 재생성 — Unity asset 직접 편집 아니므로 manage_asset MCP 경유 불필요. `Assets/StreamingAssets/Songs/*.vmsong` 는 텍스트 자산이며 sub-spec 05/06 모두 텍스트 직접 편집 전제).

6. **EditMode 테스트 갱신** — `Assets/SessionPanel/Tests/FolderScanSongCatalogTests.cs`:
   - 기존 두 테스트의 파일명을 신 규약으로 교체. `alpha-piano-easy.vmsong`, `beta-drumkit-easy.vmsong` 등. 기존 검증(`SupportedInstrumentIds.Contains`, `GetChartPath(diff)` 매핑, parse 실패 skip)은 모두 유지.
   - **신규 테스트 1:** `SameSongname_MultipleFiles_AreGroupedIntoOneEntry` — `cool-song-piano-easy.vmsong`, `cool-song-piano-hard.vmsong`, `cool-song-drum-easy.vmsong` 3파일 → entries.Count == 1, `SupportedInstrumentIds = {piano, drum}`, `GetDifficultiesFor("piano") = {Easy, Hard}`, `GetDifficultiesFor("drum") = {Easy}`, `GetChartPath("piano", "Hard") = "Songs/cool-song-piano-hard.vmsong"`.
   - **신규 테스트 2:** `HyphenInSongname_IsPreservedAsSongname` — `my-cool-song-piano-easy.vmsong` 한 파일 → entries[0].SongId 또는 그루핑 키가 `my-cool-song` 으로 잡히고 instrument=piano/difficulty=Easy 로 분리.
   - **신규 테스트 3:** `MalformedFileName_LessThanTwoHyphens_IsSkipped` — `no_hyphens.vmsong`, `only-one.vmsong` 은 entries 에 등재되지 않음.

7. **컴파일·테스트 검증** — `unity-test-runner` 1회 호출로 EditMode 회귀 + 컴파일 0 error 확인.

## Deliverables

- `Assets/SessionPanel/Scripts/ISongCatalog.cs` — `ISongEntry` 에 `GetDifficultiesFor(instrumentId)` + `GetChartPath(instrumentId, difficulty)` 추가.
- `Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs` — 파일명 파서 도입, `MultiFileSongEntry` 신설, `[Channels]` 기반 instrument 추출 제거(파일명에서 직접 추출).
- `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` — `GetDifficultiesFor` / `GetChartPath(instrumentId, difficulty)` 호출로 교체.
- `Assets/StreamingAssets/Songs/test_song-piano-easy.vmsong` — 신 포맷 분할 결과(피아노 트랙).
- `Assets/StreamingAssets/Songs/test_song-drumkit-easy.vmsong` — 신 포맷 분할 결과(드럼킷 트랙).
- `Assets/StreamingAssets/Songs/test.vmsong` — 삭제 (관련 `.meta` 함께 삭제).
- `Assets/SessionPanel/Tests/FolderScanSongCatalogTests.cs` — 기존 2건 갱신 + 신규 3건 추가.

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/SessionPanel/Scripts/ISongCatalog.cs` 의 `ISongEntry` 에 `GetDifficultiesFor(string instrumentId)` 와 `GetChartPath(string instrumentId, string difficulty)` 시그니처가 존재한다.
  **검증:** `Grep "GetDifficultiesFor\(string instrumentId\)" Assets/SessionPanel/Scripts/ISongCatalog.cs` 1건 매치 + `Grep "GetChartPath\(string instrumentId, string difficulty\)" Assets/SessionPanel/Scripts/ISongCatalog.cs` 1건 매치.
- [ ] `[auto-hard]` `FolderScanSongCatalog.BuildEntriesFromFolder` 가 파일명 마지막 두 하이픈을 `instrument`/`difficulty` 로 파싱하고, 동일 `<songname>` 파일들을 1개 entry 로 그루핑한다.
  **검증:** EditMode 테스트 `SameSongname_MultipleFiles_AreGroupedIntoOneEntry` 통과 — `cool-song-piano-easy.vmsong` + `cool-song-piano-hard.vmsong` + `cool-song-drum-easy.vmsong` 3파일 → `entries.Count == 1`, `entry.SupportedInstrumentIds = {piano, drum}` (OrdinalIgnoreCase), `entry.GetDifficultiesFor("piano")` 의 정규화 집합 == `{Easy, Hard}`.
- [ ] `[auto-hard]` 곡명에 하이픈이 포함된 파일도 마지막 두 하이픈만 분리해 `songname` 을 올바르게 추출한다.
  **검증:** EditMode 테스트 `HyphenInSongname_IsPreservedAsSongname` — `my-cool-song-piano-easy.vmsong` → entry 1건, instrument=`piano`, difficulty=`Easy`, `GetChartPath("piano", "Easy")` 가 `Songs/my-cool-song-piano-easy.vmsong` 을 반환.
- [ ] `[auto-hard]` 하이픈 2개 미만의 잘못된 파일명은 entry 에 등재되지 않는다.
  **검증:** EditMode 테스트 `MalformedFileName_LessThanTwoHyphens_IsSkipped` — `no_hyphens.vmsong` + `only-one.vmsong` 2파일만 있는 폴더 스캔 결과 `entries.Count == 0`.
- [ ] `[auto-hard]` 같은 곡에 piano-easy 만 있고 piano-hard 가 없으면 `GetDifficultiesFor("piano")` 는 `{Easy}` 만 반환한다.
  **검증:** 위 그루핑 테스트의 sub-assert — `entry.GetDifficultiesFor("drum")` 의 정규화 집합 == `{Easy}` 이고 `Hard` 미포함.
- [ ] `[auto-hard]` `GetChartPath(instrumentId, difficulty)` 가 그 (instrument, difficulty) 에 매칭되는 파일이 없을 때 `null` 을 반환한다.
  **검증:** 그루핑 테스트의 sub-assert — `entry.GetChartPath("drum", "Hard") == null`.
- [ ] `[auto-hard]` 기존 EditMode 테스트 2건(`TwoSongs_PianoAndDrumKit_AreRegisteredWithCorrectInstruments`, `ParseFailFile_IsSkippedWithoutException`)이 신 파일명 규약으로 갱신된 후 통과한다.
  **검증:** `unity-test-runner` EditMode 실행 결과 `SessionPanel.Tests.FolderScanSongCatalogTests` 가 5건(기존 2 + 신규 3) 모두 PASS.
- [ ] `[auto-hard]` `RhythmGameSectionController` 가 `song.GetDifficultiesFor(_currentInstrument.InstrumentId)` 와 `song.GetChartPath(_currentInstrument.InstrumentId, _selectedDifficulty)` 를 호출하도록 교체된다.
  **검증:** `Grep "GetDifficultiesFor\(_currentInstrument\.InstrumentId\)" Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` 1건 이상 + `Grep "GetChartPath\(_currentInstrument\.InstrumentId" Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` 1건 이상.
- [ ] `[auto-hard]` `Assets/StreamingAssets/Songs/test.vmsong` 가 삭제되고 `test_song-piano-easy.vmsong` 및 `test_song-drumkit-easy.vmsong` 가 신 포맷으로 존재한다.
  **검증:** `Glob Assets/StreamingAssets/Songs/*.vmsong` 결과에 `test.vmsong` 미포함, `test_song-piano-easy.vmsong` + `test_song-drumkit-easy.vmsong` 포함. 각 파일 첫 줄 `[Meta]` 헤더로 시작.
- [ ] `[auto-hard]` 본 plan 변경 후 Unity 컴파일 에러 0건.
  **검증:** `unity-test-runner` 또는 `mcp__UnityMCP__read_console` 로 변경 직후 console error count == 0 (warning 무관).
- [ ] `[manual-hard]` `SampleScene` 또는 `TestSceneSanyo` 진입 후 SessionPanel 의 Rhythm Game 섹션에서 신 포맷 `test_song` 항목 1개가 표시되고, piano 악기 잡으면 `easy` 난이도만, drumkit 악기 잡으면 `easy` 난이도만 노출되며 Play 버튼 누르면 기존과 동일하게 세션이 시작된다.
  **검증:** 헤드셋 또는 Editor Play 모드에서 SessionPanel → Rhythm Game → `test_song` 선택 → 난이도 1개 자동 선택 → Play → BPM 바·노트 시각화 정상 동작 시뮬레이션.

## Out of Scope

- 인스트루먼트 토글·템포 조절 — `07-session-options-instrument-toggle-tempo` plan.
- 핫리로드(파일 추가 감지 후 자동 카탈로그 갱신) — Out of Scope (sub-spec 06 자체 Out of Scope).
- `.vmsong` 내부 포맷(`[Meta]`, `[Channels]`, `[Track:*]`) 자체 변경 — 본 plan은 파일명만 다루고 본문 파싱은 기존 `VmSongParser` 위임.
- 본 plan은 sub-spec 06이 도입한 "그 instrument 에 매칭되는 파일이 없는 곡 = 비활성" 의 UI 표시 정책 자체는 변경하지 않음. `RhythmGameSectionController.RefreshSongList` 가 `SupportedInstrumentIds.Contains` 로 이미 처리하고 있으므로 그루핑 결과만 정합 유지하면 동일 시멘틱.
- BPM 바·결과 화면 등 다른 UI 영역.
- 씬 자산(`SampleScene.unity`, `TestSceneSanyo.unity`) 직접 편집 — `FolderScanSongCatalog` 컴포넌트 인스턴스는 그대로 유지.

## Notes

- `GetChartPath(string difficulty)` 단일 인자 overload 는 후속 plan에서 모든 호출자가 instrumentId 인자를 넘기도록 완전 마이그레이션된 후 제거 검토. 본 plan에서는 backward-compat 으로 남기되, 내부 구현이 "첫 instrument" 를 임의 선택하므로 신규 호출자는 새 overload 사용을 권장(인터페이스 doc-comment 로 명시 권장).
- difficulty 키 정규화: 파일명 토큰 `easy` → entry 노출 시 `"Easy"` 로 PascalCase 정규화할지, 원본 그대로 둘지는 기존 UI 가 case-insensitive 비교를 안 하므로 영향 있다. `Easy/Normal/Hard` 와 무관한 키(`expert` 등)는 그대로 전달. `OnDifficultyClicked` 가 받은 문자열을 그대로 `GetChartPath` 에 다시 넘기므로 round-trip 만 일관되면 됨 — 본 plan은 **파일명 토큰을 원본 그대로 보존**(소문자 그대로), Difficulties 합집합도 원본 케이스 유지.
- 2026-05-18 자동 AC 결과: 10/10 PASS.
  - AC1 (ISongEntry 시그니처) PASS — Grep 매치 확인.
  - AC2~AC6 (파일명 파싱·그루핑·하이픈 보존·malformed skip·null 반환) PASS — EditMode SameSongname_MultipleFiles_AreGroupedIntoOneEntry / HyphenInSongname_IsPreservedAsSongname / MalformedFileName_LessThanTwoHyphens_IsSkipped 전부 PASS.
  - AC7 (기존 EditMode 2건 갱신 후 5건 PASS) PASS — FolderScanSongCatalogTests 5/5 PASS.
  - AC8 (Controller 호출면 교체) PASS — Grep 매치 확인.
  - AC9 (test.vmsong 마이그레이션) PASS — Glob 결과 test_song-piano-easy.vmsong + test_song-drumkit-easy.vmsong 존재, test.vmsong 부재.
  - AC10 (컴파일 0건) PASS — read_console types=error 결과 CS 0건. SessionPanel.Tests 전체 15/15 PASS (회귀 없음).
- 2026-05-18 manual-hard 검증 (사용자 직접 Play 모드): test_song 단일 항목 표시 + Piano/DrumKit 잡으면 각각 easy 단일 난이도 노출 + Play 정상 시작 PASS.
- 부수 변경 (interface 추가로 인한 필수 companion 갱신): StubSongCatalog.SongEntryImpl + RhythmGameSectionRefreshTests.StubSongEntry 도 GetDifficultiesFor/GetChartPath(instrumentId, difficulty) stub 추가.

## Handoff

ISongEntry 인터페이스에 GetDifficultiesFor(string instrumentId) : IReadOnlyCollection<string> 및 GetChartPath(string instrumentId, string difficulty) : string 2 메서드 신규. 기존 GetChartPath(string difficulty) overload 는 "첫 instrument" 로 위임하는 backward-compat 형태로 유지.

FolderScanSongCatalog.BuildEntriesFromFolder 가 파일명 마지막 두 하이픈을 (instrument, difficulty) 로 파싱하고, 동일 <songname> 파일들을 1개 MultiFileSongEntry 로 그루핑. 그룹핑 키는 OrdinalIgnoreCase. (instrument, difficulty) → 상대 경로 매핑은 Dictionary 로 보관.

Assets/StreamingAssets/Songs/test.vmsong (구 단일 파일 멀티트랙) 삭제 + test_song-piano-easy.vmsong + test_song-drumkit-easy.vmsong 분할 파일 신규 작성. [Tempo] 100 BPM 양쪽 유지.

RhythmGameSectionController.OnSongRowClicked 의 난이도 버튼 빌드는 GetDifficultiesFor(_currentInstrument.InstrumentId), LoadChart/AutoShowBpmFromSong 의 차트 경로 조회는 GetChartPath(_currentInstrument.InstrumentId, diff) 로 교체.

EditMode 테스트 FolderScanSongCatalogTests 5건: TwoSongs_PianoAndDrumKit_AreRegisteredWithCorrectInstruments / ParseFailFile_IsSkippedWithoutException (기존 2건 갱신) + SameSongname_MultipleFiles_AreGroupedIntoOneEntry / HyphenInSongname_IsPreservedAsSongname / MalformedFileName_LessThanTwoHyphens_IsSkipped (신규 3건). 전부 PASS.

sub-spec 07(instrument 토글·템포) 은 GetDifficultiesFor/GetChartPath(instrumentId, difficulty) 인터페이스 면을 그대로 활용해 (a) instrument 토글 버튼이 그 곡의 instrument 집합을 SupportedInstrumentIds 에서 조회, (b) 선택된 instrument 의 난이도 파일을 GetChartPath 로 로드해 자동 반주에 사용 가능.
