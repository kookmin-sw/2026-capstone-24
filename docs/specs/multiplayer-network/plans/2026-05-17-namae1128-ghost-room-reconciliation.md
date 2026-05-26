# Ghost room reconciliation (heartbeat 만료 + DS terminate 콜백)

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Done (auto AC) — manual-hard pending`

## Goal

`READY`/`ACTIVE` 상태의 room 이 heartbeat 임계 안에 신호를 보내지 않거나 `PROVISIONING`/`SERVER_STARTING` 상태가 timeout 안에 ready callback 에 도달하지 못하면 backend 가 주기 reconciliation 으로 자동 청소하고, dedicated server 가 정상 Shutdown 으로 닫힐 때는 새로 추가되는 `/internal/rooms/{id}/terminate` 콜백으로 backend 가 즉시 인지해 ECS task 정리·`rooms.closed_at` 기록을 마무리한다. ghost room 4경로 중 시나리오 B (DS Shutdown 했는데 backend 모름), C (Fargate 강제 종료), D (DS 부팅 실패) 를 본 plan 이 1차로 닫는다.

## Context

### 03-room-session.md / 05-room-server-manager.md 가 요구하는 것

[`03-room-session.md`](../specs/03-room-session.md) 의 Behavior 마지막 문단은 "`READY` 또는 `ACTIVE` 의 룸 서버가 heartbeat 을 제때 보내지 못하거나 런타임이 비정상으로 판단되면 `UNHEALTHY → TERMINATING → TERMINATED` 로 수습된다" 를 요구한다. [`05-room-server-manager.md`](../specs/05-room-server-manager.md) What 섹션은 `RoomServerManager` 가 "주기적 liveness reconciliation 책임" 을 가지며 "이벤트 유실, callback 누락, 프로세스 강제 종료가 발생해도 ghost room 과 orphan instance 를 정리할 수 있어야 한다" 고 박았다. 현재 backend 는 ready/heartbeat 콜백 수신·`markUnhealthy()`/`markTerminated()` 도메인 메서드까지는 갖췄지만, 임계 시간을 넘긴 instance 를 *주기적으로* 골라내 정리하는 컴포넌트가 없다 — `@EnableScheduling` 어디에도 부착되어 있지 않다.

### 이전 plan 산출

- `2026-05-01-namae1128-room-session-lifecycle` (archive): Photon Fusion 권위 인스턴스 + `RoomAuthority.OnPlayerLeft` 가 마지막 플레이어 퇴장 시 `runner.Shutdown()` 을 호출하는 흐름 구축. 단 backend 에는 알리지 않는다.
- `2026-05-07-namae1128-aws-dev-topology-ec2-fargate`: ECS RunTask + RoomServerManagerImpl + `/internal/rooms/{id}/ready` + `/heartbeat` 두 콜백, `room_server_instances` 스키마, ECS reconciliation 은 본 plan 책임에서 제외 (별도 후속 plan 으로 미룬다 — 본 plan 은 heartbeat/timeout 기반만).

### Decisions

- [`decisions/01-default-scene.md`](../decisions/01-default-scene.md): 본 plan 은 default 씬에 의존하는 자산을 수정하지 않으므로 *Consequence 충돌 없음*.
- [`decisions/02-clientcompat-enforcement.md`](../decisions/02-clientcompat-enforcement.md): admission mismatch 거부는 `UNHEALTHY` 시그널로 취급하지 않는다고 박혔다. 본 plan 의 reconciliation 은 admission 거부 카운트를 unhealthy 신호로 잘못 잡지 않는다 (heartbeat 시간만 본다).

### Ghost room 4경로와 본 plan 의 범위

| # | 시나리오 | 본 plan 의 책임 |
|---|---|---|
| A | 클라이언트 일시 단절 → 영구 끊김 | heartbeat 임계 도달 후 청소 (UX 보강은 별도 후속 plan) |
| B | 정상 퇴장 → DS Shutdown 했는데 backend 모름 | terminate 콜백 + idle 만료 fallback **둘 다 부착** |
| C | Fargate task 강제 종료 (Spot, crash) | heartbeat 임계 청소 |
| D | DS 부팅 실패 (ready 미도달, `PROVISIONING`/`SERVER_STARTING` 영구) | provisioning timeout 청소 |

### 현재 코드 상태 사실 박제 — 본 plan 이 재사용할 자산

- `backend/src/main/java/com/murang/room/domain/RoomServerInstance.java`: `markUnhealthy()` (`READY`/`ACTIVE` 일 때만 `UNHEALTHY` 전이), `beginTermination()`, `markTerminated(now)`, `markFailed(now)` 모두 존재 — 신규 도메인 메서드 추가 없이 재사용.
- `backend/src/main/java/com/murang/room/repository/RoomServerInstanceRepository.java`: `findAllByStatusInAndLastHeartbeatAtBefore(statuses, threshold)` 쿼리 **이미 정의되어 있으나 사용처 없음** — 본 plan 의 heartbeat reconciliation 이 첫 사용처.
- `backend/src/main/java/com/murang/room/manager/RoomServerManagerImpl.java`: `terminate(roomId, reason)` 는 `beginTermination` → `runtimeProvider.stopRoomTask` → `markTerminated` + `room.close` 까지 한 트랜잭션으로 처리. 본 plan 의 helper 들은 `terminate` 를 호출 위임만 한다.
- `backend/src/main/java/com/murang/room/controller/RoomInternalCallbackController.java`: `X-Internal-Token` 검증을 `verifyInternalToken(token)` 으로 캡슐화. 신규 `/terminate` 엔드포인트는 이 헬퍼를 재사용.
- `backend/src/main/java/com/murang/MurangApplication.java`: `@SpringBootApplication + @ConfigurationPropertiesScan` 만 부착. `@EnableScheduling` 부재.

## Verified Structural Assumptions

- `RoomServerInstance.markUnhealthy()` 는 `READY` 또는 `ACTIVE` 일 때만 `UNHEALTHY` 로 전이하고 다른 상태에서는 no-op. heartbeat reconciliation 은 `READY`/`ACTIVE` 두 상태만 대상으로 잡는다. — `Read backend/src/main/java/com/murang/room/domain/RoomServerInstance.java (2026-05-17)`
- `RoomServerInstance.beginTermination()` 은 `TERMINATED` 가 아니면 무조건 `TERMINATING` 으로 보내므로, `terminate(roomId, reason)` 를 이미 `TERMINATED` 상태에서 호출하면 멱등 보장이 된다 (단 `room.close(now)` 가 두 번 호출되면 `closed_at` 덮어쓰기 가능 — 본 plan 의 terminate 콜백 핸들러는 호출 전 status 검사로 가드). — `Read backend/src/main/java/com/murang/room/domain/RoomServerInstance.java (2026-05-17)`
- `RoomServerInstanceStatus` enum 정의 8개: `PROVISIONING / SERVER_STARTING / READY / ACTIVE / UNHEALTHY / TERMINATING / TERMINATED / FAILED`. heartbeat reconciliation 대상 = `{READY, ACTIVE}`, provisioning timeout 대상 = `{PROVISIONING, SERVER_STARTING}`, 청소 제외 = `{UNHEALTHY, TERMINATING, TERMINATED, FAILED}`. — `Read backend/src/main/java/com/murang/room/domain/RoomServerInstanceStatus.java (2026-05-17)`
- `RoomServerInstanceRepository.findAllByStatusInAndLastHeartbeatAtBefore(List, Instant)` 시그니처 확인. `lastHeartbeatAt` 가 `null` 인 row (= `markReady` 이전) 는 JPA derived query 의 `IS NULL` 분기 미포함이므로 결과에서 빠진다 — heartbeat reconciliation 대상은 자연스럽게 `READY` 이후 (markReady 가 `lastHeartbeatAt = now` 설정) 로 한정된다. — `Read backend/src/main/java/com/murang/room/repository/RoomServerInstanceRepository.java (2026-05-17)`
- `RoomServerManagerImpl.terminate(Long, String)` 가 이미 `Transactional` + `runtimeProvider.stopRoomTask` 호출 (실패 시 warn 후 계속) + `markTerminated` + `room.close(now)` 까지 단일 트랜잭션으로 처리. 본 plan 의 `markUnhealthyAndTerminate` 헬퍼는 이를 그대로 위임하되 호출 전 `markUnhealthy()` 만 추가로 호출. `markFailed` 경로는 별도 헬퍼 — `provisioning timeout` 은 `room.close` 도 수행 (room 자체도 닫힌다). — `Read backend/src/main/java/com/murang/room/manager/RoomServerManagerImpl.java (2026-05-17)`
- `RoomInternalCallbackController` 의 `verifyInternalToken(String)` 은 `properties.sharedSecret()` 가 null/blank 이면 토큰 검증 skip (test profile 호환), 아니면 `MessageDigest.isEqual` 로 상수시간 비교 후 mismatch 시 `ApiException.roomInternalForbidden()` 던짐. 신규 `/terminate` 엔드포인트도 동일 헬퍼 호출 1줄로 통일. — `Read backend/src/main/java/com/murang/room/controller/RoomInternalCallbackController.java (2026-05-17)`
- `MurangApplication` 에 `@EnableScheduling` 부재. 본 plan 의 `RoomReconciliationScheduler` 가 `@Scheduled` 를 쓰려면 `MurangApplication` 또는 별도 `@Configuration` 한 곳에 `@EnableScheduling` 을 추가해야 한다 — 선택은 `RoomServerManagerConfiguration` (이미 `@EnableConfigurationProperties` 부착, room 도메인 한정 scope 유지). — `Read backend/src/main/java/com/murang/MurangApplication.java (2026-05-17)`, `Read backend/src/main/java/com/murang/room/config/RoomServerManagerConfiguration.java (2026-05-17)`
- `RoomServerCallbackReporter.HeartbeatIntervalSeconds = 30f`. 기본 heartbeat 임계 `heartbeat-timeout` 을 `90s` (3 × 30s) 로 잡으면 1~2회 콜백 유실은 견디고 3회 연속 실패에서 잡힌다. `reconciliation-interval` 기본 `30s` 는 heartbeat 주기와 일치 — race 발생 시 다음 tick 에서 자연 수렴. — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs (2026-05-17)`
- `RoomServerCallbackConfig` env key 컨벤션: `ROOM_ID`, `ROOM_READY_CALLBACK_URL`, `ROOM_HEARTBEAT_CALLBACK_URL`, `ROOM_RUNTIME_VERSION`, `MURANG_ROOM_INTERNAL_CALLBACK_SHARED_SECRET`. **신규 추가 env key 는 같은 prefix 규칙 (`ROOM_*_CALLBACK_URL`) 을 따른다** → `ROOM_TERMINATE_CALLBACK_URL`. `TryParse` 는 ready/heartbeat 둘 다 비면 null 반환해 backend 통합 비활성 모드로 빠진다 — terminate URL 은 *선택* 으로 두어 null 이어도 비활성만 되고 부팅을 막지 않게 한다 (ready/heartbeat 와 동일 빈도로 ECS RunTask 가 주입할 것이므로 사실상 항상 셋된다). — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackConfig.cs (2026-05-17)`
- `RoomAuthority.OnPlayerLeft` (line 141-152): `runner.IsServer` 확인 후 `runner.ActivePlayers.Any()` 가 false 이면 `_ = runner.Shutdown()` 만 호출 (fire-and-forget, await 없음). 본 plan 은 이 분기 직전에 `_callbackReporter?.ReportTerminated("last-player-left")` 를 호출하도록 수정한다 — `ReportTerminated` 가 코루틴 시작 후 즉시 반환하므로 Shutdown 호출이 await 없이도 콜백 발사를 막지 않는다. (단 Shutdown 후 GameObject 가 즉시 파괴되면 코루틴이 끊긴다 — 본 plan 의 Approach 4 단계에서 `ReportTerminated` 가 `UnityWebRequest` 동기 `SendWebRequest` 의 timeout 5s 안에 끝나도록 *blocking POST 코루틴* 으로 만들고, Shutdown 호출은 그 코루틴 완료 후로 미룬다). — `Read Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs (2026-05-17)`
- `RoomServerBootstrap.EnsureRunner` 가 `_callbackReporter` 를 `gameObject.AddComponent<RoomServerCallbackReporter>()` 로 부착 후 `Initialize(RoomServerCallbackConfig.FromProcessEnvironment())` 호출. **`RoomAuthority` 도 같은 GameObject 에 부착** 되므로 `OnPlayerLeft` 안에서 `GetComponent<RoomServerCallbackReporter>()` 로 reporter 참조 획득이 가능. 별도 wire-up 의존주입 인터페이스 추가 불필요. — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs (2026-05-17)`
- `backend/src/test/java/com/murang/room/controller/RoomInternalCallbackControllerTest.java` 는 `@SpringBootTest + @AutoConfigureMockMvc + ActiveProfiles("test") + @MockBean RoomServerManager` 패턴. 신규 `/terminate` 테스트는 동일 패턴을 그대로 답습한다 (별도 test infra 추가 없음). `VALID_TOKEN = "test-internal-secret"` 는 `application-test.yml` 또는 동등 source 에 정의됐다고 가정 (기존 ready/heartbeat 테스트가 그 토큰으로 통과 중). — `Read backend/src/test/java/com/murang/room/controller/RoomInternalCallbackControllerTest.java (2026-05-17)`
- `Assets/Multiplayer/Scripts/Room/Server/` 폴더에는 `.asmdef` 가 없다 — 같은 폴더의 다른 server 스크립트들도 Assembly-CSharp (Unity 기본 어셈블리) 로 컴파일된다. `RoomServerCallbackReporter.ReportTerminated` 추가는 신규 namespace import 없이 기존 `UnityEngine`, `UnityEngine.Networking`, `System.Collections`, `System.Globalization`, `System.Text` 만 사용하므로 asmdef reference 갱신 불필요. — `Glob Assets/Multiplayer/Scripts/Room/**/*.asmdef → Tests asmdef 만 발견 (2026-05-17)`

## Approach

본 plan 은 backend 단계 (A) 와 Unity DS 단계 (B) 로 명확히 분리한다. 두 단계는 독립적으로 PR 분할 가능하지만 통합 검증은 같이 한다.

### A. 백엔드 (Spring) 단계

1. **`RoomReconciliationProperties` 신규 도입** — `backend/src/main/java/com/murang/room/config/RoomReconciliationProperties.java`
   - prefix `murang.room.reconciliation`
   - 필드: `Duration heartbeatTimeout` (기본 `PT90S`), `Duration provisioningTimeout` (기본 `PT5M`), `Duration scanInterval` (기본 `PT30S`).
   - `RoomServerManagerConfiguration` 의 `@EnableConfigurationProperties(...)` 에 본 properties 클래스를 추가한다.
   - **`application.yml` / `application-aws-dev.yml` 본문 변경은 없다** — 기본값만으로도 안전 동작하고, 환경별 튜닝은 본 plan Out of Scope.

2. **`RoomServerManager` 인터페이스에 reconciliation 전용 helper 추가**:
   - `void markUnhealthyAndTerminate(Long roomId, String reason)` — `RoomServerInstance.markUnhealthy()` 호출 후 기존 `terminate(roomId, reason)` 흐름과 동일하게 `runtimeProvider.stopRoomTask` + `markTerminated` + `room.close` 수행. 단일 `@Transactional`.
   - `void markProvisioningTimedOut(Long roomId, String reason)` — `PROVISIONING`/`SERVER_STARTING` 상태일 때만 `markFailed(now)` 호출. ECS task 가 이미 attach 되었으면 `runtimeProvider.stopRoomTask` 도 try (실패 시 warn). `room.close(now)` 수행.
   - `RoomServerManagerImpl` 의 기존 `terminate` 로직을 private `terminateInternal(roomId, reason, beforeAction)` 로 약간 재구조화해 두 helper 가 공유한다. public `terminate` 시그니처는 변경 없음.
   - **호출자가 멱등성 책임을 갖지 않도록** 두 helper 의 진입부에서 `instance.getStatus()` 가 이미 `TERMINATED`/`FAILED` 면 조용히 return (`@Transactional` 안에서 no-op).

3. **`RoomReconciliationScheduler` 신규 컴포넌트** — `backend/src/main/java/com/murang/room/manager/RoomReconciliationScheduler.java`
   - `@Component`, `@Scheduled(fixedDelayString = "#{@roomReconciliationProperties.scanInterval.toMillis()}")` 로 SpEL 기반 동적 주기.
   - `scan()` 메서드:
     1. `now = Instant.now(clock)`
     2. `heartbeatThreshold = now - heartbeatTimeout` → `instanceRepository.findAllByStatusInAndLastHeartbeatAtBefore(List.of(READY, ACTIVE), heartbeatThreshold)` → 각 row 에 `manager.markUnhealthyAndTerminate(roomId, "reconciliation:heartbeat-timeout")` 호출.
     3. `provisioningThreshold = now - provisioningTimeout` → `instanceRepository.findAllByStatusIn(List.of(PROVISIONING, SERVER_STARTING))` 결과를 stream 필터 `createdAt.isBefore(provisioningThreshold)` → `manager.markProvisioningTimedOut(roomId, "reconciliation:provisioning-timeout")`.
     4. 각 호출은 try/catch 로 격리해 한 row 의 transaction 실패가 다른 row 청소를 막지 않도록 한다 (catch 후 ERROR log + 다음 row).
   - `Clock` 주입은 기존 `roomServerClock` bean 재사용.

4. **`@EnableScheduling` 부착 위치 결정** — `RoomServerManagerConfiguration` 에 `@EnableScheduling` 추가. `MurangApplication` 까지 끌어올리면 다른 도메인이 의도치 않게 영향 받을 수 있다. room 도메인 안에서만 스케줄링이 활성화되도록 한정한다.

5. **`/internal/rooms/{id}/terminate` 엔드포인트 추가** — `RoomInternalCallbackController`:
   - 신규 DTO `controller/dto/RoomTerminateCallbackRequest.java`: record `{ @NotBlank String reason }`.
   - `@PostMapping("/{roomId}/terminate")` 메서드. `verifyInternalToken(token)` 호출. `roomServerManager.findByRoomId(roomId)` 로 현재 status 확인:
     - 없거나 이미 `TERMINATED`/`FAILED` → 204 (idempotent).
     - 그 외 → `manager.terminate(roomId, "ds-callback:" + request.reason())` 호출 후 204.
   - **token 검증 실패는 기존 ready/heartbeat 와 동일하게 `ApiException.roomInternalForbidden()` (403).**

6. **Backend 테스트 추가** — `backend/src/test/java/com/murang/room/`
   - `manager/RoomReconciliationSchedulerTest.java`:
     - `@SpringBootTest + ActiveProfiles("test") + @MockBean RoomServerManager` + 실 `RoomServerInstanceRepository` (in-memory DB).
     - 시나리오: heartbeat 임계 초과 row 1개 + `READY` 임계 이내 row 1개 + `ACTIVE` 임계 초과 row 1개 + `TERMINATED` row 1개 → `markUnhealthyAndTerminate` 가 정확히 2회 (`READY` 초과 + `ACTIVE` 초과) 호출되는지 verify.
     - 시나리오: `PROVISIONING` 임계 초과 + `SERVER_STARTING` 임계 이내 → `markProvisioningTimedOut` 가 1회 호출.
     - 시나리오: 1개 row 의 `markUnhealthyAndTerminate` 호출이 RuntimeException 던지면 다른 row 청소는 계속.
   - `manager/RoomServerManagerImplTest.java` 확장:
     - `markUnhealthyAndTerminate_persistsUnhealthyTransitionAndCallsStopTask` — `READY` 상태에서 호출 후 status `TERMINATED`, `room.closed_at` 셋팅, stub 의 `lastStop.reason()` = `"reconciliation:heartbeat-timeout"`.
     - `markUnhealthyAndTerminate_isIdempotentForAlreadyTerminated` — `TERMINATED` 상태 row 에 호출하면 stub stop 호출되지 않음.
     - `markProvisioningTimedOut_marksFailedAndAttemptsStopTask` — `SERVER_STARTING` (`ecsTaskArn` attach 후) 에서 호출 → status `FAILED`, `room.closed_at` 셋팅, stub stop 호출.
   - `controller/RoomInternalCallbackControllerTest.java` 확장:
     - `terminate_withValidToken_returnsNoContentAndCallsManager` — `manager.findByRoomId` mock 으로 `READY` snapshot 반환 → `manager.terminate(42L, "ds-callback:last-player-left")` 1회 verify.
     - `terminate_missingToken_returnsForbidden` — `manager.terminate` 호출되지 않음 verify.
     - `terminate_alreadyTerminated_returnsNoContentAndDoesNotCallManager` — `manager.findByRoomId` mock 이 `TERMINATED` snapshot 반환 → `terminate` 호출되지 않음, 204.

### B. Unity Dedicated Server 단계

7. **`RoomServerCallbackConfig` env key 1개 추가** — `Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackConfig.cs`:
   - 신규 상수 `public const string EnvTerminateCallbackUrl = "ROOM_TERMINATE_CALLBACK_URL";`
   - 신규 프로퍼티 `public string TerminateCallbackUrl { get; }`
   - `TryParse` 에서 terminate URL 은 **선택** — null 이어도 config 자체는 valid (ready/heartbeat 가 필수, terminate 만 비어도 backend 통합 부분 활성). null 인 경우 `RoomServerCallbackReporter.ReportTerminated` 는 no-op.
   - 절대 URL 검증은 ready/heartbeat 와 동일 `IsAbsoluteHttpUrl` 헬퍼 재사용. 비어 있지 않으면 검증 통과 강제.

8. **`RoomServerCallbackReporter.ReportTerminated(string reason)` 신규 public 메서드** — `Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs`:
   - signature: `public IEnumerator ReportTerminated(string reason)` — caller 가 `yield return reporter.ReportTerminated(...)` 로 *완료 대기 가능*.
   - 내부:
     1. `IsActive` 가 false 거나 `_config.TerminateCallbackUrl` 이 null/empty 면 즉시 yield break.
     2. heartbeat loop 진행 중이면 `StopCoroutine(_heartbeatLoop); _heartbeatLoop = null` (terminate 후에는 heartbeat 발사 의미 없음 — backend 가 어차피 정리).
     3. body 빌드: `{"reason":"<escaped>"}` (`EscapeForJson` 재사용).
     4. `BuildJsonPost(_config.TerminateCallbackUrl, body)` + `yield return request.SendWebRequest()`. timeout 5s 동일.
     5. 실패 시 Debug.LogWarning (Shutdown 진행은 막지 않는다 — idle 만료 fallback 이 backend 측에 있다).
   - **Update 호출 흐름 보존** — `ReportReady` 가 `_readyReported` 가드로 1회만 발사하는 패턴을 답습해, `ReportTerminated` 도 내부에 `_terminateReported` 가드를 둬 중복 호출 시 즉시 yield break.

9. **`RoomAuthority.OnPlayerLeft` 수정** — `Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs`:
   - 메서드 시그니처는 `void` 이므로 `Coroutine` 직접 시작 패턴으로 변경. 마지막 플레이어 퇴장 분기 안에서:
     1. `RoomServerCallbackReporter reporter = GetComponent<RoomServerCallbackReporter>();`
     2. `if (reporter != null && reporter.IsActive) { StartCoroutine(TerminateAndShutdownRoutine(runner, reporter)); } else { _ = runner.Shutdown(); }`
   - 신규 private `IEnumerator TerminateAndShutdownRoutine(NetworkRunner runner, RoomServerCallbackReporter reporter)`:
     - `yield return reporter.ReportTerminated("last-player-left");`
     - `_ = runner.Shutdown();`
   - **GameObject 가 Shutdown 직후 파괴되어 코루틴이 끊기는 race 방지** — Shutdown 호출은 ReportTerminated 코루틴이 끝난 *다음 프레임* 에 일어나므로 POST 요청 자체는 5s timeout 까지 완료 후 발사된다 (UnityWebRequest 의 SendWebRequest 는 비동기 IO 이지만 yield 로 완료 시점까지 대기).

10. **`RoomServerBootstrap` 변경 없음** — `_callbackReporter` 가 이미 같은 GameObject 에 부착되므로 `RoomAuthority.OnPlayerLeft` 가 `GetComponent` 로 접근 가능. 추가 wire-up 불필요.

### asmdef 의존 확인

- `Assets/Multiplayer/Scripts/Room/Server/` 폴더는 `.asmdef` 가 없어 Assembly-CSharp 기본 어셈블리로 컴파일된다. 신규 import 도 기존과 동일 (`UnityEngine`, `UnityEngine.Networking`, `System.*`) 이므로 asmdef reference 추가 단계 없음.

## Deliverables

### 신규 파일

- `backend/src/main/java/com/murang/room/config/RoomReconciliationProperties.java` — `@ConfigurationProperties(prefix="murang.room.reconciliation")` record. heartbeat/provisioning timeout + scan interval.
- `backend/src/main/java/com/murang/room/manager/RoomReconciliationScheduler.java` — `@Scheduled` 컴포넌트. heartbeat 임계 초과 row + provisioning timeout row 청소.
- `backend/src/main/java/com/murang/room/controller/dto/RoomTerminateCallbackRequest.java` — record `{ @NotBlank String reason }`.
- `backend/src/test/java/com/murang/room/manager/RoomReconciliationSchedulerTest.java` — 임계 분기 시나리오 3종 검증.

### 수정 파일

- `backend/src/main/java/com/murang/room/config/RoomServerManagerConfiguration.java` — `@EnableScheduling` 추가 + `@EnableConfigurationProperties` 에 `RoomReconciliationProperties` 등록.
- `backend/src/main/java/com/murang/room/manager/RoomServerManager.java` — `markUnhealthyAndTerminate`, `markProvisioningTimedOut` 두 메서드 인터페이스 추가.
- `backend/src/main/java/com/murang/room/manager/RoomServerManagerImpl.java` — 두 helper 구현, 기존 `terminate` 로직과 공유 가능한 private 헬퍼로 약간 재구조화. **기존 public 시그니처 변경 없음**.
- `backend/src/main/java/com/murang/room/controller/RoomInternalCallbackController.java` — `/terminate` 엔드포인트 메서드 추가. 신규 DTO 사용.
- `backend/src/test/java/com/murang/room/manager/RoomServerManagerImplTest.java` — 신규 helper 검증 테스트 3건 추가.
- `backend/src/test/java/com/murang/room/controller/RoomInternalCallbackControllerTest.java` — `/terminate` 엔드포인트 토큰·idempotent·정상 호출 케이스 3건 추가.
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackConfig.cs` — `EnvTerminateCallbackUrl` 상수 + `TerminateCallbackUrl` 프로퍼티 + `TryParse` 의 optional 처리 + URL 검증.
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs` — `ReportTerminated(string)` 코루틴 메서드 + heartbeat loop stop 처리 + `_terminateReported` 가드.
- `Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs` — `OnPlayerLeft` 의 마지막 플레이어 분기에서 reporter 가 활성이면 `TerminateAndShutdownRoutine` 코루틴을 통해 콜백 발사 후 Shutdown.

## Acceptance Criteria

- [ ] `[auto-hard]` backend Gradle 빌드가 신규/수정 파일을 포함해 컴파일 에러 없이 통과한다.
  **검증:** `./gradlew :backend:compileJava :backend:compileTestJava` 종료코드 0.
- [ ] `[auto-hard]` `RoomReconciliationSchedulerTest` 3개 시나리오 (heartbeat 초과 분기, provisioning timeout 분기, 한 row 예외 격리) 가 모두 통과한다.
  **검증:** `./gradlew :backend:test --tests "com.murang.room.manager.RoomReconciliationSchedulerTest"` 종료코드 0.
- [ ] `[auto-hard]` `RoomServerManagerImplTest` 의 신규 3건 (`markUnhealthyAndTerminate` 정상·멱등, `markProvisioningTimedOut`) 이 통과한다.
  **검증:** `./gradlew :backend:test --tests "com.murang.room.manager.RoomServerManagerImplTest"` 종료코드 0 + 신규 테스트 메서드명이 결과 XML 에 포함.
- [ ] `[auto-hard]` `RoomInternalCallbackControllerTest` 의 `/terminate` 3건 (정상 토큰 → 204 + manager 호출, missing token → 403, 이미 TERMINATED → 204 + manager 미호출) 이 통과한다.
  **검증:** `./gradlew :backend:test --tests "com.murang.room.controller.RoomInternalCallbackControllerTest"` 종료코드 0.
- [ ] `[auto-hard]` Unity Editor 컴파일 결과 (DS scripts) 에 에러가 없고 `RoomServerCallbackReporter.ReportTerminated`, `RoomServerCallbackConfig.TerminateCallbackUrl` 심볼이 존재한다.
  **검증:** Unity MCP `read_console` 후 0 errors + `Grep "public IEnumerator ReportTerminated" Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs` 1줄 hit + `Grep "TerminateCallbackUrl" Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackConfig.cs` 2줄 이상 hit.
- [ ] `[auto-soft]` `@EnableScheduling` 가 `RoomServerManagerConfiguration` 한 곳에만 부착되어 다른 도메인 scope 와 격리되어 있다.
  **검증:** `Grep "@EnableScheduling" backend/src/main/java` 결과 1줄, 파일 = `RoomServerManagerConfiguration.java`.
- [ ] `[auto-soft]` reconciliation scheduler 가 Spring context boot 시 `@Scheduled` 빈으로 등록되어 30s 주기로 1회 이상 호출된다 (정합성 검증).
  **검증:** `RoomReconciliationSchedulerTest` 안에 Spring context 로딩만 검증하는 `@SpringBootTest` 케이스 1건이 통과 + `ApplicationContext.getBean(RoomReconciliationScheduler.class)` 가 non-null.
- [ ] `[manual-hard]` AWS dev 환경에서 룸 생성 → DS ready 도달 후 ECS task 의 컨테이너 프로세스를 강제 종료 (`aws ecs stop-task --task <arn>`) → 90s 이내 backend `room_server_instances.status` 가 `TERMINATED`, `room.closed_at` 셋팅, CloudWatch Logs 에 `reconciliation:heartbeat-timeout` 사유 로그 확인.
  **검증:** EC2 SSH → `aws ecs stop-task` 실행 후 90~150s 후 DB `SELECT status, terminated_at FROM room_server_instances WHERE id=<id>` 가 `TERMINATED + 시각` 반환 + Spring 로그에 `reconciliation:heartbeat-timeout` 문자열 grep hit.
- [ ] `[manual-hard]` AWS dev 환경에서 룸 생성 후 마지막 클라이언트가 정상 퇴장 → DS `RoomAuthority.OnPlayerLeft` 의 마지막 분기 → `POST /internal/rooms/{id}/terminate` 가 backend 에 도달, status 가 `TERMINATED` 로 30s 이내 (= scheduler tick 1회 안짝) 전이된다.
  **검증:** Editor 클라이언트 2명 입장 후 둘 다 disconnect → 30s 이내 `SELECT status FROM room_server_instances WHERE id=<id>` 가 `TERMINATED`, `aws ecs describe-tasks` 가 `lastStatus=STOPPED` 반환. Spring 로그 grep `ds-callback:last-player-left` 1줄 hit.
- [ ] `[manual-hard]` 의도적으로 ready callback URL 을 잘못 주입해 ECS task 를 띄워 `SERVER_STARTING` 상태에 머무르게 한 뒤 5분 후 status `FAILED` + `room.closed_at` 셋팅 + ECS StopTask 시도 로그 확인.
  **검증:** `aws ecs run-task` 시 `ROOM_READY_CALLBACK_URL=http://127.0.0.1:9/notexist` 로 override → 5~6분 후 DB `status = FAILED` + Spring 로그에 `reconciliation:provisioning-timeout` grep hit.
- [ ] `[manual-hard]` `MURANG_ROOM_INTERNAL_CALLBACK_SHARED_SECRET` 가 설정된 환경에서 잘못된 토큰으로 `/internal/rooms/1/terminate` 호출 시 403 + 정상 토큰 + 잘못된 roomId (존재 X) 호출 시 204 (idempotent) 반환.
  **검증:** EC2 에서 `curl -X POST https://<endpoint>/internal/rooms/1/terminate -H "X-Internal-Token: wrong" -d '{"reason":"x"}' -i` → `HTTP/1.1 403` + 토큰 정상 + 없는 roomId → `HTTP/1.1 204`.

## Out of Scope

- **클라이언트 재연결 보강** (시나리오 A 의 UX 보강) — 별도 후속 plan. 본 plan 은 ghost 상태로 남는 instance 만 청소하고 클라이언트가 재합류할 수 있게 만드는 것은 다루지 않는다.
- **ECS DescribeTasks 기반 stale task reconciliation** — `instance.status = TERMINATED` 인데 ECS task 가 살아있는 orphan, 또는 그 반대 (task 죽었는데 backend 는 `ACTIVE`) 의 *runtime-side 사실확인* 은 별도 후속 plan. 본 plan 은 backend 의 heartbeat 시각 + DS terminate 콜백만 신호로 사용한다.
- **heartbeat/timeout 임계값의 환경별 튜닝** — properties 로 env override 가능하게 노출만 하고, 기본값 1세트 (90s / 5min / 30s) 만 박는다. 운영 데이터로 튜닝하는 plan 은 후속.
- **DS process SIGTERM 핸들러로 terminate 콜백 발사** — ECS task 가 `aws ecs stop-task` 로 우아한 종료 신호를 받아 DS 가 SIGTERM 을 받았을 때 terminate 콜백을 마지막 발사하는 흐름. idle 만료 fallback 이 이미 있으므로 본 plan 의 필수는 아니다. 후속 plan 후보.
- **CloudWatch custom metric** (예: `reconciliation_terminated_count`) — `aws-dev-topology` plan 의 CloudWatch 6 metrics 작업 범위에 포함되고, 본 plan 은 로깅까지만 보장.
- **DS Editor / local Docker 모드 reporter terminate 흐름 검증** — 본 plan 의 `IsActive == false` 시 no-op 분기로 자연 회피되며, 별도 시나리오 검증 plan 없음.

## Notes

- `RoomReconciliationScheduler` 의 `@Scheduled` 는 single-thread 기본 풀에서 실행된다. EC2 단일 인스턴스 dev 환경에서는 충분하나, 향후 Spring 인스턴스 다중화 시 분산 락 또는 leader election 이 필요해진다 (예: ShedLock). 본 plan 은 dev 단일 인스턴스 가정.
- `markProvisioningTimedOut` 에서 `runtimeProvider.stopRoomTask` 실패는 warn 후 계속 — ECS RunTask 가 사실상 실패해 task ARN 자체가 없는 경우 (`provisioned = runtimeProvider.startRoomTask` 가 throw 한 뒤 `markFailed` 까지 이미 실행된 경우) 본 helper 가 다시 호출되더라도 멱등 가드로 no-op.
- DS terminate 콜백의 `reason` 문자열은 `last-player-left` 외에도 향후 `shutdown-signal`, `crash` 등 확장 가능. backend 는 `ds-callback:` prefix 만 부여하고 reason 자체는 자유 텍스트로 받아 로그·관측에만 사용한다.
- `room.close(now)` 가 두 번 호출되면 `closed_at` 가 덮어쓰여지지만, terminate 콜백 핸들러는 status 가 `TERMINATED`/`FAILED` 면 호출 자체를 skip 하므로 사용자 가시 동작에 영향 없음 (테스트로 검증).
- **구현 시 SpEL 대체**: 본 plan 의 Approach 는 `@Scheduled(fixedDelayString = "#{@roomReconciliationProperties.scanInterval.toMillis()}")` 를 가정했으나, `@ConfigurationPropertiesScan` 자동 등록은 FQCN 기반 빈 이름을 만들어 SpEL `@roomReconciliationProperties` 참조가 깨진다. 실제 구현은 property placeholder `${murang.room.reconciliation.scan-interval-ms:30000}` (ms 단위 정수) 로 대체했다. 결과적으로 env override 키도 `MURANG_ROOM_RECONCILIATION_SCAN_INTERVAL_MS` (ms 정수) — `RoomReconciliationProperties.scanInterval` Duration 필드는 보존되지만 scheduler 의 `fixedDelay` 와는 분리된다.

## Handoff

코드/테스트는 commit `98e6fa7` (2026-05-18) 에서 일괄 반영. 2026-05-26 재검증 결과:

- AC#1 컴파일: `./gradlew compileJava compileTestJava` BUILD SUCCESSFUL (UP-TO-DATE).
- AC#2 `RoomReconciliationSchedulerTest`: 4/4 PASS (heartbeat 초과·provisioning timeout·예외 격리·Spring context bean 등록).
- AC#3 `RoomServerManagerImplTest`: 10/10 PASS (신규 `markUnhealthyAndTerminate` 정상/멱등 + `markProvisioningTimedOut` 포함).
- AC#4 `RoomInternalCallbackControllerTest`: 8/8 PASS (`/terminate` 정상/403/idempotent 3건 포함).
- AC#5 Unity 컴파일/심볼: `unity-test-runner` 결과 EditMode 143/143 PASS, `Murang.Multiplayer.Room.Tests` 67/67 PASS (plan 작성 시 57건 → 10건 추가, 전부 PASS), 컴파일 에러 0건. `ReportTerminated` / `TerminateCallbackUrl` 심볼 grep 통과.
- AC#6 `@EnableScheduling` scope: `RoomServerManagerConfiguration.java` 1곳만 부착 — grep 1 hit.
- AC#7 scheduler bean 등록: `RoomReconciliationSchedulerTest` 의 Spring context 케이스 PASS.

**Manual-hard 4건은 pending** — 모두 AWS dev 환경 (EC2 SSH + `aws ecs stop-task` + DB SELECT + CloudWatch grep) 사용자 직접 수행 항목. `aws-dev-topology-ec2-fargate` plan 의 real 모드 검증 사이클과 묶어서 수행 가능.

다음 plan 이 알아야 할 산출:

- `RoomReconciliationProperties` env 키: 기본값 `heartbeatTimeout=PT90S`, `provisioningTimeout=PT5M`, `scanInterval=PT30S` (Duration). **단 scheduler 의 `@Scheduled(fixedDelayString)` 는 SpEL 의존을 피해 `${murang.room.reconciliation.scan-interval-ms:30000}` ms 정수 placeholder 로 분리** — env override 시 `MURANG_ROOM_RECONCILIATION_SCAN_INTERVAL_MS` 사용 (Duration 필드와 별도 키).
- `RoomServerManager` helper 시그니처: `void markUnhealthyAndTerminate(Long, String)` (READY/ACTIVE → UNHEALTHY 전이 후 terminate 흐름) + `void markProvisioningTimedOut(Long, String)` (PROVISIONING/SERVER_STARTING → FAILED + room.close). 둘 다 `TERMINATED`/`FAILED` 상태에 대해 멱등 가드 내장.
- `/internal/rooms/{roomId}/terminate` 엔드포인트: body `{ "reason": "<text>" }`, header `X-Internal-Token`. 204 (idempotent: 없는 roomId 또는 이미 TERMINATED/FAILED), 403 (토큰 mismatch). 정상 경로는 `manager.terminate(roomId, "ds-callback:" + reason)` 호출.
- DS `RoomServerCallbackReporter.ReportTerminated(string reason)` 코루틴: `IsActive==false` 또는 `TerminateCallbackUrl` null/empty 면 즉시 yield break. `_terminateReported` 가드로 중복 호출 no-op. heartbeat loop 는 호출 시 중단. 호출자는 `yield return reporter.ReportTerminated(...)` 로 완료 대기 가능.
- `RoomAuthority.OnPlayerLeft` 의 마지막 플레이어 분기는 reporter 활성 시 `TerminateAndShutdownRoutine` 코루틴으로 콜백 발사 후 `runner.Shutdown()` 호출. 비활성 시 기존 fire-and-forget 흐름 보존.
- **후속 plan 후보**:
  - ECS DescribeTasks 기반 stale task reconciliation (본 plan 은 heartbeat 시각 + DS 콜백만 신호로 사용, runtime-side 사실확인은 별도).
  - DS SIGTERM 핸들러로 terminate 콜백 발사 (idle fallback 만으로도 동작은 보장되지만 graceful 신호 추가).
  - heartbeat/provisioning 임계값 환경별 튜닝 (운영 데이터 수집 후).
  - `persistent-demo-room-policy` plan — scheduler 의 heartbeat-timeout 후보 추출에 `is_persistent=false` 필터 추가 필요. 본 plan 의 `RoomReconciliationScheduler` 가 진입점.
