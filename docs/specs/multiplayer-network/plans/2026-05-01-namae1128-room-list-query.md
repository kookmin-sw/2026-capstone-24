# 공개 룸 목록 조회 (잠금 표시 포함)

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Ready`

## Goal

클라이언트가 현재 열려 있는 룸 목록을 조회할 수 있는 데이터 인터페이스를 제공한다. 각 룸 항목에 정원·현재 인원·잠금 여부 플래그를 포함해 노출하고, 룸이 새로 생기거나 사라질 때 갱신 이벤트를 발화한다. UI 바인딩은 04-presence-ui spec 소관이라 데이터 노출까지만 다룬다.

## Context

`docs/specs/multiplayer-network/specs/03-room-session.md`의 What은 다음을 명시한다:

> 모든 룸은 룸 목록에 노출되고 비밀번호가 설정된 룸은 잠금 상태로 표시된다.

선행 plan `2026-05-01-namae1128-room-session-lifecycle.md`가 룸 라이프사이클을 구현하고, Photon Fusion의 `SessionInfo.Properties`에 `IsLocked` 플래그를 노출한다(키 상수는 `RoomSessionPropertyKeys.IsLocked`). 본 plan은 이 SessionInfo를 받아 클라이언트 친화적인 데이터 구조로 변환·노출하는 표면만 만든다.

Photon Fusion의 룸 목록 조회는 `INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner, List<SessionInfo>)` 콜백으로 들어온다. 이 콜백을 받기 위해 클라이언트는 Photon Lobby에 합류해야 한다(`NetworkRunner.JoinSessionLobby(...)`). 룸 합류와는 독립된 흐름이므로 별도 컴포넌트로 분리한다.

UI 표시(잠금 아이콘 시각화, 룸 클릭 시 비밀번호 입력 다이얼로그 등)는 `04-presence-ui` spec에서 다룬다.

### 핵심 결정

- **데이터 모델**: `RoomListEntry { roomName, currentPlayers, maxPlayers, isLocked }` 불변 구조체.
- **갱신 모델**: 폴링 대신 이벤트. `OnRoomListUpdated(IReadOnlyList<RoomListEntry>)` 단일 이벤트로 전체 목록 스냅샷을 매번 다시 보낸다(SessionListUpdated 콜백이 이미 전체 스냅샷을 주기 때문).
- **변환 책임 분리**: SessionInfo → RoomListEntry 매핑은 순수 함수로 분리해 단위 테스트가 가능하게 한다(Photon NetworkRunner 의존 없음).
- **Lobby 합류 시점**: `RoomListQuery` 컴포넌트가 활성화되면 자동으로 lobby 합류, 비활성화 시 lobby 이탈.

## Approach

1. **데이터 구조 정의** — `Assets/Multiplayer/Scripts/Room/Client/RoomListEntry.cs`. `readonly struct` 또는 `record` 형태로 4개 필드만.
2. **변환 함수** — `Assets/Multiplayer/Scripts/Room/Client/RoomListMapper.cs`. `static RoomListEntry FromSessionInfo(SessionInfo)`. `IsLocked`은 `Properties` 딕셔너리에서 `RoomSessionPropertyKeys.IsLocked` 키로 조회, 키 부재 시 `false`. `currentPlayers`/`maxPlayers`는 `SessionInfo.PlayerCount`/`MaxPlayers`에서 매핑.
3. **룸 목록 조회 컴포넌트** — `Assets/Multiplayer/Scripts/Room/Client/RoomListQuery.cs`. MonoBehaviour. `INetworkRunnerCallbacks` 일부 구현 또는 별도 콜백 등록. `OnEnable`에서 `NetworkRunner.JoinSessionLobby` 호출, `OnDisable`에서 lobby 이탈. `OnSessionListUpdated` 수신 시 `RoomListMapper.FromSessionInfo`로 변환해 `OnRoomListUpdated` 이벤트 발화.
4. **갱신 이벤트** — `event Action<IReadOnlyList<RoomListEntry>> OnRoomListUpdated` public 노출. 마지막 스냅샷을 `IReadOnlyList<RoomListEntry> CurrentList { get; }` 프로퍼티로도 제공해 늦게 구독한 측이 즉시 현재 상태를 읽을 수 있게.
5. **단위 테스트** — `RoomListMapper`에 대해 (a) 잠금 플래그 true/false/키부재 매핑, (b) 정원/현재 인원 매핑, (c) 룸 이름 매핑 케이스를 Edit Mode 테스트로 작성. SessionInfo는 Photon 측 mock 또는 테스트용 fake 어댑터로 주입.
6. **수동 검증 절차** — Plan 1의 Dedicated Server 환경을 재사용. 공개 룸 1개와 비밀번호 룸 1개를 띄운 상태에서 별도 클라이언트가 `RoomListQuery`만 활성화한 씬을 실행해 두 항목 모두 받는지, 잠금 플래그가 정확한지, 룸 추가/소멸 시 이벤트가 다시 오는지 확인.

## Deliverables

- `Assets/Multiplayer/Scripts/Room/Client/RoomListEntry.cs`
- `Assets/Multiplayer/Scripts/Room/Client/RoomListMapper.cs`
- `Assets/Multiplayer/Scripts/Room/Client/RoomListQuery.cs`
- `Assets/Multiplayer/Scenes/RoomListSmokeTest.unity` — `RoomListQuery`만 올린 검증용 씬
- `Assets/Multiplayer/Scripts/Room/Tests/RoomListMapperTests.cs`

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Multiplayer/Scripts/Room/Client/`의 추가 파일들이 컴파일 에러 없이 빌드된다.
- [ ] `[auto-hard]` `RoomListMapper`의 단위 테스트(잠금 플래그 true/false/키부재, 정원/현재 인원, 룸 이름 매핑)가 모두 통과한다.
- [ ] `[manual-hard]` Plan 1 Dedicated Server에 공개 룸 1개와 비밀번호 룸 1개를 띄운 상태에서 `RoomListSmokeTest` 씬을 실행하면 `OnRoomListUpdated`가 발화하고 두 항목이 모두 포함되며, 비밀번호 룸의 `isLocked == true`, 공개 룸의 `isLocked == false`다.
- [ ] `[manual-hard]` 위 상태에서 새 룸이 추가로 생성되거나 기존 룸이 소멸하면 `OnRoomListUpdated`가 다시 발화하고 갱신된 스냅샷이 전달된다.

## Out of Scope

- UI 표시(잠금 아이콘, 룸 카드, 비밀번호 입력 다이얼로그 등) — `04-presence-ui` spec 소관
- 룸 검색·필터링·정렬
- 룸 정원·잠금 외의 부가 메타(생성자, 생성 시각 등)

## Notes

- `RoomListQuery`는 룸에 합류하지 않은 상태에서만 lobby에 머문다. 룸에 합류하면 Photon Fusion 규칙상 lobby에서 자동 분리될 수 있는데, 본 plan은 "로비에서 목록 조회"만 다루므로 룸 합류 후 목록 갱신이 끊겨도 의도된 동작으로 본다(UI 측에서 룸 합류 후에는 목록을 숨기는 게 자연스러움).
- SessionInfo가 비공개 룸을 노출하지 않도록 Photon Cloud 측에서 막혀 있는지는 Plan 1의 SessionProperties 등록 방식에 의존한다. 만약 비공개 정책이 필요해지면 spec을 갱신해야 한다(현 spec은 "모든 룸 목록 노출").

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. 04-presence-ui 가 의존하는 공개 API:
- `RoomListQuery.OnRoomListUpdated` 이벤트
- `RoomListQuery.CurrentList` 프로퍼티
- `RoomListEntry { roomName, currentPlayers, maxPlayers, isLocked }`
-->
