# Presence UI 인-룸 패널 (참가자 리스트 + 퇴장)

**Linked Spec:** [`04-presence-ui.md`](../specs/04-presence-ui.md)
**Caused By:** [`2026-05-16-namae1128-presence-ui-lobby-panel.md`](./2026-05-16-namae1128-presence-ui-lobby-panel.md)
**Status:** `Ready`

## Goal

`SampleScene` 안 월드스페이스 Canvas 로 구현된 인-룸 패널을 추가해, 룸에 합류한 유저가 (1) 현재 룸 참가자의 닉네임 목록을 실시간으로 보고 (2) 퇴장 버튼으로 룸을 떠나 로비 상태로 복귀할 수 있게 한다. 룸 합류·퇴장 이벤트로 로비 패널과의 토글이 닫히는 사이클을 완성한다. [`04-presence-ui.md`](../specs/04-presence-ui.md) Behavior 의 "다른 유저 입퇴장 → 접속 유저 목록 즉시 갱신" 항목을 본 plan 이 닫는다.

## Context

선행 [`2026-05-16-namae1128-presence-ui-lobby-panel.md`](./2026-05-16-namae1128-presence-ui-lobby-panel.md) 가 로비 ↔ in-room 토글을 위해 `MultiplayerLobbyPanel.OnEnteredRoom` 이벤트를 발행하지만, 그 이벤트를 받아 in-room 측 UI 를 활성화하고 퇴장 시 다시 로비를 부르는 측은 별도 plan 으로 분리. 본 plan 은 그 부분을 채운다.

참가자 닉네임은 backend `playerId` 를 신원 키로 쓰지만 표시 값은 `nickname`. [`AuthSession`](../../../../Assets/Multiplayer/Scripts/Auth/AuthSession.cs) 가 `UserMeResponse.nickname` 을 보유하므로 본인 닉네임은 로컬에서 즉시 표시 가능. 다른 유저의 닉네임은 Photon Custom Auth 의 `UserId` 와 `AuthenticationValues` 에 인코딩된 메타데이터를 통해 받아야 하나, 현 시점 `RoomClient.JoinSessionAsync` 는 `playerId` 만 `AuthenticationValues` 에 넣고 nickname 은 안 보냄 ([`Read Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs (2026-05-16)`](../../../../Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs)). 본 plan 은 nickname 전달 채널을 추가하지 않고, **1차 출시 범위에서는 `PlayerRef` + `playerId` 만 행으로 표시**한다. nickname 표시는 후속 plan 으로 분리.

이 단순화는 [`04-presence-ui.md`](../specs/04-presence-ui.md) Open Question(이미 닫힘) "닉네임 목록까지 바로 필요한지" 에 대해 1차 출시는 "현재 인원/정원 + playerId" 로 충분하다는 가정을 박제하고 진행한다. 실기기 검증에서 부족하다 판정 시 후속 plan 으로 nickname 채널 추가.

## Verified Structural Assumptions

- `RoomClient` 는 `INetworkRunnerCallbacks` 를 직접 구현해 `OnConnectedToServer`/`OnDisconnectedFromServer`/`OnShutdown` 을 처리 — `Read Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs (2026-05-16)`. `OnPlayerJoined`/`OnPlayerLeft` 는 빈 메서드이므로 본 plan 에서 채우거나, 별도 listener 컴포넌트가 같은 `NetworkRunner` 에 callback 등록해 받음. **본 plan 은 후자(별도 listener)** 를 채택해 `RoomClient` 의 다른 책임을 침범하지 않는다.
- `NetworkRunner.LocalPlayer` 는 Fusion 의 `PlayerRef` 로 합류한 본인 식별자. `runner.SessionInfo.PlayerCount` 가 현재 룸 인원, `runner.SessionInfo.MaxPlayers` 가 정원 — `Read Library/PackageCache/com.exitgames.photon.fusion@<hash>/Runtime/...` 가정 (Photon Fusion 표준 API). MCP 미사용 — 가정, Photon Fusion 표준 surface.
- `MultiplayerLobbyPanel.OnEnteredRoom : event Action<string roomName>` 가 선행 plan 에서 발행됨. 본 plan 은 그 이벤트를 listen 해 in-room 활성화.

## Approach

1. **`MultiplayerInRoomPanel` MonoBehaviour 신설** (`Assets/Multiplayer/Scripts/Presence/MultiplayerInRoomPanel.cs`).
   - 인스펙터 필드: `MultiplayerLobbyPanel lobbyPanel`, `RoomClient roomClient`, `Transform participantRowParent`, `ParticipantRowEntry participantRowPrefab`, `TMP_Text roomNameLabel`, `TMP_Text participantCountLabel`, `Button leaveButton`, `GameObject inRoomRoot`.
   - 초기 `inRoomRoot.SetActive(false)`. `MultiplayerLobbyPanel.OnEnteredRoom += HandleEnteredRoom` 으로 활성 트리거.
2. **참가자 리스트 갱신 listener**.
   - `MultiplayerInRoomPanel` 내부 (또는 별도 `RoomParticipantListListener` 컴포넌트) 가 `INetworkRunnerCallbacks` 를 구현하고, `HandleEnteredRoom` 직후 `roomClient` 가 보유한 `NetworkRunner` 에 `AddCallbacks(this)` 로 등록.
   - `OnPlayerJoined(NetworkRunner runner, PlayerRef player)` / `OnPlayerLeft(...)` 에서 내부 `Dictionary<PlayerRef, ParticipantEntry>` 갱신 → `RefreshParticipantRows()`.
   - `OnConnectedToServer`/`OnShutdown` 에서도 호출해 초기 LocalPlayer 한 줄을 즉시 표시.
3. **참가자 행 표시 (1차)**.
   - `ParticipantRowEntry` (`Assets/Multiplayer/Scripts/Presence/ParticipantRowEntry.cs`) 컴포넌트: `TMP_Text label`, `void Bind(string text)`.
   - 표시 문자열: `<playerId 짧은 prefix 6자> (You)` 또는 `<playerId 짧은 prefix 6자>`. nickname 채널은 후속 plan.
   - `participantCountLabel` 은 `runner.SessionInfo` 에서 `<current>/<max>` 표시.
4. **퇴장 흐름**.
   - `leaveButton.onClick` → `OnLeaveClicked()` (async).
   - `roomClient.LeaveRoomAsync()` await → `inRoomRoot.SetActive(false)` → `lobbyPanel.ShowLobby()` 호출.
   - `MultiplayerLobbyPanel.ShowLobby()` 메서드 추가 (선행 plan 결과물에 한 줄 보강): `lobbyRoot.SetActive(true)`, statusLabel 초기화.
5. **세션 측 우발 종료 처리**.
   - `OnShutdown(NetworkRunner, ShutdownReason)` 콜백에서 if `inRoomRoot.activeSelf` then auto-leave 흐름 (퇴장과 동일).
6. **EditMode 테스트** (가능한 범위만).
   - `ParticipantListBuilderTests`: `PlayerRef → display string` 매핑 함수 (`ParticipantDisplayFormatter` static) 단위 테스트. 자기 자신 분기, prefix 길이 등 ≥ 4 케이스.
   - 실제 callback 흐름은 PlayMode/실기기.

## Deliverables

- `Assets/Multiplayer/Scripts/Presence/MultiplayerInRoomPanel.cs` — in-room 패널 MonoBehaviour (`INetworkRunnerCallbacks` 구현)
- `Assets/Multiplayer/Scripts/Presence/ParticipantRowEntry.cs` — 참가자 행 UI 컴포넌트
- `Assets/Multiplayer/Scripts/Presence/ParticipantDisplayFormatter.cs` — `PlayerRef`/`playerId` → 표시 문자열 매핑 (static)
- `Assets/Multiplayer/Resources/ParticipantRow.prefab` — 참가자 행 prefab
- `Assets/Scenes/SampleScene.unity` — `MultiplayerInRoomPanel` Canvas + 자식 UI 배치 + reference 와이어링
- `Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs` — `ShowLobby()` public 메서드 한 줄 보강
- `Assets/Multiplayer/Scripts/Room/Tests/ParticipantDisplayFormatterTests.cs` — EditMode 단위 테스트

## 검증 기준

본 plan 의 acceptance 는 [`01-user-auth`](../specs/01-user-auth.md) 와 동일하게 **Quest 실기기 빌드(APK 사이드로드)** 만으로 판정한다. Unity Editor Play 환경에서만 재현되는 동작 이슈는 acceptance 위반으로 취급하지 않으며, 별도 plan 으로 분리하지 않고 무시한다. (참고: [`cd80f2b docs(specs/multiplayer-network/01-user-auth)`](https://github.com/kookmin-sw/2026-capstone-24/commit/cd80f2b).)

## Acceptance Criteria

- [ ] `[auto-hard]` `ParticipantDisplayFormatterTests` 단위 테스트 통과 — LocalPlayer "You" 분기, prefix 길이, 빈 playerId 처리 등 ≥ 4 케이스.
- [ ] `[auto-hard]` Unity Editor 컴파일 통과 + 기존 EditMode 테스트(`RoomPasswordHasherTests`, `RoomAuthorityValidateJoinTests`, `RoomListMapperTests`, `RoomServerCallbackConfigTests`, `RoomProvisioningServiceTests`) 회귀 없음.
- [ ] `[manual-hard]` Quest 실기기 빌드 (aws-dev backend): 선행 plan 의 룸 생성 흐름 통과 후 in-room 패널이 활성화되고 본인이 1명으로 표시. 2번째 Quest (또는 Quest 한 대 + Quest 빌드의 PC standalone build) 로 합류해 듀얼 입퇴장 검증 (참가자 리스트 실시간 반영). Leave 버튼 클릭 시 로비 복귀 + Photon shutdown 까지 한 사이클 확인.

## Out of Scope

- nickname 표시 (별도 plan — Photon Custom Auth 에 nickname 채널 추가가 필요)
- 호스트 표시·연결 상태·아이콘 마커
- 친구·초대
- 보이스/텍스트 채팅
- 룸 메타데이터 변경 (이름, 비밀번호 변경 등)

## Notes

- 본인 표시("You") 의 정확한 판단 기준은 `runner.LocalPlayer == playerRef`. Photon Fusion 의 LocalPlayer 가 즉시 채워지지 않을 가능성이 있어 `OnConnectedToServer` 이후 한 번 `RefreshParticipantRows()` 를 재호출하는 안전 장치 필요.
- nickname 채널을 추가하는 후속 plan 에서는 Photon `AuthenticationValues.SetAuthPostData(...)` 또는 RoomJoinTicket payload 로 nickname 을 함께 보내는 방식이 가장 단순.
- `tools/run-stack-smoke` 자동화 (별도 active plan) 의 `same-session` 시나리오에 본 plan 의 행 갱신 검증을 결합하는 회귀 가드 강화는 후속 plan 책임.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
