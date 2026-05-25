# Presence UI 로비 패널 (룸 생성 + 룸 목록)

**Linked Spec:** [`04-presence-ui.md`](../../../multiplayer-network/specs/04-presence-ui.md)
**Status:** `Done (2026-05-25) — superseded by 2026-05-18 lobby-migration-and-ux plan (lobby 패널이 TestSceneSanyo 로 마이그레이션 + UX 리디자인 + VR 키보드 통합 모두 완료, Quest 실기기 검증 통과).`

## Goal

default 씬([`../decisions/01-default-scene.md`](../decisions/01-default-scene.md)) 안 월드스페이스 Canvas 로 구현된 로비 패널을 추가해, 인증 통과한 유저가 (1) 새 룸을 생성하거나 (2) admission 가능한 룸 목록을 보고 합류할 수 있게 한다. 룸 생성 흐름은 [`RoomClient.CreateRoomThroughBackendAsync`](../../../../Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs)(backend 경유) 를 통해 ECS Fargate 룸 서버를 띄우고 READY 도달 후 합류한다. 룸 목록은 기존 [`RoomListQuery`](../../../../Assets/Multiplayer/Scripts/Room/Client/RoomListQuery.cs) 의 Photon SessionList 결과를 사용한다. [`quest-onsite-integration-verification`](./2026-05-11-namae1128-quest-onsite-integration-verification.md) AC #5 (Quest 빌드 → EC2 → Fargate 합류 → 서버 로그 입장 기록) 가 본 plan 완료로 발화 가능해진다.

## Context

이전 plan 들에서 backend/dedicated-server/Unity 라이브러리는 완성됐지만 UI 트리거가 없어 Quest 가 `CreateRoomThroughBackendAsync` 를 호출할 경로가 부재한 상태다 ([`6c78faf`](https://github.com/kookmin-sw/2026-capstone-24/commit/6c78faf), [`1ed3c94`](https://github.com/kookmin-sw/2026-capstone-24/commit/1ed3c94), [`8ca2408`](https://github.com/kookmin-sw/2026-capstone-24/commit/8ca2408) 참고). 본 plan 은 그 트리거 + 룸 목록 표시를 한 패널로 묶어 채운다.

[`04-presence-ui.md`](../specs/04-presence-ui.md) 의 What/Behavior 중 본 plan 책임:

- 로비 상태에서 admission-open 룸만 나오는 룸 목록.
- 룸 이름·비밀번호·정원 메타 옵션만 받는 룸 생성 UI (콘텐츠 옵션은 spec 상 Out of Scope — default 씬 + 기배치 오브젝트 공유).
- 입장 버튼 → 세션 상태 변경 + UI 즉시 반영 (lobby 패널 비활성 + in-room 패널 활성 토글).

In-room 측 참가자 리스트 + 퇴장 버튼은 [`2026-05-16-namae1128-presence-ui-in-room-panel`](./2026-05-16-namae1128-presence-ui-in-room-panel.md) plan 책임.

[`MultiplayerAuthGate`](../../../../Assets/Multiplayer/Scripts/Auth/MultiplayerAuthGate.cs) 가 이미 같은 씬에서 인증 후 `Multiplayer Active` 상태로 전이하므로, 로비 패널 활성화 조건은 `AuthSession.CurrentState == Authenticated` 로 한다. AccessToken 도 그쪽에서 받는다.

## Verified Structural Assumptions

- default 씬 (당시 매핑 `SampleScene`, 2026-05-16 기준) 안 `MultiplayerAuthGate` GameObject 는 World-Space Canvas 하위 (`activateButton`/`statusLabel` 필드 보유). 로비 패널 Canvas 는 그 옆 형제 GameObject 로 두고 패널 토글로 표시 — `Read Assets/Multiplayer/Scripts/Auth/MultiplayerAuthGate.cs (2026-05-16)`. **default 씬 매핑이 변경된 경우 implementer 는 현재 매핑 씬([`../decisions/01-default-scene.md`](../decisions/01-default-scene.md))에서 `MultiplayerAuthGate` 의 존재·구조를 재검증한다.**
- `RoomClient`/`RoomListQuery` 는 같은 GameObject 에 `NetworkRunner` 와 함께 부착되는 패턴 — `Read Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs (2026-05-16)` , `Read Assets/Multiplayer/Scripts/Room/Client/RoomListQuery.cs (2026-05-16)`
- `MultiplayerAuthConfig` 는 `deviceBackendBaseUrl` (Quest 빌드용) 과 `editorBackendBaseUrl` (Editor 용) 두 URL 을 보유. `BackendApiClient` 를 빌드할 때 둘 중 하나를 골라 base URL 로 사용해야 함 — `Read Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset (2026-05-16)`
- `RoomCreateRequest.maxPlayers` backend validation: `@Min(1) @Max(32)`, `photonSessionName` pattern: `^[A-Za-z0-9_\-]+$`, length ≤ 128 — `Read backend/src/main/java/com/murang/room/controller/dto/RoomCreateRequest.java (2026-05-16)`
- `RoomServerInstanceStatus` 명세 값: `PROVISIONING`/`SERVER_STARTING`/`READY`/`ACTIVE`/`UNHEALTHY`/`TERMINATING`/`TERMINATED`/`FAILED` — `Read backend/src/main/java/com/murang/room/domain/RoomServerInstanceStatus.java (2026-05-16)`

## Approach

1. **`MultiplayerLobbyPanel` MonoBehaviour 신설** (`Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs`).
   - 인스펙터 필드: `MultiplayerAuthGate authGate` (참조), `RoomClient roomClient`, `RoomListQuery roomListQuery`, `MultiplayerAuthConfig authConfig`, UI 필드 (`TMP_InputField roomNameInput`, `TMP_InputField passwordInput`, `Toggle passwordEnabledToggle`, `TMP_InputField maxPlayersInput`, `Button createButton`, `Transform roomRowParent`, `RoomRowEntry roomRowPrefab`, `TMP_Text statusLabel`, `GameObject lobbyRoot`).
   - 활성 조건: `AuthSession.CurrentState == Authenticated` 일 때만 `lobbyRoot.SetActive(true)`. 인증 전엔 비활성.
   - `Awake/OnEnable` 에서 backend base URL 결정 (`Application.isEditor ? authConfig.editorBackendBaseUrl : authConfig.deviceBackendBaseUrl`) → `BackendApiClient` → `BackendApiClientRoomAdapter` → `RoomProvisioningService` 빌드. 한 번 빌드 후 보관.
2. **룸 생성 흐름**.
   - `createButton.onClick` → `OnCreateClicked()` (async void wrapper around async Task).
   - 입력값 클라이언트-사이드 검증: 룸 이름 `^[A-Za-z0-9_\-]+$` + 1~128자, maxPlayers 1~32, passwordEnabledToggle 켜진 경우 password 비어있지 않아야 함. 실패 시 statusLabel 빨강 텍스트로 안내, backend 호출 안 함.
   - 검증 통과 → `statusLabel` 을 "Creating room…" 으로 갱신, `createButton.interactable=false`.
   - `RoomClient.CreateRoomThroughBackendAsync(options, accessToken, _provisioningService, runtimeVersion)` 호출. `accessToken` 은 `authGate` 가 보유한 `AuthSession.CurrentState.AccessToken`. `runtimeVersion` 은 `Application.version` 사용.
   - 성공: `statusLabel` 을 "Joined: <roomName>" 으로 갱신, `lobbyRoot.SetActive(false)`, in-room 패널 활성화 (event 발행 — 두 번째 plan 이 listen).
   - 실패: `RoomProvisioningFailedException`, `TimeoutException`, `ApiException` 분기별 statusLabel 메시지 (한글 OK — 폰트 atlas 이슈는 별도 plan, 본 plan 에서는 영문 메시지 또는 fallback ASCII 유지).
3. **룸 목록 표시**.
   - `roomListQuery.OnRoomListUpdated += HandleRoomListUpdated` 에 구독.
   - 핸들러는 받은 `IReadOnlyList<RoomListEntry>` 를 `roomRowPrefab` 인스턴스로 풀-회수 패턴 (Object pooling 단순 버전 — 활성 개수만큼 enable, 나머지 disable).
   - 각 row 가 `RoomRowEntry` (`Assets/Multiplayer/Scripts/Presence/RoomRowEntry.cs`) 컴포넌트로 텍스트(`<name> (<current>/<max>)`) + 잠금 아이콘 + Join 버튼 표시.
   - 빈 리스트일 땐 별도 `emptyLabel` text 가 표시되도록 분기 (인스펙터에 `TMP_Text emptyLabel` 필드 추가).
4. **룸 합류 흐름 (목록 클릭)**.
   - Row 의 Join 버튼 → `MultiplayerLobbyPanel.OnJoinClicked(RoomListEntry entry)`.
   - 잠긴 룸이면 동일 패널 안의 `passwordInput` 사용 (단순화: 별도 모달 안 만듦. 입력 비었으면 빨강 안내).
   - `roomClient.JoinRoomAsync(new RoomJoinOptions { PlayerId=..., RoomName=entry.RoomName, Password=...}, ct)` 호출. 인증된 `playerId` 는 `authGate.CurrentPlayerId` 등에서 받음 (필요 시 `MultiplayerAuthGate` 에 `CurrentPlayerId` getter 한 줄 추가).
   - 성공: lobby off, in-room 패널 활성화 event 발행.
   - 실패: statusLabel 메시지 (`RoomJoinFailureReason` 분기).
5. **로비 ↔ in-room 토글 이벤트**.
   - `MultiplayerLobbyPanel.OnEnteredRoom : event Action<string roomName>` 발행.
   - `OnLeftRoom` 은 두 번째 plan 의 in-room 패널 측 이벤트로 위임. 본 plan 에선 좌측 화살표만: in-room 측이 leave 했음을 다시 lobby 에 알리는 채널 (event 또는 직접 메서드 호출) 은 두 번째 plan 의 in-room 패널이 `MultiplayerLobbyPanel.ShowLobby()` 메서드 호출하는 식.
6. **default 씬 변경**.
   - `unity-scene-writer` 로 `MultiplayerAuthGate` Canvas 옆에 `MultiplayerLobbyPanel` GameObject + Canvas + UI 자식들 (input fields, buttons, scroll view) 배치.
   - 인스펙터 referencing 완료. 초기 `lobbyRoot.SetActive(false)`.
   - `RoomRow.prefab` 별도 prefab 생성 (`Assets/Multiplayer/Resources/RoomRow.prefab`).
7. **EditMode 테스트** (가능한 범위만).
   - `MultiplayerLobbyPanelInputValidationTests`: 입력값 검증 (룸 이름 패턴, maxPlayers 범위, password 토글 분기) 만 단위 테스트. UI 컴포넌트 의존성 없는 순수 검증 함수로 분리 (`LobbyInputValidator` static 클래스).
   - 룸 생성/합류 자체 동작은 PlayMode/실기기 검증.

## Deliverables

- `Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs` — 로비 패널 MonoBehaviour
- `Assets/Multiplayer/Scripts/Presence/RoomRowEntry.cs` — 룸 목록 행 UI 컴포넌트
- `Assets/Multiplayer/Scripts/Presence/LobbyInputValidator.cs` — 순수 검증 함수 (static)
- `Assets/Multiplayer/Resources/RoomRow.prefab` — 룸 목록 행 prefab
- default 씬 자산 ([`../decisions/01-default-scene.md`](../decisions/01-default-scene.md) 가 현재 매핑된 `.unity` 경로 박제) — `MultiplayerLobbyPanel` Canvas + 자식 UI 배치 + reference 와이어링
- `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthGate.cs` — `CurrentPlayerId` getter 추가 (필요시)
- `Assets/Multiplayer/Scripts/Room/Tests/LobbyInputValidatorTests.cs` — EditMode 검증 함수 단위 테스트

## 검증 기준

본 plan 의 acceptance 는 [`01-user-auth`](../specs/01-user-auth.md) 와 동일하게 **Quest 실기기 빌드(APK 사이드로드)** 만으로 판정한다. Unity Editor Play 환경에서만 재현되는 동작 이슈는 acceptance 위반으로 취급하지 않으며, 별도 plan 으로 분리하지 않고 무시한다. (참고: [`cd80f2b docs(specs/multiplayer-network/01-user-auth)`](https://github.com/kookmin-sw/2026-capstone-24/commit/cd80f2b).)

## Acceptance Criteria

- [ ] `[auto-hard]` `LobbyInputValidator` 단위 테스트 통과 — 룸 이름 패턴(영문/숫자/_/-/길이), maxPlayers 1~32, password 토글 시 password 비어있지 않음 분기 등 ≥ 8 케이스.
- [ ] `[auto-hard]` Unity Editor 컴파일 통과 (`Murang.Multiplayer.Presence` namespace 신설 또는 기존 asmdef 유지, 어느 쪽이든 reference 해소 깨지지 않음).
- [ ] `[manual-hard]` Quest 실기기 빌드 (aws-dev backend + Fargate room-server 작동 상태): 인증 후 로비 패널에서 룸 이름 입력 → Create → spring 로그에 `POST /api/v1/rooms`, ECS task RunTask 호출, ready callback 수신, 클라이언트가 Photon 세션에 client 모드로 합류, room server 로그에 입장 기록. ([`quest-onsite-integration-verification`](./2026-05-11-namae1128-quest-onsite-integration-verification.md) AC #5 와 같은 시나리오를 본 plan 검증으로 확정한다.)

## Out of Scope

- 참가자 리스트 / 퇴장 버튼 (in-room 패널 plan)
- 별도 비밀번호 입력 모달 (본 plan 은 lobby 의 password input 필드 공유, 추후 UX 개선 plan 에서 분리 가능)
- 보이스/텍스트 채팅
- 친구·초대
- 룸 콘텐츠 옵션 (악기/오브젝트 선택 — spec out of scope)
- 한글 폰트 atlas 깨짐 처리 (별도 plan)

## Notes

- `RoomProvisioningService` 의 `ReadyTimeout` 기본 120s 는 Fargate cold start + Unity 헤드리스 부팅 (~30~60s) + 여유를 기준. 로비 statusLabel 은 5s 단위로 카운터를 표시해 사용자가 멈춘 줄 알지 않도록 한다.
- spec 04 의 Open Questions 는 이미 닫혔지만 (현재 "_없음_"), 추후 실기기 테스트에서 `현재 인원/정원` 표시만으로 부족하다 판단되면 후속 plan 으로 분리한다.
- Editor 에서 빠른 회귀를 보고 싶을 때는 backend `MURANG_META_VERIFIER_MODE=mock` 으로 일시 토글해도 무방하지만, 그 결과는 acceptance 근거가 아니다 (검증 기준 섹션 참고).

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
