# Stick No-Penetration — Velocity-Based Surface Stop

**Sub-Spec:** [`02-stick-no-penetration.md`](../specs/02-stick-no-penetration.md)
**Status:** `Draft`
**Date:** 2026-05-10

## Components

- **AnchoredStickGhostFollower** (기존, 갱신) — stick root에 부착된 추종 컴포넌트. attach 중 ghost wrist를 추종하는 책임은 유지하되, 동작이 transform 직접 덮어쓰기에서 Rigidbody driver(velocity·angularVelocity 설정) 호출로 바뀐다. Bind 단계에서 Rigidbody 모드 전환을 수행한다.
- **GhostAnchor** (신규, 빈 GameObject) — stick prefab 자식의 빈 GameObject. stick-local pose가 prefab-fixed이며 wrist pivot 역할을 한다. driver는 GhostAnchor의 world pose가 ghost wrist의 world pose와 일치하도록 stick을 움직인다.
- **Stick Rigidbody** (기존, 모드 변경) — attach 중 `isKinematic=false`, `useGravity=false`로 동작한다. detach 시점엔 stick 자체가 Destroy되므로 모드 복원은 불요.
- **DrumPiece SolidCollider** (기존, 무변경) — 각 drum piece prefab(Snare/HiHat/Crash/Ride/BassDrum/FloorTom/MidTom/HighTom)에 hit zone trigger와 분리된 solid collider가 이미 존재한다. 본 설계는 layer 변경 없이 광범위 통과 방지로 간다.
- **DrumKitStickAnchor** (기존, 미세 변경) — Bind/Detach 라이프사이클 자체는 그대로 유지. Bind 호출 경로에 Rigidbody 모드 토글이 추가될 뿐.
- **StickHitSweeper** (기존, 무변경) — BoxCastAll 기반 hit 감지 그대로. hit velocity source가 `ghostFollower.Velocity`(ghost wrist 속도)라 stick이 막혀 멈춰도 hit 임계 영향 없음.

## Data / Control Flow

- DrumKitStickAnchor.AttachSticks → AnchoredStickGhostFollower.Bind → Rigidbody 모드 전환(`isKinematic=false`, `useGravity=false`) → GhostAnchor의 stick-local 변환 1회 캐시 → stick을 ghost wrist world pose로 1회 정렬(초기 어긋남 0).
- 매 FixedUpdate (AnchoredStickGhostFollower) → ghost wrist world pose에서 target stick world pose 역산(`ghostWrist.world × ghostAnchorLocalToRoot⁻¹`) → 현재 Rigidbody pose와의 위치·회전 delta 계산 → `linearVelocity = dPos / Time.fixedDeltaTime`, `angularVelocity = dRot / Time.fixedDeltaTime` 으로 driver 적용.
- 표면 접촉 시 → stick의 non-kinematic Rigidbody가 drum piece solid collider에 막혀 물리 엔진이 자연 정지 → 사용자 입력 위치(ghost wrist)와 stick 시각 위치 사이 어긋남이 자동 발생·유지(sub-spec What 4항).
- 표면 이탈 시 → ghost wrist와 GhostAnchor의 위치 차이가 다시 0으로 수렴할 수 있는 상태가 됨 → driver가 자연스럽게 stick을 ghost로 끌어당겨 추종 재개(sub-spec Behavior 2번).
- Hit 감지 흐름 → StickHitSweeper.FixedUpdate가 BoxCastAll로 DrumHitZone trigger 콜라이더를 스윕 → DrumHitZone.TryProcessHit이 `ghostFollower.Velocity` 기반으로 hit 임계 판정. 본 설계로 stick의 실제 이동이 막혀도 ghost wrist 속도는 그대로라 hit 임계 영향 없음.
- Detach 시 → DrumKitStickAnchor.Detach → PlayHandPoseDriver.PopSourceOverride → stick instance Destroy. Rigidbody 모드 복원 불요.

## Boundaries

- **건드린다**:
  - `Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs` — SyncToGhost가 transform 직접 덮어쓰던 로직을 FixedUpdate 기반 Rigidbody driver로 교체. Bind에서 Rigidbody 모드 전환 추가.
  - drum stick prefab들 (`drum_stick_L_Sanyo`, `drum_stick_R_Sanyo`, 비-Sanyo 변형 포함) — 자식 GhostAnchor 빈 GameObject 추가. Rigidbody 직렬값(useGravity, isKinematic 초기값) 정합성.
- **건드리지 않는다**:
  - drum piece prefab들의 collider 구조 / layer (광범위 정책이라 layer 변경 불요).
  - DrumHitZone의 trigger collider와 hit 판정 로직.
  - StickHitSweeper의 BoxCastAll 로직과 velocity source 결정.
  - PlayHandPoseDriver의 push/pop priority chain (ARD 02 유지).
  - hands/02-instrument-no-penetration sub-spec — 손-표면 통과 방지는 별개.
  - 드럼킷 외 환경 객체의 layer / Physics matrix.
  - detach 직후 stick의 외부 공간 거동 (즉시 Destroy로 발생하지 않음).

## Invariants

- Attach 중 stick Rigidbody는 항상 `isKinematic=false`, `useGravity=false`다. 이 모드에서만 표면 충돌이 작동한다.
- GhostAnchor의 stick-local pose는 prefab-fixed이며 Bind 이후 변형되지 않는다.
- Driver는 FixedUpdate에서 `linearVelocity`·`angularVelocity`만 설정한다. `transform.position`·`rotation` 직접 변경은 금지 — 그 순간 물리 엔진의 충돌 처리가 무력화된다.
- ghost source는 driver에 의해 변형되지 않는다 (단방향: ghost → stick).
- Attach 중 XRGrabInteractable은 disabled 상태를 유지한다 (ARD 02).
- Hit 임계 판정에 사용되는 속도는 stick의 Rigidbody velocity가 아니라 ghost wrist에서 산출한 `ghostFollower.Velocity`다.

## Assumptions

- 각 drum piece prefab에 hit zone trigger와 분리된 solid BoxCollider(`m_IsTrigger: 0`)가 이미 존재한다 — 출처: `Read Assets/Instruments/Drum/Prefabs/Snare_Sanyo.prefab (2026-05-10)`.
- Stick prefab(`drum_stick_L_Sanyo` 등)은 stick root에 BoxCollider 1개 + Rigidbody 1개 + GripPoseHand 자식을 갖는다 — 출처: `Read Assets/Instruments/Drum/Prefabs/drum_stick_L_Sanyo.prefab (2026-05-10)`.
- `AnchoredStickGhostFollower.Velocity`는 ghost wrist의 frame 간 위치 변화로 산출되며 stick의 실제 이동과 무관하다 — 출처: `Read Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs (2026-05-10)`.
- DrumKit anchor 정렬 후 사용자의 stick 도달 범위에 사용자 관찰을 깰 만한 드럼킷 외 solid 환경 객체가 들어오지 않는다 — 광범위 통과 방지 채택의 근거. 이 가정이 깨지면 본 sub-spec Trade-offs에 따라 sub-spec을 갱신하고 layer 분리로 전환한다.
- ARD 02의 PlayHand source override는 stick 안 wrist의 stick-local pose가 변하지 않는 한 유지된다. 본 driver는 stick world pose만 바꾸고 stick 내부 계층은 건드리지 않으므로 영향 없음 — 출처: `Read docs/specs/drum-stick/decisions/02-stick-hand-coupling.md (2026-05-10)`.

## Open Tech Decisions

- [ ] **Velocity Driver 적용 방식** — Rigidbody의 `linearVelocity`·`angularVelocity`를 직접 할당하는 방식과 `MovePosition`·`MoveRotation`을 사용하는 방식 사이 분기. 전자는 충돌 시 자연 정지에 직관적이나 jitter·과속 가능, 후자는 sweep 보장 특성이 다르고 non-kinematic에서의 의미가 약하다. 후속 ARD 1건으로 닫는다.
- [ ] **GhostAnchor 셋업 메커니즘** — stick prefab에 빈 GameObject를 미리 직렬화(prefab-fixed)하는 방식과 Bind 시점에 ghost wrist를 1회 sample해 stick-local 변환을 동적 캐시하는 방식 사이 분기. 후속 ARD 1건으로 닫는다.
