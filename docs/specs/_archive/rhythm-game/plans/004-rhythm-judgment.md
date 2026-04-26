# Rhythm Judgment — Note Match & Grade Events

**Linked Spec:** [`judgment.md`](../specs/judgment.md)
**Status:** `Done`

## Goal

채점 대상 트랙의 노트를 마스터 시계 기준 시간 윈도우(±50ms / ±150ms)로 매칭하고, Perfect / Good / Miss 판정을 이벤트로 발화하는 채점 컴포넌트(`RhythmJudge`)를 도입한다. 후속 모듈(점수, UI 등)이 이벤트 구독으로 결과를 받는 단일 인터페이스를 마련한다.

## Context

이 리듬게임은 외부 오디오 없이 MIDI로 발화되며, 마스터 시계는 plan 003에서 도입된 `RhythmClock`이다. 본 plan은 클락의 첫 정식 소비자로 입력 채점 로직을 구축한다 (`judgment.md`).

### 선결 결정 (spec에 이미 반영)

- **시간 윈도우 폭**: Perfect ±50ms / Good ±150ms / Miss > 150ms. 난이도별 차등 없음(고정).
- **동시 노트(코드) 매칭**: 음별 독립 매칭. 각 노트는 자신과 같은 음의 입력으로 따로 판정되며, 매칭되지 않은 노트는 Miss.
- **롱노트**: NoteOn 시각만 판정. 길이 정보(`durationTicks`)는 무시. Hold/NoteOff 판정은 후속 sub-spec.
- **결과 노출**: 이벤트 발화. 다른 모듈은 구독으로 결과를 받는다.

### 현재 코드 상태 (plan 001/002/003 기준)

- `Assets/RhythmGame/Scripts/Data/`에 자료구조 완성: `VmSongChart`, `TempoMap`, `ChartTrack`, `ChartNote`.
- `Assets/RhythmGame/Scripts/Data/TempoMap.Resolver.cs`에 `TempoMap.TickToSeconds(int)` 구현.
- `Assets/RhythmGame/Scripts/Runtime/Clock/`에 `IRhythmClock` / `RhythmClock` / `ITimeProvider` / `UnityTimeProvider` 완성.
- `Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs`는 `instrument.MidiTriggered`를 구독해 `Debug.Log`만 찍고 있음 (plan 003 직후 상태). 본 plan에서 Judge에 위임.
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs`는 `StartSession(VmSongChart chart, RhythmSong song)`으로 chart 주입 경로가 있고 `clock.Start(chart)`까지 호출. `Update()`는 비어 있음.
- 테스트 어셈블리 `Assets/RhythmGame/Tests/Editor/`는 plan 002/003에서 구축. `RhythmClockTests.cs`에 EditMode 테스트 패턴 존재.

### 채점 대상 채널 결정 위치

플레이어가 잡은 악기 ↔ 채점 대상 채널의 자동 해소(예: `InstrumentBase` → `channelMap` 룩업)는 본 plan 범위 밖이다. 본 plan은 **채점 대상 채널을 외부에서 정수로 주입받는 경로**만 마련하고, 실제 wiring은 후속 plan(`chart-import` 또는 `session-flow`)에서 채운다.

### 입력 음높이 비교

`MidiEvent.Note`(byte)와 `ChartNote.midiNote`(byte)를 같은 정수로 비교. 옥타브 보정/이명동음 같은 변환은 하지 않는다.

## Approach

### 1. 판정 결과 데이터 타입

`Assets/RhythmGame/Scripts/Runtime/Judgment/JudgmentGrade.cs`

```csharp
public enum JudgmentGrade { Perfect, Good, Miss }
```

`Assets/RhythmGame/Scripts/Runtime/Judgment/JudgmentEvent.cs`

```csharp
public readonly struct JudgmentEvent
{
    public readonly int           channel;
    public readonly byte          midiNote;
    public readonly int           tick;
    public readonly double        scheduledTime;   // 마스터 시계 기준 노트 예정 시각(초)
    public readonly double        inputTime;       // 입력이 도달한 시각(초). Miss이면 NaN.
    public readonly JudgmentGrade grade;

    public JudgmentEvent(int channel, byte midiNote, int tick,
                         double scheduledTime, double inputTime, JudgmentGrade grade)
    { ... }
}
```

### 2. `RhythmJudge` 본체

`Assets/RhythmGame/Scripts/Runtime/Judgment/RhythmJudge.cs`

상수:

```csharp
const double PERFECT_WINDOW_SEC = 0.050;
const double GOOD_WINDOW_SEC    = 0.150;
```

생성자: `RhythmJudge(IRhythmClock clock)`.

내부 상태:

- `IRhythmClock _clock`
- `List<PendingNote> _pending` — 시각 오름차순 정렬된 미판정 노트 큐. 매칭/Miss 처리 시 head부터 소비.
- `int _nextMissCheckIndex` — Miss 검사용 cursor (시간이 지난 노트만 검사).
- `bool _running`

`PendingNote`는 internal struct: `int channel, byte midiNote, int tick, double scheduledTime, bool judged`.

이벤트:

```csharp
public event System.Action<JudgmentEvent> Judged;
```

메서드:

- `Start(VmSongChart chart, int judgedChannel)`:
  - chart의 트랙 중 `track.channel == judgedChannel`만 선택.
  - 해당 트랙의 모든 노트를 순회해 `PendingNote { scheduledTime = chart.tempoMap.TickToSeconds(note.tick) }` 리스트로 빌드. 길이 정보(`durationTicks`)는 사용하지 않는다.
  - `scheduledTime` 오름차순 정렬.
  - `_running = true`, `_nextMissCheckIndex = 0`.
- `Stop()`: `_running = false`. 큐는 그대로 둔다(필요 없으면 GC). 다음 `Start`에서 재빌드.
- `OnInput(MidiEvent midiEvent)`:
  - `_running`이 false면 no-op.
  - 입력 시각 `t = _clock.CurrentTime`.
  - 후보 선정: `_pending` 중 `judged == false`이며 `midiNote == midiEvent.Note`이며 `|scheduledTime - t| ≤ GOOD_WINDOW_SEC`인 노트.
  - 후보 중 `|scheduledTime - t|` 최솟값을 가진 노트를 선택.
  - 선택 노트의 `judged = true`. 차이 `≤ PERFECT_WINDOW_SEC`이면 `Perfect`, 그 외이면 `Good`.
  - `Judged(JudgmentEvent ...)` 발화.
  - 후보가 없으면 무시 (이벤트 미발화).
- `Tick()`:
  - `_running`이 false면 no-op.
  - `now = _clock.CurrentTime`.
  - `_pending`을 `_nextMissCheckIndex`부터 순회하며 `judged == false`이고 `now > scheduledTime + GOOD_WINDOW_SEC`인 노트는 `judged = true` 처리하고 `Miss`(inputTime=NaN) 이벤트 발화.
  - 선두에서 연속으로 처리된 만큼 `_nextMissCheckIndex` 전진. 정렬되어 있으므로 head에서부터 검사하면 충분.
  - 단, head가 매칭 미판정 상태로 남아 있을 수 있으므로(아직 윈도우 안), `now > scheduledTime + GOOD_WINDOW_SEC`을 만족하지 않는 노트를 만나면 break.

설계 메모:

- 매칭/Miss 모두 `judged` 플래그로 일관 처리. `_pending`을 List에서 제거하지 않는 이유: 인덱스 안정성 + 후일 Hold 판정 확장 시 같은 노트 재참조 가능성.
- `_nextMissCheckIndex`는 head cursor라서, 매칭으로 head가 `judged=true`가 되면 Tick에서 같이 전진. 일반 리듬게임의 standard pattern.

### 3. `RhythmSession` 통합

`Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs` 수정:

- 생성자 시그니처: `RhythmSession(InstrumentBase instrument, RhythmSong song, IRhythmClock clock, RhythmJudge judge)`.
- `OnMidiTriggered`의 `Debug.Log`를 제거하고 `judge.OnInput(midiEvent)` 호출로 교체.
- `Start`/`Stop`은 입력 구독만 책임 (judge 라이프사이클은 host가 제어).

### 4. `RhythmGameHost` 갱신

`Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` 수정:

- 필드 추가: `RhythmJudge judge`.
- `Awake()`에서 `judge = new RhythmJudge(clock)` 생성.
- `StartSession` 시그니처 확장: `StartSession(VmSongChart chart, RhythmSong song, int judgedChannel)`.
  - `clock.Start(chart)` 호출 후 `judge.Start(chart, judgedChannel)` 호출.
  - session 생성 시 judge 주입.
- `StopSession`에서 `judge.Stop()` 호출.
- `Update()`에 `judge.Tick()` 추가 (Miss 검사용 폴링).
  - `activeSession`이 null이면 호출하지 않음.

### 5. EditMode 테스트

`Assets/RhythmGame/Tests/Editor/RhythmJudgeTests.cs` 신규.

`FakeTimeProvider`는 `RhythmClockTests.cs`와 동일 패턴으로 같은 파일 안에 internal로 정의 (또는 별도 헬퍼 파일이 있다면 재사용). 테스트 helper로 `List<JudgmentEvent>`에 이벤트를 캡처.

테스트 차트는 단일 트랙으로 inline 생성: `var chart = new VmSongChart(); chart.tempoMap.segments.Add(new TempoSegment(0, bpm: 120, ticksPerQuarter: 480, ...));` (실제 `TempoSegment` API는 `Assets/RhythmGame/Scripts/Data/TempoSegment.cs` 확인). tempoMap이 0 tick → 0초로 매핑되는 단순 구성으로 가정한 뒤, `TickToSeconds`가 예측 가능한 값을 내도록 노트의 tick 값을 직접 조정.

테스트 케이스:

1. **InputWithinPerfectWindow_FiresPerfect** — 노트 시각 1.0s, 입력 시각 1.03s → 단일 Perfect 이벤트.
2. **InputWithinGoodWindow_FiresGood** — 노트 시각 1.0s, 입력 시각 1.10s → 단일 Good 이벤트.
3. **InputOutsideAllWindows_DoesNotMatch** — 노트 시각 1.0s, 입력 시각 0.5s → 즉시 이벤트 미발화. 이후 1.20s까지 진행시키고 Tick 호출 → Miss 발화.
4. **InputWrongNote_DoesNotMatch** — 노트 음 60, 입력 음 64 → 즉시 이벤트 미발화. 이후 시간 진행 + Tick → Miss(음 60).
5. **NotePassedWithoutInput_FiresMissOnTick** — 노트 시각 1.0s, 시간 1.20s에서 Tick 호출 → Miss 발화.
6. **InputMatched_NoMissLater** — 1.03s 입력으로 Perfect 매칭. 이후 시간 진행해도 같은 노트에 대해 추가 이벤트 없음.
7. **MultipleNotesAtSameTime_IndependentMatching** — 같은 시각 1.0s에 음 60/64/67. 1.03s에 음 60 입력. 시간 1.20s + Tick → 60은 Perfect, 64/67은 Miss 두 건.
8. **OnlyJudgedChannel_IsTracked** — 차트에 채널 1(judgedChannel)과 채널 10 노트가 있을 때, 채널 10 노트는 Miss/매칭 어느 이벤트도 발화하지 않는다.
9. **DuplicateInput_DoesNotDoubleMatch** — 같은 노트에 1.03s에 입력하고 다시 1.04s에 입력. 첫 입력만 Perfect 매칭, 두 번째는 무시 (해당 노트가 이미 judged).
10. **NearestNoteMatched_WhenMultipleSameNoteInWindow** — 음 60이 0.95s와 1.05s에 두 개. 입력 1.03s → 1.05s 노트가 매칭 (차 0.02s가 0.08s보다 작음). 0.95s 노트는 시간 진행 후 Miss.
11. **OnInputBeforeStart_NoOp** — Start 호출 전 OnInput → 이벤트 미발화 + 예외 없음.
12. **TickBeforeStart_NoOp** — Start 호출 전 Tick → 이벤트 미발화 + 예외 없음.

각 테스트는 `FakeTimeProvider.Now`를 직접 set하고 `clock.Start(chart)` 후 시간을 흘려 `judge.Tick()`을 명시적으로 호출하는 패턴.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Judgment/JudgmentGrade.cs` — enum.
- `Assets/RhythmGame/Scripts/Runtime/Judgment/JudgmentEvent.cs` — 결과 struct.
- `Assets/RhythmGame/Scripts/Runtime/Judgment/RhythmJudge.cs` — 본체 (입력 매칭 + Miss 폴링 + 이벤트 발화).
- `Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs` — judge 주입으로 수정. `OnMidiTriggered`의 Debug.Log를 `judge.OnInput`으로 교체.
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — judge 보유, `StartSession`에 `judgedChannel` 파라미터 추가, `Update()`에서 `judge.Tick()` 호출.
- `Assets/RhythmGame/Tests/Editor/RhythmJudgeTests.cs` — 신규 EditMode 테스트.

## Acceptance Criteria

- [ ] `RhythmJudge`가 spec의 Behavior 시나리오 6개를 모두 만족 (Perfect, Good, Miss, 다른 음 무기여, 동시 노트 음별 독립, 판정 이벤트 노출).
- [ ] 입력 시각이 노트 시각 ±50ms 이내일 때 `Perfect` 이벤트가 발화된다 (테스트로 검증).
- [ ] 입력 시각이 ±50ms 밖이지만 ±150ms 이내일 때 `Good` 이벤트가 발화된다.
- [ ] 입력이 ±150ms 안에 도달하지 않은 노트는 시각 + 150ms를 넘긴 시점의 `Tick()`에서 `Miss` 이벤트가 발화된다.
- [ ] 입력 음이 노트 음과 다르면 그 노트의 판정에 기여하지 않는다 (이벤트 미발화).
- [ ] 같은 시각의 동시 노트는 각각 독립적으로 판정된다 (한 음 매칭이 다른 음에 영향 없음).
- [ ] 한 노트는 한 번만 판정된다 (중복 입력은 무시, Miss 이후 입력도 무시).
- [ ] `judgedChannel`로 지정되지 않은 채널의 노트는 매칭/Miss 어느 이벤트도 발화하지 않는다.
- [ ] `Judged` 이벤트의 페이로드(`channel`, `midiNote`, `tick`, `scheduledTime`, `inputTime`, `grade`)가 spec이 요구하는 "어느 노트, 어떤 등급" 식별을 가능하게 한다.
- [ ] `RhythmSession`이 입력 처리 시 자체 로깅 없이 주입된 `RhythmJudge`로 위임한다.
- [ ] `RhythmGameHost`가 chart 시작 시 `judge.Start(chart, judgedChannel)`을 호출하고, 매 프레임 `judge.Tick()`을 호출하며, `StopSession`에서 `judge.Stop()`을 호출한다.
- [ ] 신규 EditMode 테스트(`RhythmJudgeTests`)가 모두 통과한다 (Unity Test Runner / `run_tests`).
- [ ] Unity 콘솔에 컴파일 에러 0.

## Out of Scope

- 점수 합산 / 등급(S/A/B/C) / 결과 화면.
- 노트 시각화 UI (떨어지는 노트 등).
- 롱노트 Hold/NoteOff 판정 — `durationTicks`는 무시한다. 후속 sub-spec.
- 플레이어가 잡은 악기 ↔ 채점 대상 채널의 자동 해소 — 본 plan은 `judgedChannel`을 외부에서 정수로 주입받는 경로만 마련. 실제 wiring은 `chart-import` 또는 `session-flow` plan에서.
- 자동 반주(`accompaniment`) — 본 plan은 채점 대상 트랙의 자동 발화를 다루지 않는다. 차트의 다른 트랙은 본 plan 범위에서 무음.
- 입력 지연(latency) 보정 — `clock.CurrentTime`을 그대로 입력 시각으로 사용한다.

## Notes

- `_pending`을 List 인덱스 + judged 플래그로 운영하는 이유: 후일 롱노트 Hold 단계에서 같은 노트의 NoteOff 판정으로 재참조해야 할 가능성 + Miss cursor 단순화. 매칭 시 노트를 List에서 제거하면 인덱스가 흔들린다.
- Miss 검사 cursor(`_nextMissCheckIndex`)는 head에서만 전진한다. 정렬된 큐 가정 하에 시간이 지나도 매칭되지 않은 head 노트는 결국 `now > scheduledTime + 150ms`를 만족해 Miss 처리된 뒤 cursor가 다음으로 넘어간다.
- `RhythmGameHost.Update()`의 매 프레임 `Tick()` 호출은 frame clock 기반(plan 003)이라 정확도가 ±1 frame이다. dsp 기반 정밀화는 후속 plan에서 `ITimeProvider` 교체 + Tick 빈도 조정으로 처리.
- `judgedChannel = 0` 같은 미설정 값을 차트에 채널 0이 있을 가능성이 있으면 충돌하므로, 호스트는 항상 의미 있는 값으로 호출해야 한다 (테스트는 1 이상으로 진행).
