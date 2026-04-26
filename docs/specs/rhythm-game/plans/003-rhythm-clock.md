# Rhythm Clock — Master Time Source

**Linked Spec:** [`timing-clock.md`](../specs/timing-clock.md)
**Status:** `Done`

## Goal

세션 동안 곡 시작 시각(0초)부터 흐르는 단일 마스터 시계를 정식 컴포넌트(`RhythmClock`)로 도입한다. Start/Pause/Resume/Stop 상태 전환과 tick↔seconds 변환을 제공해 후속 모듈(`judgment`, `accompaniment`)이 같은 "지금"을 참조할 수 있게 한다.

## Context

이 리듬게임은 외부 오디오 파일을 동반하지 않고 모든 사운드를 MIDI로 발화한다. 따라서 곡 진행을 따라가는 마스터 시계가 시스템 자체에서 만들어져야 하며, 채점/자동 반주가 동시에 보는 단일 진실원이 필요하다 (`timing-clock.md`).

### 선결 결정 (이 plan에서 채택)

- **정밀도 진실원**: 엔진 프레임 시계 (`UnityEngine.Time.timeAsDouble`).
  - 이유: VR 환경에서 광학 동기와 자연스럽고 구현이 단순. dsp 시계 전환은 후속 plan에서 `ITimeProvider` 교체로 가능.
- **이번 범위**: Start / Pause / Resume / Stop. 시간 신축(연습 모드 슬로우다운)은 후속 plan으로 미룸.
- **카운트인**: `session-flow` 책임. `RhythmClock`은 0초부터 단조 증가만.

### 현재 코드 상태 (Plan 001/002 기준)

- `Assets/RhythmGame/Scripts/Data/`에 자료구조 완성: `VmSongChart`, `TempoMap`, `ChartTrack`, `ChartNote` 등.
- `Assets/RhythmGame/Scripts/Data/TempoMap.Resolver.cs`에 `TempoMap.TickToSeconds(int)` 구현 완료.
- 프로토타입 `Assets/RhythmGame/Scripts/Runtime/ChartAutoPlayer.cs`가 자체적으로 `Time.deltaTime`을 누적해 발화 중. 본 plan은 시계 컴포넌트만 추출하고, `ChartAutoPlayer` 마이그레이션은 후속 plan에서.
- `Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs`는 `float elapsedTime` 누적기를 직접 보유. 본 plan에서 clock 주입으로 교체.
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs`는 `Tick(deltaTime)`으로 session을 진행. clock 도입 후 단순화.
- 테스트 어셈블리 `Assets/RhythmGame/Tests/Editor/`는 Plan 002에서 이미 구축.

## Approach

### 1. 인터페이스 / 시간 소스 추상화

`Assets/RhythmGame/Scripts/Runtime/Clock/IRhythmClock.cs`

```csharp
public enum RhythmClockState { Idle, Running, Paused, Stopped }

public interface IRhythmClock
{
    RhythmClockState State { get; }
    double CurrentTime { get; }              // 곡 시작부터 누적된 초

    event System.Action<RhythmClockState> StateChanged;

    void Start(VmSongChart chart);
    void Pause();
    void Resume();
    void Stop();

    double TickToSeconds(int tick);          // 활성 chart의 TempoMap에 위임
}
```

`Assets/RhythmGame/Scripts/Runtime/Clock/ITimeProvider.cs`

```csharp
public interface ITimeProvider { double Now { get; } }
```

`Assets/RhythmGame/Scripts/Runtime/Clock/UnityTimeProvider.cs`

```csharp
public sealed class UnityTimeProvider : ITimeProvider
{
    public double Now => UnityEngine.Time.timeAsDouble;
}
```

### 2. `RhythmClock` 구현

`Assets/RhythmGame/Scripts/Runtime/Clock/RhythmClock.cs`

- 의존성: 생성자에서 `ITimeProvider` 주입 (기본값으로 `UnityTimeProvider` 사용 가능).
- 내부 필드:
  - `RhythmClockState _state = Idle`
  - `VmSongChart _chart`
  - `double _anchorRealTime` — 마지막 Running 진입 시 `provider.Now`
  - `double _pausedAccumulated` — 그 전까지의 Running 구간 합
- `CurrentTime` 계산:
  - `Idle` / `Stopped` → `0`
  - `Running` → `_pausedAccumulated + (provider.Now - _anchorRealTime)`
  - `Paused` → `_pausedAccumulated`
- `Start(chart)`: 모든 상태에서 호출 가능. `_chart = chart`, `_pausedAccumulated = 0`, `_anchorRealTime = provider.Now`, state → Running.
- `Pause()`: Running일 때만 동작. `_pausedAccumulated += provider.Now - _anchorRealTime`. state → Paused. 그 외 상태는 no-op.
- `Resume()`: Paused일 때만 동작. `_anchorRealTime = provider.Now`. state → Running. 그 외 no-op.
- `Stop()`: Idle 외에서 동작. `_pausedAccumulated = 0`. state → Stopped. 그 외 no-op.
- `TickToSeconds`: `_chart?.tempoMap.TickToSeconds(tick) ?? 0.0`.
- 상태 변경 시 `StateChanged` 이벤트 발화 (실제로 변경된 경우만).
- 잘못된 전이는 예외 없이 무시 (호출 측 단순화). `coding.md`에 따라 진단 로그 추가하지 않는다.

### 3. `RhythmSession` 통합

`Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs` 수정:

- `float elapsedTime` 필드와 `Tick(float)` 메서드 제거.
- 생성자 시그니처: `RhythmSession(InstrumentBase instrument, RhythmSong song, IRhythmClock clock)`.
- `ElapsedTime` 프로퍼티는 `clock.CurrentTime`을 float로 캐스팅해 노출 (혹은 `double`로 시그니처 변경).
- `Start()`는 clock 시작을 호출하지 않는다 — 외부(`RhythmGameHost`)가 chart까지 알고 있으므로 host가 clock을 시작. session은 자체 입력 구독만 책임.
- `Stop()`도 clock 정지 책임을 가지지 않는다. host가 일괄 제어.

### 4. `RhythmGameHost` 갱신

`Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` 수정:

- `RhythmClock` 인스턴스 보유 (`new RhythmClock(new UnityTimeProvider())`).
- `Update()`의 `activeSession?.Tick(Time.deltaTime)` 제거 (clock이 provider 기반으로 자가 진행).
- `StartSession(RhythmSong song)`: `RhythmSong`에서 `VmSongChart`를 가져오는 경로가 아직 정해지지 않았으면, 본 plan에서는 `StartSession(VmSongChart chart, RhythmSong song)`로 시그니처를 일단 확장하거나, 임시로 `null` chart로 clock을 시작해도 무방. 핵심은 clock과 session 라이프사이클 일원화.
  - 현 단계에서 `RhythmSong`이 `VmSongChart`를 들고 있지 않다면, 본 plan은 host의 chart 인자 경로만 추가하고 실제 wiring은 후속 plan에서 수행.
- `StopSession`: clock.Stop() 후 session.Stop().

### 5. EditMode 테스트

`Assets/RhythmGame/Tests/Editor/RhythmClockTests.cs` 신규.

테스트용 `FakeTimeProvider`는 같은 파일 안에 internal로 둔다 (`Now`를 임의로 setter로 진행).

테스트 케이스:

1. **Start_PutsStateRunning_AndCurrentTimeBeginsAtZero** — `Start(chart)` 직후 state==Running, CurrentTime==0.
2. **CurrentTime_AdvancesWithTimeProvider** — Start 후 provider.Now를 +1.5초 진행시키면 CurrentTime ≈ 1.5.
3. **Pause_FreezesCurrentTime** — Running 도중 +0.5초 흐른 뒤 Pause. provider.Now를 추가로 +1초 진행해도 CurrentTime은 0.5 유지.
4. **Resume_ContinuesFromPausedTime** — 위 시나리오 다음 Resume 후 provider.Now를 +0.3초 진행 → CurrentTime ≈ 0.8.
5. **Stop_ResetsCurrentTimeToZero** — Running/Paused 어느 쪽에서든 Stop 후 CurrentTime==0, state==Stopped.
6. **Restart_AfterStop_BeginsAtZero** — Stop 후 다시 Start하면 CurrentTime==0에서 재시작.
7. **TickToSeconds_DelegatesToTempoMap** — chart의 TempoMap에 segment를 넣고, clock.TickToSeconds가 같은 값을 반환.
8. **PauseFromIdle_NoOp** — Idle 상태에서 Pause 호출해도 state는 Idle 유지.
9. **ResumeFromRunning_NoOp** — Running 상태에서 Resume 호출해도 상태/시간 변동 없음.
10. **StateChanged_FiresOnlyOnActualTransition** — 같은 상태로의 전이는 이벤트를 발화하지 않는다.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Clock/IRhythmClock.cs` — 인터페이스 + `RhythmClockState` enum.
- `Assets/RhythmGame/Scripts/Runtime/Clock/ITimeProvider.cs` — 시간 소스 추상화.
- `Assets/RhythmGame/Scripts/Runtime/Clock/UnityTimeProvider.cs` — `Time.timeAsDouble` 구현.
- `Assets/RhythmGame/Scripts/Runtime/Clock/RhythmClock.cs` — 본체.
- `Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs` — clock 주입으로 수정.
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — clock 보유 및 라이프사이클 갱신.
- `Assets/RhythmGame/Tests/Editor/RhythmClockTests.cs` — 신규 EditMode 테스트.

## Acceptance Criteria

- [ ] `RhythmClock`이 spec의 5개 Behavior 시나리오를 모두 만족 (시작=0 단조증가, tick→seconds, BPM 변화 누적 — TempoMap 위임, judgment/accompaniment가 같은 시각, Pause→Resume 이어붙임).
- [ ] `CurrentTime`은 `ITimeProvider.Now`가 증가한 만큼만 증가하고, Paused 동안 일정하다.
- [ ] Pause 직후 Resume하면 누적 CurrentTime이 일시정지 시점 값을 보존하고 그 이후 다시 흐른다.
- [ ] `Stop` 호출 시 `CurrentTime`이 0으로 리셋되고 state가 Stopped로 전이된다.
- [ ] 잘못된 전이(`Pause` from Idle/Stopped, `Resume` from Running 등)는 예외 없이 무시되고 상태도 유지된다.
- [ ] `TickToSeconds`는 활성 chart의 `TempoMap.TickToSeconds`와 동일한 값을 반환한다 (테스트로 검증).
- [ ] `RhythmSession`이 자체 elapsedTime 누적기를 사용하지 않고 주입된 clock을 통해 시각을 얻는다.
- [ ] `StateChanged` 이벤트가 실제 상태 전이 시에만 발화한다.
- [ ] 신규 EditMode 테스트(`RhythmClockTests`)가 모두 통과한다 (Unity Test Runner / `run_tests`).
- [ ] Unity 콘솔에 컴파일 에러 0.

## Out of Scope

- 시간 신축 / 연습 모드 슬로우다운.
- 카운트인 처리 (`session-flow` 책임).
- 오디오 dsp 시계로의 전환 (필요 시 후속 plan에서 `ITimeProvider` 구현 교체로 처리).
- `ChartAutoPlayer.cs`의 신규 clock 마이그레이션 — 프로토타입은 그대로 두고 후속 plan에서 정식 통합.
- Judgment/Accompaniment의 clock 구독 로직 — 각 sub-spec plan에서.
- `RhythmSong` ↔ `VmSongChart` 연결 — 별도 wiring plan에서 처리. 본 plan은 chart를 외부에서 주입받는 경로만 마련.

## Notes

- `ITimeProvider` 추상화는 결정적 테스트 외에도, dsp 시계 전환 시 `RhythmClock` 본체 변경 없이 provider만 교체할 수 있게 하는 확장점이다.
- `partial`이 아닌 별도 namespace/폴더(`Runtime/Clock/`)로 분리해 후속 모듈이 의존 그래프를 쉽게 파악하도록 한다.
- `RhythmGameHost`의 chart 주입 경로는 본 plan에서 일단 시그니처만 잡아두고, 실제 곡 선택 → chart 로드 흐름은 `chart-import` / `session-flow` plan에서 채운다.
