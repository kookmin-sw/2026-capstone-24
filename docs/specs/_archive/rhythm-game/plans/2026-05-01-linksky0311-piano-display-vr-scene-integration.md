# 피아노 노트 디스플레이 VR 씬 통합

**Linked Spec:** [`gameplay-display.md`](../specs/gameplay-display.md)
**Status:** `Done`

## Goal

NoteDisplayCanvas를 Piano 자식으로 배치하고 JudgmentPopup을 씬에 추가해, Play 모드에서 VR 플레이어가 피아노 앞에 서면 노트 낙하와 판정 결과를 바로 볼 수 있게 한다.

## Context

`Assets/Scenes/SampleScene.unity` 기준 현재 상태:

- **NoteDisplayCanvas** (씬 루트, fileID 200000001/Transform 200000007): 월드 좌표 (0.091, 1.12, 0.7)에 고정됨. Piano와 무관한 위치여서 Piano를 이동하면 UI가 따라오지 않음.
- **NoteDisplayPanel** (fileID 200000003): Canvas + NoteDisplayPanel 컴포넌트 보유. `judgmentPopup` 필드가 `{fileID: 0}` (null). JudgmentPopup GameObject가 씬에 없어 판정 팝업이 뜨지 않음.
- **Piano 프리팹 인스턴스** (PrefabInstance fileID 447916926): 월드 좌표 (0.091, -0.084, 1.016), Y 회전 180°(플레이어 방향). prefab guid `20c3ebc0d8a60954b9bf428c65c948ce`.
- **RhythmGameHost** (fileID 100000002): `targetInstrument: {fileID: 1530775798}` (Piano InstrumentBase 참조). 현행 유지.
- **VR Player (XR Origin)**: 월드 (0, 0, 0). Main Camera 월드 y ≈ 1.12.

판정 흐름은 코드 레벨에서 이미 완성돼 있다:
`Piano.MidiTriggered` → `RhythmSession.OnMidiTriggered` → `RhythmJudge.OnInput` → `RhythmJudge.Judged` event → `NoteDisplayPanel.OnJudged` → `JudgmentPopup.Show(grade)`

이 plan은 이 흐름의 말단(JudgmentPopup)을 씬에 연결하고, NoteDisplayCanvas를 Piano 자식으로 이동하는 씬 수정만 수행한다.

## Approach

### 1. NoteDisplayCanvas → Piano 자식으로 reparent

Unity MCP `manage_gameobject`로 NoteDisplayCanvas의 parent를 Piano로 변경한다.

- Piano의 root Transform은 PrefabInstance `447916926`의 루트 GameObject. Unity Editor에서 `manage_gameobject(action="modify", target="NoteDisplayCanvas", parent="Piano")` 형태로 처리.
- reparent 후 **로컬 위치**를 `(0, 1.3, 0.5)`로 설정한다.
  - Piano local 좌표계: Y=180 회전이므로 local +X = world -X, local +Z = world -Z.
  - local (0, 1.3, 0.5) → world ≈ (0.091, 1.22, 0.516): 피아노 앞 플레이어 시야 정면.
- 로컬 **회전은 0** (identity). Canvas는 local +Z 방향 시청자에게 앞면이 보임. Piano Y=180 덕에 Canvas도 플레이어(-Z 쪽)를 향하게 된다.
- 스케일 `(0.001, 0.001, 0.001)` 유지.

### 2. JudgmentPopup GameObject 생성

NoteDisplayPanel 자식으로 JudgmentPopup 오브젝트를 생성한다.

- `manage_gameobject(action="create", name="JudgmentPopup", parent="NoteDisplayPanel")`
- RectTransform 앵커: 중앙 고정 (`anchorMin=(0.3, 0.4)`, `anchorMax=(0.7, 0.7)`).
- 컴포넌트 추가 순서:
  1. `CanvasGroup` (JudgmentPopup 스크립트가 Awake에서 참조)
  2. `UnityEngine.UI.Text` (legacy) — 폰트 사이즈 80, 중앙 정렬, 초기 텍스트 ""
  3. `JudgmentPopup` 스크립트 (`displayDuration = 0.6`)

### 3. judgmentPopup 필드 연결

`manage_components(action="set_property", target="NoteDisplayPanel", component_type="NoteDisplayPanel", property="judgmentPopup", value=<JudgmentPopup 인스턴스 ID>)`

### 4. 씬 저장 및 검증

`manage_scene(action="save")` 후 Play 모드 진입해 콘솔 에러 없음·판정 팝업 표시 확인.

## Deliverables

- `Assets/Scenes/SampleScene.unity` — NoteDisplayCanvas reparent + 로컬 오프셋, JudgmentPopup GameObject 추가·연결

## Acceptance Criteria

- [ ] `[auto-hard]` Unity 콘솔에 컴파일 에러 0건
- [ ] `[auto-hard]` Play 모드 진입 시 콘솔에 NullReferenceException 0건
- [ ] `[manual-hard]` Unity Editor Game View에서 Play 모드 진입 시 피아노 앞 월드 공간에 NoteDisplayPanel이 표시되고 노트가 위에서 아래로 낙하한다
- [ ] `[manual-hard]` Play 모드 중 피아노 건반에 해당하는 MIDI 노트가 발생하면(ChartAutoPlayer 자동 재생 또는 Piano 직접 입력) Perfect / Good / Miss 텍스트가 NoteDisplayPanel 위에 잠깐 표시된 후 사라진다

## Out of Scope

- 드럼 NoteDisplayCanvas 씬 배치 (DrumNoteDisplayAdapter 자체는 구현됨)
- 외형 디자인 개선 (폰트, 색상, 크기 최적화)
- 세션 진입 메뉴 UI
- RhythmGameHost 위치 변경

## Notes

JudgmentPopup에서 `Text(Legacy)` 사용: 2026년 기준 이 프로젝트는 TextMeshPro 의존성이 명시적으로 없으므로 `UnityEngine.UI.Text`로 작성. 추후 TMP로 교체 가능.

## Handoff

NoteDisplayCanvas는 Piano 자식으로 이동 (local pos 0, 1.3, 0.5). JudgmentPopup GameObject가 NoteDisplayPanel 자식으로 생성되고 NoteDisplayPanel.judgmentPopup 필드에 연결됨. 판정 흐름 완성: Piano.MidiTriggered → RhythmSession.OnMidiTriggered → RhythmJudge.Judged event → NoteDisplayPanel.OnJudged → JudgmentPopup.Show(grade).
