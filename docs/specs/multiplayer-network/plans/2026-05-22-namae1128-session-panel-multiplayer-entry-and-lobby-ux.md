# 세션 패널 멀티플레이 진입 통합 + LobbyPanel MaxPlayers 스테퍼 + 비밀번호 오버레이

**Linked Spec:** [`07-lobby-panel-ui.md`](../specs/07-lobby-panel-ui.md)
**Status:** `Ready`

## Goal

(1) 세션 패널(SessionPanel)에 "멀티플레이" 항목을 볼륨·리듬게임처럼 한 섹션으로 추가해, 버튼 클릭으로 TestSceneSanyo 의 기존 `MultiplayerAuthGate` 루트(WorldSpace Canvas)를 토글 표시한다. (2) `MultiplayerLobbyPanel` 의 `MaxPlayersInput` (TMP_InputField) 를 `- / 숫자 / +` 스테퍼로 교체해 1~32 정수 클램프. (3) 잠금 방 행 클릭 시 즉시 입장이 아닌 비밀번호 입력 오버레이를 표시. spec 07 의 What 4건 (세션 패널 버튼 항목 + 스테퍼 + 잠금 오버레이) 을 한 번에 좁히되, `MultiplayerLobbyPanel.cs` 의 public API·SerializeField 12건 + InRoomPanel 와이어는 보존한다.

## Context

`presence-ui-lobby-migration-and-ux` plan (Handoff 박제) 이후 TestSceneSanyo 에는 다음이 영구 박제돼 있다 — `MultiplayerAuthGate`(root fileID `800001000`, WorldSpace Canvas, LocalPos `(0,0,1.8)`, LocalRotY=90°, AnchoredPos `(4,1)`) / `MultiplayerLobbyPanel`(root fileID `310000000`, MonoBehaviour `310000001`) + `LobbyRoot`(`310001000`, `m_IsActive: 0`) + 자식 트리 (CreateForm / RoomList / StatusLabel) / `MultiplayerInRoomPanel`(`2045667644`) / `SessionPanelController`(root `2039824624`). backend 흐름 (`CreateRoomThroughBackendAsync` / `JoinRoomAsync` / DS READY 콜백 + Photon 합류) 도 e2e 검증 통과 (2026-05-19 commit history).

본 plan 의 세 가지 변경 사유:

- **세션 패널 통합 (사용자 명시 요구)** — 현재 멀티플레이 진입은 `MultiplayerAuthGate` Canvas 자체가 default 씬에 항상 활성 상태로 떠 있고 `ActivateButton` 으로 인증 흐름을 시작한다. 사용자는 *"지금 현재 있는 멀티플레이 패널을 세션 패널에서 볼륨 조절·리듬게임 진입과 같이 하나의 항목으로 넣고 싶어"* 라고 명시. SessionPanelController 가 `panelPrefab` 을 Instantiate 한 뒤 `VolumeSectionController` / `RhythmGameSectionController` 를 `GetComponentInChildren` 으로 찾아 Inject 하는 패턴을 그대로 답습 — 새 `MultiplayerSectionController` 가 같은 패턴으로 자동 와이어된다. **asmdef 의존 회피**: SessionPanel.Runtime asmdef 에 `Murang.Multiplayer` 를 추가하지 않기 위해 새 controller 는 `[SerializeField] private GameObject multiplayerPanelRoot;` 만 들고 toggle 시 `SetActive` 만 호출. SessionPanelController 는 새 `[SerializeField] private GameObject _multiplayerPanelObject;` 1개를 추가해 Inspector 에서 `MultiplayerAuthGate` GameObject 를 와이어 → `EnsurePanelInstance()` 의 inject 단계에서 controller 에 전달.
- **MaxPlayers 스테퍼** — 현재 `MaxPlayersInput` (TMP_InputField, fileID `310006000`) 1개로 자유 텍스트 입력 → VR 키보드 미스 입력 위험 + spec 07 의 "(- 버튼 / 숫자 표시 / + 버튼)" 요구 불일치. backend validation 범위 (`LobbyInputValidator.MinMaxPlayers = 1`, `MaxMaxPlayers = 32`) 는 그대로 재사용.
- **잠금 방 오버레이** — 현재 `HandleJoinRequested(RoomListEntry entry)` 가 좌측 `CreateForm.PasswordInput` 텍스트를 그대로 사용 → 룸 생성 폼과 잠금 방 입장이 같은 입력칸을 공유해 UX 혼동 + spec 07 의 "방 목록 위에 오버레이 팝업" 요구 불일치. `entry.IsLocked` 분기에 별도 오버레이 root + confirm/cancel 흐름을 신설.

씬·코드 양쪽 모두 최소 변경 원칙. **세션 패널 통합 부분은 SessionPanel.prefab 안 VolumeSectionController/RhythmGameSectionController 자식 트리·MultiplayerInRoomPanel·MultiplayerAuthGate·VRKeyboard·VR Player·Trombone·기타 root 모두 손대지 않는다.**

## Verified Structural Assumptions

- `SessionPanelController.EnsurePanelInstance()` (lines 238-256) 패턴: `_panelInstance = Instantiate(panelPrefab); _panelInstance.SetActive(false); _volCtrl = _panelInstance.GetComponentInChildren<VolumeSectionController>(true); _rhythmCtrl = _panelInstance.GetComponentInChildren<RhythmGameSectionController>(true);`. **신규 `MultiplayerSectionController` 도 같은 자리에서 `GetComponentInChildren<MultiplayerSectionController>(true)` 로 자동 수집 후 `Inject(_multiplayerPanelObject)` 호출.** controller 의 부작용: SessionPanel 이 Hidden 으로 닫혀도 multiplayerPanelRoot 자체는 controller 가 관리 (SessionPanel SetActive 와 직교) — 즉 사용자가 멀티플레이 진입 후 SessionPanel 을 닫아도 MultiplayerAuthGate Canvas 는 유지된다. — `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-22)`.
- `SessionPanel.Runtime.asmdef` references = `Instruments`, `RhythmGame.Data`, `RhythmGame.Runtime`, `Unity.InputSystem`, `Unity.XR.Interaction.Toolkit`, `Unity.TextMeshPro`. **`Murang.Multiplayer` 없음.** 본 plan 의 신규 `MultiplayerSectionController.cs` 는 `Murang.Multiplayer` 타입을 import 하지 않고 `GameObject.SetActive(bool)` 만 호출하므로 asmdef reference 추가 불필요. — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-22)`.
- `Murang.Multiplayer.asmdef` references = `Fusion.Unity`, `Unity.TextMeshPro`, autoReferenced=true. LobbyPanel 코드 변경분(`MultiplayerLobbyPanel.cs`)은 같은 asmdef 안 `Assets/Multiplayer/Scripts/Presence/` 에 머무르므로 reference 추가 불필요. — `Read Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef (2026-05-22)`.
- TestSceneSanyo `MultiplayerAuthGate` root (fileID `800001000`, `m_IsActive: 1`) — Canvas (`800001002`, RenderMode=WorldSpace) + CanvasScaler (`800001003`) + GraphicRaycaster (`800001004`) + `MultiplayerAuthGate` MonoBehaviour (`800001005`, `MultiplayerLobbyPanel.authGate` SerializeField 가 이 fileID 를 참조) + 1개 추가 컴포넌트 (`800001006`). 자식 2건 (fileID `800001011`, `800001031`). **본 plan 은 이 root 의 `m_IsActive` 를 직렬화 단계에서 변경하지 않는다** — 런타임 `multiplayerPanelRoot.SetActive(false)` 초기화는 새 controller 의 `Awake()` 에서 1회 수행. — `Read Assets/Scenes/TestSceneSanyo.unity:5076-5117 (2026-05-22)`.
- TestSceneSanyo `MultiplayerLobbyPanel` root (`310000000`) MonoBehaviour SerializeField 12건 (`authGate`/`roomClient`/`roomListQuery`/`authConfig`/`roomNameInput`/`passwordEnabledToggle`/`passwordInput`/`maxPlayersInput`/`createButton`/`roomRowParent`/`roomRowPrefab`/`emptyLabel`/`statusLabel`/`lobbyRoot`) 가 현재 모두 non-zero fileID. 본 plan 은 `maxPlayersInput`(TMP_InputField) 한 건만 제거하고 신규 3건(`maxPlayersDisplay`/`maxPlayersMinusButton`/`maxPlayersPlusButton`) + 잠금 오버레이용 4건(`passwordOverlayRoot`/`passwordOverlayInput`/`passwordOverlayConfirmButton`/`passwordOverlayCancelButton`) 을 추가. **나머지 11건은 fileID·GUID 그대로 보존**. — `Read Assets/Scenes/TestSceneSanyo.unity:695-720 (2026-05-22)`.
- TestSceneSanyo `MultiplayerInRoomPanel.lobbyPanel` SerializeField = `{fileID: 310000001}` (`MultiplayerLobbyPanel` MonoBehaviour). 본 plan 은 이 cross-ref 를 건드리지 않는다. — `Read Assets/Scenes/TestSceneSanyo.unity:7201-7218 (2026-05-22)`.
- `MultiplayerLobbyPanel.cs` 의 `HandleJoinRequested(RoomListEntry entry)` (line 300) 가 잠금 분기의 단일 진입점. 현재 line 313-323: `passwordInput.text` → `ValidateJoinPassword(entry.IsLocked, passwordRaw)` → `RunJoinRoomAsync(entry, entry.IsLocked ? passwordRaw : null)`. **본 plan 은 비잠금이면 즉시 `RunJoinRoomAsync(entry, null)`, 잠금이면 `_pendingJoinEntry = entry; OpenPasswordOverlay();` 로 분기**. `RoomJoinOptions(playerId, roomName, passwordOrNull)` 3번째 인자가 nullable 비밀번호 — 그대로 재사용. — `Read Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs:300-345 (2026-05-22)`.
- `LobbyInputValidator.ValidateMaxPlayers(int)` 가 1~32 범위 검증 (line 85-94). 스테퍼는 동일 범위로 internal clamp 하므로 invalid 입력 자체가 발생하지 않지만, `OnCreateClicked` 의 `ValidateCreate(..., maxPlayers, ...)` 호출 (line 165-169) 은 그대로 두어 backend rule 의 단일 진실원을 유지. `ValidateJoinPassword(locked, raw)` (line 58-61) 는 잠금 오버레이의 confirm 시 동일 호출. — `Read Assets/Multiplayer/Scripts/Presence/LobbyInputValidator.cs (2026-05-22)`.
- `VRKeyboardField` 컴포넌트: TMP_InputField 와 같은 GameObject 에 부착하면 `OnPointerClick` → `keyboard.Open(_inputField)`. `keyboard` 미연결 시 `Start()` 에서 `FindObjectOfType<VRWorldKeyboard>(true)` 자동 탐색. **신설 `PasswordOverlayInput` TMP_InputField 에도 `VRKeyboardField` 만 부착하면 자동 와이어** (이전 plan Handoff 박제). — Handoff 박제 인용 (`2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md` Phase C).
- TestSceneSanyo `MultiplayerLobbyPanel/LobbyRoot/CreateForm/MaxPlayersInput` (fileID `310006000`) RectTransform 부모 `m_Father: {fileID: 310002001}` (CreateForm). 컴포넌트 5건 (RectTransform / Image / CanvasRenderer / TMP_InputField / 추가 1건). 자식 1개 (text area). 삭제 시 같은 CreateForm 자식 sibling 들 (RoomNameInput / PasswordEnabledToggle / PasswordInput / CreateButton) 은 그대로. — 이전 plan Handoff 박제 (`2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md`).
- TestSceneSanyo `MultiplayerLobbyPanel/LobbyRoot/RoomList` (fileID `310008000`) 자식 2건 (ScrollView + EmptyLabel). 본 plan 의 `PasswordJoinOverlay` 는 RoomList 의 sibling order 마지막 자식으로 신설 → uGUI render order 상 위에 표시 + LobbyRoot inactive 시 함께 숨김. — 이전 plan Handoff 박제 동일 출처.

## Approach

### Phase 1 — SessionPanel 멀티플레이 섹션 신설 (asmdef 분리 유지)

1. **신규 `Assets/SessionPanel/Scripts/MultiplayerSectionController.cs`** (namespace `SessionPanel`):
   - 클래스 `MultiplayerSectionController : MonoBehaviour` + `[SerializeField] private Button entryButton;` + `[SerializeField] private TMP_Text buttonLabel;` (선택, 디폴트 "Multiplayer") + 내부 `GameObject _multiplayerPanelRoot;`.
   - `public void Inject(GameObject multiplayerPanelRoot)` — SessionPanelController 가 `EnsurePanelInstance()` 에서 호출. 내부 필드에 저장 + `_multiplayerPanelRoot.SetActive(false)` (초기 닫힌 상태). 단 multiplayerPanelRoot 가 null 이면 entryButton.interactable=false 로 grace.
   - `Awake()` 에서 `entryButton.onClick.AddListener(OnEntryClicked);` `OnDestroy()` 대칭 RemoveListener.
   - `OnEntryClicked()` — `if (_multiplayerPanelRoot == null) return; _multiplayerPanelRoot.SetActive(!_multiplayerPanelRoot.activeSelf);` (토글). 진입 진단 로그 추가 금지 (CLAUDE.md "상시 규칙").
   - **import**: `UnityEngine`, `UnityEngine.UI`, `TMPro`. **Murang.Multiplayer 미import**. asmdef reference 변경 불필요.

2. **`Assets/SessionPanel/Scripts/SessionPanelController.cs` 수정**:
   - `[SerializeField] private GameObject _multiplayerPanelObject;` 추가 (header `[Header("Multiplayer integration")]`).
   - 필드 `private MultiplayerSectionController _mpCtrl;` 추가.
   - `EnsurePanelInstance()` 의 `_rhythmCtrl` inject 블록 뒤에 다음 추가:
     ```
     _mpCtrl = _panelInstance.GetComponentInChildren<MultiplayerSectionController>(true);
     if (_mpCtrl != null) _mpCtrl.Inject(_multiplayerPanelObject);
     ```
   - 그 외 메서드·필드는 손대지 않는다. PanelState 머신·VR 인터랙터 토글·hit dot 등 기존 로직 보존.

3. **`Assets/SessionPanel/Prefabs/SessionPanel.prefab` 자식 트리에 멀티플레이 섹션 추가**:
   - SessionPanel.prefab 안 `VolumePanel`(name "VolumePanel") + `RhythmGamePanel` 형식과 같은 형태로 새 panel root 추가 (예: 신규 `MultiplayerPanel` GameObject — `VerticalLayoutGroup` 또는 부모 TabPanelController 의 `tabPanels[]` 슬롯에 추가). 자식 1건 `EntryButton` (UI Button) + 그 자식 Label (TMP_Text "Multiplayer"). `MultiplayerSectionController` 컴포넌트는 `MultiplayerPanel` GameObject 에 부착 + `entryButton` SerializeField 를 자식 Button 으로 와이어 + `buttonLabel` 도 옵션 와이어.
   - SessionPanel.prefab 의 `TabPanelController.tabButtons[]` + `tabPanels[]` 배열에 새 탭 추가 — 기존 `VolumeTabButton`/`RhythmTabButton` 옆에 `MultiplayerTabButton` 신설. 사용자 강조: **VolumeSectionController / RhythmGameSectionController 의 기존 자식 트리는 절대 건드리지 않는다.**
   - 추가 RectTransform/Layout 디자인은 implementer 가 prefab 편집 시 결정 (manual-hard 가독성 검증).

4. **TestSceneSanyo.unity 와이어**:
   - SessionPanelController (root `2039824624`, MonoBehaviour `2039824626`) 의 신규 SerializeField `_multiplayerPanelObject` 를 `MultiplayerAuthGate` GameObject (fileID `800001000`) 로 직렬화 와이어.
   - 다른 root GameObject (VR Player, Trombone, Piano, DrumKit, RhythmGameHost\_\*, Environment, InstrumentSystem, StubSongCatalog, MultiplayerLobbyPanel, MultiplayerInRoomPanel, VRKeyboard, MultiplayerRoomNetworking, MultiplayerAuthBootstrap) 일체 변경 금지.

### Phase 2 — LobbyPanel MaxPlayers 스테퍼 + 비밀번호 오버레이 (코드)

`Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs` 수정:

1. **SerializeField 교체·추가**:
   - 삭제: `[SerializeField] private TMP_InputField maxPlayersInput;`
   - 추가:
     ```
     [Header("UI — MaxPlayers stepper")]
     [SerializeField] private TMP_Text maxPlayersDisplay;
     [SerializeField] private Button maxPlayersMinusButton;
     [SerializeField] private Button maxPlayersPlusButton;
     [SerializeField] private int maxPlayersInitial = 4;

     [Header("UI — Password join overlay")]
     [SerializeField] private GameObject passwordOverlayRoot;
     [SerializeField] private TMP_InputField passwordOverlayInput;
     [SerializeField] private Button passwordOverlayConfirmButton;
     [SerializeField] private Button passwordOverlayCancelButton;
     ```

2. **스테퍼 상태 머신**:
   - 내부 필드 `private int _maxPlayers;` + `private RoomListEntry _pendingJoinEntry;`.
   - `Awake()` 의 `RefreshPasswordInputState()` 호출 직전에:
     ```
     _maxPlayers = Mathf.Clamp(maxPlayersInitial, LobbyInputValidator.MinMaxPlayers, LobbyInputValidator.MaxMaxPlayers);
     RefreshMaxPlayersDisplay();
     if (maxPlayersMinusButton != null) maxPlayersMinusButton.onClick.AddListener(OnMaxPlayersMinus);
     if (maxPlayersPlusButton  != null) maxPlayersPlusButton .onClick.AddListener(OnMaxPlayersPlus);
     if (passwordOverlayRoot != null) passwordOverlayRoot.SetActive(false);
     if (passwordOverlayConfirmButton != null) passwordOverlayConfirmButton.onClick.AddListener(OnPasswordOverlayConfirm);
     if (passwordOverlayCancelButton  != null) passwordOverlayCancelButton .onClick.AddListener(OnPasswordOverlayCancel);
     ```
   - `OnDestroy()` 에 대칭 RemoveListener 4건 추가.
   - 신규 메서드 `OnMaxPlayersMinus()` / `OnMaxPlayersPlus()` — `_maxPlayers` ±1 후 `Mathf.Clamp(_, Min, Max)` + `RefreshMaxPlayersDisplay();`. 경계에서 minus/plus 버튼 `interactable` 토글.
   - `RefreshMaxPlayersDisplay()` — `if (maxPlayersDisplay != null) maxPlayersDisplay.text = _maxPlayers.ToString(); if (maxPlayersMinusButton != null) maxPlayersMinusButton.interactable = _maxPlayers > LobbyInputValidator.MinMaxPlayers; if (maxPlayersPlusButton != null) maxPlayersPlusButton.interactable = _maxPlayers < LobbyInputValidator.MaxMaxPlayers;`.

3. **`ParseMaxPlayers()` 대체**:
   - 기존 메서드 (line 449-456) 제거.
   - `OnCreateClicked()` 의 `int maxPlayers = ParseMaxPlayers();` → `int maxPlayers = _maxPlayers;` 한 줄 치환.

4. **잠금 방 오버레이 흐름**:
   - `HandleJoinRequested(RoomListEntry entry)` 의 line 313-323 분기 재작성:
     - 비잠금: `if (!entry.IsLocked) { await RunJoinRoomAsync(entry, null); return; }`.
     - 잠금: `_pendingJoinEntry = entry; OpenPasswordOverlay(); return;`. (await 없이 return → 메서드 종료.)
   - 신규 `OpenPasswordOverlay()` — `if (passwordOverlayInput != null) passwordOverlayInput.text = string.Empty; if (passwordOverlayRoot != null) passwordOverlayRoot.SetActive(true);`.
   - 신규 `async void OnPasswordOverlayConfirm()` — `string raw = passwordOverlayInput != null ? passwordOverlayInput.text : string.Empty; var v = LobbyInputValidator.ValidateJoinPassword(locked: true, raw); if (!v.Success) { SetStatus("Failed: " + v.ErrorMessage); return; } if (passwordOverlayRoot != null) passwordOverlayRoot.SetActive(false); var entry = _pendingJoinEntry; _pendingJoinEntry = default; await RunJoinRoomAsync(entry, raw);`.
   - 신규 `OnPasswordOverlayCancel()` — `if (passwordOverlayRoot != null) passwordOverlayRoot.SetActive(false); _pendingJoinEntry = default; if (passwordOverlayInput != null) passwordOverlayInput.text = string.Empty;`.

5. **로그 정책 준수** — 기존 `Debug.LogError`/`LogWarning` catch 만 유지. 신규 진단 로그 추가 금지.

### Phase 3 — LobbyPanel 씬 구조 변경 (TestSceneSanyo.unity)

본 plan 은 plan 본문에 박제된 fileID 좌표를 기준으로 Unity Editor 에서 직접 편집 (또는 unity-scene-writer 보조). 변경 범위는 `MultiplayerLobbyPanel/LobbyRoot/` 하위로 한정.

1. **MaxPlayersInput → MaxPlayersStepper 교체**:
   - 기존 `MaxPlayersInput`(fileID `310006000`) GameObject + 모든 컴포넌트·자식 삭제.
   - 신설 GameObject `MaxPlayersStepper` 를 CreateForm(`310002001`) 자식으로 추가. RectTransform: 기존 AnchorMin/Max/AnchoredPos/SizeDelta 와 동일하되 가로 확장 (예 `(300, 80)`). HorizontalLayoutGroup(spacing=10, childAlignment=Middle Center, childControlWidth/Height=false, childForceExpandWidth/Height=false) 부착.
   - 자식 3건:
     - `MinusButton` (UI Button, SizeDelta `(60,60)`, 자식 Label TMP_Text="-", fontSize 36) → `maxPlayersMinusButton` 와이어.
     - `MaxPlayersDisplay` (TMP_Text, SizeDelta `(120,60)`, alignment Center, fontSize 36, 초기 text="4") → `maxPlayersDisplay` 와이어.
     - `PlusButton` (UI Button, SizeDelta `(60,60)`, 자식 Label TMP_Text="+", fontSize 36) → `maxPlayersPlusButton` 와이어.

2. **PasswordJoinOverlay 신설**:
   - `MultiplayerLobbyPanel/LobbyRoot/RoomList`(`310008000`) 의 sibling order 마지막 자식으로 신설. 이름 `PasswordJoinOverlay`, RectTransform stretch (Anchor `(0,0)~(1,1)`, offsetMin/Max 0).
   - `m_IsActive: 0` 으로 직렬화 (Awake 호출 전 안전 상태).
   - 자식 트리:
     - `Background` (Image, color `(0,0,0,0.7)`, stretch fill, raycastTarget=true → 뒤 행 클릭 차단).
     - `Panel` (Image 또는 빈 RectTransform, AnchoredPos `(0,0)`, SizeDelta `(500,300)`, 중앙):
       - `TitleLabel` (TMP_Text, text="Enter Password", alignment Center).
       - `PasswordOverlayInput` (TMP_InputField, ContentType=`Password`, SizeDelta `(440,60)`) + `VRKeyboardField` 컴포넌트 부착 (TMP_InputField 와 같은 GameObject) → `passwordOverlayInput` 와이어.
       - `ButtonRow` (HorizontalLayoutGroup):
         - `ConfirmButton` (UI Button, label "Confirm") → `passwordOverlayConfirmButton` 와이어.
         - `CancelButton` (UI Button, label "Cancel") → `passwordOverlayCancelButton` 와이어.

3. **`MultiplayerLobbyPanel` MonoBehaviour 와이어 갱신** (.unity 직렬화):
   - 기존 `maxPlayersInput` SerializeField 라인 제거 (필드 자체가 삭제됨).
   - 신규 7건 추가: `maxPlayersDisplay` / `maxPlayersMinusButton` / `maxPlayersPlusButton` / `maxPlayersInitial: 4` / `passwordOverlayRoot` / `passwordOverlayInput` / `passwordOverlayConfirmButton` / `passwordOverlayCancelButton`.
   - 보존 11건 (`authGate`/`roomClient`/`roomListQuery`/`authConfig`/`roomNameInput`/`passwordEnabledToggle`/`passwordInput`/`createButton`/`roomRowParent`/`roomRowPrefab`/`emptyLabel`/`statusLabel`/`lobbyRoot`) fileID·GUID 그대로.

### Phase 4 — 테스트·회귀

1. CLAUDE.md 정책 적용: 본 plan 은 Unity 런타임 `.cs` 2건(`MultiplayerSectionController.cs` 신설 + `SessionPanelController.cs` 수정 + `MultiplayerLobbyPanel.cs` 수정) 을 건드리므로 `unity-test-runner` 1회 호출해 EditMode 회귀(`LobbyInputValidatorTests`, `SessionPanelWiringTests`) 통과 확인.
2. Editor Play 모드 (또는 Quest 빌드) 에서:
   - SessionPanel 의 멀티플레이 탭/항목 클릭 → MultiplayerAuthGate 활성화 → 기존 인증 흐름 정상 진입.
   - LobbyPanel MaxPlayersStepper minus/plus 동작 + 1·32 경계에서 버튼 disable.
   - 잠금 방 행 클릭 → PasswordJoinOverlay 표시 → confirm 으로 join / cancel 로 dismiss.

## Deliverables

- `Assets/SessionPanel/Scripts/MultiplayerSectionController.cs` — 신규 (toggle controller, asmdef 변경 없음).
- `Assets/SessionPanel/Scripts/SessionPanelController.cs` — `_multiplayerPanelObject` SerializeField 추가 + `EnsurePanelInstance()` 에 MP controller inject 한 줄.
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab` — 멀티플레이 섹션 자식 트리 + TabPanelController 배열 갱신.
- `Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs` — `maxPlayersInput` 삭제 + 스테퍼 3 SerializeField + 잠금 오버레이 4 SerializeField + `ParseMaxPlayers()` 제거 + 스테퍼 상태 머신 + 잠금 분기 메서드 4건.
- `Assets/Scenes/TestSceneSanyo.unity` — (a) SessionPanelController `_multiplayerPanelObject` 와이어 (`MultiplayerAuthGate` fileID `800001000`) (b) `MaxPlayersInput` 삭제 + `MaxPlayersStepper` 신설 (3 자식) (c) RoomList 자식으로 `PasswordJoinOverlay` 신설 (Background + Panel + 자식 6건) (d) `MultiplayerLobbyPanel` MonoBehaviour 와이어 7건 추가 + 1건 삭제.

## Acceptance Criteria

### 코드 변경 (Phase 1·2)

- [ ] `[auto-hard]` `MultiplayerSectionController.cs` 신규 파일 존재 + `namespace SessionPanel` + `public void Inject(GameObject multiplayerPanelRoot)` 시그니처 존재 + `Murang.Multiplayer` import 0건.
  **검증:** `Grep "namespace SessionPanel" Assets/SessionPanel/Scripts/MultiplayerSectionController.cs` ≥ 1 매치 + `Grep -n "Murang.Multiplayer" Assets/SessionPanel/Scripts/MultiplayerSectionController.cs` 0 매치.
- [ ] `[auto-hard]` `SessionPanel.Runtime.asmdef` 의 `references` 배열에 `Murang.Multiplayer` 가 추가되지 않았다 (asmdef 분리 유지).
  **검증:** `Grep "Murang.Multiplayer" Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef` 0 매치.
- [ ] `[auto-hard]` `SessionPanelController.cs` 에 `_multiplayerPanelObject` SerializeField 및 `GetComponentInChildren<MultiplayerSectionController>` 호출 존재.
  **검증:** `Grep "_multiplayerPanelObject|GetComponentInChildren<MultiplayerSectionController>" Assets/SessionPanel/Scripts/SessionPanelController.cs` ≥ 2 매치.
- [ ] `[auto-hard]` `MultiplayerLobbyPanel.cs` 에서 `maxPlayersInput` 식별자 완전 제거 + `maxPlayersDisplay`/`maxPlayersMinusButton`/`maxPlayersPlusButton`/`passwordOverlayRoot`/`passwordOverlayInput`/`passwordOverlayConfirmButton`/`passwordOverlayCancelButton` 7건 신규 SerializeField 존재 + `ParseMaxPlayers` 식별자 0건.
  **검증:** `Grep "maxPlayersInput|ParseMaxPlayers" Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs` 0 매치 AND `Grep "maxPlayersDisplay|passwordOverlayRoot" Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs` ≥ 2 매치.
- [ ] `[auto-hard]` Unity EditMode 회귀 — `LobbyInputValidatorTests` + `SessionPanelWiringTests` 통과.
  **검증:** `unity-test-runner` subagent 1회 호출, 결과 14/14 (또는 변경 후 전체) 통과 + console error 0건.

### 씬 직렬화 정합 (Phase 3)

- [ ] `[auto-hard]` TestSceneSanyo `SessionPanelController` MonoBehaviour 직렬화에 `_multiplayerPanelObject: {fileID: 800001000}` 추가.
  **검증:** `Grep "_multiplayerPanelObject: \{fileID: 800001000\}" Assets/Scenes/TestSceneSanyo.unity` ≥ 1 매치.
- [ ] `[auto-hard]` TestSceneSanyo `MultiplayerLobbyPanel` MonoBehaviour 직렬화에서 `maxPlayersInput:` 라인 제거 + `maxPlayersDisplay:` / `maxPlayersMinusButton:` / `maxPlayersPlusButton:` / `passwordOverlayRoot:` / `passwordOverlayInput:` / `passwordOverlayConfirmButton:` / `passwordOverlayCancelButton:` 7 라인 추가.
  **검증:** `Grep "maxPlayersInput:" Assets/Scenes/TestSceneSanyo.unity` 0 매치 AND `Grep "maxPlayersDisplay:|passwordOverlayRoot:" Assets/Scenes/TestSceneSanyo.unity` ≥ 2 매치 (값이 non-zero fileID).
- [ ] `[auto-hard]` 보존된 SerializeField 11건 (`authGate: {fileID: 800001005}`, `lobbyRoot: {fileID: 310001000}`, `roomRowPrefab guid: be23a82a60cc438489c6af36d9c50bfb`, `authConfig guid: e446380221ca43f2b77b3d17c7f05105`, 등) 의 fileID/GUID 가 본 plan 변경 전후 동일.
  **검증:** `Grep "guid: be23a82a60cc438489c6af36d9c50bfb|guid: e446380221ca43f2b77b3d17c7f05105|lobbyRoot: \{fileID: 310001000\}" Assets/Scenes/TestSceneSanyo.unity` ≥ 3 매치.
- [ ] `[auto-hard]` `PasswordJoinOverlay` GameObject 가 RoomList 자식으로 신설 + `m_IsActive: 0` 초기 상태 + `LobbyRoot` (`310001000`) 자체는 여전히 `m_IsActive: 0`.
  **검증:** TestSceneSanyo.unity 에서 `PasswordJoinOverlay` 인근 5줄 안 `m_IsActive: 0` 매치 1건 + LobbyRoot 의 `m_IsActive: 0` 보존 확인.
- [ ] `[auto-hard]` `MultiplayerAuthGate` root (fileID `800001000`) 의 `m_IsActive` 가 본 plan 직렬화 단계에서 변경되지 않음 (런타임 controller 가 토글).
  **검증:** `Grep -A 2 "m_Name: MultiplayerAuthGate" Assets/Scenes/TestSceneSanyo.unity` 결과의 `m_IsActive: 1` 보존 확인.

### 런타임 동작 (Phase 4)

- [ ] `[manual-hard]` Editor Play 모드 또는 Quest 빌드 — SessionPanel 호출 (pinch 또는 panelToggleAction) → 멀티플레이 항목/탭 클릭 → MultiplayerAuthGate Canvas 가 SetActive(true) 로 표시 + 기존 `ActivateButton` 인증 흐름 정상 진입. 한 번 더 클릭 시 비활성화 토글.
  **검증:** Quest 또는 Editor Play 에서 시뮬레이션 시나리오: SessionPanel 열기 → Multiplayer 클릭 → AuthGate 표시 → 다시 클릭 → AuthGate 숨김.
- [ ] `[manual-hard]` LobbyPanel 활성화 시 MaxPlayersStepper 의 - / + 버튼이 가시적으로 표시되고 클릭마다 디스플레이 숫자가 ±1 변경. 1·32 경계에서 해당 버튼 비활성(gray).
  **검증:** Quest 또는 Editor 시각 확인 — 1 도달 시 MinusButton 회색, 32 도달 시 PlusButton 회색, 그 외 정상 색.
- [ ] `[manual-hard]` 잠금 방 행 클릭 시 PasswordJoinOverlay 가 RoomList 위에 배경 dimmer + 입력 패널로 표시. Confirm → 빈 비밀번호면 `Failed: Password is required` statusLabel + 오버레이 유지 / 유효 비밀번호면 `RoomClient.JoinRoomAsync` 호출 + 오버레이 닫힘. Cancel → 오버레이 닫힘 + 입력 초기화. 비잠금 방은 기존대로 즉시 입장.
  **검증:** Quest 또는 Editor mock RoomListQuery 가 잠금/비잠금 row 양쪽 emit 한 상태에서 행 클릭 시나리오 4건.
- [ ] `[manual-hard]` SessionPanel 의 VolumePanel / RhythmGamePanel 탭은 본 plan 적용 후에도 기존 기능 그대로 동작 (volume slider 변경 + rhythm 곡 선택·preview·play).
  **검증:** Editor Play 또는 Quest 에서 두 탭 회귀 확인.

### 정합성

- [ ] `[auto-hard]` `git diff --stat` 결과 변경 파일이 다음 5건 외 0건 (Unity Editor 가 자동 save 한 ProjectSettings 일부는 예외): `Assets/SessionPanel/Scripts/MultiplayerSectionController.cs`(신규) / `Assets/SessionPanel/Scripts/SessionPanelController.cs` / `Assets/SessionPanel/Prefabs/SessionPanel.prefab` / `Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs` / `Assets/Scenes/TestSceneSanyo.unity`.
  **검증:** `git diff --stat` 출력 inspect.
- [ ] `[auto-hard]` Unity Editor `read_console` types=error 0건 (도메인 재컴파일 후).
  **검증:** `mcp__UnityMCP__read_console` action=get types=["error"] 결과 카운트 0.

## Out of Scope

- SessionPanel.prefab 의 VolumeSectionController / RhythmGameSectionController 자식 트리 변경 — 본 plan 은 멀티플레이 섹션 *추가*만 다룬다.
- MultiplayerAuthGate Canvas 의 World Space LocalPosition / Rotation / Scale 변경 — 이전 plan (`presence-ui-lobby-migration-and-ux`) 의 박제값 그대로 유지.
- 가상 키보드 추가 부착 (PasswordOverlayInput 외) — `RoomNameInput` / `PasswordInput` 은 이전 plan 에서 이미 VRKeyboardField 부착됨.
- 비밀번호 hash 송신 (잠금 방 입장) — 본 plan 은 raw 비밀번호를 `RoomClient.JoinRoomAsync` 에 그대로 전달 (기존 동작 보존). backend hash 정합은 이전 Handoff 의 minor issue 2 와 묶이는 별도 plan.
- nickname 채널 / 호스트 마커 / 채팅 등 04-presence-ui Out of Scope 그대로.
- SessionPanel 닫힘 시 MultiplayerAuthGate 도 같이 닫힘 처리 — 사용자 명시 요구 없음. 본 plan 의 controller 는 SessionPanel 가시성과 직교 (사용자가 멀티플레이 진입 후 SessionPanel 닫아도 LobbyPanel/InRoomPanel 동작 유지).

## Notes

- `MultiplayerSectionController` 가 `MultiplayerLobbyPanel` API 를 직접 호출하지 않는 이유 = asmdef 분리 유지 + 결합도 최소화. 단순 `SetActive` 토글만으로 충분 (MultiplayerAuthGate Canvas 가 인증 흐름의 진입점이고, AuthGate 통과 후 LobbyPanel 자동 활성화는 `MultiplayerLobbyPanel.HandleAuthenticationCompleted` 가 이미 책임).
- 본 plan 적용 후 SessionPanel 의 panel 가시성과 multiplayerPanelRoot 가시성이 완전 직교라는 점은 사용자 멘탈 모델 (one-shot toggle) 과 다를 수 있다. 후속 plan 후보: SessionPanel 이 Hidden 으로 전환될 때 multiplayerPanelRoot 도 함께 SetActive(false) 옵션 (현재 plan 범위 아님).
- VRWorldKeyboard 인스턴스 공유로 인한 PasswordOverlayInput ↔ CreateForm.PasswordInput 의 keyboard target 전환은 이전 Handoff 박제 동작과 동일 — 잠금 오버레이 활성 동안만 PasswordOverlayInput 으로 갱신.
- 후속 plan 후보: 한글 폰트 atlas / passwordHash env / MaxPlayers off-by-one (이전 Handoff 의 minor issues 그대로).
