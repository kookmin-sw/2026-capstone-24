# Session Panel Start Menu Section

**Linked Spec:** [`02-start-menu-section.md`](../specs/02-start-menu-section.md)
**Status:** `Ready`

## Goal

세션 패널의 시작 메뉴 섹션을 채운다. anchoring plan 산출물 안에 곡 / 난이도 / 반주 트랙 토글 / 시작 버튼 UI를 채우고, 활성 악기 신호로 곡 목록을 갱신·초기화한다. 시작 액션은 활성 악기 prefab의 자식 `RhythmGameHost.StartSession`을 반주 트랙 마스킹 인자와 함께 호출한다. 자동 반주 production 마스킹 적용은 후속 plan으로 위임.

## Context

session-panel `02-start-menu-section` sub-spec의 첫 plan이다. spec [`02-start-menu-section.md`](../specs/02-start-menu-section.md)이 정의한 핵심:

- 활성 악기가 잡혀 있을 때만 노출. 핀치 호출(악기 미잡음)에선 비활성.
- 곡 목록: 모든 곡 노출, 잡은 악기 트랙이 없는 곡은 비활성.
- 난이도: 그 곡 차트의 난이도 목록.
- 반주 트랙: 잡은 악기 트랙 제외 + 채널별 on/off, 기본 모두 on.
- 시작 액션 → 결정된 곡 / 난이도 / 반주 구성을 rhythm-game session-flow로 전달.
- 곡/난이도 선택 후 다른 악기로 바꾸거나 놓으면 초기화.

본 plan은 다음 4가지에 의존하며 plan-implementer가 새 세션에서 받았을 때 그 산출물이 이미 적용된 상태를 가정한다:

- **anchoring plan(`2026-05-05-linksky0311-session-panel-anchoring`) 선행 적용.** SessionPanel.prefab의 `StartMenuSectionContainer` placeholder + `IActiveInstrumentProvider` / `IActiveInstrument` 인터페이스. 패널·섹션 가시성·토글은 anchoring plan이 책임.
- **volume-section plan(`2026-05-05-linksky0311-session-panel-volume-section`) 선행 적용 권장(필수 아님).** InstrumentBase에 `instrumentId` 필드를 추가해 IActiveInstrument 구현체가 그 값을 그대로 흘려보낸다. 본 plan은 IActiveInstrument 인터페이스만 의존하므로 volume-section이 미선행이어도 dummy stub provider로 동작.
- **drum/piano 잡기 wiring**: 다른 작업자 plan에서 진행 중. 본 plan의 manual 검증은 anchoring/volume-section과 동일하게 dummy stub provider를 inspector로 임시 주입.
- **rhythm-game accompaniment plan**: 자동 반주 발화 마스킹 production 적용은 별도 plan(미작성)이 처리. 본 plan은 `RhythmGameHost.StartSession`에 마스킹 인자를 추가 + Host가 보관까지만.

본 plan이 의도적으로 미루는 책임:

- **곡 카탈로그 production**: spec Out of Scope. 본 plan은 `ISongCatalog` 인터페이스 정의 + 임시 stub만 둠. 난이도 데이터 모델, 곡-차트 link, 곡 추가/삭제 UX는 별도 후속 sub-spec.
- **자동 반주 발화 마스킹의 실제 적용**: rhythm-game `accompaniment` plan으로 위임. 본 plan은 결정 전달까지만.

## Verified Structural Assumptions

- `Assets/Instruments/Piano/Prefabs/Piano.prefab` 루트 직속 자식 `RhythmGameHost`(`Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs`)가 정확히 1개 부착, `songDatabase` 직렬화 reference는 `Assets/RhythmGame/Data/Piano_SongDatabase.asset`을 가리킨다. — `unity-scene-reader 보고 (2026-05-05)`
- `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` 루트 직속 자식 `RhythmGameHost`가 정확히 1개 부착, `songDatabase` 직렬화 reference는 `DrumKit_SongDatabase.asset`을 가리킨다. — `unity-scene-reader 보고 (2026-05-05)`
- `Piano_SongDatabase.asset` / `DrumKit_SongDatabase.asset`: `instrumentKey` 값이 각각 `"Piano"`, `"DrumKit"`. `songs[]` 길이 1, 둘 다 동일 `Assets/RhythmGame/Data/SampleSong.asset`을 reference. 본 plan의 stub catalog는 두 SongDatabase를 합쳐 곡 entry를 만들고 `SupportedInstrumentIds`에 instrumentKey를 누적한다. — `unity-scene-reader 보고 (2026-05-05)`
- `Assets/RhythmGame/Data/SampleChart_Beginner.asset`은 m_Script GUID가 가리키는 `RhythmChart` 클래스가 코드베이스에 부재한 **orphan asset**(missing script)이다. 본 plan의 stub catalog는 이 자산을 타입 reference로 사용하지 않고 `Assets/StreamingAssets/Songs/test.vmsong` 텍스트 파일을 `VmSongParser.Parse`로 로드하는 패턴(ChartAutoPlayer와 동일)을 따른다. — `Read Assets/RhythmGame/Data/SampleChart_Beginner.asset + grep RhythmChart class (2026-05-05)`
- `Assets/RhythmGame/Scripts/Data/Parsing/VmSongParser.cs`: `public static ParseResult Parse(string text)` API가 `VmSongChart`를 만들어 반환. 본 plan에서 `File.ReadAllText(Path.Combine(Application.streamingAssetsPath, song.GetChartPath(diff)))` → `VmSongParser.Parse` 패턴으로 차트 로드. — `Read Assets/RhythmGame/Scripts/Data/Parsing/VmSongParser.cs (2026-05-05)`
- `RhythmGameHost.StartSession(VmSongChart chart, RhythmSong song, int judgedChannel)` 현 시그니처. 코드베이스에 호출부 0건(`grep 'StartSession(' Assets`로 호출부 없음 확인) — 본 plan이 첫 호출부를 추가하므로 4번째 인자(`IReadOnlyDictionary<int, bool> accompanimentEnabled = null`) 추가 시 외부 호출부 영향 0. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs (2026-05-05)`
- `Assets/Scenes/SampleScene.unity` 루트 직속 ChartAutoPlayer 인스턴스 존재(`channelBindings`: ch1→Piano, ch10→DrumKit). 본 plan은 ChartAutoPlayer를 건드리지 않고, 자동 반주 마스킹 production 적용은 rhythm-game accompaniment plan에 위임. — `unity-scene-reader 보고 (2026-05-05)`
- SessionPanel.prefab은 anchoring plan 미구현으로 현재 미존재. 본 plan은 anchoring 산출물(`Assets/SessionPanel/Prefabs/SessionPanel.prefab` + `StartMenuSectionContainer` 자식 placeholder + `IActiveInstrumentProvider` / `IActiveInstrument` 인터페이스)이 적용된 상태를 전제. — `unity-scene-reader 보고 (2026-05-05)`

## Approach

1. **`ISongCatalog` 인터페이스 + 데이터 컨트랙트 정의** — `Assets/SessionPanel/Scripts/ISongCatalog.cs`. 본 plan은 인터페이스만 정의, production 카탈로그는 후속 sub-spec.

   ```csharp
   public interface ISongCatalog {
       IReadOnlyList<ISongEntry> Songs { get; }
       event System.Action Changed; // production 카탈로그가 hot-reload 시 발화. stub은 미사용.
   }
   public interface ISongEntry {
       string SongId { get; }
       string Title { get; }
       string Artist { get; }
       IReadOnlyList<string> Difficulties { get; }
       IReadOnlyCollection<string> SupportedInstrumentIds { get; }
       string GetChartPath(string difficulty); // streamingAssets 상대경로
   }
   ```

2. **임시 stub catalog 구현** — `Assets/SessionPanel/Scripts/StubSongCatalog.cs` (MonoBehaviour, `ISongCatalog` 구현):
   - SerializeField: `RhythmSongDatabase[] sourceDatabases` (Piano_SongDatabase + DrumKit_SongDatabase wire).
   - `Awake`에서 source database를 합쳐 `songId`별로 entry를 만든다. 같은 songId가 여러 SongDatabase에서 등장하면 `SupportedInstrumentIds`에 instrumentKey를 누적.
   - `Difficulties`는 hard-coded `["Beginner"]`.
   - `GetChartPath(difficulty)`는 모든 곡에 대해 `"Songs/test.vmsong"`을 반환 (production 카탈로그가 곡별 경로를 매핑할 자리).

3. **`IActiveInstrument`에 `RhythmHost` 멤버 추가** — anchoring plan 산출물 `Assets/SessionPanel/Scripts/IActiveInstrumentProvider.cs`(또는 `IActiveInstrument.cs`)에 멤버 1개 append:

   ```csharp
   public interface IActiveInstrument {
       Transform PanelAnchor { get; }
       string InstrumentId { get; }
       RhythmGameHost RhythmHost { get; } // ← 본 plan 추가
   }
   ```

   anchoring plan은 인터페이스만 정의하고 구현은 후속 잡기 plan에 위임했으므로 본 plan은 인터페이스 멤버만 추가한다. 후속 잡기 plan이 RhythmHost를 wire한다(예: `instrumentTransform.GetComponentInChildren<RhythmGameHost>()`).

4. **StartMenuSection UI prefab 신설** — `Assets/SessionPanel/UI/StartMenuSection.prefab`. 자식 GameObject:
   - `SongList` — VerticalLayoutGroup. 동적으로 `SongRow.prefab` 인스턴스 추가.
   - `DifficultyList` — HorizontalLayoutGroup. 동적으로 `DifficultyButton.prefab` 추가.
   - `AccompanimentList` — VerticalLayoutGroup. 동적으로 `AccompanimentToggle.prefab` 추가.
   - `StartButton` — UGUI Button + 라벨 "Start".
   - 모두 World-space Canvas 자식 RectTransform.

5. **하위 UI prefab 3개 신설** — 각각 단일 위젯:
   - `Assets/SessionPanel/UI/SongRow.prefab` — Button + 곡명 라벨 + 아티스트 라벨. 활성/비활성 시 라벨 alpha 0.4.
   - `Assets/SessionPanel/UI/DifficultyButton.prefab` — Button + 난이도 라벨.
   - `Assets/SessionPanel/UI/AccompanimentToggle.prefab` — Toggle + 채널 라벨.

6. **`StartMenuSectionController` 스크립트 신설** — `Assets/SessionPanel/Scripts/StartMenuSectionController.cs`:
   - SerializeField: `songRowPrefab`, `difficultyButtonPrefab`, `accompanimentTogglePrefab`, `songListContent`, `difficultyListContent`, `accompanimentListContent`, `startButton`(`UnityEngine.UI.Button`), `activeInstrumentProviderObject`(`UnityEngine.Object` → 인터페이스 캐스팅), `songCatalogObject`(같은 패턴).
   - 내부 상태:
     - `currentInstrument: IActiveInstrument` (null 가능)
     - `selectedSong: ISongEntry`
     - `selectedDifficulty: string`
     - `accompanimentEnabled: Dictionary<int, bool>`
   - 흐름:
     - **Enable / 활성 악기 변경 (`ActiveInstrumentChanged`)**: 곡 목록을 다시 빌드. 곡 row가 `SupportedInstrumentIds.Contains(instrument.InstrumentId)`이면 활성, 아니면 비활성(반투명 + 클릭 차단). 곡/난이도/반주 선택 초기화.
     - **곡 row 클릭** → `selectedSong = song`. 난이도 목록 갱신. 반주 후보 초기화.
     - **난이도 클릭** → `selectedDifficulty = ...`. 차트 텍스트 로드 (`File.ReadAllText(Path.Combine(Application.streamingAssetsPath, selectedSong.GetChartPath(selectedDifficulty)))`) → `VmSongParser.Parse`. `chart.channelMap.entries`로부터 `instrumentKey == currentInstrument.InstrumentId`인 채널을 jugged channel로 식별, 나머지 채널을 반주 후보로 토글 row 생성, default `true`.
     - **반주 토글 변경** → `accompanimentEnabled[channel] = isOn`.
     - **시작 버튼**: 모든 선택이 갖춰진 상태에서 `currentInstrument.RhythmHost.StartSession(chart, RhythmSong-from-stub-or-fallback, judgedChannel, accompanimentEnabled)`.
   - 패널이 핀치 호출(activeInstrument == null) 상태에선 anchoring plan이 StartMenuSectionContainer GameObject SetActive(false). 본 컨트롤러는 가시성 토글 책임 없음.

7. **`RhythmGameHost.StartSession` 시그니처 확장** — `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` 수정:

   ```csharp
   public RhythmSession StartSession(VmSongChart chart, RhythmSong song, int judgedChannel,
                                     IReadOnlyDictionary<int, bool> accompanimentEnabled = null) {
       lastAccompanimentEnabled = accompanimentEnabled;
       // 기존 로직 그대로
   }
   public IReadOnlyDictionary<int, bool> LastAccompanimentEnabled => lastAccompanimentEnabled;
   IReadOnlyDictionary<int, bool> lastAccompanimentEnabled;
   ```

   - 기본값 `null`로 두어 호출부 0건이라도 컴파일 안전. 자동 반주 발화 마스킹은 후속 rhythm-game accompaniment plan이 `Host.LastAccompanimentEnabled`를 읽어 적용.

8. **SampleScene 와이어링** — anchoring plan 산출물 SessionPanel.prefab의 `StartMenuSectionContainer` 자식에 `StartMenuSection.prefab` 인스턴스 + `StartMenuSectionController` 부착. inspector wire:
   - `activeInstrumentProviderObject` ← anchoring plan dummy provider 또는 후속 production wire.
   - `songCatalogObject` ← SampleScene 루트에 새로 생성한 `StubSongCatalog` GameObject. 그 GameObject의 `sourceDatabases`에 Piano/DrumKit_SongDatabase wire.

## Deliverables

- `Assets/SessionPanel/Scripts/ISongCatalog.cs` — 신설. `ISongCatalog` + `ISongEntry` 인터페이스.
- `Assets/SessionPanel/Scripts/StubSongCatalog.cs` — 신설. RhythmSongDatabase[]를 합쳐 곡 entry 노출.
- `Assets/SessionPanel/Scripts/StartMenuSectionController.cs` — 신설.
- `Assets/SessionPanel/UI/StartMenuSection.prefab` — 신설. 4개 자식 컨테이너 + 시작 버튼.
- `Assets/SessionPanel/UI/SongRow.prefab` — 신설.
- `Assets/SessionPanel/UI/DifficultyButton.prefab` — 신설.
- `Assets/SessionPanel/UI/AccompanimentToggle.prefab` — 신설.
- `Assets/SessionPanel/Scripts/IActiveInstrumentProvider.cs` (anchoring plan 산출물) — 수정. `IActiveInstrument`에 `RhythmGameHost RhythmHost { get; }` 멤버 추가.
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — 수정. `StartSession` 시그니처 확장 + `LastAccompanimentEnabled` 노출.
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab` (anchoring plan 산출물) — 수정. StartMenuSectionContainer에 StartMenuSection.prefab 인스턴스 부착 + StartMenuSectionController inspector wire.
- `Assets/Scenes/SampleScene.unity` — `StubSongCatalog` GameObject 추가 + Piano/DrumKit_SongDatabase wire + `StartMenuSectionController.songCatalogObject` wire.

## Acceptance Criteria

- [ ] `[auto-hard]` `ISongCatalog.cs`, `StubSongCatalog.cs`, `StartMenuSectionController.cs`, 수정된 `RhythmGameHost.cs`, 수정된 `IActiveInstrumentProvider.cs` 모두 컴파일 통과 (`read_console` 컴파일 에러 0).
- [ ] `[auto-hard]` `RhythmGameHost.StartSession` 시그니처가 4-arg(`VmSongChart, RhythmSong, int, IReadOnlyDictionary<int, bool>`)로 확장되었고 4번째 인자는 default `null` (grep 단일 매치).
- [ ] `[auto-hard]` `IActiveInstrument` 인터페이스에 `RhythmGameHost RhythmHost` 멤버가 정확히 1회 등장 (grep 단일 매치).
- [ ] `[auto-hard]` `StartMenuSection.prefab` 인스턴스화 후 자식 트리에 `SongList`, `DifficultyList`, `AccompanimentList`, `StartButton` GameObject가 정확히 1개씩 존재.
- [ ] `[auto-hard]` `SongRow.prefab`, `DifficultyButton.prefab`, `AccompanimentToggle.prefab` 각각 인스턴스화 시 콘솔 에러·예외 0건.
- [ ] `[auto-hard]` `StartMenuSection.prefab` 인스턴스화 시 콘솔 에러·예외 0건.
- [ ] `[auto-hard]` `StubSongCatalog.sourceDatabases`가 SampleScene에서 정확히 2개(Piano_SongDatabase + DrumKit_SongDatabase)로 직렬화됨 (직렬화 reference grep).
- [ ] `[manual-hard]` SampleScene Editor Play (anchoring + volume-section plan 적용 후 가정) + dummy provider Current=null 상태로 패널 호출(컨트롤러 메뉴 버튼) → `StartMenuSectionContainer`가 SetActive(false), 시작 메뉴 UI 노출되지 않음.
- [ ] `[manual-hard]` dummy provider Current를 Piano(stub IActiveInstrument: InstrumentId=`"Piano"`, RhythmHost=Piano prefab 자식 host) 로 set → `StartMenuSectionContainer` SetActive(true), 곡 목록에 SampleSong이 활성 row로 노출.
- [ ] `[manual-hard]` 곡 row 클릭 → 난이도 목록에 `"Beginner"` 1개 노출. 난이도 클릭 → 반주 트랙 목록에 Piano 채널(channel 1) 제외 채널들 토글로 노출 + 모두 default ON.
- [ ] `[manual-hard]` 시작 버튼 누름 → `RhythmGameHost.StartSession`이 호출되고 콘솔 에러 0. inspector 또는 Debug.Log로 `RhythmHost.LastAccompanimentEnabled`가 UI 토글 상태와 일치함을 확인.
- [ ] `[manual-hard]` 곡/난이도가 선택된 상태에서 dummy provider Current를 다른 IActiveInstrument로 swap 또는 null로 변경 → 곡/난이도/반주 선택이 모두 초기화되고 UI가 비워짐.
- [ ] `[manual-hard]` `Piano_SongDatabase.songs`에서 SampleSong을 임시로 제거(또는 다른 곡만 가진 DB로 교체) 후 dummy provider Current=Piano 시나리오 → SampleSong row가 비활성(반투명 + 클릭 차단)으로 보임. 검증 후 SongDatabase 원복.

## Out of Scope

- 곡 카탈로그 production (난이도 데이터 모델, 곡-차트 link, 곡 추가/삭제 UX, 카탈로그 hot-reload) — 별도 후속 sub-spec.
- 자동 반주 발화 마스킹의 실제 적용 — rhythm-game `accompaniment` plan에서 `RhythmGameHost.LastAccompanimentEnabled`를 소비.
- 곡 목록 정렬·필터·검색 컨트롤 — 별도 sub-spec(예: `song-list-controls`).
- 결과 화면 / 점수 합산 / 등급.
- 노트 시각화 UI (떨어지는 노트 등) — rhythm-game side 후속 plan.
- 멀티플레이.
- 시작 메뉴 UI의 시각·청각 디자인(텍스처, 폰트, 색상, 사운드 피드백).
- 활성 악기 신호의 production wiring (drum/piano 잡기) — 다른 작업자 plan에서.

## Notes

- `IActiveInstrument`에 `RhythmHost` 멤버를 추가하는 것은 anchoring plan 산출물 인터페이스 파일을 수정하는 변경이다. spec-implement orchestrator는 sub-spec 표 순서(01 → 02 → 03)대로 자연스럽게 위상정렬한다 — anchoring plan이 먼저 들어간 상태에서 본 plan이 인터페이스 멤버를 추가한다. anchoring plan은 인터페이스 정의만 하고 구현체는 두지 않으므로 멤버 추가가 다른 production 코드를 깨지 않는다.
- 코드베이스에 `RhythmGameHost.StartSession(...)` 호출부는 본 plan 작성 시점에 0건이다(grep). 본 plan이 첫 호출부를 추가하므로 시그니처 확장 시 외부 영향 없음.
- `accompanimentEnabled` 마스킹은 `IReadOnlyDictionary<int, bool>`로 받는다. 키가 없는 채널은 default ON으로 간주하는 게 production accompaniment plan에서 자연스럽다 — 본 plan은 UI에서 후보 채널만 추려 명시적으로 키를 채워 넘긴다.
- `SampleChart_Beginner.asset`이 orphan(missing script) 상태이므로 본 plan에선 그 자산을 무시하고 streamingAssets `Songs/test.vmsong` 텍스트 파일을 모든 곡의 Beginner 차트 source로 매핑한다. orphan 자산 자체의 정리는 본 plan 책임 밖(후속 catalog plan 또는 데이터 정리 task).

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신 -->
