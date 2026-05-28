# 플레이 버튼 입력 방식 준비 확인 + 안내 UI plan

**Linked Spec:** [`17-input-mode-readiness-check.md`](../specs/17-input-mode-readiness-check.md)
**Status:** `Done`

## Goal

`RhythmGameSectionController.OnPlayButtonClicked()`가 즉시 `host.StartSession(...)`을 호출하기 전에 현재 활성 악기가 요구하는 입력 방식(`InstrumentBase.requiredInputMode`)과 실제 활성 XR 입력 모드(VR Player의 `LeftHandTrackingGhostHand` vs `LeftControllerGhostHand`의 `activeInHierarchy`)를 비교하고, 불일치 시 게임을 시작하지 않고 안내 UI를 띄우며, 일치로 전환되면 자동으로 게임을 시작하는 readiness gate 한 겹을 추가한다.

## Context

Sub-spec 17의 What과 Behavior 4-tuple 요건:

1. 각 악기 prefab에 "필요 입력 방식"(핸드 트래킹 / 컨트롤러 / 제한 없음) 인스펙터 필드 존재
2. 플레이 버튼 클릭 시 현재 XR 입력 모드와 비교
3. 일치하거나 악기가 "제한 없음"이면 즉시 시작 (기존 흐름 유지)
4. 불일치 시 게임 시작 차단 + 안내 UI 표시 (필요 입력 방식 메시지 + 선택적 이미지)
5. 안내 UI 표시 중 사용자가 올바른 입력 방식으로 전환 → 안내 UI close + 자동 게임 시작

현재 프로젝트에 박제된 입력 방식 감지 패턴은 `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` 의 `ResolveSourceMode()` 한 곳뿐이다. 거기서 `handTrackingRoot.activeInHierarchy` / `controllerRoot.activeInHierarchy` 두 GameObject의 활성 여부로 GhostSourceMode를 결정한다(`Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` lines 153-165). 두 root는 `VR Player.prefab`에서 wiring되어 있다(GUID `a7f32f4296ccdfb42b0a66dfd8c4425e` = `LeftControllerGhostHand.prefab`, GUID `411666a83e4ce264dbaf035f51b198ca` = `LeftHandTrackingGhostHand.prefab`의 root). **이 동일 패턴을 SessionPanel 측에서 재사용**해 `XRInputModeProbe`라는 단일 헬퍼로 박제하면 XR Hands 패키지 API에 직접 의존하지 않고 같은 진실원을 공유한다.

`requiredInputMode`는 `InstrumentBase`에 enum SerializeField로 추가한다. 이미 `InstrumentBase`는 sub-spec 14·15의 `_panelAnchor`, `instrumentNetId` 등 SerializeField가 모이는 곳이며, 모든 악기(`Piano`, `Drum`, `Trombone`)가 본 클래스를 상속하므로 한 번 추가로 3개 prefab이 모두 새 필드를 갖는다. 기본값 `Any`(제한 없음)로 두면 기존 prefab의 직렬화는 그대로 통과한다.

`OnPlayButtonClicked`(`RhythmGameSectionController.cs` lines 467-502)는 현재 사전 검증 없이 바로 `host.StartSession(...)`을 호출한다. 본 plan은 그 메서드의 *앞부분*에 readiness gate를 끼워넣고, gate 미통과 시 안내 UI prefab을 instantiate하면서 폴링 코루틴을 시작한다. 폴링 코루틴은 매 프레임 `XRInputModeProbe.Current`를 다시 읽어 일치 감지 시 안내 UI를 닫고 원래 `StartSession` 흐름을 재진입한다.

안내 UI는 신규 prefab `Assets/SessionPanel/Prefabs/InputModeReadinessNotice.prefab`(WorldSpace Canvas) + 신규 컨트롤러 `Assets/SessionPanel/Scripts/InputModeReadinessNotice.cs`로 구성한다. 기존 `InstrumentGuidePanel.prefab`은 단순 'Next' 라벨 패널이라 본 plan과 무관하다(이름 충돌만 회피). SessionPanel 본 prefab과는 분리한다 — 본 UI는 SessionPanel을 닫더라도 readiness 안내는 표시되어야 하기 때문(다만 spec 17 What/Behavior가 "SessionPanel과의 공존" 정책을 명시하지 않으므로 최소 구현은 SessionPanel이 열려 있는 상태에서 한 패널의 자식 또는 인접 패널로 띄운다 — Approach 단계 참조).

본 plan의 readiness 비교 정책:

- 악기 `requiredInputMode = HandTracking` && 활성 XR = Controller → BLOCK
- 악기 `requiredInputMode = Controller` && 활성 XR = HandTracking → BLOCK
- 그 외(일치 / `Any` / 양쪽 root 모두 비활성 = 모드 미감지 == 폴백 진행) → PASS

"양쪽 root 모두 비활성"의 폴백을 PASS로 두는 이유: Editor 모드의 XR Device Simulator 같은 환경에서는 양쪽 root가 모두 inactive일 수 있는데 이를 BLOCK으로 처리하면 Editor 테스트가 막힌다. spec 17 What은 "활성 XR 입력 방식이 다르다"고 명시했으므로 *감지된 활성 모드와 불일치*만 BLOCK하는 게 의미적으로 맞다.

## Verified Structural Assumptions

- `RhythmGameSectionController.OnPlayButtonClicked()` (`Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` lines 467-502): null guard (`_currentInstrument`/`_selectedSong`/`_selectedDifficulty`/`_loadedChart`) → `RhythmGameHost host = _currentInstrument.InstrumentRoot.GetComponentInChildren<RhythmGameHost>()` → `host.StartSession(merged, rhythmSong, judgedChannel, accompaniment)` → `GameStarted?.Invoke()`. 본 plan은 null guard 직후·`host.GetComponentInChildren` 직전에 readiness gate를 끼워넣는다. — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-27)` lines 467-502
- `InstrumentBase` (`Assets/Instruments/_Core/Scripts/InstrumentBase.cs`): SerializeField 묶음은 lines 33-52 (audioOutput, soundClips, laneConfig, instrumentId, instanceVolume, `_panelAnchor`, instrumentNetId). `requiredInputMode` enum 필드를 line 52 다음에 추가, public getter `RequiredInputMode` 추가. 이미 namespace `Instruments`이며 `Piano`/`Drum`/`Trombone` 3개 자식이 본 클래스만 상속. 기본값 `Any`로 두면 모든 prefab의 SerializeField 직렬화에서 새 필드가 default로 추가되며 기존 값 변동 없음. — `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-27)` lines 33-62
- `PhysicsHandGhostFollower.ResolveSourceMode()` (`Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` lines 153-165): `handTrackingRoot.activeInHierarchy` true → `GhostSourceMode.HandTracking`, else `controllerRoot.activeInHierarchy` true → `GhostSourceMode.Controller`, 둘 다 false → `None`. **이것이 프로젝트에서 활성 XR 입력 모드를 결정하는 유일한 박제된 패턴이며 본 plan은 그대로 답습**한다(XR Hands 패키지 API 직접 호출 0건). — `Read Assets/Hands/Scripts/PhysicsHandGhostFollower.cs (2026-05-27)` lines 153-165
- `VR Player.prefab`의 controller/handtracking root wiring: `PhysicsHandGhostFollower` 인스턴스(왼손, fileID `1483050384353976789`)가 `controllerRoot: objectReference fileID 8121941442307837655`(`LeftControllerGhostHand.prefab` root, GUID `a7f32f4296ccdfb42b0a66dfd8c4425e`), `handTrackingRoot: fileID 1133154061211659073`(`LeftHandTrackingGhostHand.prefab` root, GUID `411666a83e4ce264dbaf035f51b198ca`)로 박제(lines 709-715 + 1272-1280). 오른손도 동일 GUID 두 root. 본 plan은 *왼손 한쪽*의 두 root만 참조 — 양손이 같은 XR mode를 공유하므로 한쪽으로 판단 충분. — `Read Assets/Characters/Prefabs/VR Player.prefab (2026-05-27)` lines 700-735, 1136-1141
- `SessionPanel.Runtime.asmdef` (`Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef`) references: `Instruments`, `RhythmGame.Data`, `RhythmGame.Runtime`, `Unity.InputSystem`, `Unity.XR.Interaction.Toolkit`, `Unity.TextMeshPro`. 본 plan의 신규 C# 두 개(`XRInputModeProbe.cs`, `InputModeReadinessNotice.cs`)는 모두 `SessionPanel` namespace에 두고 `Instruments`(InstrumentBase)·`UnityEngine.UI`/`TMPro`(텍스트)만 import. **asmdef reference 추가 0건**(현재 references 그대로 충분). XR Hands 패키지를 import하지 않음 — `PhysicsHandGhostFollower` 패턴 답습이라 GameObject `activeInHierarchy`만 쓴다. — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-27)`
- `Packages/manifest.json`: `com.unity.xr.hands@1.7.3`, `com.unity.xr.openxr@1.16.1`, `com.unity.xr.interaction.toolkit@3.3.1` 모두 설치. 본 plan은 직접 패키지 API를 호출하지 않으므로 가용성은 *확인만* 한다(실 사용 X). — `Read Packages/manifest.json (2026-05-27)` lines 18-21
- `Trombone.prefab`/`Piano.prefab`/`DrumKit.prefab` 모두 `InstrumentBase` 자식 컴포넌트(Trombone.cs / Piano.cs / DrumKit.cs)를 1개씩 부착. 본 plan에서 `requiredInputMode` 기본값을 `Any`로 두면 세 prefab 모두 새 필드가 자동 직렬화되며 변경 없음. 사용자가 후속에서 Piano = HandTracking, Trombone/Drum = Controller 같은 박제를 인스펙터로 설정해야 본 readiness 동작이 의미를 가진다(spec 17 Behavior의 piano = HandTracking 시나리오 직답). — `Glob Assets/Instruments/**/Prefabs/*.prefab (2026-05-27)` + `Grep class Piano|class Drum|class Trombone Assets/Instruments (2026-05-27)`
- `TestSceneSanyo.unity`의 `SessionPanelController` 인스턴스(GameObject `SessionPanelController`, line 7076, MonoBehaviour fileID `2039824626`, m_EditorClassIdentifier `Assembly-CSharp::SessionPanel.SessionPanelController`)는 `_activeInstrumentProviderObject: fileID 1368118598`(TeleportInstrumentProvider), `panelToggleAction: guid 052faaac586de48259a63d0c4782560b` 등 박제. 본 plan은 `RhythmGameSectionController`만 수정하며 `SessionPanelController` 직렬화는 변경 0. — `Read Assets/Scenes/TestSceneSanyo.unity (2026-05-27)` lines 7070-7118
- `RhythmGameSectionController.Inject(providerObj, catalogObj)` (lines 514-534): SessionPanel이 panel instance 생성 시 자동 호출. `_provider`/`_catalog` 재구독·`ResetSelection`·`RefreshSongList`. 본 plan에서 `InputModeReadinessNotice` 컨트롤러는 *세션 panel 인스턴스 외부*에 두는 게 자연스러우나, prefab 인스턴스 시점에 wiring하려면 `RhythmGameSectionController`의 SerializeField로 `InputModeReadinessNotice noticePrefab` + `Transform noticeAnchor`(또는 `Camera.main`)을 추가하고 인스펙터에서 새 prefab을 박제하는 형식이 가장 적게 wiring 비용을 부른다. — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-27)` lines 514-534

## Approach

### 1. `InputMode` enum 도입 (Instruments 도메인)

`Assets/Instruments/_Core/Scripts/InputMode.cs` 신규:

```csharp
namespace Instruments
{
    public enum InputMode { Any = 0, HandTracking = 1, Controller = 2 }
}
```

`Any` = 0으로 둬 SerializeField 기본 직렬화에서 새 필드가 추가될 때 기존 prefab들이 자동으로 `Any`를 갖게 한다(인덱스 0이 default).

### 2. `InstrumentBase`에 `requiredInputMode` SerializeField 추가

`Assets/Instruments/_Core/Scripts/InstrumentBase.cs` line 52(`instrumentNetId`) 다음에 추가:

```csharp
[Tooltip("이 악기를 플레이하기 위해 요구되는 XR 입력 방식. Any(기본)는 제한 없음.")]
[SerializeField] InputMode requiredInputMode = InputMode.Any;

public InputMode RequiredInputMode => requiredInputMode;
```

`IActiveInstrument` 인터페이스는 *변경하지 않는다*. SessionPanel 측 readiness gate는 `_currentInstrument as InstrumentBase`로 down-cast 후 `.RequiredInputMode`를 읽는다. SessionPanel 도메인은 이미 `Instruments` asmdef를 reference하므로 `InstrumentBase` 직접 접근 가능. (down-cast가 fail하면 — 즉 `IActiveInstrument`이지만 `InstrumentBase` 아님 — `Any`로 간주해 PASS.)

### 3. `XRInputModeProbe` 정적 헬퍼 (SessionPanel 도메인)

`Assets/SessionPanel/Scripts/XRInputModeProbe.cs` 신규:

```csharp
namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/XR Input Mode Probe")]
    public class XRInputModeProbe : MonoBehaviour
    {
        [Tooltip("VR Player의 LeftHandTrackingGhostHand root (또는 RightHandTrackingGhostHand).")]
        [SerializeField] GameObject handTrackingRoot;
        [Tooltip("VR Player의 LeftControllerGhostHand root (또는 RightControllerGhostHand).")]
        [SerializeField] GameObject controllerRoot;

        public Instruments.InputMode Current
        {
            get
            {
                if (handTrackingRoot != null && handTrackingRoot.activeInHierarchy)
                    return Instruments.InputMode.HandTracking;
                if (controllerRoot != null && controllerRoot.activeInHierarchy)
                    return Instruments.InputMode.Controller;
                return Instruments.InputMode.Any;
            }
        }
    }
}
```

씬에 1개 인스턴스 추가 — `TestSceneSanyo.unity`의 SessionPanelController GameObject(`2039824624`)에 같은 컴포넌트를 추가하고 wiring한다. `handTrackingRoot`/`controllerRoot` SerializeField에는 VR Player 인스턴스의 `LeftHandTrackingGhostHand` root, `LeftControllerGhostHand` root를 박제한다(scene wiring 1회). `PhysicsHandGhostFollower`가 같은 두 root를 이미 사용하므로 *진실원이 1개 더 늘어나는 게 아니라*, 같은 GameObject를 두 컴포넌트가 참조하는 형태.

`PhysicsHandGhostFollower`의 `ResolveSourceMode`와 분기 순서·`activeInHierarchy` 사용을 정확히 답습 — 양쪽 false → `Any`(None을 의미적으로 PASS로 해석).

### 4. `InputModeReadinessNotice` 안내 UI 컨트롤러

`Assets/SessionPanel/Scripts/InputModeReadinessNotice.cs` 신규:

```csharp
namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Input Mode Readiness Notice")]
    public class InputModeReadinessNotice : MonoBehaviour
    {
        [SerializeField] TMPro.TextMeshProUGUI messageLabel;
        [Tooltip("선택. 비워두면 이미지 영역이 비활성화.")]
        [SerializeField] UnityEngine.UI.Image illustration;
        [SerializeField] UnityEngine.Sprite handTrackingSprite;
        [SerializeField] UnityEngine.Sprite controllerSprite;
        [SerializeField] string handTrackingMessage = "이 악기는 핸드 트래킹으로만 연주할 수 있어요.\nQuest 설정에서 핸드 트래킹으로 전환해 주세요.";
        [SerializeField] string controllerMessage   = "이 악기는 컨트롤러로만 연주할 수 있어요.\n컨트롤러를 잡아 활성화해 주세요.";

        public void Show(Instruments.InputMode required) { /* 라벨·이미지 세팅 + gameObject.SetActive(true) */ }
        public void Hide() => gameObject.SetActive(false);
    }
}
```

해당 prefab `Assets/SessionPanel/Prefabs/InputModeReadinessNotice.prefab` 신규 — WorldSpace Canvas + TMP_Text + Image. 위치 결정 방식은 `SessionPanelController.PositionAtInstrument`와 유사하게 카메라 정면 ~1m로 잡되, 본 plan에서는 *부모를 SessionPanel Canvas의 자식*으로 박는 대신 *루트 WorldSpace Canvas*로 두고 readiness gate가 매번 카메라 forward로 PositionAt하는 식. (간단·격리.)

### 5. `RhythmGameSectionController`에 readiness gate 끼우기

`OnPlayButtonClicked` 본문을 다음으로 재구성:

```csharp
public void OnPlayButtonClicked()
{
    if (_currentInstrument == null || _selectedSong == null ||
        _selectedDifficulty == null || _loadedChart == null) return;

    // === 신규: readiness gate ===
    if (!TryGateInputMode())
    {
        // 차단됨 — 안내 UI 띄움 + 폴링 코루틴
        if (_readinessNoticeInstance == null && readinessNoticePrefab != null)
            _readinessNoticeInstance = Instantiate(readinessNoticePrefab);
        var required = (_currentInstrument as InstrumentBase)?.RequiredInputMode ?? InputMode.Any;
        _readinessNoticeInstance?.Show(required);
        if (_readinessPollCoroutine == null)
            _readinessPollCoroutine = StartCoroutine(PollReadinessAndAutoStart());
        return;
    }
    // === gate end ===

    StartSessionInternal();
}

bool TryGateInputMode()
{
    var ib = _currentInstrument as InstrumentBase;
    if (ib == null) return true;
    var required = ib.RequiredInputMode;
    if (required == InputMode.Any) return true;
    var current = inputModeProbe != null ? inputModeProbe.Current : InputMode.Any;
    if (current == InputMode.Any) return true; // 모드 미감지 — PASS
    return current == required;
}

IEnumerator PollReadinessAndAutoStart()
{
    while (true)
    {
        yield return null;
        if (TryGateInputMode())
        {
            _readinessNoticeInstance?.Hide();
            _readinessPollCoroutine = null;
            StartSessionInternal();
            yield break;
        }
    }
}

void StartSessionInternal()
{
    // 기존 OnPlayButtonClicked 본문 (host.StartSession ~ GameStarted?.Invoke) 그대로 이동
}
```

신규 SerializeField:

```csharp
[Header("Input Mode Readiness (sub-spec 17)")]
[SerializeField] XRInputModeProbe inputModeProbe;
[SerializeField] InputModeReadinessNotice readinessNoticePrefab;
InputModeReadinessNotice _readinessNoticeInstance;
Coroutine _readinessPollCoroutine;
```

기존 `_provider.ActiveInstrumentChanged += OnActiveInstrumentChanged`/`OnDisable`/`Inject` 시점에 `_readinessNoticeInstance?.Hide()` + 코루틴 중단 호출을 끼워 악기가 바뀌면 안내 UI도 닫힌다(spec 17 Behavior의 자동 close와 직교적인 추가 안전망).

### 6. `RhythmGameHost`/`SessionPanelController` 변경 없음

`SessionPanelController.OnRhythmGameStarted`(lines 262-280)는 `GameStarted` 이벤트로 트리거되므로 본 plan의 readiness gate가 통과한 *후* `StartSessionInternal`이 호출돼야 정상 트리거된다. 그래서 readiness gate 차단 시점에는 `GameStarted`를 발화시키지 *않는다* — 패널이 닫히지 않고 사용자가 차단 안내를 볼 수 있다. 폴링 코루틴이 자동 시작될 때 `StartSessionInternal` 안에서 정상 `GameStarted?.Invoke()`가 발화되며 SessionPanel이 정상 hide된다.

### 7. Scene wiring (TestSceneSanyo)

- SessionPanelController GameObject(`2039824624`)에 `XRInputModeProbe` 컴포넌트 1개 추가, `handTrackingRoot`/`controllerRoot` SerializeField에 VR Player 인스턴스의 `LeftHandTrackingGhostHand`/`LeftControllerGhostHand` root GameObject 박제.
- SessionPanel prefab 안 `RhythmGameSectionController`의 `inputModeProbe` SerializeField에 위 컴포넌트 박제, `readinessNoticePrefab`에 `InputModeReadinessNotice.prefab` 박제.

scene 변경은 SessionPanelController 인스턴스 위 1개 컴포넌트 + RhythmGameSection의 SerializeField 2개. SessionPanel.prefab의 `RhythmGameSectionController` 자식에도 동일 SerializeField가 노출되므로 prefab-level wiring(default `InputModeReadinessNotice.prefab` 참조)도 가능 — `inputModeProbe`는 scene-only이므로 scene wiring으로만 박제.

### 8. 컴파일·테스트 게이트

- `manage_script` 미사용 — Edit으로 직접 수정.
- 컴파일 확인: `read_console` MCP로 error 0건 확인.
- `unity-test-runner` 1회 호출 — SessionPanel.Tests의 `RhythmGameSection*Tests.cs`가 기존 회귀를 잡는지 확인. Dummy 기반 테스트라 `XRInputModeProbe` SerializeField가 null이면 `TryGateInputMode` 내부에서 `inputModeProbe != null ? ... : InputMode.Any` 분기로 PASS — 기존 EditMode 테스트는 그대로 통과.

### 9. spec 17 Out of Scope 답습

- Quest 시스템 메뉴 직접 진입 / 자동 모드 전환 / 다른 사전 조건 / Quest 설정 딥링크 — 본 plan은 표시·차단·자동 재시작만.

## Deliverables

- `Assets/Instruments/_Core/Scripts/InputMode.cs` — 신규 enum `Any=0/HandTracking=1/Controller=2`.
- `Assets/Instruments/_Core/Scripts/InstrumentBase.cs` — `requiredInputMode` SerializeField + public getter 1개 추가. 기존 동작 영향 0.
- `Assets/SessionPanel/Scripts/XRInputModeProbe.cs` — 신규 MonoBehaviour. `handTrackingRoot`/`controllerRoot` `activeInHierarchy`로 `Current` 반환.
- `Assets/SessionPanel/Scripts/InputModeReadinessNotice.cs` — 신규 안내 UI 컨트롤러. `Show(required)`/`Hide()`.
- `Assets/SessionPanel/Prefabs/InputModeReadinessNotice.prefab` — WorldSpace Canvas + TMP_Text + Image.
- `Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` — `OnPlayButtonClicked` 본문을 `TryGateInputMode` + `StartSessionInternal` + `PollReadinessAndAutoStart`로 분리. SerializeField 2개 추가.
- `Assets/Scenes/TestSceneSanyo.unity` — SessionPanelController GameObject에 `XRInputModeProbe` 컴포넌트 추가 + 두 root 박제. RhythmGameSectionController(SessionPanel 인스턴스 자식)의 `inputModeProbe`/`readinessNoticePrefab` 박제.

asmdef 추가 없음. 패키지 의존성 추가 없음.

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Instruments/_Core/Scripts/InputMode.cs`에 `namespace Instruments` + `enum InputMode { Any = 0, HandTracking = 1, Controller = 2 }`가 박제되어 있다.
  **검증:** `Grep -n "enum InputMode" Assets/Instruments/_Core/Scripts/InputMode.cs` 1건 + 그 다음 줄에 `Any = 0` / `HandTracking = 1` / `Controller = 2` 3건 모두 매칭.

- [ ] `[auto-hard]` `InstrumentBase.cs`에 `requiredInputMode` SerializeField가 `Instruments.InputMode` 타입으로 박제되고 public getter `RequiredInputMode`가 노출되어 있다.
  **검증:** `Grep -n "SerializeField.*InputMode requiredInputMode" Assets/Instruments/_Core/Scripts/InstrumentBase.cs` 1건 + `Grep -n "public InputMode RequiredInputMode" Assets/Instruments/_Core/Scripts/InstrumentBase.cs` 1건. 둘 모두 line 52(`instrumentNetId`) 이후 위치.

- [ ] `[auto-hard]` `XRInputModeProbe.cs`의 `Current` getter가 `handTrackingRoot.activeInHierarchy` 우선 분기 → `controllerRoot.activeInHierarchy` 두번째 분기 → 양쪽 false면 `InputMode.Any` 반환 순서로 박제되어 있다 (`PhysicsHandGhostFollower.ResolveSourceMode` 답습).
  **검증:** `Grep -n "handTrackingRoot" Assets/SessionPanel/Scripts/XRInputModeProbe.cs` ≥ 1건 + `Grep -n "controllerRoot" Assets/SessionPanel/Scripts/XRInputModeProbe.cs` ≥ 1건 + `Grep -n "InputMode.Any" Assets/SessionPanel/Scripts/XRInputModeProbe.cs` ≥ 1건. 추가로 분기 순서: `Grep -n "activeInHierarchy" Assets/SessionPanel/Scripts/XRInputModeProbe.cs` 의 첫 매칭이 `handTrackingRoot`를 가리킴(line context로 확인).

- [ ] `[auto-hard]` `RhythmGameSectionController.OnPlayButtonClicked`가 readiness gate를 통과하지 못하면 `host.StartSession` 호출 / `GameStarted?.Invoke` 발화를 모두 *건너뛴다*.
  **검증:** `Grep -n "TryGateInputMode" Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` ≥ 2건(정의 1 + 호출 ≥ 1) + `Grep -n "StartSessionInternal" Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` ≥ 2건 + `OnPlayButtonClicked` 본문 안의 `host.StartSession` 직접 호출 라인이 0건이어야 하고 `StartSessionInternal()` 호출이 1건이어야 함 (`Grep -n "host.StartSession" Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` 1건이 `StartSessionInternal` 안에 위치하는지 line context 확인).

- [ ] `[auto-hard]` `InputModeReadinessNotice.cs`가 `Show(Instruments.InputMode required)` / `Hide()` 두 메서드를 노출한다.
  **검증:** `Grep -n "public void Show" Assets/SessionPanel/Scripts/InputModeReadinessNotice.cs` 1건 + `Grep -n "public void Hide" Assets/SessionPanel/Scripts/InputModeReadinessNotice.cs` 1건.

- [ ] `[auto-hard]` `Assets/SessionPanel/Prefabs/InputModeReadinessNotice.prefab`이 존재하고 `InputModeReadinessNotice` MonoBehaviour 1개를 자식에 가진다(WorldSpace Canvas + TMP_Text 포함).
  **검증:** `Glob Assets/SessionPanel/Prefabs/InputModeReadinessNotice.prefab` 1건 + `Grep -n "m_EditorClassIdentifier.*InputModeReadinessNotice" Assets/SessionPanel/Prefabs/InputModeReadinessNotice.prefab` 1건 + `Grep -n "TextMeshProUGUI" Assets/SessionPanel/Prefabs/InputModeReadinessNotice.prefab` ≥ 1건.

- [ ] `[auto-hard]` `TestSceneSanyo.unity`의 SessionPanelController GameObject(2039824624) 컴포넌트 목록에 `XRInputModeProbe` MonoBehaviour가 추가되어 있고 `handTrackingRoot`/`controllerRoot` 두 SerializeField가 모두 비-zero fileID로 박제되어 있다.
  **검증:** `Grep -n "m_EditorClassIdentifier.*XRInputModeProbe" Assets/Scenes/TestSceneSanyo.unity` 1건 + 그 MonoBehaviour 블록 내부에 `handTrackingRoot: {fileID: <비-zero>}`, `controllerRoot: {fileID: <비-zero>}` 각 1건씩. 두 fileID는 scene 상의 `LeftHandTrackingGhostHand`/`LeftControllerGhostHand` GameObject(VR Player 인스턴스의 자식)와 일치해야 함 — 추가 grep으로 두 stripped GameObject(GUID `411666a83e4ce264dbaf035f51b198ca`/`a7f32f4296ccdfb42b0a66dfd8c4425e`)와 fileID 매칭.

- [ ] `[auto-soft]` Unity Editor 컴파일 시 SessionPanel·Instruments asmdef에서 error 0건. Play 모드 진입 직후 `read_console` filter `InputModeReadinessNotice|XRInputModeProbe|InstrumentBase|requiredInputMode` 에서 NullReferenceException/ArgumentException 0건.
  **검증:** `read_console action=get types=["error"] filter_text="InputModeReadinessNotice|XRInputModeProbe|InstrumentBase|requiredInputMode"` 결과 0건. MCP 미가용 시 Editor Console 시각 확인 후 0건 보고.

- [ ] `[auto-soft]` SessionPanel.Tests의 EditMode 회귀가 모두 통과한다. `RhythmGameSection*Tests.cs`가 `RhythmGameSectionController.OnPlayButtonClicked` null guard 동작을 검증하는 케이스를 갖고 있다면 `inputModeProbe == null` 경로(테스트 환경)에서 `TryGateInputMode` 가 PASS 반환 → 기존 동작 그대로 유지.
  **검증:** `unity-test-runner` 호출 EditMode 결과 N/N PASS(기존 회귀 baseline 유지).

- [ ] `[manual-hard]` (Behavior 1, 핸드 트래킹 요구 + 컨트롤러 활성) Trombone의 `requiredInputMode`를 인스펙터에서 임시로 `HandTracking`으로 박제 → Play 모드 진입 후 컨트롤러를 잡은 상태에서 트롬본 anchor 텔레포트 → SessionPanel 열고 곡·난이도 선택 → 플레이 버튼 클릭 → 게임이 *시작되지 않고* `InputModeReadinessNotice(Clone)`이 카메라 정면에 나타나며 "핸드 트래킹으로 전환해 주세요" 메시지가 보인다. RhythmGame note panel은 등장하지 않는다.
  **검증:** Hierarchy에서 `InputModeReadinessNotice(Clone)` GameObject `m_IsActive=true` + Trombone의 RhythmGameHost 자식에 활성화된 note panel 0건 + Scene 뷰에서 안내 텍스트 시각 확인.

- [ ] `[manual-hard]` (Behavior 2, 입력 모드 전환 시 자동 시작) Behavior 1 차단 상태에서 컨트롤러를 내려놓고 Quest를 핸드 트래킹 모드로 전환(또는 Editor에서 `LeftControllerGhostHand` GameObject SetActive(false) + `LeftHandTrackingGhostHand` SetActive(true) 시뮬레이트) → `InputModeReadinessNotice(Clone)`가 자동으로 사라지고 RhythmGame 세션이 자동 시작되어 note panel이 등장한다.
  **검증:** Hierarchy `InputModeReadinessNotice(Clone)` `m_IsActive=false`(또는 Destroyed) + Trombone RhythmGameHost 자식에 활성화된 note panel 1건 + `GameStarted` 이벤트로 SessionPanel hide 시각 확인.

- [ ] `[manual-hard]` (Behavior 3, 일치 시 즉시 시작) Trombone의 `requiredInputMode`를 `Controller`로 박제 → 컨트롤러 활성 상태에서 트롬본 anchor 텔레포트 → 플레이 버튼 클릭 → 게임이 *즉시* 시작(안내 UI 0초). 이어서 `requiredInputMode = Any`로 다시 박제 → 입력 모드 무관하게 즉시 시작 확인(Behavior 4 직답).
  **검증:** 플레이 버튼 클릭 → `InputModeReadinessNotice(Clone)` 인스턴스가 Hierarchy에 등장하지 *않음* + note panel 즉시 활성화 + SessionPanel hide. 두 케이스(Controller-Controller 일치 / Any) 모두 동일 결과.

## Out of Scope

- spec 17 Out of Scope 1~4 항목 답습 — Quest 시스템 메뉴 직접 진입·자동 모드 전환·다른 사전 조건 확인·Quest 설정 딥링크.
- `IActiveInstrument` 인터페이스에 `RequiredInputMode` 추가 — 본 plan은 down-cast (`as InstrumentBase`)로 대신해 인터페이스 표면 확장을 회피. 향후 dummy/non-InstrumentBase 활성 악기 구현이 생기면 별도 plan에서 인터페이스 확장.
- 인스펙터 wiring 본 작업 — 각 악기 prefab의 `requiredInputMode` 값을 어디로 박제할지(Piano=HandTracking? Drum=Controller? Trombone=Controller?)는 사용자 결정. 본 plan은 enum/필드만 도입하고 기본값 `Any`로 두며 prefab별 값 박제는 후속 plan(또는 사용자 인스펙터 직접 설정)에 위임.
- 안내 UI 디자인 — 본 plan은 최소 prefab 골격(Canvas + TMP_Text + Image)만 제공. 시각 디자인·다국어·아이콘 sprite 자산은 후속 plan.
- SessionPanelController 상태 머신 변경 — readiness gate는 SessionPanelController의 PanelState와 직교적으로 동작(SessionPanel은 그대로 열려 있으며 안내 UI는 별도 패널).
- XR Hands 패키지 API(`XRHandSubsystem.OnHandTrackingStatusChanged` 등) 직접 사용 — 본 plan은 `PhysicsHandGhostFollower` 패턴 답습으로 GameObject `activeInHierarchy`만 사용. 패키지 API 직접 통합은 후속 plan.
- 비-`InstrumentBase` 활성 악기에 대한 readiness 처리 — `as InstrumentBase` cast 실패 시 PASS로 폴백.

## Notes

- `InputMode.Any` 인덱스를 명시적으로 `= 0`으로 박제하는 이유: Unity SerializeField가 enum 기본값을 인덱스 0으로 직렬화하므로, 기존 prefab들(Trombone/Piano/DrumKit)이 새 필드를 자동으로 `Any`로 받아 *기존 동작 무변경* 보장.
- "양쪽 root 모두 비활성 → `Any`" 폴백 정책은 Editor XR Device Simulator 시나리오에서 의도된 동작. 헤드셋 실기에서는 두 root 중 정확히 1개만 active한 상태가 일반적이라 이 폴백은 Editor 한정으로 거의 동작.
- `PhysicsHandGhostFollower.ResolveSourceMode`는 `GhostSourceMode.None`을 반환하면 sync 자체를 skip하지만, 본 plan의 `XRInputModeProbe`는 `Any`(=PASS)로 매핑하는 게 의미적으로 정확. None/Any 명칭 불일치는 의도된 분리.
- `RhythmGameSectionController.Inject(...)`는 SessionPanel prefab이 instantiate될 때 호출되어 SerializeField들이 다시 wiring되지 않는다. `inputModeProbe`/`readinessNoticePrefab`는 SessionPanel.prefab의 prefab-level SerializeField로 박제하고, `inputModeProbe`만 scene wiring으로 인스턴스에 박는 형태가 가장 안전. (scene wiring의 prefab override 1줄로 끝.)
- 후속 plan 후보: (a) 악기 prefab별 `requiredInputMode` 값 박제(Piano=HandTracking, Trombone/Drum=Controller 가 자연스러운 디폴트), (b) 안내 UI 디자인 + 다국어 + Quest 설정 진입 안내 텍스트 폴리시, (c) XR Hands `XRHandSubsystem`을 직접 구독하는 `XRInputModeProbe` v2(이벤트 기반 — 매 프레임 `activeInHierarchy` 폴링 회피).
- 폴링 코루틴은 `LateUpdate` 단위로 1 frame마다 한 번씩 `TryGateInputMode`를 호출 — `XRInputModeProbe.Current`가 `activeInHierarchy` 두 번 호출이라 비용 무시. 다만 매 프레임 폴링이 싫으면 0.2s 주기로 `WaitForSeconds(0.2f)`로 늘려도 spec 17 What/Behavior 충족(사용자 모드 전환은 즉시 < 1s 응답이면 충분).

## Handoff

- [2026-05-28 완료] `Instruments.InputMode` enum (`Any=0/HandTracking=1/Controller=2`), `InstrumentBase.requiredInputMode` SerializeField + `RequiredInputMode` getter, `XRInputModeProbe` MonoBehaviour, `InputModeReadinessNotice` prefab+컨트롤러 구현.
- `RhythmGameSectionController.OnPlayButtonClicked` → `TryGateInputMode` + `StartSessionInternal` + `PollReadinessAndAutoStart` + `StopReadinessGate` 분리. `inputModeProbe == null`이면 Awake에서 `FindObjectOfType<XRInputModeProbe>()`로 자동 탐색.
- `TestSceneSanyo.unity` SessionPanelController GameObject에 `XRInputModeProbe` 컴포넌트 추가, `handTrackingRoot: 1928446971`, `controllerRoot: 246256112` wiring.
- auto-hard AC 7건 / auto-soft 2건 Grep+read_console PASS. EditMode 133/133 + PlayMode 4/4 PASS.
- MH 시나리오: requiredInputMode=Any 기본값으로 즉시 시작 동작 확인. 불일치 시나리오(MH-1/2)는 Editor에서 Ghost hand GameObject 수동 활성화 필요 — 디바이스에서 최종 검증 예정.
- 각 악기 prefab(Piano/Trombone/DrumKit)의 `requiredInputMode` 값은 기본값 `Any` 유지 — 후속 plan에서 실제 값 박제 예정.
