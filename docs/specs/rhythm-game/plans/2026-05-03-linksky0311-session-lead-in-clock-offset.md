# Session Lead-in 구현 — 클락 음수 시작 오프셋

**Linked Spec:** [`../specs/01-session-lead-in.md`](../specs/01-session-lead-in.md)
**Status:** `Ready`

## Goal

세션 시작 시 내부 클락이 `-leadInSeconds`에서 카운트업하도록 수정해, 차트 첫 노트가 판정선 근처가 아닌 패널 최상단에서 자연스럽게 낙하하기 시작하게 한다.

## Context

### 현재 버그

`NoteDisplayPanel`은 노트의 스폰 시각을 `spawnTime = scheduledTime - lookAheadSeconds`로 계산한다. 테스트 차트의 첫 노트가 tick=0 (scheduledTime≈0s)이면 `spawnTime = 0 - 2 = -2s`가 된다. `RhythmClock`은 `Start()` 시 `_pausedAccumulated = 0.0`으로 초기화되므로 세션 시작 시 클락은 t=0에서 출발한다. 이 시점에 `spawnTime(-2) <= now(0)`이므로 노트가 즉시 스폰 처리되고, 늦은 스폰 보정(`elapsed=2s`)에 의해 `startY=0` → 노트가 판정선 바로 위에 나타난다.

### 해결 접근

`RhythmClock.Start()`에 `leadInSeconds` 파라미터를 추가하고 `_pausedAccumulated = -leadInSeconds`로 시작한다. 클락이 -3에서 출발하면 `spawnTime=-2`가 아직 미래이므로 노트가 올바르게 패널 상단에서 스폰된다. `RhythmJudge`, `RhythmAccompaniment`, `NoteDisplayPanel` 모두 `clock.CurrentTime`과 `scheduledTime`을 비교하는 방식이라 별도 수정 없이 자연스럽게 동작한다.

### 자동 보정

spec에 따라 `leadInSeconds < lookAheadSeconds`이면 `lookAheadSeconds`로 자동 보정한다. `RhythmGameHost`가 `NoteDisplayPanel.LookAheadSeconds`를 읽어 `effectiveLeadIn = Math.Max(leadInSeconds, lookAheadSeconds)`를 계산해 `clock.Start()`에 전달한다.

### 관련 파일

- `Assets/RhythmGame/Scripts/Runtime/Clock/IRhythmClock.cs` — `Start()` 인터페이스
- `Assets/RhythmGame/Scripts/Runtime/Clock/RhythmClock.cs` — 클락 구현체
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — `StartSession()` 진입점, leadIn 파라미터 보유
- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` — `lookAheadSeconds` 필드 보유

## Approach

1. **`IRhythmClock.cs` — `Start()` 시그니처 업데이트**
   - `void Start(VmSongChart chart)` → `void Start(VmSongChart chart, double leadInSeconds = 0)`

2. **`RhythmClock.cs` — 음수 시작 오프셋 적용**
   - `Start()` 구현에서 `_pausedAccumulated = -leadInSeconds;` 로 변경.
   - `_anchorRealTime = _provider.Now;` 은 그대로 유지.

3. **`NoteDisplayPanel.cs` — `LookAheadSeconds` 프로퍼티 추가**
   - `public float LookAheadSeconds => lookAheadSeconds;` 한 줄 추가 (Public API 섹션에).

4. **`RhythmGameHost.cs` — `leadInSeconds` 필드 추가 및 전달**
   - `[SerializeField] float leadInSeconds = 3f;` Inspector 필드 추가.
   - `StartSession()` 내 `clock.Start(chart)` 호출을 다음으로 교체:
     ```
     float lookAhead = noteDisplayPanel != null ? noteDisplayPanel.LookAheadSeconds : 0f;
     double effectiveLeadIn = System.Math.Max(leadInSeconds, lookAhead);
     clock.Start(chart, effectiveLeadIn);
     ```

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Clock/IRhythmClock.cs` — `Start()` 시그니처 변경
- `Assets/RhythmGame/Scripts/Runtime/Clock/RhythmClock.cs` — `_pausedAccumulated = -leadInSeconds` 적용
- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` — `LookAheadSeconds` 프로퍼티 추가
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — `leadInSeconds` Inspector 필드 + effectiveLeadIn 계산 및 전달

## Acceptance Criteria

- [ ] `[auto-hard]` 변경 후 Unity 컴파일 에러가 없다.
- [ ] `[manual-hard]` O키로 세션 시작 시 첫 노트가 패널 최상단에서 나타나며, 판정선에 도달하는 데 약 3초가 걸린다 (판정선 근처 즉시 출현 없음).
- [ ] `[manual-hard]` 세션 시작 직후 (리드인 3초 이내) 악기를 연주해도 판정(Perfect/Good/Miss)이 발생하지 않는다.

## Out of Scope

- 카운트다운 시각 UI (3…2…1…).
- lookAheadSeconds 값 자체의 조정.
- 리드인 중 반주 발화 타이밍 — 기존 RhythmAccompaniment 로직이 처리한다.
- DrumNoteDisplayAdapter의 lookAheadSeconds 참조 — 드럼은 DrumNoteDisplayAdapter가 자체 lookAheadSeconds를 가질 수 있으나 이 plan에서는 NoteDisplayPanel 경로만 다룬다.

## Notes

## Handoff
