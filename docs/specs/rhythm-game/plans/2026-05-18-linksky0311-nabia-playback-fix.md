# 나비야 검증 실패 3건 수정 — DrumKit instrumentId 일치 + 난이도 숫자화

**Linked Spec:** [`09-nabia-playback-verification.md`](../specs/09-nabia-playback-verification.md)
**Caused By:** [`2026-05-18-linksky0311-nabia-playback-verification.md`](./2026-05-18-linksky0311-nabia-playback-verification.md)
**Status:** `Done`

## Goal

선행 검증 plan에서 발견된 3건의 manual-hard 실패(items 4 / 6 / 7)와 난이도 버튼 순서 결함을 동시에 해소한다. 근본 원인은 `DrumKit.prefab.instrumentId`("DrumKit")와 vmsong `instrument=drum` 사이의 키 불일치, 그리고 `MultiFileSongEntry.GetDifficultiesFor`가 파일 열거 알파벳 순서를 그대로 반환해 `easy → hard → normal`로 뜨던 문제다. 사용자 요청에 따라 난이도를 vmsong 파일명에 숫자 `1/2/3`으로 저장하고 UI에서는 `Easy/Normal/Hard`로 표시하도록 함께 정리한다.

## Context

선행 plan(`2026-05-18-linksky0311-nabia-playback-verification.md`)의 검증 결과:

- **AC 4 FAIL** — drum 토글 ON → 드럼 소리가 여전히 안 들림.
  - 근본 원인: `RhythmAccompaniment.BuildInstrumentMap()` (`Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs:96~110`)에서 씬 InstrumentBase를 `byKey[inst.InstrumentId] = inst`로 인덱싱한다(OrdinalIgnoreCase). DrumKit 프리팹의 `instrumentId: DrumKit`(`Assets/Instruments/Drum/Prefabs/DrumKit.prefab:372`)이 키로 등록되지만, vmsong은 `instrument=drum`이므로 `byKey.TryGetValue("drum", ...)` 실패 → channel 10이 `_map`에 없음 → `Fire()` 시 발화 대상 없음.
- **AC 6 FAIL** — 드럼 grab 시 nabia 행이 비활성(어두운색, 클릭 불가)으로 표시.
  - 근본 원인: `RhythmGameSectionController.RefreshSongList()` line 117의 `song.SupportedInstrumentIds.Contains(_currentInstrument.InstrumentId)`. `SupportedInstrumentIds`는 vmsong 파일명/내용에서 만들어진 `"piano"`/`"drum"` (OrdinalIgnoreCase HashSet, `FolderScanSongCatalog.cs:150`)이고, `_currentInstrument.InstrumentId`는 prefab 직렬화 필드 그대로 `"DrumKit"` → 비교 실패. (Piano는 `"Piano"` vs `"piano"`라 OrdinalIgnoreCase로 통과 — 그래서 piano 쪽만 정상.)
- **AC 7 FAIL** — 드럼 grab + Hard → piano 토글이 안 뜸. AC 6 fail의 직접 파생(곡 자체가 비활성이라 진입 불가).
- **추가 결함** — 난이도 버튼이 `easy → hard → normal` 순으로 표시. 원인은 `FolderScanSongCatalog.cs:34~35`가 파일을 알파벳 오름차순으로 열거하고, `MultiFileSongEntry.GetDifficultiesFor()` (line 169~173)가 `_diffsByInstrument`의 raw `List<string>`을 그대로 반환하기 때문. 같은 클래스의 `_allDifficulties`(line 156~160)는 `_diffPriority = {"Easy","Normal","Hard"}` 기준 정렬이 되어 있으나, 난이도 버튼 생성(`RhythmGameSectionController.cs:162`)에서는 `GetDifficultiesFor(currentInstrumentId)` 쪽을 호출하므로 정렬 미적용.
- **사용자 요청** — 난이도를 vmsong 파일명에 `1/2/3` 숫자로 저장하고, UI 표시는 `1→"Easy"`, `2→"Normal"`, `3→"Hard"`. 비‑숫자 파일(`test_song`)도 동일 컨벤션으로 통일.

`AC 3 pass`("drum 토글 OFF → 드럼 무음")로부터 토글 게이트(`ShouldFire`) 자체는 정상이므로, drum이 들리지 않는 원인은 게이트가 아니라 BuildInstrumentMap에서 매핑 자체가 비어 있는 것. 즉 단일 수정(instrumentId를 "drum"으로 정렬)으로 4·6·7이 동시 해소된다.

## Verified Structural Assumptions

- `DrumKit.prefab`의 직렬화 instrumentId는 `DrumKit` (line 372). DrumKit 클래스는 `InstrumentBase`를 그대로 상속하며 `instrumentId` 자체에 대한 자체 처리는 없음 — `Read Assets/Instruments/Drum/Prefabs/DrumKit.prefab (2026-05-18) line 372`, `Read Assets/Instruments/Drum/Scripts/DrumKit.cs (2026-05-18)`, `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-18) lines 34~45`.
- `InstrumentBase.instrumentId` 사용처: (a) `PlayerPrefs`/`InstanceVolumeStore` 키(line 57~58, 68~69), (b) `IActiveInstrument.InstrumentId` 노출(line 45). 따라서 prefab의 `instrumentId`를 `DrumKit → drum`으로 바꾸면 InstanceVolume PlayerPrefs 키가 변경되어 기존 사용자의 드럼 볼륨 설정이 디폴트로 리셋된다. **본 plan의 허용 부작용으로 박제**(Notes에도 명시) — `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-18) lines 34~70`.
- `RhythmAccompaniment.BuildInstrumentMap` 비교자는 `System.StringComparer.OrdinalIgnoreCase`. Piano prefab은 `instrumentId: Piano`(`Assets/Instruments/Piano/Prefabs/Piano.prefab:16743`)인데 vmsong은 `instrument=piano`이라 OrdinalIgnoreCase로 매칭. 즉 piano 측은 손대지 않아도 되고, drum 측만 정렬하면 양쪽 일관 — `Read Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs (2026-05-18) lines 93~112`, `Read Assets/Instruments/Piano/Prefabs/Piano.prefab (2026-05-18) line 16743`.
- `RhythmGameSectionController.RefreshSongList` line 117: `song.SupportedInstrumentIds.Contains(_currentInstrument.InstrumentId)`. `SupportedInstrumentIds`는 `MultiFileSongEntry`가 보관하는 OrdinalIgnoreCase HashSet (`FolderScanSongCatalog.cs:150`). 따라서 vmsong에서 추출된 키와 prefab의 `instrumentId`가 OrdinalIgnoreCase로 같아야 행 활성색이 됨 — `Read Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs (2026-05-18) lines 137~167`, `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18) line 117`.
- `FolderScanSongCatalog.BuildEntriesFromFolder` (lines 29~83) 파일명 파싱: `name = <songname>-<instrument>-<difficulty>`. `lastHyphen` 기준 split이라 songname에 하이픈이 있어도 안전. 난이도 segment에 `1/2/3` 숫자가 들어와도 split 로직 자체는 영향 없음 — `Read Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs (2026-05-18) lines 29~83`.
- `MultiFileSongEntry._diffPriority`(line 132): 현재 `{"Easy","Normal","Hard"}` (대소문자 무관 HashSet `Remove`). 본 plan은 이걸 `{"1","2","3"}`로 교체하고 `GetDifficultiesFor`가 정렬된 결과를 반환하도록 수정 — `Read Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs (2026-05-18) lines 130~174`.
- `DifficultyButtonUI.Setup(string difficulty, ...)` (`Assets/SessionPanel/Scripts/DifficultyButtonUI.cs:26~33`): `label.text = difficulty`로 그대로 표시. 본 plan에서 `RhythmGameSectionController.OnSongRowClicked` 쪽 버튼 생성 코드가 표시용 라벨을 `"Easy"/"Normal"/"Hard"`로 매핑해 넘기거나, `DifficultyButtonUI.Setup`에 별도 displayLabel 파라미터를 추가하는 두 가지 중 *전자*를 선택(컴포넌트 API 변경 회피) — `Read Assets/SessionPanel/Scripts/DifficultyButtonUI.cs (2026-05-18)`.
- 기존 테스트 영향 점검: `FolderScanSongCatalogTests.cs`는 파일명을 *직접 작성*(`alpha-piano-easy.vmsong` 등)해 `easy/hard` 문자열을 어서트한다. 본 plan은 *프로덕션 vmsong 6+2건*만 숫자 파일명으로 변경하고, 단위 테스트는 기존 `easy/hard` 컨벤션을 그대로 사용해도 무방하다 — `Catalog` 자체는 임의 difficulty 문자열을 보관하기 때문(`_diffsByInstrument` Dictionary value type은 `List<string>`). 단 `_diffPriority`를 `{"1","2","3"}`로 좁히면 기존 테스트의 `easy/hard`는 우선순위 외 영역으로 떨어져 알파벳 오름차순(`AsReadOnly()`) 영역에 들어가지만, 어서트가 `Contains("easy")` 패턴이라 통과한다 — `Read Assets/SessionPanel/Tests/FolderScanSongCatalogTests.cs (2026-05-18) lines 56~98, 126~145`.
- asmdef 의존 박제: 본 plan은 신규 C# 파일을 추가하지 않고 기존 `Assets/SessionPanel/Scripts/*.cs`와 `Assets/RhythmGame/Tests/Editor/VmSongParserTests.cs`만 수정. `Assets/SessionPanel/SessionPanel.asmdef` / `Assets/RhythmGame/Tests/Editor/RhythmGame.Tests.Editor.asmdef`(또는 동등 어셈블리)는 기존 참조 그대로 충분 — `Glob Assets/SessionPanel/*.asmdef`, `Glob Assets/RhythmGame/Tests/**/*.asmdef`로 확인된 위치. 신규 reference 불필요.
- Unity 자산 수정 정책: DrumKit.prefab 수정은 단일 라인 직렬화 필드(`instrumentId`) 교체로, `unity-asset-edit` skill 기준 manage_prefabs MCP가 우선이지만 단일 텍스트 라인 교체로도 안전한 클래스다. 본 plan은 **MCP `manage_prefabs` 우선 사용, 실패 시 텍스트 Edit fallback**으로 진행하며 사용자 사전 승인은 본 plan 채택 자체로 갈음. vmsong 파일 rename은 .meta 동반 rename이므로 `git mv` 또는 OS rename 후 `.meta` 동기 — `Read CLAUDE.md (2026-05-18)`, `Read .claude/skills/unity-asset-edit/SKILL.md (개념 박제, 본 plan 적용 범위만)`.

## Approach

1. **DrumKit prefab instrumentId 정렬** — `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` 의 `instrumentId: DrumKit` → `instrumentId: drum`. MCP `manage_prefabs`(modify 액션)로 시도하고, MCP 미가용/실패 시 텍스트 Edit fallback. 이 한 줄로 AC 4·6·7 모두 해소됨.
2. **vmsong 파일명 숫자화** — 다음 8개 파일을 `<songname>-<instrument>-<1|2|3>.vmsong`으로 rename(.meta 동반):
   - `nabia-piano-easy.vmsong` → `nabia-piano-1.vmsong`
   - `nabia-piano-normal.vmsong` → `nabia-piano-2.vmsong`
   - `nabia-piano-hard.vmsong` → `nabia-piano-3.vmsong`
   - `nabia-drum-easy.vmsong` → `nabia-drum-1.vmsong`
   - `nabia-drum-normal.vmsong` → `nabia-drum-2.vmsong`
   - `nabia-drum-hard.vmsong` → `nabia-drum-3.vmsong`
   - `test_song-piano-easy.vmsong` → `test_song-piano-1.vmsong`
   - `test_song-drum-easy.vmsong` → `test_song-drum-1.vmsong`

   `.meta` 파일도 동일 prefix로 함께 rename (GUID 유지). Bash `git mv` 사용.
3. **`FolderScanSongCatalog._diffPriority` 교체 + `GetDifficultiesFor` 정렬** — `Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs`:
   - line 132: `_diffPriority = { "Easy", "Normal", "Hard" }` → `_diffPriority = { "1", "2", "3" }`.
   - `MultiFileSongEntry.GetDifficultiesFor(string instrumentId)` (lines 169~173): `_diffsByInstrument`의 raw list 대신 `_diffPriority` 기준 정렬한 결과를 반환. 구현: instrument별 `List<string>`을 1회 정렬해 캐시(`_sortedDiffsByInstrument`)하고 그대로 반환. 정렬 함수는 `_allDifficulties` 빌더와 동일 로직 — `_diffPriority`에 있는 것 먼저(등장 순), 나머지는 OrdinalIgnoreCase 알파벳.
4. **`RhythmGameSectionController`에서 난이도 표시 라벨 매핑** — `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` `OnSongRowClicked()` (line 160~182):
   - 난이도 버튼 생성 루프(line 162)에서 `diff` 원본 문자열(`"1"/"2"/"3"`)을 콜백·내부 상태용으로 유지하되, `DifficultyButtonUI.Setup`에 넘길 *표시용 라벨*은 `MapDifficultyLabel(diff)` 결과를 사용. `MapDifficultyLabel` 헬퍼는 `"1"→"Easy"`, `"2"→"Normal"`, `"3"→"Hard"`, 그 외는 원본 그대로 반환.
   - `DifficultyButtonUI.Setup(string difficulty, ...)`의 첫 인자는 *콜백으로 돌려줄 식별자*로 쓰이므로 원본 `"1"/"2"/"3"`을 그대로 넘긴다. 단 라벨 표시만 `Setup` 직후 별도 메서드로 덮어쓰거나, `Setup`에 displayLabel 옵션 인자 추가. **선택**: `DifficultyButtonUI`에 `SetLabel(string text)` public 메서드 1개를 추가하고 `Setup` 호출 직후 `btn.SetLabel(MapDifficultyLabel(diff))` 호출. 이렇게 하면 콜백 식별자는 변경 없이 표시만 분리.
   - 그 결과 콜백으로 들어오는 `_selectedDifficulty`는 여전히 `"1"/"2"/"3"` — 이게 `GetChartPath(instrumentId, difficulty)` 키로 직접 사용되므로 파일명 segment와 정확히 일치.
5. **`VmSongParserTests.cs` 파일명 참조 업데이트** — `Assets/RhythmGame/Tests/Editor/VmSongParserTests.cs`:
   - `LoadSong("nabia-piano-easy.vmsong")` → `LoadSong("nabia-piano-1.vmsong")` 등 6건.
   - `LoadSong("test_song-drum-easy.vmsong")` → `LoadSong("test_song-drum-1.vmsong")`.
   - 테스트 메서드명은 의미 보존을 위해 변경하지 않거나, `Nabia_PianoEasy_*`처럼 라벨이 남아도 무방(주석 한 줄로 매핑 표기).
6. **컴파일·테스트 게이트** — 위 4·5번 변경 후 `unity-mcp-workflow` skill의 컴파일 대기·`read_console` 절차로 errors 0 확인. 이후 `unity-test-runner` sub-agent를 1회 호출해 `FolderScanSongCatalogTests` + `VmSongParserTests` + `RhythmGameSectionRefreshTests` + `RhythmGameSectionInstrumentToggleTests`가 모두 pass인지 확인. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 진행.
7. **수동 재검증 (선행 plan 실패 AC 한정 + 추가 결함)** — 사용자가 Editor Play로 다음을 청취·시각 확인:
   - drum 토글 ON 상태에서 nabia 세션 진입 시 드럼 킥/스네어 소리 청취(AC 4 재현).
   - drum grab → nabia 곡 행이 활성색으로 표시되고 클릭 가능(AC 6 재현).
   - drum grab + Nabia + `Hard` 버튼 클릭 시 piano 토글 1개 ON 표시(AC 7 재현).
   - 난이도 버튼 순서 `Easy → Normal → Hard`로 표시.

## Deliverables

- `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` — `instrumentId: DrumKit` → `drum`.
- `Assets/StreamingAssets/Songs/nabia-piano-{easy,normal,hard}.vmsong` × 3 — `*-{1,2,3}.vmsong`로 rename(.meta 포함).
- `Assets/StreamingAssets/Songs/nabia-drum-{easy,normal,hard}.vmsong` × 3 — 동일 rename.
- `Assets/StreamingAssets/Songs/test_song-piano-easy.vmsong`, `test_song-drum-easy.vmsong` — `*-1.vmsong`로 rename(.meta 포함).
- `Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs` — `_diffPriority` 숫자화, `GetDifficultiesFor` 정렬 반환.
- `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` — 난이도 버튼 라벨 표시 매핑(`"1"→"Easy"` 등) 적용. 내부 키는 `"1"/"2"/"3"` 유지.
- `Assets/SessionPanel/Scripts/DifficultyButtonUI.cs` — `SetLabel(string text)` 공개 헬퍼 추가(콜백 식별자와 라벨 분리).
- `Assets/RhythmGame/Tests/Editor/VmSongParserTests.cs` — 파일명 참조 6+1건 업데이트.

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/StreamingAssets/Songs/` 폴더에 새 8건(`nabia-piano-1.vmsong`, `nabia-piano-2.vmsong`, `nabia-piano-3.vmsong`, `nabia-drum-1.vmsong`, `nabia-drum-2.vmsong`, `nabia-drum-3.vmsong`, `test_song-piano-1.vmsong`, `test_song-drum-1.vmsong`)이 존재하고, 구 8건(`*-easy.vmsong`, `*-normal.vmsong`, `*-hard.vmsong`)은 0건이다.
  **검증:** `Glob Assets/StreamingAssets/Songs/*-1.vmsong` 결과 ≥ 4건(nabia 2 + test_song 2), `Glob Assets/StreamingAssets/Songs/*-2.vmsong` 결과 ≥ 2건, `Glob Assets/StreamingAssets/Songs/*-3.vmsong` 결과 ≥ 2건, `Glob Assets/StreamingAssets/Songs/*-easy.vmsong` 결과 0건.
- [ ] `[auto-hard]` `DrumKit.prefab`의 `instrumentId` 직렬화 값이 `drum`이다.
  **검증:** `Grep -n "instrumentId:" Assets/Instruments/Drum/Prefabs/DrumKit.prefab` 결과 `instrumentId: drum` 1줄이고 `DrumKit`이 없다.
- [ ] `[auto-hard]` `FolderScanSongCatalog.cs`의 `_diffPriority`가 `"1"/"2"/"3"` 3원소 배열이고, `MultiFileSongEntry.GetDifficultiesFor()`가 `_sortedDiffsByInstrument` 캐시 또는 정렬 결과를 반환한다.
  **검증:** `Grep -n "_diffPriority" Assets/SessionPanel/Scripts/FolderScanSongCatalog.cs` 결과에 `"1"`, `"2"`, `"3"` 리터럴이 보이고, `GetDifficultiesFor` 본문에 `_diffPriority` 또는 사전 정렬 캐시 참조가 보인다.
- [ ] `[auto-hard]` `VmSongParserTests.cs`의 `LoadSong("nabia-*-easy.vmsong")` 등 8건 호출이 모두 신규 숫자 파일명으로 교체됐다.
  **검증:** `Grep -n "LoadSong\\(\"nabia-" Assets/RhythmGame/Tests/Editor/VmSongParserTests.cs` 결과 `LoadSong("nabia-piano-1.vmsong")`, `LoadSong("nabia-piano-2.vmsong")` … 형태로만 일치하고 `easy/normal/hard` 리터럴이 LoadSong 인자 자리에 없다.
- [ ] `[auto-hard]` EditMode 테스트 스위트가 컴파일 후 errors 0이고, `FolderScanSongCatalogTests` / `VmSongParserTests` / `RhythmGameSectionRefreshTests` / `RhythmGameSectionInstrumentToggleTests`가 모두 통과한다.
  **검증:** `unity-test-runner` sub-agent 호출 결과 EditMode 전체 pass. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 본 AC를 `pass(skip)`로 처리하고 Notes에 사유 기록.
- [ ] `[manual-hard]` (선행 AC 4 재검증) 피아노 grab → Nabia 선택 → 난이도 선택(Easy로 표시되는 첫 버튼) → drum 토글 ON 유지 → Play. 8마디 동안 드럼 킥/스네어 소리가 들린다. 동시에 피아노 노트는 시각적으로 내려오고 채점된다.
  **검증:** Editor Play → 위 시퀀스 청취/시각 확인. `read_console`에서 `[RhythmAccompaniment]` 발화 로그가 channel 10에 대해 출현하거나, 오디오 출력에 드럼 트랙이 들린다.
- [ ] `[manual-hard]` (선행 AC 6 재검증) 드럼 grab 상태에서 SessionPanel을 열면 곡 목록의 `Nabia` 행이 *활성색*(`Color(0.22, 0.28, 0.48, 1)`)으로 표시되고, 클릭하면 difficulty 영역이 열린다.
  **검증:** Editor Play → 드럼 컨트롤러 grab → SessionPanel 오픈 → Nabia 행 시각 + 클릭 동작 확인.
- [ ] `[manual-hard]` (선행 AC 7 재검증) 드럼 grab + Nabia 선택 + 난이도 "Hard" 버튼 클릭 → 반주 토글 영역에 `piano` 라벨 토글이 1개, ON 상태로 표시된다.
  **검증:** Editor Play → 위 시퀀스 → Instrument Toggle Container 자식 수 = 1, 라벨 = `piano`, isOn = true 시각 확인.
- [ ] `[manual-hard]` (추가 결함 재검증) 곡 선택 후 난이도 버튼이 위→아래 또는 좌→우 순서로 `Easy`, `Normal`, `Hard`로 표시되며, 어떤 곡·악기 조합에서도 순서가 유지된다.
  **검증:** Editor Play → Nabia 선택 → difficulty container 자식 0/1/2번 버튼 라벨 = `Easy`/`Normal`/`Hard` 시각 확인 (피아노·드럼 양쪽).
- [ ] `[manual-hard]` (회귀 점검) test_song-piano(=`test_song-piano-1.vmsong`)와 test_song-drum(=`test_song-drum-1.vmsong`)도 곡 목록에 표시되고 선택 시 단일 난이도 `Easy`로 표시된다.
  **검증:** Editor Play → 피아노/드럼 각각 grab → test_song 행 활성 → 난이도 1개 `Easy`로 표시 확인. 본 항목은 09 spec Out of Scope("test_song 회귀")의 보조 점검이며, 실패해도 별도 plan으로 분리해 본 plan은 통과 가능.
- [ ] `[manual-hard]` (선행 plan 재검증) [`2026-05-18-linksky0311-nabia-playback-verification.md`](./2026-05-18-linksky0311-nabia-playback-verification.md)의 실패 AC 3건(items 4 / 6 / 7) 가 이 plan 적용 후 재검증에서 통과한다.
  **검증:** 위 manual-hard AC 4·6·7 재검증 항목이 모두 pass.

## Out of Scope

- `Piano.prefab`의 `instrumentId: Piano` → `piano` 정규화. 현재 OrdinalIgnoreCase 비교 덕에 동작하지만 가독성 측면에서 후속 정리 가치는 있음. 본 plan은 검증 실패 원인이 아니므로 미루어둠.
- 난이도 표시 i18n(영문 Easy/Normal/Hard 외 한글/일본어 등). 현 UI는 영문 라벨로 통일.
- vmsong 파일 내부 `[Meta]` 섹션에 `difficulty=1/2/3` 같은 메타 필드 추가. 본 plan은 파일명 segment만 사용하므로 메타 변경 없음.
- DrumKit InstanceVolume PlayerPrefs 마이그레이션. 키 변경(`DrumKit` → `drum`)으로 기존 사용자의 드럼 볼륨이 디폴트로 리셋되지만, 본 프로젝트는 학부 캡스톤 단계이며 영속 사용자 데이터가 없으므로 마이그레이션 없이 진행.
- 09 sub-spec의 Behavior E(드럼 + Hard + piano 토글 ON 플레이백) 신규 검증. 선행 plan AC 8과 동일 시나리오로 이미 통과했으므로 본 plan에서는 재검증 대상에서 제외.
- 자동화 발화 로그 후크(Note 카운트/채널 발화 카운트). 09 Notes의 후속 plan 후보로 그대로 둠.

## Notes

- DrumKit instrumentId 변경의 부작용으로 `InstanceVolumeStore` PlayerPrefs 키가 `DrumKit` → `drum`으로 바뀌어 기존 사용자의 드럼 볼륨 설정이 초기값으로 리셋된다. 학부 캡스톤 단계라 허용 가능한 부작용으로 박제. 사용자가 명시적으로 마이그레이션을 요구하면 별도 plan으로 분리.
- `DifficultyButtonUI.SetLabel`은 라벨 표시를 콜백 식별자에서 분리하기 위해 새로 추가하는 작은 표면이다. 기존 `Setup`의 `difficulty` 매개변수는 *식별자 + 라벨 겸용*에서 *식별자 전용*으로 의미가 미세하게 좁아진다 — 호출자(`RhythmGameSectionController`)만 영향을 받으므로 안전.
- `MultiFileSongEntry._sortedDiffsByInstrument` 캐시 도입 시 thread-safety는 고려하지 않는다(Awake 단계에서 1회 빌드, 이후 read-only).
- 선행 plan의 Notes "(c) 토글 OFF인데 드럼이 들림" 회귀는 본 plan과 별개의 시나리오이므로 추가 점검 없이 둔다. 본 plan은 "토글 ON인데 들리지 않음" 쪽 수정.

## Handoff

적용 완료(2026-05-21). (a) DrumKit.prefab `instrumentId: drum` — vmsong `instrument=drum`과 OrdinalIgnoreCase 매칭. (b) vmsong 파일명 컨벤션 확정: `<song>-<instrument>-<1|2|3>.vmsong` (`1=Easy, 2=Normal, 3=Hard`). (c) UI 라벨 매핑: `RhythmGameSectionController.MapDifficultyLabel("1"→"Easy", "2"→"Normal", "3"→"Hard")`, `DifficultyButtonUI.SetLabel`로 식별자·표시 분리. 재검증 통과: drum 반주 소리 들림(AC 4), drum grab 시 Nabia 활성색(AC 6), Hard 선택 시 piano 토글 표시(AC 7), 난이도 버튼 순서 Easy→Normal→Hard. EditMode 133/133 PASS.
