# Song Selection Activation — 진단·수정

**Linked Spec:** [`04-song-selection-activation.md`](../specs/04-song-selection-activation.md)
**Status:** `Done`

## Goal

악기를 잡은 상태에서 세션 패널이 열렸을 때 `RhythmGameSectionController.RefreshSongList()`가 해당 악기를 지원하는 곡 row를 항상 활성(클릭 가능)으로 표시하도록 한다. 현재는 모든 row가 비활성(회색)으로 그려져 사용자가 리듬게임을 시작할 수 없는 회귀 버그를 수정한다.

## Context

session-panel `04-song-selection-activation` sub-spec의 첫(그리고 유일하게 계획된) plan이다. spec [`04-song-selection-activation.md`](../specs/04-song-selection-activation.md)가 정의하는 핵심:

- 악기를 잡은 상태에서 패널이 열리면 그 악기 트랙이 있는 곡 row가 클릭 가능 상태로 표시되어야 한다.
- 악기 미잡음 + 핀치 호출 케이스는 시작 메뉴 섹션 자체가 노출되지 않으므로 본 plan 범위 밖(`02-start-menu-section.md`의 노출 토글이 담당).

본 plan은 회귀 진단·수정·재발 방지의 3축으로 구성한다.

### 현재 코드 상태 (Inject·OnEnable 타이밍 분석)

`Assets/SessionPanel/Scripts/RhythmGameSectionController.cs`:

- `Awake()`는 SerializedField 값에서 `_provider` / `_catalog`를 채운다. prefab의 SerializedField는 `{fileID: 0}` (null) — wiring은 scene 단계의 `SessionPanelController.EnsurePanelInstance()`가 `_rhythmCtrl.Inject(...)`로 주입한다.
- `OnEnable()`은 `_currentInstrument = _provider?.Current`로 1회 동기화 후 `RefreshSongList()`를 호출하지만, `_provider`가 그 시점 null이면(즉 Awake 단계에서 Inject보다 먼저 OnEnable이 발화한 경우) `_provider?.Current`는 null이고 `RefreshSongList` 안에서 `if (_catalog == null || _currentInstrument == null) return;` 가드에 걸려 곡 row 자체가 빈 상태가 된다.
- `Inject()`는 `_currentInstrument = _provider?.Current` 다음 `ResetSelection() + RefreshSongList()`를 호출하지만, **`Inject` 호출 시점 panel은 아직 비활성**(EnsurePanelInstance가 TransitionTo `InstrumentOpened`의 `_panelInstance.SetActive(true)` 이전에 `Inject`를 호출)이라 `isActiveAndEnabled == false` → `ActiveInstrumentChanged` 구독이 스킵되며, 직후 panel을 활성화하는 `SetActive(true)`로 `OnEnable`이 다시 발화해 구독을 set up한다.

### 가설 분기

세 가지 1차 후보 중 하나에 root cause가 있다:

1. **Awake-OnEnable 1프레임 레이스**: 패널 인스턴스 prefab의 RhythmGameSectionController GameObject가 prefab 직렬화 상태에서 `m_IsActive: 1`이면, Instantiate 직후 `_rhythmCtrl.Inject` 호출보다 먼저 `Awake → OnEnable`이 한 번 발화한다. Inject가 그 후 `RefreshSongList`로 한 번 더 그리지만 — Inject는 `_provider?.Current`를 직접 읽어서 채우므로 row가 정확히 그려져야 한다. 하지만 OnEnable이 다시 panel SetActive(true)로 호출되어 row를 한 번 더 갈아엎으며, 그 시점 `_currentInstrument`는 OnEnable이 다시 `_provider?.Current`로 재할당 — `_provider`가 null이면(Awake에선 prefab의 null Object 캐스팅) `_currentInstrument`가 null이 돼서 row 자체가 안 그려진다.
2. **`Inject`의 `isActiveAndEnabled` 가드**: Inject는 비활성 단계에서 호출되므로 `ActiveInstrumentChanged` 구독을 스킵. 패널이 활성된 다음 OnEnable이 구독을 set up하긴 하나, OnEnable의 `_currentInstrument = _provider?.Current` 재할당은 Inject가 채워둔 값을 덮어쓴다. 만약 OnEnable 직전에 `Inject`로 `_provider`가 set 됐다면 동일 값으로 덮어쓰므로 문제 없음. 하지만 OnDisable → 재OnEnable 사이클에서 panel이 닫혔다 다시 열리면 OnEnable이 Inject 없이 단독으로 발화하며, 그때 `_provider`가 만약 panel SetActive(false) 중에 `_rhythmCtrl.Inject(null, null)` 등으로 invalidate 됐다면(현 코드에선 그렇진 않음) null.
3. **`SongRowUI.Setup`의 `active` 파라미터 vs Image 컬러 분리**: `RefreshSongList`는 row의 background Image에는 `supported ? normalCol : grayCol`을 직접 set 하지만, `SongRowUI.Setup(song, supported, ...)` 안에서는 `button.interactable = active`만 set한다. 만약 `SongRowUI`가 prefab에서 `button` SerializedField에 wiring이 빠져 있으면 `button.interactable` 변경이 적용 안 되며, 동시에 `RefreshSongList`의 직접 색 변경도 Button.colors.disabledColor 우선 적용 규칙(`button.interactable == false` 시 disabledColor만 보여짐)에 의해 회색으로만 보일 수 있다. SongRow prefab의 button wiring 상태를 직접 확인해야 한다.

본 plan의 진단 단계(Approach 1)는 디버그 로그를 1회만 출력(라인 단위 Log)해 어느 가설인지 박제한다.

### 본 plan이 의도적으로 미루는 책임

- **NoteDisplayPanel 위치 조정**: 사용자가 prefab을 직접 편집해 처리. 본 plan은 건드리지 않는다.
- **곡 카탈로그 출처/스캔 정책**: `rhythm-game/specs/05-song-catalog.md`로 위임. 본 plan은 현 `StubSongCatalog + RhythmSongDatabase[]` 와이어링 그대로 사용.
- **시작 메뉴 섹션 노출 토글(악기 미잡음 시 비활성)**: `02-start-menu-section.md`가 담당. 본 plan은 이미 그 plan이 시행돼 패널 안에 RhythmGameSectionController가 부착된 상태를 가정.

## Verified Structural Assumptions

- `Assets/Scenes/SampleScene.unity::StubSongCatalog`의 `sourceDatabases`에는 `DrumKit_SongDatabase.asset` (`guid: de6fec7c8d5abe04887bc7dc22e08a29`) + `Piano_SongDatabase.asset` (`guid: 9b7785221d8a3c5409d752aff6368e29`) 두 SO가 정상 와이어링 — `Grep StubSongCatalog Assets/Scenes/SampleScene.unity` 결과 라인 1313-1315. 곡 entry 손실 가능성은 0. — `Read Assets/Scenes/SampleScene.unity (2026-05-17)`
- `Assets/RhythmGame/Data/DrumKit_SongDatabase.asset::instrumentKey = "DrumKit"`, `Piano_SongDatabase.asset::instrumentKey = "Piano"`. 양쪽 모두 동일 `SampleSong.asset` (`guid: b423c8e4bc28d95469931a8c7b369c0e`) 1곡 보유. — `Read Assets/RhythmGame/Data/{DrumKit,Piano}_SongDatabase.asset (2026-05-17)`
- `Assets/Instruments/Drum/Prefabs/DrumKit.prefab::instrumentId = "DrumKit"` (라인 371), `Assets/Instruments/Piano/Prefabs/Piano.prefab::instrumentId = "Piano"` (라인 16729). `InstrumentBase : IActiveInstrument`가 이 값을 그대로 `InstrumentId` getter로 노출. ID 문자열 동일성은 케이스 포함 일치. — `Grep ^  instrumentId: Assets (2026-05-17)`
- `Assets/SessionPanel/Scripts/StubSongCatalog.cs::SongEntryImpl._instrumentIds`는 `HashSet<string>`(기본 ordinal comparer). `BuildCatalog`가 `db.instrumentKey`(=DrumKit/Piano)를 그대로 `AddSupportedInstrument`로 넣는다. `Contains` 비교는 정확히 ordinal-case-sensitive — instrumentId가 동일 케이스라면 true. — `Read Assets/SessionPanel/Scripts/StubSongCatalog.cs (2026-05-17)`
- `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs`의 SerializedField: `songListContent` / `songRowPrefab` / `noSongPanel` / `songSelectedPanel` / `previewButton` / `bpmBar` / `difficultyContainer` / `difficultyButtonPrefab` / `playButton` / `activeInstrumentProviderObject` / `songCatalogObject`. `Inject(UnityEngine.Object, UnityEngine.Object)` 공개 메서드는 `SessionPanelController.EnsurePanelInstance` 1군데에서만 호출. — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-17)`
- `Assets/Instruments/_Core/Scripts/IActiveInstrumentProvider.cs::IActiveInstrumentProvider.ActiveInstrumentChanged` 이벤트 시그니처: `event System.Action<IActiveInstrument> ActiveInstrumentChanged;`. `TeleportInstrumentProvider`는 anchor teleport 발생 시 `_current = next; ActiveInstrumentChanged?.Invoke(_current);`로 즉시 발화 (cf. `Assets/Instruments/_Core/Scripts/TeleportInstrumentProvider.cs`). 같은 instrument로의 재teleport는 `ReferenceEquals` 체크로 차단 — 이벤트 누락 가능성 0. — `Read Assets/Instruments/_Core/Scripts/{IActiveInstrumentProvider,TeleportInstrumentProvider}.cs (2026-05-17)`
- `Assets/Scenes/SampleScene.unity::SessionPanelController._songCatalogObject = {fileID: 181637959}` (StubSongCatalog 인스턴스), `_activeInstrumentProviderObject = {fileID: 8369896}` (TeleportInstrumentProvider 인스턴스, m_Script GUID `1bb93744547bbbe46b6c0a02f816ed78`). — `Grep StubSongCatalog/TeleportInstrumentProvider Assets/Scenes/SampleScene.unity (2026-05-17)`
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab::RhythmGameSectionController.activeInstrumentProviderObject = {fileID: 0}` + `songCatalogObject = {fileID: 0}` — prefab 단계엔 의도적으로 null, 런타임 `Inject`가 단일 진실원. — `Grep activeInstrumentProviderObject Assets/SessionPanel/Prefabs/SessionPanel.prefab (2026-05-17)`
- asmdef `Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef`의 references: `Instruments` / `RhythmGame.Data` / `RhythmGame.Runtime` / `Unity.InputSystem` / `Unity.XR.Interaction.Toolkit` / `Unity.TextMeshPro`. 본 plan이 추가할 코드는 모두 기존 namespace(`SessionPanel`, `Instruments`)와 `UnityEngine` 만 쓰므로 references 추가 불필요. — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-17)`

## Approach

### 1단계: 1회성 진단 로그로 root cause 박제 (수정 후 제거)

`RhythmGameSectionController.RefreshSongList()` 진입 시 다음 정보를 `Debug.Log` 1회 출력해 어느 분기에서 문제가 발생했는지 박제:

- `_catalog == null` 여부 + `_catalog?.Songs.Count`
- `_currentInstrument == null` 여부 + `_currentInstrument?.InstrumentId`
- `isActiveAndEnabled` + `_provider == null` 여부
- 각 곡에 대해 `song.SongId / song.SupportedInstrumentIds(join ",")` + `supported` 결과

본 로그는 Approach 2의 수정 적용 후 **단일 commit 안에서 제거**한다. 진단 결과(어떤 가설이 맞았는지)는 plan `## Notes`에 박제한다.

### 2단계: 본문 수정 — `Inject`/`OnEnable` 양쪽 진입로 명시화 + null-safe 재시도

본 plan은 진단 결과와 무관하게 가설 1·2·3 모두를 막는 합집합 패치를 적용한다 (회귀 방지 우선):

a. **`RhythmGameSectionController.OnEnable`**: `_provider`가 null이면 SerializedField에서 한 번 더 시도(`_provider = activeInstrumentProviderObject as IActiveInstrumentProvider; _catalog = songCatalogObject as ISongCatalog;`). 이미 Inject가 채웠다면 no-op. 그다음 기존대로 `_currentInstrument = _provider?.Current` + `RefreshSongList()`.

b. **`RhythmGameSectionController.Inject`**: 호출 시점 `isActiveAndEnabled`가 false라도 `ActiveInstrumentChanged` 구독을 무조건 한다(중복 구독 방지 위해 `-=` 후 `+=`). 그래야 `Inject → SetActive(true) → OnEnable`의 순서에서 OnEnable이 중복 구독을 시도해도 한쪽이 빠지지 않는다. OnEnable·OnDisable 양쪽도 idempotent하게(`-=`→`+=`) 유지.

c. **`RhythmGameSectionController.RefreshSongList`**: 입력 가드(`_catalog == null || _currentInstrument == null`)에 도달하면 `noSongPanel`만 활성화하고 ShowDetail은 false (현 동작 유지). 추가로 row 생성 직후 `SongRowUI.Setup` 호출 전에 `button.targetGraphic`이 null이면 row의 background Image로 바인딩(현재 `rowBtn.targetGraphic = bg;` 라인이 이미 처리 — 유지).

d. **`SongRowUI.Setup`**: button SerializedField wiring 누락 케이스를 fall-back. `button == null`이면 `GetComponent<Button>()`로 lazy 회수. 회수 후에도 null이면 plan AC가 실패하도록 그대로 둔다(prefab 누락 회귀 알람).

### 3단계: 회귀 테스트 1개 추가

`Assets/SessionPanel/Tests/RhythmGameSectionRefreshTests.cs` (신규):

- `[Test] SupportedSong_IsActive_WhenCurrentInstrumentMatches`:
  - in-memory `ISongCatalog` stub + `IActiveInstrumentProvider` stub(`DummyActiveInstrument`/`DummyActiveInstrumentProvider` 재활용) 으로 `RhythmGameSectionController`를 빈 Canvas 위에 instantiate.
  - `Inject(provider, catalog)` 호출 → `songListContent` 자식 row 수 == 카탈로그 곡 수.
  - 매칭 곡의 row Button.interactable == true, 미매칭 row == false.
  - Edit mode test로 작성(런타임 awake 흐름 인위적으로 강제). UnityXR 의존성 없음.

본 테스트는 `SessionPanel.Tests.asmdef`에 자동 포함(폴더 위치).

### 4단계: 진단 로그 제거 + manual screenshot 검증

진단 결과를 `## Notes`에 박제한 다음 1단계의 `Debug.Log` 제거. Editor Play 모드 + Piano teleport → 패널 자동 노출 → 곡 row 활성 색(파란색) 확인 screenshot 1회.

## Deliverables

- `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` — `Inject`/`OnEnable`/`RefreshSongList` null-safe 강화 (수정).
- `Assets/SessionPanel/Scripts/SongRowUI.cs` — `Setup`에서 button SerializedField null-safe lazy fetch (수정).
- `Assets/SessionPanel/Tests/RhythmGameSectionRefreshTests.cs` — 회귀 테스트 (신규).
- (작업 중간 산출물) 진단 `Debug.Log` 추가/제거 — commit 단위로는 본 plan에 흔적 0건.

## Acceptance Criteria

- [ ] `[auto-hard]` 수정 후 Unity Editor가 컴파일 에러 0건으로 완료된다.
  **검증:** `unity-test-runner` Task 호출 결과의 `read_console` 보고에서 `error CS` 0건 + `editor_state.isCompiling == false`.

- [ ] `[auto-hard]` `RhythmGameSectionRefreshTests.SupportedSong_IsActive_WhenCurrentInstrumentMatches` 테스트가 통과한다. catalog가 `["DrumKit"]`/`["Piano"]` 지원 곡 2개를 가지고, provider.Current.InstrumentId가 `"DrumKit"`일 때 DrumKit 곡 row의 `Button.interactable == true`, Piano 전용 곡 row의 `Button.interactable == false`.
  **검증:** `unity-test-runner` Task 호출로 EditMode 실행 → `PASS RhythmGameSectionRefreshTests.SupportedSong_IsActive_WhenCurrentInstrumentMatches`.

- [ ] `[auto-hard]` `RhythmGameSectionController.Inject`가 비활성 상태(`isActiveAndEnabled == false`)에서 호출돼도 `ActiveInstrumentChanged` 이벤트를 1회 정상 구독해, 이후 provider가 발화시 `RefreshSongList`가 호출된다.
  **검증:** 동일 테스트 파일에 `[Test] Inject_BeforeActivation_SubscribesToProviderChange` 추가 — Inject 호출(go 비활성) → provider.RaiseChanged(piano) → `songListContent.childCount > 0` 확인.

- [ ] `[auto-soft]` 기존 `SessionPanelWiringTests` 3 테스트가 회귀 없이 통과한다 (`_activeInstrumentProviderObject` / `_songCatalogObject` / `panelPrefab` 와이어링).
  **검증:** `unity-test-runner` 결과에서 `SessionPanelWiringTests.*` 3건 모두 PASS.

- [ ] `[auto-hard]` 진단용 `Debug.Log` 잔존 0건. plan 종료 시점 코드 베이스에 `[RhythmGame Diag]` 또는 진단 prefix 문자열이 남지 않는다.
  **검증:** `Grep "\\[RhythmGame Diag\\]" Assets/SessionPanel/Scripts` 결과 0건.

- [ ] `[manual-hard]` Editor Play 모드에서 사용자가 Piano로 teleport하면 세션 패널이 자동 노출되고, RhythmGame 섹션의 곡 목록에서 Piano 지원 곡 row가 파란색(활성) + 클릭 가능, 비지원 곡 row가 회색(비활성) + 클릭 불가로 표시된다. 곡 클릭 시 difficulty 버튼 컨테이너가 채워진다.
  **검증:** Editor Play → SampleScene → Piano anchor로 teleport → 세션 패널 노출 확인 → 곡 row 클릭 → difficulty 버튼 노출 screenshot 1장 첨부.

## Out of Scope

- NoteDisplayPanel 위치 조정 — 사용자가 prefab 직접 편집으로 처리.
- 곡 카탈로그 폴더 스캔/외부 소스 — `rhythm-game/specs/05-song-catalog.md`.
- 시작 메뉴 섹션 노출 자체(악기 미잡음 시 비활성) — `02-start-menu-section.md` 산출물에 의존.
- 볼륨 섹션 회귀 진단 — `03-volume-section.md`.
- 멀티플레이 환경에서 타 사용자 악기 곡 표시 — 본 spec 범위 밖.

## Notes

- 진단 결과(가설 1·2·3 중 어느 것이 root cause였는지)는 4단계 종료 시 본 섹션에 박제한다.
- 만약 진단에서 가설 외 새 원인(예: `SongRowUI.button` SerializedField 누락)이 드러나면 본 plan에서 prefab YAML도 함께 수정한다 (`/spec-build`의 atomic commit이 prefab edit MCP 절차를 따른다).
- 2026-05-17 진단 결과: 가설 2(Inject 호출 시 isActiveAndEnabled == false 가드로 ActiveInstrumentChanged 구독 누락)가 root cause. 합집합 패치(Inject 가드 제거 + OnEnable null-safe provider 폴백 + SongRowUI.button lazy GetComponent 폴백)로 가설 1·2·3 모두 봉쇄.
- 2026-05-17 manual-hard 검증 (TestSceneSanyo, 사용자 직접 Play 모드): Piano/DrumKit teleport → 세션 패널 자동 노출 → 잡은 악기 지원 곡 row 파란색·클릭 가능 / 비지원 곡 row 회색·클릭 불가 / 곡 클릭 시 difficulty 버튼 컨테이너 채워짐. 의도대로 동작 확인. — 사용자 보고 (parksky0311@gmail.com).

## Handoff

RhythmGameSectionController.Inject의 isActiveAndEnabled 가드 제거 + OnEnable의 null-safe provider 폴백 추가로 가설 2(Inject 호출 시 비활성 상태에서 구독 누락)를 수정함. SongRowUI.Setup에 button null-safe lazy GetComponent 폴백 추가(가설 3 예방). 회귀 테스트 RhythmGameSectionRefreshTests 2건(SupportedSong_IsActive_WhenCurrentInstrumentMatches, Inject_BeforeActivation_SubscribesToProviderChange) 신규 추가. EditMode 전체 10/10 통과. manual-hard(Piano teleport → 곡 row 활성 확인, TestSceneSanyo)도 pass — 사용자 직접 검증 완료.
