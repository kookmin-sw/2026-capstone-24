# Session Panel Anchoring

**Linked Spec:** [`01-anchoring.md`](../specs/01-anchoring.md)
**Status:** `Ready`

## Goal

세션 패널의 "그릇"과 anchoring·섹션 가시성·토글 골격을 만든다. 트리거 입력은 컨트롤러 메뉴 버튼 stub으로 두고(왼손 핀치는 후속 swap), 활성 악기 신호는 추상 인터페이스만 정의해 후속 plan이 wire한다. 시작 메뉴/볼륨 섹션 컨텐츠는 placeholder 컨테이너만 둔다.

## Context

session-panel 피처의 첫 plan이다. spec [`01-anchoring.md`](../specs/01-anchoring.md)이 정의한 4가지 책임을 한 시스템에서 풀어낸다:

1. 트리거 입력 → 패널 호출/토글
2. 패널 위치 결정 (핀치 호출 시 왼손 손바닥 근처, 악기 활성 시 악기 anchor)
3. 컨텍스트별 섹션 가시성 (활성 악기 없을 땐 볼륨만, 있을 땐 시작 메뉴 + 볼륨)
4. 상태 전이 (악기 활성/해제, 토글 재발화)

본 plan은 다음 두 가지를 의도적으로 추상화 + 미루기로 처리한다:

- **왼손 핀치 입력.** [`hands/specs/03-left-pinch-gesture.md`](../../hands/specs/03-left-pinch-gesture.md)이 spec만 박제되어 있고 plan이 없다 + Open Questions 4건 미해결. 본 plan은 컨트롤러 메뉴 버튼을 stub 입력으로 사용하는 `PanelToggle` Action을 추가한다. 후속 hands plan이 들어올 때 같은 Action에 핀치 binding을 추가하는 방식으로 swap된다.
- **활성 악기 신호.** drum/piano 잡기 와이어링은 다른 작업자가 별도 plan에서 진행 중이다. 본 plan은 `IActiveInstrumentProvider` / `IActiveInstrument` 추상 인터페이스만 정의하고 구현은 두지 않는다. manual-hard 검증은 dummy stub provider를 inspector로 임시 주입해서 진행한다.

섹션 컨텐츠(시작 메뉴, 볼륨)는 [`02-start-menu-section.md`](../specs/02-start-menu-section.md) / [`03-volume-section.md`](../specs/03-volume-section.md)의 plan이 채울 자리만 placeholder GameObject로 둔다. 본 plan은 컨테이너 활성/비활성 토글만 책임진다.

## Verified Structural Assumptions

- VR Player의 왼손 root: `Assets/Characters/Prefabs/VR Player.prefab`의 `Camera Offset/Hands/Left/LeftHandTrackingHandRoot` (TrackedPoseDriver 부착, controller·hand-tracking 양쪽 활성). 손바닥 spawn 기준 후보는 자식 ghost hand의 `L_Wrist`. 본 plan은 깊은 경로를 코드에 박제하지 않고 `SessionPanelController`의 SerializeField로 scene에서 직접 wiring한다. — `unity-scene-reader 보고 (2026-05-05)`
- `Assets/Instruments/Piano/Prefabs/Piano.prefab` 루트 직속 자식: `PianoInteraction` / `AudioOutput` / `PianoModel` / `RhythmGameHost` / `RhythmUIRoot`. 본 plan은 prefab 루트에 빈 GameObject `PanelAnchor`를 새 자식으로 추가한다. — `unity-scene-reader 보고 (2026-05-05)`
- `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` 루트 직속 자식: `AudioOutput` / 9개 piece nested prefab(BassDrum, Snare, HighTom, MidTom, FloorTom, HiHat, CrashCymbal, RideCymbal, drum_stick류) / `RhythmGameHost` / `RhythmUIRoot`. 본 plan은 DrumKit 루트에 빈 GameObject `PanelAnchor`를 새 자식으로 추가한다. nested piece prefab 안에는 두지 않는다(nested override 사고 방지). — `unity-scene-reader 보고 (2026-05-05)`
- `Assets/Settings/Input/InputSystem_Actions.inputactions`의 Action Map 2개: `Player`, `UI`. `Player` map의 기존 액션(Move/Look/Attack/Interact/Crouch/Jump/Previous/Next/Sprint)에 panel 관련 후보 없음. 본 plan은 `Player` map에 새 Button 액션 `PanelToggle`을 추가하고 binding으로 좌측 컨트롤러 메뉴 버튼을 둔다. — `unity-scene-reader 보고 (2026-05-05)`
- 메인 작업 scene: `Assets/Scenes/SampleScene.unity`. VR Player·Piano·DrumKit 모두 루트 직속 instance, prefab override 없음. 본 plan은 prefab asset 수정만으로 끝낼 수 있다(scene-only override 추가 없음). scene-only `PianoAnchor`/`DrumKitAnchor` GameObject가 따로 존재하지만 텔레포트 anchor 용도로 추정 — 본 plan에선 사용하지 않는다. — `unity-scene-reader 보고 (2026-05-05)`

## Approach

1. **`PanelToggle` Input Action 추가** — `Assets/Settings/Input/InputSystem_Actions.inputactions`의 `Player` map에 Button 액션 `PanelToggle` 추가. binding 1차: `<XRController>{LeftHand}/menuButton`. 보조 binding: `<XRController>{LeftHand}/secondaryButton` (헤드셋별 바인딩 호환성). inputactions JSON은 단일 propertyPath 변경이 아니므로 [`unity-asset-edit`](../../../../.claude/skills/unity-asset-edit/SKILL.md) skill 절차에 따라 진행. MCP `manage_asset`로 처리 시도 → 안 되면 사용자 동의 후 직접 텍스트 편집(action + binding 행 추가만 = 좁은 범위).

2. **활성 악기 추상화 정의** — `Assets/Instruments/_Core/Scripts/IActiveInstrumentProvider.cs` 신설. 본 plan은 인터페이스만 정의하고 구현은 두지 않는다.

   ```csharp
   public interface IActiveInstrumentProvider {
       event System.Action<IActiveInstrument> ActiveInstrumentChanged;
       IActiveInstrument Current { get; }
   }
   public interface IActiveInstrument {
       Transform PanelAnchor { get; }
       string InstrumentId { get; }
   }
   ```

   `Assets/Instruments/Instruments.asmdef`가 자동으로 포함하므로 별도 asmdef 작업 없음.

3. **Piano.prefab에 `PanelAnchor` 자식 추가** — [`unity-asset-edit`](../../../../.claude/skills/unity-asset-edit/SKILL.md) skill 절차로 MCP `manage_prefabs` 또는 `manage_gameobject`(prefab open) 사용. 빈 GameObject 추가 + local position `(0, 1.0, 0.3)` 정도(피아노 윗쪽, 사용자 시야 범위 내). 정확한 오프셋은 manual 검증에서 조정.

4. **DrumKit.prefab에 `PanelAnchor` 자식 추가** — 같은 절차. 빈 GameObject 추가 + local position `(0, 1.4, 0.0)` 정도(드럼 셋업 위쪽). 정확한 오프셋은 manual 검증에서 조정.

5. **SessionPanel prefab 신설** — `Assets/SessionPanel/Prefabs/SessionPanel.prefab`. 루트: world-space Canvas (`Render Mode = WorldSpace`) + RectTransform (0.4m × 0.3m). 자식 2개: `StartMenuSectionContainer`, `VolumeSectionContainer` (둘 다 빈 GameObject placeholder). 본 plan은 컨텐츠를 채우지 않는다.

6. **`SessionPanelController` 스크립트 신설** — `Assets/SessionPanel/Scripts/SessionPanelController.cs`. 책임:
   - SerializeField: `panelPrefab` (SessionPanel.prefab), `leftHandSpawnTransform` (Transform — scene에서 wrist 후보로 wire), `pinchSpawnLocalOffset` (Vector3, default `(0, 0.05, 0.15)`), `activeInstrumentProvider` (UnityEngine.Object — 인터페이스를 직렬화하기 위한 패턴), `panelToggleAction` (InputActionReference → `PanelToggle`).
   - 내부 상태 enum: `Hidden` / `PinchOpened` / `InstrumentOpened`.
   - 전이 표:

     | 현재 → 다음 | 트리거 |
     |---|---|
     | Hidden → PinchOpened | `PanelToggle` performed + `Current == null` |
     | Hidden → InstrumentOpened | `ActiveInstrumentChanged` → non-null |
     | PinchOpened → InstrumentOpened | `ActiveInstrumentChanged` → non-null (위치 이동 + 시작 메뉴 컨테이너 활성) |
     | PinchOpened → Hidden | `PanelToggle` performed (토글) |
     | InstrumentOpened → Hidden | `ActiveInstrumentChanged` → null **또는** `PanelToggle` performed |

   - 가시성: `PinchOpened` → `VolumeSectionContainer` 활성, `StartMenuSectionContainer` 비활성. `InstrumentOpened` → 둘 다 활성.
   - 위치 결정: `PinchOpened` → `leftHandSpawnTransform.TransformPoint(pinchSpawnLocalOffset)` + 카메라 방향으로 yaw 회전. `InstrumentOpened` → `Current.PanelAnchor.position` / `rotation` 그대로.
   - 패널 인스턴스는 lazy instantiate(첫 호출 시 1회) 후 재사용. `SetActive`로 visibility만 토글.

7. **SampleScene.unity 인스턴스 와이어링** — VR Player와 동등 위치(scene 루트)에 빈 GameObject `SessionPanelManager` 추가 + `SessionPanelController` 부착. inspector 와이어:
   - `panelPrefab` ← `SessionPanel.prefab`
   - `leftHandSpawnTransform` ← VR Player 인스턴스의 왼손 wrist 후보 Transform (Hands/Left ghost hand 안 `L_Wrist` 예상, manual에서 확정)
   - `panelToggleAction` ← `InputSystem_Actions.inputactions`의 `Player/PanelToggle`
   - `activeInstrumentProvider` ← `null` 그대로 (후속 plan이 wire). manual 검증에서는 임시 dummy provider 컴포넌트를 같은 GameObject에 부착해서 주입.

## Deliverables

- `Assets/Settings/Input/InputSystem_Actions.inputactions` — `Player` map에 `PanelToggle` Button 액션 + LeftHand 메뉴 버튼 / 보조 버튼 binding 추가.
- `Assets/Instruments/_Core/Scripts/IActiveInstrumentProvider.cs` — 신설. `IActiveInstrumentProvider` + `IActiveInstrument` 추상 인터페이스.
- `Assets/Instruments/Piano/Prefabs/Piano.prefab` — 루트에 `PanelAnchor` 빈 GameObject 자식 추가.
- `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` — 루트에 `PanelAnchor` 빈 GameObject 자식 추가.
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab` — 신설. World-space Canvas + 2개 placeholder section container.
- `Assets/SessionPanel/Scripts/SessionPanelController.cs` — 신설. 입력·상태·가시성·위치 결정 통합.
- `Assets/Scenes/SampleScene.unity` — `SessionPanelManager` GameObject 추가 + `SessionPanelController` 부착 + inspector 와이어링.

## Acceptance Criteria

- [ ] `[auto-hard]` `Piano.prefab` 인스턴스화 후 `PanelAnchor` 자식 GameObject가 prefab 루트 직속에 정확히 1개 존재 (MCP `find_gameobjects`로 단일 매치 확인).
- [ ] `[auto-hard]` `DrumKit.prefab` 인스턴스화 후 `PanelAnchor` 자식 GameObject가 prefab 루트 직속에 정확히 1개 존재.
- [ ] `[auto-hard]` `Piano.prefab` 인스턴스화 시 콘솔 에러·예외 0건 (MCP `read_console` 직렬화 sanity).
- [ ] `[auto-hard]` `DrumKit.prefab` 인스턴스화 시 콘솔 에러·예외 0건.
- [ ] `[auto-hard]` `SessionPanel.prefab`의 루트 Canvas `m_RenderMode` 직렬화 값이 `2`(WorldSpace) — 단일 매치 grep.
- [ ] `[auto-hard]` `SessionPanel.prefab` 인스턴스화 시 자식으로 `StartMenuSectionContainer`, `VolumeSectionContainer` 두 GameObject가 정확히 1개씩 존재.
- [ ] `[auto-hard]` `IActiveInstrumentProvider.cs`와 `SessionPanelController.cs`가 컴파일 통과 (Editor 도메인 reload 후 `read_console` 컴파일 에러 0).
- [ ] `[auto-hard]` `InputSystem_Actions.inputactions` 안에 액션명 `PanelToggle`이 정확히 1회 등장 + LeftHand 컨트롤러 binding 최소 1개 동반 (grep).
- [ ] `[manual-hard]` SampleScene Editor Play 모드에서 활성 악기 stub provider Current가 null인 상태로 `PanelToggle`(컨트롤러 메뉴 버튼) 발화 → 패널이 왼손 wrist 위쪽에 노출되며 `VolumeSectionContainer`만 활성, `StartMenuSectionContainer`는 비활성.
- [ ] `[manual-hard]` dummy stub provider의 Current를 inspector로 임의의 IActiveInstrument(테스트용 컴포넌트로 PanelAnchor Transform 노출)로 set → 패널이 해당 PanelAnchor world position으로 이동 + `StartMenuSectionContainer`도 활성으로 전환.
- [ ] `[manual-hard]` 위 활성 상태에서 `PanelToggle` 재발화 → 패널이 `Hidden`(SetActive false)으로 닫힘.
- [ ] `[manual-hard]` 활성 상태에서 dummy stub의 Current를 다시 null로 변경 → 패널이 `Hidden`으로 닫힘 (악기 놓음 시뮬레이션).
- [ ] `[manual-hard]` SampleScene Play 진입 시 `SessionPanelController` 관련 콘솔 에러·예외 0건.

## Out of Scope

- 왼손 핀치 제스처 자체의 검출 / `hands/specs/03-left-pinch-gesture.md` 구현 — `hands` 피처에서 별도 plan. 본 plan은 컨트롤러 stub 입력만 둔다.
- drum/piano 잡기 신호를 `IActiveInstrumentProvider`에 wire하는 작업 — 다른 작업자가 진행 중인 잡기 시스템 plan에서 처리.
- 시작 메뉴 섹션의 내부 컨텐츠(곡 목록·난이도·반주 트랙·시작 버튼) — [`02-start-menu-section.md`](../specs/02-start-menu-section.md)의 plan.
- 볼륨 섹션의 내부 컨텐츠(마스터·내 악기·persist) — [`03-volume-section.md`](../specs/03-volume-section.md)의 plan.
- 패널의 시각 디자인 (텍스처, 폰트, 색상, 애니메이션). 본 plan은 placeholder Canvas 배경만 둔다.
- 핀치 호출 시 패널 위치의 정밀 튜닝. 본 plan의 manual 검증은 "왼손 손바닥 근처에 합리적으로 보임" 정도까지만 확인하고, 실제 사용성 튜닝은 후속 plan(또는 spec resolve)에서 진행.

## Notes

- `IActiveInstrumentProvider`를 SerializeField로 받는 패턴: Unity는 인터페이스를 직렬화하지 못하므로 `[SerializeField] UnityEngine.Object _activeInstrumentProviderObject` + 런타임 캐스팅 방식 또는 `[SerializeReference]` 둘 중 하나로 처리. 구현 시 단순한 캐스팅 패턴 권장.
- `PanelToggle` Action의 핸드 트래킹 핀치 swap은 `hands/03-left-pinch-gesture` plan이 들어올 때 binding 1개 추가로 끝난다 — 본 plan의 Action 이름·map 위치를 변경하지 않으면 swap이 자연스럽다.
- 본 plan의 manual 검증용 dummy stub provider는 production 코드가 아니라 임시 테스트 도구이므로 plan-implementer가 `Assets/SessionPanel/Editor/` 또는 `Tests/`에 두고, plan 완료 후 후속 plan이 production wiring을 추가할 때 dummy를 제거한다.

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신 -->
