# RhythmGame 도메인 가이드

악기 입력을 차트와 비교해 PERFECT/GOOD/MISS 판정·표시·반주 재생까지 한 사이클로 묶는 도메인이다. **악기 측은 RhythmGame을 알지 않는다 — `InstrumentBase.MidiTriggered` 이벤트만 발행하면 자동 연결된다.**

## 1. asmdef 의존 트리

```
Instruments
    ▲
    │
RhythmGame.Data ◄─── RhythmGame.Runtime.Clock
    ▲                    ▲
    │                    │
RhythmGame.Runtime ──────┘
    ▲
    ├──── RhythmGame.Runtime.Dev   (Editor only, ChartAutoPlayer/AutoLauncher)
    └──── RhythmGame.Tests.Editor  (Editor only, 회귀 테스트)
```

- `Data` — 순수 데이터(SO·struct·파서). 의존: `Instruments`(`InstrumentLaneConfig`)만
- `Runtime.Clock` — `IRhythmClock` + `ITimeProvider`. `Data`만 의존, 다른 Runtime을 끌어들이지 않는다
- `Runtime` — 메인 세션/판정/디스플레이/반주. `Data`+`Runtime.Clock`+`Instruments`
- `Runtime.Dev` — Editor 전용 입력 자동화. 프로덕션 빌드 제외
- `Tests.Editor` — 위 4개 모두 의존

> 의존을 추가할 때는 위 방향을 거꾸로 그리지 말 것. `Data`가 `Runtime`을 알게 되면 Clock 분리의 의미가 사라진다.

## 2. 세션 spine

| 위치 | 책임 |
|---|---|
| `Scripts/Runtime/RhythmGameHost.cs` | 차트·악기·채널별 세션을 초기화·시작·종료하는 진입점. Clock/Judge/Display/Accompaniment를 한 곳에서 조율 |
| `Scripts/Runtime/RhythmSession` | 악기 `InstrumentBase.MidiTriggered` 구독 → `judge.OnInput(midiEvent)` 라우팅. 세션 단위 수명 관리 |
| `Scripts/Runtime/Judgment/RhythmJudge` | 차트 노트 대기열 vs 입력 시각을 비교해 `Judged` (`Action<JudgmentEvent>`) 발행. PERFECT/GOOD/MISS 등급은 `JudgmentGrade.cs` |
| `Scripts/Runtime/RhythmAccompaniment` | 예약된 반주 MIDI 이벤트를 시계에 따라 채널별 악기로 자동 발화. NoteOn/Off를 직접 `TriggerMidi` 호출 |
| `Scripts/Runtime/Clock/RhythmClock.cs` | `IRhythmClock` 구현. 리드-인 오프셋 + `ITimeProvider`(DSP/Unity 선택) 기반 currentTime 노출 |

### 데이터 흐름 (단일 통로)

```
[InstrumentBase.MidiTriggered]
        │ 모든 NoteOn/Off/Choke
        ▼
RhythmSession  ─ 구독
        │
        ▼
RhythmJudge.OnInput(midiEvent)
        │ 차트 대기열과 비교
        ▼
Judged 이벤트 (Action<JudgmentEvent>)
        │
        ▼
activeNoteDisplay.OnJudged   ─ RhythmGameHost 에서 구독
        │
        ▼
JudgmentPopup.Show + NoteVisual 제거
```

판정 결과는 외부 구독자에게도 노출되어 점수/통계/네트워크 sync에 재활용 가능하다.

## 3. Clock 두 백엔드

- `Scripts/Runtime/Clock/DspTimeProvider.cs` — `AudioSettings.dspTime`. 오디오 클럭과 동기. **프로덕션 기본값**.
- `Scripts/Runtime/Clock/UnityTimeProvider.cs` — `Time.timeAsDouble`. 오디오 비활성/테스트 환경용.

`RhythmClock`은 위 둘 중 하나를 `ITimeProvider` 인터페이스로 받아 `Now`를 노출. 인터페이스 분리 덕에 EditMode 테스트에서 가짜 clock 주입 가능 — `Tests/Editor/RhythmClockTests.cs` 참고.

## 4. Display 어댑터 패턴

| 항목 | 책임 |
|---|---|
| `Scripts/Runtime/Display/INoteDisplayController.cs` | 디스플레이의 공통 계약. `Init/Tick/OnJudged/Dispose` |
| `Scripts/Runtime/Display/NoteDisplayPanel.cs` | World Space Canvas 1장(88건반 가로 배치). 단일 악기 단일 패널에 사용. prefab: `Prefabs/NoteDisplayPanel.prefab` |
| `Scripts/Runtime/Display/DrumNoteDisplayAdapter` | 드럼 파츠별로 패널을 동적 `Instantiate`해 각자 다른 위치/회전에 배치 |
| `Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` | 부채꼴로 5-partial 패널을 배치 + 슬라이드 색상 가이드 |
| `NoteVisual.cs` | 낙하 오브젝트. 매 프레임 y 감소, 0 도달 시 판정 |
| `JudgmentPopup.cs` | 결과 텍스트 팝업(PERFECT/GOOD/MISS) |
| `BillboardUI.cs` | LateUpdate에서 카메라 방향으로 회전 + 기울기 보정 |

→ 새 악기에 디스플레이를 붙일 때는 `INoteDisplayController` 구현체를 추가하고 `NoteDisplayPanel.prefab`을 instantiate하는 어댑터를 만든다. `NoteDisplayPanel.prefab` 자체는 lane 매핑(`InstrumentLaneConfig`)만 받으면 88건반 가로 패널로 재사용된다.

## 5. 데이터 자산 매핑

`Assets/RhythmGame/Data/*.asset` ↔ SO 타입:

| 파일 | 타입 | 정의 |
|---|---|---|
| `SampleSong.asset` | `RhythmSong` | `Scripts/Data/RhythmSong.cs` — songId, title, artist, audioClip |
| `SampleChart_Beginner.asset` | `VmSongChart` | `Scripts/Data/VmSongChart.cs` — tempoMap + channelMap + tracks |
| `Drum_LaneConfig.asset`, `Piano_LaneConfig.asset`, `Trombone_LaneConfig.asset` | `InstrumentLaneConfig` | `Instruments/_Core/Scripts/InstrumentLaneConfig.cs` — MIDI ↔ lane 양방향 매핑 |
| `DrumKit_SongDatabase.asset`, `Piano_SongDatabase.asset` | `RhythmSongDatabase` | `Scripts/Data/RhythmSongDatabase.cs` — instrumentKey + songs[] |

> `*_LaneConfig.asset` 만 Instruments 도메인 정의의 SO다. 다른 자산은 모두 RhythmGame.Data 정의.

## 6. 차트 포맷 (.vmsong)

차트는 `Assets/StreamingAssets/Songs/*.vmsong` 텍스트 파일.

섹션 구조:
- `[META]` — songId/title/artist
- `[RESOLUTION]` — ticks-per-quarter
- `[TEMPO]` — TempoSegment 목록
- `[CHANNELS]` — channel ↔ instrumentId 매핑
- `[TRACK#]` — 노트 목록 (channel + tick + midi + velocity)

파서: `Scripts/Data/Parsing/VmSongParser.Parse(string text) → VmSongChart`.

호출 지점:
- `Scripts/Runtime/Dev/RhythmGameAutoLauncher.LoadChart`
- `Scripts/Runtime/Dev/ChartAutoPlayer.Start`
- `SessionPanel/Scripts/FolderScanSongCatalog.cs` 가 디스크에서 직접 읽어 전달

## 7. Dev 도구 (`Runtime.Dev` asmdef)

| 스크립트 | 용도 |
|---|---|
| `Scripts/Runtime/Dev/ChartAutoPlayer.cs` | VR 입력 없이 차트의 NoteOn/Off를 자동으로 악기에 보냄. 오디오/타이밍 검증용 |
| `Scripts/Runtime/Dev/RhythmGameAutoLauncher.cs` | 씬 시작 시 자동으로 차트 로드 + 세션 시작. 테스트 씬 부팅 단축 |

프로덕션 빌드에 포함되지 않는다 (`Runtime.Dev.asmdef`의 `includePlatforms: Editor`).

## 8. RhythmGame Prefab

`Assets/RhythmGame/Prefabs/NoteDisplayPanel.prefab` — World Space Canvas 1장. 각 악기 prefab의 자식(또는 어댑터가 동적 instantiate)으로 들어간다. **씬 직접 배치 안 함**.

악기 prefab 안에 NoteDisplayPanel 인스턴스가 임베드되어 있는 이유는 §4의 어댑터 패턴 — 악기 prefab은 자기 lane 배치(부채꼴/가로/원형)에 맞게 NoteDisplayPanel 인스턴스를 자기 children으로 들고 있는다.

## 9. 더 깊이 보고 싶을 때

- 세션 라이프사이클: `Scripts/Runtime/RhythmGameHost.cs`
- 판정 알고리즘(허용 윈도우): `Scripts/Runtime/Judgment/RhythmJudge.cs`
- 차트 파싱 세부: `Scripts/Data/Parsing/VmSongParser.cs`
- TempoMap → 실시간 변환: `Scripts/Data/TempoMap.Resolver.cs`
- 회귀 테스트 시리즈: `Tests/Editor/{RhythmClockTests, RhythmJudgeTests, TempoMapResolverTests, VmSongParserTests, RhythmAccompanimentTests, DrumNoteDisplayAdapterTests, NoteDisplayPanelWorldYOverrideTests}.cs`
