# 멀티플레이 로비 패널 UI 구조·상태 골격

**Linked Spec:** [`../specs/07-lobby-panel-ui.md`](../specs/07-lobby-panel-ui.md)
**Status:** `Ready`

## Goal

세션 패널의 `[멀티플레이]` 버튼(sub-spec 08 책임)이 spawn 또는 show 할 독립
`LobbyPanel.prefab` + `LobbyPanelController` 1세트를 신설한다. 좌측 방 생성 폼,
우측 방 목록, 비밀번호 입력 오버레이까지 UI 구조·상태머신을 모두 짠 채로
완성하되 **backend API 연동은 본 plan 범위 밖** — 모든 백엔드 호출 지점은
`IRoomActionSink` 인터페이스로 캡슐화해 nop 구현체를 와이어하고 plan을 종료한다.

## Context

### spec 07 의 위치

`07-lobby-panel-ui.md` 는 04-presence-ui 의 lobby 패널 UX를 대체하는 신규 설계.
spec What 은 (1) 좌/우 2-컬럼 + 좌측 5-위젯 (room name / password input / password
toggle / max players stepper / create button), (2) 우측 스크롤 가능한 방 목록,
(3) 비밀번호 있는 방 클릭 시 오버레이 팝업, (4) 백엔드 연동 X / UI 구조·상태만,
을 요구한다. 메인 추론은 본 plan 이 그 UI 구조 1차 골격 단일 plan임을 박제했다.

### 기존 자산과의 관계

이미 `Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs` (484 라인,
백엔드 연동 포함) 가 archive 된 [`presence-ui-lobby-migration-and-ux`](../../_archive/multiplayer-network/plans/2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md)
에서 TestSceneSanyo 에 박제·운영 중이다. spec 07 은 그것을 **대체할 신규 UI 설계**
이지만 **본 plan 은 기존 컴포넌트·씬 인스턴스를 일절 건드리지 않는다.**

본 plan 의 산출물:

- 신규 prefab `Assets/Multiplayer/Prefabs/LobbyPanel.prefab` (씬에 박지 않음)
- 신규 컨트롤러 `Assets/Multiplayer/Scripts/Presence/LobbyPanelController.cs`
- 신규 인터페이스 `Assets/Multiplayer/Scripts/Presence/IRoomActionSink.cs` (백엔드 호출 추상 표면)
- 신규 nop 구현 `Assets/Multiplayer/Scripts/Presence/StubRoomActionSink.cs` (Inspector 와이어 가능한 MonoBehaviour, UI 동작 검증용)

기존 `MultiplayerLobbyPanel` / `MultiplayerInRoomPanel` / 그 와이어 / TestSceneSanyo
씬 root 22개는 모두 손대지 않는다. session-panel sub-spec 08 plan 이 별도 사이클에서
- spec 07 의 신규 LobbyPanel.prefab 을 어디서 spawn/show 할지
- 기존 `MultiplayerLobbyPanel` 컴포넌트를 어떻게 정리할지

를 결정한다.

### 결정 박제 인용

- [`01-default-scene.md`](../decisions/01-default-scene.md): default 씬 = `Assets/Scenes/TestSceneSanyo.unity` (2026-05-17~). 본 plan 의 prefab 은 씬에 박지 않으므로 직접 영향 없음. 다만 ## Acceptance 의 prefab 로드 검증을 Editor 에서 수행할 때 default 씬을 손대지 않음을 보장.
- [`02-clientcompat-enforcement.md`](../decisions/02-clientcompat-enforcement.md): admission 단계 strict-equal `clientCompatibilityVersion` 검증. 본 plan 은 백엔드 연동 X 이므로 이 게이트와 무관. spec 08 또는 실연동 plan 에서 호출 시 `Application.version` 전달 책임을 가진다 — `IRoomActionSink` 시그니처가 이 후속 책임을 막지 않도록 `string runtimeVersion` 인자를 남겨둔다 (constraint).
- [`05-persistent-demo-room-policy.md`](../decisions/05-persistent-demo-room-policy.md): persistent 룸은 일반 룸 목록에 그대로 노출 — UX 분기 없음. 본 plan 의 RoomList 행은 일반/persistent 구분 표기 X (정책 일관성).

## Verified Structural Assumptions

- **현행 Multiplayer asmdef references**: `Fusion.Unity`, `Unity.TextMeshPro`, `Instruments` 3건만 등록. 본 plan 신규 코드는 `UnityEngine.UI` (Button/Toggle/ScrollRect) + `TMPro` (이미 등록됨) 만 사용. `UnityEngine.UI` 는 `autoReferenced: true` + UGUI assembly 기본 link 이므로 추가 reference 불필요. — `Read Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef (2026-05-27)`
- **신규 namespace**: 모두 `Murang.Multiplayer.Presence` 로 통일. 기존 `MultiplayerLobbyPanel` 과 동일 폴더·동일 namespace 라 cross-cutting 의존 추가 없음. 신규 컴포넌트 이름은 `LobbyPanelController` (기존 `MultiplayerLobbyPanel` 과 클래스 명 충돌 회피). — `Read Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs (2026-05-27)`
- **기존 RoomRow 프리팹 자산**: `Assets/Multiplayer/Resources/RoomRow.prefab` 존재 (Resources/ 폴더, GUID 박제: 기존 코드 `be23a82a60cc438489c6af36d9c50bfb` archive plan 박제). 본 plan 은 이 prefab 을 **재사용하지 않는다** — `RoomRowEntry` 는 backend `RoomListEntry` DTO (`Murang.Multiplayer.Room.Client`) 에 결합되어 있다. spec 07 의 백엔드 분리 원칙을 위해 신규 행 컴포넌트 `LobbyRoomRow` + `LobbyRoomRowData` POCO 를 분리 신설한다. — `Read Assets/Multiplayer/Scripts/Presence/RoomRowEntry.cs (2026-05-27)`
- **TestSceneSanyo 손대지 않음**: 본 plan 산출물 prefab 은 씬에 인스턴스화하지 않는다. session-panel sub-spec 08 plan 이 spawn/show 책임을 가짐. — 작성자 보장 (씬 편집 명령을 plan Approach 에 포함하지 않음).
- **외부 API side-effect 박제**: 본 plan 의 `LobbyPanelController` 는 외부 컴포넌트의 public API 를 호출하지 않는다. `IRoomActionSink` 는 본 plan 이 새로 정의하는 인터페이스이며 `StubRoomActionSink` 는 nop 구현이라 frame-loop / OnEnable 부작용 없음. 기존 `MultiplayerLobbyPanel` 호출도 없다. — `Read Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs (2026-05-27)`
- **UI 컴포넌트 enum/Flags 사용처**: 본 plan 은 `LayoutGroup.childAlignment` (`TextAnchor` enum, Unity 기본) + `ContentSizeFitter.FitMode` 외에 직렬화에서 의미가 뒤집힐 만한 enum 을 쓰지 않는다. Inspector 와이어는 prefab YAML 직접 작성 대신 Editor 에서 prefab 생성 (`manage_asset` 또는 수동) 으로 확정하며, controller 의 SerializeField 만 plan 본문에 박제. enum 박제 항목 해당 없음. — 작성자 검토.

## Approach

### 단계 1 — 인터페이스 + nop 구현 (백엔드 분리 표면)

`Assets/Multiplayer/Scripts/Presence/IRoomActionSink.cs` 를 신설한다. UI 가 호출할
3개의 추상 action 만 노출:

```csharp
namespace Murang.Multiplayer.Presence
{
    public readonly struct LobbyCreateRequest
    {
        public string RoomName { get; }
        public int MaxPlayers { get; }
        public bool PasswordEnabled { get; }
        public string PasswordRaw { get; } // PasswordEnabled=false 시 null/empty
        public string RuntimeVersion { get; } // decision 02 admission gate constraint
        public LobbyCreateRequest(string roomName, int maxPlayers, bool passwordEnabled, string passwordRaw, string runtimeVersion);
    }

    public readonly struct LobbyJoinRequest
    {
        public string RoomId { get; }
        public string RoomName { get; }
        public string PasswordRaw { get; } // 비밀번호 없는 방 = null/empty
        public string RuntimeVersion { get; }
        public LobbyJoinRequest(string roomId, string roomName, string passwordRaw, string runtimeVersion);
    }

    public interface IRoomActionSink
    {
        void RequestCreate(LobbyCreateRequest request);
        void RequestJoin(LobbyJoinRequest request);
        event System.Action<System.Collections.Generic.IReadOnlyList<LobbyRoomRowData>> RoomListUpdated;
    }
}
```

`Assets/Multiplayer/Scripts/Presence/LobbyRoomRowData.cs` 신설 — backend DTO 와
분리된 UI 전용 POCO. 필드: `string RoomId, string RoomName, int CurrentPlayers,
int MaxPlayers, bool HasPassword`.

`Assets/Multiplayer/Scripts/Presence/StubRoomActionSink.cs` 신설 — MonoBehaviour,
`IRoomActionSink` 구현, 호출 시 Inspector 토글에 따라 (a) 무시 또는 (b)
미리 등록된 더미 `LobbyRoomRowData[]` 를 `RoomListUpdated` 로 즉시 발화. UI
구조 검증과 수동 테스트용. Inspector 필드:
- `LobbyRoomRowData[] seedRows` (Serializable POCO)
- `bool emitOnEnable`
- 디버그 read-only label (마지막 RequestCreate / RequestJoin payload)

### 단계 2 — Controller

`Assets/Multiplayer/Scripts/Presence/LobbyPanelController.cs` 신설.

**책임**:
- SerializeField 와이어 19건 (단계 3 에 정의 prefab 의 자식 트리와 1:1).
- Awake: 모든 Button.onClick / Toggle.onValueChanged listener 등록 + 비밀번호 토글 초기 상태 반영 + 스테퍼 초기값 (`maxPlayersDefault: int = 4`, Inspector tunable, 범위 1~32) 표시 + RoomList placeholder (EmptyLabel) 표시.
- OnEnable: `_sink.RoomListUpdated += HandleRoomListUpdated` 등록. OnDisable: 해제.
- `IRoomActionSink _sink` 를 Inspector `_sinkObject: UnityEngine.Object` → `as IRoomActionSink` explicit wiring (Multiplayer 도메인의 다른 컨트롤러와 동일 패턴, [`Assets/SessionPanel/CLAUDE.md`](../../../Assets/SessionPanel/CLAUDE.md) §3 참조).
- 비밀번호 토글 OnValueChanged: `passwordInput.interactable = on`, OFF 전환 시 `passwordInput.text = string.Empty`. CanvasGroup alpha 로 시각적 회색화 (toggle off 시 `passwordInputCanvasGroup.alpha = 0.4f`, on 시 1.0).
- 스테퍼: `+` / `-` 버튼 클릭 시 `_currentMaxPlayers` ±1, clamp [1, 32], `maxPlayersLabel.text = _currentMaxPlayers.ToString()`. 경계에서 버튼 `interactable = false`.
- CreateButton 클릭: `LobbyInputValidator.ValidateCreate` 호출(기존 코드 재사용) → 실패 시 `statusLabel.text = "Failed: " + msg` / 성공 시 `_sink.RequestCreate(new LobbyCreateRequest(...))`. **백엔드 호출 X — RequestCreate 호출만으로 UI 책임 종료.**
- RoomRow 클릭: `LobbyRoomRowData.HasPassword == false` → 즉시 `_sink.RequestJoin(...)`. `HasPassword == true` → 비밀번호 오버레이 활성화 (`passwordOverlayRoot.SetActive(true)` + `overlayPasswordInput.text = string.Empty` + `_pendingJoinRow = row`).
- 오버레이 확인 버튼: `_sink.RequestJoin(new LobbyJoinRequest(_pendingJoinRow.RoomId, _pendingJoinRow.RoomName, overlayPasswordInput.text, Application.version))` + 오버레이 닫기.
- 오버레이 취소 버튼: 오버레이 닫기 (`passwordOverlayRoot.SetActive(false)` + `_pendingJoinRow = default`).
- `HandleRoomListUpdated(IReadOnlyList<LobbyRoomRowData>)`: row pool 패턴 (기존 `MultiplayerLobbyPanel.EnsureRowCapacity` 와 동일 형태) — `LobbyRoomRow` prefab 을 풀에 instantiate, `Bind(LobbyRoomRowData)` 호출, 부족분만 활성화. EmptyLabel 가시성 갱신.

**비밀번호 오버레이 root** 는 prefab 트리 안에서 `LobbyRoot/PasswordOverlay`
(기본 SetActive false). RoomList 위에 겹치도록 sibling order 뒤쪽. 본 plan 의
오버레이는 controller 가 `SetActive(true/false)` 로 토글 (별도 fade/anim 없음 — Out of Scope).

### 단계 3 — LobbyPanel.prefab 신설

`Assets/Multiplayer/Prefabs/LobbyPanel.prefab` 을 World Space Canvas 로 신설.

**계층 (controller SerializeField 19건과 1:1 매핑)**:

```
LobbyPanel (root)
  Canvas (WorldSpace) + CanvasScaler + GraphicRaycaster + TrackedDeviceGraphicRaycaster
  + LobbyPanelController (script)
  └ LobbyRoot                                  [SetActive: false at Awake → ShowLobby 호출 시 true]
    ├ CreateForm                               (AnchorMin 0,0 / AnchorMax 0.5,1)
    │  VerticalLayoutGroup (spacing 20, padding 30)
    │  ├ RoomNameInput   (TMP_InputField)
    │  ├ PasswordEnabledToggle (Toggle, label "비밀번호 사용")
    │  ├ PasswordInput   (TMP_InputField + CanvasGroup)
    │  ├ MaxPlayersStepper
    │  │  HorizontalLayoutGroup
    │  │  ├ MinusButton  (Button "-")
    │  │  ├ MaxPlayersLabel (TMP_Text)
    │  │  └ PlusButton   (Button "+")
    │  └ CreateButton    (Button "방 만들기")
    ├ RoomList                                 (AnchorMin 0.5,0 / AnchorMax 1,1)
    │  ├ ScrollView (ScrollRect vertical)
    │  │  └ Viewport
    │  │     └ Content (VerticalLayoutGroup + ContentSizeFitter VerticalFit=PreferredSize)
    │  └ EmptyLabel    (TMP_Text "방이 없습니다")
    ├ PasswordOverlay                          [SetActive: false]
    │  Image (semi-transparent backdrop)
    │  └ OverlayPanel
    │     VerticalLayoutGroup
    │     ├ OverlayTitleLabel (TMP_Text "비밀번호 입력")
    │     ├ OverlayPasswordInput (TMP_InputField)
    │     ├ OverlayConfirmButton (Button "확인")
    │     └ OverlayCancelButton (Button "취소")
    └ StatusLabel                              (TMP_Text, bottom-anchored)
```

추가 신규 prefab: `Assets/Multiplayer/Prefabs/LobbyRoomRow.prefab` —
`LobbyRoomRow` MonoBehaviour 부착, HorizontalLayoutGroup 자식 `NameLabel`
(TMP_Text) + `PlayersLabel` (TMP_Text `{cur}/{max}` 포맷) + `LockIcon`
(GameObject 켜기/끄기) + `JoinButton` (Button, 행 전체 클릭 영역 또는 명시
버튼). `Bind(LobbyRoomRowData)` 가 채움.

prefab 생성은 Unity MCP `manage_asset` create + `manage_gameobject` add_child
시퀀스 또는 Editor 수동 작성. 본 plan 은 prefab YAML 의 정확한 fileID 박제를
요구하지 않음 — controller 의 SerializeField 와이어가 모두 non-zero 인지를
acceptance 로 검증한다.

### 단계 4 — 회귀 + 컴파일 검증

- `unity-test-runner` 1회 호출 (CLAUDE.md "Unity 런타임 코드(`.cs`) 수정 후" 정책). EditMode 회귀 — 기존 `Murang.Multiplayer.Room.Tests` 가 깨지지 않는지만 확인 (본 plan 은 신규 namespace 추가이므로 회귀 0건이 기대값).
- `read_console` types=error 0건 확인.

## Deliverables

- `Assets/Multiplayer/Scripts/Presence/IRoomActionSink.cs` — UI ↔ 백엔드 분리 인터페이스 + `LobbyCreateRequest` / `LobbyJoinRequest` struct + `RoomListUpdated` 이벤트
- `Assets/Multiplayer/Scripts/Presence/LobbyRoomRowData.cs` — UI 전용 행 POCO (backend DTO 와 분리)
- `Assets/Multiplayer/Scripts/Presence/StubRoomActionSink.cs` — `IRoomActionSink` nop 구현 MonoBehaviour (Inspector seedRows 발화)
- `Assets/Multiplayer/Scripts/Presence/LobbyPanelController.cs` — spec 07 의 상태/이벤트/검증 책임을 모두 가진 신규 컨트롤러 (기존 `MultiplayerLobbyPanel` 과 클래스 명 분리)
- `Assets/Multiplayer/Scripts/Presence/LobbyRoomRow.cs` — `LobbyRoomRowData` 바인딩 뷰, JoinRequested 이벤트
- `Assets/Multiplayer/Prefabs/LobbyPanel.prefab` — 좌/우 2-컬럼 + 비밀번호 오버레이 World Space Canvas
- `Assets/Multiplayer/Prefabs/LobbyRoomRow.prefab` — 우측 방 목록 행 prefab

## Acceptance Criteria

### 코드 / 컴파일

- [ ] `[auto-hard]` 신규 5개 `.cs` 파일이 `Assets/Multiplayer/Scripts/Presence/` 에 존재하고 `Murang.Multiplayer.Presence` namespace 사용.
  **검증:** `Grep "namespace Murang.Multiplayer.Presence" Assets/Multiplayer/Scripts/Presence/{IRoomActionSink,LobbyRoomRowData,StubRoomActionSink,LobbyPanelController,LobbyRoomRow}.cs` 각각 ≥ 1 매치.
- [ ] `[auto-hard]` `LobbyPanelController` 가 `MultiplayerLobbyPanel` 와 별개 클래스이고 기존 컴포넌트 source 가 손대지지 않음.
  **검증:** `git diff --stat HEAD Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs` 가 0줄 변경.
- [ ] `[auto-hard]` Unity Editor compile 성공 — `read_console` types=error 0건. (회귀 가드)
  **검증:** Unity MCP `read_console(types=["Error"])` 호출, 컴파일 후 0건.
- [ ] `[auto-hard]` `IRoomActionSink` 가 `RequestCreate` / `RequestJoin` / `RoomListUpdated` 3개 멤버 노출 + `LobbyCreateRequest` 가 `RuntimeVersion` 필드 보유 (decision 02 후속 admission gate constraint).
  **검증:** `Grep "RuntimeVersion" Assets/Multiplayer/Scripts/Presence/IRoomActionSink.cs` ≥ 2 매치 (struct 정의 + 둘 다).

### Prefab 직렬화

- [ ] `[auto-hard]` `Assets/Multiplayer/Prefabs/LobbyPanel.prefab` 자산 존재 + `.meta` 동반.
  **검증:** `Glob Assets/Multiplayer/Prefabs/LobbyPanel.prefab` 1건 매치 + `LobbyPanel.prefab.meta` 동반.
- [ ] `[auto-hard]` `LobbyPanel.prefab` 안에서 `LobbyPanelController` MonoBehaviour 가 부착되어 있고 SerializeField 와이어가 모두 non-zero (`{fileID: 0}` 매치 0건).
  **검증:** `Grep "{fileID: 0}" Assets/Multiplayer/Prefabs/LobbyPanel.prefab` 의 매치 수가 정상값(빈 슬롯 0, 단 RectTransform 부모 등 시스템 fileID 0 인 자리는 제외)인지 prefab YAML 의 SerializeField 블록 (`m_authGate` 등 본 plan 와이어 19건) 만 발췌 검사. 자동: `Grep "_(sinkObject|roomNameInput|passwordEnabledToggle|passwordInput|maxPlayersLabel|plusButton|minusButton|createButton|roomRowPrefab|roomRowParent|emptyLabel|passwordOverlayRoot|overlayPasswordInput|overlayConfirmButton|overlayCancelButton|statusLabel|lobbyRoot|passwordInputCanvasGroup|defaultMaxPlayers): " Assets/Multiplayer/Prefabs/LobbyPanel.prefab` 매치한 라인 모두 `fileID: 0` 으로 끝나지 않음.
- [ ] `[auto-hard]` `Assets/Multiplayer/Prefabs/LobbyRoomRow.prefab` 자산 존재 + `LobbyRoomRow` MonoBehaviour 부착.
  **검증:** `Glob Assets/Multiplayer/Prefabs/LobbyRoomRow.prefab` 1건 + `Grep "LobbyRoomRow" Assets/Multiplayer/Prefabs/LobbyRoomRow.prefab` ≥ 1 매치.

### 씬 비변경

- [ ] `[auto-hard]` 본 plan atomic commit 의 변경 파일에 `Assets/Scenes/TestSceneSanyo.unity` 가 **포함되지 않음** (씬 손대지 않음 보장).
  **검증:** `git diff --stat HEAD Assets/Scenes/TestSceneSanyo.unity` 0줄 변경.
- [ ] `[auto-hard]` 기존 `MultiplayerLobbyPanel` / `MultiplayerInRoomPanel` / `RoomRowEntry` source 가 본 plan commit 에서 0줄 변경.
  **검증:** `git diff --stat HEAD Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs Assets/Multiplayer/Scripts/Presence/MultiplayerInRoomPanel.cs Assets/Multiplayer/Scripts/Presence/RoomRowEntry.cs` 모두 0줄.

### UI 상태 머신 (런타임 검증)

- [ ] `[manual-hard]` Editor 에서 빈 씬 또는 dev 검증 씬에 `LobbyPanel.prefab` instantiate + `StubRoomActionSink` 와이어 + Play. 좌측 CreateForm 5위젯, 우측 RoomList(EmptyLabel "방이 없습니다"), 하단 StatusLabel 이 표시된다. 비밀번호 토글 OFF 상태에서 PasswordInput 이 회색(`CanvasGroup.alpha ≈ 0.4`) + 비상호작용.
  **검증:** Editor Play 시각 검증 — 좌측 폼 5위젯 + 우측 placeholder + 회색 PasswordInput 모두 확인.
- [ ] `[manual-hard]` PasswordEnabledToggle ON 클릭 시 PasswordInput 활성화 (alpha 1.0, interactable true). OFF 로 되돌리면 PasswordInput.text 가 비워지고 다시 회색·비활성.
  **검증:** Editor Play 토글 클릭 시각 검증.
- [ ] `[manual-hard]` MaxPlayersStepper `+` 클릭 시 라벨 숫자 1 증가, `-` 클릭 시 1 감소. 1 에서 `-` 추가 클릭 시 변화 없음 + `MinusButton.interactable=false`. 32 에서 `+` 추가 클릭 시 변화 없음 + `PlusButton.interactable=false`.
  **검증:** Editor Play 스테퍼 +/- 반복 클릭 시각 검증, 경계값 1·32 에서 버튼 비활성 확인.
- [ ] `[manual-hard]` `StubRoomActionSink.seedRows` 에 비밀번호 있는 행 1건 + 없는 행 1건 등록 후 `emitOnEnable=true` 로 Play. 우측 RoomList 에 2행 표시 + EmptyLabel 비활성화. 비밀번호 없는 행 클릭 → `RequestJoin` 디버그 라벨 즉시 갱신. 비밀번호 있는 행 클릭 → PasswordOverlay 활성화 + OverlayPasswordInput 빈 상태.
  **검증:** Editor Play seedRows 시나리오 시각 검증, StubRoomActionSink Inspector 디버그 라벨 변화 확인.
- [ ] `[manual-hard]` PasswordOverlay 의 취소 버튼 클릭 시 오버레이 닫힘 + RequestJoin 미발화. 확인 버튼 클릭 시 오버레이 닫힘 + `RequestJoin` payload 의 `PasswordRaw` 가 OverlayPasswordInput.text 그대로 전달.
  **검증:** Editor Play 오버레이 확인/취소 시각 검증, StubRoomActionSink 디버그 라벨로 payload 정합 확인.
- [ ] `[manual-hard]` CreateButton 클릭 시 (RoomName 입력된 상태) `RequestCreate` 발화 — StubRoomActionSink 디버그 라벨에 `RoomName`, `MaxPlayers`, `PasswordEnabled`, `RuntimeVersion=Application.version` payload 반영.
  **검증:** Editor Play CreateButton 클릭 시 디버그 라벨에 4개 필드 모두 표시.

### 회귀

- [ ] `[auto-hard]` `unity-test-runner` 서브에이전트 1회 호출 — EditMode 회귀 0 실패. (CLAUDE.md 정책)
  **검증:** Task tool subagent_type=unity-test-runner 호출 결과 Pass.

## Out of Scope

- 백엔드 API 연동 (실 `IRoomActionSink` 구현 = `RoomClient` / `RoomListQuery` 결선). 후속 plan 책임 — sub-spec 08 의 [멀티플레이] 버튼이 patch 후 본 prefab 의 sink 슬롯을 실 어댑터로 갈아끼움.
- session-panel `[멀티플레이]` 버튼 추가 + LobbyPanel.prefab spawn/show 진입점 — `session-panel` sub-spec 08 의 별도 plan.
- VR 가상 키보드 연동 (방 이름·비밀번호 입력칸의 TMP_InputField 와 `VRKeyboardField` 부착) — spec 07 본문이 "추후" 로 박제. 본 plan 은 InputField 만 둠.
- TestSceneSanyo 안 기존 `MultiplayerLobbyPanel` root 5번째 GameObject 정리·교체 — 후속 plan.
- 인-룸 패널 (참가자 목록 + Leave) — 별도 sub-spec 책임 (spec 07 Out of Scope 박제).
- persistent 룸 별도 시각 표기 — decision 05 "일반 룸 목록에 그대로 노출, UX 분기 없음".
- 호스트/권한 마커, 친구·초대, 보이스/텍스트 채팅 — 04-presence-ui Out of Scope.

## Notes

- 클래스 이름 분리(`MultiplayerLobbyPanel` vs `LobbyPanelController`) 는 동일 namespace 에서 두 컴포넌트가 공존 가능하게 하기 위함. 후속 plan 에서 `MultiplayerLobbyPanel` 을 정리/삭제하는 시점이 오면 `LobbyPanelController` 가 그 자리를 계승.
- `IRoomActionSink` 의 `RuntimeVersion` 필드는 [decision 02](../decisions/02-clientcompat-enforcement.md) 의 admission strict-equal 게이트 constraint 박제 — 실 sink 구현 plan 이 `Application.version` 을 그대로 채워 backend join ticket / RPC 에 전달해야 한다. 본 plan controller 가 호출 시 이미 `Application.version` 으로 채워 보내므로 stub 도 그 값을 가시화한다.
- prefab 의 World Space Canvas 좌표 (LocalPosition / Rotation / Scale) 는 본 plan 에서 임의값으로 두고 (예: `0, 0, 0` / `identity` / `0.002, 0.002, 0.002`) sub-spec 08 plan 의 spawn 시점에 실 VR 시야 좌표로 조정한다. 본 plan acceptance 는 좌표값을 검사하지 않는다.
- VR 가상 키보드는 본 plan 에서 부착하지 않는다. spec 07 본문이 "가상 키보드 연결은 추후" 박제. 후속 plan 에서 `VRKeyboardField` 를 3개 TMP_InputField + 오버레이 InputField 1개에 추가하면 됨.

## Handoff

### 2026-05-28 진행 상태 (manual-hard pending)

**Status: In Progress** — atomic commit으로 산출물 박제, manual-hard 6건은 사용자 별도 브랜치 작업 후 재개 예정.

**자동 검증 결과**:
- `auto-hard` 8건 모두 PASS
  - 5개 신규 `.cs` 파일이 `Murang.Multiplayer.Presence` namespace 사용 확인
  - `IRoomActionSink` `RuntimeVersion` 6 매치 (≥ 2 요구 충족, decision 02 admission gate constraint)
  - `MultiplayerLobbyPanel.cs` / `MultiplayerInRoomPanel.cs` / `RoomRowEntry.cs` / `Assets/Scenes/TestSceneSanyo.unity` 모두 0줄 변경 확인
  - Unity Editor compile error 0건 (`Murang.Multiplayer.dll` + `Murang.Multiplayer.Room.Tests.dll` 컴파일 성공)
  - `LobbyPanel.prefab` + `.meta` 존재, `LobbyPanelController` 부착, 19개 SerializeField 와이어 모두 fileID non-zero (자동 생성된 prefab YAML 1784–1802 라인)
  - `LobbyRoomRow.prefab` + `LobbyRoomRow` 컴포넌트 부착 (guid `a287dcb36fc2a8d489d3b41c242ffd8f`)
- `unity-test-runner` 회귀: **EditMode 57/57 PASS** (`Murang.Multiplayer.Room.Tests` 기존 케이스 무영향, 신규 namespace 추가만)
- console errors 0건

**manual-hard 6건 (사용자 Editor 검증 대기)**:
1. 초기 표시 (좌측 폼 5위젯 + 우측 EmptyLabel + 회색 PasswordInput)
2. PasswordEnabledToggle ON/OFF
3. MaxPlayersStepper 1~32 경계값
4. StubRoomActionSink seedRows 2건 + RoomRow 클릭 분기
5. PasswordOverlay 확인/취소
6. CreateButton payload 4필드 박제

**현재 prefab의 visual fidelity 주의**:
- `LobbyPanel.prefab`은 Unity MCP `manage_gameobject` + `manage_components`로 자동 생성된 최소 구조. TMP_InputField/Button/Toggle의 placeholder/label/background sprite 등 시각 자식은 부재. SerializeField 와이어는 모두 정상이지만 manual-hard 검증 시 텍스트가 안 보이거나 클릭 영역이 작을 수 있음.
- World Space Canvas 좌표는 `position=(0,0,0)`, `rotation=identity`, `scale=(0.001,0.001,1)` 기본값. session-panel 통합 plan에서 실 VR 시야 좌표로 조정 예정.

**후속**:
- manual-hard 6건 검증 후 plan Status → Done, spec 07 표 Status → Done, 정리 commit 추가.
- 사용자가 요청한 범위 조정 (session-panel sub-spec 08): TabBar에 "멀티플레이" 버튼 1개만 추가 + 싱글플레이/룸 내부 상태별로 Lobby/InRoom 패널 분기. 별도 plan으로 분리 예정.
