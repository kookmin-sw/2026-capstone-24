# RhythmClock 기반 자동 반주 구현

**Linked Spec:** [`accompaniment.md`](../specs/accompaniment.md)
**Status:** `In Progress`

## Goal

`ChartAutoPlayer`(Time.deltaTime 기반)를 `RhythmAccompaniment`(IRhythmClock.CurrentTime 기반)로 교체해, 자동 반주 노트 발화 타이밍이 마스터 시계(DSP 시간)와 동기화된다.

## Context

### 문제 상황

현재 `ChartAutoPlayer`는 `_elapsed += Time.deltaTime`으로 경과 시간을 추적한다.  
`RhythmGameHost`가 사용하는 `RhythmClock`은 `AudioSettings.dspTime`(DSP 클락)을 기준으로 동작한다.  
두 시간 소스는 게임 시작 직후 첫 프레임 deltaTime 스파이크 등으로 인해 즉시 어긋나며, 떨어지는 노트(NoteDisplayPanel이 `clock.CurrentTime` 기준으로 렌더링)와 들리는 소리(ChartAutoPlayer가 `Time.deltaTime` 기준으로 발화)가 맞지 않는다.

### 현재 코드 구조

- `ChartAutoPlayer` (MonoBehaviour): 씬에 배치되어 `Start()`에서 vmsong을 파싱하고, `Update()`에서 `_elapsed += Time.deltaTime`로 모든 채널 노트를 발화. `RhythmGameHost`/`RhythmSession`과 완전히 분리되어 있어 세션 라이프사이클에 종속되지 않음.
- `RhythmGameHost`: `StartSession(chart, song, judgedChannel)`에서 `RhythmClock.Start(chart)` → `RhythmJudge.Start(...)` → `RhythmSession.Start()` 순으로 세션을 시작함. 현재 accompaniment 연동 없음.
- `IRhythmClock.CurrentTime`: `double`, DSP 시간 기반. `RhythmClock`이 구현체.

### 결정사항

- `ChartAutoPlayer`를 삭제하고 `RhythmAccompaniment`(새 MonoBehaviour)로 대체한다.
- `RhythmAccompaniment`는 `IRhythmClock`을 외부에서 주입받아 `CurrentTime`으로 이벤트를 발화한다.
- `RhythmGameHost`가 `RhythmAccompaniment`의 `Begin(chart, judgedChannel, clock)` / `End()`를 호출해 세션 라이프사이클에 연동한다.
- 채점 채널(judgedChannel)은 자동 반주에서 제외한다.
- 씬에 대응 악기가 없는 채널은 조용히 skip한다(콘솔 경고 없음).

## Approach

1. **`RhythmAccompaniment` 스크립트 생성** (`Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs`)
   - `[SerializeField] ChannelBinding[] channelBindings` — 채널 번호 → InstrumentBase 매핑 (ChartAutoPlayer와 동일 구조)
   - `Begin(VmSongChart chart, int judgedChannel, IRhythmClock clock)`: 이벤트 목록 빌드, 재생 시작. judgedChannel 트랙 제외.
   - `End()`: 재생 중단, 내부 상태 초기화.
   - `Update()`: `clock.CurrentTime`과 `fireTime`(double) 비교해 발화. clock이 null이거나 Idle/Stopped이면 skip.
   - NoteOn + NoteOff 모두 스케줄 (fireTime = TickToSeconds(tick), TickToSeconds(tick + durationTicks)).
   - `ScheduledEvent.fireTime`은 `double` 타입 (clock.CurrentTime과 단위 통일).

2. **`RhythmGameHost` 수정** (`Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs`)
   - `[SerializeField] RhythmAccompaniment accompaniment` 필드 추가.
   - `StartSession()`: `clock.Start(chart)` 이후 `accompaniment?.Begin(chart, judgedChannel, clock)` 호출.
   - `StopSession()`: `accompaniment?.End()` 호출 (activeSession null 체크 블록 안에서).

3. **씬 업데이트** (`Assets/Scenes/SampleScene.unity`)
   - `ChartAutoPlayer` 컴포넌트 제거.
   - `RhythmAccompaniment` 컴포넌트 추가 (같은 오브젝트 또는 RhythmGameHost 오브젝트에).
   - ChannelBindings 와이어링: channel 1 → Piano, channel 10 → DrumKit (기존 ChartAutoPlayer 설정과 동일).
   - `RhythmGameHost.accompaniment` 필드에 `RhythmAccompaniment` 연결.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs` — 신규 생성
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — accompaniment 연동 추가
- `Assets/Scenes/SampleScene.unity` — ChartAutoPlayer 교체, RhythmAccompaniment 와이어링

## Acceptance Criteria

- [ ] `[auto-hard]` `RhythmAccompaniment.cs`가 컴파일 에러 없이 빌드된다.
- [ ] `[auto-hard]` `ChartAutoPlayer.cs`가 씬에서 제거되었거나, 존재하더라도 씬 오브젝트에 연결된 인스턴스가 없다.
- [ ] `[manual-hard]` Play 모드에서 피아노 세션(judgedChannel=1) 시작 시 드럼 반주 소리가 들리고, 드럼 소리와 피아노 노트 낙하 타이밍이 눈으로 확인할 수 있을 만큼 일치한다.
- [ ] `[manual-hard]` 세션 종료(StopSession 호출) 후 반주 소리가 더 이상 발화되지 않는다.

## Out of Scope

- 트랙별 음량/믹스 조절 — 추후 설정 UI.
- 트랙 on/off 선택 UI — 추후 설정 UI.
- 씬에 없는 트랙에 대한 콘솔 경고 또는 UI 알림.
- 일시정지(Pause/Resume) 연동 — 현재 세션에 Pause 기능 없음.
- ChartAutoPlayer.cs 파일 자체 삭제 — 씬에서 연결 해제로 충분. 파일 삭제는 별도 chore.

## Notes

## Handoff
