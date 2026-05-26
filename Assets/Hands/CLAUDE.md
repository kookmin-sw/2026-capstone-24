# Assets/Hands — VR 손 시스템 가이드

VR 손은 **3개 레이어로 책임을 분리**한다. 새 악기에 손을 붙일 때는 손 코드를 건드리지 않고, **악기 prefab 안에 `GripPoseHand`만 만들면 된다.**

## 1. 3-Layer 손

| Layer | 입력 | 물리 | 렌더 | 책임 |
|---|:-:|:-:|:-:|---|
| **Ghost** | ✅ VR 추적 | ✗ | ✗ | 컨트롤러/핸드트래킹 입력을 뼈 계층으로 변환 |
| **Physics** | ✗ | ✅ Rigidbody/Collider | ✗ | Ghost 뼈를 따라가는 충돌체 |
| **Play** | ✗ | ✗ | ✅ | 사용자에게 보이는 손, Physics(또는 Ghost) 포즈 미러링 |

데이터 흐름: `VR Input → Ghost → (PhysicsHandGhostFollower) → Physics → (PlayHandPoseDriver) → Play`

레퍼런스: `Scripts/PhysicsHandGhostFollower.cs`, `Scripts/PlayHandPoseDriver.cs`

## 2. Grip Pose Override 원칙

**Play Hand는 어떤 물체를 잡고 있는지 모른다.** 그 물체를 운전하는 컨테이너(anchor·socket 등)가 손에 뼈 계층을 "주입"한다.

- 진입점: `PlayHandPoseDriver.PushSourceOverride(gripRoot, gripWristRoot)` / `PopSourceOverride()`
- 우선순위: **Grip Override > Physics > Ghost(fallback)**
- 트리거: 컨테이너 컴포넌트가 자신의 부착/해제 신호에 맞춰 직접 `Push/Pop`을 호출한다. 표준 예: `Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs` (TeleportationAnchor가 stick을 spawn/destroy하고 grip override를 운전).

→ **새 악기는 손 코드를 건드리지 않는다.** 악기 prefab 안에 `GripPoseHand`만 만들고, 그 악기를 spawn하거나 attach하는 컨테이너가 `Push/Pop`을 부른다.

## 3. DrumStick 예시 (GripPoseHand 구조)

stick은 "잡은 자세"의 뼈 포즈를 `GripPoseHand` 자식 계층에 **정적으로 박제**한다. 컨테이너가 `PushSourceOverride(GripPoseHand, R_Wrist)`를 호출하면 `PlayHandPoseDriver`가 매 프레임 stick의 뼈를 읽어 PlayHand의 **같은 이름** 뼈에 복사 → 사용자에게는 stick을 쥔 손이 보인다.

Hierarchy (`Assets/Instruments/Drum/Prefabs/drum_stick_R.prefab`):
```
drum_stick (root)
└── GripPoseHand                       [GripPoseHandPreview]
    ├── R_Wrist
    │   ├── R_IndexMetacarpal → Proximal → Intermediate → Distal → Tip
    │   ├── R_Middle… / R_Ring… / R_Little…  (동일 5단)
    │   ├── R_ThumbMetacarpal → Proximal → Distal → Tip
    │   └── R_Palm
    └── PreviewMesh [EditorOnly]
        └── HandMeshPreview [SkinnedMeshRenderer]
```

함정:
- 뼈 이름이 `RightPlayHand/R_Wrist` 하위와 다르면 그 뼈는 적용되지 않는다(이름 매칭).
- `PreviewMesh`는 반드시 **R_Wrist의 sibling**. `PlayHandPoseDriver.BuildJointMap`이 wrist에서 재귀로 뼈를 수집하기 때문에 wrist 안에 mesh가 있으면 매칭이 깨진다. 런타임 가시성은 `GripPoseHandPreview`가 자동 비활성화로 처리.
- L variant는 `L_Wrist` 이름만 다르고 구조 동일.

레퍼런스: `Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs` — anchor→손 운전의 표준 형태.

## 4. 새 악기에 손 붙이기 (예: 바이올린 활)

1. 악기 prefab 루트에 `Rigidbody` + `Collider` 추가. 자유 집기(anchor 없이 손으로 직접 잡는) 모드가 필요하다면 `XRGrabInteractable` + 별도 grab→손 브리지를 직접 마련. anchor·socket 같은 컨테이너 모델이라면 추가하지 않는다.
2. 루트 자식으로 `GripPoseHand` 빈 GameObject 생성 → `GripPoseHandPreview` 컴포넌트 부착.
3. `RightPlayHand/R_Wrist` 뼈 계층을 `GripPoseHand` 아래로 복사(이름·구조 보존). 좌/우 별도 variant.
4. 악기를 잡는 자세에 맞춰 `R_Wrist` 및 손가락 뼈들의 localPosition/Rotation 편집.
5. 운전 책임 wiring: 악기를 spawn/attach하는 컨테이너(anchor 등)가 `transform.Find("GripPoseHand")` / `Find("L_Wrist" or "R_Wrist")`로 탐색해 `PlayHandPoseDriver.PushSourceOverride(gripPoseHand, wrist)`를 호출하고, 해제 시 `PopSourceOverride()`로 복귀. `DrumKitStickAnchor.BindStickAndPushOverride`가 표준 형태.
6. (선택) `PreviewMesh`(R_Wrist의 sibling) 아래에 PlayHand의 SkinnedMeshRenderer 복사본을 두고 본 재바인딩 → 에디터에서 잡힌 손 모양 확인용.
7. 검증: `Tools/Hands/Validate Grip Pose Wiring` 실행 (GripPoseHand 구조·뼈 일치·PreviewMesh 점검).

→ DrumStick의 `Editor/DrumStickSetup.cs`를 prefab 골격 자동화 템플릿으로, `DrumKitStickAnchor.cs`를 운전 컨테이너 템플릿으로 삼는다.

## 5. 멀티플레이어 hand sync (원격 손)

로컬 손은 §1~§4로 끝. **원격 player 손은 Multiplayer 도메인이 본 도메인의 Ghost 단계를 캡쳐해 다른 client로 송신**한다. Hands 도메인 자체는 Multiplayer를 모른다 — 단방향 의존(Multiplayer → Hands).

데이터 흐름:

```
[로컬 client]
Ghost (VR 입력 뼈)
    │ wrist + 25본 localRotation 추출
    ▼
Multiplayer/Multiplay/LocalHandPoseSource   (씬 배치, client only)
    │ RPC_PushPose
    ▼
[server] NetworkedWristPose  ─ [Networked] (StateAuthority만 쓰기)
    │
    ▼
[원격 client]
PlayerHandRig.prefab 의 자식
    └─ LeftRemoteHandRenderer.prefab / RightRemoteHandRenderer.prefab
       (Multiplayer/Prefabs/, 본 이름 매칭으로 SkinnedMeshRenderer 갱신)
```

본 이름 매칭 규약: `Multiplayer/Scripts/Multiplay/RemoteHandBoneNames` (`RightSide`/`LeftSide` 배열) 가 25본 정렬 순서를 박제(Index→Middle→Ring→Little→Thumb→Palm, `L_`/`R_` 프리픽스). **본을 추가/이름 변경하면 양 client 의 Ghost prefab 과 RemoteHandRenderer prefab 모두에 반영해야 한다.**

자세한 토폴로지 / RPC 패턴은 `Assets/Multiplayer/CLAUDE.md` 참조.
