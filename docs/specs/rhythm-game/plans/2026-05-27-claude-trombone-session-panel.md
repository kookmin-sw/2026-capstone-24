# Trombone SessionPanel 진입·토글·이탈 자동 닫힘 검증 plan

**Linked Spec:** [`14-trombone-session-panel.md`](../specs/14-trombone-session-panel.md)
**Status:** `Done`

## Goal

트롬본 anchor에 텔레포트한 상태에서 **왼손 메뉴 입력(menuButton)**으로 SessionPanel을 열고 닫을 수 있으며, anchor 이탈 시 SessionPanel이 자동으로 닫히는 동작이 **이미 구현된 wiring**(`Trombone.prefab._panelAnchor`, `InstrumentTeleportLink.linkedInstrument`, `SessionPanelController.snapOnce`, `InputSystem_Actions.PanelToggle`)을 통해 작동함을 박제·검증한다. 검증 과정에서 누락된 wiring 또는 시각적 회귀가 발견되면 *그것만* 보완한다. 본 plan은 구조 변경이 아닌 **end-to-end 동작 확인 + 누락 시 minimal fix** plan이다.

## Context

Sub-spec 14는 트롬본에 SessionPanel이 다른 악기와 동일한 진입 경험을 갖도록 wiring을 요구한다. 그런데 spec 문맥 작성 이후 누적된 plan·commit들이 이미 다음 wiring을 모두 박제한 상태다:

1. **선행 plan `13-trombone-note-display` (Done)**: Trombone.prefab의 `_panelAnchor` 슬롯에 PanelAnchor child Transform 박제(현재 fileID `5307817778198693025`), `InstrumentTeleportLink.linkedInstrument`에 Trombone InstrumentBase(fileID `6350044881741792298`) 박제.
2. **선행 plan `2026-05-21-linksky0311-trombone-session-panel-snap-once.md` (Done)**: `SessionPanelController.snapOnce` SerializeField(디폴트 true) + LateUpdate 1줄 추가 — 트롬본이 head를 따라가도 SessionPanel은 *attach 시점 위치에 고정*. `TestSceneSanyo.unity` 인스턴스에 `snapOnce: 1`로 직렬화(line 7117).
3. **commit `f17a437` (2026-05-27)**: `PanelAnchor`를 Trombone root 직속 → `Rig`(=`tromboneRoot`, fileID `9100000000000000002`) 자식으로 reparent. 이로써 trombone attach 시 PanelAnchor가 mouth 위치를 따라가게 되어, snap-once 시점의 패널 위치가 사용자 mouth 근처에 박힌다.
4. **`InputSystem_Actions.inputactions`**: `PanelToggle` action이 LeftHand menuButton + LeftHand secondaryButton + Keyboard `m`에 binding됨(lines 489, 500, 511).
5. **`TestSceneSanyo.unity` SessionPanelController**: `_activeInstrumentProviderObject: 1368118598` (TeleportInstrumentProvider, line 6617), `panelToggleAction: 052faaac586de48259a63d0c4782560b` (line 7115).

`OnActiveInstrumentChanged` 동작(`SessionPanelController.cs` lines 144-151)도 spec 요건과 일치한다:
- "anchor 전환은 패널을 자동으로 열지 않는다" → spec "패널을 호출할 수 있는 상태가 된다"와 부합 (자동 오픈 아님, 사용자가 명시적 입력으로 오픈).
- "이미 열려 있던 패널은 닫아 이전 위치 잔류를 막고" → spec "트롬본 anchor에서 이탈하면 SessionPanel도 닫힌다"와 부합. 비악기 구역으로 텔레포트 시 `InstrumentTeleportLink.linkedInstrument == null` → `AnyAnchorTeleported(null)` → `TeleportInstrumentProvider.Current = null` → `ActiveInstrumentChanged(null)` → SessionPanelController가 `_state != Hidden`이면 즉시 Hidden 전환.

해법 — **검증 우선, 발견 시 minimal fix**:

본 plan은 wiring 변경을 *최소 0건*으로 잡고 시작해, manual 검증 시 누락이 드러난 경우에만 그 한 항목을 보완한다. 가능한 누락 후보:
- (A) `panelToggleAction.action.Enable()` 호출은 `OnEnable`에서만 발생 — XR 디바이스가 늦게 enable되거나 action map이 disabled 상태면 input이 안 들어올 수 있음. 검증 시 `read_console`로 input warning 확인.
- (B) 트롬본 `_panelAnchor`가 mouth-aligned 상태일 때 `Quaternion.LookRotation` 회전 계산이 부자연스러운 각도가 나올 가능성. SessionPanelController는 회전을 camera 기준으로 계산하므로 anchor의 rotation은 무관 — *위치만* 1:1 대입.
- (C) 트롬본 anchor에서 다른 트롬본 아닌 anchor로 텔레포트 시 `ActiveInstrumentChanged`가 정상 발화 → SessionPanel 자동 close. 만약 비악기 구역에 `InstrumentTeleportLink`가 부착되지 않았으면(linkedInstrument=null 발화 안 됨) 자동 close가 안 됨.

(C)는 scene 차원 wiring 검증이 필요하므로 본 plan에서 manual-hard로 박제하며, 누락 시 본 plan에서 비악기 anchor에 `InstrumentTeleportLink(linkedInstrument=null)` 추가하는 보완을 수행한다.

## Verified Structural Assumptions

- `Trombone.prefab` 의 `InstrumentBase` 직렬화 슬롯: `instrumentId: Trombone` (line 990), `_panelAnchor: {fileID: 5307817778198693025}` (line 992). PanelAnchor GameObject(fileID `3770232664706562036`, name `PanelAnchor`, line 1141)는 Transform(fileID `5307817778198693025`)의 `m_Father: {fileID: 9100000000000000002}` (line 1160)으로 `Rig`(=`tromboneRoot`)의 자식. `m_LocalPosition: (0, 0, 0)`, `m_LocalRotation: y=-0.5, w=0.8660254`(-60° yaw 틸트). — `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-27)` lines 990-992, 1131-1161
- `Trombone.prefab` 의 anchor TeleportationAnchor(`TromboneAnchor` GameObject, fileID `4560918512242688054`, line 1467-1487)에 `InstrumentTeleportLink`(MonoBehaviour fileID `178898607786906297`) 부착. `linkedInstrument: {fileID: 6350044881741792298}` (line 1658) → 같은 prefab 안 Trombone MonoBehaviour(GameObject `3300842808265667475`, m_EditorClassIdentifier `Instruments::Instruments.Trombone`, line 985)와 일치. — `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-27)` lines 974-995, 1467-1658
- `InstrumentBase.PanelAnchor` 게터(`Assets/Instruments/_Core/Scripts/InstrumentBase.cs` line 62): `_panelAnchor != null ? _panelAnchor : transform`. 트롬본은 `_panelAnchor` 박제되어 있어 PanelAnchor Transform 그대로 반환. PanelAnchor의 부모가 `Rig` (commit `f17a437`)이므로 trombone attach 시 mouth 위치 추적. — `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-27)` line 62 + `git show f17a437` (2026-05-27)
- `TromboneAnchor.AttachTromboneToMouth` (`Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs` lines 148-166)는 attach 진입 시 (1) `m_CachedRestorePosition/Rotation = tromboneRoot.position/rotation` 캐시, (2) `AlignTromboneToMouth()` 즉시 호출(line 158)로 1프레임 중간 상태 없이 mouth-align, (3) `m_IsAttached = true` (line 165). `LateUpdate` (lines 180-186)는 `m_IsAttached`일 때만 매 프레임 `AlignTromboneToMouth()` 재호출. → attach 후 Rig + PanelAnchor가 player head 따라 매 프레임 이동. — `Read Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs (2026-05-27)` lines 148-203
- `SessionPanelController.OnPanelToggle` (`Assets/SessionPanel/Scripts/SessionPanelController.cs` lines 127-142): `Hidden` 상태에서 `_provider.Current != null`이면 `InstrumentOpened` 전환, `null`이면 `PinchOpened` 전환. `PinchOpened`/`InstrumentOpened` 상태에서 다시 호출되면 `Hidden`으로 전환(토글 닫힘). — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-27)` lines 127-142
- `SessionPanelController.OnActiveInstrumentChanged` (lines 144-151): "anchor 전환은 패널을 자동으로 열지 않는다. 이미 열려 있던 패널은 닫아 ... 사용자가 panelToggleAction(pinch)으로 명시적으로 열 때만 표시된다." 즉 `_state != Hidden`이면 즉시 `Hidden`으로 전환 — spec 14의 "anchor 이탈 시 자동 닫힘" 동작이 이 코드 경로로 달성됨(이탈 = ActiveInstrument 변경). — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-27)` lines 144-151
- `SessionPanelController.PositionAtInstrument` (lines 342-356): `_panelInstance.transform.position = _mainCamera.transform.position + horizontalForward * instrumentSpawnDistance` (line 353-354), `rotation = Quaternion.LookRotation(horizontalForward)` (line 355). **anchor의 position을 직접 1:1 대입하는 식이 아니라 camera forward 기준 `instrumentSpawnDistance`(디폴트 1m) 앞**에 패널을 둠. (※ snap-once plan handoff에서 박제된 식과 다름 — 현재 코드는 camera-relative 식.) → trombone PanelAnchor의 위치/회전은 SessionPanel 위치 계산에 무관, **카메라 위치·forward만 사용**. — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-27)` lines 342-356
- `SessionPanelController.snapOnce` (line 29, 디폴트 true): InstrumentOpened 진입 시 `LateUpdate`에서 1회 `PositionAtInstrument` 호출 후 `_trackInstrument = false`로 추적 중단(lines 218-223). `TestSceneSanyo.unity` SessionPanelController 인스턴스(GameObject `2039824624`, MonoBehaviour `2039824626`, line 7106)에서 `snapOnce: 1` 직렬화 확정(line 7117). — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-27)` lines 28-31, 211-225 + `Read Assets/Scenes/TestSceneSanyo.unity (2026-05-27)` lines 7106-7118
- `TeleportInstrumentProvider` (`Assets/Instruments/_Core/Scripts/TeleportInstrumentProvider.cs`)는 `InstrumentTeleportLink.AnyAnchorTeleported` 정적 이벤트를 구독(OnEnable line 21), payload `InstrumentBase`가 직전 Current와 다르면 `ActiveInstrumentChanged?.Invoke(_current)` 발화(lines 29-37). `TestSceneSanyo.unity`에 `TeleportInstrumentProvider` 1개 존재(MonoBehaviour fileID `1368118598`, m_EditorClassIdentifier `Instruments::Instruments.TeleportInstrumentProvider`, line 6617). SessionPanelController가 `_activeInstrumentProviderObject: 1368118598` (line 7113)로 이 인스턴스를 explicit-wired. — `Read Assets/Instruments/_Core/Scripts/TeleportInstrumentProvider.cs (2026-05-27)` + `Read Assets/Scenes/TestSceneSanyo.unity (2026-05-27)` lines 6606-6618, 7113
- `InstrumentTeleportLink.OnTeleporting` (`Assets/Instruments/_Core/Scripts/InstrumentTeleportLink.cs` line 44-47): `BaseTeleportationInteractable.teleporting` UnityEvent에 등록(`OnEnable` line 35) → 텔레포트 완료 시 `AnyAnchorTeleported?.Invoke(linkedInstrument)` 정적 이벤트 발화. `linkedInstrument` null이면 비악기 구역으로 해석되어 Provider.Current=null. — `Read Assets/Instruments/_Core/Scripts/InstrumentTeleportLink.cs (2026-05-27)` lines 32-47
- `InputSystem_Actions.inputactions` 의 `PanelToggle` action(`Assets/Settings/Input/InputSystem_Actions.inputactions` lines 89-98)에 3개 binding: `<XRController>{LeftHand}/menuButton` (line 489), `<XRController>{LeftHand}/secondaryButton` (line 500), `<Keyboard>/m` (line 511). **왼손 menuButton이 명시적으로 binding됨** — spec 14의 "왼손 메뉴 입력" 요건 정확 일치. — `Read Assets/Settings/Input/InputSystem_Actions.inputactions (2026-05-27)` lines 89-98, 485-518
- `SessionPanel.Runtime.asmdef` (`Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef`)의 references: `Instruments`, `RhythmGame.Data`, `RhythmGame.Runtime`, `Unity.InputSystem`, `Unity.XR.Interaction.Toolkit`, `Unity.TextMeshPro`. 본 plan은 신규 C# 추가 0건이므로 asmdef reference 추가 불필요. 검증 시 누락 보완이 발생해도 `InstrumentTeleportLink` 추가 부착(scene 단)이라 asmdef 변경 없음. — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-27)`
- 트롬본 인스턴스는 `TestSceneSanyo.unity`에 PrefabInstance(line 7421-7511, `m_SourcePrefab: guid fc52e515dd232414db9528626212a1da`)로 배치, world position override `(2.711, 0.817, 0.888)` (line 7447-7457). `m_AddedComponents: []` (line 7510) → scene-level component 추가 없음. `_panelAnchor` override 없음(scene grep `_panelAnchor` 결과 0건) → prefab 박제값 그대로 사용. — `Read Assets/Scenes/TestSceneSanyo.unity (2026-05-27)` lines 7421-7511 + `Grep _panelAnchor Assets/Scenes/TestSceneSanyo.unity (2026-05-27)` 0건

## Approach

### 1. Static wiring 검증 (auto-hard 3건)

코드·prefab·scene wiring이 spec 14 요건을 만족하는지 Grep 기반으로 박제한다. 모든 검증 grep이 1건 이상 매칭하면 본 단계 PASS. 0건이 나오는 항목이 있으면 sub-section 5(누락 보완)로 이동.

### 2. Input action runtime 검증 (auto-soft 1건)

Play 모드 진입 직후 `read_console`에서 `PanelToggle` action 또는 InputSystem 관련 warning/error가 0건임을 확인. action map이 enabled 상태인지 SessionPanelController.OnEnable이 1회 호출되었는지를 `read_console` log filter로 확인(SessionPanelController가 자체 로그를 찍지 않으므로 컴파일 error 0건 + Play 진입 시 NullReferenceException 0건으로 간접 확인).

### 3. Manual 동작 검증 (manual-hard 3건, spec Behavior 3-tuple 직답)

`TestSceneSanyo` Play 모드에서 spec 14의 3개 Given/When/Then 시나리오를 그대로 재현:

- **MH-1 (오픈)**: 트롬본 anchor 텔레포트 → 왼손 menuButton 발화 → SessionPanel이 카메라 정면 약 1m(`instrumentSpawnDistance=1`) 거리에 오픈. snap-once 디폴트 true이므로 1회 위치 결정 후 player가 머리를 움직여도 패널은 그 위치에 고정. **주의**: 현재 `PositionAtInstrument`는 trombone PanelAnchor 위치를 *직접 사용하지 않고* camera-forward+1m로 패널을 둔다(Verified Assumptions 박제). 즉 패널이 "트롬본의 패널 앵커 위치"가 아니라 "카메라 정면 1m"에 뜬다. 이는 spec 14 What "트롬본의 패널 앵커 위치에 나타난다"와 *문구상* 차이가 있으나, 다른 악기(piano/drum)도 동일 식을 쓰고 있어 spec 14의 의도는 "사용자가 자연스럽게 조작 가능한 거리·각도"로 해석. manual 검증에서 사용자가 만족하면 PASS.

- **MH-2 (토글 닫힘)**: SessionPanel이 열린 상태에서 다시 왼손 menuButton 발화 → 패널이 즉시 닫힘(`TransitionTo(Hidden)` 경로, lines 137-140).

- **MH-3 (anchor 이탈 자동 닫힘)**: SessionPanel이 열린 상태에서 트롬본 anchor가 아닌 다른 위치(피아노 anchor 또는 비악기 텔레포트 영역)로 텔레포트 → SessionPanel이 자동 닫힘. 다른 악기 anchor면 `ActiveInstrumentChanged(otherInstrument)` 발화 → `_state != Hidden`이라 Hidden 전환(line 149-150). 비악기 영역이면 `linkedInstrument=null`이 wiring된 `InstrumentTeleportLink`가 있어야 `AnyAnchorTeleported(null)` 발화 → Provider.Current=null → ActiveInstrumentChanged(null) → Hidden.

### 4. 비악기 영역 wiring 확인 (manual-hard MH-3 보강 분기)

MH-3 검증 중 "비악기 텔레포트 영역으로 갔는데 SessionPanel이 닫히지 않는다"가 관찰되면, 해당 텔레포트 영역(TeleportationArea 또는 TeleportationAnchor) GameObject에 `InstrumentTeleportLink` 컴포넌트가 부착되어 있고 `linkedInstrument: {fileID: 0}` (null)로 설정되어 있는지 MCP `find_gameobjects search_term="InstrumentTeleportLink" search_method=by_component`로 확인. 부착이 없는 비악기 영역이 발견되면 **이 plan에서** 그 영역에 `InstrumentTeleportLink` 추가(linkedInstrument=null)로 보완. MCP 미가용 시 사용자에게 inspector wiring 요청 후 본 plan 미완료 표시 unresolved에 기록.

### 5. 누락 보완 (조건부, 검증 결과 의존)

위 검증 중 누락이 드러나면 *그 항목만* fix:

- (a) `_panelAnchor` 미wiring 발견 시: Trombone.prefab의 `_panelAnchor` 슬롯에 PanelAnchor Transform fileID 박제 — 현재 이미 wiring되어 있으므로 발생 가능성 낮음.
- (b) `InstrumentTeleportLink.linkedInstrument` 미wiring 발견 시: Trombone.prefab의 TromboneAnchor GameObject의 InstrumentTeleportLink에 linkedInstrument 박제 — 현재 이미 wiring됨.
- (c) 비악기 텔레포트 영역에 `InstrumentTeleportLink` 미부착 발견 시: 해당 영역에 컴포넌트 추가, linkedInstrument=null로 설정.

(a)(b)는 현재 wiring 박제로 발생 가능성 0에 가까우므로 본 plan은 (c) 관찰 시에만 보완 수행을 예상.

### 6. 컴파일·테스트 게이트

본 plan은 C# 코드 변경 0건이 기본이라 컴파일·회귀 테스트가 항상 PASS 예상. 단 5-(c) 보완을 수행하면 scene YAML만 수정되므로 컴파일 영향 0, 테스트 회귀 0. `unity-test-runner` 1회 호출은 회귀 안전망으로 수행. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 본 단계 skip.

## Deliverables

본 plan의 deliverable은 *검증 결과 박제*가 기본이며, 누락 발견 시 다음 중 0~1개 항목이 추가될 수 있다:

- *(조건부, MH-3 검증에서 누락 발견 시)* `Assets/Scenes/TestSceneSanyo.unity` — 비악기 텔레포트 영역에 `InstrumentTeleportLink` 컴포넌트 추가(linkedInstrument=null). 다른 변경 없음.
- 검증 PASS 보고는 본 plan의 `## Handoff` 섹션에 박제.

C# 코드 변경 없음. Prefab YAML 변경 없음(누락 발견 시에만 scene 1줄 추가).

## Acceptance Criteria

- [ ] `[auto-hard]` Trombone.prefab의 `_panelAnchor` 슬롯이 비-zero fileID로 박제되어 있고, 그 fileID가 PanelAnchor GameObject Transform과 일치한다 (sub-spec 13 plan handoff 답습).
  **검증:** `Grep -n "_panelAnchor: {fileID: " Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 출력에 `_panelAnchor: {fileID: 0}`이 *아닌* 행이 1건 매칭 + `Grep -n "m_Name: PanelAnchor" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 1건 매칭 + 두 grep의 fileID가 같은 GameObject 블록을 가리킴(line 992의 fileID와 line 1139의 component fileID가 동일).

- [ ] `[auto-hard]` Trombone.prefab의 TromboneAnchor GameObject에 부착된 `InstrumentTeleportLink`의 `linkedInstrument` 슬롯이 Trombone InstrumentBase MonoBehaviour를 가리킨다 (Trombone instrumentId='Trombone' 박제).
  **검증:** `Grep -n "InstrumentTeleportLink" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 1건 + 그 다음 줄 `linkedInstrument: {fileID: <비-zero>}`가 같은 prefab 안 `m_EditorClassIdentifier: Instruments::Instruments.Trombone` 블록의 MonoBehaviour fileID와 일치(line 1658 ↔ line 974). 두 fileID가 같은 GameObject `3300842808265667475`(line 980)를 가리킴.

- [ ] `[auto-hard]` `InputSystem_Actions.inputactions`의 `PanelToggle` action에 `<XRController>{LeftHand}/menuButton` binding이 존재한다 (spec 14 "왼손 메뉴 입력" 직답).
  **검증:** `Grep -n "<XRController>{LeftHand}/menuButton" Assets/Settings/Input/InputSystem_Actions.inputactions` 1건 + 그 binding 블록의 `"action": "PanelToggle"`이 동반(line 489·493 인접).

- [ ] `[auto-hard]` `TestSceneSanyo.unity`의 SessionPanelController 인스턴스가 `snapOnce: 1`로 직렬화되어 트롬본 attach 시 panel이 1회 위치 결정 후 고정된다 (snap-once plan handoff 답습).
  **검증:** `Grep -n "snapOnce: 1" Assets/Scenes/TestSceneSanyo.unity` 1건 + 동일 GameObject 블록의 `m_EditorClassIdentifier: Assembly-CSharp::SessionPanel.SessionPanelController` (line 7106) 매칭.

- [ ] `[auto-soft]` Play 모드 진입 시 `read_console`에서 SessionPanel·Trombone·InstrumentTeleportLink·InputSystem 관련 NullReferenceException 또는 ArgumentException 0건. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 본 AC skip.
  **검증:** Unity Editor Play 진입 직후 `read_console action=get types=["error"] filter_text="SessionPanel|Trombone|InstrumentTeleport|InputSystem"` 결과 0건. MCP 미가용이면 Editor Console 시각 확인 후 0건 보고.

- [ ] `[manual-hard]` (MH-1, 오픈) Play 모드에서 트롬본 anchor에 텔레포트해 attach 상태가 된 직후 왼손 menuButton(또는 secondaryButton)을 발화하면 SessionPanel이 카메라 정면 약 1m 거리에 1회 등장하여 player가 머리를 움직여도 그 위치에 *고정* 유지된다 (`snapOnce=true` 동작).
  **검증:** XR Device Simulator 또는 헤드셋으로 `TestSceneSanyo` Play → 트롬본 anchor 텔레포트(attach 완료 시 양손 GripHandPose 부착 시각 확인) → 왼손 menuButton 발화 → SessionPanel 인스턴스 `SessionPanel(Clone)` GameObject가 카메라 정면에 등장. 이후 player head를 yaw/pitch ±30° 회전 → SessionPanel world position이 1프레임 차이 미만으로 변화 없음을 Scene 뷰 + Hierarchy에서 시각 확인.

- [ ] `[manual-hard]` (MH-2, 토글 닫힘) MH-1 상태에서 다시 왼손 menuButton을 발화하면 SessionPanel이 즉시 비활성화되어 손에 닿는 버튼이 사라진다.
  **검증:** MH-1 직후 → 왼손 menuButton 재발화 → Hierarchy의 `SessionPanel(Clone)` GameObject의 `m_IsActive`가 false(Inspector 시각 확인) 또는 Scene 뷰에서 패널이 사라짐 확인. 재차 menuButton 발화 시 다시 같은 위치(또는 새 카메라 forward 위치)에 등장하여 토글이 양방향 동작함을 1회 더 확인.

- [ ] `[manual-hard]` (MH-3, anchor 이탈 자동 닫힘) MH-1으로 패널이 열린 상태에서 트롬본 anchor가 아닌 다른 위치(피아노 anchor 또는 비악기 텔레포트 영역)로 텔레포트하면 SessionPanel이 자동으로 닫힌다.
  **검증:** MH-1 직후 → 피아노 anchor로 텔레포트 → SessionPanel `SessionPanel(Clone)`이 비활성 또는 즉시 새 인스턴스가 닫힌 상태로 전환됨 시각 확인 → (선택) 다시 비악기 텔레포트 영역(SampleScene 기본 ground area)으로 텔레포트 → 동일 자동 close 확인. 만약 비악기 영역에서 close가 안 되면 Approach 4·5-(c)에 따라 해당 영역에 `InstrumentTeleportLink(linkedInstrument=null)` 추가 후 재검증.

## Out of Scope

- `SessionPanelController.PositionAtInstrument` 의 위치 계산식 변경 (현재 camera-forward+`instrumentSpawnDistance` 식) — piano/drum 공용이며 본 plan은 그 식을 *그대로* 답습. anchor PanelAnchor 위치를 직접 사용하는 식으로 변경하려면 별도 sub-spec.
- SessionPanel UI 내용 변경 — spec 14 Out of Scope 1번 답습 (session-panel sub-spec 담당).
- 왼손 메뉴 입력 방식 변경 — spec 14 Out of Scope 2번 답습 (기존 session-panel 스펙 담당). 본 plan은 `PanelToggle` action의 기존 binding을 *확인*만 한다.
- 트롬본 노트 디스플레이 변경 — spec 14 Out of Scope 3번 답습 (sub-spec 13 담당).
- `snapOnce` SerializeField 동작 수정 — snap-once plan(`2026-05-21-linksky0311-trombone-session-panel-snap-once.md`)이 이미 박제, 본 plan은 *직렬화 값 검증*만.
- Trombone.prefab의 PanelAnchor child 위치·회전 조정 — commit `f17a437` 답습 (Rig 자식 재부착·-60° yaw 이미 박제). 사용자가 manual 검증 중 "패널 위치가 어색하다"고 판단하면 별도 sub-spec 또는 후속 plan 후보로 박제.
- 트롬본 세션 차트 제작 또는 곡 목록 활성화 — sub-spec 14는 SessionPanel **표시**만 다루며, 트롬본 트랙 차트 제작은 별도 sub-spec(예: 트롬본 정식 vmsong 제작) 책임.
- 비악기 텔레포트 영역의 누락 wiring 보완 외 scene 수정 — MH-3 보강 분기에서 `InstrumentTeleportLink` 추가만 허용, 다른 컴포넌트 변경 금지.

## Notes

- 본 plan은 **wiring 0건 변경 PASS**가 best path며, 실제로 가장 가능성 높은 시나리오다. 모든 wiring이 이미 박제되어 있으므로 manual 검증이 모두 PASS하면 그대로 종료.
- 만약 사용자가 MH-1 manual에서 "패널이 트롬본의 패널 앵커가 아닌 카메라 정면에 뜬다"를 문제로 인식하면, 그것은 spec 14 What 문구("트롬본의 패널 앵커 위치에 나타난다")와 현재 코드(`PositionAtInstrument`가 camera-forward 사용)의 *의미적 불일치*다. 본 plan은 piano/drum이 동일 식을 쓰며 일관성을 유지하므로 PASS로 해석하지만, 사용자가 *원래 의도*가 PanelAnchor.position 직접 사용이라고 결정하면 후속 plan으로 `PositionAtInstrument`를 anchor-direct 식으로 변경하는 sub-spec을 별도 등록한다. 이는 트롬본·피아노·드럼 모두 영향을 받으므로 cross-instrument 변경 plan으로 분리.
- 후속 plan 후보: (a) `PositionAtInstrument`를 `PanelAnchor.position` 직접 사용 식으로 변경(피아노·드럼·트롬본 일괄 회귀 검증 동반), (b) 트롬본 attach 시 PanelAnchor의 yaw 회전(-60°)이 사용자 시점에서 자연스러운지 시각 검증 후 미세 조정, (c) 비악기 영역 wiring 전수 점검 sub-spec — SampleScene/TestSceneSanyo 안 모든 텔레포트 영역의 `InstrumentTeleportLink` 부착 여부 일괄 박제.
- snap-once plan Handoff에 "트롬본 attach 직후 1회 정렬 위치는 `tromboneRoot`가 이미 mouth-aligned 된 상태"라 박제됐는데, 현재 `PositionAtInstrument`는 anchor를 사용하지 않고 camera 기준이라 그 박제는 사실상 dead-code 시점(과거 식 기준). snap-once의 *효과*(매 프레임 추적 끔)는 여전히 유효 — camera-forward 식도 매 프레임 호출되면 머리 회전에 따라 패널이 따라온다.
- 본 plan은 sub-spec 14의 What/Behavior 3-tuple을 만족시키는 **최소 작업 집합** 박제로 끝났다. 추가 일이 발견되면 unresolved에 기록.

## Handoff

- [2026-05-27 완료] Static wiring 검증 AC1~AC4 Grep PASS. auto-soft AC5 (read_console 에러 0건) PASS. unity-test-runner EditMode 133/133 + PlayMode 4/4 PASS.
- 비악기 영역 wiring: Floor TeleportationArea에 `InstrumentTeleportLink(linkedInstrument: {fileID: 0})` 이미 박제 → scene 변경 0건.
- MH-1/MH-2/MH-3 사용자 확인 — 트롬본 SessionPanel 오픈·토글·이탈 자동 닫힘 모두 정상 동작. C# 코드 변경 0건.
- `PositionAtInstrument`는 camera-forward+1m 식(anchor 직접 사용 아님) — piano/drum과 동일 일관성. 사용자 문제 없음으로 확인.
