# 드럼 멀티패널 + 판정 팝업

**Linked Spec:** [`note-display.md`](../specs/note-display.md)
**Status:** `Done`

## Goal

드럼처럼 파츠가 분리된 악기에 각 파츠 표면 위 패널을 런타임에 자동 배치하고, 모든 악기에 공통으로 Perfect / Good / Miss 판정 결과 팝업(페이드아웃)을 추가한다.

## Context

note-display spec 결정사항:
- 드럼 파츠 패널의 위치·방향은 파츠 형태에서 런타임에 자동 계산한다.
- 판정 결과(Perfect / Good / Miss)는 패널 안에 팝업된 뒤 짧게 페이드아웃된다.

Plan 007 산출물 전제:
- `NoteDisplayPanel` MonoBehaviour: `Show(chart, judgedChannel, clock)` / `Hide()` API 완성.
- `RhythmGameHost`: `StartSession()` / `StopSession()`에서 패널을 Show/Hide.
- `RhythmJudge.Judged` 이벤트: `JudgmentEvent`(channel, midiNote, tick, scheduledTime, inputTime, grade) 발행.

기존 코드 현황:
- `DrumKit` (InstrumentBase 서브클래스): `DrumPiece` 자식 오브젝트들을 보유.
- `DrumPiece` MonoBehaviour: 각 드럼 파츠(스네어, 하이햇, 킥 등). 자신이 속한 `DrumKit`에 hit을 보고.
- `Renderer` 또는 `Collider`를 통해 각 파츠의 Bounds를 읽을 수 있다.
- `JudgmentGrade` enum: `Perfect`, `Good`, `Miss`.

드럼 파츠별 레인 구성은 `InstrumentLaneConfig`(Plan 006 산출물)의 각 `LaneEntry`가 특정 드럼 파츠의 midiNote에 대응한다. 한 파츠 = 레인 1개이므로, 패널도 파츠마다 1개씩 생성한다.

## Approach

1. **`DrumNoteDisplayAdapter` 작성** (`Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs`)
   - 역할: 드럼 전용 패널 자동 배치 컴포넌트.
   - `Init(InstrumentLaneConfig config, VmSongChart chart, int judgedChannel, IRhythmClock clock)` — DrumPiece 목록을 순회해 각 파츠의 midiNote를 config에서 조회하고, 파츠당 `NoteDisplayPanel`을 Instantiate해 파츠 위에 배치.
   - 패널 Transform 계산: 파츠 오브젝트의 `Renderer.bounds`(또는 `Collider.bounds`) 상단 중심 + 약간의 y 오프셋, 회전은 파츠 오브젝트의 up 방향 기준.
   - 파츠에 midiNote가 config에 없으면 해당 파츠는 패널을 생성하지 않는다.
   - `Hide()` — 생성한 패널 전부 Hide 후 Destroy.

2. **`RhythmGameHost`에 드럼 어댑터 분기 추가**
   - `InstrumentBase`가 `DrumKit`이면 `DrumNoteDisplayAdapter`를 사용하고, 그렇지 않으면 Plan 007의 단일 `NoteDisplayPanel`을 사용하도록 분기.
   - 분기는 `StartSession()` / `StopSession()` 내에서 처리.

3. **`JudgmentPopup` 작성** (`Assets/RhythmGame/Scripts/Runtime/Display/JudgmentPopup.cs`)
   - `NoteDisplayPanel`의 자식 오브젝트로 배치되는 MonoBehaviour.
   - `Show(JudgmentGrade grade)` — grade에 따라 "Perfect" / "Good" / "Miss" 텍스트(또는 색상)로 설정하고, `displayDuration`(기본 0.6초) 동안 표시 후 알파 페이드아웃.
   - `NoteDisplayPanel`이 `JudgmentPopup` 레퍼런스를 `[SerializeField]`로 보유하고, `Show()`를 호출하는 메서드 `OnJudged(JudgmentEvent e)` 제공.

4. **`RhythmJudge.Judged` → `JudgmentPopup` 연결**
   - `RhythmGameHost.StartSession()`에서 `judge.Judged += activeNoteDisplay.OnJudged` 구독.
   - `StopSession()`에서 구독 해제.

5. **씬 배치 — 드럼**
   - 드럼 GameObject에 `DrumNoteDisplayAdapter`를 추가하고, `NoteDisplayPanel` 프리팹 레퍼런스를 연결.
   - `RhythmGameHost`의 분기 로직이 런타임에 어댑터를 감지하도록 설정.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs` — 드럼 파츠별 패널 자동 배치
- `Assets/RhythmGame/Scripts/Runtime/Display/JudgmentPopup.cs` — 판정 결과 팝업 페이드아웃
- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` — `OnJudged(JudgmentEvent)` 메서드 추가 (Plan 007 파일 수정)
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — 드럼 분기 + Judged 이벤트 연결 추가 (Plan 007 파일 수정)
- `Assets/Scenes/SampleScene.unity` — 드럼 패널 어댑터 씬 배치

## Acceptance Criteria

- [x] `[manual-hard]` Play 모드에서 드럼 세션 시작 시, 각 DrumPiece 위에 독립 패널이 자동으로 배치되고 해당 파츠의 노트가 내려온다.
- [x] `[manual-hard]` 피아노 세션은 Plan 007 동작(단일 패널)이 유지된다 — 드럼 분기 추가 후에도 회귀 없음.
- [x] `[manual-hard]` 노트 히트 판정 이벤트 발생 시 해당 패널에 "Perfect" / "Good" / "Miss" 텍스트가 나타나고, 0.6초 내에 알파가 0으로 페이드아웃된다.
- [x] `[auto-hard]` `laneConfig`에 midiNote 매핑이 없는 DrumPiece에는 패널이 생성되지 않고 예외가 발생하지 않는다.
- [x] `[manual-hard]` 세션 종료 시 드럼 파츠별 패널이 모두 제거된다.

## Out of Scope

- 드럼 파츠 인식 방식(Renderer vs Collider) 이외의 파츠 위치 계산 방식 — plan 내 Approach 기준 사용.
- 판정 팝업 애니메이션 커스터마이징 (displayDuration 이외의 트윈 파라미터).
- 점수 합산 / 결과 화면.

## Handoff

- `INoteDisplayController` (Display/INoteDisplayController.cs): `Hide()` + `OnJudged(JudgmentEvent)` — NoteDisplayPanel·DrumNoteDisplayAdapter 공통 인터페이스. RhythmGameHost.activeNoteDisplay 타입.
- `DrumNoteDisplayAdapter` (Display/DrumNoteDisplayAdapter.cs): `Init(InstrumentLaneConfig, VmSongChart, int judgedChannel, IRhythmClock)` — DrumHitZone 순회 → 파츠당 NoteDisplayPanel Instantiate·배치. `Hide()` — 전 패널 Destroy. `OnJudged(JudgmentEvent)` — midiNote로 대상 패널 라우팅.
- `JudgmentPopup` (Display/JudgmentPopup.cs): `Show(JudgmentGrade)` — Perfect(노랑)/Good(초록)/Miss(빨강) 텍스트 + 0.6초 CanvasGroup 알파 페이드. NoteDisplayPanel의 `[SerializeField] judgmentPopup` 필드에 프리팹 단계에서 연결.
- `NoteDisplayPanel` (Display/NoteDisplayPanel.cs): `OnJudged(JudgmentEvent e)` 추가 — judgmentPopup?.Show(e.Grade). `SetLaneConfig(InstrumentLaneConfig)` 추가.
- `RhythmGameHost` (Runtime/RhythmGameHost.cs): `StartSession()`에서 instrument가 DrumKit이면 DrumNoteDisplayAdapter.Init(), 아니면 noteDisplayPanel.Show(). `judge.Judged += activeNoteDisplay.OnJudged` 구독. `StopSession()`에서 구독 해제 + `activeNoteDisplay.Hide()`.
- DrumNoteDisplayAdapter 씬 배치: DrumKit GameObject에 컴포넌트로 추가, `noteDisplayPanelPrefab` 필드에 NoteDisplayPanel 프리팹 연결 필요 (Inspector 또는 씬 YAML 편집).

## Notes

- `JudgmentPopup`의 페이드는 `CanvasGroup.alpha`를 `Time.deltaTime` 기반 Lerp로 구현하면 단순하다.
- DrumPiece에 Renderer가 없는 경우 Collider.bounds를 폴백으로 사용한다. 둘 다 없으면 `transform.position + Vector3.up * 0.1f`를 기본값으로.
- 판정 팝업 텍스트 색: Perfect=노랑, Good=초록, Miss=빨강 정도가 시인성이 좋다 (조정 가능).
