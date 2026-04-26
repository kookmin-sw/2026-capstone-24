# `.vmsong` Parser & Tick→Seconds Resolver

**Linked Spec:** [`chart-format.md`](../../../rhythm-game/specs/chart-format.md)
**Status:** `Done`

## Goal

`.vmsong` 텍스트 파일을 Plan `001`이 정의한 `VmSongChart` 자료구조로 파싱하는 로더와, tick 위치를 BPM 변화 구간을 누적 반영해 절대 시각(초)으로 변환하는 리졸버를 구현하고, EditMode 테스트로 결정성과 시간 정확도를 검증한다.

## Context

`chart-format.md` 핵심 행동 요구:

- 같은 차트 → 같은 자료구조 (deterministic).
- 차트에 정의된 정보만으로 모든 노트의 절대 시각(초) 계산 가능.
- BPM이 곡 중간에 바뀌는 구간이 정의되면 절대 시각은 그 변화를 누적 반영.

본 plan은 위 두 행동을 코드로 구현하는 단계다. UI/판정/반주 sub-spec과는 독립이다.

### Plan 001이 만든 전제

`Assets/RhythmGame/Scripts/Data/`에 다음 자료구조가 이미 존재한다 (Plain C# `[Serializable]`).

- `VmSongChart` — root: `title`, `artist`, `songId`, `TempoMap tempoMap`, `ChannelInstrumentMap channelMap`, `List<ChartTrack> tracks`.
- `TempoMap` — `int ticksPerQuarter`, `List<TempoSegment> segments`.
- `TempoSegment` — `int tick`, `float bpm`, `int beatsPerBar`, `int beatUnit`.
- `ChannelInstrumentMap` — `List<Entry{int channel, string instrumentKey}>`.
- `ChartTrack` — `int channel`, `List<ChartNote> notes`.
- `ChartNote` — `int tick`, `byte midiNote`, `int durationTicks`, `byte velocity`.
- `RhythmChannels.DrumChannel == 10`.

### `.vmsong` 신택스 (Plan 001 결정 사항 — 본 plan에서 그대로 구현)

```
# 주석은 '#' 이후 라인 끝까지. 빈 줄 무시.
# 섹션 헤더: [Section] 또는 [Section:Index]. 헤더 외 한 줄당 의미 단위 1개.
# key=value 쌍은 공백으로 구분, 한 라인에 여러 쌍 가능.
# 섹션/키 이름은 대소문자 구분 없음. value는 대소문자 보존.

[Meta]
title  = <string>     # 필수
artist = <string>     # 선택
songId = <string>     # 선택. 없으면 파일 stem(파서 외부에서 주입).

[Resolution]
ticksPerQuarter = <int>   # 필수, 양의 정수

[Tempo]                   # 필수, 최소 1개 segment
tick=<int> bpm=<float> [beats=<int>] [beatUnit=<int>]
...

[Channels]                # 필수
channel=<int 1..16> instrument=<string>
...

[Track:<int 1..16>]       # 1개 이상 필수
tick=<int> note=<int 0..127> len=<int> vel=<int 0..127>
...
```

상속 규칙: `Tempo`의 `beats`/`beatUnit`은 생략 시 직전 segment 값 상속. 첫 segment에서 생략되면 기본 4/4.

### 시간 변환 공식

`secondsPerTick(bpm, ticksPerQuarter) = 60.0 / (bpm * ticksPerQuarter)`

`TickToSeconds(targetTick)`은 `tempoMap.segments`를 tick 오름차순으로 순회하며,

1. 현재 segment의 시작 tick `s_i`와 다음 segment 시작 tick `s_{i+1}` 사이에 `targetTick`이 들어가면 그 segment에서 `(targetTick - s_i) * secondsPerTick(bpm_i)`을 더하고 종료.
2. 그렇지 않으면 `(s_{i+1} - s_i) * secondsPerTick(bpm_i)`을 누적하고 다음 segment로.

마지막 segment는 종료 tick이 없으므로 무한히 연장된다.

### 현재 코드 상태

- 파서: 없음.
- TempoMap에 메서드 없음 (Plan 001 자료구조는 데이터만).
- 테스트 어셈블리: 리듬게임 영역에 EditMode 테스트 어셈블리가 있는지 확인 필요. 없으면 본 plan에서 신규 생성.

## Approach

### 1. 토크나이저 / 라인 디스패처

`Assets/RhythmGame/Scripts/Data/Parsing/VmSongTokenizer.cs` (또는 파서 내부 private):

- 입력 `string text`를 라인 단위로 분리.
- 각 라인에서 `#` 이후 제거(인용 처리 없음 — 단순 라인 주석).
- 트림 후 빈 라인 스킵.
- 라인 분류: `[…]` 헤더 / `key=value …` 본문 / 그 외 → 에러 항목 누적.
- 헤더는 `[Name]` 또는 `[Name:Index]`. Index는 십진 정수.
- 본문 라인은 공백 분리 토큰들 각각을 `key=value`로 다시 분해. `key`는 invariant lower로 normalize.

### 2. 섹션 디스패처 / `VmSongParser`

`Assets/RhythmGame/Scripts/Data/Parsing/VmSongParser.cs`:

```csharp
public static class VmSongParser {
    public sealed class ParseError {
        public int line;        // 1-based
        public string message;
    }
    public sealed class ParseResult {
        public VmSongChart chart;          // 부분적으로 채워질 수 있음
        public List<ParseError> errors = new();
        public bool Success => errors.Count == 0;
    }
    public static ParseResult Parse(string text);
}
```

- 섹션별 핸들러 dictionary: `meta`, `resolution`, `tempo`, `channels`, `track`.
- `track` 헤더는 인덱스 필수. 채널 번호로 사용.
- 알 수 없는 섹션/키는 에러 누적 후 스킵 (파싱은 가능한 만큼 계속).
- 필수 섹션 누락 시 결과 끝에서 에러 항목 추가.
- `Tempo` 핸들러는 `beats`/`beatUnit` 상속 처리.
- 모든 트랙의 노트 리스트와 TempoMap.segments를 tick 오름차순 정렬해 invariant 보장. **같은 tick에 여러 노트가 있으면 입력 순서를 보존** (안정 정렬).

### 3. `TempoMap` 시간 변환 메서드

`Assets/RhythmGame/Scripts/Data/TempoMap.cs`에 partial 또는 동일 파일 안에 메서드 추가 (Plan 001은 데이터 필드만 정의했으므로, 본 plan에서 메서드를 덧붙인다):

```csharp
public partial class TempoMap {
    /// targetTick의 절대 시각(초). segments는 tick 오름차순 가정.
    public double TickToSeconds(int targetTick);
}
```

- segments가 비어 있거나 첫 segment의 tick이 0보다 크면 0 tick 기준 누적이 깨진다 → 첫 segment의 시작 tick은 0이라는 invariant를 파서가 강제. 만족 못 하면 파서가 에러 추가.
- 음수 tick은 호출 측 책임. 본 메서드는 0 미만이면 0으로 clamp.

### 4. EditMode 테스트

`Assets/RhythmGame/Tests/Editor/RhythmGame.Tests.Editor.asmdef` — 미존재 시 신규 생성. `nunit.framework` 참조, `Editor` 플랫폼 한정.

테스트 케이스:

`VmSongParserTests.cs`:

1. **MinimalChart_ParsesAllSections** — 메타/해상도/템포/채널/트랙1을 가진 최소 차트가 에러 0으로 파싱되고, 필드들이 기대값과 일치.
2. **Determinism_SameInput_EquivalentResult** — 같은 텍스트를 두 번 파싱한 결과가 (구조적 동등성 비교 헬퍼로) 같음.
3. **DrumChannel10_PreservedInChannelMap** — `[Channels]`에 `channel=10 instrument=drum`가 있을 때 결과에 보존.
4. **Comments_AndBlankLines_Ignored** — `#` 주석/빈 라인을 섞어도 파싱 결과 동일.
5. **CaseInsensitive_SectionAndKey** — `[META]`, `Title=...` 같은 변형도 인식.
6. **UnknownSection_AddsError_ContinuesParsing** — 알 수 없는 섹션이 있어도 다른 섹션은 정상 파싱되고 에러 항목 1개 누적.
7. **TrackHeader_RequiresChannelIndex** — `[Track]` (인덱스 없음)은 에러.
8. **NotesSorted_ByTickAscending** — 입력 순서가 뒤섞여도 결과의 `notes`가 tick 오름차순.
9. **TempoSegments_FirstMustStartAtZero** — 첫 segment의 tick이 0이 아니면 에러.

`TempoMapResolverTests.cs`:

1. **SingleSegment_120Bpm_480Tpq** — `ticksPerQuarter=480`, `bpm=120` 단일 segment에서 480 tick → 0.5초 (정확).
2. **MultiSegment_AccumulatesCorrectly** — `ticksPerQuarter=480`, segment 0=120bpm, segment 1@480tick=60bpm. 960 tick의 절대시각 = 0.5 + 0.5*2 = 1.5초.
3. **TickAtSegmentBoundary_UsesPriorSegment** — 정확히 segment 시작 tick을 질의하면 누적 결과만 반환 (해당 시점 secondsPerTick은 0배).
4. **NegativeTick_ClampsToZero** — 입력 -1 → 0 반환.
5. **EmptyTempoMap_ReturnsZero** — segments 비어 있으면 0 반환.

### 5. 통합 — RhythmSession 등 런타임 스텁

본 plan은 파서/리졸버를 구현하는 데까지만. `RhythmSession` 등 런타임이 새 파서를 호출해 노트를 시간 순으로 발화하는 로직은 `timing-clock` / `judgment` / `accompaniment` plan들의 영역.

단, 컴파일 깨짐 방지 차원에서 RhythmSession.cs 등이 신규 타입(`VmSongChart`)을 시그니처에 받기만 한다면 본 plan에서 시그니처 정리만 가볍게 한다 (Plan 001에서 이미 처리됐을 가능성 높음).

## Deliverables

- `docs/specs/rhythm-game/plans/002-chart-parser-and-time-resolver.md` — 본 파일.
- `Assets/RhythmGame/Scripts/Data/Parsing/VmSongParser.cs` — 메인 파서 + `ParseResult`/`ParseError`.
- (선택) `Assets/RhythmGame/Scripts/Data/Parsing/VmSongTokenizer.cs` — 분리하면 테스트 용이.
- `Assets/RhythmGame/Scripts/Data/TempoMap.Resolver.cs` (partial) 또는 `TempoMap.cs` 직접 수정 — `TickToSeconds` 메서드 추가.
- `Assets/RhythmGame/Tests/Editor/RhythmGame.Tests.Editor.asmdef` — 미존재 시 신규.
- `Assets/RhythmGame/Tests/Editor/VmSongParserTests.cs`
- `Assets/RhythmGame/Tests/Editor/TempoMapResolverTests.cs`

## Acceptance Criteria

- [ ] `VmSongParser.Parse(text)`가 Plan 001 본문의 예제 차트를 에러 0으로 파싱하고, 결과의 메타/채널 매핑/템포 segment 수/트랙 수/노트 수가 예제와 일치한다.
- [ ] 같은 입력을 두 번 파싱하면 구조적으로 동등한 `VmSongChart`가 생성된다 (테스트로 검증).
- [ ] `Channels` 섹션의 `channel=10 instrument=drum` 라인이 결과 `channelMap`에 보존된다.
- [ ] `[META]`, `Title=` 등 대소문자 변형 섹션/키가 동등하게 인식된다.
- [ ] 섹션 또는 라인이 잘못돼도 파서가 예외 없이 `ParseError` 항목을 누적하고 가능한 만큼 파싱을 계속한다.
- [ ] 모든 트랙의 노트와 `tempoMap.segments`가 tick 오름차순으로 정렬돼 결과에 담긴다 (입력 순서와 무관).
- [ ] 첫 tempo segment의 tick이 0이 아닐 때 파서가 `ParseError`를 추가한다.
- [ ] `TempoMap.TickToSeconds`가 단일 BPM에서 `(tick / ticksPerQuarter) * (60 / bpm)` 결과와 정확히 일치한다 (부동소수점 허용 오차 1e-6 이내).
- [ ] `TempoMap.TickToSeconds`가 BPM 변화 구간을 누적해 계산한다 (위 Approach §4 테스트 2번 시나리오 통과).
- [ ] 음수 tick 입력은 0초로 clamp된다.
- [ ] 모든 신규 EditMode 테스트가 통과한다 (Unity Test Runner 또는 `run_tests`).
- [ ] Unity 콘솔에 컴파일 에러 0.

## Out of Scope

- `.vmsong` 파일을 Unity 에셋으로 import하는 메커니즘 (`chart-import` sub-spec).
- 파서 결과를 런타임 발화/판정/반주에 연결하는 로직 (`timing-clock`, `judgment`, `accompaniment`).
- 파서 성능 최적화, 스트리밍 파싱.
- `.vmsong` writer (시리얼라이즈) — 본 plan은 read-only.

## Notes

- 파서 에러는 throw가 아닌 누적이 원칙. 차트 작성자(사람)에게 한 번에 여러 문제를 보고할 수 있어야 하기 때문.
- `partial class TempoMap`을 쓰면 Plan 001에서 만든 데이터 정의 파일을 건드리지 않고 본 plan이 메서드만 추가할 수 있다. 두 plan의 책임 분리에 유리.
- 후속 plan들이 노트를 "시간 순"으로 처리하므로, 파싱 단계의 정렬 invariant가 매우 중요하다.
