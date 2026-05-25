# Multiplayer 도메인 가이드

Meta 인증 → 백엔드 룸 provisioning → Photon Fusion 2 **dedicated-server** 합류 → 런타임 MIDI/Hand sync 까지 한 책임으로 묶는 도메인이다. Hands·Instruments·RhythmGame 도메인은 Multiplayer를 모른다 — **본 도메인이 단방향으로 그들의 이벤트를 구독하거나 컴포넌트를 spawn 한다.**

## 1. 모듈 매트릭스

| 폴더 | 책임 | 어셈블리 |
|---|---|---|
| `Scripts/Auth/` | Meta 토큰 → 백엔드 JWT 인증 게이트 | `Murang.Multiplayer.asmdef` |
| `Scripts/Backend/{Dto,Http}/` | REST DTO + HTTP 클라이언트 | 동일 |
| `Scripts/Room/{Client,Common,Server,Tests}/` | 룸 생성/합류/서버 권한. `Tests/`는 EditMode 회귀 | + `Room.Tests.asmdef` |
| `Scripts/Multiplay/` | 룸 내부 런타임 sync (MIDI/Hand) | 동일 |
| `Scripts/Presence/` | 로비/룸 UI + VR 키보드 | 동일 |
| `Scripts/Editor/` | 서버 빌드 메뉴 + 에디터 자동화 | `Murang.Multiplayer.Editor.asmdef` |

## 2. Auth 흐름

```
[Quest 부팅]
   │
   ▼
AuthBootstrap.Start()                          ─ Auth/AuthBootstrap.cs:48-52
   │ MultiplayerAuthConfig 로드, IMetaTokenProvider 선택
   ▼
AuthSession.EnsureAuthenticatedAsync()         ─ Auth/AuthSession.cs:28-55
   │ 3단계 폴링: cached AccessToken → refresh → 신규 login
   ▼
MultiplayerAuthGate                            ─ Auth/MultiplayerAuthGate.cs:69-113
   │ OnAuthenticationCompleted 발화
   ▼
MultiplayerLobbyPanel 활성화 (Presence/)
```

`IMetaTokenProvider` 구현체 2종:
- `MockMetaTokenProvider` — 설정값 기반 dummy token (개발용, 모든 플랫폼)
- `RealMetaTokenProvider` — Quest Android 전용 Oculus Platform SDK 리플렉션 호출. **에디터/PC에서는 에러를 던진다** → 에디터 작업 시 Mock 강제

`MultiplayerAuthConfig.asset` 은 `Resources/` 안. 백엔드 base URL, Mock 토큰 prefix, nickname 정규화, command-line override를 한 자리에 박제.

## 3. Room 흐름

| 단계 | 코드 |
|---|---|
| 룸 생성 요청 | `Room/Client/RoomClient.CreateRoomAsync` (`RoomClient.cs:40-50`) |
| Backend READY 폴링 | `Room/Client/RoomProvisioningService` (`:9-26`) — 실패 상태 즉시 예외, 기본 timeout 120초 |
| Fusion 합류 | `RoomConnectionTokenCodec` (token 직렬화) + `RoomJoinVerdictCodec` (verdict 부호화) |
| 룸 목록 | `Room/Client/RoomListQuery` (`:23-25`) — Fusion Lobby 합류 후 `OnSessionListUpdated` 콜백 |
| Session property 매핑 | `Room/Client/RoomListMapper` — Fusion `SessionInfo.Properties` → `RoomListEntry`. bool은 int coerce 대응 |

**테스트 mock 주입 표면**: `IRoomBackendApi` (`Room/Client/IRoomBackendApi.cs`) — `POST /api/v1/rooms`, `GET /api/v1/rooms/{id}` 두 메서드만 추상화. `BackendApiClientRoomAdapter` 가 프로덕션 구현, 테스트는 직접 mock.

서버 측 권한:
- `Room/Server/RoomAuthority.cs:36-50` — `Initialize(name, maxPlayers, passwordHash)` 후 `ValidateJoin()` 정적 메서드로 verdict 반환
- `Room/Server/RoomServerBootstrap.cs:45-50` — `RoomServerConfig` 로드, `NetworkRunner` 시작, 권한·콜백·spawner 초기화
- `Room/Server/RoomServerCallbackReporter.cs:10-26` — 부팅 후 public IP 조회 → ready callback 1회 발사 → 30초 heartbeat loop (Spring `/internal/rooms/{id}/ready`, `/heartbeat` POST)

## 4. Fusion 2 dedicated-server 토폴로지 (핵심)

**client → client 직접 RPC 가 동작하지 않는다.** 모든 client-to-client 통신은 server를 경유한다. 이는 본 프로젝트의 가장 중요한 박제 포인트다.

### MIDI relay 2단계 패턴

`Multiplay/MidiNetBus.cs:8-13` 헤더 주석 원문:

```
1) client → server : RPC_SendMidiToServer
                     (RpcSources.All, RpcTargets.StateAuthority)
2) server → all clients : RPC_RelayMidiToClients
                          (RpcSources.StateAuthority, RpcTargets.All)
```

서버는 sustained note를 player별로 추적해 leave 시 자동 cleanup.

### Hand pose sync 패턴

같은 이유로 `NetworkedWristPose` 의 `[Networked]` 값은 server만 쓸 수 있다(`StateAuthority`). client는 RPC로 wrist+25본 pose 를 server에 송신, server가 `[Networked]` 적용 → 다른 client는 보간된 값 수신.

→ **새 sync 데이터 추가 시 반드시 RPC 2단계로 설계.** client→server RPC + server→client `[Networked]` 또는 server→all RPC.

## 5. 런타임 sync 4-spawn

| 컴포넌트 | spawn/active 주체 | 트리거 | 책임 |
|---|---|---|---|
| `MidiNetBus.prefab` | server | `MidiNetBusSpawner.OnSceneLoadDone` (`:32-50`, IsServer 가드) | 룸당 1개. MIDI broadcast |
| `PlayerHandRig.prefab` | server | `PlayerHandRigSpawner.OnPlayerJoined` | player당 1개. InputAuthority=player. Left/Right `NetworkedWristPose` wrapper |
| `LocalMidiEmitter` | client (씬 배치) | 자체 Start | 씬 내 모든 `InstrumentBase.MidiTriggered` 구독 → `MidiNetBus.RPC_SendMidiToServer` |
| `LocalHandPoseSource` | client (씬 배치) | 자체 Update | 좌우 wrist + 25 본 localRotation → `RPC_PushPose` |

본 매칭 규약은 `Multiplay/RemoteHandBoneNames.cs:13-14` 에 25본 정렬 순서가 박제. Index→Middle→Ring→Little→Thumb→Palm, `L_`/`R_` 프리픽스. 본 추가 시 양 client·remote renderer prefab 모두에 반영.

## 6. `Resources/` vs `Prefabs/` 분리

| 경로 | 용도 | 로드 방식 |
|---|---|---|
| `Prefabs/MidiNetBus.prefab`, `PlayerHandRig.prefab` | Fusion이 spawn하는 NetworkObject | `.meta`에 `labels: [FusionPrefab]` → NetworkProjectConfig 자동 수집 |
| `Prefabs/{Left,Right}RemoteHandRenderer.prefab` | PlayerHandRig 자식으로만 사용 | nested prefab |
| `Resources/MultiplayerAuthConfig.asset` | 인증 설정 SO | `Resources.Load` |
| `Resources/RoomServerConfig.asset` | 서버 부팅 설정 SO + `NetworkPrefabRef` 등록 | `Resources.Load` |
| `Resources/RoomClientConfig.asset` | 클라 룸 설정 SO | `Resources.Load` |
| `Resources/ParticipantRow.prefab`, `RoomRow.prefab` | UI list item, 코드에서 동적 `Resources.Load` instantiate | `Resources.Load` |

**판단 기준**: Fusion이 NetworkRunner 시점에 spawn하는 NetworkObject → `Prefabs/` + `FusionPrefab` 라벨. 일반 SO/runtime-load asset → `Resources/`.

## 7. 서버 빌드 entry

- `Scripts/Editor/RoomServerBuildMenu.cs:20-36` — 메뉴 `Tools/Multiplayer/Build Dedicated Server (Windows|Linux)` 가 `RoomServerBoot.unity` 씬을 dedicated server 빌드.
- `tools/push-room-server-image.{ps1,sh}` — 빌드 산출물 → Docker 이미지 → ECR push → Fargate task definition 의 image tag 갱신.
- `tools/deploy-ec2-control-plane.{ps1,sh}` — control-plane EC2 인스턴스 배포.
- 운영 문서: `docs/ops/aws-dev-runbook.md`, `aws-dev-topology.md`, `cloudwatch-minimum-observability.md`.

## 8. 스모크 씬 4종 (dev-only, 빌드 제외)

`EditorBuildSettings`에서 모두 `enabled: 0`. 개발자가 수동으로 열어 개별 flow를 검증한다.

| 씬 | Probe/Bootstrap | 검증 |
|---|---|---|
| `Scenes/AuthSmokeTest.unity` | `AuthBootstrap` + `AuthSmokeProbe` (`Auth/AuthSmokeProbe.cs`) | Meta 토큰 취득 → 백엔드 로그인 |
| `Scenes/RoomClientSmokeTest.unity` | `RoomClient` + `RoomClientSmokeProbe` | backend 룸 생성/합류 → Fusion 세션 join |
| `Scenes/RoomListSmokeTest.unity` | `RoomListQuery` + `RoomListSmokeProbe` | Fusion Lobby → SessionListUpdated 콜백 |
| `Scenes/RoomServerBoot.unity` | `RoomServerBootstrap` + `RoomAuthority` + `RoomServerCallbackReporter` + spawner들 | server 부팅 → ready callback → heartbeat loop. **dedicated server 빌드 entry 씬** |

> `AuthSmokeProbe` 는 위 외에도 메인 씬(`TestSceneSanyo`)에 들어가 있다. 의도된 dev 가시화이며 release 빌드에서 시각적으로만 표시된다.

## 9. UI 구조 (Presence/)

| 컴포넌트 | 책임 |
|---|---|
| `MultiplayerLobbyPanel.cs:15-20` | 인증 후 활성화. 룸 생성·합류 UI (이름·패스워드·maxPlayers) |
| `MultiplayerInRoomPanel.cs:14-22` | `OnEnteredRoom` 시 활성화. 참가자 목록 + Leave |
| `VRWorldKeyboard.cs` | 대/소문자·숫자·Backspace·Submit·Space·Shift VR 키패드 (XRRayInteractor 호환) |
| `VRKeyboardField.cs` | TMP_InputField에 부착 → `OnPointerClick` 시 키보드 활성 |
| `LobbyInputValidator.cs` | backend와 동일 규칙 (sessionName 패턴/길이, maxPlayers 범위, 비밀번호 128자 제한) |

## 10. 더 깊이 보고 싶을 때

- MIDI relay 구현: `Scripts/Multiplay/MidiNetBus.cs`
- Hand pose [Networked]: `Scripts/Multiplay/NetworkedWristPose.cs`
- 인증 폴링 분기: `Scripts/Auth/AuthSession.cs`
- 룸 provisioning state machine: `Scripts/Room/Client/RoomProvisioningService.cs`
- 비밀번호 해싱 + 정규화: `Scripts/Room/Common/RoomPasswordHasher.cs`
- 회귀 테스트: `Scripts/Room/Tests/{LobbyInputValidator, ParticipantDisplayFormatter, RoomAuthorityValidateJoin, RoomListMapper, RoomPasswordHasher, RoomProvisioningService, RoomServerCallbackConfig}Tests.cs`
- 운영 스크립트: `tools/`, 운영 문서: `docs/ops/`
