# 나비야 6개 VMSong + drumkit instrumentKey 정합화 + 파서 테스트 6건 추가

**Linked Spec:** [`08-nabia-sheet-music.md`](../specs/08-nabia-sheet-music.md)
**Status:** `Done`

## Goal

`Assets/StreamingAssets/Songs/`에 한국 동요 '나비야'(C장조 4/4 120BPM 8마디)
피아노 3난이도·드럼 3난이도 총 6개 `.vmsong` 파일을 추가하고, 기존 드럼 차트의
파일명(`test_song-drumkit-easy.vmsong`)·내부 `instrument=DrumKit` 표기를
`test_song-drum-easy.vmsong` / `instrument=drum`으로 통일한다. 6개 신규 파일
파싱 성공을 보장하는 NUnit 테스트 케이스를 `VmSongParserTests.cs`에 추가한다.

## Context

- 리듬게임 모드는 `VMSong` 텍스트 차트가 단일 진실원이며 별도 오디오 파일은 없다.
  현재 카탈로그에는 `test_song-piano-easy`·`test_song-drumkit-easy` 두 차트만 있고
  음악적 완결성이 부족해 사용자 체감용 곡이 부재하다.
- sub-spec 08 What은 "`test_song-drum-easy.vmsong`의 파일명 instrument 키(`drum`)와
  파일 내부 `instrument` 값(`DrumKit`) 불일치 수정"을 명시. 실제 파일을 검증한 결과
  **파일명도 `drumkit`** 이라 *목표 상태*가 되려면 파일 rename + 내부 값 변경을
  동시에 수행해야 한다. sub-spec Behavior의 세 번째 시나리오("test_song-drum-easy.vmsong
  파싱 시 instrumentKey == drum")가 통과하려면 파일명이 `drum`이어야 한다.
- `VmSongParser`는 `instrument` 값을 **string 그대로 보존**한다 — case-sensitive
  비교를 어디서 하는지는 본 plan의 책임이 아니지만, sub-spec이 "값이 `drum`(대소문자 무관)"
  로 박제하므로 본 plan은 표기상 모두 소문자 `drum`을 채택한다.
- 8마디 곡 구조(파라미터화 정확값): `ticksPerQuarter=480`, `120 BPM`, 4/4박자,
  1박=480 ticks, 1마디=1920 ticks, 총 8마디=15360 ticks.
- 드럼 채널 = 10 (`RhythmChannels.DrumChannel`), 피아노 채널 = 1. 드럼은 동시 타격
  제약을 난이도별로 차등 적용 — Easy ≤1, Normal·Hard ≤2.

## Verified Structural Assumptions

- 기존 파일 `Assets/StreamingAssets/Songs/test_song-drumkit-easy.vmsong` 내부:
  `[Channels] channel=10  instrument=DrumKit` / `[Track:10]` 사용 — `Read Assets/StreamingAssets/Songs/test_song-drumkit-easy.vmsong (2026-05-18)`
- 동반 `.meta` 파일 `Assets/StreamingAssets/Songs/test_song-drumkit-easy.vmsong.meta`
  존재. Unity는 `.vmsong`을 TextAsset으로 import — rename 시 `.meta`도 함께 rename
  필요(GUID 보존). `Glob Assets/StreamingAssets/Songs/*.vmsong.meta (2026-05-18)`
- `RhythmChannels` 정의: `DrumChannel=10`, `MinChannel=1`, `MaxChannel=16`. 1..16
  범위 외 channel은 파서가 reject — `Read Assets/RhythmGame/Scripts/Data/RhythmChannels.cs (2026-05-18)`
- `VmSongParser`는 `[Channels]` 항목의 `instrument` 값을 변환·정규화 없이
  `ChannelInstrumentMap.Entry.instrumentKey`에 string 그대로 저장. 따라서 `drum` /
  `DrumKit` / `drumkit`은 **다른 키로 취급된다** (파서 자체는 동일성 검사를 안 함). — `Read Assets/RhythmGame/Scripts/Data/Parsing/VmSongParser.cs (2026-05-18)` (HandleChannel: lines 268-287)
- `VmSongParser`는 노트 `tick`이 동일할 때 입력 순서를 보존하는 stable insertion
  sort를 적용 — 동시 노트는 입력 순서 그대로 보존. (음높이 정렬 가정 금지.) — `Read Assets/RhythmGame/Scripts/Data/Parsing/VmSongParser.cs (2026-05-18)` (StableInsertionSort)
- `Assets/RhythmGame/Tests/Editor/RhythmGame.Tests.Editor.asmdef`는
  `RhythmGame.Data` 참조를 이미 보유 — `VmSongParser`·`RhythmChannels` 호출 가능.
  새 테스트 케이스는 asmdef 변경 없이 추가 가능. — `Read Assets/RhythmGame/Tests/Editor/RhythmGame.Tests.Editor.asmdef (2026-05-18)`
- `VmSongParserTests.cs`는 NUnit `[TestFixture]` 클래스 `VmSongParserTests` 하나에
  9개 테스트 메서드를 보유. 추가 테스트는 동일 클래스에 메서드 append 형태로 안전. — `Read Assets/RhythmGame/Tests/Editor/VmSongParserTests.cs (2026-05-18)`
- 기존 차트 작성 컨벤션: 키-값 사이 공백 자유(`tick=0  note=48  len=200  vel=90`),
  `#` 시작 라인·인라인 `#` 주석 모두 파서가 strip — `Read Assets/StreamingAssets/Songs/test_song-piano-easy.vmsong (2026-05-18)`

## Approach

### 음악 자료 사전 계산

**4마디 멜로디(곡 통일):**
| 마디 | 멜로디 (tick offset, MIDI, len) | 코드 |
|---|---|---|
| 1 / 5 | 0:G4(67)·480:E4(64)·960:E4(64)len960 | C |
| 2 / 6 | 0:F4(65)·480:D4(62)·960:D4(62)len960 | G7 |
| 3 | 0:C4(60)·480:D4(62)·960:E4(64)·1440:F4(65) | C |
| 4 | 0:G4(67)·480:G4(67)·960:G4(67)len960 | C(2박)+G7(2박) |
| 7 | 0:C4(60)·480:E4(64)·960:G4(67)·1440:G4(67) | C |
| 8 | 0:E4(64)·480:D4(62)·960:C4(60)len960 | C |

멜로디 노트 `len=440`(quarter)·`len=920`(half) — staccato 회피용 약간 짧게.
`vel=92` 통일.

**피아노 코드 구성:**
- Easy 코드 근음만(C코드=C4(60), G7코드=G4(67)) 각 박마다 quarter note `len=440 vel=70`
- Normal 코드 근음+5도 동시(C코드=C4(60)+G4(67), G7코드=G4(67)+D5(74)) 각 박마다 `len=440 vel=68`
- Hard 8분음표 아르페지오 3음 반복 상행
  - C코드 아르페지오 시퀀스: C4·E4·G4·C4·E4·G4·C4·E4 (8개 = 1마디 8분음표)
    → tick offset 0/240/480/720/960/1200/1440/1680, MIDI: 60·64·67·60·64·67·60·64, `len=220 vel=65`
  - G7코드 아르페지오: G4·B4·D5·G4·B4·D5·G4·B4
    → MIDI: 67·71·74·67·71·74·67·71, 같은 offset·len·vel
  - 4마디(C 2박+G7 2박): 0/240/480/720은 C 아르페지오(C4·E4·G4·C4), 960/1200/1440/1680은 G7 아르페지오(G4·B4·D5·G4)

**드럼(channel=10):**
MIDI 매핑: 36=킥(Kick), 38=스네어(Snare), 42=Closed HiHat, 43=Floor Tom, 45=Mid Tom
- Easy: 1박 킥, 2박 스네어, 3박 킥, 4박 스네어 (1마디당 4개)
  → tick offset 0:36·480:38·960:36·1440:38, `len=60 vel=100`(킥)/`vel=90`(스네어). 동시 ≤1.
- Normal: 8분음표 HiHat(42) 8개 + 비트마다 킥(beat 1,3) 또는 스네어(beat 2,4)
  → HiHat tick offset 0/240/480/720/960/1200/1440/1680, `len=60 vel=70`
  → Kick/Snare beat tick offset 0:36·480:38·960:36·1440:38, `len=60 vel=100/90`
  → 동시 노트 검증: HiHat과 Kick/Snare는 0/480/960/1440 동시 (HH+K, HH+S, HH+K, HH+S = 2개씩). 240/720/1200/1680은 HH만. **최대 2.**
- Hard: 16분음표 HiHat 16개 + 비트마다 킥/스네어 + 마지막 2마디(7·8) 톰 필인
  - 1~6마디: HiHat 16분(tick 0,120,240,...,1800 총 16개) `vel=65 len=60` + 비트 K/S(0/480/960/1440) `vel=100/90 len=60`
    → 동시 검증: 0/480/960/1440에서 HH+K 또는 HH+S = 2개. 그 외 12 tick은 HH 단독. **최대 2.**
  - 7마디(Hi-Hat 8분으로 축소 + Tom fill 4박째에 시작):
    HiHat 8분 8개(tick 0,240,480,...,1680) + K/S beat 4개(0:36·480:38·960:36·1440:38) + 4박째 Floor Tom 16분음표 4개(1440/1560/1680/1800 note=43 vel=85 len=60)
    → 동시 1440에서 HH+S+FT = 3 위반! → **수정안**: 7마디 4박째 K/S 비트 생략, Floor Tom으로 대체. 즉 0:36·480:38·960:36 (3개만) + HH 8개 + FT 4개(1440/1560/1680/1800). 1440 = HH+FT (2). 1560/1680/1800 = HH+FT(only at 1680) — tick 1560/1800은 HH 없음 → FT 단독. 1680은 HH+FT(2). **최대 2.**
  - 8마디(전부 Tom 필인 마무리):
    Mid Tom 4개(0:45·240:45·480:45·720:45) → Floor Tom 4개(960:43·1200:43·1440:43·1680:43) 각 `vel=90 len=60`. HiHat 8분 8개(tick 0,240,...,1680). K beat 1박만(0:36 vel=100).
    → 동시: tick 0 = HH+MT+K = 3 위반! → **수정안**: 8마디 K 비트 제거. tick 0 = HH+MT(2), 240=HH+MT, 480=HH+MT, 720=HH+MT, 960=HH+FT, 1200=HH+FT, 1440=HH+FT, 1680=HH+FT. **최대 2.**
  - 7마디 K/S 4박째 1440 충돌 재확인: 위 수정 후 1440=HH+FT = 2 OK.

### 차트 파일 작성 (StreamingAssets/Songs)

각 파일 헤더 공통:
```
[Meta]
title  = Nabia
songid = nabia_001

[Resolution]
ticksPerQuarter = 480

[Tempo]
tick=0  bpm=120  beats=4  beatUnit=4

[Channels]
channel=<N>  instrument=<key>

[Track:<N>]
# 8마디 (1마디=1920 ticks, 총 0~15359)
```
- 피아노 파일 3개: `channel=1 instrument=piano`, `[Track:1]`.
- 드럼 파일 3개: `channel=10 instrument=drum`, `[Track:10]`.
- songid는 모두 `nabia_001` 동일(곡 통일성).

각 마디 시작 tick = `(마디번호-1) * 1920`. 각 음표 tick = 마디시작 + 박offset.

**파일 6종 절대 경로(생성):**
1. `Assets/StreamingAssets/Songs/nabia-piano-easy.vmsong`
2. `Assets/StreamingAssets/Songs/nabia-piano-normal.vmsong`
3. `Assets/StreamingAssets/Songs/nabia-piano-hard.vmsong`
4. `Assets/StreamingAssets/Songs/nabia-drum-easy.vmsong`
5. `Assets/StreamingAssets/Songs/nabia-drum-normal.vmsong`
6. `Assets/StreamingAssets/Songs/nabia-drum-hard.vmsong`

Unity 에디터가 자동으로 `.meta`를 생성하므로 작성자가 별도 생성 불필요(첫 import 시 GUID 발급).

### 기존 드럼 파일 정합화

1. `Assets/StreamingAssets/Songs/test_song-drumkit-easy.vmsong` 파일 *rename* →
   `Assets/StreamingAssets/Songs/test_song-drum-easy.vmsong`.
2. 동반 `.meta` 파일도 같은 prefix로 rename (GUID 보존).
3. rename 후 내부 텍스트 `instrument=DrumKit` → `instrument=drum` Edit.
4. `[Track:10]` 헤더와 노트 라인은 그대로 유지(채널 10 동일).

> Unity asset rename은 Editor가 인덱스를 갱신할 수 있도록 `manage_asset`로 수행이
> 권장되지만, `StreamingAssets/` 폴더는 일반 file copy 대상이라 OS-level rename도
> 안전하다. plan 본문은 *결과*(파일명·내부값)만 박제하고 절차는 메인 세션이 선택.

### 테스트 추가

`Assets/RhythmGame/Tests/Editor/VmSongParserTests.cs`의 `VmSongParserTests`
클래스에 다음 NUnit 테스트 메서드 6개를 append:

1. `Nabia_PianoEasy_ParsesSuccessfully` — `File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Songs/nabia-piano-easy.vmsong"))` 로 읽어 `Parse()` 호출, `result.Success == true`, `tracks.Count == 1`, `tracks[0].channel == 1`, `notes.Count > 0`, `channelMap.entries[0].instrumentKey == "piano"`.
2. `Nabia_PianoNormal_ParsesSuccessfully` — 위와 동일 패턴, 파일명만 `nabia-piano-normal.vmsong`.
3. `Nabia_PianoHard_ParsesSuccessfully` — 동일 패턴, `nabia-piano-hard.vmsong`.
4. `Nabia_DrumEasy_ParsesSuccessfully` — `nabia-drum-easy.vmsong`, `tracks[0].channel == 10`, `channelMap.entries[0].instrumentKey == "drum"`.
5. `Nabia_DrumNormal_ParsesSuccessfully` — 동일 패턴 `nabia-drum-normal.vmsong`.
6. `Nabia_DrumHard_ParsesSuccessfully` — 동일 패턴 `nabia-drum-hard.vmsong` + 모든 tick에서 동시 노트 수 ≤ 2 검증 (Dictionary<int,int> count로 그룹핑).

Drum Hard 동시노트 검증 의사코드:
```csharp
var counts = new Dictionary<int,int>();
foreach (var n in result.chart.tracks[0].notes)
    counts[n.tick] = counts.GetValueOrDefault(n.tick) + 1;
foreach (var kv in counts)
    Assert.LessOrEqual(kv.Value, 2, $"tick={kv.Key} has {kv.Value} simultaneous notes");
```

추가 import: `using System.IO;` `using UnityEngine;` (위쪽 using 블록).
`Application.streamingAssetsPath`는 Editor에서도 동작. `RhythmGame.Data` 참조는
asmdef에 이미 존재.

### 기존 `test_song-drum-easy` 테스트(선택)

기존 테스트 3 (`DrumChannel10_PreservedInChannelMap`)은 인라인 chart 문자열을
사용하므로 파일 rename·내용 수정의 영향을 받지 않는다. 별도 테스트 케이스
`TestSongDrumEasy_InstrumentKeyIsDrum`을 추가해 파일에서 직접 읽고
`instrumentKey == "drum"` 확인. 이는 sub-spec Behavior 3번째 시나리오 검증과 직결.

## Deliverables

- `Assets/StreamingAssets/Songs/nabia-piano-easy.vmsong` — 피아노 Easy 차트(생성)
- `Assets/StreamingAssets/Songs/nabia-piano-normal.vmsong` — 피아노 Normal 차트(생성)
- `Assets/StreamingAssets/Songs/nabia-piano-hard.vmsong` — 피아노 Hard 차트(생성)
- `Assets/StreamingAssets/Songs/nabia-drum-easy.vmsong` — 드럼 Easy 차트(생성)
- `Assets/StreamingAssets/Songs/nabia-drum-normal.vmsong` — 드럼 Normal 차트(생성)
- `Assets/StreamingAssets/Songs/nabia-drum-hard.vmsong` — 드럼 Hard 차트(생성)
- `Assets/StreamingAssets/Songs/test_song-drum-easy.vmsong` — 기존 `test_song-drumkit-easy.vmsong` 파일 rename + 내부 `instrument=DrumKit` → `instrument=drum`
- `Assets/StreamingAssets/Songs/test_song-drum-easy.vmsong.meta` — 동반 meta rename(GUID 보존)
- `Assets/RhythmGame/Tests/Editor/VmSongParserTests.cs` — 6개 nabia 파싱 테스트 + 1개 test_song-drum-easy instrumentKey 테스트 append

## Acceptance Criteria

- [ ] `[auto-hard]` 6개 `nabia-*.vmsong` 파일이 모두 `Assets/StreamingAssets/Songs/`에 존재한다.
  **검증:** `Glob Assets/StreamingAssets/Songs/nabia-*.vmsong` 결과 정확히 6건 (piano-easy, piano-normal, piano-hard, drum-easy, drum-normal, drum-hard).
- [ ] `[auto-hard]` 6개 nabia 파일 각각이 `VmSongParser.Parse()`에서 `result.Success == true`를 반환한다 (errors 0건).
  **검증:** Unity Test Runner에서 `Nabia_PianoEasy_ParsesSuccessfully` 외 5개 NUnit 테스트가 모두 PASS.
- [ ] `[auto-hard]` 피아노 3파일은 `channelMap.entries[0].instrumentKey == "piano"`, `tracks[0].channel == 1`. 드럼 3파일은 `instrumentKey == "drum"`, `tracks[0].channel == 10`.
  **검증:** 위 6개 NUnit 테스트 안 `Assert.AreEqual`.
- [ ] `[auto-hard]` `nabia-drum-hard.vmsong`의 모든 tick에서 동시 노트 수 ≤ 2.
  **검증:** `Nabia_DrumHard_SimultaneousNotesCappedAtTwo` NUnit 테스트 (tick별 카운트 Dictionary 그룹핑).
- [ ] `[auto-hard]` 기존 드럼 파일이 `Assets/StreamingAssets/Songs/test_song-drum-easy.vmsong` 경로로 존재하고 `test_song-drumkit-easy.vmsong`은 존재하지 않는다.
  **검증:** `Glob Assets/StreamingAssets/Songs/test_song-*.vmsong` → `test_song-drum-easy.vmsong` 존재, `test_song-drumkit-easy.vmsong` 부재.
- [ ] `[auto-hard]` `test_song-drum-easy.vmsong`을 파서에 통과시키면 `channelMap.entries[0].instrumentKey == "drum"`.
  **검증:** 신규 NUnit 테스트 `TestSongDrumEasy_InstrumentKeyIsDrum` PASS.
- [ ] `[auto-hard]` 모든 nabia 피아노 파일은 8마디 총 15360 ticks 안쪽에 노트가 위치한다(`max(tick + durationTicks) ≤ 15360`).
  **검증:** 각 nabia piano 테스트 안 노트 리스트 순회 + `Assert.LessOrEqual`. (Hard는 마지막 8분음표 tick=1680+220=1900 → 마지막 마디 0-base 14*1920=음표 tick 14*1920+1680 = 28560 → 8마디 곡이므로 마지막 마디는 7번째 0-base=마디번호 8 = tick 7*1920~8*1920-1. 따라서 max tick+len = 7*1920+1680+220 = 15340 ≤ 15360 OK.)
- [ ] `[auto-hard]` 컴파일 에러·경고가 새로 발생하지 않는다 (VmSongParserTests.cs 수정 후).
  **검증:** Unity MCP `read_console` 호출 결과 Error 0건, 새 Warning 0건. (메인 세션이 `unity-mcp-workflow` skill 절차로 컴파일 대기 후 확인.)
- [ ] `[auto-soft]` 기존 9개 파서 테스트(`MinimalChart_ParsesAllSections` 외)가 회귀 없이 PASS.
  **검증:** Unity Test Runner 전체 NUnit Editor 테스트 실행 결과.
- [ ] `[manual-hard]` Unity Editor에서 6개 신규 `.vmsong` 파일을 Project 창에 선택하면 Inspector에 TextAsset preview로 차트 내용이 정상 표시된다(깨진 문자·BOM·CRLF 이슈 없음).
  **검증:** Editor에서 6개 파일을 순차 선택해 Inspector preview 확인, 한 줄도 깨지지 않음.

## Out of Scope

- 곡 카탈로그(`RhythmSongDatabase`) 수동 등록 — 06 sub-spec(`multi-file-song-catalog`) 책임. 본 plan은 파일만 배치.
- 나비야 음악적 편곡 품질 검수 — sub-spec Out of Scope에 명시.
- `test_song-piano-easy.vmsong` 변경 — sub-spec Out of Scope("test_song 관련 다른 파일 변경").
- 다른 악기(트럼펫·기타 등) 차트.
- 09 sub-spec(`nabia-playback-verification`)이 다룰 실제 씬 로드·재생 검증.

## Notes

- 2026-05-18: [manual-hard PASS] Unity Editor Inspector에서 6개 nabia .vmsong TextAsset preview 정상 표시 확인.
- sub-spec 본문은 기존 드럼 파일명을 `test_song-drum-easy.vmsong`로 *전제*하지만
  실제 파일은 `test_song-drumkit-easy.vmsong`이다. 본 plan은 파일 rename으로
  목표 상태를 만족시킨다. 09 sub-spec 또는 다른 plan이 이 파일명을 hard-code로
  참조하지 않는지 메인 세션이 추가 확인 필요(검색: `test_song-drumkit-easy`).
- 드럼 노트 매핑(36/38/42/43/45)은 General MIDI 드럼 표준 + 본 프로젝트 기존
  파일 답습. `DrumNoteDisplayAdapter.cs`가 어떤 MIDI를 어떤 lane으로 매핑하는지는
  09 plan에서 검증.
- `.meta` rename은 GUID 보존을 위해 *반드시* 함께 수행. GUID 분실 시 카탈로그가
  곡을 재인덱스해야 한다. Unity Editor 안에서 rename하면 자동, OS-level rename은
  수동.
- 드럼 Hard 4박째 동시 노트 충돌 회피를 위해 7마디 4박 K/S와 8마디 1박 K를
  제거한 점은 *plan 차원의 음악적 의사결정*. 사용자가 보기에 어색하면 후속 plan으로
  미세 조정.

## Handoff

<!-- /spec-build의 doc-updater가 plan Done 시 자동 갱신 -->
- 6개 nabia .vmsong 파일(piano 3 + drum 3) 생성 완료. test_song-drumkit-easy.vmsong → test_song-drum-easy.vmsong rename + instrument=drum 수정 완료.
- VmSongParserTests.cs에 7개 NUnit 테스트 추가 (nabia 6건 + TestSongDrumEasy_InstrumentKeyIsDrum 1건).
- EditMode 118/118 pass, PlayMode 4/4 pass. Inspector TextAsset preview 6개 정상 확인.
- 드럼 Hard bar7·8 동시 노트 ≤2 제약 충족 (4박째 K/S 제거, Tom fill 대체).
