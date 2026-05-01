# 노트 디스플레이 패널

**Linked Spec:** [`note-display.md`](../specs/note-display.md)
**Status:** `Done`

## Goal

악기 표면에 World Space Canvas 패널을 붙이고, 세션이 시작되면 레인과 낙하 노트가 표시되고 세션이 종료되면 사라지는 핵심 노트 시각화 시스템을 구현한다. 이 Plan은 피아노(단일 패널) 기준으로 완성하며, 드럼 멀티패널과 판정 팝업은 Plan 008에서 다룬다.

## Context

note-display spec 결정사항:
- 레인+낙하 노트(Guitar Hero 방식). 레인 구성은 `InstrumentLaneConfig`(Plan 006 산출물)에서 읽는다.
- 채점 대상 트랙의 모든 노트(화음 포함)가 대응 레인에 표시된다.
- 세션 비활성 시 패널은 표시되지 않는다.

기존 코드 현황:
- `RhythmGameHost` (MonoBehaviour): `StartSession(chart, song, judgedChannel)` / `StopSession()` 공개 메서드 보유. `uiRoot` Transform 필드가 있으나 현재 미사용.
- `RhythmClock` / `IRhythmClock`: `CurrentTime`(double, 초 단위)을 제공하는 마스터 시계.
- `VmSongChart.tracks`: `ChartTrack` 목록. 각 `ChartNote`는 `tick`·`midiNote`·`durationTicks` 보유.
- `TempoMap.TickToSeconds(int tick)`: tick → 절대 시각(초) 변환.
- `InstrumentBase.LaneConfig`(Plan 006 산출물): 레인-MIDI 매핑.

Chart 노트는 tick 기반이므로, 표시 시점은 `scheduledTime - lookAheadSeconds` 에 노트 오브젝트를 생성하고, 낙하 속도를 `panelHeight / lookAheadSeconds`로 계산한다.

제약:
- World Space Canvas는 악기 GameObject의 자식으로 배치한다 (씬에서 Transform 수동 설정).
- 노트 오브젝트는 풀링 없이 Instantiate/Destroy로 시작해도 무방 (추후 풀링은 Notes).
- 이 Plan에서는 드럼 파츠별 패널 배치 로직을 포함하지 않는다.

## Approach

1. **`NoteDisplayPanel` MonoBehaviour 작성** (`Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs`)
   - SerializeField: `InstrumentLaneConfig laneConfig`, `float lookAheadSeconds = 2f`, `float panelHeight`.
   - `Show(VmSongChart chart, int judgedChannel, IRhythmClock clock)` — 판정 채널 트랙의 노트 목록을 읽어 내부 큐에 적재하고 패널 활성화.
   - `Hide()` — 진행 중인 노트 오브젝트 전부 제거, 패널 비활성화.
   - `Update()` — `clock.CurrentTime`을 읽어, 큐에서 `scheduledTime - lookAheadSeconds <= now`인 노트를 꺼내 레인에 스폰.

2. **`NoteVisual` MonoBehaviour 작성** (`Assets/RhythmGame/Scripts/Runtime/Display/NoteVisual.cs`)
   - SerializeField: `float fallSpeed` (패널이 계산해서 주입).
   - `Init(float fallSpeed, float lifetime)` — 스폰 시 초기화.
   - `Update()` — `transform.localPosition.y`를 매 프레임 감소. lifetime 초과 시 Destroy(gameObject).

3. **레인 레이아웃 설정**
   - `NoteDisplayPanel`이 `Awake`에서 `laneConfig.LaneCount`를 읽어 레인 영역(RectTransform)을 동적 생성.
   - 레인은 패널 가로를 균등 분할. 각 레인에 판정선(하단 고정 이미지) 포함.

4. **세션 연동 — `RhythmGameHost` 수정**
   - `[SerializeField] NoteDisplayPanel noteDisplayPanel` 필드 추가.
   - `StartSession()`에서 `noteDisplayPanel.Show(chart, judgedChannel, clock)` 호출.
   - `StopSession()`에서 `noteDisplayPanel.Hide()` 호출.

5. **씬 배치 (피아노 기준)**
   - 피아노 GameObject 하위에 World Space Canvas + `NoteDisplayPanel` 컴포넌트를 추가하고, Transform 오프셋·크기를 조정해 건반 위에 패널이 오도록 배치.
   - `RhythmGameHost`의 `noteDisplayPanel` 필드에 연결.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` — 패널 핵심 로직
- `Assets/RhythmGame/Scripts/Runtime/Display/NoteVisual.cs` — 낙하 노트 단위 오브젝트
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — `noteDisplayPanel` 필드 및 Show/Hide 호출 추가
- `Assets/Scenes/SampleScene.unity` — 피아노 하위 Canvas·패널 배치

## Acceptance Criteria

- [ ] `[manual-hard]` 에디터 Play 모드에서 `RhythmGameHost.StartSession()`을 호출하면 피아노 위 패널이 나타나고 노트가 위에서 아래로 내려온다.
- [ ] `[manual-hard]` `StopSession()`을 호출하면 패널과 모든 노트가 사라진다.
- [ ] `[manual-hard]` 같은 tick의 화음 노트(2개 이상)가 있으면 각기 다른 레인에 동시에 노트가 스폰된다.
- [ ] `[auto-hard]` `laneConfig`에 없는 midiNote를 가진 노트는 무시되고 예외가 발생하지 않는다.
- [ ] `[manual-hard]` 레인 수가 config에 따라 변경되면(재생 전) 레인 레이아웃이 그에 맞게 달라진다.

## Out of Scope

- 드럼 파츠별 패널 자동 배치 → Plan 008.
- Perfect/Good/Miss 팝업 표시 → Plan 008.
- 노트 오브젝트 풀링.
- 난이도별 노트 색 구분.

## Handoff

- `NoteDisplayPanel` (MonoBehaviour, `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs`)
  - `Show(VmSongChart chart, int judgedChannel, IRhythmClock clock)` — 세션 시작 시 호출
  - `Hide()` — 세션 종료 시 호출
  - SerializeField: `InstrumentLaneConfig laneConfig`, `float lookAheadSeconds = 2f`, `float panelHeight = 1f`, `NoteVisual noteVisualPrefab`
- `NoteVisual` (MonoBehaviour, `Assets/RhythmGame/Scripts/Runtime/Display/NoteVisual.cs`)
  - `Init(float fallSpeed, float lifetime)` — NoteDisplayPanel이 스폰 시 주입
- `RhythmGameHost` (`Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs`)
  - `[SerializeField] NoteDisplayPanel noteDisplayPanel` 필드
  - `StartSession()` → `noteDisplayPanel.Show()`, `StopSession()` → `noteDisplayPanel.Hide()`
- 씬 배치: Piano 하위 `NoteDisplayCanvas` → `NoteDisplayPanel` (laneConfig=`Piano_LaneConfig.asset` 연결됨)
- Plan 008이 `OnJudged(JudgmentEvent)` 메서드를 `NoteDisplayPanel`에 추가해야 함

## Notes

- `lookAheadSeconds`는 Inspector에서 조정 가능하도록 SerializeField로 열어 둔다.
- 판정선은 패널 하단 10~15% 높이 지점에 고정 이미지로 배치하면 직관적이다.
- 세션 없이 패널만 테스트하려면, 임시 `[ContextMenu]` 메서드로 Show/Hide를 호출할 수 있게 해두면 편리하다.
