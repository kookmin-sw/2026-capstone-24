# Persistent 데모 룸 정책 (운영자 전용 internal endpoint + lifecycle 예외)

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Done`

## Goal

시연·발표 자리에서 즉시 join 가능한 룸 1개를 backend lifecycle 의 예외로 박아 0명 상태에서도 종료되지 않고 heartbeat reconciliation 의 timeout 청소 대상에서도 제외한다. 권한 채널은 운영자 전용 internal endpoint 1개로 한정 (`X-Internal-Token` 재사용), 동시 1개 강제 (이미 살아 있으면 409). 일반 룸 생성 API DTO 에는 `persistent` 옵션을 노출하지 않는다. 정책 단일 진실원: [`decisions/05-persistent-demo-room-policy.md`](../decisions/05-persistent-demo-room-policy.md).

## Context

### 03-room-session.md 가 요구하는 것

[`03-room-session.md`](../specs/03-room-session.md) `## What` 마지막 문단 (line 21) 은 "persistent 플래그가 설정된 시연/데모 전용 룸은 본 정책의 예외" + "동시에 살아 있는 persistent 룸은 1개로 제한되며, 생성 권한은 운영자에게만 부여된다 (일반 룸 생성 API 의 옵션으로 노출되지 않음)" 를 박았다. Behavior 섹션 (line 57-63) 은 두 시나리오를 추가했다:

1. persistent 룸의 마지막 유저 퇴장 → 룸은 종료되지 않고 0명 상태 유지 + heartbeat reconciliation timeout 청소 제외.
2. 이미 살아 있는 persistent 룸이 있을 때 두 번째 persistent 룸 생성 시도 → 거절 + 충돌 사유 전달.

본 plan 은 두 시나리오를 backend 분기 + internal endpoint + DS Shutdown skip 으로 실현한다.

### decision 단일 진실원

[`decisions/05-persistent-demo-room-policy.md`](../decisions/05-persistent-demo-room-policy.md) (Accepted, 2026-05-26) 의 Consequences 6건이 본 plan 의 Approach 와 1:1 대응한다:

1. `rooms.is_persistent BOOLEAN NOT NULL DEFAULT FALSE` 컬럼 추가.
2. `RoomServerManagerImpl` 의 last-player-left 처리에 `is_persistent=true` skip 분기.
3. `RoomReconciliationScheduler` 의 heartbeat-timeout 후보 추출에 `is_persistent=false` 제약.
4. 운영자 전용 internal endpoint `POST /internal/rooms/persistent` 신설 — `X-Internal-Token` 재사용, 동시 1개 강제 (이미 살아 있으면 409).
5. 일반 룸 생성 API DTO 에 `persistent` 옵션 노출 X.
6. 운영 매뉴얼 한 줄 박제.

추가로 본 plan 은 decision Consequences 에 *명시되지 않은 핵심 안전장치* 를 발견해 Approach 4 (DS 측 `IS_PERSISTENT` 전파 + `RoomAuthority.OnPlayerLeft` Shutdown skip) 로 채운다 — DS 가 persistent 인지 모르면 `RoomAuthority.OnPlayerLeft` 가 `TerminateAndShutdownRoutine` 코루틴으로 `POST /terminate` 발사 + `runner.Shutdown()` 을 실행해 DS 프로세스가 죽고, backend 가 persistent 플래그로 콜백을 무시해도 ECS task 는 STOPPED 가 되어 row 만 살아남는 ghost 가 된다. 즉 DS 측 분기가 빠지면 정책이 실제로 동작하지 않는다.

### 이전 plan 산출 (재사용)

[`2026-05-17-namae1128-ghost-room-reconciliation.md`](./2026-05-17-namae1128-ghost-room-reconciliation.md) 의 Handoff:

- `RoomReconciliationProperties` (heartbeat=90s, provisioning=5m, scan=30s) + `RoomReconciliationScheduler.scan()` 의 heartbeat-timeout 분기가 본 plan 의 진입점. 같은 분기에 `is_persistent=false` 제약을 추가한다.
- `RoomServerManager.markUnhealthyAndTerminate(Long, String)` / `markProvisioningTimedOut(Long, String)` 두 helper 가 이미 `TERMINATED`/`FAILED` 멱등 가드를 내장 — 본 plan 의 persistent skip 가드는 이들 helper *외부* (scheduler 진입 시) 에 박는다. helper 자체는 호출되면 동작하도록 두고, scheduler 가 애초에 호출하지 않는다.
- `RoomServerCallbackConfig.EnvTerminateCallbackUrl` + `RoomServerCallbackReporter.ReportTerminated(string reason)` + `RoomAuthority.TerminateAndShutdownRoutine` 의 last-player-left 흐름이 이미 본 plan 의 *수정 지점* 이다 — DS 측 persistent 플래그가 없으면 이 흐름이 그대로 발사돼 룸이 죽는다.
- `/internal/rooms/{id}/terminate` 엔드포인트 (DS 콜백) 는 본 plan 에서 *수정* 대상 — backend 측에서 persistent 룸이면 콜백 자체를 idempotent skip 하여 204 반환 (현재는 status 가 `TERMINATED`/`FAILED` 인 경우만 skip).

### Decisions / 충돌 검사

- [`decisions/01-default-scene.md`](../decisions/01-default-scene.md): 본 plan 은 default 씬 자산을 수정하지 않는다 — persistent 룸도 동일한 default 씬을 로드한다 (decision 05 Out of Scope 명시 — "콘텐츠·배치 차이 없음"). 충돌 없음.
- [`decisions/02-clientcompat-enforcement.md`](../decisions/02-clientcompat-enforcement.md): admission 거부 카운트를 unhealthy 신호로 잡지 않는 규칙 — 본 plan 의 persistent skip 분기는 admission 거부와 무관 (heartbeat 시간 + last-player-left 만 본다). 충돌 없음.
- decision 05 Out of Scope: ECS task 자체 crash 시 자동 재시작은 본 plan 도 다루지 않는다. desired_count 기반 ECS Service 전환은 후속 plan 후보.

## Verified Structural Assumptions

- `rooms` 테이블 현재 스키마는 `room_id BIGINT PK / owner_user_id / photon_session_name UNIQUE / max_players / created_at / closed_at / password_hash`. 신규 컬럼 `is_persistent BOOLEAN NOT NULL DEFAULT FALSE` 는 V5 (`password_hash` 추가) 다음 V6 로 추가한다 — Flyway naming 컨벤션. — `Read backend/src/main/resources/db/migration/V3__create_rooms.sql (2026-05-27)`, `Read backend/src/main/resources/db/migration/V5__add_password_hash_to_rooms.sql (2026-05-27)`
- 마이그레이션 채널: **Flyway** (`spring.flyway.enabled: true`, `locations: classpath:db/migration`). JPA 는 `ddl-auto: validate` 로 스키마 변경 X (Flyway 가 단일 진실원). 신규 컬럼은 V6 SQL 파일 + `Room` 엔티티 필드 1쌍 추가 — JPA validate 가 컬럼 매칭을 강제한다. — `Read backend/src/main/resources/application.yml (2026-05-27)` lines 6-16
- `Room` 엔티티 위치: `backend/src/main/java/com/murang/room/domain/Room.java`. 현재 필드: `roomId(Long PK) / ownerUserId / photonSessionName / maxPlayers / passwordHash / createdAt / closedAt`. 정적 팩토리: `Room.open(ownerUserId, photonSessionName, maxPlayers, passwordHash, now)` — 본 plan 은 새 정적 팩토리 `Room.openPersistent(ownerUserId, photonSessionName, maxPlayers, passwordHash, now)` 를 추가하고 기존 `open` 은 `isPersistent=false` 기본값으로 호출되도록 위임 (caller 호출처 호환 보존). — `Read backend/src/main/java/com/murang/room/domain/Room.java (2026-05-27)`
- `Room` 엔티티에 `passwordHash` 외 추가 필드는 없다. 본 plan 의 `isPersistent` 필드 위치: `@Column(name = "is_persistent", nullable = false) private boolean isPersistent;` + getter `isPersistent()` + 기본값은 entity 생성 시 false (`Room.open` 경로) / true (`Room.openPersistent` 경로). — `Read backend/src/main/java/com/murang/room/domain/Room.java (2026-05-27)` lines 19-104
- `RoomTaskStartRequest` 현재 시그니처: `record RoomTaskStartRequest(Long roomId, String photonSessionName, int maxPlayers, String roomRuntimeVersion, URI readyCallbackUrl, URI heartbeatCallbackUrl)`. 본 plan 은 마지막에 `boolean isPersistent` 필드를 1개 추가 — record signature 확장. caller 1곳 (`RoomServerManagerImpl.provision`), 사용처 1곳 (`EcsRoomRuntimeProvider.buildEnvironment`). — `Read backend/src/main/java/com/murang/room/runtime/RoomTaskStartRequest.java (2026-05-27)`
- ECS 환경변수 주입 컨벤션: `EcsRoomRuntimeProvider.buildEnvironment(request)` 가 `KeyValuePair` 리스트를 ContainerOverride 에 실어 보낸다. 기존 env keys: `ROOM_ID`, `PHOTON_SESSION_NAME`, `MAX_PLAYERS`, `ROOM_RUNTIME_VERSION`, `ROOM_READY_CALLBACK_URL`, `ROOM_HEARTBEAT_CALLBACK_URL`, `MURANG_ROOM_INTERNAL_CALLBACK_SHARED_SECRET`. **단 `ROOM_TERMINATE_CALLBACK_URL` 은 아직 buildEnvironment 에 없다** — ghost-room plan 의 Handoff 박제는 "ready/heartbeat 와 동일 빈도로 ECS RunTask 가 주입할 것" 이라 적혔으나 실제 `EcsRoomRuntimeProvider.buildEnvironment` 코드는 미반영 상태. 본 plan 은 (a) `ROOM_IS_PERSISTENT` + (b) 누락된 `ROOM_TERMINATE_CALLBACK_URL` 두 env 를 함께 추가한다 (terminate URL 은 본 plan 의 DS skip 분기 검증에 필수). — `Read backend/src/main/java/com/murang/room/runtime/ecs/EcsRoomRuntimeProvider.java (2026-05-27)` lines 166-181
- `RoomCallbackUrlBuilder` 는 `readyCallbackUrl(roomId)` / `heartbeatCallbackUrl(roomId)` 두 메서드만 가진다. **`terminateCallbackUrl(roomId)` 미정의** — 본 plan 은 `RoomCallbackUrlBuilder` 에 메서드를 추가하고 `RoomServerManagerImpl.provision` 에서 `RoomTaskStartRequest` 빌드 시 함께 전달한다. — `Read backend/src/main/java/com/murang/room/manager/RoomCallbackUrlBuilder.java (2026-05-27)`
- `RoomServerInstanceRepository` 의 reconciliation 진입 쿼리: `findAllByStatusInAndLastHeartbeatAtBefore(List<RoomServerInstanceStatus>, Instant)`. 이 쿼리는 `room_server_instances` 테이블만 기준 — `is_persistent` 는 `rooms` 테이블 컬럼이므로 join 또는 별도 stream filter 가 필요. **본 plan 은 derived query 의 join 복잡도를 피해 scheduler 쪽에서 결과를 받은 후 `roomRepository.findAllById` 로 batch 조회 → `is_persistent=false` row 만 통과** 시키는 stream filter 방식을 채택 (provisioning timeout 도 동일). 이 방식은 `room_server_instances` 테이블 스키마를 건드리지 않고 reconciliation 진입점만 수정한다. — `Read backend/src/main/java/com/murang/room/repository/RoomServerInstanceRepository.java (2026-05-27)`, `Read backend/src/main/java/com/murang/room/manager/RoomReconciliationScheduler.java (2026-05-27)`
- `RoomRepository.findAllById(Iterable)` 는 Spring Data JPA 기본 제공 — 신규 메서드 추가 불필요. 본 plan 의 scheduler 는 이 메서드로 batch 조회 후 `Map<Long, Room>` 으로 변환해 stream filter. — `Read backend/src/main/java/com/murang/room/repository/RoomRepository.java (2026-05-27)`
- DS 측 `RoomServerCallbackConfig` 의 env key 컨벤션: 모든 키가 `ROOM_*` prefix (예외: `MURANG_ROOM_INTERNAL_CALLBACK_SHARED_SECRET`). `TryParse` 는 ready/heartbeat URL 이 비면 null 반환 (backend 통합 비활성). terminate URL 은 *optional*. 본 plan 의 신규 키 `ROOM_IS_PERSISTENT` 도 *optional* — null/empty 면 `IsPersistent=false` 로 fallback (안전 기본값). 파싱은 `bool.TryParse` 사용. — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackConfig.cs (2026-05-27)` lines 14-101
- `RoomAuthority.OnPlayerLeft` (line 149-170) 현 상태 (ghost-room plan 이후): `runner.IsServer` 확인 → `runner.ActivePlayers.Any()` false 면 → `RoomServerCallbackReporter reporter = GetComponent<RoomServerCallbackReporter>()` 획득 → `reporter != null && reporter.IsActive` 일 때 `StartCoroutine(TerminateAndShutdownRoutine(runner, reporter))`, else `_ = runner.Shutdown()`. 본 plan 은 이 분기에 **`reporter.IsPersistent` 체크 1단계를 가장 앞에 삽입** — true 면 `return` (Shutdown skip + terminate POST 도 발사하지 않음). — `Read Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs (2026-05-27)` lines 149-177
- `RoomServerCallbackReporter` 의 외부 노출 API: `IsActive { get; }`, `void Initialize(RoomServerCallbackConfig)`, `void ReportReady()`, `IEnumerator ReportTerminated(string reason)`. 내부 frame-level loop: `HeartbeatLoop()` 는 `RoomServerCallbackReporter._heartbeatLoop` coroutine 으로 30s 주기 발사 (지속 동작 — persistent 룸도 0명 상태에서 heartbeat 계속 송신해야 backend 가 `last_heartbeat_at` 을 갱신하고 `is_persistent=true` 필터로 reconciliation 청소 대상에서 제외된다 — 즉 heartbeat 자체는 건드릴 필요 없음, persistent 필터만 추가하면 됨). `ReportTerminated` 는 `_terminateReported` 가드로 중복 호출 no-op. **본 plan 의 `IsPersistent` 프로퍼티는 `_config?.IsPersistent ?? false` 한 줄로 노출하면 충분 — Initialize 흐름과 frame loop 에 영향 없음**. — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs (2026-05-27)` lines 28-106
- `RoomServerBootstrap.EnsureRunner` 가 `_callbackReporter = gameObject.AddComponent<RoomServerCallbackReporter>()` 부착 + `_callbackReporter.Initialize(RoomServerCallbackConfig.FromProcessEnvironment())` 호출. **`RoomAuthority` 도 같은 GameObject 에 부착** — 본 plan 의 `OnPlayerLeft` 가드는 `GetComponent<RoomServerCallbackReporter>()` 로 그대로 reporter 참조 획득. Bootstrap 자체는 수정 불필요. — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs (2026-05-27)` lines 93-153
- `RoomInternalCallbackController` 의 `/terminate` 엔드포인트 (line 66-78) 는 status 가 `TERMINATED`/`FAILED` 또는 snapshot empty 일 때만 idempotent 204. 본 plan 은 추가 조건으로 `room.isPersistent()` 도 idempotent 204 처리한다 — DS 측에서 호출이 발사되지 않도록 막아두긴 했지만 (수동 RPC, DS 패치 누락 등) 이중 안전망. **snapshot 에 `isPersistent` 정보가 없으므로** `RoomServerSnapshot` record 에 `boolean isPersistent` 필드 추가 + `RoomServerSnapshot.of(Room, RoomServerInstance)` 에서 `room.isPersistent()` 매핑. — `Read backend/src/main/java/com/murang/room/controller/RoomInternalCallbackController.java (2026-05-27)` lines 66-78, `Read backend/src/main/java/com/murang/room/manager/RoomServerSnapshot.java (2026-05-27)`
- `ApiException` 에 conflict (HTTP 409) 헬퍼는 `nicknameDuplicate()` (AUTH_NICKNAME_DUPLICATE) 와 `roomNameDuplicate()` (ROOM_NAME_DUPLICATE) 두 개만 존재. 본 plan 은 신규 헬퍼 `persistentRoomAlreadyExists()` + `ErrorCode.PERSISTENT_ROOM_ALREADY_EXISTS(HttpStatus.CONFLICT, "이미 살아 있는 persistent 룸이 있습니다.")` 추가. — `Read backend/src/main/java/com/murang/common/exception/ApiException.java (2026-05-27)`, `Read backend/src/main/java/com/murang/common/exception/ErrorCode.java (2026-05-27)`
- ConfigurationProperties 등록: `MurangApplication` 에 `@ConfigurationPropertiesScan` 부착됨 → `@ConfigurationProperties` annotated record 는 패키지 스캔으로 자동 등록 (e.g. `RoomReconciliationProperties` 가 그렇게 등록됨). 본 plan 의 신규 ConfigurationProperties 클래스 추가 없음 — 기존 `RoomInternalCallbackProperties.sharedSecret` 만 재사용. — `Grep ConfigurationPropertiesScan|EnableConfigurationProperties (2026-05-27)`
- `RoomInternalCallbackController` 의 `verifyInternalToken(token)` 헬퍼는 `properties.sharedSecret()` null/blank 시 검증 skip (test profile 호환), 아니면 `MessageDigest.isEqual` 상수시간 비교 후 mismatch 시 `ApiException.roomInternalForbidden()`. 본 plan 의 신규 `POST /internal/rooms/persistent` 도 동일 헬퍼 1줄 호출. — `Read backend/src/main/java/com/murang/room/controller/RoomInternalCallbackController.java (2026-05-27)` lines 80-92
- `RoomServerManagerImpl.provision(RoomProvisioningCommand)` 는 `roomRepository.findByPhotonSessionName` 으로 중복 검사 → `Room.open(...)` 으로 save → `RoomServerInstance.provision` → `runtimeProvider.startRoomTask` → `attachEcsTask`. 본 plan 의 `provisionPersistent(...)` 는 같은 단계를 거치되 (a) `Room.openPersistent(...)` 호출, (b) 진입 가드로 `roomRepository.findFirstByIsPersistentTrueAndClosedAtIsNull()` 가 non-empty 면 `ApiException.persistentRoomAlreadyExists()` 던짐, (c) `RoomTaskStartRequest` 에 `isPersistent=true` 채움. — `Read backend/src/main/java/com/murang/room/manager/RoomServerManagerImpl.java (2026-05-27)` lines 48-70
- `RoomRepository` 에 신규 derived query `Optional<Room> findFirstByIsPersistentTrueAndClosedAtIsNull()` 추가 — Spring Data JPA derived query naming. 인덱스는 기존 `idx_rooms_closed_at` 가 부분 커버 (persistent 룸은 사실상 1개이므로 풀스캔 비용 무시). — `Read backend/src/main/java/com/murang/room/repository/RoomRepository.java (2026-05-27)`
- `RoomServerInstanceStatus` enum: `PROVISIONING / SERVER_STARTING / READY / ACTIVE / UNHEALTHY / TERMINATING / TERMINATED / FAILED` 8개. 본 plan 의 persistent skip 분기는 enum 변경 없음 — 기존 status 그대로 사용. — `Read backend/src/main/java/com/murang/room/domain/RoomServerInstanceStatus.java (2026-05-27)`
- 테스트 패턴: `RoomInternalCallbackControllerTest` 는 `@SpringBootTest + @AutoConfigureMockMvc + ActiveProfiles("test") + @MockBean RoomServerManager` 패턴. `VALID_TOKEN = "test-internal-secret"` 는 `application-test.yml` 정의값. `RoomReconciliationSchedulerTest` 는 dual 패턴 — Spring context bean 등록 검증은 `@SpringBootTest + @MockBean`, scan() 로직 검증은 mock 기반 단위 테스트. 본 plan 의 신규 테스트는 두 패턴 모두 답습. — `Read backend/src/test/java/com/murang/room/controller/RoomInternalCallbackControllerTest.java (2026-05-27)`, `Read backend/src/test/java/com/murang/room/manager/RoomReconciliationSchedulerTest.java (2026-05-27)` lines 1-80
- asmdef 의존성: `Assets/Multiplayer/Scripts/Room/Server/` 에는 `.asmdef` 없음 → Assembly-CSharp 기본 컴파일. `Assets/Multiplayer/Scripts/Room/Tests/` 에만 `Murang.Multiplayer.Room.Tests.asmdef` (references: `UnityEngine.TestRunner / UnityEditor.TestRunner / Murang.Multiplayer / Fusion.Unity`) 존재. **`Assets/Multiplayer/Scripts/Room/` 폴더에 `Murang.Multiplayer.asmdef` 가 없음 — Tests asmdef 가 `Murang.Multiplayer` 를 reference 하지만 실제 server scripts 가 Assembly-CSharp 으로 컴파일되면 reference 가 비대칭이다**. 본 plan 은 신규 namespace import 없이 기존 `UnityEngine` 만 사용하므로 asmdef reference 갱신 단계 추가 불필요 — `IsPersistent` 프로퍼티 추가는 기존 `Murang.Multiplayer.Room.Server` namespace 내부. — `Glob Assets/Multiplayer/Scripts/Room/**/*.asmdef (2026-05-27)`, `Read Assets/Multiplayer/Scripts/Room/Tests/Murang.Multiplayer.Room.Tests.asmdef (2026-05-27)`
- 운영 매뉴얼 박제 위치: `docs/dev/` 디렉토리 또는 README. 본 plan 은 `docs/dev/operations/persistent-demo-room.md` (신규) 에 박제 — `docs/dev/` 디렉토리는 기존에 운영 메모를 모아두는 관례 (Implementer 가 디렉토리 부재 시 함께 생성). decision 의 "1줄 절차" 가이드를 보존하고 cURL 예시 1쌍 (생성 + 종료) 만 박는다.

## Approach

본 plan 은 backend (A) 와 DS (B) 두 단계로 분리한다. 두 단계 모두 통과해야 정책이 실제로 동작 — backend 만 박으면 DS 가 마지막 플레이어 퇴장 시 Shutdown + terminate POST 를 발사해 ECS task 가 죽고 row 만 남는 ghost 가 되고, DS 만 박으면 reconciliation 이 90s 후 unhealthy → terminate 로 청소한다. 두 단계가 일치해야 0명 상태에서 룸이 유지된다.

### A. 백엔드 (Spring) 단계

1. **Flyway V6 마이그레이션** — `backend/src/main/resources/db/migration/V6__add_is_persistent_to_rooms.sql`:
   ```sql
   ALTER TABLE rooms
       ADD COLUMN is_persistent BOOLEAN NOT NULL DEFAULT FALSE;
   ```
   기존 row 전부 false 로 채워진다. `ddl-auto: validate` 가 컬럼 매핑 강제하므로 `Room` 엔티티에 필드 추가 후에만 boot 성공.

2. **`Room` 엔티티 변경** — `backend/src/main/java/com/murang/room/domain/Room.java`:
   - 필드 `@Column(name = "is_persistent", nullable = false) private boolean isPersistent;` 추가.
   - getter `public boolean isPersistent()` 추가.
   - 정적 팩토리 `Room.open(ownerUserId, photonSessionName, maxPlayers, passwordHash, now)` 는 그대로 보존 (호출처 변경 X) — 내부에서 `isPersistent = false` 기본값 셋팅.
   - 신규 정적 팩토리 `public static Room openPersistent(Long ownerUserId, String photonSessionName, int maxPlayers, String passwordHash, Instant now)` — `isPersistent = true` 셋팅 후 `open` 동일 흐름.

3. **`RoomRepository` 진입 쿼리 추가** — `backend/src/main/java/com/murang/room/repository/RoomRepository.java`:
   - `Optional<Room> findFirstByIsPersistentTrueAndClosedAtIsNull()` — persistent 룸 중복 검사용.

4. **`RoomTaskStartRequest` record 확장** — `backend/src/main/java/com/murang/room/runtime/RoomTaskStartRequest.java`:
   - 마지막에 `URI terminateCallbackUrl` + `boolean isPersistent` 두 필드 추가 (terminate URL 은 ghost-room plan handoff 의 미완 잔여 — 본 plan 에서 함께 닫는다).
   - `RoomServerManagerImpl.provision` / `provisionPersistent` 양쪽이 새 record 시그니처로 호출.

5. **`RoomCallbackUrlBuilder.terminateCallbackUrl(roomId)` 추가** — 기존 ready/heartbeat 패턴 답습. `/internal/rooms/{id}/terminate` 리졸브.

6. **`RoomServerManager` 인터페이스에 `provisionPersistent` 추가** — `RoomProvisioningCommand` 를 그대로 받아 `RoomServerSnapshot` 반환:
   ```java
   RoomServerSnapshot provisionPersistent(RoomProvisioningCommand command);
   ```
   - `RoomServerManagerImpl.provisionPersistent`:
     1. `roomRepository.findFirstByIsPersistentTrueAndClosedAtIsNull()` 가 non-empty 면 → `ApiException.persistentRoomAlreadyExists()` 던짐 (409).
     2. 기존 `provision` 흐름과 동일하되 `Room.openPersistent(...)` 호출, `RoomTaskStartRequest` 에 `isPersistent=true`.
   - 코드 중복 최소화를 위해 두 메서드가 공유하는 private `provisionInternal(RoomProvisioningCommand, boolean isPersistent)` 로 재구조화. public `provision` 시그니처 변경 없음.

7. **`RoomServerSnapshot` 에 `isPersistent` 필드 추가** — record 마지막에 추가 + `RoomServerSnapshot.of(Room, RoomServerInstance)` 매핑 갱신. **외부 가시 응답 `RoomResponse` 는 변경 없음** (사용자에게 노출하지 않음 — decision Consequences 5번 "DTO 에 옵션 노출 X" 와 정합).

8. **`RoomServerManagerImpl.markUnhealthyAndTerminate` 진입 가드 추가** — heartbeat-timeout 분기에서 호출되지만 직접 호출자가 있을 수도 있으므로 helper *자체* 에도 가드 박음:
   ```java
   if (room.isPersistent()) {
       log.info("markUnhealthyAndTerminate skipped (persistent room) room_id={}", roomId);
       return;
   }
   ```
   `markProvisioningTimedOut` 도 동일 가드 (persistent 룸이 PROVISIONING/SERVER_STARTING 에 머무를 일은 없지만 안전망).

9. **`RoomReconciliationScheduler.scan()` 의 `is_persistent=false` 필터** — `backend/src/main/java/com/murang/room/manager/RoomReconciliationScheduler.java`:
   - heartbeat-timeout 후보 row 를 받은 후 `roomRepository.findAllById(roomIds)` 로 batch 조회 → `Map<Long, Room>` → stream filter `room != null && !room.isPersistent()` 통과만 `markUnhealthyAndTerminate` 호출.
   - provisioning-timeout 분기도 동일 stream filter.
   - 생성자에 `RoomRepository` 주입 추가.

10. **`/internal/rooms/{id}/terminate` 엔드포인트 idempotent 조건 확장** — `RoomInternalCallbackController.terminate`:
    - `snapshot.get().isPersistent()` 도 idempotent 204 조건 (status 검사와 OR 결합).
    - log 1줄 추가: persistent 룸 콜백 무시 사실 박제.

11. **운영자 전용 internal endpoint 신설** — `RoomInternalCallbackController` 에 메서드 추가 (별도 컨트롤러 분리 X — `X-Internal-Token` 검증 헬퍼와 토큰 공유, scope 한정 유지):
    ```java
    @PostMapping("/persistent")
    public ResponseEntity<ApiResponse<RoomResponse>> createPersistent(
            @RequestHeader(value = INTERNAL_TOKEN_HEADER, required = false) String token,
            @Valid @RequestBody RoomCreatePersistentRequest request) {
        verifyInternalToken(token);
        RoomServerSnapshot snapshot = roomServerManager.provisionPersistent(
                new RoomProvisioningCommand(
                        request.ownerUserId(),  // 운영자가 명시
                        request.photonSessionName(),
                        request.maxPlayers(),
                        request.passwordHash(),
                        request.roomRuntimeVersion()));
        return ResponseEntity.ok(ApiResponse.ok(RoomResponse.of(snapshot)));
    }
    ```
    - 신규 DTO `controller/dto/RoomCreatePersistentRequest.java`: record `{ @Positive Long ownerUserId, @NotBlank @Size(max=128) String photonSessionName, @Min(1) @Max(32) int maxPlayers, @Size(max=64) String passwordHash, @NotBlank String roomRuntimeVersion }`.
    - 운영자가 EC2 SSH 후 `curl -H 'X-Internal-Token: $SECRET' POST /internal/rooms/persistent` 호출. 일반 유저는 path 자체에 도달 가능하지만 `X-Internal-Token` 검증으로 403.

12. **`ErrorCode` + `ApiException` 추가**:
    - `ErrorCode.PERSISTENT_ROOM_ALREADY_EXISTS(HttpStatus.CONFLICT, "이미 살아 있는 persistent 룸이 있습니다.")`.
    - `ApiException.persistentRoomAlreadyExists()` 정적 팩토리.

13. **`EcsRoomRuntimeProvider.buildEnvironment` 확장** — `ROOM_IS_PERSISTENT` env (`"true"`/`"false"`) + `ROOM_TERMINATE_CALLBACK_URL` env 두 개 추가. `RoomTaskStartRequest.isPersistent()` / `terminateCallbackUrl()` 값을 그대로 넘김.

14. **Backend 테스트**:
    - `manager/RoomServerManagerImplTest` 확장:
      - `provisionPersistent_savesPersistentRoomWithFlag` — `Room.isPersistent()=true`, `RoomTaskStartRequest.isPersistent()=true` 가 ECS provider stub 에 전달.
      - `provisionPersistent_whenAnotherPersistentRoomAlive_throwsConflict` — 기존 persistent 룸 row 존재 시 `ApiException(PERSISTENT_ROOM_ALREADY_EXISTS)` 던짐.
      - `markUnhealthyAndTerminate_skipsPersistentRoom` — `Room.isPersistent()=true` 인 row 호출 시 stub.stopTask / `markUnhealthy` 미호출 + status 그대로.
      - `markProvisioningTimedOut_skipsPersistentRoom` — 동일.
    - `manager/RoomReconciliationSchedulerTest` 확장:
      - `scan_heartbeatExpired_filtersOutPersistentRooms` — persistent 룸 1개 + 일반 룸 1개 모두 heartbeat 임계 초과 → `markUnhealthyAndTerminate` 는 일반 룸 1개만 호출.
      - `scan_provisioningExpired_filtersOutPersistentRooms` — persistent 룸 1개 + 일반 룸 1개 모두 provisioning 임계 초과 → `markProvisioningTimedOut` 일반 룸 1개만 호출.
    - `controller/RoomInternalCallbackControllerTest` 확장:
      - `createPersistent_withValidToken_returnsOkAndCallsManager` — `manager.provisionPersistent` 1회 호출 + 200 + `RoomResponse` body 의 `roomId` 검증.
      - `createPersistent_missingToken_returnsForbidden` — `manager.provisionPersistent` 미호출.
      - `createPersistent_conflict_returnsConflict` — `manager.provisionPersistent` 가 `ApiException.persistentRoomAlreadyExists()` 던지면 응답 409 + body `code: "PERSISTENT_ROOM_ALREADY_EXISTS"`.
      - `terminate_persistentRoom_returnsNoContentAndDoesNotCallManager` — `snapshot.isPersistent()=true` mock → `manager.terminate` 미호출 + 204.
    - `domain/RoomTest` (없으면 신규): `open_setsIsPersistentFalse`, `openPersistent_setsIsPersistentTrue` 단위 테스트 1쌍.

### B. Unity Dedicated Server 단계

15. **`RoomServerCallbackConfig` 확장** — `Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackConfig.cs`:
    - 신규 상수 `public const string EnvIsPersistent = "ROOM_IS_PERSISTENT";`
    - 신규 프로퍼티 `public bool IsPersistent { get; }`
    - 생성자에 `bool isPersistent` 파라미터 추가.
    - `TryParse` 에서 `GetTrimmedOrNull(env, EnvIsPersistent)` 으로 raw 획득 → `bool.TryParse(raw, out bool parsed)` 가 false 면 `false` 기본값 (안전 — env key 누락 시 일반 룸으로 취급해 lifecycle 정상 동작).

16. **`RoomServerCallbackReporter.IsPersistent` 프로퍼티 노출** — `Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs`:
    - `public bool IsPersistent => _config != null && _config.IsPersistent;` 한 줄 추가.
    - 기존 frame loop (`HeartbeatLoop`, `ReportReadyRoutine`, `ReportTerminated`) 동작 변경 없음 — persistent 룸도 heartbeat 은 정상 송신해야 backend `last_heartbeat_at` 가 갱신되어 reconciliation 의 `is_persistent=false` 필터와 정합 (이중 안전망).

17. **`RoomAuthority.OnPlayerLeft` persistent skip 분기 추가** — `Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs` line 158-169:
    ```csharp
    if (!runner.ActivePlayers.Any())
    {
        RoomServerCallbackReporter reporter = GetComponent<RoomServerCallbackReporter>();
        if (reporter != null && reporter.IsPersistent)
        {
            Debug.Log("[RoomAuthority] OnPlayerLeft: persistent room, skipping Shutdown.");
            return;
        }
        if (reporter != null && reporter.IsActive)
        {
            StartCoroutine(TerminateAndShutdownRoutine(runner, reporter));
        }
        else
        {
            _ = runner.Shutdown();
        }
    }
    ```

18. **`RoomServerBootstrap` 변경 없음** — `_callbackReporter` 가 같은 GameObject 에 부착되므로 `GetComponent` 접근 가능. Initialize 흐름은 그대로 `RoomServerCallbackConfig.FromProcessEnvironment()` 가 새 env key 까지 파싱.

### C. 운영 매뉴얼 박제

19. **`docs/dev/operations/persistent-demo-room.md` 신규** — 디렉토리 부재 시 함께 생성. 내용은 1줄 절차 + cURL 예시 2쌍:
    - 생성: `curl -X POST https://api.mu-rang.com/internal/rooms/persistent -H "X-Internal-Token: $SECRET" -H "Content-Type: application/json" -d '{"ownerUserId":1,"photonSessionName":"demo-2026-q2","maxPlayers":8,"roomRuntimeVersion":"v0.1.0"}'` → 201 + roomId 박기.
    - 종료: `curl -X POST https://api.mu-rang.com/internal/rooms/{roomId}/terminate -H "X-Internal-Token: $SECRET" -H "Content-Type: application/json" -d '{"reason":"demo-end"}'` → persistent 가드가 idempotent 204 반환 → **별도 path** (decision 의 "명시적 종료 수동 절차") 필요: 운영자가 SQL 직접 `UPDATE rooms SET is_persistent = FALSE WHERE room_id = ?` 한 후 `/terminate` 재호출 → 정상 종료 흐름. 매뉴얼에 SQL 한 줄 박제.
    - 생성 충돌 시 (이미 살아있는 persistent 룸) 응답 예시: `HTTP/2 409 + {"code":"PERSISTENT_ROOM_ALREADY_EXISTS"}` 박기.

### asmdef 의존 확인

- `Assets/Multiplayer/Scripts/Room/Server/` 폴더는 `.asmdef` 없음 → Assembly-CSharp 기본 컴파일. 신규 import 없음 — 모두 기존 namespace 내부 변경. asmdef reference 추가 단계 없음.

## Deliverables

### 신규 파일

- `backend/src/main/resources/db/migration/V6__add_is_persistent_to_rooms.sql` — `ALTER TABLE rooms ADD COLUMN is_persistent BOOLEAN NOT NULL DEFAULT FALSE;`
- `backend/src/main/java/com/murang/room/controller/dto/RoomCreatePersistentRequest.java` — record `{ @Positive Long ownerUserId, @NotBlank @Size(max=128) @Pattern(...) String photonSessionName, @Min(1) @Max(32) int maxPlayers, @Size(max=64) String passwordHash, @NotBlank @Size(max=64) String roomRuntimeVersion }`.
- `backend/src/test/java/com/murang/room/domain/RoomTest.java` — `open_setsIsPersistentFalse` / `openPersistent_setsIsPersistentTrue` (이미 존재하면 그 안에 추가).
- `docs/dev/operations/persistent-demo-room.md` — 1줄 절차 + cURL 생성/종료 예시 + SQL `UPDATE rooms SET is_persistent=FALSE` 한 줄.

### 수정 파일

- `backend/src/main/java/com/murang/room/domain/Room.java` — `isPersistent` 필드 + getter `isPersistent()` + 정적 팩토리 `openPersistent(...)` 추가.
- `backend/src/main/java/com/murang/room/repository/RoomRepository.java` — `findFirstByIsPersistentTrueAndClosedAtIsNull()` 추가.
- `backend/src/main/java/com/murang/room/runtime/RoomTaskStartRequest.java` — `URI terminateCallbackUrl` + `boolean isPersistent` 두 필드 추가.
- `backend/src/main/java/com/murang/room/manager/RoomCallbackUrlBuilder.java` — `terminateCallbackUrl(Long roomId)` 메서드 추가.
- `backend/src/main/java/com/murang/room/manager/RoomServerManager.java` — `provisionPersistent(RoomProvisioningCommand)` 인터페이스 추가.
- `backend/src/main/java/com/murang/room/manager/RoomServerManagerImpl.java` — `provisionPersistent` 구현 + 기존 `provision` 과 공유 `provisionInternal(command, isPersistent)` private 헬퍼로 재구조화, `markUnhealthyAndTerminate` / `markProvisioningTimedOut` 진입 가드 추가.
- `backend/src/main/java/com/murang/room/manager/RoomServerSnapshot.java` — `boolean isPersistent` 필드 + `of(Room, RoomServerInstance)` 매핑 갱신.
- `backend/src/main/java/com/murang/room/manager/RoomReconciliationScheduler.java` — `RoomRepository` 주입 + heartbeat / provisioning 분기에 `is_persistent=false` stream filter.
- `backend/src/main/java/com/murang/room/runtime/ecs/EcsRoomRuntimeProvider.java` — `ENV_IS_PERSISTENT` + `ENV_TERMINATE_CALLBACK_URL` 두 키 + `buildEnvironment` 항목 2건 추가.
- `backend/src/main/java/com/murang/room/controller/RoomInternalCallbackController.java` — `/terminate` 의 idempotent 조건 `OR snapshot.isPersistent()`, 신규 `POST /persistent` 메서드.
- `backend/src/main/java/com/murang/common/exception/ErrorCode.java` — `PERSISTENT_ROOM_ALREADY_EXISTS` 추가.
- `backend/src/main/java/com/murang/common/exception/ApiException.java` — `persistentRoomAlreadyExists()` 정적 팩토리.
- `backend/src/test/java/com/murang/room/manager/RoomServerManagerImplTest.java` — `provisionPersistent` 2건 + `markUnhealthyAndTerminate_skipsPersistentRoom` / `markProvisioningTimedOut_skipsPersistentRoom` 2건 추가.
- `backend/src/test/java/com/murang/room/manager/RoomReconciliationSchedulerTest.java` — `scan_heartbeatExpired_filtersOutPersistentRooms` + `scan_provisioningExpired_filtersOutPersistentRooms` 2건 추가.
- `backend/src/test/java/com/murang/room/controller/RoomInternalCallbackControllerTest.java` — `createPersistent` 3건 + `terminate_persistentRoom_returnsNoContent` 1건 추가.
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackConfig.cs` — `EnvIsPersistent` 상수 + `IsPersistent` 프로퍼티 + `TryParse` 파싱.
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs` — `public bool IsPersistent` 프로퍼티 1줄 노출.
- `Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs` — `OnPlayerLeft` 마지막 플레이어 분기에 `reporter.IsPersistent` skip 가드 추가.
- `docs/specs/multiplayer-network/specs/03-room-session.md` — `## Implementation Plans` 표에 본 plan 행 추가.

## Acceptance Criteria

- [ ] `[auto-hard]` backend Gradle 빌드가 신규/수정 파일을 포함해 컴파일 에러 없이 통과한다.
  **검증:** `./gradlew :backend:compileJava :backend:compileTestJava` 종료코드 0.

- [ ] `[auto-hard]` Flyway V6 마이그레이션이 Spring boot 컨텍스트 로딩 시 적용되어 `rooms.is_persistent BOOLEAN NOT NULL DEFAULT FALSE` 컬럼이 생긴다.
  **검증:** `./gradlew :backend:test --tests "com.murang.room.controller.RoomInternalCallbackControllerTest"` 종료코드 0 (Spring context boot 가 V6 적용 + JPA validate 가 컬럼 매칭 강제). 추가로 `Grep "is_persistent" backend/src/main/resources/db/migration/V6__add_is_persistent_to_rooms.sql` 1줄 hit.

- [ ] `[auto-hard]` `RoomTest` 가 `Room.open(...)` 의 `isPersistent=false` 와 `Room.openPersistent(...)` 의 `isPersistent=true` 를 검증한다.
  **검증:** `./gradlew :backend:test --tests "com.murang.room.domain.RoomTest"` 종료코드 0 + `open_setsIsPersistentFalse` / `openPersistent_setsIsPersistentTrue` 메서드명 결과 XML 포함.

- [ ] `[auto-hard]` `RoomServerManagerImplTest` 의 신규 4건 (`provisionPersistent` 정상/충돌, `markUnhealthyAndTerminate` persistent skip, `markProvisioningTimedOut` persistent skip) 이 통과한다.
  **검증:** `./gradlew :backend:test --tests "com.murang.room.manager.RoomServerManagerImplTest"` 종료코드 0 + 신규 4 메서드명 결과 XML 포함.

- [ ] `[auto-hard]` `RoomReconciliationSchedulerTest` 의 신규 2건 (heartbeat / provisioning persistent filter) 이 통과한다.
  **검증:** `./gradlew :backend:test --tests "com.murang.room.manager.RoomReconciliationSchedulerTest"` 종료코드 0 + 신규 2 메서드명 결과 XML 포함.

- [ ] `[auto-hard]` `RoomInternalCallbackControllerTest` 의 신규 4건 (`/persistent` 정상·403·409, `/terminate` persistent skip) 이 통과한다.
  **검증:** `./gradlew :backend:test --tests "com.murang.room.controller.RoomInternalCallbackControllerTest"` 종료코드 0 + 신규 4 메서드명 결과 XML 포함.

- [ ] `[auto-hard]` `EcsRoomRuntimeProvider.buildEnvironment` 가 `ROOM_IS_PERSISTENT` + `ROOM_TERMINATE_CALLBACK_URL` 두 env 를 ContainerOverride 에 포함한다.
  **검증:** `./gradlew :backend:test --tests "com.murang.room.runtime.ecs.EcsRoomRuntimeProviderTest"` 종료코드 0 + `Grep "ROOM_IS_PERSISTENT|ROOM_TERMINATE_CALLBACK_URL" backend/src/main/java/com/murang/room/runtime/ecs/EcsRoomRuntimeProvider.java` 2개 이상 hit. 기존 테스트가 신규 env 를 검증하지 않으면 본 plan 에서 `runTask_includesPersistentAndTerminateEnvVariables` 단위 테스트 1건 추가.

- [ ] `[auto-hard]` Unity Editor 컴파일 결과 (DS scripts) 에 에러가 없고 `RoomServerCallbackConfig.IsPersistent`, `RoomServerCallbackReporter.IsPersistent` 심볼이 존재한다.
  **검증:** Unity MCP `read_console` 후 0 errors + `Grep "public bool IsPersistent" Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackConfig.cs` 1줄 hit + `Grep "public bool IsPersistent" Assets/Multiplayer/Scripts/Room/Server/RoomServerCallbackReporter.cs` 1줄 hit.

- [ ] `[auto-soft]` 일반 룸 생성 API `POST /api/v1/rooms` 의 `RoomCreateRequest` DTO 에 `persistent` 필드가 노출되지 않는다.
  **검증:** `Grep "persistent" backend/src/main/java/com/murang/room/controller/dto/RoomCreateRequest.java` 0 hit + 기존 `RoomControllerTest` 모두 PASS (테스트가 새 필드 도입을 깨지 않음을 확인).

- [ ] `[auto-soft]` `RoomResponse` DTO 에 `isPersistent` 필드가 노출되지 않는다 (decision Consequences 5번과 정합 — 운영자만 알 정보).
  **검증:** `Grep "persistent" backend/src/main/java/com/murang/room/controller/dto/RoomResponse.java` 0 hit.

- [ ] `[manual-hard]` AWS dev 환경에서 운영자 cURL 로 `POST /internal/rooms/persistent` 호출 → 200 + roomId 반환 + DB `SELECT room_id, is_persistent, closed_at FROM rooms WHERE is_persistent = TRUE AND closed_at IS NULL` 가 1 row 반환. ECS task 가 정상 ready 도달 후 클라이언트 2명 입장 → 둘 다 퇴장해도 30초 후 + 5분 후 DB `is_persistent=TRUE, closed_at IS NULL` 유지, ECS task `lastStatus=RUNNING` 유지, `room_server_instances.status` 가 `ACTIVE` 또는 `READY`.
  **검증:** EC2 SSH → 첫 cURL → response body `{ "roomId": X, ... }` 박제 → Quest 2대 또는 Editor 2대로 합류 후 둘 다 퇴장 → 5분 후 `SELECT status, last_heartbeat_at FROM room_server_instances WHERE room_id=X` 가 `(READY|ACTIVE, 30초 이내)`, `aws ecs describe-tasks --tasks <arn>` 가 `lastStatus=RUNNING`, Spring 로그에 `markUnhealthyAndTerminate skipped (persistent room)` 또는 reconciliation 의 filter 통과 로그 grep hit (그리고 `ds-callback:last-player-left` 로그는 발사되지 않아야 함).

- [ ] `[manual-hard]` 이미 살아 있는 persistent 룸이 있는 상태에서 두 번째 `POST /internal/rooms/persistent` 호출 시 409 + body `{"code":"PERSISTENT_ROOM_ALREADY_EXISTS"}` 반환.
  **검증:** EC2 cURL `-i` 옵션으로 첫 생성 → 200 박제 → 같은 명령 재실행 → `HTTP/2 409 + code: "PERSISTENT_ROOM_ALREADY_EXISTS" / detail: "이미 살아 있는 persistent 룸이 있습니다."` 응답 박제.

- [ ] `[manual-hard]` 운영 매뉴얼 절차 (수동 종료) 가 실제로 동작한다 — persistent 룸 row 의 `is_persistent=FALSE` UPDATE SQL 후 `/terminate` 콜백 발사 → status `TERMINATED` + `closed_at` 셋팅 + ECS task `STOPPED`.
  **검증:** EC2 SSH → `mysql -e "UPDATE rooms SET is_persistent = FALSE WHERE room_id = X;"` → `curl -X POST /internal/rooms/X/terminate -H "X-Internal-Token: $SECRET" -d '{"reason":"demo-end"}'` → 204 → 30초 후 `SELECT status, closed_at FROM room_server_instances WHERE room_id=X` 가 `(TERMINATED, 시각)` + `aws ecs describe-tasks` 가 `lastStatus=STOPPED`. `docs/dev/operations/persistent-demo-room.md` 의 SQL 한 줄이 정확하게 그 cURL 흐름과 일치하는지 확인.

## Out of Scope

- **ECS task 자체 crash 시 자동 재시작** (desired_count 기반 ECS Service 전환) — decision 05 Out of Scope 명시. 본 plan 은 정책 박제까지만, 후속 plan 후보.
- **동시 persistent 룸 N개 운영** — decision 05 Out of Scope. 본 plan 은 동시 1개 강제 (`findFirstByIsPersistentTrueAndClosedAtIsNull` 가드).
- **일반 유저가 클라이언트 UI 로 persistent 룸을 생성하는 흐름** — `RoomCreateRequest` DTO 에 옵션 노출 X. 본 plan 은 internal endpoint 만.
- **persistent 룸의 콘텐츠·배치 차이** — default 씬 + 기배치 오브젝트 그대로. 본 plan 은 lifecycle 분기만.
- **`X-Internal-Token` 외 별도 운영자 ACL** — decision 의 운영자 채널 정책 답습. 향후 audit logging / IP 화이트리스트 추가는 별도 spec 후보.
- **운영자 채널의 audit log 박제** — Spring `logging.pattern` 기본 출력으로만 남기고 별도 audit table 은 만들지 않는다.
- **persistent 룸의 통계/관측 metric (uptime, current players)** — `aws-dev-topology` plan 의 CloudWatch metrics 작업 범위에 위임. 본 plan 은 일반 룸과 동일 metric path 만 보장.
- **DS 측 `RoomServerAutomationMonitor` 의 persistent 분기** — `RoomServerAutomationMonitor` 는 일반 룸의 자동화 시나리오 트리거이지, persistent 룸은 사용자가 직접 들락날락하는 것이므로 별도 분기 불필요 (현재 흐름이 자연스럽게 정합).

## Notes

- decision 05 의 Consequences 6건 + 본 plan 이 발견한 *DS 측 persistent 전파* 분기 (Approach 4) = 총 7개 변경점. DS 분기가 누락되면 정책이 실제로 동작하지 않아 ghost room 가 다시 발생하므로 본 plan 의 핵심 안전장치다.
- ghost-room plan Handoff 박제는 "ECS RunTask 가 `ROOM_TERMINATE_CALLBACK_URL` env 를 ready/heartbeat 와 동일 빈도로 주입할 것" 이라 적었으나 실제 `EcsRoomRuntimeProvider.buildEnvironment` 코드에는 없다 — 본 plan 의 Approach 13 에서 함께 닫는다. M1/M2 manual-hard 가 pending 인 이유 중 하나일 가능성 있음.
- `Room.openPersistent` 의 `ownerUserId` 는 운영자의 user_id 를 그대로 넘긴다 — 일반 룸과 동일 구조이지만 의미는 "이 룸을 만든 운영자". RoomCreatePersistentRequest 가 `@Positive Long ownerUserId` 를 명시 요구.
- `roomRepository.findFirstByIsPersistentTrueAndClosedAtIsNull()` 는 `closed_at IS NULL` 조건이므로 운영자가 SQL `UPDATE rooms SET is_persistent=FALSE WHERE room_id=X` 만 한 상태에서 (`closed_at` 아직 null) 두 번째 persistent 룸을 만들려고 시도하면 ❗️ first row 가 여전히 매치되어 409 가 반환된다. 운영 매뉴얼은 "UPDATE → /terminate → 30s 대기" 순서를 박제해야 함.
- reconciliation scheduler 의 `findFirstByIsPersistentTrueAndClosedAtIsNull` 같은 단일 쿼리 대신 batch `findAllById` + stream filter 방식을 채택한 이유: heartbeat-timeout 후보가 (사실상 1~2건 이내) 매우 적고, `room_server_instances` 와 `rooms` 의 join 쿼리를 derived query 로 표현하면 컨벤션이 흐트러진다. 후보 row 수가 많아지는 시점에 `@Query` JPQL 로 재작성 후보.
- `RoomServerCallbackConfig.TryParse` 의 `EnvIsPersistent` 누락 시 false 기본값 → 일반 룸 lifecycle 로 fallback (안전 측). 운영자 환경에서 env 누락은 ECS task definition 검증으로 잡힌다.
- 본 plan 의 manual-hard M1/M2/M3 모두 AWS dev 환경 한정 — 사용자 직접 검증 항목이고 mock 회귀로는 대체 불가. M1 은 5분 이상 대기가 필요 (heartbeat-timeout = 90s + 안전 마진).
- `docs/dev/operations/persistent-demo-room.md` 신규 디렉토리: 기존 `docs/dev/` 가 없는 경우 함께 생성. `docs/specs/` 와 분리해 운영 메모 위주로 모아둔다.

## Handoff

### Auto AC (commit `abc2764`, 2026-05-27)

- AC#1 Gradle compileJava + compileTestJava BUILD SUCCESSFUL.
- AC#2 Flyway V6 적용 — 다른 테스트의 Spring context boot 가 JPA `ddl-auto=validate` 로 컬럼 매칭 강제 통과 + `V6__add_is_persistent_to_rooms.sql` 파일 존재.
- AC#3 `RoomTest`: 2/2 PASS (`open_setsIsPersistentFalse`, `openPersistent_setsIsPersistentTrue`).
- AC#4 `RoomServerManagerImplTest`: 14/14 (신규 4건 `provisionPersistent_savesPersistentRoomWithFlag` / `provisionPersistent_whenAnotherPersistentRoomAlive_throwsConflict` / `markUnhealthyAndTerminate_skipsPersistentRoom` / `markProvisioningTimedOut_skipsPersistentRoom` 포함).
- AC#5 `RoomReconciliationSchedulerTest`: 6/6 (신규 2건 + 기존 4건 — 단 `fakeRoom()` 헬퍼에 `ReflectionTestUtils.setField(room, "roomId", roomId)` 추가 필요했음, `Room.roomId` 가 `@GeneratedValue(IDENTITY)` 라 fake 객체에선 null 이라 scheduler 의 `Map<Long, Room>` lookup 이 깨지는 문제).
- AC#6 `RoomInternalCallbackControllerTest`: 12/12 (신규 4건 `/persistent` 3건 + `/terminate` persistent skip 1건 포함).
- AC#7 `EcsRoomRuntimeProviderTest`: 12/12 (신규 `runTask_includesPersistentAndTerminateEnvVariables` 1건 포함). `ROOM_IS_PERSISTENT` + `ROOM_TERMINATE_CALLBACK_URL` 두 env 가 ContainerOverride 에 정상 주입.
- AC#8 Unity unity-test-runner EditMode 133/133 PASS (Murang.Multiplayer.Room.Tests 포함), 컴파일 에러 0건. `RoomServerCallbackConfig.IsPersistent` / `RoomServerCallbackReporter.IsPersistent` 심볼 grep hit.
- AC#9 `RoomCreateRequest` 에 `persistent` 키워드 0 hit.
- AC#10 `RoomResponse` 에 `persistent` 키워드 0 hit + manual-hard AC#12 의 200 응답 body 에서도 `isPersistent` 필드 미노출 확인.

### Manual-hard AC#12 / AC#13 PASS (2026-05-27 02:25~02:28 UTC, EC2 `ip-10-10-1-18`)

**Pre-check**: spring 컨테이너 재빌드 (`docker compose -f docker-compose.ec2-dev.yml --env-file ~/.env.aws-dev up -d --build spring`), `Started MurangApplication in 16.69 seconds`. `POST /internal/rooms/persistent` 빈 body → `HTTP/2 400 VALIDATION_REQUEST` 으로 endpoint 노출 확인. INTERNAL_TOKEN length=64 prefix `5b369f1e...`.

**AC#12** (두 번째 persistent 생성 시 409):
- 첫 cURL `{"ownerUserId":1,"photonSessionName":"persistent-ac12-test","maxPlayers":8,"roomRuntimeVersion":"v0.1.0"}` → **HTTP/2 200** + body `{"data":{"roomId":29,"status":"SERVER_STARTING",...}}` (traceId `ef154f49-48f6-4a1c-b225-7d503adcadcd`). **추가 확인**: `RoomResponse` body 에 `isPersistent` 필드 미노출 — AC#10 wire 응답에서도 검증됨.
- 두 번째 cURL `{"...photonSessionName":"persistent-ac12-second",...}` → **HTTP/2 409** + `{"code":"PERSISTENT_ROOM_ALREADY_EXISTS","detail":"이미 살아 있는 persistent 룸이 있습니다."}` (traceId `bc809807-63df-44fe-b4f0-a0f00fd2bbb8`).

**AC#13** (매뉴얼 종료 절차 — UPDATE → /terminate → TERMINATED):
- (보너스) SQL UPDATE *없이* `/terminate` 호출 → **HTTP/2 204** + DB 상태 `is_persistent=1, closed_at=NULL, status=READY` 유지 — `/terminate` 의 persistent 가드 idempotent skip 분기 동작 확인 (traceId `d730bd02-8792-4db7-8dfa-8450dec36445`).
- `UPDATE rooms SET is_persistent = FALSE WHERE room_id = 29;` 실행.
- `/terminate` 재호출 `{"reason":"ac13-cleanup"}` → **HTTP/2 204** (traceId `a2e20099-4032-438b-86f2-03ba1175c7a0`).
- DB 최종 상태 — `rooms.is_persistent=0, closed_at=2026-05-27 02:28:41.466432`, `room_server_instances.status=TERMINATED, terminated_at=2026-05-27 02:28:41.466432`. `terminate` 가 동기 처리 (별도 대기 불필요).

**예상 외 확인**: AC#13 (b) 시점에 `status=READY` 였다는 건 ECS RunTask + Photon Fusion 세션 부팅 + ready callback 까지 정상 동작했단 강한 시그널 — ECS 인프라 자체는 살아있음.

### Manual-hard AC#11 PASS (2026-05-27 09:21~09:29 UTC, EC2 ip-10-10-1-18 + Quest 실기기)

DS 이미지 재빌드 (Unity Linux Headless Server → Docker → ECR tag `v0.1.14` → ECS task definition revision) 완료 후 검증:

- persistent 룸 `perf-measure-test` (roomId=30) 생성 직후 ECS RunTask 가 새 image (`v0.1.14`) 로 부팅, status `SERVER_STARTING` → `READY` 도달.
- Quest 1대 합류 (JOIN_TIME=09:21:12) → 1~2분 활동 (손 움직임 + 악기 인터랙션) → 단독 퇴장 (LEAVE_TIME=09:24:04, 마지막 플레이어) → 5분 무인 대기 (09:24~09:29).
- 5분 후 (09:29:25) 검증:
  - `rooms.is_persistent=1, closed_at=NULL` ✓
  - `room_server_instances.status=READY, last_heartbeat_at=09:29:22.901428` (3초 신선) → DS 0명 상태에서도 heartbeat 정상 송신 ✓
  - ECS `lastStatus=RUNNING` ✓
  - Spring 로그 grep `reconciliation|ds-callback|markUnhealthy|persistent` **0 hit** → 4중 가드 동시 정합:
    - `RoomReconciliationScheduler.scan()` 의 `is_persistent=false` 필터로 룸 30 청소 후보 추출 안 됨 (backend 측 가드)
    - DS 의 `RoomAuthority.OnPlayerLeft` 의 `reporter.IsPersistent` skip 가드로 `/terminate` 콜백 미발사 + Shutdown skip (**DS 측 IsPersistent 가드 정상 동작 — 본 plan 의 핵심 안전장치**)
    - `markUnhealthyAndTerminate skipped (persistent room)` 0 hit → helper 자체가 호출 안 됨 (scheduler 필터 단계에서 막힘)
    - `persistent` 키워드 자체 0 hit → reconciliation 로그가 출력 안 됨 (필터링 후 후보 0건)

**2인 Quest 확장 검증** (팀원 합류 시점):
- 사용자 + 팀원 같은 룸 합류 → 손 동기화 + MIDI sync 활성.
- hand-pose payload 1차 축소 plan (commit `b3aa95d`) 과 합쳐 본 사이클 핵심 가치 확인 — **"비정상적으로 긴 latency 해소" 사용자 직접 보고 PASS**.

### 다음 plan 이 알아야 할 산출

- `Room.openPersistent(...)` / `RoomRepository.findFirstByIsPersistentTrueAndClosedAtIsNull()` 시그니처 박제.
- `RoomServerCallbackConfig.EnvIsPersistent` (`"ROOM_IS_PERSISTENT"`) / `RoomServerCallbackReporter.IsPersistent` / `RoomAuthority.OnPlayerLeft` persistent skip 가드 신규 심볼.
- `RoomTaskStartRequest` 가 8-인자 record 로 확장됨 (`terminateCallbackUrl`, `isPersistent` 추가).
- `EcsRoomRuntimeProvider.buildEnvironment` 가 `ROOM_IS_PERSISTENT` + `ROOM_TERMINATE_CALLBACK_URL` 두 env 주입 — ghost-room plan handoff 의 미완 잔여였던 terminate URL 도 본 plan 에서 함께 wire-up.
- 운영 매뉴얼 경로 `docs/dev/operations/persistent-demo-room.md` — 발표/시연 사전 작업 가이드의 단일 진실원.

### 후속 plan 후보

- ECS Service 전환으로 persistent 룸 task 자동 재시작 (desired_count=1) — 사용자 결정으로 dev 상시 사용은 abandon, 시연 한정 정책 유지 시 본 후속도 우선순위 낮음.
- persistent 룸 uptime CloudWatch metric.
- 운영자 채널 audit log (`/internal/rooms/persistent` 호출 이력 저장).
- ghost-room plan 의 manual-hard 잔여 (M1~M3 — heartbeat-timeout 청소, 정상 퇴장 콜백, provisioning-timeout) 도 본 사이클에 함께 검증 가능 — `~/.env.aws-dev` + `docker-compose.ec2-dev.yml` 환경 그대로 사용.
