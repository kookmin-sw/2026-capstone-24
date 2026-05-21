# Anchor & Mouth Attach/Detach 라이프사이클 박제

**Linked Spec:** [`01-anchor-mouth-attach-detach.md`](../specs/01-anchor-mouth-attach-detach.md)
**Status:** `Done`

## Goal

Trombone.prefab 에 MouthPiece 기준점 + Body·Slide 각 GripPoseHand 골격을 박제하고, VR Player.prefab Main Camera 아래에 Mouth 기준점을 추가하며, TestSceneSanyo 에 TromboneAnchor scene root + `TromboneAnchor.cs` 컴포넌트를 도입해 텔레포트 entry/exit 한 순간에 본체 정렬 + 양손 GripPose Push/Pop 이 1:1 로 함께 발화하는 라이프사이클 단위를 완성한다.

## Context

본 sub-spec 은 트럼본 피처의 첫 단계로, anchor 진입·이탈에 묶여 일어나는 "본체 정렬 + 양손 GripPose 적용 + 원위치 복귀 + GripPose 해제" 4 액션이 단일 라이프사이클로 동작함을 책임진다 (Linked Spec §What W1·W2·W3 + Behavior). 슬라이드 운전(02) 과 발음(03) 은 본 plan 이 박제한 attach 상태를 전제로 동작하므로 attach/detach 의 정합성(중간 상태 부재·중복 발화 차단)은 다음 plan 이 재발명하지 않도록 본 plan 에서 확정한다.

트리거 모델은 ARD 01 (`docs/specs/trombone/decisions/01-anchor-attach-detach-trigger.md`) 에서 *drum-stick 동일* (TeleportationAnchor.selectExited + LocomotionProvider.locomotionStarted pending window) 로 결정됐다. 동일 패턴이 `Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs` 에 이미 검증돼 있으므로 본 plan 의 `TromboneAnchor.cs` 는 그 골격을 답습하되, drum-stick 의 *Instantiate / Destroy* 모델 대신 *displace / restore* 모델 (단일 본체를 anchor 진입 직전 좌표에서 입 좌표로 이동, detach 시 캐시된 원래 좌표로 복귀) 로 구현한다 — Tech Spec §Comparable Siblings 의 핵심 차이.

attach 상태 유지 단계에서 매 프레임 트럼본 root 좌표를 VR Player Mouth 좌표로 따라가게 만드는 책임도 본 컴포넌트에 둔다 (Tech Spec §Data/Control Flow). 이때 align 의 양 끝은 (a) Trombone.prefab 자식 `MouthPiece` 의 world transform, (b) VR Player.prefab Main Camera 자식 `Mouth` 의 world transform 이다 — MouthPiece 의 trombone-root-local offset 을 prefab 박제 단계에서 정렬자세에 맞게 한 번 정해 두면 런타임에서 단순한 역계산으로 매 프레임 본체 root 좌표가 도출된다 (Tech Spec §Invariants: "한 프레임 내 중간 상태 존재 금지" 를 만족시키는 단일 단계 갱신).

## Verified Structural Assumptions

- Trombone.prefab 현재 hierarchy: root `Trombone` (Transform only, localPosition `(0.5745591, 0.817, 1.268995)`) 아래 자식 `Body` (Transform + MeshFilter + MeshRenderer, localRotation `(-0.7071068, 0, 0, 0.7071067)`, localScale 100) 와 `Slide` (Transform + MeshFilter + MeshRenderer, localScale `(116.5, 97.8, 115.9)`) 2 개. `MouthPiece` / `GripPoseHand` 자식은 부재. 출처: `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-17)`.
- VR Player.prefab Main Camera (GameObject fileID 3487105840860713677, tag `MainCamera`, m_Father `Camera Offset`) 의 Transform 자식 리스트 = `[]` (line 35: `m_Children: []`). Mouth 자식 없음. 출처: `Read Assets/Characters/Prefabs/VR Player.prefab (2026-05-17)`.
- DrumKitStickAnchor.cs 동작 요약 (호출 외부 API 박제, *동작 전체* 인용):
  - `OnEnable` 에서 `TeleportationAnchor.selectExited` 구독 + `EnsureLocomotionSubscription()` 호출. teleportationProvider 는 BaseTeleportationInteractable 이 OnSelectExited 직전 lazy-resolve 하므로 OnEnable 에서는 null 일 수 있고, `OnAnchorSelectExited` 안에서 self-heal 재구독한다.
  - `OnDisable` 에서 selectExited 및 locomotionStarted 구독 해제 + `m_IsAttached==true` 면 PhysicsHands 가시성 복구 — 본 trombone 적용 시 PhysicsHands 가시성 핸들링은 동일하게 유지.
  - `OnAnchorSelectExited(args)`: `args.isCanceled` 면 즉시 return (cancel 시 attach/detach 모두 발화 안 됨). 통과하면 `m_PendingAttachFrame = Time.frameCount` 만 기록 — 실제 attach 는 locomotionStarted 에서.
  - `OnLocomotionStarted`: `withinPendingWindow == (m_PendingAttachFrame >= 0 && frame - m_PendingAttachFrame <= 2)` 면 attach 경로 (이미 `m_IsAttached` 면 no-op), 아니면 detach 경로.
  - `BindStickAndPushOverride`: `stickInstance.transform.Find("GripPoseHand")` → `.Find(wristChildName)` 으로 source 뼈 탐색 후 `driver.PushSourceOverride(gripPoseHand, stickHandWristRoot)`.
  - `Detach`: PhysicsHands 가시성 복구 → 좌/우 `PopSourceOverride()` → `Destroy(stickInstance)` 순서 (deterministic, PlayHand fallback 깜빡임 방지).
  - 출처: `Read Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs (2026-05-17)`.
- `PlayHandPoseDriver.PushSourceOverride(root, wristRoot)` / `PopSourceOverride()` side effect (호출 외부 API 박제, *동작 전체*):
  - Push 는 `m_OverrideSourceRoot/WristRoot` 를 세팅한 뒤 `RefreshActiveSource()` → desired source 가 바뀌면 `m_IsInitialized=false` 로 리셋 후 즉시 `TryEnsureInitialized()` 재실행 — 같은 프레임 안에서 override 가 활성화돼 한 프레임 jump 가 없다.
  - Pop 은 override 두 필드를 null 로 비우고 동일하게 `RefreshActiveSource()` 호출. 우선순위는 `grip override > physics(activeInHierarchy) > ghost fallback > null`.
  - `syncRootTransform == true` 이면 매 `LateUpdate` + `Application.onBeforeRender` 에서 `transform.SetPositionAndRotation(activeSourceRoot.position, activeSourceRoot.rotation)` 으로 PlayHand 의 root transform 도 source root 에 묶인다 — 즉 Push 한 GripPoseHand 자체의 world pose 가 PlayHand world pose 를 운전한다. 따라서 본 plan 의 본체 정렬은 Trombone root 만 움직이면 그 자식 GripPoseHand 의 world pose 가 따라가고 PlayHand 도 그 GripPoseHand 를 따라간다 — *세 단계 한 프레임 안 정합* 이 요구된다 (Tech Spec §Invariants).
  - `LateUpdate` 와 `onBeforeRender` 두 곳에서 모두 `SyncPose` 가 실행되므로 본 plan 의 trombone root pose 갱신은 *LateUpdate* 단계에서 수행해야 같은 프레임의 PlayHand 갱신 직전에 위치가 결정된다. 출처: `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-17)`.
- `Instruments.asmdef` references 목록 = `[Unity.InputSystem, Hands, Unity.XR.Interaction.Toolkit]`. `TromboneAnchor.cs` 는 `Assets/Instruments/Trombone/Scripts/` 에 두면 본 asmdef 에 자동 포함되며 `PlayHandPoseDriver` (Hands asmdef, 글로벌 namespace) / `TeleportationAnchor` / `LocomotionProvider` 모두 import 가능. 신규 reference 추가 불필요. 출처: `Read Assets/Instruments/Instruments.asmdef (2026-05-17)` + `Read Assets/Hands/Hands.asmdef (2026-05-17)` + `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-17)`.
- `Assets/Instruments/Trombone/Scripts/` 폴더에는 아직 `.asmdef` 가 없다 (`Glob Assets/Instruments/Trombone/**` 결과 = Models/Prefabs 만). 가장 가까운 상위 폴더의 asmdef 는 `Assets/Instruments/Instruments.asmdef` 이므로 신규 `TromboneAnchor.cs` 는 자동으로 Instruments assembly 에 포함된다. 출처: `Glob Assets/Instruments/Trombone/**/* (2026-05-17)`.
- `Assets/Hands/Editor/GripPoseValidator.cs` 존재 — `Tools/Hands/Validate Grip Pose Wiring` 메뉴가 GripPoseHand 구조·뼈 매칭·PreviewMesh 위치를 점검. 본 plan 의 GripPoseHand 검증에 활용. 출처: `Glob Assets/Hands/Editor/*GripPose* (2026-05-17)`.
- `Assets/Instruments/Drum/Prefabs/drum_stick_R.prefab` / `drum_stick_L.prefab` 존재 — GripPoseHand 뼈 계층의 reference donor. 본 plan 은 그 R_Wrist / L_Wrist 계층을 *복사* 해 Body/GripPoseHand 와 Slide/GripPoseHand 의 자식으로 둔다 (Hands CLAUDE.md §4 단계 3 답습). 출처: `Glob Assets/Instruments/Drum/Prefabs/drum_stick_R.prefab (2026-05-17)`.
- TestSceneSanyo 안 트럼본 anchor 부재 — 검색 결과 `Trombone` 키워드는 단 1 회만 등장 (line 2726 의 prefab path 추정) 으로 anchor scene 객체는 없다. 본 plan 이 신규 생성. 출처: `Grep Trombone in TestSceneSanyo.unity (2026-05-17)`.

## Approach

1. **Trombone.prefab 골격 박제** (manage_asset / manage_gameobject MCP) — Body 자식으로 `GripPoseHand` 빈 GameObject 추가 후 `GripPoseHandPreview` 컴포넌트 부착. `drum_stick_L.prefab` 의 `GripPoseHand/L_Wrist` 서브트리 (뼈 + `PreviewMesh`/`HandMeshPreview` SkinnedMeshRenderer) 를 복사해 Body/GripPoseHand 아래로 이식. 이름·구조 보존 (PlayHandPoseDriver.BuildJointMap 이 wrist 에서 재귀 수집 + 이름 매칭이므로 깨면 적용 0). PreviewMesh 는 L_Wrist 의 sibling 로만 둔다 (Hands CLAUDE.md §3 함정 1·2 회피). Slide 자식으로 동일한 `GripPoseHand` 생성하되 `R_Wrist` variant 이식. 마지막으로 Trombone root 자식에 빈 Transform `MouthPiece` 추가 — localPosition/localRotation 은 "사용자가 본체를 잡고 입에 댄 자세" 에서 입 기준점 위치에 오도록 정렬자세 시뮬로 결정 (manual 시각 조정 단계).
2. **VR Player.prefab Mouth 기준점 박제** (manage_gameobject MCP) — Main Camera (fileID 3487105840860713677) 자식으로 빈 Transform `Mouth` 1 개 추가. localPosition `(0, -0.05, 0.10)` 정도로 입 위치 근사 박제, localRotation identity. 정확한 정합값은 사용자가 헤드셋 착용 후 정렬자세 시각 검증에서 조정 (Out of Scope: 정렬값 자체 튜닝).
3. **`Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs` 신규** (manage_script MCP) — `Instruments` namespace. `[RequireComponent(typeof(TeleportationAnchor))][DisallowMultipleComponent]`. SerializeField: `leftPlayHandDriver`/`rightPlayHandDriver` (PlayHandPoseDriver), `tromboneRoot` (Transform), `mouthPieceOnTrombone` (Transform — Trombone.prefab 의 MouthPiece 참조), `mouthOnPlayer` (Transform — VR Player.prefab 의 Mouth 참조). 내부 상태: `m_IsAttached`, `m_PendingAttachFrame=-1`, `k_PendingAttachWindowFrames=2`, `m_CachedRestorePosition`/`m_CachedRestoreRotation` (최초 detach 시점이 아니라 *최초 attach 직전*에 1회만 캐시 — Tech Spec §Invariants 마지막 조항).
4. **이벤트 wiring** — `OnEnable` 에서 selectExited 구독 + `EnsureLocomotionSubscription()`. `OnAnchorSelectExited(args)`: `args.isCanceled` 면 return, 아니면 self-heal 구독 시도 후 `m_PendingAttachFrame=Time.frameCount`. `OnLocomotionStarted`: pending window 안 → 이미 attached 면 return (재텔레포트 no-op), 아니면 `AttachTromboneToMouth()`. 밖 → `m_IsAttached` 면 `Detach()`. drum-stick 의 시그니처·조건 그대로 답습.
5. **`AttachTromboneToMouth()`** — 최초 호출이면 `m_CachedRestorePosition/Rotation = tromboneRoot.position/rotation` 캐시. 본체 정렬 = `AlignTromboneToMouth()` 즉시 1 회 호출 (LateUpdate 갱신과 동일 식 사용 — 한 프레임 중간 상태 부재). 그 후 좌·우 `PushSourceOverride(tromboneRoot.Find("Body/GripPoseHand"), .Find("Body/GripPoseHand/L_Wrist"))` / 동일하게 `Slide/GripPoseHand` + `R_Wrist`. `m_IsAttached=true`.
6. **`LateUpdate` 동안 attach 상태 유지** — `m_IsAttached==true` 이면 매 프레임 `AlignTromboneToMouth()` 호출. PlayHandPoseDriver 의 LateUpdate (`DefaultExecutionOrder(10010)`) 보다 먼저 실행되도록 본 컴포넌트는 `[DefaultExecutionOrder(10005)]` 박제 (PlayHandPoseDriver 가 본 plan 의 root 갱신을 본 같은 프레임에 source 로 읽어가도록).
7. **`AlignTromboneToMouth()`** — `mouthPieceOnTrombone` 의 trombone-root-local pose 를 사용해 역계산: `tromboneRoot.rotation = mouthOnPlayer.rotation * Quaternion.Inverse(mouthPieceLocalRotation)`, `tromboneRoot.position = mouthOnPlayer.position - tromboneRoot.rotation * mouthPieceLocalPosition`. (Tech Spec §Data/Control Flow 의 "MouthPiece 의 trombone-root-local offset 으로 역계산" 박제.)
8. **`Detach()`** — Pop 양손 → `tromboneRoot.SetPositionAndRotation(m_CachedRestorePosition, m_CachedRestoreRotation)` → `m_IsAttached=false`. drum-stick 과 달리 Destroy 단계 없음 (단일 본체 displace/restore).
9. **TestSceneSanyo scene 세팅** (manage_gameobject MCP) — 빈 GameObject `TromboneAnchor` 를 scene root 에 생성. `TeleportationAnchor` (XRI) + 본 plan 의 `TromboneAnchor` 컴포넌트 부착. 4 개 SerializeField (left/right PlayHandPoseDriver, tromboneRoot, mouthPieceOnTrombone, mouthOnPlayer) 를 scene 의 VR Player rig 와 Trombone instance 에서 wire. anchor 의 teleportAnchorTransform 도 적절한 위치로 설정 (사용자가 직접 시각 조정).
10. **검증** — `Tools/Hands/Validate Grip Pose Wiring` 메뉴 실행 (Trombone.prefab 의 Body/GripPoseHand 와 Slide/GripPoseHand 양쪽이 통과해야 함). 컴파일 후 Editor Play 모드에서 anchor 텔레포트 시 본체 정렬 + GripPose 발화, anchor 외부 텔레포트 시 원위치 복귀, 같은 anchor 재텔레포트 시 no-op, cancel 텔레포트 시 무발화 — 4 시나리오 모두 시각 확인.

## Deliverables

- `Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs` — 신규 anchor 컴포넌트. selectExited + locomotionStarted pending window, attach/detach, attach 동안 LateUpdate align, displace/restore 모델.
- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` — `Body/GripPoseHand/{L_Wrist tree, PreviewMesh}`, `Slide/GripPoseHand/{R_Wrist tree, PreviewMesh}`, `MouthPiece` 자식 3 개 신규 추가.
- `Assets/Characters/Prefabs/VR Player.prefab` — Main Camera 자식 `Mouth` 빈 Transform 1 개 신규 추가.
- `Assets/Scenes/TestSceneSanyo.unity` — `TromboneAnchor` scene root 1 개 + TeleportationAnchor + TromboneAnchor 컴포넌트, SerializeField 4 개 wiring.

## Acceptance Criteria

- [x] `[auto-hard]` `TromboneAnchor.cs` 가 `Assets/Instruments/Trombone/Scripts/` 에 존재하고, `namespace Instruments`, `[RequireComponent(typeof(TeleportationAnchor))]`, `[DisallowMultipleComponent]`, `[DefaultExecutionOrder(10005)]` 가 모두 부착돼 있다.
  **검증:** `Grep -n "namespace Instruments|RequireComponent\(typeof\(TeleportationAnchor\)\)|DisallowMultipleComponent|DefaultExecutionOrder\(10005\)" Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs` 결과 4 줄 매칭.
- [x] `[auto-hard]` TromboneAnchor 가 selectExited 의 `isCanceled` 분기와 locomotionStarted 의 pending window 판정 (`m_PendingAttachFrame >= 0 && frame - m_PendingAttachFrame <= k_PendingAttachWindowFrames`) 을 drum-stick 과 동일한 형태로 답습한다.
  **검증:** `Grep -n "args.isCanceled|m_PendingAttachFrame|k_PendingAttachWindowFrames" Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs` 결과 ≥ 4 줄 매칭 + 해당 조건식 1 줄에 `frame - m_PendingAttachFrame <= k_PendingAttachWindowFrames` 포함.
- [x] `[auto-hard]` Trombone.prefab 안에 `Body/GripPoseHand/L_Wrist`, `Body/GripPoseHand/PreviewMesh`, `Slide/GripPoseHand/R_Wrist`, `Slide/GripPoseHand/PreviewMesh`, `MouthPiece` 5 개 자식이 존재한다 (PreviewMesh 가 Wrist 의 sibling — Wrist 안이 아님).
  **검증:** `Grep -n "m_Name: L_Wrist|m_Name: R_Wrist|m_Name: GripPoseHand|m_Name: PreviewMesh|m_Name: MouthPiece" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과 ≥ 7 매칭 (GripPoseHand 2, PreviewMesh 2, L_Wrist 1, R_Wrist 1, MouthPiece 1) + `Tools/Hands/Validate Grip Pose Wiring` 메뉴 실행 결과 양쪽 GripPoseHand 통과.
- [x] `[auto-hard]` VR Player.prefab Main Camera 자식 `Mouth` 가 존재한다 (Transform only).
  **검증:** `Grep -n "m_Name: Mouth" Assets/Characters/Prefabs/VR Player.prefab` 결과 ≥ 1 매칭 + 그 GameObject 의 `m_Father` 가 Main Camera (fileID 652725695442906022) 의 Transform fileID 와 일치.
- [x] `[auto-soft]` Unity Editor 컴파일 0 error (`read_console types=[error]`). `TromboneAnchor.cs` 가 `Instruments` asmdef 에 자동 포함돼 `PlayHandPoseDriver` / `TeleportationAnchor` / `LocomotionProvider` 가 모두 resolve.
  **검증:** `read_console action=get types=[error] count=20` 결과 TromboneAnchor 관련 컴파일 에러 0 건 + `editor_state.isCompiling == false`.
- [ ] `[manual-hard]` Editor Play 모드 / 헤드셋에서 TromboneAnchor 외부에 서 있다가 anchor 로 텔레포트 → 한 프레임 안에 트럼본 본체의 MouthPiece 가 VR Player Mouth 와 정렬되고, 좌·우 PlayHand 가 Body/GripPoseHand·Slide/GripPoseHand 의 뼈 포즈로 전환된다 (본체와 손이 같은 프레임에 함께 움직임 — 시각적으로 한쪽이 뒤늦게 따라붙는 일 없음).
  **검증:** Play 모드 진입 → anchor 외부에서 anchor 로 1 회 텔레포트 → 첫 렌더 프레임에 본체·양손이 동시 정렬되어 있는지 시각 확인.
- [ ] `[manual-hard]` 위 상태에서 사용자 머리를 회전·이동 (HMD pose 변경) 해도 매 프레임 본체가 입을 따라간다 (트럼본이 입에 붙어 있는 듯한 시각 정합).
  **검증:** Play 모드에서 attached 상태로 머리를 좌우·전후로 천천히 움직였을 때 MouthPiece 가 Mouth 와 계속 일치, 트럼본이 공중에 떠 있거나 한 프레임 뒤처지지 않음.
- [ ] `[manual-hard]` anchor 외부 다른 지점으로 텔레포트 → 트럼본이 anchor 진입 *직전* scene 배치 위치/회전으로 복귀하고, 양손 GripPose 가 해제되어 평상시 손 표시로 돌아간다 (PlayHand 가 Physics 또는 Ghost fallback 로 정상 복귀).
  **검증:** Play 모드에서 attached 상태로 anchor 외부 임의 지점 텔레포트 → 트럼본이 정확히 원래 scene 위치로 복귀 + 손이 즉시 평상시 손 모양으로 복귀.
- [ ] `[manual-hard]` 같은 anchor 로 재텔레포트 → attach 가 중복 발화하지 않고 시각·자세가 그대로 유지된다 (캐시된 원위치는 *최초 attach 직전* 값이 그대로 유지 — 재텔레포트로 캐시가 갱신되지 않음).
  **검증:** Play 모드에서 anchor → anchor 텔레포트 반복 → 자세 유지 + 외부 텔레포트 시 여전히 *최초* scene 위치로 복귀.
- [ ] `[manual-hard]` 텔레포트 cancel (트리거 release 전에 다른 입력으로 취소) 시 attach 도 detach 도 발화하지 않는다.
  **검증:** Play 모드에서 텔레포트 ray 를 anchor 위에 띄운 채 트리거 release 대신 cancel 입력 → 본체·손 상태 불변.

## Out of Scope

- MouthPiece / Mouth 의 *정확한* localPosition·localRotation 튜닝값 결정 — 본 plan 은 align 식과 기준점 *존재* 만 박제하고, 시각적 정합값 미세 조정은 사용자가 헤드셋 시각 검증 단계에서 수행한다.
- 슬라이드 운전 (오른손 Grip 동안 슬라이드 좌우 위치 추적) — 02 sub-spec.
- 발음 / 글리산도 / MIDI 이벤트 표면 (왼손 Grip + 슬라이드 위치 → 음높이) — 03 sub-spec.
- 본체와 신체·환경의 collision — 트럼본 피처 전체 범위 외.
- 텔레포트 외 자유 이동 (조이스틱) 으로 anchor 영역 안팎 이동 시 attach/detach — ARD 01 §Consequences 에 따라 의도된 무발화.
- 멀티플레이어 동기화 — 트럼본 피처 전체 범위 외.
- PlayHandPoseDriver / PhysicsHandGhostFollower / Hands 시스템 코드 자체 수정 — Tech Spec §Boundaries 의 "건드리지 않는다".
- DrumKitStickAnchor.cs 수정 (공통 헬퍼 추출 등) — ARD 01 §Consequences 에 따라 본 plan 은 *재발명 없이 답습* 만, 공통화는 별도 plan 후보로 미룬다.

## Notes

- ARD 01 §Consequences 마지막 조항 (DrumKitStickAnchor 패턴이 깨지면 본 결정 재평가) 을 트리거하지 않으려면 본 plan 의 selectExited/locomotionStarted/pendingWindow 시그니처를 drum-stick 과 1:1 로 유지한다. 변경 차이는 attach 내용(Instantiate→Align) 과 detach 내용(Destroy→Restore) 뿐.
- LateUpdate 실행 순서: 본 plan `[DefaultExecutionOrder(10005)]` < PlayHandPoseDriver `[DefaultExecutionOrder(10010)]`. 같은 프레임 안에 trombone root pose 가 먼저 정해진 뒤 PlayHand 가 그 자식 GripPoseHand 를 source 로 읽어가는 순서가 보장된다. 이 순서가 깨지면 Tech Spec §Invariants ("한 프레임 내 중간 상태 부재") 가 깨지므로 후속 plan 이 절대 그 순서를 뒤집지 않도록 본 노트에 박제.
- 공통 헬퍼 (drum-stick / trombone 공유 selectExited+pendingWindow 골격) 추출은 ARD 01 §Consequences 에서 "plan 에서 결정" 으로 위임됐는데, 본 plan 은 답습으로 처리하고 공통화는 미래 plan 후보로 둔다 — 현재 호출 시점에 단 2 개 호출자라 추출 부담 > 가치.
- 구현 단계에서 `TromboneAnchor.cs` 에 plan §Approach 3 에 명시되지 않은 `leftPhysicsHand` / `rightPhysicsHand` (`GameObject`) 2 개 SerializeField 가 추가됐다. DrumKitStickAnchor 의 `SetPhysicsHandsActive(false/true)` 패턴 답습으로, attach 동안 PhysicsHand 가시성 복구를 담당한다. ARD 01 의 "drum-stick 동일" 결정 범위 내. 결과적으로 TromboneAnchor 의 SerializeField 는 총 7 개 (left/right PlayHandDriver, tromboneRoot, mouthPieceOnTrombone, mouthOnPlayer, left/right PhysicsHand).
- MouthPiece localPosition 은 `(0, 0, 0)` 로 박제. AlignTromboneToMouth 의 역계산식이 그대로 동작하지만 트럼본 root 와 입이 동일 위치에 오므로 헤드셋 시각 검증 단계에서 사용자가 trombone-root-local offset 으로 조정 필요. (Out of Scope 박제 — plan §Out of Scope 첫 항목.)
- Reviewer / unity-test-runner 자동 검증 결과:
  - reviewer: `approved` — diff vs plan 의도 정합, 관찰 5건 모두 의도 내 또는 fine.
  - unity-test-runner: `EditMode 102/102 pass`, `PlayMode 4/4 pass`, `Console errors: 0`. 회귀 없음.
- Manual-hard 5 개 (AC 6–10) 는 사용자 헤드셋 시각 검증이 필요한 항목으로 본 plan commit 시점에는 `pending` 상태. 사용자가 sub-spec 03 종료 후 일괄 검증 예정 — 본 시점에 자동 검증 가능한 axis 는 모두 통과.

## Handoff

- **Trombone scene 인스턴스 root Transform** — TestSceneSanyo `Trombone` GameObject. 02 sub-spec 의 slide controller 가 동일 root 의 `Slide` 자식 Transform 을 운전 대상으로 잡는다.
- **Slide 자식 Transform** — `Trombone/Slide` (MeshFilter + MeshRenderer + 신규 자식 GripPoseHand). 본 plan 시점의 `Slide.localPosition.x = -0.58953875`. 02 sub-spec 이 이 값을 baseline slide x 의 초기값으로 사용.
- **MouthPiece / Mouth Transform 박제** — `Trombone/MouthPiece` (localPosition 미튜닝) 및 VR Player `Main Camera/Mouth` (localPosition `(0, -0.05, 0.10)` 임시값). 03 sub-spec 의 발음 라이프사이클은 본 align 식 결과에 의존하지 않는다 (Slide.localPosition.x 만 사용).
- **TromboneAnchor scene 컴포넌트** — `Instruments.TromboneAnchor` (scene root `TromboneAnchor` 에 부착). 02 / 03 sub-spec 의 enable 신호 source. 본 plan 은 `m_IsAttached` 를 private 으로 둔다 — 02 / 03 plan 이 공개 표면이 필요하면 별도 read-only property 또는 event 추가로 결정.
- **PlayHandPoseDriver source override 슬롯** — attach 동안 좌/우 driver 가 각각 `Body/GripPoseHand` + `Slide/GripPoseHand` 를 점유. 02 / 03 sub-spec 은 이 슬롯을 건드리지 않는다 (오른손 Grip 은 슬라이드 운전 입력 source 로만 사용, PlayHand override 는 본 plan 의 점유 그대로 유지).
- **Trombone.prefab 골격 변경** — Body/GripPoseHand, Slide/GripPoseHand (각 30 개 뼈 + PreviewMesh), MouthPiece 자식 신규. drum_stick_L/R 의 GripPoseHand 서브트리를 깊은 복사. 02 sub-spec 의 TromboneSlideController 는 Slide GameObject 에 부착되며 자식 GripPoseHand 가 자동 추종.
