# Drum Session Panel Position Fix — _panelAnchor Y·Z 거리 검증 및 미세 조정

**Linked Spec:** [`12-drum-session-panel-position-fix.md`](../specs/12-drum-session-panel-position-fix.md)
**Status:** `Done`

## Goal

DrumKit prefab의 `_panelAnchor` Transform `localPosition`이 sub-spec 10 plan handoff에서 이미 `(-0.5, 0.9, 0.2)`로 설정되어 있다. 본 plan은 이 값이 드럼 연주 자세에서 SessionPanel을 손으로 조작하기에 적합한 거리·높이인지 Play 모드에서 manual 검증하고, 필요 시 Y·Z만 미세 조정해 사용자가 편하게 조작 가능한 위치를 확정한다. X 위치(-0.5)는 sub-spec Out of Scope 답습으로 변경하지 않는다.

## Context

Sub-spec 12 What 요건:
1. DrumKit prefab의 `_panelAnchor` Y·Z를 드럼 연주 자세에서 SessionPanel이 편하게 조작 가능한 거리·높이에 표시되도록 조정.
2. 조정된 값이 prefab에 고정되어 인스턴스 재생성 시에도 동일 위치에 패널이 뜬다.
3. X 조정 / 다른 악기 / SessionPanel 회전은 Out of Scope.

현재 상태(이전 sub-spec handoff 박제):

- DrumKit.prefab `_panelAnchor` (GameObject `PanelAnchor`, fileID `1479758216802493830`)의 `m_LocalPosition = (-0.5, 0.9, 0.2)`. sub-spec 10 plan에서 NoteDisplayPanel 회전 기준점으로 추가됐고, sub-spec 11 plan은 그 forward 방향을 드럼 노트 패널 회전 계산에 사용. 본 plan은 **localPosition Y·Z 값을 SessionPanel 조작 거리에 맞게 검증·조정**하는 작업이다.
- `InstrumentBase.PanelAnchor` (line 56): `_panelAnchor != null ? _panelAnchor : transform`. _panelAnchor가 wiring되어 있으므로 그 Transform이 그대로 반환된다.
- `SessionPanelController.PositionAtInstrument()` (line 344-364): `_provider.Current.PanelAnchor`의 `position`을 그대로 `_panelInstance.transform.position`에 대입. 즉, `_panelAnchor`의 world position이 곧 SessionPanel의 world position. X·Y·Z 모두 1:1로 반영된다. (회전은 카메라 방향 기준으로 별도 계산 — Out of Scope.)

해법(메인 추론 한 줄 답습: "현재 값 `(-0.5, 0.9, 0.2)`가 적합한지 manual 검증, 필요시 Y·Z만 조정"):

1. **현재 값 baseline 확인**: prefab의 현재 Y=0.9, Z=0.2를 grep으로 박제(auto-hard).
2. **Play 모드 manual 검증**: 헤드셋(또는 Editor Play)에서 드럼 anchor 텔레포트 → SessionPanel 열기 → 손으로 InstrumentToggleButton/DifficultyButton에 닿는 거리인지 시각·물리 확인.
3. **조정 필요 시**: manual 검증에서 "너무 멀다 / 너무 낮다 / 너무 가깝다"가 확인되면 사용자가 만족하는 Y·Z 값을 Inspector로 잡고, 그 값을 prefab `m_LocalPosition`에 직접 박제(MCP `manage_asset` 또는 사용자 승인 후 텍스트 Edit). X는 -0.5 유지.
4. **인스턴스 재생성 검증**: prefab 변경 후 씬에서 인스턴스를 한 번 더 생성·세션 시작해도 동일 위치 확인.

본 plan은 **이미 적용된 값이 적합할 가능성**도 있다 — 그 경우 manual-hard AC는 "현재 `(0.9, 0.2)`로 충분히 편한 거리이며 추가 조정 불필요"로 사용자가 PASS 처리하고, 별도 prefab 수정 없이 종료. 조정이 필요하면 사용자가 만족할 때까지 반복.

## Verified Structural Assumptions

- DrumKit.prefab (`Assets/Instruments/Drum/Prefabs/DrumKit.prefab`)의 `PanelAnchor` GameObject (fileID `3716779064653313039`, name `PanelAnchor`)는 Transform fileID `1479758216802493830`을 단일 컴포넌트로 가지며 `m_LocalPosition: {x: -0.5, y: 0.9, z: 0.2}`, `m_LocalRotation: identity`, `m_LocalScale: (0.6666667, 0.6666667, 0.6666667)`, `m_Father: 6839219354136266365`(DrumKit root). — `Read Assets/Instruments/Drum/Prefabs/DrumKit.prefab (2026-05-20)` lines 272-302
- `InstrumentBase.PanelAnchor` 게터는 `_panelAnchor != null ? _panelAnchor : transform` (line 56). DrumKit prefab의 `_panelAnchor` 필드가 위 PanelAnchor GameObject Transform(fileID `1479758216802493830`)에 wiring되어 있어 (sub-spec 10 plan에서 박제) PanelAnchor Transform이 그대로 반환된다. — `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-20)`
- `SessionPanelController.PositionAtInstrument()` (line 344-364)은 `Transform anchor = _provider.Current.PanelAnchor`를 받아 `_panelInstance.transform.position = anchor.position` (line 349)로 패널 world position을 결정. **X·Y·Z 모두 1:1 그대로 사용**되며 별도 오프셋 없음. 회전은 main camera 방향 기준 LookRotation으로 별도 계산(line 352-358, 본 plan 대상 아님). — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-20)` lines 330-364
- `_panelAnchor`는 sub-spec 11 plan에서 **드럼 노트 패널 회전 기준** (`DrumNoteDisplayAdapter.ComputePanelRotation`이 `host.PanelAnchor.forward`를 사용)으로도 쓰이지만, **회전 계산은 forward 방향만** 사용한다. `localPosition` Y·Z를 변경해도 `localRotation`이 identity로 유지되는 한 forward 방향은 변하지 않으므로 sub-spec 11 결과물은 영향받지 않는다. localRotation은 본 plan에서 건드리지 않음. — `Read Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs (2026-05-20)` lines 52-57 + sub-spec 11 handoff
- 동일 PanelAnchor Transform이 `DrumNoteDisplayAdapter.ComputePanelPosition`의 노트 패널 *위치* 계산에는 사용되지 *않는다* — 노트 패널 위치는 별도 `worldYOverride` 또는 collider bounds 기반(sub-spec 10 handoff). 따라서 `_panelAnchor.localPosition` Y·Z 변경은 **SessionPanel 위치에만 영향**, 노트 패널 위치는 무관. — `Read Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs (2026-05-20)` (sub-spec 10 handoff 참조)
- DrumKit.prefab은 root Transform에 prefab world scale (1, 1, 1) 가정. `_panelAnchor.localScale = 0.6666667`은 패널 *위치*에는 무관하다(scale은 자식 좌표만 영향). SessionPanel은 PanelAnchor의 *world position*만 사용하며 PanelAnchor의 scale은 SessionPanel rect size에 영향 없음(SessionPanelController가 자체 prefab scale 사용). — `Read Assets/Instruments/Drum/Prefabs/DrumKit.prefab (2026-05-20)` + `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-20)`
- 본 plan은 asmdef 의존 변경 없음(C# 코드 변경 없음, prefab YAML만 수정 가능성). 직렬화 자산 수정 결정 트리(`.claude/skills/unity-mcp-workflow/SKILL.md`)에 따라 prefab 수정 시 **MCP `manage_asset` 우선**, 텍스트 직접 Edit은 plan 명시 또는 사용자 승인 선행. 본 plan은 단일 직렬 필드(`m_LocalPosition.y`, `m_LocalPosition.z`) 변경이므로 사용자 승인 하에 텍스트 Edit으로 처리해도 안전 (단일 필드는 GUID·fileID 의존 없음). — `Read Assets/Instruments/Drum/Prefabs/DrumKit.prefab (2026-05-20)` line 297

## Approach

### 1. 현재 baseline 박제 (auto-hard 1번)

Plan 실행 전 prefab의 현재 `_panelAnchor` localPosition을 grep으로 확인한다:

```
Grep "m_LocalPosition: \{x: -0.5, y: 0.9, z: 0.2\}" Assets/Instruments/Drum/Prefabs/DrumKit.prefab
```

또는 fileID `1479758216802493830`의 Transform 블록 내 `m_LocalPosition` 라인 1건이 매칭되어야 한다. 이 baseline이 sub-spec 10 plan handoff와 일치함을 확인.

### 2. Play 모드 manual 검증 (manual-hard 1번)

Editor를 열어 다음 시나리오 실행:

1. SampleScene 로드.
2. Play 모드 진입.
3. 헤드셋(또는 Editor Game View에서 XR Device Simulator로) 드럼 anchor에 텔레포트 → 활성 악기가 DrumKit로 전환되는지 확인.
4. SessionPanel을 열어 InstrumentToggleButton·DifficultyButton·SongRow 영역이 손으로 닿는 거리(약 0.4~0.6m forward, 가슴~눈 사이 높이)에 표시되는지 시각·물리 확인.
5. 패널이 너무 멀면 Z 감소(예: 0.2 → 0.0 또는 음수). 패널이 너무 낮으면 Y 증가(예: 0.9 → 1.1). 패널이 너무 가까워 시야를 가리면 Z 증가(예: 0.2 → 0.35).
6. 사용자가 만족하는 Y·Z 값을 결정.

### 3. 조정 필요 시 prefab 수정 (조건부)

manual 검증에서 조정이 필요하면 다음 중 하나로 prefab 수정:

- **MCP 사용 가능 시**: `manage_asset` 또는 `manage_gameobject`로 `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` 내 PanelAnchor (fileID `1479758216802493830`)의 Transform.localPosition.y / .z 만 변경. X(-0.5)는 유지.
- **MCP 미가용 시**: 사용자 승인 후 prefab 파일을 직접 Edit. 변경 라인은 단 1줄:
  ```yaml
  m_LocalPosition: {x: -0.5, y: <조정 Y>, z: <조정 Z>}
  ```
  (line 297 위치, fileID `1479758216802493830` Transform 블록 내)

조정이 불필요하다면(현재 `(0.9, 0.2)`가 충분히 편한 거리) 본 단계 skip하고 plan을 "PASS - 조정 불필요"로 종료.

### 4. 인스턴스 재생성 검증 (manual-hard 2번)

prefab 변경 후 (또는 변경 없이 baseline 확정 후):

1. 씬에서 DrumKit 인스턴스를 다시 한 번 배치 (또는 기존 인스턴스를 prefab apply override로 reset).
2. Play 모드 진입 → 드럼 anchor 텔레포트 → SessionPanel 열기.
3. 두 번째 인스턴스도 동일한 (조정 후) world position에 SessionPanel을 표시하는지 확인.

### 5. 사용자 검증 후 최종 값 박제 (auto-hard 2번)

조정 후의 최종 Y·Z 값(또는 조정 없이 유지 시 기존 값)을 prefab grep으로 박제. AC 라벨 [auto-hard]에 명시된 패턴이 매칭되어야 plan 종료 가능.

## Deliverables

- `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` *(조건부 수정)* — PanelAnchor Transform(fileID `1479758216802493830`)의 `m_LocalPosition` Y·Z 값을 사용자가 manual 검증을 통해 확정한 값으로 변경. 조정 불필요 시 변경 없음. X는 -0.5 유지.

## Acceptance Criteria

- [ ] `[auto-hard]` DrumKit.prefab의 PanelAnchor Transform(fileID `1479758216802493830`)의 `m_LocalPosition` 라인이 정확히 1줄 존재하며, X 성분이 `-0.5`로 유지되어 있다 (sub-spec Out of Scope 답습 — X 변경 금지).
  **검증:** `Grep "m_LocalPosition: \{x: -0.5" Assets/Instruments/Drum/Prefabs/DrumKit.prefab` 출력에 PanelAnchor 블록(line ~297 부근) 매칭 1건 이상. 또한 PanelAnchor GameObject fileID `3716779064653313039`가 매칭된 Transform fileID `1479758216802493830`과 같은 블록 내에 존재.

- [ ] `[auto-hard]` PanelAnchor Transform의 `m_LocalRotation`이 identity(`{x: -0, y: -0, z: -0, w: 1}`)로 유지되어 sub-spec 11의 드럼 노트 패널 회전 계산(forward 방향)이 깨지지 않는다.
  **검증:** `Grep -n "m_LocalRotation: \{x: -0, y: -0, z: -0, w: 1\}" Assets/Instruments/Drum/Prefabs/DrumKit.prefab` 결과 line 296 매칭(PanelAnchor 블록 내). 또한 `Read Assets/Instruments/Drum/Prefabs/DrumKit.prefab` lines 288-302로 직접 확인. + sub-spec 11 회귀: Play 모드에서 드럼 세션 시작 시 노트 패널이 여전히 anchor forward 방향을 향함(시각 1회 확인).

- [ ] `[auto-soft]` `unity-test-runner` 회귀에서 `SessionPanel.Tests` 및 `RhythmGame.Tests.Editor` 어셈블리 전체 PASS (prefab YAML 단일 라인 변경이 기존 테스트에 영향 없음 회귀 확인). MCP 미가용 시 `MCP UNAVAILABLE` 보고 후 진행.
  **검증:** `unity-test-runner` 서브에이전트 호출 결과 보고 — PASS 또는 `MCP UNAVAILABLE`.

- [ ] `[manual-hard]` Play 모드에서 SampleScene을 열어 드럼 anchor에 텔레포트한 뒤 SessionPanel을 열면, 패널이 사용자의 손이 자연스럽게 닿는 거리(약 forward 0.0~0.5m, 가슴~눈 사이 높이)에 표시되어 InstrumentToggleButton·DifficultyButton·SongRow를 손가락으로 누를 수 있다.
  **검증:** Play 진입 → 드럼 anchor 텔레포트(활성 악기 = DrumKit 확인) → SessionPanel 열기 → 헤드셋 또는 Editor Game View에서 사용자 손 위치 기준으로 패널 버튼이 닿는 거리·시야 범위에 있는지 시각·물리 확인. 너무 멀면 Z 감소, 너무 낮으면 Y 증가 방향으로 사용자가 직접 조정 후 재검증.

- [ ] `[manual-hard]` 사용자 검증·조정이 완료된 후, 같은 prefab 인스턴스를 씬에 한 번 더 추가(또는 prefab apply override reset)하고 Play 모드에서 드럼 anchor에 텔레포트 → SessionPanel 열기를 반복하면, 두 번째 인스턴스에서도 첫 번째와 동일한 world position에 패널이 표시된다.
  **검증:** SampleScene Hierarchy에 DrumKit prefab 두 번째 인스턴스 드래그 → 첫 인스턴스 위치로 이동(또는 별도 위치에 둔 채 anchor 텔레포트로 활성 전환) → SessionPanel 열어 위치 비교 → 두 인스턴스 모두 동일 local offset(`_panelAnchor.localPosition`)을 갖고 root 기준 동일 world position에 SessionPanel 표시.

- [ ] `[manual-hard]` sub-spec 11 회귀: 드럼 세션 시작 시 노트 패널 회전이 sub-spec 11 결과와 동일하게 PanelAnchor forward 방향을 향한다(_panelAnchor.localPosition Y·Z 변경이 회전에 영향 없음 확인).
  **검증:** 드럼 anchor 텔레포트 → 리듬게임 세션 시작 → 노트 패널이 PanelAnchor forward 방향(드럼 정면)을 향하고 panelTiltDegrees=0(수직)으로 표시되는지 시각 확인. 사용자(카메라)가 좌우로 이동해도 노트 패널이 회전하지 않는지 1회 확인.

## Out of Scope

- `_panelAnchor.localPosition.x` 조정 (sub-spec Out of Scope 1번 답습 — X=-0.5 유지).
- 피아노·트럼본·하프 등 다른 악기의 `_panelAnchor` 위치 변경 (sub-spec Out of Scope 2번 답습).
- SessionPanel 회전(orientation) 동작 수정 (sub-spec Out of Scope 3번 답습 — `SessionPanelController.PositionAtInstrument`의 카메라 기반 LookRotation 그대로).
- `_panelAnchor.localRotation` / `localScale` 변경 — localRotation은 sub-spec 11이 forward 방향 기준점으로 사용 중이라 identity 유지, localScale은 SessionPanel 위치에 무관하므로 변경 불필요.
- C# 코드 수정 — 본 plan은 prefab YAML 단일 라인(잠재적) 수정으로 한정.
- DrumNoteDisplayAdapter의 노트 패널 위치/회전 로직 변경 (sub-spec 10·11이 이미 처리).

## Notes

- 본 plan은 manual 검증이 핵심이며 prefab 수정은 *조건부*다. 현재 `(0.9, 0.2)` 값이 사용자가 충분히 편하게 조작 가능한 거리라면 prefab 변경 0줄로 종료 가능.
- 조정 필요 시 결정한 최종 Y·Z 값은 Handoff에 박제해 후속 plan(예: 피아노/트럼본 SessionPanel 위치 조정)이 참고할 수 있게 한다.
- X 변경 금지 룰은 sub-spec 12 Out of Scope에 명시되어 있음 — manual 검증 중 "X도 조정하고 싶다"는 판단이 나오면 본 plan에서 처리하지 않고 사용자에게 별도 sub-spec 제안.
- `SessionPanelController.PositionAtInstrument`의 회전 계산은 camera-look 기반이라 사용자가 어디서 anchor에 도달해도 패널이 사용자를 향한다 — Y·Z 조정으로 거리만 바뀌고 회전은 자동 보정됨.
- 후속 plan 후보: 피아노·트럼본도 동일 패턴으로 SessionPanel 위치 검증(별도 sub-spec).

## Handoff

- `_panelAnchor.localPosition = (-0.5, 0.9, 0.2)` 현재 값이 Play 모드 검증에서 충분히 편한 거리·높이로 확인됨. 추가 Y·Z 수정 없이 유지.
- MH-1~3 모두 pass: SessionPanel 조작 거리 적합, prefab 인스턴스 재생성 동일 위치, sub-spec 11 노트 패널 회전 불변 확인.
