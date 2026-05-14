# Stick GripPoseHand 정렬 역산

**Linked Spec:** [`01-anchor-auto-attach-detach.md`](../specs/01-anchor-auto-attach-detach.md)
**Status:** `Done`

## Goal

`AnchoredStickGhostFollower`가 stick root의 world pose를 ghost wrist에 단순 매칭하던 것을, **`stick.GripPoseHand/L_Wrist`(또는 `R_Wrist`)가 ghost wrist에 정확히 일치하도록 stick root의 world pose를 역산**하는 식으로 바꾼다. 그 결과 (a) stick body가 손에서 자연스러운 방향으로 뻗고, (b) `PlayHandPoseDriver`가 source root(=`GripPoseHand`)를 따라가면서 동시에 ghost wrist와 일치 → PlayHand가 컨트롤러와 정렬된다.

## Context

### 선행 plan에서 박제된 결손

선행 plan [`2026-05-02-sanyoentertain-anchor-auto-attach-detach.md`](../../_archive/drum-stick/plans/2026-05-02-sanyoentertain-anchor-auto-attach-detach.md)의 Handoff "stick GripPoseHand 정렬 결손"에서 다음이 박제됐다.

> `AnchoredStickGhostFollower`가 `stick.transform.SetPositionAndRotation(ghostWristSource.position/rotation)` 단순 매칭이라 `stick.GripPoseHand`의 prefab-내부 local offset만큼 stick body가 손에서 어긋난 방향으로 뻗고, PlayHand world pose가 ghost wrist world pose와 일치하지 않는다(컨트롤러와 어긋나 보임). 올바른 정렬은 `stick.transform.world = ghostWrist.world × (stick.GripPoseHand.localToRoot)⁻¹`.

선행 plan의 manual-hard 4건은 모두 pass (=lifecycle이 동작) 이지만, 시각 정렬은 부정확하다. 본 plan은 그 시각 정렬만 닫는 분리된 후속 작업이다 (검증 실패 분기가 아니므로 `Caused By` 헤더는 부착하지 않는다).

### 정렬 식 도출

- 목표: `stickHandWristRoot.world = ghostWrist.world` (PlayHand가 source wrist를 따라가도록 한 ARD 02의 결합 모델에서, ghost wrist와 source wrist가 일치하면 PlayHand world pose도 ghost wrist에 일치).
- `stickHandWristRoot`는 stick root 의 자식 (`drum_stick_L/GripPoseHand/L_Wrist`). 따라서 `stickHandWristRoot.world = stickRoot.world × stickHandWristRoot.localToRootMatrix` (여기서 `localToRootMatrix` 는 stick root 기준 누적 local→root 변환).
- 두 식을 만족시키려면: `stickRoot.world = ghostWrist.world × (stickHandWristRoot.localToRootMatrix)⁻¹`.
- prefab-내부 `GripPoseHand`/`L_Wrist` local pose 는 prefab 직렬화 정적 값 → Bind 시점 1회 캐시 가능. 이후 stick root world pose 만 매 frame 재계산.

### 박제된 ARD 결정

- **ARD 02 — Stick↔손 결합은 PlayHand source override 모델.** stick prefab 의 `GripPoseHand` child wrist 를 `PushSourceOverride(stickHandRoot, stickHandWristRoot)` 로 push. PlayHand 의 `syncRootTransform=true` 에서 PlayHand root 의 world pose = source root (=stick의 GripPoseHand) world pose. 본 plan 의 정렬 식은 이 결합을 깨지 않는 범위 안에서 stick root 만 조정한다.
- **ARD 04 — `manage_prefabs` / `manage_components` MCP 1차.** 본 plan 은 prefab/scene 변경이 없는 코드 1개 수정이라 MCP 호출 없이 텍스트 Edit 으로 완결.

### 호출 외부 API side effect 박제

- `Transform.SetPositionAndRotation(Vector3 pos, Quaternion rot)`: stick root 의 world position/rotation 을 한 번에 set. 자식(`GripPoseHand` 포함) 은 local 보존된 채 새 world pose 로 따라온다. local 값이 prefab-fixed 인 `GripPoseHand` 와 그 자식 `L_Wrist` 는 stick root 의 새 world pose 에 그대로 attached. 즉 stick root world pose 만 결정하면 `stickHandWristRoot.world` 는 자동 결정된다.
- `PlayHandPoseDriver` frame loop 동작 (선행 plan 박제 그대로): `LateUpdate` 와 `OnBeforeRender` 에서 `SyncDesiredSourceIfChanged` → `TryEnsureInitialized` → `SyncPose` 실행. `syncRootTransform=true` 면 PlayHand root world pose = `m_ActiveSourceRoot` (=`stick.GripPoseHand` GameObject 의 transform) world pose. — `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-03)` line 184-195 `SyncPose` / `SyncRootTransform`. 따라서 본 plan 의 stick root world pose 변경이 → `GripPoseHand` world → PlayHand world 까지 같은 frame 안에 propagate.
- `AnchoredStickGhostFollower` 자체 frame loop: `[DefaultExecutionOrder(10005)]` (PhysicsHandGhostFollower 10000 → 본 follower 10005 → PlayHandPoseDriver 10010). `LateUpdate` 와 `Application.onBeforeRender` 양쪽에서 `SyncToGhost` 호출. 본 plan 은 `SyncToGhost` 의 식을 교체할 뿐 hook 시점은 그대로. `OnEnable`/`OnDisable` 의 onBeforeRender 등록·해제 동작 보존.
- `Bind(Transform ghostWristSource, PlayHandPoseDriver playHandDriver, Transform stickHandWristRoot)` 동작: 3 필드 set + `Rigidbody.isKinematic=true` + `XRGrabInteractable.enabled=false`. 본 plan 은 Bind 호출 시점에 `stickHandWristRoot` 가 stick root 의 자손이라는 사실을 이용해 prefab-fixed local 변환을 1회 캐시. — `Read Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs (2026-05-03)`.

## Verified Structural Assumptions

- `drum_stick_L.prefab` variant root 의 hierarchy: root `drum_stick_L` → child `GripPoseHand` (Transform + GripPoseHandPreview) → child `L_Wrist` (Transform, OpenXR 표준 25-joint hand 의 wrist). `R_Wrist` 는 mirror prefix. `L_Wrist`/`R_Wrist` 의 stick root 기준 local pose 는 prefab 직렬화 정적 값 (런타임 동안 변하지 않음). — `unity-scene-reader 보고 (2026-05-03, manage_prefabs get_hierarchy on Assets/Instruments/Drum/Prefabs/drum_stick_L.prefab — 30 items, root child = GripPoseHand → L_Wrist + 25 hand bones + PreviewMesh/HandMeshPreview)`
- 선행 plan 적용 결과로 `drum_stick_L/R.prefab` variant root 에 `AnchoredStickGhostFollower` 가 1개 부착돼 있고, `GripPoseHand/PreviewMesh/HandMeshPreview` 에 `HidePhysicsHandInPlayMode` 가 부착돼 있다 (variant root componentTypes: Transform, MeshFilter, MeshRenderer, Rigidbody, XRGrabInteractable, GripPoseProvider, BoxCollider, AnchoredStickGhostFollower). — `unity-scene-reader 보고 (2026-05-03, 위 동일 호출 결과)`
- `AnchoredStickGhostFollower.Bind(...)` 의 세 번째 매개 `Transform stickHandWristRoot` 는 본 follower 가 부착된 GameObject(stick root) 의 자손 transform 이다 (`DrumKitStickAnchor.BindStickAndPushOverride` 가 `stickInstance.transform.Find("GripPoseHand").Find("L_Wrist"|"R_Wrist")` 로 찾아 전달). 즉 `stickHandWristRoot` 의 `Matrix4x4.Inverse(transform.worldToLocalMatrix) × stickHandWristRoot.localToWorldMatrix` 같은 누적 식이 stick root 기준 local pose 를 준다. — `Read Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs (2026-05-03)` line 84-105 `BindStickAndPushOverride`.
- `PlayHandPoseDriver.syncRootTransform` 직렬화 default = `true` (필드 declaration `[SerializeField] bool syncRootTransform = true;`). PlayHand root 의 world pose 가 매 frame source root world pose 로 강제된다. 본 plan 의 정렬 식이 만족되면 `GripPoseHand.world = ghostWrist.world` 이고 `PlayHand.world = ghostWrist.world` 가 동시에 성립. — `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-03)` line 36-37, 192-195.
- `Matrix4x4.TRS(pos, rot, Vector3.one) * Matrix4x4.Inverse(localMatrix)` 같은 행렬 합성 후 `Matrix4x4.GetPosition()` / `Matrix4x4.rotation` 으로 stick root world pose 추출 가능. UnityEngine 6000.3 의 `Matrix4x4` 는 `rotation` (Quaternion) / `GetPosition()` (Vector3) 둘 다 public. 동등하게 Transform 연산 (`Quaternion`/`Vector3` 단계별 합성) 으로도 도출 가능 — 본 plan 은 행렬 한 단계가 코드 가독성·수치 안정 양쪽에서 단순. — `Read Library/PackageCache/com.unity.modules.{}/... 또는 UnityEngine.CoreModule (2026-05-03, 표준 API 박제)`.

## Approach

### 단계 1 — `AnchoredStickGhostFollower.cs` Bind 시점에 local 변환 캐시

`Bind(...)` 본문에 다음 추가:

- `m_StickHandWristRoot` 가 non-null 이면 **stick root 기준 누적 local matrix** 를 1회 계산해 필드에 캐시.
  - `m_WristLocalToRoot = transform.worldToLocalMatrix * m_StickHandWristRoot.localToWorldMatrix;`
  - 위 식은 (현재 frame 의 stick root world pose) 와 (현재 frame 의 wrist world pose) 의 비를 stick root 의 local space 로 환산 → wrist 의 stick-root-local 변환 행렬.
  - prefab-fixed 정적 pose 이므로 1회 캐시면 충분. 이후 frame 에서 stick root 가 어디로 옮겨가도 `m_WristLocalToRoot` 는 불변.
- 캐시 실패 (wrist null) 케이스는 follower 가 fallback 으로 *기존 식* (단순 ghost wrist 매칭) 을 쓰도록 둔다 — Bind 가 wrist 인자를 항상 채워 보내는 게 정상 경로지만, 방어.

### 단계 2 — `SyncToGhost` 식 교체

기존:
```csharp
transform.SetPositionAndRotation(m_GhostWristSource.position, m_GhostWristSource.rotation);
```

교체:
```csharp
if (m_HasWristCache)
{
    var ghostWorld = Matrix4x4.TRS(m_GhostWristSource.position, m_GhostWristSource.rotation, Vector3.one);
    var stickWorld = ghostWorld * m_WristLocalToRoot.inverse;
    transform.SetPositionAndRotation(stickWorld.GetPosition(), stickWorld.rotation);
}
else
{
    // fallback: 기존 단순 매칭 (Bind 가 wrist 를 못 받았을 때만)
    transform.SetPositionAndRotation(m_GhostWristSource.position, m_GhostWristSource.rotation);
}
```

식 의미: `stickRoot.world = ghostWrist.world × wristLocalToRoot⁻¹` → 이 stick root 위에서 prefab-fixed `GripPoseHand/L_Wrist` 를 다시 따라가면 wrist 의 world pose 가 정확히 `ghostWrist.world` 가 된다.

scale 은 `Vector3.one` 로 고정 — drum_stick variant root 의 `localScale` 은 prefab 직렬화에서 `(1,1,1)` 가 default 이고, 본 plan 은 scale 변경을 다루지 않는다. (만약 향후 stick scale 이 1 이외가 되면 식의 행렬 부분을 `transform.localScale` 로 갱신해야 하나, 현재 범위 밖.)

### 단계 3 — 회귀 방지

- `LateUpdate` 와 `OnBeforeRender` 양쪽에서 `SyncToGhost` 호출하는 hook 은 그대로. 본 plan 은 식만 교체.
- `OnEnable`/`OnDisable` 의 `Application.onBeforeRender` 등록·해제 보존.
- `Rigidbody.isKinematic=true` / `XRGrabInteractable.enabled=false` Bind 시점 호출 보존.
- `[DefaultExecutionOrder(10005)]` 보존 (PhysicsHand 10000 → 본 follower 10005 → PlayHand 10010 의 frame 순서가 유지돼야 stick root 가 set 된 뒤 PlayHand 가 source root 를 읽는다).

## Deliverables

- `Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs` — Bind 시 wrist 의 stick-root-local 변환을 1회 캐시. SyncToGhost 식을 `stickRoot = ghostWrist × wristLocalToRoot⁻¹` 로 교체.

## Acceptance Criteria

- [ ] `[auto-hard]` `AnchoredStickGhostFollower.cs` 컴파일 성공 — `read_console types=error filter_text=AnchoredStickGhostFollower` 0 매치 + `editor_state.isCompiling=false`.
- [ ] `[auto-hard]` `AnchoredStickGhostFollower.cs` 의 `SyncToGhost` 본문에 `m_GhostWristSource.position/rotation` 을 stick root 에 그대로 set 하는 단일 라인 (`transform.SetPositionAndRotation(m_GhostWristSource.position, m_GhostWristSource.rotation)`) 이 정상 경로 (= wrist cache 가 채워진 경우) 에서 사용되지 않는다 — grep 으로 해당 라인이 fallback 분기 (`else` 또는 `!m_HasWristCache`) 안에만 존재함을 단일 매치 확인.
- [ ] `[auto-hard]` 본 plan 적용 후 `drum_stick_L.prefab` / `drum_stick_R.prefab` variant root 의 `AnchoredStickGhostFollower` 컴포넌트가 여전히 1개 부착돼 있다 (`manage_prefabs get_hierarchy` 결과 root item `componentTypes` 에 `AnchoredStickGhostFollower` 단일 매치) — 코드 변경이 prefab 직렬화에 영향 주지 않음을 가드.
- [ ] `[auto-hard]` 본 plan 적용 후 `DrumKitAnchor` 의 `TeleportationAnchor.m_TeleportTrigger == 0` 보존 (= OnSelectExited per `BaseTeleportationInteractable.TeleportTrigger`). 선행 plan AC 의 enum 의도 값 회귀 가드.
- [ ] `[manual-hard]` Play 모드 진입 → drum anchor 로 텔레포트 → 도착 직후 양손에 들린 stick 이 손에서 자연스러운 방향으로 뻗는다 (이전: stick body 가 손과 어긋난 방향으로 뻗어 prefab-내부 offset 만큼 떠 있는 어색함 — 본 plan 후 stick 의 grip 위치 = 손바닥 위치).
- [ ] `[manual-hard]` 위 상태에서 `LeftPlayHand` / `RightPlayHand` 의 world pose 가 사용자의 컨트롤러 위치 (= ghost wrist) 와 시각적으로 일치한다 (이전: PlayHand 가 stick.GripPoseHand 의 world pose 를 따라가 ghost wrist 와 어긋나 보임 — 본 plan 후 stick.GripPoseHand.world = ghostWrist.world 이므로 PlayHand 도 컨트롤러에 정렬). Editor Game View 또는 헤드셋 시점에서 한 손씩 좌우로 흔들어 컨트롤러 위치와 PlayHand 의 시각 위치가 함께 따라가는 것을 육안 확인.
- [ ] `[manual-hard]` drum anchor 외부로 텔레포트 → stick detach 동작이 선행 plan 과 동일하게 보존된다 (양손에서 동시 사라짐). 본 plan 이 attach 식만 교체하고 detach 경로는 건드리지 않았음을 회귀 검증.

## Out of Scope

- `DrumKitAnchor` GameObject 의 Y rotation 보정 → 동시 작성 plan [`2026-05-03-sanyoentertain-drum-anchor-forward-rotation.md`](./2026-05-03-sanyoentertain-drum-anchor-forward-rotation.md).
- `stick.transform.localScale` 이 `(1,1,1)` 가 아닐 경우의 정렬 — 현재 prefab 직렬화 default = `(1,1,1)` 이므로 본 plan 은 scale 변환을 식에서 제외.
- attach 도중 controller↔hand-tracking 입력 모드 전환의 ghost wrist 재바인딩 — 선행 plan Notes 의 후속 plan 후보 그대로.
- stick 이 드럼 부품을 통과하는지의 통과 방지 → `02-stick-no-penetration.md`.

## Notes

- `Matrix4x4.GetPosition()` / `Matrix4x4.rotation` 은 Unity 2021.2+ 에서 사용 가능 (현재 6000.3.10f1 충족). 만약 이전 Unity 버전 호환을 요구하는 일이 생기면 `MultiplyPoint3x4` + `Quaternion.LookRotation` 으로 풀어 쓸 수 있다.
- 단계 1 의 캐시 변환은 prefab-fixed 라 1회로 충분하지만, 만약 prefab 의 GripPoseHand local pose 가 향후 변하면 캐시는 자동 따라간다 (Bind 시점 = Instantiate 직후이므로 새 prefab 값을 잡는다). 즉 본 plan 의 캐시 전략은 prefab pose 변경에 robust.
- 본 plan 은 코드 1 파일 수정. prefab/scene 직렬화 변경 없음. plan-implementer 가 commit 단위 = AnchoredStickGhostFollower.cs 1 파일.
