# 원격 손 시각화 — Tech Spec

**Sub-Spec:** [`01-remote-hand-visualization.md`](../specs/01-remote-hand-visualization.md)
**Status:** `Draft`
**Date:** 2026-05-19

## Components

- **PlayerHandRig** (신규, NetworkObject) — 룸 합류 시 dedicated server 가 한 플레이어당 1개 spawn 하는 네트워크 컨테이너. 왼손·오른손 pose 와 닉네임 라벨 텍스트를 한 곳에 묶는다. inputAuthority = 그 플레이어.
- **NetworkedHandPose** (신규, NetworkBehaviour) — PlayerHandRig 의 자식 2개 (L / R). wrist global pose + 손가락 본 26개의 localRotation 을 [Networked] 필드로 보관.
- **LocalHandPoseSource** (신규) — inputAuthority 클라이언트에서 PlayHand prefab 의 본 transform 을 매 틱 읽어 NetworkedHandPose 의 networked 필드에 쓰는 단방향 writer.
- **RemoteHandRenderer** (신규, prefab) — RightPlayHand / LeftPlayHand 의 SkinnedMeshRenderer + 본 계층 사본. Ghost / Physics / 입력 컴포넌트 일체 없음. NetworkedHandPose 의 [Networked] 값을 자기 본 계층에 매 프레임 적용.
- **HandNameTag** (신규) — RemoteHandRenderer 의 자식. wrist 위에 닉네임 텍스트 worldspace 라벨을 표시. inputAuthority 클라이언트에서는 자기 라벨을 hide.
- **PlayerHandRigSpawner** (신규) — RoomAuthority 의 OnPlayerJoined / OnPlayerLeft 훅에 끼어들어 PlayerHandRig 를 Spawn / Despawn.
- **PlayHand prefab** (기존, `Assets/Hands/Prefabs/Play/{Right,Left}PlayHand.prefab`) — 본 계층의 단일 진실원. 직접 수정하지 않고 읽기만 한다.
- **RoomAuthority** (기존, `Assets/Multiplayer/Scripts/Room/Server/RoomAuthority.cs`) — 합류·퇴장 콜백의 진입점.

## Data / Control Flow

- **합류 시:** 클라이언트가 룸 join 성공 → `RoomAuthority.OnPlayerJoined` → `PlayerHandRigSpawner` 가 `runner.Spawn(playerHandRig, inputAuthority: player)` → 모든 클라이언트에 PlayerHandRig + 자식 NetworkedHandPose L/R + RemoteHandRenderer L/R 가 생성 → inputAuthority 클라이언트는 자기 RemoteHandRenderer / HandNameTag 를 hide.
- **매 틱 (inputAuthority 클라이언트):** PlayHand `R_Wrist` / `L_Wrist` 본 global pose + 손가락 본 25개 localRotation → `LocalHandPoseSource` → `NetworkedHandPose` 의 [Networked] 필드 write.
- **매 프레임 (다른 클라이언트):** Fusion 이 보간한 [Networked] 값 → RemoteHandRenderer 가 자기 본 계층에 적용 → SkinnedMeshRenderer 렌더.
- **퇴장 시:** `RoomAuthority.OnPlayerLeft` → `PlayerHandRigSpawner` 가 그 플레이어 PlayerHandRig 를 Despawn → 모든 클라이언트에서 그 손이 즉시 사라짐.
- **Late join:** Fusion 의 [Networked] join snapshot 메커니즘이 자동으로 신규 클라이언트에 기존 PlayerHandRig 들의 현재 state 를 전달.

## Boundaries

- **건드린다**: `RoomAuthority` (합류·퇴장 훅 추가 진입점), 새 `Assets/Multiplayer/Scripts/Multiplay/` 폴더 (신규 컴포넌트 일체), default 씬 (`TestSceneSanyo.unity`) 에 PlayerHandRig prefab reference 등록.
- **건드리지 않는다**: PlayHand prefab 본체, Ghost / Physics 손 계층, `PlayHandPoseDriver`, `InstrumentBase` 및 자식 악기 일체, 기존 `RoomClient.cs` 의 join 흐름.

## Invariants

- PlayerHandRig 의 inputAuthority 는 한 플레이어에만 부여되고, 그 플레이어 클라이언트만 그 NetworkedHandPose 에 쓴다.
- RemoteHandRenderer 는 collider / Rigidbody 를 갖지 않는다 — 다른 사람 손이 자기 악기 collider 를 건드려 NoteOn 을 트리거하는 일이 발생할 수 없다.
- 본인 클라이언트는 자기 PlayerHandRig 의 시각 렌더를 hide 한다 — 자기 PlayHand 와 자기 RemoteHandRenderer 가 같은 화면에 겹쳐 보이지 않는다.
- 본 이름 매칭은 PlayHand prefab 의 본 이름 (R_Wrist, R_IndexMetacarpal, R_IndexProximal, …) 과 RemoteHandRenderer 의 본 이름이 정확히 일치해야 적용된다.

## Assumptions

- PlayHand prefab 의 본 계층은 손당 총 26개 본 (wrist 1 + Index/Middle/Ring/Little 각 5단 = 20 + Thumb 4단 = 4 + Palm 1). 본 이름 prefix 는 우측 `R_`, 좌측 `L_`. 출처: `Read Assets/Hands/Prefabs/Play/RightPlayHand.prefab + LeftPlayHand.prefab (2026-05-19)`.
- Photon Fusion 의 [Networked] 필드는 late-join 클라이언트에 join snapshot 으로 현재 state 를 자동 전달한다. 출처: Photon Fusion 2 문서 (구현 plan 단계에서 검증).
- dedicated server 는 headless 모드 (`-batchmode -nographics`, OpenXR loader 비활성). PlayerHandRig 의 SkinnedMeshRenderer 는 서버에서 렌더 대상이 없고 NetworkObject 상태 컨테이너로만 동작한다. 출처: `docs/specs/_archive/multiplayer-network/plans/2026-05-09-namae1128-dedicated-server-build-openxr-toggle.md`.
- 표시 닉네임 source 는 multiplayer-network 의 04-presence-ui 가 이미 사용하는 표시명 채널을 그대로 재활용한다. 출처: `Read docs/specs/multiplayer-network/specs/04-presence-ui.md (2026-05-19)`.

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `Assets/Hands/Prefabs/Play/{Right,Left}PlayHand.prefab` | RemoteHandRenderer prefab | PlayHand 는 VR 입력 추적 + Ghost / Physics 의존 + grip override 가 모두 결합된 풀 3-Layer 손. RemoteHandRenderer 는 시각 SkinnedMeshRenderer + 본 계층만, 입력 / 물리 / grip override 일체 없음. |

### Prefab Hierarchy (참고 박제)

원천: `Read Assets/Hands/Prefabs/Play/RightPlayHand.prefab (2026-05-19)`.

```
RightPlayHand
└── R_Wrist
    ├── R_IndexMetacarpal → R_IndexProximal → R_IndexIntermediate → R_IndexDistal → R_IndexTip
    ├── R_MiddleMetacarpal → R_MiddleProximal → R_MiddleIntermediate → R_MiddleDistal → R_MiddleTip
    ├── R_RingMetacarpal → R_RingProximal → R_RingIntermediate → R_RingDistal → R_RingTip
    ├── R_LittleMetacarpal → R_LittleProximal → R_LittleIntermediate → R_LittleDistal → R_LittleTip
    ├── R_ThumbMetacarpal → R_ThumbProximal → R_ThumbDistal → R_ThumbTip
    └── R_Palm
```

LeftPlayHand 도 동일 구조 (`L_` prefix).

## Open Tech Decisions

- [x] 손 pose 송신을 [Networked] 본 단위 quaternion 직접 송신 vs 압축 pose snapshot 으로 할 것인지. → [decisions/01-hand-pose-network-encoding.md](../decisions/01-hand-pose-network-encoding.md)
