# Stick Velocity Driver — Surface Stop via Non-Kinematic Rigidbody

**Linked Spec:** [`02-stick-no-penetration.md`](../specs/02-stick-no-penetration.md)
**Status:** `Done`

## Goal

attach 중 stick의 ghost 추종 메커니즘을 transform 직접 덮어쓰기에서 non-kinematic Rigidbody의 velocity·angularVelocity 직접 할당으로 교체해, 사용자의 손 입력은 끊지 않으면서 stick이 drum piece solid collider에 막혀 표면에서 자연 정지하도록 만든다. 부수적으로 양손 stick끼리는 `Physics.IgnoreCollision` 1회 호출로 서로 통과시킨다.

## Context

sub-spec 01(`01-anchor-auto-attach-detach`)이 끝난 시점의 attach 중 stick은 매 frame `transform.SetPositionAndRotation`으로 ghost wrist world pose에 강제 정렬되며 `isKinematic=true`다. 이 모드에서는 drum piece solid collider가 있어도 stick이 그대로 통과한다 — sub-spec 02 What 1·2(통과 방지·표면 정지)가 깨진다.

Tech Spec(`tech-specs/02-stick-no-penetration.md`, Accepted 2026-05-10)은 attach 중 Rigidbody를 `isKinematic=false` + `useGravity=false`로 두고 매 FixedUpdate에 ghost wrist world pose에서 target stick world pose를 역산해(`ghostWrist.world × wristLocalToRoot⁻¹`) Rigidbody에 velocity·angularVelocity를 할당하는 driver 패턴을 박제한다. ARD 05는 driver 메서드를 `linearVelocity`/`angularVelocity` 직접 할당으로 닫고(MovePosition은 채택 안 함), ARD 06은 driver의 추종 기준점을 별도 GhostAnchor 없이 기존 `GripPoseHand/L_Wrist`(또는 `R_Wrist`) transform 재사용으로 닫는다 — `m_WristLocalToRoot` 캐시 패턴 그대로 유지. ARD 07은 양손 stick끼리의 collider 충돌을 `Physics.IgnoreCollision(L.collider, R.collider, true)` 1회 런타임 호출로 비활성화하도록 닫는다 (prefab/Physics matrix 무변경).

ARD 04(prefab 수정 경로)에 따라 stick prefab의 Rigidbody 직렬값 갱신은 `manage_prefabs`/`manage_components` MCP 우선이고, ARD 06에 따라 변경 대상은 `_Sanyo` 접미사 변형 한정 (`drum_stick_L_Sanyo`, `drum_stick_R_Sanyo`, `drum_stick_Sanyo`).

본 plan은 self-contained 한 세션 분량으로, 코드 1파일 갱신·코드 1파일에 1줄 추가·prefab 1개(base prefab `drum_stick.prefab`이 아닌 base의 직렬값을 override 한 형태로 `_Sanyo` variant들에 한정 적용)에 한정한다.

## Verified Structural Assumptions

- `AnchoredStickGhostFollower` 현재 코드는 Bind 시점에 `m_WristLocalToRoot = transform.worldToLocalMatrix × wrist.localToWorldMatrix`를 캐시하고 `Rigidbody.isKinematic=true` + `XRGrabInteractable.enabled=false`를 강제. SyncToGhost는 LateUpdate / OnBeforeRender에서 `transform.SetPositionAndRotation`으로 ghost world pose 적용. `Velocity` public property는 ghost wrist의 frame 간 위치 변화에서 산출(stick 실제 이동과 무관) — 출처: `Read Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs (2026-05-10)`.
- `DrumKitStickAnchor.AttachSticks`는 좌·우 stick prefab을 Instantiate해 두 stick instance를 양쪽 모두 알고 있는 단일 진입점이며, `BindStickAndPushOverride`로 follower.Bind + driver.PushSourceOverride를 호출. Detach는 PopSourceOverride → Destroy 순서 — 출처: `Read Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs (2026-05-10)`.
- `drum_stick.prefab` (base prefab, `_Sanyo` 3개 variant 모두의 source) 직렬값: `m_Layer: 9`, `m_Mass: 0.15`, `m_UseGravity: 1`, `m_IsKinematic: 0`, `m_Interpolate: 1`, `m_CollisionDetection: 2 (Continuous)`. BoxCollider 1개(`m_IsTrigger: 0`, Size=0.02×0.02×0.37, Center≈(0,0,0.007)) — 출처: `Read Assets/Instruments/Drum/Prefabs/drum_stick.prefab (2026-05-10)`.
- `drum_stick_L_Sanyo.prefab` / `drum_stick_R_Sanyo.prefab`은 base prefab `drum_stick.prefab` (guid `fe086597558106146ab900d46b9d5a59`)의 variant이며 PrefabInstance modification에 위치·회전·이름·`AnchoredStickGhostFollower` 추가만 있고 Rigidbody 직렬값 override는 없음 (즉 base의 `m_UseGravity: 1` / `m_IsKinematic: 0`을 그대로 상속). `drum_stick_Sanyo.prefab`도 같은 base의 variant — 출처: `Read Assets/Instruments/Drum/Prefabs/drum_stick_L_Sanyo.prefab (2026-05-10)`.
- 8개 drum piece `_Sanyo` prefab(`Snare/HighTom/MidTom/FloorTom/CrashCymbal/RideCymbal/BassDrum/HiHat`) 모두 ≥2개의 BoxCollider 보유 (`m_IsTrigger: 0` solid collider + `m_IsTrigger: 1` hit zone trigger 분리). HiHat은 3개 — 출처: `Grep m_IsTrigger Assets/Instruments/Drum/Prefabs/*_Sanyo.prefab (2026-05-10)`.
- `Assets/Instruments/Instruments.asmdef`는 `Unity.InputSystem`, `Hands`, `RhythmGame.Data`, `Unity.XR.Interaction.Toolkit` reference를 갖는다. 본 plan이 import하는 namespace(UnityEngine, UnityEngine.XR.Interaction.Toolkit.*)는 모두 기존 reference로 충당되며 신규 reference 추가는 불요. 본 plan은 신규 C# 파일을 생성하지 않으므로 asmdef 변경 0건 — 출처: `Read Assets/Instruments/Instruments.asmdef (2026-05-10)`.

## Approach

1. **`AnchoredStickGhostFollower.cs` driver 교체** (`Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs` 직접 Edit — 코드 파일이라 ARD 04 prefab MCP 정책 미적용).
   - `Bind`에서 `Rigidbody.isKinematic=true` 강제 → `isKinematic=false; useGravity=false`로 변경. (Tech Spec Components·Invariants 박제 그대로.)
   - `Bind` 마지막에 stick의 초기 world pose를 ghost wrist world pose 1회 정렬(`stickWorld = ghostWorld × m_WristLocalToRoot⁻¹`). `DrumKitStickAnchor.AttachSticks`가 이미 Instantiate 직후 ghost wrist pose로 1회 SetPositionAndRotation을 하지만, 그 시점은 wrist transform 기준이 아니라 ghost wrist 기준이라 stick 내부 wrist의 stick-local 차이만큼 어긋난다 → `Bind` 시점 1회 재정렬로 초기 어긋남 0 보장.
   - `LateUpdate`/`OnBeforeRender`의 `SyncToGhost` 호출 제거. `transform.SetPositionAndRotation` 호출 자체를 driver 경로에서 추방 (Tech Spec Invariants: "transform.position·rotation 직접 변경 금지").
   - `FixedUpdate`를 driver로 전환:
     - 기존 `m_Velocity` 산출 로직(ghost wrist frame 간 위치 변화 → public Velocity)은 그대로 유지 (`StickHitSweeper`가 hit 임계 판정에 본 값을 쓰므로 Tech Spec Components 박제대로 무변경).
     - 추가로 `ghostWorld = TRS(m_GhostWristSource.position, m_GhostWristSource.rotation, one)` 계산 → `targetStickWorld = ghostWorld × m_WristLocalToRoot⁻¹` → `dPos = targetPos − rb.position`, `dRot = targetRot × Quaternion.Inverse(rb.rotation)` → `rb.linearVelocity = dPos / Time.fixedDeltaTime`, `rb.angularVelocity = ToAngularVelocity(dRot, Time.fixedDeltaTime)` (axis-angle 변환, 180°+ wrap 처리: `if (angle > 180f) angle -= 360f`).
     - `m_HasWristCache=false` fallback 경로는 driver 적용 시 `targetStickWorld = ghostWorld` 사용 (현행 SyncToGhost fallback과 동일 의미).
   - `[DefaultExecutionOrder(10005)]` 보존 (sub-spec 01 후속 plan handoff에 박제된 PhysicsHand 10000 → follower 10005 → PlayHand 10010 순서 유지).
   - jitter clamp/deadzone 같은 추가 파라미터는 ARD 05 Consequences 박제 따라 본 plan에서 도입하지 않는다 (필요 시 후속 plan으로 분리).

2. **`DrumKitStickAnchor.cs`에 stick-stick collision ignore 추가** (직접 Edit, ARD 07).
   - `AttachSticks` 내부 두 `BindStickAndPushOverride` 호출 직후 한 곳에서 `Physics.IgnoreCollision(leftCol, rightCol, true)` 1회 호출. collider 참조는 각 stick instance의 `GetComponentInChildren<Collider>()` 또는 stick root 자체의 `GetComponent<Collider>()`로 획득 (base prefab `drum_stick.prefab` Layer 9 stick root에 BoxCollider 1개 박제 사실 사용).
   - Detach 복원 호출 불요 (Unity의 IgnoreCollision pair는 collider 객체 수명에 묶이고 detach 시 두 stick은 Destroy됨).
   - 한쪽 collider null fallback: 양쪽 모두 non-null일 때만 호출 (방어 1줄).

3. **`_Sanyo` 변형 stick prefab 3개 Rigidbody 직렬값 정합화** (`drum_stick_L_Sanyo.prefab`, `drum_stick_R_Sanyo.prefab`, `drum_stick_Sanyo.prefab`).
   - 현재 base `drum_stick.prefab` 직렬값이 `m_UseGravity: 1` / `m_IsKinematic: 0`인데 `_Sanyo` variant는 override 없음 → 즉 base 값을 그대로 상속 = attach 전 상태에서 중력 적용 + 비-kinematic. 본 sub-spec은 attach 중 driver가 모드를 강제하므로 prefab 직렬값은 attach 전(=Instantiate 직후 Bind 호출 사이의 1 frame) 의미만 가진다. 안전하게 `m_UseGravity: 0` + `m_IsKinematic: 0`으로 prefab 직렬값을 정합화 (driver 의도와 일치, attach 전 1-frame 동안의 잠깐 낙하 방지).
   - 변경 도구: ARD 04에 따라 **`manage_prefabs`/`manage_components` MCP 우선**. MCP가 base prefab의 Rigidbody 직렬값을 `_Sanyo` variant에서 override 추가 형태로 처리 가능하면 그대로 진행. propertyPath `m_UseGravity` 필드 override가 MCP 표면에서 안 잡히면 (Rigidbody는 일반적으로 `manage_components`로 잘 잡히는 표면이라 가능성 낮음) plan 본문 fallback 경로 — sub-agent 단독 판단 금지, 사용자 승인 후 prefab YAML 직접 Edit으로 PrefabInstance.m_Modifications에 m_UseGravity·m_IsKinematic override 2개 항목 추가.
   - 비-Sanyo 변형(`drum_stick_L`, `drum_stick_R`, `drum_stick`)은 ARD 06 박제대로 건드리지 않는다.

4. **컴파일·검증** ([`unity-mcp-workflow`](../../../../.claude/skills/unity-mcp-workflow/SKILL.md) 절차 따라):
   - `manage_script`로 두 .cs 갱신 → `editor_state`의 `isCompiling` 폴링 → `read_console`로 컴파일 에러 확인.
   - 사용자 헤드셋 검증으로 Behavior 1·2·3·4 확인 (수동, 자동 검증 어려움).

## Deliverables

- `Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs` — driver 메서드 transform 덮어쓰기 → FixedUpdate 기반 velocity·angularVelocity 할당으로 교체. Bind 시점 Rigidbody 모드 전환(`isKinematic=false`, `useGravity=false`) + 초기 정렬 1회 추가. SyncToGhost LateUpdate/OnBeforeRender 호출 제거.
- `Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs` — `AttachSticks` 내 두 BindStickAndPushOverride 호출 직후 `Physics.IgnoreCollision(leftStickCollider, rightStickCollider, true)` 1회 호출 추가.
- `Assets/Instruments/Drum/Prefabs/drum_stick_L_Sanyo.prefab` — Rigidbody `m_UseGravity` override `0` 추가 (PrefabInstance modification으로).
- `Assets/Instruments/Drum/Prefabs/drum_stick_R_Sanyo.prefab` — 동일.
- `Assets/Instruments/Drum/Prefabs/drum_stick_Sanyo.prefab` — 동일.

## Acceptance Criteria

- [ ] `[auto-hard]` `AnchoredStickGhostFollower.cs`와 `DrumKitStickAnchor.cs` 둘 다 컴파일 성공 (read_console에서 신규 error 0건).
- [ ] `[auto-hard]` `AnchoredStickGhostFollower.cs`의 `Bind` 메서드가 `Rigidbody.isKinematic=false`와 `useGravity=false` 두 줄을 모두 가진다 (텍스트 grep 검증, 새 driver 사상 박제 확인).
- [ ] `[auto-hard]` `AnchoredStickGhostFollower.cs`에서 `transform.SetPositionAndRotation` 호출이 Bind 시 초기 정렬 1곳을 제외하고 SyncToGhost / LateUpdate / OnBeforeRender 경로에서 제거됨 (Tech Spec Invariants "transform.position·rotation 직접 변경 금지" 준수, 텍스트 grep로 호출 위치 수 확인).
- [ ] `[auto-hard]` `DrumKitStickAnchor.cs`의 `AttachSticks` 흐름에 `Physics.IgnoreCollision(` 호출 1회 존재 (텍스트 grep 검증).
- [ ] `[auto-hard]` 3개 `_Sanyo` stick prefab 모두 Rigidbody `m_UseGravity: 0` 직렬값을 갖는다 (prefab YAML grep 또는 manage_prefabs read 검증).
- [ ] `[manual-hard]` (Behavior 1) DrumKit anchor에 텔레포트해 양손 스틱이 부착된 상태에서 사용자가 손을 드럼 표면 안쪽으로 밀어 넣어도 스틱이 표면에서 멈춰 보이며 표면 안쪽으로 들어가지 않는다 (8개 drum piece 모두에서 적어도 하나 이상 확인 — Snare/Crash/Ride/HiHat/BassDrum 우선).
- [ ] `[manual-hard]` (Behavior 2) 표면에 닿아 멈춰 있던 스틱이 사용자가 손을 표면에서 떼어 위로 들어 올리면 다시 손 위치를 따라가 자연스럽게 표면에서 떨어진다.
- [ ] `[manual-hard]` (Behavior 3) 한 손 스틱으로 드럼 표면을 빠르게 연속 타격할 때 연주에 지장을 줄 만한 레이턴시·떨림 없이 다음 타격이 인식된다 (StickHitSweeper의 hit 판정이 끊기지 않음을 함께 관찰).
- [ ] `[manual-hard]` (Behavior 4) 양손 스틱이 같은 영역에서 만났을 때 두 스틱이 서로 통과하며 막히지 않는다.
- [ ] `[auto-soft]` Play Mode 진입 후 anchor에 attach하지 않은 상태에서 잠깐 동안 (Instantiate 후 Bind 호출 사이) `_Sanyo` stick prefab들의 시각 위치가 중력으로 떨어지지 않는다 (prefab `m_UseGravity: 0` 정합화 효과 — 실패 시 Notes 기록 후 다음 plan으로).
- [ ] `[auto-soft]` (What 4 — 통과 방지에 따른 ghost-stick 어긋남 허용 검증) Play Mode에서 attach 상태로 stick을 drum 표면에 닿게 한 뒤, ghost wrist source(`m_GhostWristSource.position`)와 stick `Rigidbody.position`(또는 `transform.position`) 사이의 거리가 0보다 큰 frame이 1 frame 이상 관측된다 (간이 디버그 로그 또는 inspector watch로 확인). 이 어긋남은 통과 방지 결과로 발생하는 자연스러운 결과이며 본 plan이 의도적으로 허용한다 — 이 어긋남 때문에 Behavior 1·2·3이 깨지지 않는 것은 위 manual-hard AC들이 별도 보장한다. (사용자가 헤드셋을 착용하지 않은 상태에서도 Editor Play Mode에서 손 입력 없이 anchor만 attach시켜 stick과 drum surface가 겹치도록 anchor 위치를 임시 조정해 관측 가능. 검증 비용이 과하면 Behavior 1 manual 검증 중 inspector로 함께 관측해도 충족.)

## Out of Scope

- velocity 할당 시 jitter·과속 완화 파라미터(max-velocity clamp / deadzone / 회전 smoothing). ARD 05 Consequences 박제 따라 본 plan은 도입하지 않는다 — 사용자 헤드셋 관찰에서 jitter가 사용자 경험을 깨면 후속 plan으로.
- drum piece prefab의 collider 구조 / layer 변경 (Tech Spec Boundaries 박제 — 광범위 통과 방지 정책이라 layer 분리 불요).
- Physics matrix / 신규 layer 추가 (ARD 07 박제 — `Physics.IgnoreCollision` 1회 호출만).
- Stick과 PhysicsHand 간 collider 관계 — `hands/02-instrument-no-penetration` sub-spec 책임.
- 비-Sanyo 변형 drum stick prefab(`drum_stick_L`, `drum_stick_R`, `drum_stick`) — ARD 06 박제 따라 건드리지 않는다.
- 환경 객체(드럼킷 외 solid)와의 stick 충돌 — Tech Spec Assumptions 박제 (드럼킷 anchor 부근 사용자 stick 도달 범위에 사용자 관찰을 깰 만한 환경 객체가 없음 가정). 가정이 깨지면 sub-spec 갱신 후 후속 plan.
- Detach 시 Rigidbody 모드 복원 (Tech Spec Data/Control Flow 박제 — stick instance 자체가 Destroy되므로 복원 불요).

## Notes

- sub-spec 01 plan handoff 가정 박제: stick은 매 attach마다 spawn-destroy 모델 (ARD 03), `_Sanyo` 변형 한정 변경 (ARD 06), wrist 계층은 `GripPoseHand/L_Wrist`/`R_Wrist` (OpenXR 25-joint 표준).
- sub-spec 01 후속 plan(`2026-05-03-sanyoentertain-stick-gripposehand-alignment.md`)에서 `m_WristLocalToRoot` 캐시 패턴이 박제됐고, 본 plan은 해당 캐시를 driver 역산식에 그대로 재사용한다. 별도 `m_GhostAnchorLocalToRoot` 같은 두 번째 캐시 도입하지 않음 (ARD 06 Consequences).
- sub-spec 01 handoff에 "drum_stick_L_Sanyo Layer = 0 (Default)" 기록이 있으나 base prefab `drum_stick.prefab` 실제 직렬값은 `m_Layer: 9`. variant override로 Layer를 0으로 바꾸는 modification은 `drum_stick_L_Sanyo.prefab` PrefabInstance에 보이지 않음 → handoff 박제는 부정확한 것으로 본 plan은 base 박제 사실(Layer 9)을 사용. Layer 자체는 본 plan 변경 대상이 아니라 실제 영향은 없으나, 향후 plan이 stick layer를 가정으로 박제할 때 본 Notes를 출처로 참고.
- ARD 04 fallback 진입 조건: `manage_prefabs`/`manage_components`로 처리 못 하는 표면(예: PrefabInstance.m_Modifications에 m_UseGravity override 신규 항목 추가가 MCP에서 반영 안 됨, 또는 base prefab Rigidbody가 SerializeReference 다형 필드로 잡혀 표면 미노출) 발견 시 plan 본문에 fallback 명시한 뒤 사용자 승인 받아 prefab YAML 직접 Edit으로 진행. sub-agent 단독 판단 금지.
- `Physics.IgnoreCollision`은 collider 객체 수명에 묶이므로 stick instance가 Destroy되는 순간 자동 정리됨. 다음 attach에서 새 instance 한 쌍에 대해 다시 1회 호출 필요 (현행 AttachSticks 흐름이 매 attach마다 호출되므로 이 요구를 자연 만족).

## Handoff

### 코드/자산 산출물 (실제 적용 결과)

- `Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs` — driver 방식이 transform 덮어쓰기에서 FixedUpdate 기반 `Rigidbody.linearVelocity`/`angularVelocity` 직접 할당으로 교체. `Bind` 시 `isKinematic=false` + `useGravity=false`로 전환하고 ghost wrist 기준 stick world pose 1회 정렬 (line 66~67·89). `LateUpdate`/`OnBeforeRender`의 `SyncToGhost` 호출 제거 — transform 직접 변경 경로 완전 폐기 (Tech Spec Invariants 준수).
- `Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs` — `AttachSticks`에 `Physics.IgnoreCollision(leftCol, rightCol, true)` 1회 호출 추가 (line 108, ARD 07). collider 참조는 `GetComponentInChildren<Collider>()`로 획득, null 양쪽 fallback 가드.
- `Assets/Instruments/Drum/Prefabs/drum_stick_Sanyo.prefab` — Rigidbody 직접 직렬값 `m_UseGravity: 0` (line 120).
- `Assets/Instruments/Drum/Prefabs/drum_stick_L_Sanyo.prefab`, `drum_stick_R_Sanyo.prefab` — PrefabInstance.m_Modifications에 `propertyPath: m_UseGravity` + `value: 0` override 추가 (line 1104~1105). ARD 04 fallback 경로(YAML 직접 편집) 사용 — MCP `manage_prefabs`/`manage_components`가 PrefabInstance modification 신규 항목 추가를 처리 못 하는 표면이라 fallback 진입.

### 검증 결과

- `[auto-hard]` 1~5 모두 pass (read_console 0건, grep 매치 일치, prefab 직렬값/modification 일치).
- `[auto-soft]` 10 pass (정합화 정적 검증), 11 skipped (Behavior 1 manual 검증 시 inspector watch로 함께 관측 가능 — 메인 manual 통과로 간접 확인).
- `[manual-hard]` 6~9 모두 pass (사용자 헤드셋 검증).

### 후속 plan/sub-spec이 알아야 할 사실

- attach 중 stick Rigidbody 모드는 `isKinematic=false` + `useGravity=false` non-kinematic. driver는 FixedUpdate에서 `linearVelocity`/`angularVelocity`만 설정하며 `transform.position`/`rotation` 직접 변경은 금지 (Tech Spec Invariants).
- stick의 `Velocity` public property(ghost wrist frame 간 위치 변화 산출)는 무변경. StickHitSweeper의 hit 임계 판정에 본 값을 그대로 사용 (Tech Spec Components).
- 양손 stick collider IgnoreCollision pair는 collider 객체 수명에 묶이며 detach 시 stick instance Destroy로 자동 해제. 다음 attach마다 새 instance 한 쌍에 대해 재호출 필요 — 현행 AttachSticks 흐름이 매 attach마다 호출하므로 자연 만족.
- `_Sanyo` 변형 stick prefab 3개의 Rigidbody 직렬값은 attach 전(=Instantiate~Bind 사이 1 frame) 의미로 `m_UseGravity: 0` 정합화. 비-Sanyo 변형은 무변경 (ARD 06).
- jitter clamp/deadzone 등 velocity 완화 파라미터는 본 plan에 도입하지 않음 (ARD 05). 사용자 헤드셋 관찰에서 jitter가 경험을 깨면 후속 plan으로.
