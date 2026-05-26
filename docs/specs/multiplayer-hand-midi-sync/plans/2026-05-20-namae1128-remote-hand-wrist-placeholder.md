# 원격 손 wrist placeholder — 첫 합류 시각 회귀

**Linked Spec:** [`01-remote-hand-visualization.md`](../specs/01-remote-hand-visualization.md)
**Status:** `Ready`

## Goal

같은 룸에 합류한 두 클라이언트가 서로의 양손 **wrist (= 손목 1개씩, 손가락 본 미포함)** 위치·회전을 placeholder 큐브 형태로 자기 화면에서 볼 수 있게 만든다. host 가 자기 손을 흔들면 client 화면의 placeholder 가 같은 자세로 따라 움직이는 manual-hard 시각 회귀까지 도달한다.

## Context

본 sub-spec (`01-remote-hand-visualization`) 의 첫 plan. Tech Spec 의 7 컴포넌트 (`PlayerHandRig` / `NetworkedHandPose` / `LocalHandPoseSource` / `RemoteHandRenderer` / `HandNameTag` / `PlayerHandRigSpawner` + 기존 `RoomAuthority` / `PlayHand prefab`) 중 **wrist 만 + 닉네임 라벨 제외 + SkinnedMeshRenderer 본 재바인딩 제외** 의 최소 단위만 본 plan 이 책임진다.

manual-hard 검증 단위를 가장 작게 자르는 이유: ARD 01 ([decisions/01-hand-pose-network-encoding.md](../decisions/01-hand-pose-network-encoding.md)) 가 손 본 단위 quaternion 직접 송신을 결정했으므로 본 단위 26개 × 2손 의 [Networked] 필드 추가는 자명한 후속 작업이지만, 손가락 본까지 한 plan 에 묶으면 "wrist 가 어디서 끊겼는지" / "본 매칭이 어디서 깨졌는지" 진단이 섞여 PR 검토가 길어진다. 본 plan 은 "다른 사람 위치가 룸에서 보인다" 의 사회적 감각이 성립하는 가장 작은 단위만 닫는다.

본 plan 의 base 에서 multiplayer-network 04-presence-ui 의 TestSceneSanyo 마이그레이션이 이미 완료된 상태로 가정 — TestSceneSanyo 에 `RoomClient` / `MultiplayerLobbyPanel` / `MultiplayerInRoomPanel` 가 wired 되어 있다.

## Verified Structural Assumptions

- **TestSceneSanyo 의 client-side 멀티플레이어 wiring** — `RoomClient` (script GUID `c7e639ab2e4c4104dbb540b70bc77dd8`) 가 line 6413 에서, `MultiplayerLobbyPanel` (GUID `ca9c15cecfba79748aa26da982249b8d`) 가 line 623 에서, `MultiplayerInRoomPanel` (GUID `a29273255ccbe3c439c95dcb9e7af985`) 가 line 7454 에서 직렬화되어 있다. 본 plan 은 같은 씬에 `LocalHandPoseSource` 컴포넌트 1개 + `PlayerHandRig` prefab 자산 참조 (RoomServerConfig SO 안) 만 신규 wiring 한다. — `Grep TestSceneSanyo.unity (2026-05-20)`
- **RoomServerBootstrap 의 callback 등록 패턴** — `RoomServerBootstrap.EnsureRunner()` (line 86-126) 가 `_runner.AddCallbacks(_authority)`, `_runner.AddCallbacks(_automationMonitor)`, `_callbackReporter.Initialize(...)` 의 3-callback 등록 패턴을 보유. 신규 `PlayerHandRigSpawner` 도 동일 패턴으로 같은 GameObject 에 컴포넌트로 부착 + `_runner.AddCallbacks(_handRigSpawner)` 1줄 추가만 한다. RoomAuthority 본체 무수정. — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs (2026-05-20)`
- **RoomAuthority.OnPlayerJoined / OnPlayerLeft 의 server-only 가드** — `RoomAuthority.cs` line 112 (`if (!runner.IsServer) return;`) + line 153 동일 패턴으로 server build 만 callback 의 server 분기를 실행한다. 신규 `PlayerHandRigSpawner` 도 동일 가드 적용 — client build (TestSceneSanyo) 는 callback 받지만 spawn 분기 진입하지 않음. — `Read Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs (2026-05-20)`
- **PlayHand prefab 의 wrist 본 이름** — `RightPlayHand.prefab` (GUID `53319ed4d09380a4a8686d13bbd88c6f`) 의 wrist 본 이름은 `R_Wrist` (line 912), `LeftPlayHand.prefab` (GUID `91fcefda33a1e854191bc7594bc104cc`) 는 `L_Wrist` (CLAUDE.md 박제). 본 plan 의 `LocalHandPoseSource` 는 이름 매칭이 아닌 **Inspector wired Transform 참조** 로 wrist transform 을 읽으므로 본 이름 의존 0. PlayHand prefab 본체 무수정. — `Grep Assets/Hands/Prefabs/Play/RightPlayHand.prefab (2026-05-20)` + `Read Assets/Hands/CLAUDE.md (2026-05-20)`
- **asmdef reference 가용성** — 신규 C# 4종 (`PlayerHandRigSpawner`, `NetworkedWristPose`, `LocalHandPoseSource`, `PlayerHandRigBootstrap` 작성 폴더 = `Assets/Multiplayer/Scripts/Multiplay/`) 은 `Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef` 산하에 들어간다. 현 references = `["Fusion.Unity", "Unity.TextMeshPro"]`. 본 plan 의 import 는 `Fusion`, `UnityEngine` 만 — 둘 다 충족 (TextMeshPro 미사용 — 닉네임 라벨 후속 plan). asmdef reference 추가 0. — `Read Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef (2026-05-20)`
- **RoomServerConfig 의 SO 슬롯 확장 가능성** — `RoomServerConfig.cs` (line 8-61) 는 6개 `[SerializeField]` 필드 (roomName / maxPlayers / passwordHash / isVisible / customLobbyName / useDefaultPhotonCloudPorts) + 6개 public getter + `BuildSessionProperties` 메서드 만. 신규 `[SerializeField] NetworkPrefabRef playerHandRigPrefab` + `public NetworkPrefabRef PlayerHandRigPrefab` getter 1개 추가. 직렬화 호환 — 기존 `RoomServerConfig.asset` 인스턴스에 신규 field 1개 추가는 Unity 직렬화 정합 안전. — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerConfig.cs (2026-05-20)`
- **PlayHandPoseDriver 의 wrist transform 접근 경로** — `PlayHandPoseDriver.cs` (line 33-34) 의 `[SerializeField] Transform targetWristRoot` 가 `RightPlayHand/R_Wrist` (또는 `LeftPlayHand/L_Wrist`) 본 transform 의 인스턴스 참조 — TestSceneSanyo wiring 에서 line 7815-7821 의 `leftPlayHandDriver` / `rightPlayHandDriver` 가 그 컴포넌트 인스턴스를 가리킨다. `LocalHandPoseSource` 는 `[SerializeField] Transform leftWristSource` / `rightWristSource` 2개 슬롯에 동일 transform 을 직접 wiring (PlayHandPoseDriver 의존 0). PlayHandPoseDriver 의 LateUpdate 가 `targetWristRoot.position/rotation` 를 매 프레임 갱신하므로 `LocalHandPoseSource.FixedUpdateNetwork` 시점에서 안정적으로 read 가능. — `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-20)` + `Grep TestSceneSanyo.unity (2026-05-20)`

## Approach

1. **`PlayerHandRig` NetworkObject prefab 신규** (`Assets/Multiplayer/Prefabs/PlayerHandRig.prefab`)

   - Root GameObject `PlayerHandRig` + `NetworkObject` 컴포넌트 (Fusion auto-discovers, NetworkPrefabRef 자동 등록).
   - 자식 2개: `LeftHand` / `RightHand`. 각 자식은 `NetworkedWristPose` 컴포넌트 + 시각용 placeholder 큐브 (`MeshFilter` + `MeshRenderer`, scale `(0.05, 0.05, 0.05)`, Cube primitive 메시) 1개. **`Collider` / `Rigidbody` 일체 부착 금지** (Tech Spec Invariant: 원격 손 충돌 0건).
   - 자식 큐브의 material 은 빨강 / 파랑 placeholder material 1개 (`Assets/Multiplayer/Materials/RemoteHandPlaceholder.mat` 신규). 후속 plan 에서 SkinnedMeshRenderer 본 재바인딩 시 교체 예정.

2. **`NetworkedWristPose` NetworkBehaviour 신규** (`Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs`)

   - `Fusion.NetworkBehaviour` 상속.
   - 2개 `[Networked]` 필드: `public Vector3 WristPosition { get; set; }` + `public Quaternion WristRotation { get; set; }`. Fusion 자동 보간 활용 (ARD 01 의 단순성 우선 결정 그대로).
   - `public override void Render()` 에서 `transform.SetPositionAndRotation(WristPosition, WristRotation)` — 모든 클라이언트 (input/state/non-authority 무관) 가 보간된 [Networked] 값을 자기 transform 에 매 프레임 적용.
   - **본 plan 은 손가락 본 25개 localRotation [Networked] 필드 미포함** — 후속 plan 책임. 본 단위 quaternion 추가 시 ARD 01 의 대역폭 측정 AC 가 자연스러운 위치.

3. **`LocalHandPoseSource` MonoBehaviour 신규** (`Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs`)

   - `[SerializeField] Transform leftWristSource` / `rightWristSource` — TestSceneSanyo 의 `LeftPlayHand/L_Wrist` / `RightPlayHand/R_Wrist` 인스턴스를 Inspector 에서 직접 wiring.
   - `RoomClient` 가 가진 `NetworkRunner` 의 `LocalPlayer` 가 자기 `PlayerHandRig` 의 `InputAuthority` 와 일치하는지 매 틱 확인 — 일치하는 `PlayerHandRig` 1개를 캐싱.
   - `LateUpdate` 에서 자기 `PlayerHandRig.LeftHand.NetworkedWristPose.WristPosition/Rotation` 에 `leftWristSource.position/rotation` 을 write (right 동일). 다른 사람 `PlayerHandRig` 에는 write 하지 않음 (InputAuthority 가드).
   - PlayerHandRig 미발견 (룸 미합류 또는 spawn 직전) 시 silent skip.

4. **`PlayerHandRigSpawner` MonoBehaviour 신규 + INetworkRunnerCallbacks** (`Assets/Multiplayer/Scripts/Multiplay/PlayerHandRigSpawner.cs`)

   - `MonoBehaviour, INetworkRunnerCallbacks` 상속. `[SerializeField] RoomServerConfig config` (또는 직접 `NetworkPrefabRef`). `RoomServerBootstrap` 과 같은 GameObject 에 부착.
   - `OnPlayerJoined(runner, player)` — `if (!runner.IsServer) return;` 가드 후 `runner.Spawn(config.PlayerHandRigPrefab, position: Vector3.zero, rotation: Quaternion.identity, inputAuthority: player)` 호출 + `Dictionary<PlayerRef, NetworkObject>` 에 보관.
   - `OnPlayerLeft(runner, player)` — server-only 가드 후 보관된 `NetworkObject` 를 `runner.Despawn(...)`.
   - 다른 `INetworkRunnerCallbacks` 메서드 13개는 empty body.

5. **`RoomServerBootstrap` 의 spawner 등록 1줄 추가** (`Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs`)

   - `EnsureRunner` 안에 `_handRigSpawner = GetComponent<PlayerHandRigSpawner>() ?? gameObject.AddComponent<PlayerHandRigSpawner>(); _handRigSpawner.Initialize(config); _runner.RemoveCallbacks(_handRigSpawner); _runner.AddCallbacks(_handRigSpawner);` 3-4줄 추가. 기존 `_authority` / `_automationMonitor` / `_callbackReporter` 와 동일 패턴.
   - `private PlayerHandRigSpawner _handRigSpawner;` 필드 1개 추가.

6. **`RoomServerConfig.cs` 에 prefab 슬롯 1개 추가** (`Assets/Multiplayer/Scripts/Room/Server/RoomServerConfig.cs`)

   - `[SerializeField] private NetworkPrefabRef playerHandRigPrefab;` + `public NetworkPrefabRef PlayerHandRigPrefab => playerHandRigPrefab;` 2줄 추가.
   - 결정 사유 (Notes 참조): RoomServerBootstrap 에 직접 prefab field 를 두는 대안보다 SO 슬롯이 자연스럽다 — config 는 이미 6개 [SerializeField] 의 SO 단일 진실원이고, server build 의 prefab 슬롯은 build-time 결정이라 SO 의 본성과 일치한다. RoomServerConfig.asset 인스턴스에 prefab GUID 1개 추가는 직렬화 hazard 없음.

7. **TestSceneSanyo wiring** (client-side, `Assets/Scenes/TestSceneSanyo.unity`)

   - 새 GameObject `LocalHandPoseSource` 1개 추가, `LocalHandPoseSource` 컴포넌트 부착, `leftWristSource` / `rightWristSource` 슬롯에 기존 씬 안의 `LeftPlayHand/L_Wrist` / `RightPlayHand/R_Wrist` 본 transform 을 Inspector 에서 wiring. **server scene (별도 build) 측의 wiring (RoomServerConfig.asset 의 PlayerHandRigPrefab 슬롯 채움) 은 본 plan Deliverables 의 별도 단계.**

8. **inputAuthority placeholder hide** — `NetworkedWristPose.Spawned()` (Fusion lifecycle) 에서 `if (Object.HasInputAuthority) gameObject.SetActive(false);` 또는 자식 placeholder MeshRenderer.enabled = false. **자기 PlayHand 와 자기 PlayerHandRig 가 같은 화면에 겹쳐 보이지 않음을 보장** (Tech Spec Invariant 3). placeholder 가 비활성이어도 [Networked] write 는 자기 GameObject 가 살아있을 때만 가능 — 위 방식 중 자식 MeshRenderer.enabled = false 가 안전 (gameObject 자체는 활성 유지).

9. **컴파일 + 직렬화 검증** — Unity Editor 로드 → `read_console types=["error"]` 0건 → PlayerHandRig.prefab YAML grep 으로 NetworkObject 직렬화 정합 + Collider 0건 확인.

10. **manual-hard 듀얼 클라이언트 회귀** — host = Editor Play (TestSceneSanyo) + 별도 client = standalone build (TestSceneSanyo) + server = local dedicated server build (`-batchmode -nographics`) 또는 aws-dev mock. host 가 자기 손을 흔들 때 client 화면의 빨강/파랑 큐브 2개가 동일 자세로 따라 움직임을 시각 회귀.

## Deliverables

- `Assets/Multiplayer/Prefabs/PlayerHandRig.prefab` — NetworkObject + 자식 2개 (LeftHand/RightHand) + 각 자식의 `NetworkedWristPose` + placeholder Cube MeshFilter/MeshRenderer. Collider/Rigidbody 0건.
- `Assets/Multiplayer/Materials/RemoteHandPlaceholder.mat` — placeholder 큐브 material (빨강 또는 파랑 단색).
- `Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs` — NetworkBehaviour, [Networked] Vector3 + Quaternion 1쌍, Render() 적용, Spawned() hide-self-if-input-authority.
- `Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs` — MonoBehaviour, LateUpdate 에서 자기 PlayerHandRig 의 wrist [Networked] 필드에 wrist transform 을 write.
- `Assets/Multiplayer/Scripts/Multiplay/PlayerHandRigSpawner.cs` — INetworkRunnerCallbacks, OnPlayerJoined → Spawn / OnPlayerLeft → Despawn (server-only).
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerConfig.cs` — `playerHandRigPrefab` [SerializeField] + getter 추가.
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs` — `_handRigSpawner` 필드 + `EnsureRunner` 의 spawner 등록 4줄 추가.
- `Assets/Scenes/TestSceneSanyo.unity` — `LocalHandPoseSource` GameObject + 컴포넌트 신설 + `leftWristSource` / `rightWristSource` wiring.
- `Assets/Multiplayer/Resources/RoomServerConfig.asset` — `playerHandRigPrefab` 슬롯에 `PlayerHandRig.prefab` GUID 박제.

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Multiplayer/Prefabs/PlayerHandRig.prefab` 가 존재하고, `NetworkObject` 컴포넌트 1개 + 자식 2개 (LeftHand / RightHand) + 각 자식의 `NetworkedWristPose` 컴포넌트 + MeshFilter/MeshRenderer 1쌍이 직렬화돼 있다. Collider / Rigidbody 컴포넌트 매치 0건.
  **검증:** `Grep -P "Collider|Rigidbody" Assets/Multiplayer/Prefabs/PlayerHandRig.prefab` 결과 0 매치 + `Grep "NetworkObject|NetworkedWristPose|MeshRenderer|MeshFilter" Assets/Multiplayer/Prefabs/PlayerHandRig.prefab` 각각 ≥ 1 매치.
- [ ] `[auto-hard]` `NetworkedWristPose.cs` 의 [Networked] 필드 정확히 2개 (`WristPosition` Vector3 + `WristRotation` Quaternion). 손가락 본 [Networked] 필드 0건 (후속 plan 미루기 확인).
  **검증:** `Grep "^\s*\[Networked\]\s+public" Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs` 결과 = 2 매치 (선언 라인만, 주석 false positive 회피).
- [ ] `[auto-hard]` `PlayerHandRigSpawner.OnPlayerJoined` / `OnPlayerLeft` 가 `if (!runner.IsServer) return;` 가드를 모두 보유 (client build 가 spawn/despawn 분기에 진입하지 않음).
  **검증:** `Grep -A 3 "OnPlayerJoined|OnPlayerLeft" Assets/Multiplayer/Scripts/Multiplay/PlayerHandRigSpawner.cs` 결과에 `runner.IsServer` 가드 2건 매치.
- [ ] `[auto-hard]` `RoomServerConfig.cs` 에 `playerHandRigPrefab` 필드 + `PlayerHandRigPrefab` getter 추가. `RoomServerBootstrap.EnsureRunner` 에 `_handRigSpawner` AddCallbacks 호출 추가.
  **검증:** `Grep "playerHandRigPrefab" Assets/Multiplayer/Scripts/Room/Server/RoomServerConfig.cs` ≥ 2 매치 + `Grep "_handRigSpawner|AddCallbacks\(_handRigSpawner\)" Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs` ≥ 2 매치.
- [ ] `[auto-hard]` TestSceneSanyo 에 `LocalHandPoseSource` 컴포넌트가 직렬화돼 있고, wrist transform 도달 보장 — `leftWristSource`/`rightWristSource` 가 Inspector wired (fileID non-zero) **또는** `LocalHandPoseSource.cs` 의 `EnsureWristSources()` 자동 fallback (GameObject.Find) 코드 존재 (둘 중 하나로 의도 만족).
  **검증:** `Grep "LocalHandPoseSource" Assets/Scenes/TestSceneSanyo.unity` ≥ 1 매치 + (`Grep "EnsureWristSources" Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs` ≥ 1 매치 **또는** `Grep -A 1 "leftWristSource:|rightWristSource:" Assets/Scenes/TestSceneSanyo.unity` 의 fileID 가 둘 다 0 이 아님).
- [ ] `[auto-hard]` Unity Editor 로드 + TestSceneSanyo 진입 시 콘솔 에러·예외 0건. (asmdef 의존 누락 / NetworkObject 직렬화 미스 / 컴파일 에러 0)
  **검증:** Unity MCP `read_console types=["error"]` 호출 결과 0 매치 ([`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md) 절차 그대로).
- [ ] `[auto-hard]` `Assets/Multiplayer/Resources/RoomServerConfig.asset` 의 `playerHandRigPrefab` 슬롯이 PlayerHandRig.prefab GUID 로 wiring.
  **검증:** `Grep "playerHandRigPrefab" Assets/Multiplayer/Resources/RoomServerConfig.asset` 결과에 `Assets/Multiplayer/Prefabs/PlayerHandRig.prefab.meta` 의 guid 와 동일한 `guid:` 매치 1건.
- [ ] `[manual-hard]` Unity Editor (host = Play 모드, TestSceneSanyo) + standalone build (client, TestSceneSanyo) + local dedicated server build (`-batchmode -nographics`) 의 3-process 듀얼 클라이언트 환경에서 두 클라이언트가 같은 룸에 합류한 직후, host 가 양손을 좌·우·앞·뒤로 흔들면 client 화면에서 빨강·파랑 placeholder 큐브 2개가 동일 자세로 ≤ 200ms 지연 안에 따라 움직인다. host 자기 placeholder 는 host 화면에 보이지 않는다. host 가 룸을 떠나면 client 화면의 두 큐브가 즉시 사라진다.
  **검증:** 시각 회귀 — host 가 손을 흔드는 동안 client 화면에서 큐브 2개가 host 손과 동일 자세 추종 + host Leave 직후 큐브 사라짐 + host 화면 자기 placeholder 없음.

## Out of Scope

- 손가락 본 25개 × 2손 [Networked] 송수신 (후속 plan — ARD 01 의 대역폭 측정 AC 도 그 plan 에서).
- RemoteHandRenderer 의 SkinnedMeshRenderer 본 재바인딩 + PlayHand 본 계층 재사용 (후속 plan — placeholder 큐브를 hand mesh 로 교체).
- HandNameTag 닉네임 라벨 worldspace 텍스트 (후속 plan — multiplayer-network 04-presence-ui 의 표시명 채널 재활용).
- 손이 잡고 있는 도구 (drum stick 등) 의 별도 동기화 (sub-spec out-of-scope 박제 그대로 — grip 자세가 보이면 충분).
- 네트워크 재연결 후 손 pose 복원 (sub-spec out-of-scope).
- 타이밍 보정 / 예측 / 지연 보상 (sub-spec out-of-scope — Fusion 기본 보간만).
- 본 단위 대역폭 측정 AC (ARD 01 의 Consequences — 본 plan 의 wrist-only 단계에서는 본 단위 quaternion 부재라 의미 없음; 손가락 본 추가 plan 의 책임).
- `RoomAuthority.cs` 본체 수정 (Tech Spec Boundary — 별도 callback 등록 패턴 그대로).
- `PlayHand` prefab 본체 수정 (Tech Spec Boundary — 읽기만).
- `InstrumentBase` / 자식 악기 일체 (Tech Spec Boundary).

## Notes

- **PlayerHandRig prefab 슬롯 위치 결정**: RoomServerBootstrap MonoBehaviour 의 inline `[SerializeField]` vs RoomServerConfig SO 의 `[SerializeField]` 둘 다 가능. **SO 슬롯 선택 사유** — config 는 이미 6개 build-time 결정 필드의 단일 진실원 SO 고, prefab reference 도 server build 시 결정되는 build-time fact 이므로 SO 의 본성에 일치. MonoBehaviour 슬롯은 씬 인스턴스 단위 override 가 가능해 multi-scene server 시나리오에서 의도치 않은 발산이 일어날 수 있어 회피.
- **placeholder 큐브 vs 본 단위 손 mesh** — 본 plan 은 wrist 회귀 시각화의 최소 manual-hard 만 노린다. 큐브 → 손 mesh 전환은 본 단위 송수신 plan 과 SkinnedMeshRenderer 본 재바인딩 plan 의 자연스러운 다음 단계.
- **ARD 01 의 대역폭 측정 AC 위치** — 본 plan 의 wire payload 는 wrist 1개 × 2손 = `(Vector3 + Quaternion) × 2 = 56 bytes` 수준이라 ARD 의 "룸 정원 가정 대역폭 측정" 임계 평가가 의미 없다. 손가락 본 25개 × 2 = 50개 quaternion (`16 bytes × 50 = 800 bytes`) 추가 plan 이 그 AC 의 자연스러운 위치.
- **inputAuthority hide 방식 trade-off** — `gameObject.SetActive(false)` 는 [Networked] 보간 자체를 멈출 수 있어 회피, 자식 MeshRenderer.enabled = false 가 안전 (state 컨테이너는 활성 유지, 시각만 hide).
- **server-side 의 PlayerHandRig 렌더** — dedicated server build 는 `-batchmode -nographics` + OpenXR loader 비활성 (`docs/specs/_archive/multiplayer-network/plans/2026-05-09-namae1128-dedicated-server-build-openxr-toggle.md`). PlayerHandRig 의 MeshRenderer 는 server 에서 렌더 대상 0건이고 NetworkObject 상태 컨테이너로만 동작.
- **자동 wiring fallback 도입 사유 (2026-05-20)** — MCP `manage_components.set_property` 가 GameObject instanceID → component (Transform/NetworkBehaviour) 자동 변환을 처리 못 함. Inspector wiring 의존을 줄이고 자동화 친화성을 높이기 위해 두 군데에 fallback 추가: (1) `PlayerHandRig.LeftHand`/`RightHand` getter 가 `transform.Find("LeftHand"/"RightHand").GetComponent<NetworkedWristPose>()` 로 자동 조회. (2) `LocalHandPoseSource.EnsureWristSources()` 가 LateUpdate 첫 호출 시 `GameObject.Find("LeftPlayHand"/"RightPlayHand")` 로 자동 wiring. Inspector slot 도 보존 — wired 면 우선 사용. AC #5 evidence 도 fallback 인정으로 약화.
- **손 authority 경로 RPC 재설계 (2026-05-21, manual-hard 2차 시도 직전 사용자 진단)** — dedicated-server 모델 (`GameMode.Server` 가 spawn, client 는 `GameMode.Client` + InputAuthority) 에서 `[Networked]` state 변경은 **StateAuthority (server) 만 가능**. 1차 구현의 `LocalHandPoseSource.WriteWristPose` 가 client 측에서 `_cachedRig.LeftHand.WristPosition = pos;` 같이 [Networked] 필드를 직접 set 하는 패턴은 Fusion 의 권한 모델 위반 — server 가 다음 tick 에 덮어쓰거나 무시. 수정: `NetworkedWristPose` 에 `[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)] void RPC_PushPose(Vector3, Quaternion)` 추가, `LocalHandPoseSource.WriteWristPose` 가 RPC 호출로 변경. server 가 RPC 본문에서 `WristPosition = pos; WristRotation = rot;` 적용. client 의 `Render()` 가 보간된 [Networked] 값을 자기 transform 에 그림. weaving 누락 fix + 본 authority 재설계 둘 다 적용 후 manual-hard 재검증.
- **NetworkProjectConfig AssembliesToWeave 누락 발견 (2026-05-20, manual-hard 1차 시도 후)** — 1차 manual-hard 시도에서 client #2 가 룸에 합류해도 host 측 placeholder 큐브가 안 보임 (server log 에 `PlayerHandRigSpawner` 호출 흔적 없음, Fusion 정상 ready). 원인: `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` 의 `AssembliesToWeave` 에 `Murang.Multiplayer` 가 누락. Fusion source weaving 은 `[Networked]` properties / `NetworkBehaviour` IL 을 컴파일 시점에 변환하는데, weave 대상 asmdef 가 아니면 `[Networked]` 가 평범한 auto-property 가 되고 NetworkBehaviour 인식도 깨진다. 기존 `RoomClient`/`RoomAuthority`/`RoomServerBootstrap` 은 `MonoBehaviour, INetworkRunnerCallbacks` (callback 만 사용, `[Networked]` 없음) 라 weave 불요였고 정상 동작. 본 plan 이 `Murang.Multiplayer` 에 처음 `NetworkBehaviour` + `[Networked]` 를 도입해 누락이 드러남. 수정: NetworkProjectConfig.fusion 의 AssembliesToWeave 에 `"Murang.Multiplayer"` 1줄 추가. 또 진단 로그 (`PlayerHandRigSpawner.OnPlayerJoined/Left` 의 Debug.Log) 를 명시적으로 추가해 다음 사이클의 CloudWatch 에서 spawn 호출 여부·prefab 유효성·spawn 결과를 즉시 확인 가능하도록 강화. 후속 sub-spec 02 (MIDI) 가 같은 namespace 에 `NetworkBehaviour` (MidiNetBus) 를 도입할 때 본 사실이 Verified Structural Assumption 으로 박제되어야 한다.

## Handoff

_본 plan 완료 시 doc-updater 가 갱신._
