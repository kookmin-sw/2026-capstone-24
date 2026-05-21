# Presence UI 후속 — Leave 후 reference 살리기 + stale room 정리 + MaxPlayers 카운트 보정

**Linked Spec:** [`04-presence-ui.md`](../specs/04-presence-ui.md)
**Caused By:** [`2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md`](./2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md)
**Status:** `Ready`

## Goal

직전 lobby-migration-and-ux plan(Done) 의 end-to-end manual-hard 통과 직후 2명 e2e 테스트(2026-05-19)에서 발견된 minor issue 3건을 해소해 "Leave → 다시 Create" 와 "정원 표시" 사용자 경험을 정상화한다. 본 plan 적용 후 사용자는 (1) InRoomPanel 의 LeaveButton 을 눌러 LobbyPanel 로 돌아온 뒤 곧장 새 룸을 생성·합류할 수 있고, (2) RoomList 가 자신이 떠난 빈 룸을 더 이상 admission-open 으로 광고하지 않으며, (3) InRoomPanel 의 참가자 카운트 가 입력한 정원과 일치한다.

## Context

### 직전 plan Handoff trace (2026-05-19 박제)

[`2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md`](./2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md) Handoff "남은 minor issues" 4건 중 본 plan 이 묶어 다루는 3건:

1. **Issue A — Leave 후 RoomClient reference missing (Critical)**: InRoomPanel 의 LeaveButton → `RoomClient.LeaveRoomAsync()` → `_runner.Shutdown()` (default `destroyGameObject: true`) → `MultiplayerRoomNetworking` GameObject 자체가 destroy → 같은 GameObject 의 RoomClient/RoomListQuery component 도 함께 destroy → LobbyPanel.roomClient SerializeField 가 Missing → 다음 CreateButton 누르면 `IsReadyForBackendCall` 의 `roomClient == null` 분기로 "Failed: RoomClient reference is missing." 발화. **2명 e2e 테스트 (2026-05-19) 에서 재현됨**.

2. **Issue B — Leave 후 stale room 목록 (Medium)**: 본인이 떠난 후에도 빈 룸이 RoomListQuery 결과에 그대로 표시 + 인원수 갱신 안 됨. backend `RoomServerInstance.status` 가 ACTIVE 유지 (DS 가 살아있는 한). spec [`05-room-server-manager.md`](../specs/05-room-server-manager.md) Behavior 는 "room instance가 마지막 유저 퇴장 ... 종료 절차" 를 약속하지만 실제 e2e 에서 동작하지 않음. **2명 e2e 테스트 (2026-05-19) 에서 재현됨**.

3. **Issue C — MaxPlayers PlayerCount off-by-one (Medium)**: 사용자 입력 정원이 InRoomPanel 에 `(현재인원)/(입력정원+1)` 로 표시 (입력 4 → 1/5, 입력 2 → 1/3). Photon Fusion `SessionInfo.MaxPlayers` 가 server slot 1을 추가로 카운트할 가능성. 본 plan 영향 범위 내.

### Out of Scope 분리 사유

남은 minor issue 4번 (**passwordHash env var 누락** — backend `EcsRoomRuntimeProvider.buildEnvironment` 가 password hash 를 ECS env 에 안 보냄 → DS 가 잠금 룸 password check 불가) 은 본 plan 의 Out of Scope. 잠금 룸 e2e 검증 인프라(2명이 같은 잠금 룸에 합류) 가 별도 cycle 이고, fix 대상이 본 plan 의 client/DS 가 아닌 backend Java 코드라 commit·rollback 단위가 분리되는 것이 위험 격리상 유리. 별도 후속 plan 후보.

### Decision 정렬

[`decisions/01-default-scene.md`](../decisions/01-default-scene.md) (default = TestSceneSanyo) — 본 plan 은 default 씬 추상 표기만 사용. `TestSceneSanyo` 직접 박제는 코드 변경 결과 검증 단계에서만.

## Verified Structural Assumptions

### Issue A — Shutdown destroyGameObject default semantics

[`RoomClient.cs`](../../../../Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs) `Read (2026-05-19)`:

- **line 125-139** `LeaveRoomAsync()`: pending join 정리 후 `_runner.IsRunning` 이면 `await _runner.Shutdown();` 호출. parameter 0개 → Fusion 의 default `destroyGameObject: true` 적용.
- **line 368-374** `ShutdownRunnerIfRunningAsync()` (private helper): 동일하게 `await _runner.Shutdown();` parameter 0개. `JoinSessionAsync` 의 failure 경로 (line 221, 236) 에서 호출.

[`RoomListQuery.cs`](../../../../Assets/Multiplayer/Scripts/Room/Client/RoomListQuery.cs) `Read (2026-05-19)`:

- **line 28-34** `OnDisable()`: `_runner.IsRunning` 이면 `await _runner.Shutdown();` 호출. parameter 0개. RoomListQuery 가 분리된 GameObject 라면 LobbyPanel 마이그레이션 후 별도 GameObject 일 가능성도 있으나, 직전 plan Handoff 의 trace ("`MultiplayerRoomNetworking` GameObject 가 destroy → RoomClient/RoomListQuery 도 함께 destroy") 는 RoomClient/RoomListQuery/RoomServerCallbackReporter 등이 모두 같은 GameObject 에 부착돼 있음을 함의.

**Photon Fusion `NetworkRunner.Shutdown` signature**: Fusion 2 의 NetworkRunner 는 DLL 형태(`Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll`) 로 제공되어 소스 직접 Grep 불가. 그러나 Fusion 2 공식 doc + 동일 코드베이스의 e2e 재현 trace 에 따르면 `Shutdown(bool destroyGameObject = true, ShutdownReason shutdownReason = ShutdownReason.Ok, ...)` 형태로 첫 parameter 가 `destroyGameObject` 이며 default `true`. 본 plan implementer 는 fix 적용 후 `NetworkRunner.Shutdown(destroyGameObject: false)` 가 컴파일 통과하는지로 signature 일치를 1차 검증한다. — `Fusion 2 doc + 직전 plan Handoff issue 3 trace 박제 (2026-05-19)`

### Issue B — DS 측 last-player terminate 흐름 (이미 구현됨, 동작 검증 필요)

[`RoomAuthority.cs`](../../../../Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs) `Read (2026-05-19)` — **놀랍게도 이미 구현돼 있음**:

- **line 141-160** `public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)`: `runner.IsServer && !runner.ActivePlayers.Any()` 이면 `RoomServerCallbackReporter.ReportTerminated("last-player-left")` coroutine + `runner.Shutdown()`.
- **line 162-167** `TerminateAndShutdownRoutine`: `yield return reporter.ReportTerminated(reason)` → `_ = runner.Shutdown();` (역시 parameter 0개 — 단 DS process 자체 종료가 목적이라 `destroyGameObject` 영향은 무의미).

[`RoomServerBootstrap.cs`](../../../../Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs) `Read (2026-05-19)`:
- **line 90-98** `EnsureRunner`: RoomAuthority 를 NetworkRunner 콜백으로 `AddCallbacks(_authority)` 등록.

[`RoomServerCallbackReporter.cs`](../../../../Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs) `Read (2026-05-19)`:
- **line 74-106** `ReportTerminated(reason)` IEnumerator: heartbeat coroutine 중단 + `POST {TerminateCallbackUrl}` (body=`{"reason":"..."}`). UnityWebRequest timeout 5s. log "terminate POST succeeded/failed".

**즉 코드는 존재하나 e2e 에서는 실제 stale room 이 남았다**. 가능 원인 가설 (implementer 가 e2e 로그 + Photon `OnPlayerLeft` 시점 `ActivePlayers` 값 확인 후 fix 방향 결정):

- **가설 H1**: `OnPlayerLeft` 는 implicit public method 이고 explicit interface `void INetworkRunnerCallbacks.OnPlayerLeft` 가 별도 line 440-442 `RoomClient.cs` 처럼 빈 구현으로 박혀 있어 Fusion 이 explicit 쪽을 호출 → `RoomAuthority.OnPlayerLeft` 가 안 불림. RoomAuthority 자체엔 explicit 빈 구현이 없으므로 implicit method 가 unique → 호출됨이 정상. **단** Fusion 의 callback dispatch 가 explicit interface 구현만 호출하는 정책이면 이 가설이 성립. 검증: implementer 가 DS CloudWatch log 에 `Debug.Log` 1줄 추가해 `OnPlayerLeft` 진입 여부 확인.
- **가설 H2**: `OnPlayerLeft` 가 호출되긴 하지만 `ActivePlayers.Any()` 가 client 가 disconnect 처리 완료 전 시점 → count 1 → terminate 분기 미진입. 검증: log + `ActivePlayers.Count()` 출력.
- **가설 H3**: `ReportTerminated` POST 는 발사됐으나 backend 가 status=TERMINATED 처리 후에도 RoomListQuery 의 Photon 측 session list 가 여전히 ACTIVE (DS process 종료가 비동기) → Photon 의 sessionList 캐시 정리 시간이 필요. **이 경우 본 plan 의 scope 가 아니라 polling/reconciliation 쪽** — 일단 H1/H2 확인 후 그래도 stale 이면 H3 으로 escalation.

— `Read Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs (2026-05-19)`, `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs (2026-05-19)`, `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs (2026-05-19)`

### Issue C — DS 측 PlayerCount 설정 위치

[`RoomServerBootstrap.cs`](../../../../Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs) `Read (2026-05-19)`:
- **line 56-67** `StartGameArgs`: `PlayerCount = maxPlayers` (line 60). `maxPlayers` 는 `ResolveMaxPlayers()` 결과 (`-maxPlayers` CLI arg → `MAX_PLAYERS` env → `config.MaxPlayers` fallback). server slot 별도 처리 없음.

[`RoomClient.cs`](../../../../Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs) `Read (2026-05-19)`:
- **line 241-269** `CreateStartGameArgs`: client side 도 `PlayerCount = Mathf.Max(1, maxPlayers ?? 8)` (line 262, `allowClientSessionCreation=true` 분기). client-host 모드에서만 적용되므로 backend-mediated 룸 생성 경로에서는 무관 (`CreateRoomThroughBackendAsync` 가 `JoinSessionAsync(..., allowClientSessionCreation: false)` 로 들어가 SessionProperties/PlayerCount 미설정).

**Photon Fusion `SessionInfo.MaxPlayers` semantic**: Fusion 2 의 dedicated-server mode 는 server-only slot 을 별도로 가지지 않고 `PlayerCount` 가 곧 client-facing max 와 동일하다는 doc 도 있고, 반대로 +1 carve-out 한다는 trace 도 있음. 일관되게 +1 재현된 실측 사실 (입력 4 → 1/5, 입력 2 → 1/3, Handoff 박제) 이 우선. implementer 가 다음 중 하나 채택:

- **Option α (DS-side carve-out)**: `RoomServerBootstrap.cs` line 60 `PlayerCount = maxPlayers + 1` 로 송신. client 측 표시 시엔 `SessionInfo.MaxPlayers - 1` 표시. 양쪽 둘 다 변경 필요 (DS + InRoomPanel UI binder).
- **Option β (client-side display-only)**: DS line 60 `PlayerCount = maxPlayers` 유지. InRoomPanel 의 `UpdateParticipantCount` 측에서 `SessionInfo.MaxPlayers - 1` 로 표시. user-facing 가독성만 보정.
- **Option γ (Photon API 정확한 semantic 확립 후 결정)**: implementer 가 Fusion 2 doc 또는 실험으로 `PlayerCount` ↔ `SessionInfo.MaxPlayers` 매핑을 확정한 후 1회만 변경.

**권장 = Option γ → α 또는 β**. 단순 -1 만 박는 Option β 는 향후 Fusion semantic 이 바뀌면 깨질 수 있으므로 Option α 가 명시적 carve-out 으로 더 안전하지만, e2e 가 더 무겁다. implementer 판단.

— `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs (2026-05-19)`, `Read Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs (2026-05-19)`, Handoff 재현 trace 박제 (2026-05-19)

### asmdef 정렬

- 본 plan 의 모든 .cs 변경 대상은 `Assets/Multiplayer/Scripts/` 단일 asmdef `Murang.Multiplayer.asmdef` 산하 (Editor / Tests asmdef 와 별개). 신규 asmdef reference 불필요. — `Read Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef (2026-05-19)`

## Approach

3 phase 로 분리. 각 phase 는 commit 단위가 별개여도 무방하나 atomic 으로 묶어도 무방. e2e 검증은 phase 별로 한 사이클씩 가능.

### Phase A — Shutdown destroyGameObject:false 명시 (Issue A fix)

[`RoomClient.cs`](../../../../Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs) 와 [`RoomListQuery.cs`](../../../../Assets/Multiplayer/Scripts/Room/Client/RoomListQuery.cs) 의 client-side `_runner.Shutdown()` 호출 3건을 `_runner.Shutdown(destroyGameObject: false)` 로 변경:

1. `RoomClient.cs:137` (`LeaveRoomAsync` 안) — 본 issue 의 root cause.
2. `RoomClient.cs:372` (`ShutdownRunnerIfRunningAsync` 안) — `JoinSessionAsync` 의 startGame 실패·timeout 경로. 같은 root cause 영향. failure 발생 시 GameObject 까지 destroy 되면 RoomClient 자체가 사라져 사용자가 LobbyPanel 재시도 불가.
3. `RoomListQuery.cs:32` (`OnDisable` 안) — Photon lobby 자체를 떠나는 분기. RoomListQuery 가 enable/disable 으로 lobby 재진입 구조라면 GameObject destroy 는 부적합.

**DS 측 `runner.Shutdown()` 은 변경하지 않는다.** `RoomAuthority.cs:157`, `RoomAuthority.cs:166` 의 server shutdown 은 DS process 자체 종료가 목적 — `destroyGameObject` 가 true 든 false 든 process 종료 시점에 의미 없음.

**SerializeField 검증**: fix 적용 후 LobbyPanel 의 `roomClient`/`roomListQuery` SerializeField 는 그대로 component reference 유지. Missing 상태로 직렬화되지 않음 (GameObject 가 살아있으니 component instance ID 가 보존됨).

### Phase B — last-player-leave terminate 동작 검증 + 필요시 보강 (Issue B fix)

`RoomAuthority.OnPlayerLeft` (line 141-160) 은 이미 last-player terminate 로직을 가지고 있으나 e2e 에서 동작하지 않은 사실 (Handoff trace) 을 해석하기 위해 다음 단계 진행:

1. **진단 단계 (필수)**: `RoomAuthority.OnPlayerLeft` 진입 시점 + `runner.ActivePlayers.Count()` 값을 `Debug.Log` 로 출력. DS 를 재빌드/재배포 → 2명 e2e 시나리오 (둘 다 같은 룸 입장 → 둘 다 leave) → CloudWatch 에서 log 확인.
   - **가설 H1 결과**: 로그가 0건이면 callback dispatch 자체가 안 됨. RoomAuthority 의 `OnPlayerJoined`/`OnPlayerLeft` 를 explicit interface implementation 으로 바꾸거나(예: `void INetworkRunnerCallbacks.OnPlayerLeft(...)` ), 또는 `MonoBehaviour` 의 Unity 콜백 매칭 충돌이라면 별도 이벤트 채널로 우회.
   - **가설 H2 결과**: 로그가 찍히되 ActivePlayers.Count() ≥ 1 이면 분기 미진입. fix: `if (!runner.ActivePlayers.Any() || runner.ActivePlayers.All(p => p == player))` 처럼 "방금 떠난 player 제외" 조건 추가. 또는 1-frame delay 후 재검사.
   - **가설 H3 결과**: terminate POST log 까지 성공하지만 backend status 가 ACTIVE 유지 또는 Photon sessionList 가 ACTIVE 표기. → backend `RoomReconciliationScheduler` 또는 Photon 쪽 sessionList 캐시 문제. 본 plan scope 초과 시 [`05-room-server-manager.md`](../specs/05-room-server-manager.md) 후속 plan 으로 escalation 하고 본 plan 은 H1/H2 결과까지만 fix.
2. **수정 단계**: 위 가설 결과에 따라 RoomAuthority 또는 ReportTerminated 호출 시점을 보정. 변경 범위는 `Assets/Multiplayer/Scripts/Room/Server/*.cs` 내부로 한정.
3. **검증 단계**: 2명 e2e 시나리오 재실행. 마지막 client 가 leave 한 직후 ~5초 이내 RoomList 갱신 시 해당 룸 사라짐 확인.

### Phase C — MaxPlayers semantic 확립 + 표시 보정 (Issue C fix)

1. **진단 단계 (필수)**: DS 부팅 직후 `Debug.Log($"PlayerCount sent={maxPlayers}")` + InRoomPanel 활성화 직후 `Debug.Log($"SessionInfo.MaxPlayers={sessionInfo.MaxPlayers} PlayerCount={sessionInfo.PlayerCount}")` 로 양쪽 값을 한 번 측정. Fusion 2 의 변환 규칙을 사실로 확립.
2. **수정 단계**: 측정 결과로 Option α/β/γ 중 채택:
   - 만약 SessionInfo.MaxPlayers == (DS PlayerCount) + 1 이 일관되게 재현 → DS 가 server slot 1을 추가로 등록한다는 의미. **Option α**: DS line 60 `PlayerCount = maxPlayers + 1` 송신 + 클라이언트 표시에서 `MaxPlayers - 1`. 또는 **Option β**: DS PlayerCount 유지 + 클라이언트 표시 -1.
   - 만약 SessionInfo.MaxPlayers == DS PlayerCount 그대로면 다른 원인 (예: InRoomPanel binder 가 PlayerCount 가 아닌 다른 source 를 잘못 읽음) — 코드 흐름 재조사.
3. **검증 단계**: 정원 2/4/8 입력 사이클로 InRoomPanel 표시 일치 확인.

### Phase 순서·atomic 자유도

implementer 가 3 phase 를 별도 commit 으로 쪼개도 무방. 단 atomic commit 으로 묶는다면 본 plan 의 Acceptance Criteria 가 한 번에 모두 검증되어야 함. Phase A 의 변경은 `RoomClient.cs` 3 line + `RoomListQuery.cs` 1 line 로 가장 작고 영향 범위가 client-only 라 먼저 들어가도 안전. Phase B/C 는 DS 재배포 필요 → ECR push + ECS task definition revision 사이클이 묶임.

## Deliverables

### 코드 변경

- `Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs` — Phase A: `_runner.Shutdown()` 2건 → `_runner.Shutdown(destroyGameObject: false)`.
- `Assets/Multiplayer/Scripts/Room/Client/RoomListQuery.cs` — Phase A: `_runner.Shutdown()` 1건 → `_runner.Shutdown(destroyGameObject: false)`.
- `Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs` — Phase B: 진단 log + (필요시) `OnPlayerLeft` 분기 조건 보강. interface 명시 변경 시 explicit interface implementation 으로 전환.
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs` — Phase C: 진단 log + (Option α 채택 시) `PlayerCount = maxPlayers + 1`.
- `Assets/Multiplayer/Scripts/Presence/MultiplayerInRoomPanel.cs` 또는 `ParticipantListBinder.cs` (정확한 파일은 implementer 가 InRoomPanel 의 PlayerCount 표시 binder 확인 후 결정) — Phase C: 표시 시 `MaxPlayers - 1` 보정.

### DS 재배포

- `tools/push-room-server-image.sh <tag>` — ECR push + `murang-room-server` task definition 새 revision. Phase B/C 변경 발생 시 필수.

### 신규 코드

- 신규 .cs 파일 없음. 모두 기존 파일 line 단위 fix.

## Acceptance Criteria

### Phase A — Leave 후 reference 살리기

- [ ] `[auto-hard]` `Grep "Shutdown\(destroyGameObject:" Assets/Multiplayer/Scripts/Room/Client/` 가 ≥ 3 매치 (RoomClient.cs 2건 + RoomListQuery.cs 1건).
  **검증:** `Grep "Shutdown\(destroyGameObject:" path=Assets/Multiplayer/Scripts/Room/Client/ output_mode=content`
- [ ] `[auto-hard]` `Grep "_runner.Shutdown\(\)" Assets/Multiplayer/Scripts/Room/Client/` 매치 0건 (이전 default-call 패턴 잔존 없음).
  **검증:** `Grep "_runner\.Shutdown\(\)" path=Assets/Multiplayer/Scripts/Room/Client/ output_mode=count` 결과 0.
- [ ] `[auto-hard]` Unity Editor EditMode 컴파일 통과 + `read_console` types=error 0건. `NetworkRunner.Shutdown(destroyGameObject: false)` signature 일치 확인.
  **검증:** `mcp__unityMCP__read_console types=["error"]` + `unity-test-runner` 1회 호출.
- [ ] `[manual-hard]` Quest 실기기 빌드 — InRoomPanel 의 LeaveButton 클릭 → LobbyPanel 복귀 → CreateButton 클릭 → 신규 룸 정상 생성 + 합류 (이전엔 "Failed: RoomClient reference is missing." 발화).
  **검증:** Quest 헤드셋 시나리오 — Activate → 룸 생성 → InRoom → Leave → Lobby → 두 번째 룸 생성. statusLabel 에 "Failed: RoomClient reference is missing." 미발화.

### Phase B — stale room 정리

- [ ] `[auto-soft]` `Grep "OnPlayerLeft" Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs` 가 진입 진단 log 또는 explicit interface 형태로 박힘.
  **검증:** `Grep "OnPlayerLeft" path=Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs output_mode=content`
- [ ] `[manual-hard]` 2명 e2e — 둘 다 같은 룸 합류 → 한 명 leave → 남은 한 명 leave → ~10초 이내 둘 다의 RoomList 에서 해당 룸 사라짐. CloudWatch DS log 에 `[RoomServerCallbackReporter] terminate POST succeeded` 1줄 출력.
  **검증:** Quest 2대 헤드셋 시나리오 + CloudWatch Logs Insights query (`fields @message | filter @message like /terminate POST/`).
- [ ] `[manual-hard]` 같은 시나리오에서 backend DB `room_server_instance` 테이블의 해당 row `status` 가 `TERMINATED` 로 갱신 (terminate POST 미발사 시 reconciliation timeout 후에야 갱신되므로 즉시 갱신 여부가 fix 검증 지표).
  **검증:** EC2 SSH + `mysql -e "SELECT id, status, updated_at FROM room_server_instance ORDER BY id DESC LIMIT 5"` 1회.

### Phase C — 정원 표시 보정

- [ ] `[auto-soft]` `Grep "PlayerCount" Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs` 의 line 60 위치에 변경 사실 (+1 carve-out 또는 진단 log 보강) 가 박힘.
  **검증:** `Grep "PlayerCount" path=Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs output_mode=content -n=true`
- [ ] `[manual-hard]` Quest 실기기 빌드 — 정원 2 입력 후 룸 생성 → InRoomPanel 의 참가자 카운트 "1 / 2" (이전 "1 / 3" 재현).
  **검증:** Quest 헤드셋 시나리오 + InRoomPanel 의 ParticipantCountLabel 스크린 확인.
- [ ] `[manual-hard]` 정원 4 입력 후 룸 생성 → InRoomPanel 카운트 "1 / 4" (이전 "1 / 5" 재현).
  **검증:** Quest 헤드셋 시나리오.

### 정합성 검증

- [ ] `[auto-hard]` `git diff --stat` 결과: `Assets/Multiplayer/Scripts/Room/Client/RoomClient.cs`, `RoomListQuery.cs`, `Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs`, `RoomServerBootstrap.cs`, 그리고 InRoomPanel binder 파일 외에는 변경 없음. (씬 .unity / decisions / prefab 변경 0건.)
  **검증:** `git diff --stat` 1회.
- [ ] `[auto-hard]` Unity Editor `read_console` types=error 0건 (Phase A 컴파일 후, Phase B/C 변경 후 양쪽).
  **검증:** `mcp__unityMCP__read_console types=["error"]` 2회.
- [ ] `[auto-soft]` EditMode 회귀 테스트 (`Murang.Multiplayer.Room.Tests`) 통과 — 직전 plan 에서 14/14 통과 기준 유지.
  **검증:** `unity-test-runner` Task 1회 호출.

## Out of Scope

- **남은 minor issue 4번 — passwordHash env var 누락**. backend `EcsRoomRuntimeProvider.buildEnvironment` 가 password hash 를 ECS env 에 안 보냄 → DS 가 잠금 룸 password check 불가. 무잠금 룸은 정상 동작. **fix 대상이 backend Java 코드이고 잠금 룸 e2e 검증 인프라가 별개 cycle 이라 본 plan 에서 분리. 후속 plan 후보.**
- **한글 폰트 atlas 도입** — 직전 plan 의 영문화 임시 조치 rollback 은 별도 plan.
- **SampleScene 의 multiplayer GameObject 정리** — 직전 plan 이래 Out of Scope 그대로.
- **Photon Fusion `SessionInfo.MaxPlayers` semantic 의 백엔드 doc 화** — 본 plan 의 Phase C 진단 단계에서 사실을 확립하면 후속 plan 에서 [`05-room-server-manager.md`](../specs/05-room-server-manager.md) 의 capacity semantic 박제에 반영. 본 plan 자체에서는 코드 fix 까지만.
- **Fusion 2 `NetworkRunner.Shutdown` signature 가 향후 패키지 업그레이드로 변경되는 경우의 호환성** — 본 plan 은 현 버전 fix. 패키지 업그레이드는 별도 plan.

## Notes

- Phase A 는 client-only 변경이라 ECR/ECS 재배포 없이 Quest 빌드만 다시 사이드로드해도 검증 가능. Phase B/C 의 DS-side 변경은 `tools/push-room-server-image.sh` 실행 후 `murang-room-server` task definition 의 새 revision 이 들어가야 다음 RunTask 부터 반영됨.
- Phase B 의 H1 가설 (callback dispatch 방식) 이 사실이면 본 plan 직후 RoomAuthority 의 모든 implicit public method (`OnPlayerJoined`, `OnPlayerLeft`) 를 explicit interface 로 일괄 정리하는 별도 cleanup plan 후보로 둘 만함.
- Phase C 의 Option α (DS-side carve-out) 채택 시 client 측 `RoomClient.cs` line 262 `PlayerCount = Mathf.Max(1, maxPlayers ?? 8)` 의 client-host 분기 (`allowClientSessionCreation=true`) 도 같은 정책으로 +1 carve-out 해야 일관성 유지. 단 client-host 경로는 backend-mediated 룸 생성 경로와 별개이고 현 시점 e2e 에 포함 안 돼있어 본 plan 검증 범위 밖. implementer 판단으로 함께 변경하거나 후속 plan 으로 분리.
- 본 plan 의 Phase A 한 줄 fix 가 가장 user-facing impact 가 크다 (Leave 후 즉시 새 룸 생성 가능 vs 빌드 재시작). Phase B/C 가 막힐 경우에도 Phase A 만 먼저 commit·검증 권장.

## Handoff

<!-- /spec-build 완료 시 doc-updater 가 채움. -->
