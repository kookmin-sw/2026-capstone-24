# 룸 세션 라이프사이클 (비밀번호 옵션 포함)

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `In Progress`

## Goal

Photon Fusion Server 모드 권위 인스턴스를 부트스트랩하고, 클라이언트가 룸을 생성·입장·퇴장하는 라이프사이클을 구현한다. 정원 8명 제한, 룸 자동 소멸, 비밀번호 옵션 룸, 정원/비밀번호 거부 사유 전달까지 한 plan으로 다룬다.

## Context

`docs/specs/multiplayer-network/specs/03-room-session.md`는 다음 동작을 요구한다.

- 룸 권위는 전용 서버 인스턴스가 가진다 (Photon Fusion Server 모드).
- 클라이언트는 서버에 연결해 룸을 생성·입장·퇴장한다.
- 룸 정원은 8명. 초과 시 입장 거부 + 사유 전달.
- 마지막 유저 퇴장 시 룸 자동 소멸.
- 룸 생성 시 비밀번호 옵션을 켤 수 있고, 비밀번호 룸은 잠금 상태로 표시된다.
- 비밀번호 룸은 올바른 비밀번호로만 입장 가능하며, 불일치 시 거부 + 사유 전달.

선행 plan `2026-04-28-namae1128-photon-fusion-import.md`(Done)로 Photon Fusion SDK는 이미 임포트되어 있고 `Assets/Photon/Fusion/` 아래 런타임이 존재한다. `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`에 네트워크 프로젝트 설정이 들어 있다.

선행 plan `2026-04-29-namae1128-unity-client-auth-flow.md`(Done)로 멀티플레이어 어셈블리(`Murang.Multiplayer.asmdef`)와 `Assets/Multiplayer/Scripts/` 폴더 구조(`Auth/`, `Backend/`)가 마련되어 있다. 본 plan의 룸 코드는 같은 어셈블리 내 `Assets/Multiplayer/Scripts/Room/` 하위에 배치한다.

룸 목록 조회는 별도 plan(`2026-05-01-namae1128-room-list-query.md`)에서 다룬다. UI 바인딩은 `04-presence-ui` spec 소관으로 본 plan에서 제외한다.

### 핵심 결정

- **Photon Fusion 모드**: `GameMode.Server`. 별도 Dedicated Server 빌드가 NetworkRunner를 호스팅하고, 클라이언트는 `GameMode.Client`로 합류.
- **정원**: `StartGameArgs.PlayerCount = 8`. 초과 입장은 서버 권위가 거부.
- **비밀번호 전송**: 평문 전송 금지. 클라이언트 측에서 SHA-256 해시한 결과만 서버로 전달. 서버는 저장된 해시와 비교.
- **비밀번호 메타데이터**: `SessionInfo.Properties`에 `IsLocked`(bool) 플래그를 두어 룸 목록 plan이 잠금 여부를 읽을 수 있게 한다. 해시값은 서버 권위 객체 내부에만 보관하고 SessionInfo에는 노출하지 않는다.
- **거부 사유 전달**: `RoomJoinResult { success, reason }` 데이터 구조. `reason`은 enum(`RoomFull`, `WrongPassword`, `RoomNotFound`, `Other`). 서버 측에서 합류 거부 시 클라이언트의 `OnConnectFailed` 콜백 또는 RPC로 결과를 전달.
- **자동 소멸**: 마지막 유저 퇴장 시 서버 측 NetworkRunner가 룸 인스턴스를 종료. Photon Fusion의 `Shutdown()` 호출.
- **플레이어 식별자 매핑**: Fusion의 `PlayerRef`는 세션 수명 동안만 유효한 transport 식별자로 사용한다. 실제 유저 식별은 백엔드가 발급한 `playerId`를 기준으로 유지하고, Photon Custom Auth의 `UserId`에도 `playerId`를 전달한다. `metaAccountId`는 로그인 제공자 식별자, `nickname`은 표시용 값이며 room participant 상태나 UI는 `PlayerRef`를 영속 키로 저장하지 않는다.

## Approach

1. **룸 어셈블리 폴더 마련** — `Assets/Multiplayer/Scripts/Room/{Common,Server,Client}/` 서브폴더를 만든다. 기존 `Murang.Multiplayer.asmdef`에 Photon Fusion 어셈블리 의존성을 추가한다.
2. **공용 데이터 구조** — `RoomCreateOptions`, `RoomJoinOptions`, `RoomJoinResult`, `RoomJoinFailureReason` enum을 `Room/Common/`에 정의.
3. **비밀번호 해시 유틸** — `Room/Common/RoomPasswordHasher.cs`. 빈 문자열/null이면 잠금 없음으로 취급. 그 외에는 SHA-256 base64.
4. **서버 부트스트랩** — `Room/Server/RoomServerBootstrap.cs`. CLI 인자(`-roomName`, `-maxPlayers`, `-passwordHash`) 또는 ScriptableObject 설정에서 룸 메타를 읽어 `NetworkRunner.StartGame(GameMode.Server, ...)`를 호출. Dedicated Server 빌드의 진입 씬에서 자동 실행.
5. **서버 권위 컴포넌트** — `Room/Server/RoomAuthority.cs`. `INetworkRunnerCallbacks` 구현. `OnPlayerJoined`에서 정원 검사·비밀번호 검사 → 통과면 합류 유지, 실패면 RPC로 거부 사유 전송 후 해당 플레이어 disconnect. `OnPlayerLeft`에서 잔여 인원 0이 되면 `Runner.Shutdown()` 호출.
6. **클라이언트 룸 API** — `Room/Client/RoomClient.cs`. `CreateRoomAsync(RoomCreateOptions)`, `JoinRoomAsync(RoomJoinOptions)`, `LeaveRoomAsync()` 비동기 API 제공. 결과를 `RoomJoinResult`로 반환. 비밀번호는 진입 시 호출 측에서 해시해 전달하거나, `RoomClient` 내부에서 해시 처리 후 `SessionProperties`에 동봉.
7. **거부 사유 RPC** — `Room/Common/IRoomRejectionReceiver.cs`(클라 측 콜백 인터페이스) + 서버 측 RPC 정의. 서버가 `RoomJoinFailureReason`을 클라에 보내고 `RoomClient`는 이걸 받아 진행 중인 `JoinRoomAsync`의 결과로 변환.
8. **메타데이터 노출** — 룸 생성 시 `SessionProperties["IsLocked"] = (passwordHash != null)`로 등록. (Plan 2의 `RoomListQuery`가 이 플래그를 읽음)
9. **단위 테스트** — `RoomPasswordHasher`, `RoomAuthority.ValidateJoin`(정원/비밀번호 분기) Edit Mode 테스트 작성. Photon 의존 없는 순수 로직만 테스트.
10. **검증 환경 구성** — 로컬에서 Dedicated Server 빌드 1개와 Editor 클라이언트 2개 또는 추가 Stand-alone 클라이언트 빌드를 사용해 시나리오 수행. 검증 절차를 `Notes`에 명시.

## Deliverables

- `Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef` — Fusion 어셈블리 의존성 추가
- `Assets/Multiplayer/Scripts/Room/Common/RoomCreateOptions.cs`
- `Assets/Multiplayer/Scripts/Room/Common/RoomJoinOptions.cs`
- `Assets/Multiplayer/Scripts/Room/Common/RoomJoinResult.cs`
- `Assets/Multiplayer/Scripts/Room/Common/RoomJoinFailureReason.cs`
- `Assets/Multiplayer/Scripts/Room/Common/RoomPasswordHasher.cs`
- `Assets/Multiplayer/Scripts/Room/Common/RoomSessionPropertyKeys.cs` — `"IsLocked"` 등 키 상수
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs`
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerConfig.cs` — ScriptableObject (룸 이름·정원·비밀번호 해시)
- `Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs`
- `Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs`
- `Assets/Multiplayer/Scripts/Room/Client/RoomClientConfig.cs` — ScriptableObject (Photon AppId 환경 등)
- `Assets/Multiplayer/Resources/RoomServerConfig.asset` — Dedicated Server 빌드용 기본값
- `Assets/Multiplayer/Resources/RoomClientConfig.asset` — 클라이언트 기본값
- `Assets/Multiplayer/Scenes/RoomServerBoot.unity` — Dedicated Server 진입 씬
- `Assets/Multiplayer/Scenes/RoomClientSmokeTest.unity` — 클라이언트 검증용 씬
- `Assets/Multiplayer/Scripts/Room/Tests/RoomPasswordHasherTests.cs`
- `Assets/Multiplayer/Scripts/Room/Tests/RoomAuthorityValidateJoinTests.cs`
- `Assets/Multiplayer/Scripts/Room/Tests/Murang.Multiplayer.Room.Tests.asmdef`
- `ProjectSettings/EditorBuildSettings.asset` — `RoomServerBoot` 씬을 빌드 진입 씬으로 등록 (Dedicated Server 빌드 프로파일용)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Multiplayer/Scripts/Room/`가 컴파일 에러 없이 빌드된다.
- [ ] `[auto-hard]` `RoomPasswordHasher`의 단위 테스트(빈 입력은 null/잠금 없음, 동일 평문은 동일 해시, 다른 평문은 다른 해시)가 모두 통과한다.
- [ ] `[auto-hard]` `RoomAuthority.ValidateJoin`의 단위 테스트(정원 미달 통과, 정원 초과 → `RoomFull`, 비밀번호 일치 통과, 비밀번호 불일치 → `WrongPassword`, 잠금 없음 룸에 비밀번호 전달 시 통과)가 모두 통과한다.
- [ ] `[manual-hard]` Dedicated Server 빌드를 띄운 뒤 Editor 클라이언트 1로 룸을 생성하고, 별도 클라이언트 2가 같은 룸에 입장하면 양쪽이 같은 세션의 멤버로 인지된다.
- [ ] `[manual-hard]` 룸에 8명이 접속한 상태에서 9번째 클라이언트의 입장 시도가 거부되고 `RoomJoinResult.reason == RoomFull`이 클라이언트에 전달된다.
- [ ] `[manual-hard]` 비밀번호 옵션을 켜고 생성된 룸에 비밀번호 없이/잘못된 비밀번호로 입장 시도하면 거부되고 `RoomJoinResult.reason == WrongPassword`가 전달된다.
- [ ] `[manual-hard]` 비밀번호 룸에 올바른 비밀번호로 입장하면 합류된다.
- [ ] `[manual-hard]` 룸의 모든 클라이언트가 퇴장한 뒤 서버 로그에 `Runner.Shutdown` 호출이 확인되고, 동일한 룸 이름으로 다시 입장 시도하면 새 룸으로 인식(또는 부재 사유)된다.

## Out of Scope

- 룸 목록 조회 (`2026-05-01-namae1128-room-list-query.md`에서 다룸)
- UI 바인딩 (`04-presence-ui` spec 소관)
- 비밀번호 변경/재설정
- 호스트 마이그레이션
- 아바타·악기·음성 동기화
- Photon AppId 운영 환경 분리(현 단계는 SDK 임포트 시 등록된 개발용 AppId 그대로 사용)

## Notes

- Dedicated Server 빌드를 위해 Unity Hub에서 해당 플랫폼의 Dedicated Server 빌드 모듈이 설치되어 있어야 한다. 검증은 Windows Dedicated Server 기준으로 우선 수행한다.
- Photon Fusion의 SessionProperties는 Photon Cloud Lobby에 노출되므로, 비밀번호 해시 자체는 SessionProperties에 넣지 않는다(노출 위험). `IsLocked` 불리언만 노출하고 해시는 서버 권위 객체에서만 보관·비교한다.
- 검증 시 동일 머신에서 Dedicated Server + Editor 클라이언트 + 빌드 클라이언트 조합을 사용한다. 9명 정원 초과 검증은 헤드리스 클라이언트를 다중 인스턴스로 실행해 수행한다.
- 2026-05-01: `PlayerRef`는 세션 내부 transport 식별자로만 두고, 외부 세션 식별은 백엔드 `playerId`를 사용한다는 결정으로 상위 spec open question을 닫았다.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. 다음 plan(룸 목록 조회)이 의존하는 공개 API:
- `RoomSessionPropertyKeys.IsLocked` 상수
- `SessionInfo.Properties[IsLocked]`로 잠금 여부 노출
- `RoomClient` 인스턴스를 통한 룸 생성/입장/퇴장 진입점
-->
