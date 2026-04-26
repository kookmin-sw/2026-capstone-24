# DSP Clock Provider 전환

**Linked Spec:** [`timing-clock.md`](../specs/timing-clock.md)
**Status:** `Done`

## Goal

시간 소스를 엔진 프레임 시계(`Time.timeAsDouble`)에서 오디오 DSP 시계(`AudioSettings.dspTime`)로 교체해, 판정 정밀도를 프레임 단위(~16ms@60fps)에서 1ms 미만으로 향상시킨다.

## Context

리듬게임 판정 윈도우는 Perfect ±50ms / Good ±150ms이다. 현재 `UnityTimeProvider`가 `Time.timeAsDouble`을 사용 중이며, 60fps 환경에서 최대 ~16ms의 타이밍 오차가 발생할 수 있다. `AudioSettings.dspTime`은 오디오 스레드가 갱신하는 고해상도 시계로 프레임 레이트와 무관하게 1ms 미만 정밀도를 제공한다.

Plan 003에서 `ITimeProvider` 인터페이스로 시간 소스를 추상화해 두었기 때문에, `RhythmClock` 본체 변경 없이 provider 구현체만 교체하면 된다. 기존 테스트는 `FakeTimeProvider`를 사용하므로 수정 불필요.

### 현재 코드 상태

- `Assets/RhythmGame/Scripts/Runtime/Clock/ITimeProvider.cs` — `double Now { get; }` 인터페이스
- `Assets/RhythmGame/Scripts/Runtime/Clock/UnityTimeProvider.cs` — `Time.timeAsDouble` 구현체 (유지)
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs:16` — `new RhythmClock(new UnityTimeProvider())` 으로 인스턴스화 중

## Approach

1. **`DspTimeProvider.cs` 추가** — `ITimeProvider`를 구현하며 `AudioSettings.dspTime`을 반환한다. `Assets/RhythmGame/Scripts/Runtime/Clock/` 폴더에 위치.

2. **`RhythmGameHost.cs` 수정** — `Awake()`의 `new UnityTimeProvider()` 를 `new DspTimeProvider()` 로 교체한다.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Clock/DspTimeProvider.cs` — `AudioSettings.dspTime` 기반 ITimeProvider 구현체 (신규)
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — provider 교체 (수정)

## Acceptance Criteria

- [ ] `DspTimeProvider.Now`가 `AudioSettings.dspTime`을 반환한다.
- [ ] `RhythmGameHost`가 `UnityTimeProvider` 대신 `DspTimeProvider`를 사용한다.
- [ ] 기존 `RhythmClockTests` 전체가 그대로 통과한다 (FakeTimeProvider 기반이므로 영향 없음).
- [ ] Unity 콘솔에 컴파일 에러 0.

## Out of Scope

- `UnityTimeProvider` 삭제 — 에디터/테스트 용도로 존재 가치가 있으므로 유지.
- 시간 신축 / 슬로우다운 — 이번 피처에서 제외.
- 카운트인 처리 — `session-flow` 책임.

## Notes

- `AudioSettings.dspTime`은 `double`을 반환하므로 `ITimeProvider.Now`의 `double` 시그니처와 그대로 호환된다.
- Play Mode가 아닌 Editor 환경에서는 `dspTime`이 0으로 고정될 수 있다. EditMode 테스트는 `FakeTimeProvider`를 쓰므로 문제없다.
