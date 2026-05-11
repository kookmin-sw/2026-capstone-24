# Anchor Auto Attach/Detach

**Linked Spec:** [`01-anchor-auto-attach-detach.md`](../specs/01-anchor-auto-attach-detach.md)
**Status:** `Done`

## Goal

`DrumKitAnchor`(scene)에 텔레포트로 도착하는 순간 양손에 `drum_stick_L/R`을 spawn해 attach하고, 다음 텔레포트가 확정되는 순간 두 stick을 destroy해 detach한다. 그 사이 stick은 grip/trigger로 떼어지지 않고 손 포즈는 stick-잡기 포즈로 고정된다.

## Context

### Linked Spec 요지

`docs/specs/drum-stick/specs/01-anchor-auto-attach-detach.md`의 5개 What:
1. drum anchor 도착 시 양손에 동시 stick attach.
2. attach 상태에서 grip/trigger 입력으로 stick이 떨어지지 않음.
3. attach 상태에서 손은 stick-잡기 포즈 유지.
4. drum anchor 외부로 텔레포트 확정 시 양손 stick 동시 detach + 어디에도 보이지 않음.
5. 같은 anchor로 재도착 시 새로 attach.

### 박제된 ARD 결정 (단일 진실원: `docs/specs/drum-stick/decisions/`)

- **ARD 01 — Anchor 자체-컴포넌트로 attach/detach 신호 발행.** Drum anchor에 자체 MonoBehaviour를 두어 자기 자신의 `TeleportationAnchor` 이벤트로 attach 트리거를 만든다. detach는 "내가 attach 상태일 때 다음 텔레포트 시작" flag로 끊는다. broker / `TeleportationProvider.endLocomotion` 구독은 채택하지 않는다.
- **ARD 02 — Stick↔손 결합은 PlayHand source override 모델.** Stick에 신규 컴포넌트를 부착해 (a) Ghost wrist transform을 source로 frame 단위 추종, (b) stick prefab의 child로 stick-잡기 포즈 hand를 두고, (c) `PlayHandPoseDriver.PushSourceOverride(stickHandRoot, stickHandWristRoot)`로 grip override 슬롯 점유, (d) stick-hand는 `HidePhysicsHandInPlayMode` 패턴으로 렌더 OFF, (e) stick의 `XRGrabInteractable`은 attach 상태에서 사용하지 않는다.
- **ARD 03 — Instantiate / Destroy 모델.** 도착 시 anchor 컴포넌트가 보유한 좌·우 stick prefab 참조에서 매번 Instantiate, detach 시 두 인스턴스 모두 Destroy. 씬 기존 `drum_stick_L`(-24686) / `drum_stick_R`(-24542) 인스턴스는 본 plan 작업 중 SampleScene에서 제거한다.
- **ARD 04 — `manage_prefabs` / `manage_components` MCP 1차.** prefab 변경(컴포넌트 추가·필드 셋업·child 추가)은 MCP 우선. MCP가 못 처리하는 표면이 발견되면 plan 본문에 명시 후 사용자 승인.

### 현재 코드 자산 요지

- `Assets/Hands/Scripts/PlayHandPoseDriver.cs` 의 `PushSourceOverride(Transform newRoot, Transform newWristRoot)` / `PopSourceOverride()` 가 grip override 슬롯의 단일 진입점. priority 체인 `grip override > physics > ghost fallback > null` (RefreshActiveSource).
- `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` 가 ghost wrist를 source로 frame 단위 추종하는 패턴. 본 plan의 stick 신규 컴포넌트는 이 패턴을 답습하되 단일 source(ghost-only) + Rigidbody 비요구로 단순화.
- `Assets/Hands/Scripts/HidePhysicsHandInPlayMode.cs` 는 `[ExecuteAlways]` + `RequireComponent(SkinnedMeshRenderer)` 로 Edit 모드는 visible / Play 모드는 hidden. stick-hand 렌더 OFF는 본 컴포넌트를 그대로 재사용.
- `Assets/Hands/Scripts/GripPoseProvider.cs` 는 `XRGrabInteractable.selectEntered/Exited` 에서 PushSourceOverride/PopSourceOverride를 호출하는 reference 패턴. 본 plan은 같은 API를 anchor 트리거 경로에서 호출.

### 자산 구조 요지

- `Assets/Locomotion/Prefabs/InstrumentAnchor.prefab` 은 `TeleportationAnchor` + `BoxCollider` 만 담는 vanilla anchor (m_TeleportTrigger=0, m_MatchOrientation=2(TargetUpAndForward)). Piano/DrumKit 용 anchor는 본 prefab의 inherit가 아니라 SampleScene 안에 직접 배치된 root GameObject로 존재 — `DrumKitAnchor` (instanceID -24822, root, `Transform`+`BoxCollider`+`TeleportationAnchor`).
- `Assets/Instruments/Drum/Prefabs/drum_stick.prefab` 은 base prefab (`drum_stick` root, `Transform`+`MeshFilter`+`MeshRenderer`+`Rigidbody`+`XRGrabInteractable`+`GripPoseProvider`+`BoxCollider`). 자식 없음.
- `Assets/Instruments/Drum/Prefabs/drum_stick_L.prefab` / `_R.prefab` 은 base 의 prefab variant. variant root에 child `GripPoseHand`(`GripPoseHandPreview` 컴포넌트) → `L_Wrist` (또는 `R_Wrist`) + `PreviewMesh/HandMeshPreview` 가 부착돼 있고, wrist 자체에 OpenXR 표준 hand bone 계층이 있다 (예: `L_Wrist/L_IndexMetacarpal/L_IndexProximal/.../L_IndexTip`, `L_Palm`, `L_ThumbMetacarpal/...` 등). 본 plan은 이 wrist 계층을 ARD 02의 "stick-hand" wrist source로 그대로 재사용한다 — 신규 child 추가 없이 기존 GripPoseHand 자식을 PlayHand source로 push한다.

### XRI 박제 (Unity 6000.3.10f1 / com.unity.xr.interaction.toolkit@5f736ad4ccd8)

- `BaseTeleportationInteractable.TeleportTrigger` enum 정의: `OnSelectExited=0`, `OnSelectEntered=1`, `OnActivated=2`, `OnDeactivated=3`. `DrumKitAnchor`(scene) 의 직렬화된 `m_TeleportTrigger=0` (= OnSelectExited).
- `BaseTeleportationInteractable.teleporting` (UnityEvent<TeleportingEventArgs>) 는 `SendTeleportRequest` 가 `m_TeleportationProvider.QueueTeleportRequest` 에 성공한 직후 invoke된다. 즉 "anchor 도착 직후" 가 아니라 "anchor 가 텔레포트 요청을 큐에 올린 직후". 큐 자체는 `TeleportationProvider.Update` 다음 frame 까지 적용 대기. 본 plan에서는 attach 시점 = `selectExited` 직후 한 frame 안에 처리.
- `TeleportationAnchor.selectExited` 는 base XRI `XRBaseInteractable` 가 발행 — anchor 가 자기 자신의 select 이벤트로 trigger 시점을 받을 수 있다. attach 신호 source로 적합.
- detach 신호 source: `TeleportationProvider.locomotionStarted` 이벤트 (`LocomotionProvider` 에 정의 — `Action<LocomotionProvider>`). drum anchor 컴포넌트가 "내가 attach 상태"일 때 이 이벤트가 한 번 들어오면 detach 한다. 단, attach 가 발생한 그 텔레포트 자체가 `locomotionStarted` 를 발행하므로, attach 시점에 한 frame guard 가 필요 — "attach 시점 frame 의 `locomotionStarted` 는 무시" 로직 (per ARD 01 의 "다음 텔레포트가 시작되는 순간"). 구체 가드 방식은 Approach 단계 4 참고.
- `LocomotionProvider.locomotionStarted` 는 매 텔레포트마다 한 번 발행. drum 외 다른 anchor(`PianoAnchor` 등)로 텔레포트하든, 평면 `TeleportationArea`로 텔레포트하든 모두 source가 된다 — 이 통합 신호로 "drum 외부로 떠남" 을 단순히 잡을 수 있다.

## Verified Structural Assumptions

- `DrumKitAnchor` 는 SampleScene root GameObject (instanceID -24822), 컴포넌트 = `Transform` + `BoxCollider` + `TeleportationAnchor` (직렬화 `m_TeleportTrigger=0`, `m_MatchOrientation=2`). 본 plan 의 anchor 신규 컴포넌트는 이 root 에 add. — `unity-scene-reader 보고 (2026-05-02, find_gameobjects + manage_components)`
- `drum_stick_L.prefab` / `drum_stick_R.prefab` 는 base `drum_stick.prefab` 의 prefab variant. variant root 에 child `GripPoseHand/L_Wrist`(또는 `R_Wrist`) + 그 아래 OpenXR 표준 hand bone (`L_IndexMetacarpal/L_IndexProximal/L_IndexIntermediate/L_IndexDistal/L_IndexTip`, `L_LittleMetacarpal/...`, `L_MiddleMetacarpal/...`, `L_Palm`, `L_RingMetacarpal/...`, `L_ThumbMetacarpal/L_ThumbProximal/L_ThumbDistal/L_ThumbTip`) 이 직렬화돼 있다. R 측은 prefix `R_`. wrist 노드 자체가 변종별 source wrist root. — `unity-scene-reader 보고 (2026-05-02, manage_prefabs get_hierarchy)`
- `drum_stick_L/R` variant root 컴포넌트: `Transform` + `MeshFilter` + `MeshRenderer` + `Rigidbody` + `XRGrabInteractable` + `GripPoseProvider` + `BoxCollider`. 본 plan 은 여기에 신규 컴포넌트 1개를 add 하고 `XRGrabInteractable` 은 attach 중 일시 비활성화 (component property `m_Enabled` toggle, ARD 02 단계 5). — `unity-scene-reader 보고 (2026-05-02, manage_prefabs get_hierarchy)`
- `GripPoseHand` 자식의 `PreviewMesh/HandMeshPreview` 는 `SkinnedMeshRenderer` 단일 child. 본 plan 은 이 GameObject 에 `HidePhysicsHandInPlayMode` 컴포넌트를 add 하면 Play 모드에서 자동 hidden 된다 (ARD 02 단계 4). — `Read Assets/Hands/Scripts/HidePhysicsHandInPlayMode.cs (2026-05-02)` — 동작: `OnEnable()`에서 `GetComponent<SkinnedMeshRenderer>().enabled = !Application.isPlaying`. Edit 모드 OnDisable 시 visible 복귀. Play 모드 종료/씬 언로드 시 조작 안 함. `[ExecuteAlways]` + `RequireComponent(SkinnedMeshRenderer)`.
- `LeftPlayHand`(scene -25420) / `RightPlayHand`(scene 동급) 는 `PlayHandPoseDriver` 부착 root, child `L_Wrist`(또는 R_Wrist) + `LeftHand`(SkinnedMeshRenderer). PlayHand 의 grip override 슬롯은 `PushSourceOverride(newRoot, newWristRoot)` 로 점유. — `unity-scene-reader 보고 (2026-05-02, find_gameobjects + manage_scene)`
- VR Player container hierarchy: `VR Player/Camera Offset/Hands/{Left,Right}/{LeftHandTrackingHandRoot, LeftControllerHandRoot, LeftPhysicsHand, LeftPlayHand}` (R 측 mirror). Left/Right 자식의 controller ghost 는 `LeftControllerHandRoot`(`TrackedPoseDriver` 부착)이며, ghost wrist root 는 `Assets/Hands/Prefabs/Ghost/LeftControllerGhostHand.prefab` 의 root child `L_Wrist` 같은 OpenXR 표준 wrist (instanceID 71448 in prefab). 본 plan 의 stick 신규 컴포넌트는 ghost wrist transform 1개를 source 로 받는다. — `unity-scene-reader 보고 (2026-05-02, manage_scene + manage_prefabs)`

### 호출 외부 API side effect 박제

- `PlayHandPoseDriver.PushSourceOverride(Transform newRoot, Transform newWristRoot)` / `PopSourceOverride()` 호출 동작 (출처: `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-02)`):
  - **호출 즉시 in-frame 적용.** push 는 `m_OverrideSourceRoot` / `m_OverrideSourceWristRoot` 를 set 하고 `RefreshActiveSource()` 호출 → `ResolveDesiredSource()` 의 priority 체인 `grip override > physics > ghost fallback > null` 의 grip override 슬롯 점유. one-frame jump 없음.
  - **target wrist 와 source wrist 의 joint 이름이 일치해야 한다.** `BuildJointMap` 이 transform name 으로 매칭 (StringComparer.Ordinal). source 의 `__Collider_*` prefix 자식은 ignore. drum_stick variant 의 GripPoseHand wrist 가 OpenXR 표준 (`L_Wrist`, `L_IndexMetacarpal`, `L_IndexProximal`, … `L_Palm`, `L_ThumbMetacarpal`, … `L_ThumbTip` — 25 joints) 이고 LeftPlayHand 의 `L_Wrist` 도 같은 표준. R 측도 mirror prefix `R_` 로 일치.
  - **frame loop 동작.** `OnEnable` 에서 `Application.onBeforeRender += OnBeforeRender` 등록, `LateUpdate` 와 `OnBeforeRender` 모두에서 `SyncDesiredSourceIfChanged` → `TryEnsureInitialized` → `SyncPose` 실행. `SyncPose` 는 `syncRootTransform=true` 면 `transform.SetPositionAndRotation(m_ActiveSourceRoot.position, m_ActiveSourceRoot.rotation)` (PlayHand root 의 world pose 가 active source root 로 매 frame 강제). `OnDisable` 에서 unregister.
  - **source root 의 activeInHierarchy 가 false 가 되면 자동 fallback.** `ResolveDesiredSource` 가 `sourceRoot.gameObject.activeInHierarchy` 체크 후 fallback. override 슬롯 점유 중에는 activeInHierarchy 검사 없이 무조건 사용 (코드 line 83-84). 즉 stick 인스턴스가 destroy 되기 전에 `PopSourceOverride()` 를 호출하지 않으면 ResolveDesiredSource 가 destroyed transform 을 참조해 NRE 위험 — Approach 단계 5 참고.
  - **`OnValidate()` 가 `ResetInitialization()` 호출.** Editor 에서 PlayHand 인스펙터를 만지면 다음 frame 에 joint map 을 다시 빌드. 본 plan 의 직접 영향 없음.
- `LocomotionProvider.locomotionStarted` (event Action<LocomotionProvider>) 동작 (출처: `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/LocomotionProvider.cs (2026-05-02)`):
  - **invoke 시점.** `OnLocomotionStateChanging` 에서 state == Moving 일 때 발행 (line 258-262). state 전이 = `LocomotionMediator` 가 `TryStartLocomotion` 콜백 안에서 발행. drum anchor 의 `selectExited` → `SendTeleportRequest` → `QueueTeleportRequest` → 다음 `TeleportationProvider.Update` 에서 `TryStartLocomotionImmediately` → state Moving 전이 → `locomotionStarted` invoke. 즉 attach trigger (`selectExited` Frame N) 와 본 이벤트 (Frame N+1 가능) 사이에 1 frame 간격이 일반적.
  - **invoke 횟수.** 매 텔레포트마다 한 번. drum 도착 텔레포트도 본 이벤트를 발행한다 — Approach 단계 4 의 frame guard 가 그것을 무시한다.
- `TeleportationAnchor.selectExited` (UnityEvent<SelectExitEventArgs>, 상속 from `XRBaseInteractable`) 는 `m_TeleportTrigger=OnSelectExited` 일 때 anchor 가 텔레포트 요청을 큐에 올리는 시점과 동일 frame 의 직전 호출 (출처: `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/Teleportation/BaseTeleportationInteractable.cs (2026-05-02)` — line 416-422 `OnSelectExited` override 가 `SendTeleportRequest` 호출). 즉 anchor 의 자체 컴포넌트가 `selectExited` 를 구독하면 "내가 도착 destination 으로 확정됐다" 를 한 frame 일찍 받을 수 있다.

## Approach

### 단계 1 — `DrumKitStickAnchor.cs` (Anchor 자체 컴포넌트, ARD 01)

`Assets/Instruments/Drums/Scripts/DrumKitStickAnchor.cs` 신설.

- 부착 대상: `DrumKitAnchor` scene root (instanceID -24822). `RequireComponent(typeof(TeleportationAnchor))`.
- SerializeField:
  - `GameObject leftStickPrefab` — `Assets/Instruments/Drum/Prefabs/drum_stick_L.prefab` 참조.
  - `GameObject rightStickPrefab` — `Assets/Instruments/Drum/Prefabs/drum_stick_R.prefab` 참조.
  - `Transform leftGhostWristSource` — 사용자가 attach 시 stick 이 따라갈 ghost wrist. SampleScene 의 `VR Player/Camera Offset/Hands/Left/LeftControllerHandRoot/.../L_Wrist` 같은 컨트롤러-tracked ghost wrist root. (자세한 경로는 implement 시점에 unity-scene-reader 로 한 번 더 박제 — 구조상 `LeftControllerHandRoot` 또는 `LeftHandTrackingHandRoot` 의 자식. ghost prefab 인스턴스 root 가 바로 ghost wrist root 인지, 그 자식 `L_Wrist` 인지 점검 필요.)
  - `Transform rightGhostWristSource` — R 측 mirror.
  - `PlayHandPoseDriver leftPlayHandDriver` / `rightPlayHandDriver` — scene 의 `LeftPlayHand` / `RightPlayHand` 의 PlayHandPoseDriver.
- 상태: `bool m_IsAttached`, `int m_AttachFrame`, `GameObject m_LeftStickInstance`, `GameObject m_RightStickInstance`.
- `OnEnable`:
  - `GetComponent<TeleportationAnchor>().selectExited.AddListener(OnAnchorSelectExited)`.
  - `LocomotionProvider locomotionStarted += OnLocomotionStarted` — locomotion provider 는 `ComponentLocatorUtility` 로 찾거나 SerializeField 로 주입. `TeleportationAnchor` 가 들고 있는 `teleportationProvider` (XRI public property) 를 재사용해 동일 provider 의 `locomotionStarted` 구독.
- `OnDisable`: 양쪽 unsubscribe.
- `OnAnchorSelectExited(SelectExitEventArgs args)`:
  - 이미 `m_IsAttached` 면 noop (재선택 방어).
  - left/right stick prefab 을 Instantiate (parent=null, world). 초기 transform 은 ghost wrist source 의 world pose 로 set.
  - 각 stick 인스턴스의 `AnchoredStickGhostFollower` (단계 2) 를 GetComponent 후 `Bind(ghostWristSource, playHandDriver, stickHandWristRoot)` 호출 — stick-hand wrist root 는 stick variant 의 `GripPoseHand/L_Wrist`(또는 `R_Wrist`).
  - 각 PlayHand 의 `PushSourceOverride(stickHandRoot, stickHandWristRoot)` 호출 — `stickHandRoot` 는 stick 인스턴스의 `GripPoseHand` GameObject, `stickHandWristRoot` 는 그 자식 `L_Wrist`(또는 `R_Wrist`). (참고: `PlayHandPoseDriver.SyncRootTransform` 이 syncRootTransform=true 일 때 PlayHand world pose 를 source root 의 world pose 로 강제하므로, source root = stick 인스턴스의 `GripPoseHand` 가 stick 자체 transform 을 따라가도록 단계 2 의 follower 가 ensure.)
  - `m_IsAttached = true; m_AttachFrame = Time.frameCount;`.
- `OnLocomotionStarted(LocomotionProvider _)`:
  - `m_IsAttached == false` 면 noop.
  - **frame guard:** `Time.frameCount == m_AttachFrame` 또는 `Time.frameCount == m_AttachFrame + 1` 이면 noop (본 텔레포트 자체가 발행한 `locomotionStarted` 무시; XRI 의 1-frame queue 지연 흡수).
  - 그 외에는 detach: 양쪽 PlayHand 의 `PopSourceOverride()` 호출 → 양쪽 stick 인스턴스 `Destroy(...)` → `m_IsAttached = false`, `m_LeftStickInstance = m_RightStickInstance = null`.

### 단계 2 — `AnchoredStickGhostFollower.cs` (Stick 신규 컴포넌트, ARD 02)

`Assets/Instruments/Drums/Scripts/AnchoredStickGhostFollower.cs` 신설.

- 부착 대상: `drum_stick_L.prefab` / `drum_stick_R.prefab` variant root.
- `[DefaultExecutionOrder(10005)]` — `PhysicsHandGhostFollower`(10000) 후, `PlayHandPoseDriver`(10010) 전. ghost → stick → playhand 체인 순서 보존.
- runtime-set 필드 (Bind 메서드):
  - `Transform m_GhostWristSource`
  - `PlayHandPoseDriver m_PlayHandDriver` (참고용; 본 컴포넌트는 직접 호출 안 함, ARD 01 의 anchor 컴포넌트가 push/pop 담당. 그러나 stick 이 destroy 되기 전 pop 책임 분배 단계 5 참고)
  - `Transform m_StickHandWristRoot` (이 stick 인스턴스의 `GripPoseHand/L_Wrist`)
- `LateUpdate` 와 `Application.onBeforeRender` (PhysicsHandGhostFollower 답습) 양쪽에서:
  - `transform.SetPositionAndRotation(m_GhostWristSource.position, m_GhostWristSource.rotation)` — stick root 의 world pose 를 ghost wrist 에 강제.
  - 본 컴포넌트는 stick-hand wrist 의 joint 추종 책임 없음 — stick-hand 는 prefab 직렬화된 정적 stick-잡기 포즈 를 그대로 유지하며, `GripPoseHand` 가 stick 자식이므로 stick 이 움직이면 자동 따라간다.
- `OnEnable` / `OnDisable` 에서 `Application.onBeforeRender` 등록·해제.
- Bind 메서드: 위 3 필드 set + (선택) `Rigidbody.isKinematic = true` 강제 (drum_stick variant 의 `Rigidbody` 가 attach 중 물리 영향 받지 않도록).

### 단계 3 — `drum_stick_L/R.prefab` 변경 (MCP, ARD 04)

각 variant 에 대해:

1. `manage_components action=add component_type=AnchoredStickGhostFollower target=<variant root>` — variant root 에 컴포넌트 추가.
2. `manage_components action=add component_type=HidePhysicsHandInPlayMode target=<variant>/GripPoseHand/PreviewMesh/HandMeshPreview` — 기존 `SkinnedMeshRenderer` 자식에 hide 컴포넌트 추가. (`RequireComponent(SkinnedMeshRenderer)` 충족됨.)
3. (직렬화 검증용) variant root 의 `XRGrabInteractable.m_Enabled` 는 prefab 에서 그대로 두고, runtime 의 `OnAnchorSelectExited` 에서 stick instance 의 `XRGrabInteractable.enabled = false` 로 일시 비활성화 (사용자 grip/trigger 입력으로 떼어지지 않게). detach 직전 unset 불필요 — 어차피 Destroy.

ARD 02 단계 5 의 두 안 ("prefab 직렬화 enabled=false" vs "runtime 일시 비활성화") 중, **runtime 일시 비활성화** 를 채택한다. 이유: `XRGrabInteractable` 을 prefab 에서 disabled 로 두면 향후 anchor 외부에서 stick 을 다른 방식으로 잡고 싶을 때 (예: 02-stick-no-penetration 또는 후속 피처) prefab 자체를 다시 만져야 하지만, runtime 토글은 본 attach 상태 한정으로 격리된다.

### 단계 4 — `DrumKitAnchor` scene instance 변경 (MCP)

1. `manage_components action=add component_type=DrumKitStickAnchor target=-24822` — anchor 컴포넌트 추가.
2. `manage_components action=set_property component_type=DrumKitStickAnchor target=-24822` 로 6개 SerializeField 셋업 (좌·우 stick prefab GUID 참조, 좌·우 ghost wrist source scene transform, 좌·우 PlayHandPoseDriver scene 참조). prefab GUID 는 `manage_asset` 으로 조회 또는 `path` 로 직접 지정 (decision 04 박제 형식).

### 단계 5 — 씬 기존 drum_stick_L/R 인스턴스 제거 (ARD 03)

SampleScene root 의 `drum_stick_L`(-24686) / `drum_stick_R`(-24542) 두 인스턴스를 `manage_gameobject action=delete` 로 제거. (본 plan-drafter 는 자산 수정 권한이 없어 제거하지 않음 — plan-implementer 가 수행.)

### 단계 6 — Pop 책임 분배 (ARD 02 의 협조 결정)

ARD 02 는 push/pop 호출 주체를 plan 에서 확정하라고 했다. 본 plan 결정: **anchor 컴포넌트가 push/pop 둘 다 담당**. 이유:
- stick 인스턴스의 OnDestroy 에서 pop 하면 anchor 컴포넌트가 `m_IsAttached` 등 자기 상태를 정리하기 전 PlayHand 가 source 를 잃을 수 있어 1-frame 의 의도치 않은 fallback 으로 깜빡임.
- anchor 컴포넌트가 detach 시퀀스를 한 frame 안에 `pop → destroy` 순으로 명시 호출하면 PlayHand 의 active source 전환과 stick 소멸이 deterministic.
- stick 의 `AnchoredStickGhostFollower.Bind` 가 받은 `m_PlayHandDriver` 는 emergency pop 용 (예: 외부에서 stick 이 미리 Destroy 되는 사고 케이스) 으로만 보유하고 정상 경로에서 호출하지 않는다.

## Deliverables

- `Assets/Instruments/Drums/Scripts/DrumKitStickAnchor.cs` — drum anchor 자체 컴포넌트. attach/detach 신호 발행 + 양쪽 stick Instantiate/Destroy + PlayHand source override push/pop.
- `Assets/Instruments/Drums/Scripts/AnchoredStickGhostFollower.cs` — drum_stick variant 부착, ghost wrist 를 source 로 stick root 의 world pose 를 매 frame 추종.
- `Assets/Instruments/Drum/Prefabs/drum_stick_L.prefab` — variant root 에 `AnchoredStickGhostFollower` add, `GripPoseHand/PreviewMesh/HandMeshPreview` 에 `HidePhysicsHandInPlayMode` add (MCP 1차).
- `Assets/Instruments/Drum/Prefabs/drum_stick_R.prefab` — 위 mirror (MCP 1차).
- `Assets/Scenes/SampleScene.unity` — `DrumKitAnchor` 에 `DrumKitStickAnchor` 컴포넌트 add + 6 SerializeField 셋업, 기존 `drum_stick_L/R` scene root 2개 제거.

## Acceptance Criteria

- [ ] `[auto-hard]` `DrumKitStickAnchor.cs` / `AnchoredStickGhostFollower.cs` 컴파일 성공 — `read_console types=error filter_text=DrumKitStickAnchor`, `filter_text=AnchoredStickGhostFollower` 모두 0 매치 + `editor_state.isCompiling=false`.
- [ ] `[auto-hard]` `drum_stick_L.prefab` / `drum_stick_R.prefab` variant root 에 `AnchoredStickGhostFollower` 컴포넌트 1개 부착 — 각 prefab 에 대해 `manage_prefabs get_hierarchy` 결과의 root item `componentTypes` 에 `AnchoredStickGhostFollower` 단일 매치 (정확 1회).
- [ ] `[auto-hard]` 두 stick variant 의 `GripPoseHand/PreviewMesh/HandMeshPreview` GameObject 컴포넌트에 `HidePhysicsHandInPlayMode` 단일 매치 (정확 1회).
- [ ] `[auto-hard]` `DrumKitAnchor` scene root (instanceID 매칭은 재load 후 변할 수 있으니 path `DrumKitAnchor` 사용) 에 `DrumKitStickAnchor` 컴포넌트 단일 매치, 6 SerializeField (좌·우 stick prefab, 좌·우 ghost wrist source, 좌·우 PlayHand) 모두 non-null.
- [ ] `[auto-hard]` SampleScene root 에 `drum_stick_L` / `drum_stick_R` GameObject 가 0건 — `find_gameobjects search_term=drum_stick_L include_inactive=true` 와 `_R` 모두 totalCount=0.
- [ ] `[auto-hard]` `DrumKitAnchor` 의 `TeleportationAnchor.m_TeleportTrigger` enum 값 직렬화 검증 = `0` (= OnSelectExited per `BaseTeleportationInteractable.TeleportTrigger` 정의) — Verified Structural Assumptions 의 enum 박제 의도 값과 grep 단일 매치. (기존 값을 plan 이 변경하지 않았는지 가드.)
- [ ] `[manual-hard]` Play 모드 진입 → drum anchor 외부에서 시작 → drum anchor 로 텔레포트 → 도착 직후 양손에 stick 이 동시에 들리고 손은 stick-잡기 포즈 (헤드셋 또는 Editor Game View 에서 육안 확인).
- [ ] `[manual-hard]` 위 상태에서 grip 또는 trigger 입력을 줘도 stick 이 손에서 떨어지지 않으며 손 포즈가 변하지 않는다.
- [ ] `[manual-hard]` drum anchor 외부 위치(예: piano anchor 또는 평면 TeleportationArea)로 텔레포트 → 양손에서 stick 이 동시에 사라지고 어디에도 보이지 않는다 (Hierarchy 검색해도 stick 인스턴스 0건).
- [ ] `[manual-hard]` 같은 drum anchor 로 다시 텔레포트 → 새 stick 인스턴스가 양손에 다시 attach 된다 (1번째와 동일 동작).

## Out of Scope

- 스틱이 드럼 부품을 통과하는지 검증 / 통과 방지 → `02-stick-no-penetration.md`.
- 손이 환경을 통과하는지 (손 자체 통과 방지) → `hands/specs/02-instrument-no-penetration.md`.
- piano anchor 등 drum 외 anchor 의 도구 자동 부착 — 본 sub-spec 은 drum 만.
- 텔레포트 외 이동(걷기, 그립이동) 으로 anchor 영역을 벗어나는 시나리오의 detach.
- 드럼 노트 인식 / 타격 깊이·속도 → 다른 피처.
- Ghost wrist source 가 controller 모드와 hand-tracking 모드 전환 시의 source 재바인딩 — 본 plan 은 attach 시점에 한 번 ghost wrist 를 set 하고 destroy 까지 유지. 사용자가 attach 도중 입력 모드를 전환하는 시나리오는 별도 후속 plan 후보 (Notes 참고).

## Notes

- attach 도중 controller↔hand-tracking 전환 케이스: `LeftControllerHandRoot` 와 `LeftHandTrackingHandRoot` 사이 active 전환이 일어나면 `m_GhostWristSource` 가 비활성 transform 을 가리키게 된다. 본 plan 은 단순화 위해 attach 시점의 ghost wrist source 1개를 고정. 실제 사용자가 attach 중 모드를 자주 전환하면 stick 이 멈춰 보일 수 있다 — 후속 plan 후보.
- ARD 04 의 MCP fallback: `DrumKitStickAnchor` 의 prefab 참조 SerializeField 셋업 시 `manage_components set_property` 가 prefab GUID 를 직접 받지 못하면, plan-implementer 는 사용자 승인 후 unity-asset-edit skill 의 직접 텍스트 Edit 경로로 우회. 본 plan 은 그 경로를 미리 자동화하지 않는다.
- `DrumKitStickAnchor` 의 `locomotionStarted` 구독 시점에 `teleportationProvider` 가 null 이면 (ComponentLocatorUtility 도 실패) `OnEnable` 에서 한 번 retry. 그래도 null 이면 attach 만 동작하고 detach 가 fail — 사용자에게 `[manual-hard]` AC 4번에서 catch.

## Handoff

### 코드/자산 산출물 (실제 적용 결과)

- 신설 파일:
  - `Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs` — anchor 자체 컴포넌트. `selectExited` → attach (양쪽 stick Instantiate + ghost wrist source bind + PlayHand source override push). `LocomotionProvider.locomotionStarted` → detach (frame guard 후 양쪽 PlayHand source override pop + 양쪽 stick Destroy).
  - `Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs` — stick variant root에 부착. `LateUpdate` / `OnBeforeRender` 에서 `stick.transform.SetPositionAndRotation(ghostWristSource.position, ghostWristSource.rotation)`. Bind 시 `Rigidbody.isKinematic=true` + `XRGrabInteractable.enabled=false`.
- 자산 변경(MCP):
  - `drum_stick_L.prefab` / `drum_stick_R.prefab` variant root: `AnchoredStickGhostFollower` 단일 부착.
  - 두 prefab `GripPoseHand/PreviewMesh/HandMeshPreview` GameObject: `HidePhysicsHandInPlayMode` 단일 부착.
  - `SampleScene.unity`: 기존 `drum_stick_L`/`drum_stick_R` scene 인스턴스 2개 제거.
  - `SampleScene.unity` `DrumKitAnchor` root: `DrumKitStickAnchor` 컴포넌트 추가 + 6 SerializeField 셋업 (좌·우 stick prefab path, 좌·우 `LeftControllerGhostHand/L_Wrist`·`RightControllerGhostHand/R_Wrist`, 좌·우 `LeftPlayHand`·`RightPlayHand` PlayHandPoseDriver).
- asmdef 변경:
  - `Assets/Instruments/Instruments.asmdef`에 `Unity.XR.Interaction.Toolkit` reference 추가 — XRI namespace(`TeleportationAnchor`, `LocomotionProvider`, `SelectExitEventArgs`) 컴파일 의존.

### 검증 결과

- `[auto-hard]` 1~6 모두 pass (read_console 0건, manage_prefabs/component resource 단일 매치, m_TeleportTrigger=0 보존).
- `[manual-hard]` 7~10 모두 pass (사용자 Play 모드 검증).

### 후속 plan이 알아야 할 사실 (Caused By 컨텍스트)

- **stick GripPoseHand 정렬 결손 — 후속 plan `02-2026-05-03-stick-gripposehand-alignment` 후보**: `AnchoredStickGhostFollower`가 `stick.transform`을 ghost wrist world pose에 단순 매칭하므로, `stick.GripPoseHand`의 prefab-내부 local offset만큼 stick body가 손에서 어긋난 방향으로 뻗는다. PlayHand는 `stick.GripPoseHand` world pose를 따라가지만 ghost wrist world pose ≠ stick.GripPoseHand world pose가 되어 PlayHand 위치가 컨트롤러와 어긋나 보인다. 올바른 정렬은 `stick.transform.world = ghostWrist.world × (stick.GripPoseHand.localToRoot)⁻¹` (= stick.GripPoseHand가 ghost wrist에 정확히 일치하도록 root world pose를 역산). 본 plan의 manual-hard 4건은 pass(=lifecycle은 동작)지만 시각적 정렬은 부정확하다.
- **DrumKitAnchor forward 방향 결손 — 후속 plan `03-2026-05-03-drum-anchor-forward-rotation` 후보**: `DrumKitAnchor` GameObject의 Y rotation이 의도와 반대(현재 forward = 드럼킷 등 뒤). `TeleportationAnchor.m_MatchOrientation=2`(TargetUpAndForward)이므로 도착 시 사용자 forward가 drum의 등 뒤로 향한다. 단순 1-line transform 변경(SampleScene `DrumKitAnchor` Y rotation +180°). 본 plan 자체의 lifecycle 동작에는 영향 없음.

### 후속 sub-spec(`02-stick-no-penetration`)이 알아야 할 사실

- stick 인스턴스는 매 attach 마다 새로 spawn(Instantiate) 되고 detach 시 Destroy 된다. 즉 02 plan의 콜라이더·rigidbody 셋업은 prefab 자산 수정으로 처리해야 한다(런타임 인스턴스만 만져도 다음 attach 때 사라짐).
- attach 중 `Rigidbody.isKinematic=true`, `XRGrabInteractable.enabled=false`로 강제됨. 02 plan이 stick의 rigidbody·collider 거동을 다룰 때 이 두 runtime override를 가정으로 박제할 수 있다.
- 두 stick variant의 wrist 계층은 OpenXR 표준 25-joint (`L_Wrist`/`R_Wrist` 아래). 02 plan이 콜라이더를 어디에 둘지 결정할 때 이 계층을 그대로 본다.
