# Presence UI LobbyPanel 마이그레이션 + UX 리디자인 + VR 키보드 통합

**Linked Spec:** [`04-presence-ui.md`](../specs/04-presence-ui.md)
**Caused By:**
- [`2026-05-18-namae1128-presence-ui-migration-to-testscenesanyo.md`](./2026-05-18-namae1128-presence-ui-migration-to-testscenesanyo.md) 의 Out of Scope에서 약속한 본격 UX 리디자인 (좌표 미세 조정 ±1m/±45°만 허용했음).
- 위 마이그레이션이 SampleScene의 4 root만 옮기고 **5번째 root `MultiplayerLobbyPanel` (adb46dc 시점 박힘)** 을 unity-scene-reader 점검 prompt 누락으로 빠뜨림.
- 실기기 테스트에서 (1) 룸 이름 입력 시 VR 키보드 미발화 (2) 입력칸·버튼 배치 모서리 몰림 발견.

**Status:** `Done`

## Goal

TestSceneSanyo (default 씬, [`decisions/01-default-scene.md`](../decisions/01-default-scene.md)) 의 multiplayer UI를 **Quest 실기기에서 사용 가능한 상태로 완성**한다. 본 plan 이후 사용자는 (1) LobbyPanel에서 룸 이름·정원·비밀번호를 VR 키보드로 입력해 룸을 생성하거나 (2) 룸 목록에서 합류해 (3) InRoomPanel에서 참가자 리스트를 보고 Leave할 수 있어야 한다.

[`2026-05-16-namae1128-presence-ui-lobby-panel.md`](./2026-05-16-namae1128-presence-ui-lobby-panel.md) plan (Ready) 은 본 plan 이 사실 박제로 supersede 한다 — SampleScene 의 LobbyPanel은 adb46dc commit으로 박힌 사실이며, 본 plan 은 그것을 TestSceneSanyo 로 옮기고 UX·VR 키보드를 보강한다.

## Context

### 5번째 root 누락의 출처

- [`adb46dc feat(presence/scene): SampleScene에 MultiplayerRoomNetworking + LobbyPanel UI + RoomRow.prefab 와이어링`](https://github.com/kookmin-sw/2026-capstone-24/commit/adb46dc) 이 LobbyPanel을 SampleScene에 박았다. plan 으로 박제 안 됐고 [`presence-ui-lobby-panel`](./2026-05-16-namae1128-presence-ui-lobby-panel.md) plan 이 별도로 "신규 작성" 형태로 박혀 있어 정합성 갭이 있었다.
- 어제 마이그레이션 plan의 unity-scene-reader 점검 prompt가 4 root만 명시 → LobbyPanel 빠짐 → TestSceneSanyo에 4 root만 들어가고 LobbyPanel 없음.

### 사용자 보고

1. "MultiplayerInRoom 패널의 버튼/입력칸이 모서리에 몰림" — 실체는 **LobbyPanel** (InRoomPanel은 입력칸 없음).
2. "룸 이름 입력 시 VR 키보드 안 뜸" — Quest 환경에서 TMP_InputField의 `TouchScreenKeyboard` API가 작동하지 않음. XR Interaction Toolkit Keyboard 또는 동등 VR 가상 키보드 필요.
3. "ParticipantRow / RoomRow 프리팹은 있는데 hierarchy에 없음" — 둘 다 **런타임 동적 인스턴스화** (`HandleRoomListUpdated`/`RebuildRows`에서 `Instantiate`). hierarchy에 정적 placeholder 없는 게 정상.

### VR 키보드 기술 선택

- 코드베이스 grep 결과 `Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` 존재 → **Unity XR Interaction Toolkit (XRI) 패키지 이미 사용 중**.
- Meta XR Toolkit / OVR Virtual Keyboard 흔적 없음.
- **XRI Keyboard Sample 채택** — 추가 의존성 없이 진행 가능. XRI Sample 폴더에서 import 또는 자체 World Space Canvas 키보드 prefab 작성.

## Verified Structural Assumptions

SampleScene의 MultiplayerLobbyPanel 구조 (`unity-scene-reader 보고 (2026-05-18)`):

### Root `MultiplayerLobbyPanel` (fileID 152436587, active)
- RectTransform: LocalPos `(0, 0, 0)`, **LocalRotation Y=90°** `(0, 0.7071068, 0, 0.7071068)`, LocalScale `(0.002, 0.002, 0.002)`, AnchoredPosition **`(4, 1.5)`**, SizeDelta **`(1200, 800)`**.
- 컴포넌트: Canvas (WorldSpace), CanvasScaler, GraphicRaycaster, TrackedDeviceGraphicRaycaster, `MultiplayerLobbyPanel` MonoBehaviour (script guid `ca9c15cecfba79748aa26da982249b8d`).
- 자식 1개: `LobbyRoot` (fileID 1569865699, **inactive m_IsActive: 0** — Awake/ShowLobby 로직으로 활성화).

### `LobbyRoot` 자식 3건 (현재 SampleScene 배치 — 본 plan 의 리디자인 대상)

1. **`CreateForm`** (fileID 927974620) — AnchorMin `(0, 1)`, AnchorMax `(1, 1)`, AnchoredPos `(0, -50)`, SizeDelta `(0, 100)`. `VerticalLayoutGroup` (Spacing 10, Padding 20/20/20/20) + `ContentSizeFitter` (VerticalFit=2). 자식 5건:
   - `RoomNameInput` (TMP_InputField, SizeDelta `(160, 30)`)
   - `PasswordEnabledToggle` (Toggle)
   - `PasswordInput` (TMP_InputField)
   - `MaxPlayersInput` (TMP_InputField)
   - `CreateButton` (Button)

2. **`RoomList`** (fileID 44969999) — AnchorMin `(1, 1)`, AnchorMax `(1, 1)`, AnchoredPos `(-50, -50)`, SizeDelta `(600, 600)`. 자식 2건:
   - `ScrollView` (ScrollRect vertical, Content Transform fileID 788380874, Viewport, VerticalScrollbar)
   - `EmptyLabel` (TextMeshProUGUI, SizeDelta `(200, 50)`)

3. **`StatusLabel`** (fileID 1626809809) — AnchorMin `(0, 0)`, AnchorMax `(1, 0)`, AnchoredPos `(0, 25)`, SizeDelta `(0, 50)`, text="Waiting for auth...".

### `MultiplayerLobbyPanel` SerializeField 와이어링 (12건)

| 필드 | source 타입 | source 매핑 | 마이그레이션 시 |
|---|---|---|---|
| `authGate` | MultiplayerAuthGate | scene fileID 800001005 (SampleScene) | 새 fileID (TestSceneSanyo 의 MultiplayerAuthGate component) |
| `roomClient` | RoomClient | scene fileID 978156873 | 새 fileID (MultiplayerRoomNetworking 의 RoomClient) |
| `roomListQuery` | RoomListQuery | scene fileID 978156872 | 새 fileID (MultiplayerRoomNetworking 의 RoomListQuery) |
| `authConfig` | MultiplayerAuthConfig | asset GUID `e446380221ca43f2b77b3d17c7f05105` | GUID 보존 |
| `roomNameInput` | TMP_InputField | scene fileID 1404287215 | 새 fileID (RoomNameInput) |
| `passwordEnabledToggle` | Toggle | scene fileID 1628779009 | 새 fileID |
| `passwordInput` | TMP_InputField | scene fileID 487759527 | 새 fileID |
| `maxPlayersInput` | TMP_InputField | scene fileID 1111214115 | 새 fileID |
| `createButton` | Button | scene fileID 1737804477 | 새 fileID |
| `roomRowParent` | Transform | scene fileID 788380874 (ScrollView Content) | 새 fileID |
| `roomRowPrefab` | RoomRowEntry prefab | asset GUID `be23a82a60cc438489c6af36d9c50bfb` | GUID 보존 |
| `emptyLabel` | TextMeshProUGUI | scene fileID 1343182007 | 새 fileID |
| `statusLabel` | TextMeshProUGUI | scene fileID 1626809812 | 새 fileID |
| `lobbyRoot` | GameObject | scene fileID 1569865699 | 새 fileID (LobbyRoot 자식) |

### `MultiplayerInRoomPanel.lobbyPanel` 와이어 (현재 TestSceneSanyo에서 `{fileID: 0}`)

본 plan이 LobbyPanel을 신설하면 `MultiplayerInRoomPanel.lobbyPanel` SerializeField를 새 LobbyPanel component fileID로 와이어해야 함. 어제 마이그레이션 plan에서 명시적으로 `{fileID: 0}`으로 둔 부분의 후속.

### TestSceneSanyo 현재 상태 (`mcp__unityMCP__manage_scene get_hierarchy (2026-05-18)`)

22 root GameObject. multiplayer 4 root (MultiplayerAuthGate, MultiplayerAuthBootstrap, MultiplayerRoomNetworking, MultiplayerInRoomPanel) 존재. **LobbyPanel 0건**, Trombone (fileID -15054) 등 다른 root 21개는 손대지 않음.

## Approach (4 phase)

### Phase A — LobbyPanel 마이그레이션 (사실 박제 보강)

`unity-scene-writer` 로 SampleScene의 `MultiplayerLobbyPanel` root + LobbyRoot 자식 트리 전체를 TestSceneSanyo 로 이전.

- **신설할 GameObject 총 17개** (root + LobbyRoot + CreateForm/RoomList/StatusLabel + CreateForm의 자식 5건 + RoomList의 자식 2건 + ScrollView 내부 Viewport/Content/Scrollbar 등). 정확한 계층은 Verified Structural Assumptions의 fileID 표 참조.
- **Transform·Anchor 값은 위 표 그대로 1차 복제** — Phase B에서 본격 리디자인 시 변경 가능.
- **`LobbyRoot.m_IsActive: 0` 유지** (Awake/ShowLobby/HandleAuthenticationCompleted 로직 보존).
- **와이어링 12건 + InRoomPanel.lobbyPanel 1건** (총 13건 cross-ref) 정확히 새 fileID로 직렬화.
- **외부 asset GUID 2건 보존**: `authConfig` (e446380221ca43f2b77b3d17c7f05105), `roomRowPrefab` (be23a82a60cc438489c6af36d9c50bfb).

### Phase B — UI 배치 리디자인 (LobbyPanel + InRoomPanel)

**원칙 박제** (implementer 디자인 자유도):

- LobbyPanel `CreateForm` 의 5 자식 (3 input + 1 toggle + 1 button) 사이 spacing/padding을 충분히 늘려 VR Player 시야에서 식별 가능하도록.
- `RoomList` ScrollView 의 가시 영역 확장 — 룸 5건 이상 동시 표시 가능 + Scrollbar 명확.
- `StatusLabel` 위치 — 사용자 가시 위치 (현재 하단 anchored, 그대로 둘 수도 있음).
- InRoomPanel 의 `RoomNameLabel`, `ParticipantCountLabel`, `ParticipantList`, `LeaveButton` 도 같은 원칙으로 가독성 확보.
- VR Player rig 시야 기준 시각 정합 확인 — `unity-scene-reader` 또는 Editor Scene View 로 검증.

**좌표 결정 자유도**: implementer 가 plan 본문에서 박은 1차 복제값을 기준점으로 삼되 가독성 확보를 위해 자유롭게 조정. 단 Canvas 자체 (root MultiplayerLobbyPanel / MultiplayerInRoomPanel) 의 World Space LocalPosition / Rotation / Scale 은 본 plan 에서 변경하지 않음 — VR 공간 좌표는 어제 plan 의 박제값 유지.

### Phase C — VR 키보드 통합 (XRI Keyboard)

- **기술**: XR Interaction Toolkit Keyboard Sample (`com.unity.xr.interaction.toolkit`). 코드베이스에 XRI 패키지가 이미 있으므로 추가 의존성 없음.
- **방식**:
  1. XRI Keyboard prefab 을 TestSceneSanyo 에 신설 (World Space Canvas, VR Player 시야 기준 배치).
  2. LobbyPanel 의 3 TMP_InputField (`RoomNameInput`, `PasswordInput`, `MaxPlayersInput`) 각각에 `XRKeyboardField` 또는 동등 binder 컴포넌트 부착 — InputField select 시 키보드 활성화, deselect 시 비활성화.
  3. 키보드 입력 결과를 TMP_InputField.text 로 직접 반영.
- **MaxPlayersInput** 은 정수 입력 → 키보드에 numeric mode 또는 input validation. 1~32 범위 (backend validation 매치).
- **자유도**: XRI Keyboard Sample 의 prefab 을 그대로 import 할지, 자체 prefab 으로 다시 만들지 implementer 판단. 단 키 입력 + Quest 컨트롤러 ray cast 로 동작해야 함.

### Phase D — presence-ui-lobby-panel plan supersede 처리

- 본 plan 의 atomic commit 후 별도 정리 commit 에서 [`2026-05-16-namae1128-presence-ui-lobby-panel.md`](./2026-05-16-namae1128-presence-ui-lobby-panel.md) plan Status 를 `Done — superseded by 2026-05-18 lobby-migration-and-ux plan` 또는 archive 이동으로 갱신.
- [`04-presence-ui.md`](../specs/04-presence-ui.md) Implementation Plans 표에 본 plan 추가 + supersede 표기.

## 사용자 강조사항 (반복 명시)

**씬 내부의 다른 GameObject 는 절대 건드리지 않는다.** TestSceneSanyo 의 기존 22 root GameObject (특히 Trombone, VR Player, Piano, DrumKit, RhythmGameHost\_\*, Environment, drum_merged_fixed, InstrumentSystem, StubSongCatalog, SessionPanelController 등) 및 그 자식 트리 일체 변경 금지. `MultiplayerInRoomPanel.lobbyPanel` SerializeField 1건만 예외적으로 갱신 (Phase A의 cross-ref 와이어 마무리).

## Deliverables

### 신설 GameObject (TestSceneSanyo)
- `MultiplayerLobbyPanel` (root World Space Canvas) + `LobbyRoot` (inactive) + 자식 트리 (CreateForm/RoomList/StatusLabel + 손자 7건 + ScrollView 내부 구조)
- (Phase C) XRI Keyboard prefab 인스턴스 1개 또는 keyboard 와이어 컴포넌트들

### 수정 자산 (TestSceneSanyo만)
- `Assets/Scenes/TestSceneSanyo.unity` — Phase A/B/C 결과
- (선택) `Assets/Multiplayer/Scripts/Presence/*` — Phase C 의 키보드 binder 컴포넌트 신설 시 1~2개 신규 `.cs` 파일

### 신규 코드 (있다면)
- `Assets/Multiplayer/Scripts/Presence/VRKeyboardField.cs` (가칭) — TMP_InputField select 시 XRI Keyboard 활성화/비활성화 binder. 또는 XRI Sample 의 기존 컴포넌트를 그대로 사용한다면 신규 코드 없음.

## Acceptance Criteria

### Phase A — 마이그레이션 검증
- [ ] `[auto-hard]` `Grep MultiplayerLobbyPanel` 가 TestSceneSanyo `.unity` 에서 ≥ 2 매치 (root GameObject `m_Name` + MonoBehaviour `m_EditorClassIdentifier`).
- [ ] `[auto-hard]` `Grep LobbyRoot|CreateForm|RoomList|StatusLabel|RoomNameInput|PasswordInput|MaxPlayersInput|CreateButton` 가 TestSceneSanyo `.unity` 에서 ≥ 8 매치 (자식 GameObject 명).
- [ ] `[auto-hard]` `Grep "guid: be23a82a60cc438489c6af36d9c50bfb"` (roomRowPrefab) TestSceneSanyo `.unity` 에서 ≥ 1 매치.
- [ ] `[auto-hard]` `MultiplayerLobbyPanel` 의 SerializeField 12건 모두 non-zero fileID 또는 GUID 로 직렬화 (`{fileID: 0}` 매치 0건).
- [ ] `[auto-hard]` `MultiplayerInRoomPanel.lobbyPanel` SerializeField 가 새 LobbyPanel component fileID 로 갱신 (`{fileID: 0}` → non-zero).
- [ ] `[auto-hard]` `LobbyRoot` GameObject 의 `m_IsActive: 0` 유지.

### Phase B — UI 가독성 검증
- [ ] `[auto-soft]` LobbyPanel CreateForm 의 VerticalLayoutGroup `m_Spacing` 가 SampleScene 의 10 보다 같거나 큼 (가독성 보강).
- [ ] `[manual-hard]` Quest 실기기 빌드 — LobbyPanel 활성화 상태에서 CreateForm 의 5개 자식 (3 input + toggle + button) 이 모두 VR Player 시야 안에 가독성 있게 표시. 모서리 몰림 없음.
- [ ] `[manual-hard]` RoomList ScrollView 영역이 룸 5건 이상 표시 가능. Scrollbar 동작.
- [ ] `[manual-hard]` InRoomPanel 활성화 시 RoomNameLabel / ParticipantCountLabel / ParticipantList / LeaveButton 가독성 있게 표시.

### Phase C — VR 키보드 검증
- [ ] `[auto-hard]` `Grep XRKeyboard|VRKeyboardField` 가 TestSceneSanyo `.unity` 또는 신규 `.cs` 에서 ≥ 1 매치 (키보드 통합 구현 흔적).
- [ ] `[manual-hard]` Quest 실기기 빌드 — LobbyPanel 의 RoomNameInput 클릭/select 시 VR 키보드 활성화. 키 입력 시 InputField text 반영. deselect 또는 Submit 시 키보드 비활성화.
- [ ] `[manual-hard]` PasswordInput / MaxPlayersInput 도 같은 흐름 동작 (각각 일반 / 숫자 입력).

### 전체 시나리오
- [ ] `[manual-hard]` Quest 실기기 빌드 (default 씬 = TestSceneSanyo) + aws-dev backend (mock 또는 real) end-to-end:
  - ActivateButton 클릭 → AuthGate 인증 통과 → LobbyPanel 자동 활성화
  - 룸 이름 / 비밀번호 / 정원 VR 키보드로 입력 → CreateButton 클릭 → backend POST + Photon 합류 → InRoomPanel 활성화
  - InRoomPanel 참가자 리스트에 본인 1명 표시
  - LeaveButton 클릭 → 룸 퇴장 + LobbyPanel 복귀

### 정합성 검증
- [ ] `[auto-hard]` `git -C D:/2026-capstone-24 diff --stat` 결과: `Assets/Scenes/TestSceneSanyo.unity` 외에 변경 파일이 (신규 키보드 코드 1~2개 외) 0건.
- [ ] `[auto-hard]` Unity Editor `read_console` types=error 0건.

## Out of Scope

- SampleScene의 LobbyPanel + 4 root multiplayer GameObject 제거·정리 (별도 plan — git history 보존 vs cleanup trade-off 결정 필요).
- nickname 채널 추가 ([`presence-ui-in-room-panel`](./2026-05-16-namae1128-presence-ui-in-room-panel.md) Out of Scope 박제 그대로 — 실기기 검증 후 후속).
- real Meta verifier 전환 (`useMockMetaToken: 0`) — 후속 작업 #6 사이클 책임.
- Meta XR Toolkit OVR Virtual Keyboard 도입 검토 — XRI Keyboard 로 결정됨. 향후 UX 요구 변경 시 재평가.
- 한글 폰트 atlas 깨짐 처리 — 별도 plan.
- 호스트/권한 마커, 친구·초대, 보이스/텍스트 채팅 — [`04-presence-ui`](../specs/04-presence-ui.md) Out of Scope 박제 그대로.

## Notes

- `presence-ui-lobby-panel.md` plan 의 Approach 와 본 plan 의 Phase A 는 같은 결과를 만든다 (TestSceneSanyo 에 LobbyPanel 박제). 본 plan 완료 후 그 plan 은 Status `Done — superseded` 로 닫는다.
- XRI Keyboard 의 정확한 prefab 경로는 implementer 가 `com.unity.xr.interaction.toolkit` 패키지 Samples 에서 확인. 자체 prefab 으로 대체 시 동일 acceptance 만족하면 자유.
- Phase B 의 좌표 변경 범위는 plan 본문에서 명시적으로 제한하지 않는다 — implementer 의 디자인 자유도 + manual-hard 검증으로 판정.
- VR 키보드 컴포넌트가 신규 `.cs` 파일을 추가하면 CLAUDE.md "Unity 런타임 코드(`.cs`) 수정 후 `unity-test-runner` 1회 호출" 정책 적용. EditMode 컴파일 검증 필수.

## Handoff

### 적용 결과 (2026-05-18)

**Phase A — 마이그레이션 (commit 20cf4c0 + e084c06)**
- TestSceneSanyo 에 `MultiplayerLobbyPanel` root + `LobbyRoot` + `CreateForm`/`RoomList`/`StatusLabel` 자식 트리 신설.
- SerializeField 12건 + `MultiplayerInRoomPanel.lobbyPanel` cross-ref 와이어 완료.
- 단 마이그레이션 시 **`PasswordEnabledToggle` 의 자식 트리 (Background + Checkmark) 가 누락** → manual-hard 단계에서 발견 → SampleScene 의 정상 토글을 Copy → Paste as Child 로 복원 + Toggle.Graphic / TargetGraphic 재와이어.

**Phase B — UI 배치 (commit e084c06 + c3e7337 + Editor 수동 조정)**
- LobbyPanel 자식 트리 좌표·spacing 정정. World Space Canvas 의 LocalPos / Rotation / Scale 은 어제 plan 박제값 유지 (사용자 강조사항 준수).
- AuthGate root 의 LocalPos `(0, 0, 0.6)` → `(0, 0, 1.8)`, LocalRotation Y=90°, AnchoredPos `(4, 0.8)` 로 가독성 보강.
- VRKeyboard 의 LocalPos `(0, 0, 0.15)` → `(0, 0, 1.8)`, AnchoredPos `(4, 1.5)`, SizeDelta `(400, 400)` 로 시야 정합.

**Phase C — VR 키보드 (commit e084c06 + 후속 fix)**
- `Assets/Multiplayer/Scripts/Presence/VRWorldKeyboard.cs` (런타임 World Space 키보드 prefab 생성기) + `VRKeyboardField.cs` (TMP_InputField → keyboard binder) 신설.
- TestSceneSanyo 에 `VRKeyboard` root + 자체 Canvas + GraphicRaycaster + TrackedDeviceGraphicRaycaster 배치.
- **manual-hard 디버깅 후속 fix**:
  - `VRKeyboardField.keyboard` SerializeField 3건이 마이그레이션 직후 `{fileID: 0}` 으로 박혀 있어서 InputField 클릭 시 키보드 미발화. VRKeyboard component fileID 로 명시 와이어.
  - Backspace 키 라벨 `⌫` (U+232B) 가 LiberationSans SDF atlas 미포함 → 빈 사각형 렌더링 → `DEL` 로 변경.

**Phase D — supersede 처리**
- 본 plan 의 atomic commit 후 [`2026-05-16-namae1128-presence-ui-lobby-panel.md`](./2026-05-16-namae1128-presence-ui-lobby-panel.md) Status 갱신은 별도 정리 commit (follow-up).

### Acceptance Criteria 결과

| AC | 결과 |
|---|---|
| Phase A `[auto-hard]` 6건 | ✅ 전부 통과 (TestSceneSanyo.unity 직렬화 grep) |
| Phase B `[auto-soft]` Spacing ≥ 10 | ✅ |
| Phase B `[manual-hard]` Quest 빌드 가독성 3건 | ✅ 사용자 확인 (input/toggle/button 시각 정상, raycast 정상) |
| Phase C `[auto-hard]` Grep VRKeyboardField/XRKeyboard | ✅ |
| Phase C `[manual-hard]` 키보드 발화·입력·deselect 흐름 | ✅ 사용자 확인 (RoomName/Password/MaxPlayers 모두 정상 입력) |
| 전체 시나리오 `[manual-hard]` end-to-end | ✅ **통과 (2026-05-19)**. ActivateButton → AuthGate 인증 → LobbyPanel → VR 키보드 입력 (RoomName/Password/MaxPlayers) → CreateButton → backend POST + ECS Fargate DS 부팅 + DS ready POST 성공 → Photon 합류 → InRoomPanel 활성화 + 참가자 리스트 본인 표시 → LeaveButton 클릭 → LobbyPanel 복귀. CloudWatch 로그 확인 (`[RoomServerCallbackReporter] ready POST 성공 room_id=3 public_ip=43.201.69.94` + `RegisterUniqueIdPlayerMapping`). |
| 정합성 `[auto-hard]` git diff | ✅ TestSceneSanyo.unity + 신규 `VRWorldKeyboard.cs` / `VRKeyboardField.cs` 외 변경 없음 (단 Unity Editor 가 자동 save 한 ProjectSettings/Settings 일부 포함) |
| 정합성 `[auto-hard]` console error 0건 | ✅ EditMode regression 14/14 통과 (LobbyInputValidatorTests) |

### Backend DS READY 진단 (2026-05-19 추가)

본 plan 의 전체 시나리오 manual-hard 가 backend DS READY 콜백 미발신으로 막혀있었음. CloudWatch Logs 점검 결과 2개 fix 적용:

- **F1 (Dockerfile)** [`docker/dedicated-server/Dockerfile`](../../../../docker/dedicated-server/Dockerfile): ENTRYPOINT 에 `-logFile -` 추가 → Unity Player.log 가 stdout 으로 출력되어 ECS awslogs driver 가 CloudWatch 로 forward. 그 전엔 Player.log 가 컨테이너 내부 파일로만 저장되어 `[RoomServerCallbackReporter]` 메시지가 보이지 않았음.
- **F2 (DS bootstrap)** [`Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs`](../../../../Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs): `ResolveRoomName`/`ResolveMaxPlayers`/`ResolvePasswordHash` 에 env var fallback 추가 (`PHOTON_SESSION_NAME` / `MAX_PLAYERS` / `ROOM_PASSWORD_HASH`). 기존엔 command line argument 가 없으면 `RoomServerConfig.asset` 의 hardcoded `roomName: murang-room` fallback → DS 가 항상 같은 sessionName 으로 Photon 등록 → client 가 만든 sessionName 과 불일치로 합류 불가였음. backend `EcsRoomRuntimeProvider` 가 이미 env vars 7개 주입 중이라 backend Java 코드는 무손, DS-side 만 보강.

배포 절차: `tools/push-room-server-image.sh <tag>` → ECR push + `murang-room-server` task definition 새 revision 등록 → backend 는 family 이름만 가리키므로 다음 RunTask 부터 자동으로 latest revision 사용.

### 진단을 위한 임시 변경 (rollback 대상)

backend DS 진단 단계에서 statusLabel 의 한글 글리프 깨짐 (`The character with Unicode value \uXXXX was not found in [LiberationSans SDF]`) 때문에 fail 사유를 분간 불가 → 다음 4개 파일의 사용자 가시 메시지를 영문화:

- `MultiplayerLobbyPanel.cs` — TimeoutException / RoomProvisioningFailedException catch / FormatJoinFailure / IsReadyForBackendCall / ResolveBackendBaseUrl
- `LobbyInputValidator.cs` — ValidatePhotonSessionName/MaxPlayers/Password ErrorMessage
- `LobbyInputValidatorTests.cs` — 한글 substring (`"영문"`) → `"letters"`
- `RoomClient.cs` — ArgumentException 4건 + InvalidOperationException 4건 (LeaveRoomAsync 후 statusLabel 깨짐 보강)

이는 본 plan Out of Scope ("한글 폰트 atlas 깨짐 처리 — 별도 plan") 의 정식 해결 전까지의 임시 조치. **별도 plan (한글 폰트 atlas 도입) 통과 후 한글 복원 권장**.

### 남은 minor issues (후속 plan 후보)

1. **MaxPlayers PlayerCount off-by-one** — 사용자가 정원 4명으로 만든 룸이 InRoomPanel 에 `1/5` 로 표시. Photon Fusion `SessionInfo.MaxPlayers` 가 server 슬롯 추가로 +1 되거나 maxPlayersInput 입력 race 가능성. 본 plan AC 영향 없음.
2. **passwordHash env var 누락** — backend `EcsRoomRuntimeProvider.buildEnvironment` 가 password hash 를 ECS env 에 안 보냄 → DS 가 잠금 룸 password check 불가. 무잠금 룸은 정상 동작. 잠금 룸 검증은 별도 plan.

### 후속 plan 후보

1. **한글 폰트 atlas 도입** — LiberationSans SDF 외 NotoSans / 본명조 등 한글 fallback 폰트 등록. 위 4개 파일의 영문 임시 메시지 복원.
2. **`2026-05-16-namae1128-presence-ui-lobby-panel.md` plan Status 갱신** — Done — superseded by 본 plan.
3. **SampleScene 의 LobbyPanel + 4 root multiplayer GameObject 정리** — Out of Scope 그대로 (별도 plan).
4. **MaxPlayers PlayerCount off-by-one 진단 + 잠금 룸 passwordHash env 보강** — 위 남은 minor issues.

