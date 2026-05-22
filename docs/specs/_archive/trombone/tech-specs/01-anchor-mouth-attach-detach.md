# Anchor & Mouth Attach/Detach — Tech Spec

**Sub-Spec:** [`01-anchor-mouth-attach-detach.md`](../specs/01-anchor-mouth-attach-detach.md)
**Status:** `Draft`
**Date:** 2026-05-17

## Components

- **TromboneAnchor** (신규) — TromboneAnchor scene root 에 부착되는 컴포넌트. TeleportationAnchor 의 selectExited + LocomotionProvider 의 locomotionStarted 를 페어로 묶어 attach / detach 라이프사이클을 운전.
- **Trombone (prefab)** (기존) — 본체. 자식으로 Body / Slide / **MouthPiece (신규 빈 Transform)** / Body·Slide 각각의 **GripPoseHand (신규)**.
- **VR Player rig** (기존) — Main Camera 아래에 **Mouth (신규 빈 Transform)** 가 추가됨. MouthPiece 정렬 대상점.
- **PlayHandPoseDriver (좌/우)** (기존) — TromboneAnchor 가 attach 시 PushSourceOverride(GripPoseHand, L_Wrist / R_Wrist), detach 시 PopSourceOverride 호출.
- **TeleportationAnchor** (기존, XR Interaction Toolkit) — selectExited 신호 발행 + teleportationProvider 노출.
- **LocomotionProvider** (기존, XR Interaction Toolkit) — locomotionStarted 신호 발행.

## Data / Control Flow

- 사용자 텔레포트 시도 → TeleportationAnchor.selectExited → TromboneAnchor 가 isCanceled 체크 후 m_PendingAttachFrame = Time.frameCount 기록.
- LocomotionProvider.locomotionStarted → TromboneAnchor 가 pending window 안이면 AttachTromboneToMouth() + Push 양손 GripPose. window 밖이면 Detach (원위치 복귀 + Pop).
- 매 프레임 attach 상태 동안 트럼본 root 의 (position, rotation) = VR Player Mouth 의 (position, rotation) 을 MouthPiece 의 trombone-root-local offset 으로 역계산 (LateUpdate). MouthPiece localTransform 은 prefab 박제된 고정값.
- detach 시 트럼본 root 의 (position, rotation) ← anchor 진입 직전에 캐시된 원래 scene 위치.

## Boundaries

- **건드린다**: TromboneAnchor scene root 1개 생성 (TestSceneSanyo 안), Trombone.prefab 에 MouthPiece + Body/GripPoseHand + Slide/GripPoseHand 자식 추가, VR Player.prefab 에 Mouth 자식 1개 추가.
- **건드리지 않는다**: PlayHandPoseDriver / PhysicsHandGhostFollower / Hands 시스템 코드, XR Interaction Toolkit 내부, DrumKitStickAnchor (참조만), Trombone.cs / Slide 운전 로직 (02, 03 sub-spec 책임).

## Invariants

- PlayHand source override 슬롯은 동시에 1개만 점유한다 (Hands 도메인 규약). detach 의 Pop 호출이 Push 와 정확히 1:1.
- attach 상태 == "트럼본이 입에 정렬 + 양손 GripPose 적용" — 둘 중 한쪽만 활성된 중간 상태는 한 프레임 내에서도 존재하지 않는다.
- 같은 anchor 재텔레포트 시 m_IsAttached == true 이면 AttachTromboneToMouth 호출 무시 (drum-stick 답습).
- 트럼본 원위치 복귀 좌표는 anchor 진입 직전 시점의 scene 좌표 — 매 detach 마다 새로 캐시하지 않는다 (최초 1회 캐시).

## Assumptions

- Trombone.prefab 의 현재 hierarchy 는 root + Body + Slide 2개 자식뿐, 컴포넌트는 MeshFilter / MeshRenderer 만. 출처: `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-17)`.
- VR Player.prefab 에는 Main Camera 만 존재, Mouth 자식 없음. 출처: `Grep VR Player.prefab (2026-05-17)`.
- DrumKitStickAnchor (`Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs`) 의 selectExited + locomotionStarted pending window 패턴은 검증됨. 출처: `Read DrumKitStickAnchor.cs (2026-05-17)`.
- PlayHandPoseDriver 의 Push/Pop API 가 GripPoseHand 자식 모델 표준 진입점. 출처: `Read Assets/Hands/CLAUDE.md (2026-05-17)`.

### Prefab Hierarchy (목표 상태)

```
Assets/Instruments/Trombone/Prefabs/Trombone.prefab
Trombone (root, Transform only)
├── Body (Transform + MeshFilter + MeshRenderer)
│   └── GripPoseHand (신규)
│       ├── L_Wrist (+ 뼈 계층, drum_stick_L 답습)
│       └── PreviewMesh (EditorOnly, sibling of L_Wrist)
├── Slide (Transform + MeshFilter + MeshRenderer)
│   └── GripPoseHand (신규)
│       ├── R_Wrist (+ 뼈 계층, drum_stick_R 답습)
│       └── PreviewMesh (EditorOnly, sibling of R_Wrist)
└── MouthPiece (신규, 빈 Transform — VR Player.Mouth 와 align 되는 기준점)

Assets/Characters/Prefabs/VR Player.prefab
... > Main Camera
        └── Mouth (신규, 빈 Transform — MouthPiece 정렬 대상점)

TestSceneSanyo (scene root)
└── TromboneAnchor (신규 GameObject)
    ├── TeleportationAnchor
    └── TromboneAnchor (컴포넌트)
        └── 참조: leftPlayHandDriver, rightPlayHandDriver, tromboneRoot,
                  mouthPieceOnTrombone, mouthOnPlayer
```

출처: `Read Trombone.prefab` (현재) + 본 Tech Spec 의 attach 모델로부터 도출 (목표).

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs` | `TromboneAnchor` | drum-stick 은 stick prefab 을 Instantiate / Destroy. trombone 은 단일 본체를 displace / restore (원래 scene 위치로 복귀). attach 단위가 "양손 GripPose" 만 vs "양손 GripPose + 본체 정렬" 인 점도 다름. |
| `docs/specs/drum-stick/specs/01-anchor-auto-attach-detach.md` (Done) | `01-anchor-mouth-attach-detach.md` | 책임 범위에 본체 정렬이 추가됨. detach 시 본체 복귀가 핵심 동작. |

## Open Tech Decisions

- [x] anchor 진입/이탈 트리거 모델 → [decisions/01-anchor-attach-detach-trigger.md](../decisions/01-anchor-attach-detach-trigger.md)
