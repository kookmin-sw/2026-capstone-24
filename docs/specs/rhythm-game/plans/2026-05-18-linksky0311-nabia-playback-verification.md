# 나비야 플레이백 & 반주 연동 — Unity 씬 검증

**Linked Spec:** [`09-nabia-playback-verification.md`](../specs/09-nabia-playback-verification.md)
**Status:** `Ready`

## Goal

08에서 제작·파서 검증이 끝난 nabia 6종 차트(`piano × {easy, normal, hard}` + `drum × {easy, normal, hard}`)가 Unity 씬에서 사용자 흐름(곡 목록 표시 → 난이도 선택 → 반주 토글 → 세션 실행) 전체에 걸쳐 의도대로 동작하는지 코드 변경 없이 실측 검증한다. 본 plan은 검증 전용으로 신규 코드/자산을 추가하지 않는다.

## Context

- sub-spec 08에서 6개 `.vmsong` 파일이 모두 정상 파싱되며 instrumentKey/channel/instrument 메타가 일관됨이 EditMode 테스트(118/118 pass)로 입증됐다.
- 그러나 파서 단위 검증은 다음을 보장하지 못한다:
  1. `FolderScanSongCatalog`가 nabia를 한 곡 엔트리로 그룹핑해 `ISongCatalog.Songs`에 노출하는지
  2. `RhythmGameSectionController.BuildInstrumentToggles()`가 선택된 난이도와 동일 파일 기준으로 반대 악기 토글만 노출하는지
  3. `BuildAccompanimentDictFromChart()` → `RhythmGameHost.StartSession()` 경로에서 토글 OFF가 실제 발화 차단으로 이어지는지
  4. nabia 피아노 노트가 NoteDisplay에서 시각적으로 내려오고 판정 가능한지
- 09의 모든 Behavior는 사용자가 헤드셋 또는 Editor Play 모드에서 직접 조작·청취로만 검증 가능한 manual-hard 케이스다. plan의 책임은 각 Behavior를 재현 가능한 시나리오로 명문화하고, 실패 시 어디를 의심해야 할지 단서를 남기는 것이다.
- 코드 동선 박제(`RhythmGameSectionController.cs` Read 결과):
  - `OnDifficultyClicked(difficulty, sender)` → `LoadChart()` → `BuildInstrumentToggles()` 순서로 호출 (lines 200~209).
  - `BuildInstrumentToggles()`는 `_selectedSong.SupportedInstrumentIds`를 순회하며 자기 자신(현재 잡은 악기)은 제외하고, 각 후보 악기에 대해 `_selectedDifficulty`와 매칭되는 차트가 있는 경우에만 토글을 추가 (lines 244~293). 따라서 난이도가 바뀌면 토글 컨테이너가 재구성됨.
  - 토글 기본값은 ON(`_instrumentToggleStates[instrumentId] = true`, `defaultOn: true`로 Setup 호출, lines 283·292).
  - `OnPlayButtonClicked()`는 `BuildAccompanimentDictFromChart(judgedChannel, merged)`로 `accompaniment` 사전을 만들어 `host.StartSession(merged, rhythmSong, judgedChannel, accompaniment)` 호출 (lines 492~494). 사전 키는 채널 정수, 값은 토글 상태.

## Verified Structural Assumptions

- `Assets/StreamingAssets/Songs/` 폴더에 nabia 6종 차트 + test_song 2종이 모두 존재 — `Glob Assets/StreamingAssets/Songs/*.vmsong (2026-05-18)`.
- `nabia-drum-easy.vmsong`은 `channel=10 instrument=drum`(`Track:10`만 보유), `nabia-piano-hard.vmsong`은 `channel=1 instrument=piano`(`Track:1`만 보유) — 즉 각 파일은 단일 채널/단일 악기. 따라서 piano를 잡고 nabia 선택 시 후보 악기는 `drum` 1종, 반대도 마찬가지로 `piano` 1종 — `Read Assets/StreamingAssets/Songs/nabia-drum-easy.vmsong (2026-05-18)`, `Read Assets/StreamingAssets/Songs/nabia-piano-hard.vmsong (2026-05-18)`.
- `RhythmGameSectionController.BuildInstrumentToggles()`는 `_selectedDifficulty`를 키로 `_selectedSong.GetDifficultiesFor(otherId)`를 호출해 매칭되는 난이도가 있을 때만 토글을 만든다. nabia는 두 악기 모두 easy/normal/hard 3종을 보유하므로 어느 난이도를 골라도 반대 악기 토글이 정확히 1개 노출되어야 한다 — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18) lines 244~293`.
- `_instrumentToggleStates`의 키는 `instrumentKey`(파일의 `instrument=` 값) 그대로 사용된다. 08에서 instrumentKey 버그 수정 후 piano/drum 두 키가 일관되게 사용됨 — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18) lines 281~298, 309, 324`.
- 본 plan은 Unity 자산을 *수정*하지 않는다. 검증 대상 씬은 `Assets/Scenes/SampleScene.unity` 또는 `Assets/Scenes/TestSceneSanyo.unity` 중 SessionPanel + RhythmGameHost가 이미 wired 되어 있는 씬을 사용. 사전 점검에서 wiring이 깨져 있으면 `unresolved`로 보고 후 중단.

## Approach

본 plan은 코드 변경이 없으므로 Approach는 "검증 절차"와 동일하다.

1. **사전 점검** — Unity Editor에서 SampleScene 또는 TestSceneSanyo를 로드. Hierarchy에서 SessionPanel 안 `RhythmGameSectionController`의 `Active Instrument Provider` / `Song Catalog` / `Instrument Toggle Container` / `Instrument Toggle Button Prefab` 인스펙터 필드가 모두 채워져 있는지 확인. 비어 있으면 검증 불가 → `unresolved`로 보고.
2. **Play 진입** — Editor Play 모드 또는 헤드셋 빌드로 진입.
3. **Behavior A 시나리오 (piano + nabia + Easy)** — 피아노 컨트롤러를 잡은 상태(=ActiveInstrument = piano)로 SessionPanel을 연다. 곡 목록에서 `Nabia` 행이 *활성색*으로 표시되는지 확인 후 클릭. 난이도 영역에 `easy/normal/hard` 3개 버튼이 뜨고 첫 버튼이 자동 선택됨. `Easy`를 클릭. 반주 토글 영역에 `drum` 토글 버튼이 1개, ON 상태로 표시되는지 확인.
4. **Behavior B 시나리오 (Easy + drum OFF)** — Behavior A 상태에서 drum 토글을 클릭해 OFF로 전환. Play 버튼을 눌러 세션 진입. 8마디(=16초 @120 BPM) 동안 피아노 멜로디 노트가 시각적으로 내려오고 사용자가 친 노트가 판정되는지 확인. **드럼(킥/스네어) 소리가 나지 않아야 한다** — Console에서 드럼 발화 로그가 없거나 오디오 출력에 드럼 트랙 부재.
5. **Behavior C 시나리오 (piano + nabia + Normal)** — 세션 종료 또는 Stop → Play 재진입. 동일 경로로 Nabia 선택 후 `Normal`을 클릭. 반주 토글 영역이 *재구성*되어 `drum` 토글이 1개 다시 표시(기본 ON). Easy → Normal 전환 시 토글 컨테이너가 1회 비워졌다가 채워지는 시각 동작 관찰.
6. **Behavior D 시나리오 (drum + nabia + Hard)** — 드럼 컨트롤러를 잡아 ActiveInstrument를 drum으로 전환. 곡 목록에서 Nabia가 여전히 활성색. 클릭 후 `Hard` 선택. 반주 토글 영역에 `piano` 토글 1개가 ON으로 표시됨.
7. **Behavior E 시나리오 (drum + Hard + piano ON 플레이백)** — Behavior D 상태에서 piano 토글을 ON 유지한 채 Play. 드럼 노트가 시각적으로 내려오고, 피아노 반주가 자동으로 들림. 사용자는 드럼 노트만 채점받음.
8. **회귀 점검 (선택)** — test_song-piano-easy / test_song-drum-easy도 동일 경로로 한 번씩 진입해 기존 곡이 깨지지 않았는지 확인.
9. **자동 가드 1건** — 6개 nabia 차트가 모두 streamingAssets에 존재하는지 Glob으로 사전 확인 (검증 환경 sanity check).

## Deliverables

- 없음 — 검증 전용 plan. 신규 또는 수정 코드/자산 없음. 본 plan 완료 시 `## Handoff`에 관찰 결과(예: 토글 OFF 시 발화 차단 동작 확인, 시각 노트 표시 정상 여부, 회귀 이슈 유무)를 1~2문단 기록한다.

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/StreamingAssets/Songs/` 폴더에 nabia 6종(`nabia-piano-easy.vmsong`, `nabia-piano-normal.vmsong`, `nabia-piano-hard.vmsong`, `nabia-drum-easy.vmsong`, `nabia-drum-normal.vmsong`, `nabia-drum-hard.vmsong`) 파일이 모두 존재한다.
  **검증:** `Glob Assets/StreamingAssets/Songs/nabia-*.vmsong` 결과 6건 정확 일치.
- [ ] `[manual-hard]` 피아노를 잡은 상태에서 SessionPanel을 열면 곡 목록에 `Nabia` 행이 *활성색*(`new Color(0.22f, 0.28f, 0.48f, 1f)`)으로 나타난다. 비활성 곡 행(반투명 어두운색)과 시각적으로 명확히 구분된다.
  **검증:** Editor Play → 피아노 컨트롤러 grab → SessionPanel 열기 → 곡 리스트 시각 확인. (대안: 곡 행 클릭이 정상 작동해 difficulty 버튼이 나타나면 supported=true로 판정된 것.)
- [ ] `[manual-hard]` 피아노 + Nabia + Easy를 선택하면 반주 토글 영역에 `drum` 라벨 토글 버튼이 정확히 1개, ON 상태로 표시된다.
  **검증:** Editor Play → 피아노 grab → Nabia 클릭 → Easy 버튼 클릭 → Instrument Toggle Container 자식 수 = 1, 라벨 텍스트 = `drum`, Toggle.isOn = true 시각 확인.
- [ ] `[manual-hard]` 위 상태에서 drum 토글을 OFF로 전환 후 Play 버튼을 누르면 세션이 시작되고, 8마디(약 16초 @120 BPM) 동안 피아노 멜로디 노트는 시각적으로 내려오고 판정되지만 드럼 킥/스네어 소리는 들리지 않는다.
  **검증:** Editor Play → 위 시퀀스 → drum 토글 클릭으로 OFF → Play 버튼 → 세션 진행 동안 청취 검증. Console 로그(`read_console`)에서 `[RhythmAccompaniment]` 등 발화 로그가 drum 채널(=10)에 대해 없거나 skip 게이트 동작 확인. 시각적으로는 NoteDisplay에 피아노 노트만 내려옴.
- [ ] `[manual-hard]` 동일 세션에서 drum 토글을 ON 유지한 채 다시 Play하면 드럼 킥/스네어 반주가 자동으로 들리고, 피아노 노트는 여전히 시각적으로 내려오며 채점된다.
  **검증:** Editor Play → 위와 동일 시퀀스 단 토글 OFF 단계 생략 → Play → 드럼 사운드 청취 확인 + 피아노 노트 판정 시각 확인.
- [ ] `[manual-hard]` 피아노 + Nabia 선택 후 Easy → Normal로 전환하면 Instrument Toggle Container의 기존 자식이 1회 destroy되고 `drum` 토글이 다시 1개 생성된다(기본 ON). Easy → Hard 전환도 동일.
  **검증:** Editor Play → Nabia + Easy → Normal 버튼 클릭 → Hierarchy에서 Instrument Toggle Container 자식 변화 관찰(또는 토글 시각 재구성 관찰). Easy에서 토글을 OFF로 바꿔둔 뒤 Normal로 전환했을 때 토글이 *ON으로 리셋*되는지(=재생성이므로 ON이어야 함) 추가 확인 — 만약 OFF 상태가 잔존하면 BuildInstrumentToggles의 `_instrumentToggleStates.Clear()` 동작에 회귀가 있는 것이므로 노트에 기록.
- [ ] `[manual-hard]` 드럼을 잡은 상태에서 Nabia + Hard를 선택하면 반주 토글 영역에 `piano` 라벨 토글이 1개, ON 상태로 표시된다.
  **검증:** Editor Play → 드럼 컨트롤러 grab → SessionPanel 열기 → Nabia 클릭 → Hard 클릭 → 토글 라벨 = `piano`, Toggle.isOn = true 시각 확인.
- [ ] `[manual-hard]` 드럼 + Nabia + Hard + piano 토글 ON 상태에서 Play하면 드럼 노트가 시각적으로 내려오며 채점되고, 동시에 피아노 멜로디·아르페지오 반주가 자동으로 들린다.
  **검증:** Editor Play → 위 시퀀스 → Play → 8마디 동안 드럼 노트 판정 + 피아노 멜로디 청취 동시 확인.

## Out of Scope

- 노트 판정 점수/등급 표시 검증 — `judgment` 및 결과 화면 spec 책임.
- BPM 슬라이더 ±5/±1 동작 검증 — sub-spec 07 책임. (단, Play 진입 시 baseBpm=120 그대로 유지된다는 가정만 사용.)
- 반주 음량/믹스 조절 — `session-panel/03-volume-section`.
- test_song 회귀 — Approach 8단계에 선택 점검으로만 두며 본 plan의 AC가 아니다.
- 시각 노트 색상·레이아웃 fidelity — `02-note-visual-fidelity` 및 `04-drum-note-display` 책임. 본 plan은 "노트가 내려온다"는 사실만 확인.
- Console에 에러/예외가 0건이라는 보장. (관찰되면 Notes에 기록하되 별도 plan 분리.)

## Notes

- 자동 가드 1건 외 모든 AC가 `[manual-hard]`인 이유: 09 sub-spec 자체가 "사용자 직접 청취·시각 확인" 검증을 목적으로 함. 자동화하려면 RhythmAccompaniment 발화 로그 후크 + NoteDisplay 인스턴스 카운트 후크를 도입해야 하나, 이는 09의 범위를 초과하므로 미루어 후속 plan 후보로 둔다.
- 검증 중 다음 회귀가 발견되면 별도 plan으로 파생: (a) 곡 행 활성색이 비활성과 구분 안 됨 — SongRowUI/RefreshSongList 회귀. (b) 난이도 전환 시 토글 컨테이너가 비어 있음 — BuildInstrumentToggles 회귀. (c) 토글 OFF인데 드럼이 들림 — BuildAccompanimentDictFromChart 또는 RhythmAccompaniment ShouldFire 게이트 회귀(07 Plan A 영역).
- Console 검증 보조: 세션 시작 직후 `read_console`로 `[RhythmGame]`, `[RhythmAccompaniment]` 태그 메시지를 캡처해 채널 발화 흐름을 텍스트로 박제하면 manual-hard 결과의 재현성을 높일 수 있다.
- 후속 plan: 본 plan의 manual-hard 검증에서 AC 4 / 6 / 7 + 난이도 버튼 순서 결함이 발견되어 [`2026-05-18-linksky0311-nabia-playback-fix.md`](./2026-05-18-linksky0311-nabia-playback-fix.md)로 파생됐다. 근본 원인은 (a) `DrumKit.prefab.instrumentId="DrumKit"` vs vmsong `instrument=drum` 키 불일치, (b) `MultiFileSongEntry.GetDifficultiesFor`가 파일 알파벳 순서 그대로 반환.

## Handoff

_미작성 — 검증 완료 후 doc-updater가 관찰 결과를 채운다._
