# Folder-Scan Song Catalog

**Linked Spec:** [`05-song-catalog.md`](../specs/05-song-catalog.md)
**Status:** `Done`

## Goal

`StreamingAssets/Songs/` 폴더의 `.vmsong` 파일을 자동 스캔해 곡 목록을 구성하는 `FolderScanSongCatalog`(`ISongCatalog` 구현체)를 추가하고, `SampleScene.unity`의 `SessionPanelController._songCatalogObject` 와이어링을 `StubSongCatalog` → `FolderScanSongCatalog` 인스턴스로 교체한다. 기존 `StubSongCatalog` / `RhythmSongDatabase` 코드·자산은 보존한다.

## Context

현재 `Assets/SessionPanel/Scripts/StubSongCatalog.cs`는 `RhythmSongDatabase[]` ScriptableObject 배열에서 곡 목록을 빌드하고, `GetChartPath(...)`은 `"Songs/test.vmsong"` 1개로 하드코딩돼 있다 (StubSongCatalog.cs L62). 따라서 새 곡을 추가하려면:

1. `.vmsong` 파일을 추가하고,
2. 해당 곡을 가리키는 `RhythmSong` ScriptableObject를 만들고,
3. 곡을 묶을 `RhythmSongDatabase` ScriptableObject 자산(악기 키별)을 만들거나 갱신해야

한다. sub-spec `05-song-catalog`의 What은 이 3-스텝을 단축해 "**`Songs/` 폴더에 `.vmsong` 파일을 떨어뜨리는 것만으로 곡이 등록된다**"로 만드는 것이다.

데이터 구조는 이미 충분하다. `VmSongParser.Parse(string)`은 `[Meta]`의 `songid`/`title`/`artist`와 `[Channels]`의 `(channel, instrumentKey)` 엔트리를 `VmSongChart`에 채워 반환하므로, 폴더 스캔 → 파일 텍스트 Read → Parse → `ISongEntry` 매핑 흐름만 새로 구현하면 된다. 차트 경로는 `Application.streamingAssetsPath`로부터의 절대 경로 또는 `"Songs/<file>.vmsong"` 상대 경로 중 기존 다운스트림(`StartGameRequested` → chart loader)이 기대하는 포맷을 유지한다 — 현재 `StubSongCatalog.GetChartPath`는 `"Songs/test.vmsong"` 상대 경로를 반환하므로 동일 규약을 따른다.

sub-spec Out of Scope에 따라 `RhythmSongDatabase` ScriptableObject 제거·핫리로드·난이도별 별도 파일은 본 plan에서 다루지 않는다. 또한 이전 두 sub-spec(02·04)은 spec-driven 코드 변경 없이 manual-hard만 통과했으므로 별도 handoff 시드 없음.

## Verified Structural Assumptions

- `SampleScene.unity`의 `StubSongCatalog` GameObject(name=`StubSongCatalog`)는 GameObject fileID `181637958`, MonoBehaviour fileID `181637959`(script guid `d5467f8327b26a84a87b3c2992539de7`)로 직렬화돼 있고, `SessionPanelController._songCatalogObject`(fileID `2648` 영역)가 이 MonoBehaviour fileID `181637959`를 참조한다 — `Read Assets/Scenes/SampleScene.unity (2026-05-17)`
- `SessionPanelController._songCatalogObject` 필드는 `UnityEngine.Object` 타입으로 직렬화되며(SessionPanelController.cs L21), `RhythmGameSectionController.Inject(_activeInstrumentProviderObject, _songCatalogObject)`로 전달돼 내부에서 `ISongCatalog`로 캐스트되는 구조다 (SessionPanelController.cs L183) — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-17)`
- `Assets/StreamingAssets/Songs/test.vmsong`의 실제 헤더: `[Meta] title=Test (Mary Had a Little Lamb)` / `songid=test_001` (artist 키 없음 — 파서는 미존재 시 `chart.artist`를 빈 상태로 남김), `[Channels] channel=1 instrument=piano` / `channel=10 instrument=DrumKit` — `Read Assets/StreamingAssets/Songs/test.vmsong (2026-05-17)`
- `VmSongParser.Parse(string text)` public API는 `ParseResult { VmSongChart chart, List<ParseError> errors, bool Success }`를 반환. `chart.channelMap.entries`는 `List<ChannelInstrumentMap.Entry { int channel; string instrumentKey; }>`로 `[Channels]` 라인 1개당 엔트리 1개를 누적한다. 필수 섹션 누락 시 `errors`에 누락 사유를 채우고 `Success=false`가 된다 (`[Meta]`/`[Resolution]`/`[Tempo]`/`[Channels]`/`[Track:N]` 1개 이상 필수) — `Read Assets/RhythmGame/Scripts/Data/VmSongParser.cs (2026-05-17)` + `Read Assets/RhythmGame/Scripts/Data/VmSongChart.cs (2026-05-17)` + `Read Assets/RhythmGame/Scripts/Data/ChannelInstrumentMap.cs (2026-05-17)`
- `ISongCatalog` / `ISongEntry` 인터페이스 시그니처: `ISongCatalog { IReadOnlyList<ISongEntry> Songs; event Action Changed; }` / `ISongEntry { string SongId; string Title; string Artist; IReadOnlyList<string> Difficulties; IReadOnlyCollection<string> SupportedInstrumentIds; string GetChartPath(string difficulty); }` — 기존 `StubSongCatalog.SongEntryImpl`은 `Difficulties = ["Easy","Normal","Hard"]`, `GetChartPath(...) = "Songs/test.vmsong"` 상대 경로를 반환했음 — `Read Assets/SessionPanel/Scripts/ISongCatalog.cs (2026-05-17)` + `Read Assets/SessionPanel/Scripts/StubSongCatalog.cs (2026-05-17)`
- `Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef`는 이미 `RhythmGame.Data`를 references에 포함하므로 `VmSongParser`/`VmSongChart` import에 asmdef 추가 작업 없음. 신규 `FolderScanSongCatalog.cs`는 같은 폴더(`Assets/SessionPanel/Scripts/`)에 둠 — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-17)`
- 신규 EditMode 테스트는 `Assets/SessionPanel/Tests/`에 두며, 이 폴더의 `SessionPanel.Tests.asmdef`는 이미 `SessionPanel.Runtime` references를 포함해 추가 작업 없음. `defineConstraints: ["UNITY_INCLUDE_TESTS"]` 유지 — `Read Assets/SessionPanel/Tests/SessionPanel.Tests.asmdef (2026-05-17)`

## Approach

1. **`FolderScanSongCatalog` MonoBehaviour 신규 작성** — `Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs`. `ISongCatalog` 구현, `[AddComponentMenu("SessionPanel/Folder Scan Song Catalog")]` 부착. `Awake()`에서 `BuildCatalog()` 호출. 핵심 흐름:
   - 스캔 루트: `Application.streamingAssetsPath + "/Songs"` (`[SerializeField] string songsRelativePath = "Songs"`로 기본값만 노출, 정적 폴더만 지원).
   - `Directory.EnumerateFiles(root, "*.vmsong", SearchOption.TopDirectoryOnly)`로 파일 목록을 결정적 순서로 정렬(`OrderBy(filename, InvariantCultureIgnoreCase)`).
   - 각 파일을 `File.ReadAllText(path)` → `VmSongParser.Parse(text)` 호출. `result.Success == false`이면 해당 파일 skip(콘솔 진단 로그 추가 없음 — CLAUDE.md "사용자가 명시적으로 요청하지 않았다면 진단 로직 추가 금지" 준수).
   - `VmSongChart.songId`가 빈 문자열이면 파일명 `Path.GetFileNameWithoutExtension(path)`를 fallback ID로 사용.
   - 각 곡의 `chart.channelMap.entries`에서 `instrumentKey`들을 `HashSet<string>`으로 모아 `SupportedInstrumentIds`로 노출.
   - `Difficulties`는 기존 stub과 동일하게 `["Easy","Normal","Hard"]` 단일 진실원 상수(spec Out of Scope에 "난이도별 별도 파일 미지원"이 박혀 있어 단일 차트가 3난이도 모두를 대표).
   - `GetChartPath(difficulty)`는 `"Songs/<file>.vmsong"` 상대 경로 반환(기존 stub 규약 유지). 절대 경로 변환은 다운스트림 chart loader 책임.
2. **테스트 가능성 분리** — 폴더 스캔/파싱 로직을 `static` helper(`internal static List<SongEntryImpl> BuildEntriesFromFolder(string absoluteFolderPath, string relativePrefix)`)로 떼어내 EditMode 테스트에서 임시 폴더를 주입해 호출. MonoBehaviour `Awake`는 이 helper에 `Application.streamingAssetsPath + "/Songs"`와 `"Songs"`를 넘긴다.
3. **EditMode 테스트 추가** — `Assets/SessionPanel/Tests/FolderScanSongCatalogTests.cs` (`[TestFixture]`). `Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString())`에 임시 폴더를 만들고 `.vmsong` 2개(예: `alpha.vmsong`에 `instrument=piano`, `beta.vmsong`에 `instrument=DrumKit`)를 `File.WriteAllText`로 떨어뜨린 뒤 `BuildEntriesFromFolder`를 호출. AC: (a) 2곡 등록, (b) songid 매칭, (c) `SupportedInstrumentIds`가 각각 `{piano}`/`{DrumKit}`를 포함, (d) `GetChartPath("Easy")`가 `"Songs/alpha.vmsong"` 형식. `[TearDown]`에서 폴더 삭제.
4. **씬 와이어링 교체 (Unity MCP)** — Unity MCP 절차는 [`unity-mcp-workflow`](../../../../.claude/skills/unity-mcp-workflow/SKILL.md)에 단일 진실원. 사실만 박제:
   - 대상 씬: `Assets/Scenes/SampleScene.unity`.
   - 단계: (a) MCP `manage_gameobject`로 기존 `StubSongCatalog` GameObject(fileID `181637958`)에 `FolderScanSongCatalog` 컴포넌트를 **추가**(GameObject 자체와 기존 `StubSongCatalog` 컴포넌트는 보존 — Out of Scope 준수). (b) `SessionPanelController._songCatalogObject` 참조를 같은 GameObject 위의 `FolderScanSongCatalog` 컴포넌트 fileID로 재와이어. (c) 씬 저장. (d) `read_console`로 Awake 시 NullReferenceException 0건 확인.
   - 부착 위치 결정 근거: 새 GameObject를 만들면 같은 씬에 2개 catalog GameObject가 공존해 검수가 헷갈리고, 반대로 기존 GameObject를 통째로 제거하면 `StubSongCatalog`가 함께 사라져 Out of Scope("기존 `RhythmSongDatabase` ScriptableObject 제거 미수행")의 정신과 어긋난다. 같은 GameObject에 컴포넌트만 추가하면 양쪽 동시 보존이 가능하고, `SessionPanelController._songCatalogObject`는 fileID로 단일 컴포넌트만 가리키므로 모호성 없음.
5. **regression check** — `unity-test-runner` 1회 호출(CLAUDE.md 테스트 정책). EditMode 신규 테스트 + 기존 SessionPanel/RhythmGame 테스트 회귀 확인.

## Deliverables

- `Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs` — 신규. `ISongCatalog` 구현체.
- `Assets/SessionPanel/Tests/FolderScanSongCatalogTests.cs` — 신규. EditMode 단위 테스트.
- `Assets/Scenes/SampleScene.unity` — 수정. `StubSongCatalog` GameObject에 `FolderScanSongCatalog` 컴포넌트 추가 + `SessionPanelController._songCatalogObject` 재와이어.

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs`가 존재하며 `class FolderScanSongCatalog : MonoBehaviour, ISongCatalog` 시그니처를 가진다.
  **검증:** `Grep "class FolderScanSongCatalog : MonoBehaviour, ISongCatalog" Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs`로 1건 일치.
- [ ] `[auto-hard]` `.cs` 추가·수정 후 Unity 에디터 컴파일이 에러 0건으로 통과한다.
  **검증:** Unity MCP `read_console` 호출에 `editor_state.isCompiling=false` 확인 후 `types=["error"]`로 필터해 신규/기존 컴파일 에러 0건.
- [ ] `[auto-hard]` EditMode 테스트 `FolderScanSongCatalogTests`에서 임시 폴더에 `.vmsong` 2개(piano·DrumKit)를 떨어뜨리면 `BuildEntriesFromFolder` 결과가 곡 2개를 반환하고 각 곡의 `SongId`/`SupportedInstrumentIds`가 파일의 `[Meta]`/`[Channels]`와 일치한다.
  **검증:** `unity-test-runner` EditMode 실행에서 `FolderScanSongCatalogTests` 전 케이스 PASS. assert: 2곡, `entries[0].SupportedInstrumentIds.Contains("piano")`, `entries[1].SupportedInstrumentIds.Contains("DrumKit")`.
- [ ] `[auto-hard]` `SampleScene.unity`의 `SessionPanelController._songCatalogObject`가 `FolderScanSongCatalog` 인스턴스 fileID를 가리키며 기존 `StubSongCatalog` 컴포넌트(fileID `181637959`)와 그 `sourceDatabases` 배열은 그대로 보존된다 (Out of Scope 준수).
  **검증:** `Grep "_songCatalogObject:" Assets/Scenes/SampleScene.unity`로 1줄 추출 → fileID가 신규 `FolderScanSongCatalog` MonoBehaviour fileID와 일치. 동시에 `Grep "EditorClassIdentifier: Assembly-CSharp::SessionPanel.StubSongCatalog" Assets/Scenes/SampleScene.unity`로 stub 컴포넌트 보존 확인(1건 존재).
- [ ] `[auto-soft]` 새 `FolderScanSongCatalog`는 파싱 실패 파일(`VmSongParser.Parse(...).Success == false`)을 skip하고 카탈로그 빌드를 계속한다(예외 throw 금지).
  **검증:** EditMode 테스트 1 케이스 추가 — 임시 폴더에 정상 1개 + 비어 있는 `.vmsong` 1개를 떨어뜨리면 결과가 곡 1개만 반환되고 throw 없음.
- [ ] `[manual-hard]` `SampleScene.unity` Play → 헤드셋(또는 에디터 PlayMode)에서 SessionPanel을 열어 곡 목록에 `Test (Mary Had a Little Lamb)` 1곡이 표시되고, piano를 잡았을 때 활성, DrumKit을 잡았을 때도 활성으로 표시된다.
  **검증:** 에디터 PlayMode에서 piano 잡고 SessionPanel 열기 → 곡 활성. 종료 후 DrumKit 잡고 다시 열기 → 같은 곡 활성. 양쪽 모두 콘솔 에러 0건.

## Out of Scope

- 기존 `RhythmSongDatabase` ScriptableObject 자산 및 `StubSongCatalog` 컴포넌트 제거 — sub-spec Out of Scope.
- 런타임 핫리로드(파일 추가 감지) — sub-spec Out of Scope. 폴더 스캔은 `Awake` 1회만.
- 난이도별 별도 `.vmsong` 파일 지원 — sub-spec Out of Scope. `Difficulties = ["Easy","Normal","Hard"]` 상수가 같은 차트를 가리킨다.
- `.vmsong` 파일 형식(파서 자체) 변경 — sub-spec Out of Scope.
- 차트 경로 절대 경로화·다운스트림 chart loader 변경 — 기존 `"Songs/<file>.vmsong"` 상대 경로 규약 유지.

## Notes

- 부착 위치 결정(self-check 명시): 신규 `FolderScanSongCatalog` 컴포넌트는 **기존 `StubSongCatalog` GameObject에 추가**한다. 새 GameObject 생성을 택하지 않은 이유는 Approach 4단계의 결정 근거 참고. 같은 GameObject에 양 컴포넌트가 공존해도 `SessionPanelController._songCatalogObject`는 fileID로 1개만 가리키므로 동작 모호성 없음.
- 후속 plan 후보: 핫리로드(파일 시스템 watcher), 난이도별 multi-file 지원, `RhythmSongDatabase` 자산 deprecate.
- 2026-05-17 자동 검증 결과:
  - AC1 (class signature) PASS — `Grep "class FolderScanSongCatalog : MonoBehaviour, ISongCatalog"` 1건.
  - AC2 (컴파일 0건) PASS — `read_console types=["error"] filter="error CS"` 0건.
  - AC3 (EditMode 2곡 등록) PASS — `unity-test-runner` 결과 `TwoSongs_PianoAndDrumKit_AreRegisteredWithCorrectInstruments` PASS.
  - AC4 (씬 와이어링) PASS — TestSceneSanyo·SampleScene 모두 `_songCatalogObject`가 FolderScanSongCatalog 인스턴스 지시, StubSongCatalog 컴포넌트와 sourceDatabases 보존 확인.
  - AC5 (파싱 실패 skip) PASS — `ParseFailFile_IsSkippedWithoutException` PASS.
- 2026-05-17 manual-hard 검증 (TestSceneSanyo, parksky0311@gmail.com 직접 Play 모드):
  - 1차 검증: 곡 표시되나 모든 row 비활성. 원인 진단 — test.vmsong [Channels] `instrument=piano`(소문자) 와 Piano prefab `instrumentId: Piano`(대문자) case mismatch 로 `SupportedInstrumentIds.Contains(InstrumentId)` 가 false 반환.
  - 수정: `FolderScanSongCatalog.BuildEntriesFromFolder()` 에서 instruments HashSet 을 `new HashSet<string>(StringComparer.OrdinalIgnoreCase)` 로 생성하도록 변경 (8라인 단위). EditMode 회귀 테스트 2/2 통과 유지.
  - 2차 검증: Piano/DrumKit 양쪽 모두 `Test (Mary Had a Little Lamb)` 활성 표시 PASS. 콘솔 에러 0건.
- 결과: sub-spec 05 의 What·Behavior 3 항목 모두 만족. 핫리로드/난이도별 별도 파일/RhythmSongDatabase 제거는 sub-spec Out of Scope 그대로 유지.

## Handoff

FolderScanSongCatalog MonoBehaviour 신규 추가 (`Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs`). `ISongCatalog` 구현체로 Awake 1회 `StreamingAssets/Songs/*.vmsong` 스캔·파싱. 정적 helper `BuildEntriesFromFolder(absoluteFolder, relativePrefix)` 로 테스트 주입 가능. instrument 키 HashSet 은 `StringComparer.OrdinalIgnoreCase` 로 생성해 chart `piano` ↔ prefab `Piano` 케이스 차이 흡수.

EditMode 회귀 테스트 `FolderScanSongCatalogTests` 2건 (`Assets/SessionPanel/Tests/FolderScanSongCatalogTests.cs`): TwoSongs_PianoAndDrumKit_AreRegisteredWithCorrectInstruments + ParseFailFile_IsSkippedWithoutException. 양쪽 PASS.

씬 와이어링: `SampleScene.unity`·`TestSceneSanyo.unity` 양쪽 StubSongCatalog GameObject 에 FolderScanSongCatalog 컴포넌트 추가, `SessionPanelController._songCatalogObject` 를 FolderScanSongCatalog 인스턴스로 재와이어. 기존 StubSongCatalog 컴포넌트와 `sourceDatabases` 배열 보존 (Out of Scope 준수). 양 씬 모두 사용자 manual-hard 통과.

후속 plan 후보 (현 시점 호출 없음): (1) 핫리로드, (2) 난이도별 multi-file 지원, (3) `RhythmSongDatabase` 자산 deprecate, (4) FolderScanSongCatalog 와 StubSongCatalog 의 단일 진실원 정리.
