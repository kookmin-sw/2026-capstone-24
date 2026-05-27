# SessionPanel snap-once 모드 — 트롬본 픽업 시 SessionPanel 위치 1회 고정

**Linked Spec:** [`13-trombone-note-display.md`](../specs/13-trombone-note-display.md)
**Caused By:** [`2026-05-21-linksky0311-trombone-note-display.md`](./2026-05-21-linksky0311-trombone-note-display.md)
**Status:** `Done`

## Goal

`SessionPanelController.LateUpdate`가 매 프레임 활성 악기의 `PanelAnchor.position`을 추적해 SessionPanel을 갱신하는 동작이, 트롬본이 머리(`mouthOnPlayer`)를 매 프레임 따라가는 특성과 결합해 SessionPanel을 트롬본과 함께 끌고 다닌다. 그 결과 선행 plan의 5개 파셜 패널(트롬본 정면 반원형 배치)이 SessionPanel에 가려져 manual-hard AC 4건이 BLOCKED 상태다. 본 plan은 `SessionPanelController`에 **snap-once 모드 SerializeField**를 추가해 `InstrumentOpened` 상태 진입 시 1회만 위치 정렬하고 이후는 추적하지 않도록 동작을 분기시켜, 트롬본을 픽업해 움직여도 SessionPanel이 픽업 시점의 world position에 고정되게 만든다. 피아노·드럼은 anchor 자체가 정적이라 1회 정렬 후 추적이 사라져도 시각 차이가 없다.

## Context

선행 plan(`2026-05-21-linksky0311-trombone-note-display.md`) 완료 후 manual-hard 4건이 모두 BLOCKED 처리됐다 — 트롬본을 grab하고 세션을 시작하면 SessionPanel이 트롬본 정면에 떠 있는 5개 파셜 패널을 시각적으로 가리거나 panel 자체가 트롬본을 따라다녀 트롬본 노트 디스플레이의 가시화·라우팅·판정 popup 위치를 확인할 수 없다.

근본 원인:

1. `SessionPanelController.LateUpdate` (lines 221~231)이 `_state == InstrumentOpened && _trackInstrument`일 때 매 프레임 `PositionAtInstrument()`를 호출. `_trackInstrument`는 `OnActiveInstrumentChanged`(line 140~161)에서 `Hidden→InstrumentOpened` 전환 시 `true`로 설정되며, 그 이후 `Hidden`으로 전환하기 전까지 매 프레임 추적이 지속된다.
2. `PositionAtInstrument()` (line 344~364)는 `Transform anchor = _provider.Current.PanelAnchor` (= 트롬본의 `_panelAnchor` = `tromboneRoot` Transform)의 `position`을 그대로 `_panelInstance.transform.position`에 대입.
3. 트롬본의 `_panelAnchor` 슬롯은 선행 plan에서 `tromboneRoot`로 wiring됨 (`Trombone.prefab` line 969: `_panelAnchor: {fileID: 9100000000000000002}`).
4. `TromboneAnchor.LateUpdate` (lines 158~164)는 attach 상태에서 `tromboneRoot`를 `mouthOnPlayer`(player head) 기준으로 매 프레임 정렬. 즉 player가 머리를 움직이면 `tromboneRoot.position`이 매 프레임 갱신.
5. (1)+(4) 결합으로 SessionPanel = `tromboneRoot.position`이 매 프레임 player head를 따라다니며, 5개 파셜 패널은 `tromboneRoot` 자식이므로 동일하게 따라다닌다 — 결과적으로 player가 어느 방향으로 보든 SessionPanel이 늘 파셜 패널과 겹치는 영역에 떠 있다.

해법 선정 — 3개 옵션 검토 후 **옵션 A 채택**:

- **옵션 A (선택)** `SessionPanelController`에 `snapOnce` SerializeField 추가. `InstrumentOpened` 진입 시 1회 위치 정렬 후 `_trackInstrument = false`로 즉시 끔. SessionPanel의 instrument 추적은 *스폰 시점 1회 고정* 의미로 변경. **사용자 요청 그대로** 답습.
- 옵션 B (기각) trombone-특정 분기를 `SessionPanelController`에 추가 — `_provider.Current.InstrumentId == "Trombone"`이면 추적 끔. 한 컴포넌트가 특정 instrumentId 문자열을 알아야 하므로 추후 신규 악기(하프 등)에서 같은 문제 재발 시 분기가 부풀어 오른다. 컨트롤러-악기 결합도 상승.
- 옵션 C (기각) `IActiveInstrument`에 `bool TrackPanel { get; }` 추가 + `InstrumentBase` 기본 true + `Trombone`만 false override. 가장 깔끔하나 인터페이스 surface가 늘어나며, `Assets/SessionPanel/Tests/DummyActiveInstrument.cs` / `RhythmGameSectionInstrumentToggleTests.StubInstrument` / `RhythmGameSectionRefreshTests.StubInstrument` 3개 stub 모두 신규 멤버 구현 필요 — 회귀 수정 부담이 크다.

옵션 A의 안전성 검토:

- piano/drum 검증: 두 악기 모두 `_panelAnchor`가 prefab 정적 child Transform이고 player를 따라다니는 attach 동작이 없다 — `TromboneAnchor.AttachTromboneToMouth`/`AlignTromboneToMouth`와 같은 head-tracking 로직이 piano·drum에 없음(piano는 anchor 텔레포트 위치에 고정, drum도 `DrumKit.prefab` 정적). 따라서 `snapOnce=true` 디폴트에서도 piano/drum의 SessionPanel 위치는 매 프레임 동일 결과 → 시각 변화 0.
- 시퀀스 검증: `TromboneAnchor.AttachTromboneToMouth`는 attach 시점에 `AlignTromboneToMouth()`를 직접 1회 호출(line 136)한 후 `m_IsAttached = true`로 설정. `OnActiveInstrumentChanged` 발화 시점에는 이미 `tromboneRoot`가 player mouth 위치로 정렬된 상태 → SessionPanel snap-once의 1회 위치 결정은 *attach 직후 정렬된 위치*로 박힌다. player가 그 위치에서 머리를 움직여도 SessionPanel은 거기 그대로 있다.
- `Hidden→InstrumentOpened`로 다시 들어올 때(악기 잠시 놓았다가 다시 잡는 경우)는 OnActiveInstrumentChanged가 다시 `_trackInstrument=true`로 set하고 LateUpdate가 1회 정렬 후 다시 false로 끄므로 매번 *재픽업 시점* 위치로 갱신된다. 즉 픽업할 때마다 SessionPanel 위치가 그 순간의 트롬본 위치로 재고정되어 자연스럽다.

본 plan은 또한 선행 plan의 manual-hard AC 4건이 본 plan 적용 후 재검증되어 통과함을 마지막 manual-hard AC로 묶어 회수한다.

## Verified Structural Assumptions

- `SessionPanelController.LateUpdate` (lines 221~231)는 `_state == InstrumentOpened && _trackInstrument` 조건 시 매 프레임 `PositionAtInstrument()` 호출. `_trackInstrument`는 멤버 bool로, `OnActiveInstrumentChanged` (lines 140~161) + `TransitionTo` (lines 174~207)에서 토글된다. — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-21)` lines 140-231
- `SessionPanelController.PositionAtInstrument` (lines 344~364)는 `_provider.Current.PanelAnchor.position`을 `_panelInstance.transform.position`에 1:1 대입, 회전은 camera 기준 LookRotation. 단일 진입점이며 X·Y·Z 모두 anchor와 동일. — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-21)` lines 344-364
- `Trombone.prefab` 의 `InstrumentBase._panelAnchor` 슬롯: `_panelAnchor: {fileID: 9100000000000000002}` (line 969), 이는 `tromboneRoot` Transform과 동일 fileID (`TrombonePartialController.tromboneRoot: 9100000000000000002` line 1000, `TromboneAnchor.tromboneRoot: 9100000000000000002` line 1541, `TromboneSlideController.tromboneRoot: 9100000000000000002` line 2653 모두 일치). 즉 SessionPanel anchor = `tromboneRoot`. — `Grep _panelAnchor|tromboneRoot Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-21)`
- `TromboneAnchor.AttachTromboneToMouth` (lines 126~144)는 attach 진입 시 `tromboneRoot.position/rotation`을 캐시한 직후 `AlignTromboneToMouth()`를 1회 호출(line 136)해 즉시 player mouth 위치로 정렬. `LateUpdate` (lines 158~164)는 `m_IsAttached`일 때만 매 프레임 `AlignTromboneToMouth()` 재호출. → attach 후 매 프레임 `tromboneRoot.position`이 player head 추적. — `Read Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs (2026-05-21)` lines 126-179
- `InstrumentBase.PanelAnchor` 게터는 `_panelAnchor != null ? _panelAnchor : transform` (line 56). 트롬본은 `_panelAnchor`가 wiring되어 있으므로 `tromboneRoot` Transform이 반환. — `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-21)` line 56
- piano/drum는 `_panelAnchor`가 정적 child Transform이며 player head를 따라가는 attach 로직이 없다 — `DrumKit.prefab`의 PanelAnchor는 fileID `1479758216802493830`(`localPosition (-0.5, 0.9, 0.2)`, sub-spec 12 plan handoff 답습), `Piano.prefab`의 _panelAnchor도 정적 child Transform. attach 시 root를 player head로 정렬하는 컴포넌트는 트롬본에만 존재. → snapOnce 디폴트 true에서도 piano/drum 시각 변화 0. — `Read docs/specs/_archive/rhythm-game/plans/2026-05-20-linksky0311-drum-session-panel-position-fix.md (2026-05-21)` lines 32-41 (sub-spec 12 plan handoff) + `Grep tromboneRoot|mouthOnPlayer Assets/Instruments/Piano/Prefabs (2026-05-21)` 0건
- `SessionPanelController`의 다른 호출자 `TransitionTo` (lines 163~208)는 `InstrumentOpened` 분기에서 `_trackInstrument = true` (line 190)로 set 후 `PositionAtInstrument()`를 직접 호출(line 194 또는 `ShowPanelAndInteractorsDelayed` line 215). 즉 진입 시 1회 정렬은 이미 보장됨. 본 plan의 snapOnce는 *그 직후* LateUpdate 추적을 끄는 것으로 충분. — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-21)` lines 163-219
- `Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef`은 `Instruments` / `RhythmGame.Data` / `RhythmGame.Runtime` / `Unity.InputSystem` / `Unity.XR.Interaction.Toolkit` / `Unity.TextMeshPro` 참조. 본 plan은 신규 import 0건(컨트롤러 내부 bool 필드 추가만), asmdef reference 추가 불필요. — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-21)`

## Approach

1. **`SessionPanelController`에 `snapOnce` SerializeField 추가** — `Assets/SessionPanel/Scripts/SessionPanelController.cs`. `panelToggleAction` 근처 인스펙터 그룹(line 25 근방)에 다음 1줄 추가:
   ```csharp
   [Tooltip("true이면 InstrumentOpened 진입 시 1회만 위치 정렬하고 이후 매 프레임 추적하지 않는다. 트롬본처럼 player head를 따라가는 악기에서 SessionPanel이 함께 끌려다니는 것을 방지한다.")]
   [SerializeField] private bool snapOnce = true;
   ```
   기본값 `true` — 모든 SessionPanelController 인스턴스가 새 동작을 채택. piano/drum은 anchor 정적이라 시각 변화 0(Verified Structural Assumptions 박제). 만약 후속 사용자가 "트럼본이 아닌 어떤 악기에서 anchor 추적이 필요해진다"고 결정하면 Inspector에서 false로 토글 가능.

2. **`OnActiveInstrumentChanged` / `TransitionTo` 분기 정리** — `OnActiveInstrumentChanged` (line 140~161) 안 `_trackInstrument = true` (line 146)와 `TransitionTo`의 `InstrumentOpened` 분기 `_trackInstrument = true` (line 190)는 그대로 둔다. 본 plan은 *LateUpdate 안에서 1회 정렬 후 끄는* 패턴을 채택해 진입 시점의 `PositionAtInstrument` 호출을 깨지 않는다.

3. **`LateUpdate` 안 snap-once 분기 추가** — 현재 (lines 221~231):
   ```csharp
   private void LateUpdate()
   {
       if (_panelInstance == null || !_panelInstance.activeSelf)
       {
           if (_hitDot != null) _hitDot.SetActive(false);
           return;
       }
       if (_state == PanelState.InstrumentOpened && _trackInstrument)
           PositionAtInstrument();
       UpdateHitDot();
   }
   ```
   를 다음으로 변경:
   ```csharp
   private void LateUpdate()
   {
       if (_panelInstance == null || !_panelInstance.activeSelf)
       {
           if (_hitDot != null) _hitDot.SetActive(false);
           return;
       }
       if (_state == PanelState.InstrumentOpened && _trackInstrument)
       {
           PositionAtInstrument();
           if (snapOnce)
               _trackInstrument = false;  // 1회 정렬 후 추적 중단 — 악기가 이후 이동해도 패널은 고정.
       }
       UpdateHitDot();
   }
   ```
   `snapOnce=true`이면 LateUpdate가 1회 `PositionAtInstrument` 실행 후 `_trackInstrument=false`로 즉시 끔. 이후 LateUpdate는 anchor를 다시 읽지 않으며 SessionPanel은 그 위치에 고정된다. `snapOnce=false`이면 기존 동작(매 프레임 추적) 그대로.

4. **재픽업 시퀀스 보장 확인** — 트롬본을 놓고(`OnActiveInstrumentChanged(null)` → `Hidden` 전환, line 156~160) 다시 잡으면(`OnActiveInstrumentChanged(instrument != null)` → `Hidden→InstrumentOpened`) 다시 `_trackInstrument=true`로 세트되고 LateUpdate가 새 위치에서 1회 정렬 후 끄는 흐름이 자연스럽게 동작. 추가 코드 불필요 — 기존 state machine을 그대로 활용.

5. **컴파일·테스트 게이트** — 위 변경 후 Unity 컴파일 대기(`unity-mcp-workflow` skill `## Script Management` 절차) → `read_console` errors 0 확인 → `unity-test-runner` sub-agent 1회 호출. 본 plan은 `SessionPanelController` 내부 필드·LateUpdate 1줄 추가만으로 API surface 변경 0이라 기존 `SessionPanel.Tests` / `RhythmGame.Tests.Editor` 어셈블리 회귀 0 예상. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 진행.

6. **scene SessionPanelController 인스턴스 SerializeField 확인** — `TestSceneSanyo`의 SessionPanelController GameObject inspector에서 `snapOnce` 필드 기본값 `true`로 자동 직렬화되는지 확인(코드 기본값이 SerializeField 초기 직렬화 값으로 적용됨). 씬 인스턴스가 이미 존재해 직렬화된 상태라면 신규 필드는 디폴트 true로 자동 채워진다(Unity SerializeField 기본 동작). MCP `find_gameobjects search_term="SessionPanelController" search_method=by_component`으로 인스턴스 1개 확인 후, 인스턴스 inspector 값이 의도와 다르면 사용자에게 inspector 토글 요청.

7. **수동 재현·재검증** — `TestSceneSanyo`에서 Play 모드 진입 → 트롬본 anchor 텔레포트 → 트롬본 attach → SessionPanel이 트롬본 앞에 1회 뜬 뒤 그 위치에 고정되어 player head를 움직여도 따라오지 않음을 확인. 이후 선행 plan AC manual-hard 4건(파셜 패널 5종 가시화, 파셜 라우팅, JudgmentPopup 라우팅, 비-트롬본 세션에서 트롬본 패널 미표시)을 차례로 재검증.

## Deliverables

- `Assets/SessionPanel/Scripts/SessionPanelController.cs` — `snapOnce` SerializeField 1개 추가(디폴트 `true`), `LateUpdate` 안에 `if (snapOnce) _trackInstrument = false;` 1줄 추가. 그 외 변경 없음. API 표면 0개 추가.

## Acceptance Criteria

- [ ] `[auto-hard]` `SessionPanelController.cs`에 `snapOnce` SerializeField가 존재하며 기본값이 `true`다.
  **검증:** `Grep -n "SerializeField\] private bool snapOnce" Assets/SessionPanel/Scripts/SessionPanelController.cs` 결과 1줄 + `Grep -n "snapOnce = true" Assets/SessionPanel/Scripts/SessionPanelController.cs` 결과 1줄.

- [ ] `[auto-hard]` `LateUpdate` 안에 `if (snapOnce) _trackInstrument = false;` (또는 동등 효과의 1줄)가 `PositionAtInstrument()` 호출 직후에 위치한다.
  **검증:** `Grep -n -A 3 "PositionAtInstrument\\(\\);" Assets/SessionPanel/Scripts/SessionPanelController.cs` 출력 안에 `LateUpdate` 블록 내 `_trackInstrument = false` 라인이 동반됨을 확인(라인 순서: `PositionAtInstrument()` → `if (snapOnce)` → `_trackInstrument = false`).

- [ ] `[auto-hard]` 변경 후 Unity 컴파일 errors 0이며 `SessionPanel.Tests` / `RhythmGame.Tests.Editor` 어셈블리 회귀 PASS. 본 plan은 API surface 추가 0개라 기존 stub(`DummyActiveInstrument`/`StubInstrument`) 수정 불필요.
  **검증:** `unity-test-runner` sub-agent 1회 호출 → PASS 보고. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 본 AC `pass(skip)` 처리.

- [ ] `[auto-soft]` `TestSceneSanyo`의 SessionPanelController 인스턴스 inspector 직렬화 값에서 `snapOnce`가 `true`로 자동 채워진다(코드 기본값 적용). 인스턴스가 이미 존재해도 신규 SerializeField는 Unity 직렬화 규칙상 기본값 적용.
  **검증:** Unity Editor 열어 `TestSceneSanyo` 로드 → Hierarchy에서 `SessionPanelController` 컴포넌트 부착 GameObject 선택 → Inspector에서 `Snap Once` 체크박스 상태 시각 확인. MCP `find_gameobjects search_term="SessionPanelController" search_method=by_component`로 인스턴스 1개 식별 후 리소스 `mcpforunity://scene/gameobject/{id}/components`로 직렬화 값 확인. 의도와 다르면 사용자에게 inspector 토글 요청. MCP 미가용 시 Notes에 기록 후 skip.

- [ ] `[manual-hard]` Play 모드 진입 → 트롬본 anchor 텔레포트 → 트롬본 attach 직후 SessionPanel이 트롬본 정면에 1회 위치 결정된 뒤, player가 머리를 좌/우/위/아래로 움직여 트롬본 본체가 head를 따라가도 SessionPanel은 attach 시점의 world position에 그대로 *고정*되어 움직이지 않는다.
  **검증:** Editor Play → XR Device Simulator 또는 헤드셋으로 트롬본 anchor 텔레포트 → 트롬본 attach 확인 → camera/head를 yaw/pitch로 ±30° 정도 회전 → SessionPanel world position이 1프레임 차이 미만으로 변화 없음을 Scene 뷰 + Hierarchy의 `SessionPanel(Clone)` transform.position 값으로 시각 확인.

- [ ] `[manual-hard]` 선행 plan(`2026-05-21-linksky0311-trombone-note-display.md`)의 manual-hard 4건 — (1) 5개 파셜 패널 반원형 가시화, (2) 파셜별 노트 라우팅(파셜 0 노트는 가장 왼쪽 패널, 파셜 4 노트는 가장 오른쪽 패널), (3) JudgmentPopup이 노트가 떨어진 파셜 패널에만 표시, (4) 비-트롬본(피아노 등) 세션에서 트롬본 파셜 패널 미표시 — 이 본 plan 적용 후 재검증에서 통과한다.
  **검증:** 본 plan 적용 후 (a) `_devtest-trombone-1.vmsong` 또는 트롬본 트랙이 든 차트로 세션 진입 → 5패널 가시 + 파셜 라우팅 + JudgmentPopup 위치 모두 시각 확인, (b) 피아노 grab 후 피아노 차트 세션 진입 → 트롬본 파셜 패널이 보이지 않음 확인. 선행 plan의 AC manual 시나리오 4건을 그대로 답습 — SessionPanel이 더 이상 트롬본을 따라다니지 않으므로 시각 차단 없이 재현 가능해야 한다.

- [ ] `[manual-hard]` 회귀 — 피아노 또는 드럼 anchor에 텔레포트해 SessionPanel을 띄우면 기존(`snapOnce` 도입 전)과 동일한 위치·동작을 보인다. piano/drum은 anchor 자체가 정적이므로 snapOnce=true 디폴트에서도 SessionPanel 위치 변화 0.
  **검증:** Editor Play → 피아노 anchor 텔레포트 → SessionPanel 위치·조작 가능성 확인 → 드럼 anchor 텔레포트 → SessionPanel이 sub-spec 12 plan에서 박제된 위치(`_panelAnchor.localPosition = (-0.5, 0.9, 0.2)`)에 표시되며 InstrumentToggleButton/DifficultyButton 조작 가능 확인.

## Out of Scope

- `IActiveInstrument` 인터페이스 surface 추가 (옵션 C 기각). 후속 plan에서 신규 악기가 SessionPanel 추적 정책을 instrument별로 분기해야 할 경우 별도 spec으로 검토.
- 트롬본-특정 분기(`InstrumentId == "Trombone"`)를 컨트롤러에 도입(옵션 B 기각).
- `SessionPanelController.PositionAtInstrument` 의 회전 계산(`Quaternion.LookRotation(awayFromCam)`) 수정 — 본 plan은 위치 추적만 끄고 회전 로직은 그대로 둠. 회전은 LateUpdate를 통하지 않는 진입 시점 1회 계산이라 추가 변경 불필요.
- `_panelAnchor` 슬롯을 트롬본 prefab에서 다른 child(예: trombone mouthpiece 근처 신규 PanelAnchor)로 교체. 선행 plan Notes에서 후속 plan 후보로 박제됨 — 본 plan은 그 자체를 건드리지 않는다.
- 5개 파셜 패널 자체의 회전·크기·X 오프셋 시각화 변경 — 선행 plan Out of Scope 답습.
- `_devtest-trombone-1.vmsong` 정리 / 정식 트롬본 차트 제작 — 선행 plan Out of Scope 답습.
- 트롬본 세션 중 SessionPanel 재표시(현재 `OnRhythmGameStarted`에서 `_hiddenByGame = true` + `SetActive(false)`로 이미 숨김 — `GameEnded`에서 복원). 본 plan은 게임 중 패널 표시 정책을 변경하지 않는다.

## Notes

- `snapOnce` 디폴트 `true`는 piano/drum에 시각 변화 0(Verified Structural Assumptions 박제)이며, 트롬본만 head-tracking 부작용이 사라진다. 만약 후속 사용자 피드백에서 "piano/drum도 매 프레임 추적이 필요한 시나리오"가 발견되면 inspector에서 false로 토글하거나 instrument-scoped 분기를 후속 plan에서 도입.
- 옵션 C(`IActiveInstrument.TrackPanel`) 기각 사유 박제: 3개 stub 수정 부담 + 인터페이스 surface 영구 추가. 옵션 A는 SessionPanelController 내부 변경만으로 회수 가능. 후속에 일부 악기는 추적, 일부는 snap이 진짜로 필요해지면 그때 옵션 C로 마이그레이션 — `snapOnce` SerializeField는 그 시점에 deprecated 처리하거나 instrument별 override로 확장.
- 본 plan은 SessionPanelController API surface 추가 0개로 회귀 비용 최소. 세션·인스펙터·prefab·테스트 어디에도 새 멤버 노출 없음(SerializeField는 *멤버*지만 외부 호출자 없음).
- 트롬본 attach 직후 1회 정렬 위치는 `tromboneRoot`가 이미 mouth-aligned 된 상태(`TromboneAnchor.AttachTromboneToMouth` line 136에서 `AlignTromboneToMouth()` 직접 호출 후 `m_IsAttached=true` set)이므로 player가 정상 자세에서 트롬본을 잡았다면 SessionPanel도 그 자세 기준으로 박힌다.
- 후속 plan 후보: (a) `_panelAnchor`를 trombone mouthpiece 근처 신규 child로 분리해 SessionPanel이 트롬본 본체와 다른 위치(player view 가장자리 등)에 뜨도록 조정 — 선행 plan Notes 답습, (b) 트롬본 외 신규 sustained/attached 악기 도입 시 동일 snap-once 정책 일괄 검증, (c) 옵션 C(`IActiveInstrument.TrackPanel`)로 인터페이스화 — 추적 정책을 악기별로 의도 박제할 필요가 생기는 시점.

## Handoff

[2026-05-27 완료] `SessionPanelController.snapOnce = true`(디폴트) SerializeField 추가, LateUpdate에서 `PositionAtInstrument()` 1회 실행 후 `_trackInstrument = false`. 피아노/드럼 시각 변화 0 확인. 선행 plan(trombone-note-display) manual-hard 4건 사용자 재확인 통과.
