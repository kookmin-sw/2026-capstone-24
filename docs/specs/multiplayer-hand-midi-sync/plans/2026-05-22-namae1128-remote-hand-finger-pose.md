# 원격 손 손가락 본 동기화 — SkinnedMesh + 손가락 26개 본

**Linked Spec:** [`01-remote-hand-visualization.md`](../specs/01-remote-hand-visualization.md)
**Status:** `Ready`

## Goal

선행 plan [`2026-05-20-namae1128-remote-hand-wrist-placeholder.md`](./2026-05-20-namae1128-remote-hand-wrist-placeholder.md) 의 wrist-only 단계를 확장해, **다른 플레이어의 손이 placeholder 큐브 대신 PlayHand SkinnedMesh 형태로 보이고 손가락 본 25개까지 추적**되도록 한다. drum stick grip override 같은 grip 자세도 본 단위 동기화로 자동 따라온다. sub-spec 01 의 What 박제 ("wrist + 손가락 본 전부") 의 나머지 5개 Behavior 항목을 본 plan 이 닫는다.

## Context

선행 plan #1 은 wrist 1개의 position + rotation 만 RPC 로 송신해 placeholder 큐브를 (0,0,0) 에서 host 의 wrist 위치로 옮기는 최소 manual-hard 단위였다. 본 plan 은 다음 3가지를 추가한다:

1. **시각**: placeholder cube → PlayHand SkinnedMesh + 본 계층 (`RemoteHandRenderer` prefab)
2. **본 단위 동기화**: 손가락 본 25개 (각 손) localRotation 을 매 LateUpdate RPC 송신
3. **grip override 자연 반영**: PlayHand 본 transform 자체가 grip override 로 stick grip 자세를 띠므로, 본 transform read → 원격 적용 시 grip 자세가 자동 반영. 별도 grip state wiring 불요.

ARD [`01-hand-pose-network-encoding.md`](../decisions/01-hand-pose-network-encoding.md) 의 결정 ("Fusion [Networked] 본 단위 quaternion 직접") 의 적용 시점. ARD 의 Consequences 의 "룸 정원 가정 대역폭 측정 AC" 도 본 plan 의 책임.

선행 plan #1 이 박제한 사실 (NetworkProjectConfig.AssembliesToWeave 에 `Murang.Multiplayer` + asmdef `allowUnsafeCode: true`, RoomServerBootstrap 의 spawner 등록 4-block, RPC_PushPose 패턴) 을 그대로 활용. 본 plan 은 NetworkedWristPose 의 [Networked] 필드 + RPC 시그니처 확장 + RemoteHandRenderer prefab 신규 + PlayerHandRig.prefab 의 자식 교체만 다룬다.

## Verified Structural Assumptions

- **PlayHand prefab 의 본 계층** — `RightPlayHand.prefab` (GUID `53319ed4d09380a4a8686d13bbd88c6f`) 의 본 계층은 `R_Wrist` 루트 + 25개 자식 본 (Index/Middle/Ring/Little 각 5단 + Thumb 4단 + Palm). `LeftPlayHand.prefab` (GUID `91fcefda33a1e854191bc7594bc104cc`) 은 `L_` prefix. 본 이름 list 는 Hands CLAUDE.md §3 박제. — `Read Assets/Hands/Prefabs/Play/{Right,Left}PlayHand.prefab (2026-05-22)`
- **PlayHandPoseDriver 의 본 적용 패턴** — `Application.onBeforeRender` + LateUpdate 양쪽에서 매 프레임 `sourceJoint.localPosition/localRotation` 을 target joint 에 복사 (`Assets/Hands/Scripts/PlayHandPoseDriver.cs:182-218`). 본 매칭은 `BuildJointMap` 이 wrist root 부터 재귀로 이름→Transform Dictionary 생성. 본 plan 의 RemoteHandRenderer 도 동일 본 이름 매칭으로 적용. — `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-22)`
- **PlayHand prefab 의 SkinnedMeshRenderer + bone array** — RightPlayHand prefab 의 SkinnedMeshRenderer 가 본 26개 transform reference 의 bone array 보유. RemoteHandRenderer prefab 도 같은 mesh + bone array 사용 — 다만 본 transform 들은 RemoteHandRenderer prefab 자체의 본 계층 가리킴. — `Glob Assets/Hands/Prefabs/Play/*.prefab (2026-05-22)`
- **NetworkedWristPose 의 RPC 시그니처 확장 가능성** — Fusion 2 의 `[Rpc]` 는 Vector3, Quaternion, 그리고 array (Quaternion[]) parameter 지원. RPC payload 는 ~456 byte/손 = ~912 byte/플레이어 (60Hz). 8명 룸 시 ~440 KB/s — Fusion 대역폭 임계 (보통 1 MB/s) 내. ARD 01 의 대역폭 측정 AC 가 본 plan 의 manual-hard 시점에 확인. — `Read Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml (2026-05-22, RPC parameter support)`
- **PlayerHandRig.prefab 의 현재 자식 구조** — root `PlayerHandRig` (NetworkObject + `PlayerHandRig` NetworkBehaviour) + 자식 `LeftHand`/`RightHand` (각각 NetworkedWristPose + MeshFilter Cube + MeshRenderer RemoteHandPlaceholder mat). 본 plan 은 자식의 Cube MeshFilter/MeshRenderer 를 제거하고 RemoteHandRenderer prefab 의 본 계층 + SkinnedMeshRenderer 로 교체. NetworkObject 의 NetworkedBehaviours 배열은 NetworkedWristPose 만 유지. — `Read Assets/Multiplayer/Prefabs/PlayerHandRig.prefab (2026-05-22)`
- **RemoteHandPlaceholder.mat 의 활용** — 본 plan 이후 placeholder cube 제거되면 .mat 도 미사용. 추후 plan 에서 정리. 본 plan 에서는 RemoteHandRenderer 의 SkinnedMeshRenderer 는 PlayHand 의 hand mesh 본 material 그대로 사용. — `Read Assets/Multiplayer/Materials/RemoteHandPlaceholder.mat (2026-05-22)`

## Approach

1. **`RemoteHandRenderer` prefab 2개 신규**
   - `Assets/Multiplayer/Prefabs/LeftRemoteHandRenderer.prefab`, `RightRemoteHandRenderer.prefab`
   - PlayHand prefab 의 본 계층 (`L_Wrist`/`R_Wrist` + 25 본) 복사 + SkinnedMeshRenderer (same hand mesh + bone array → RemoteHand 의 본 계층 참조)
   - **Ghost / Physics / 입력 / Collider / Rigidbody / PlayHandPoseDriver 일체 제거** — 시각 전용
   - Tech Spec Boundary: PlayHand prefab 본체 무수정. RemoteHandRenderer 는 별 prefab 자산.

2. **`PlayerHandRig.prefab` 자식 교체**
   - 기존 LeftHand/RightHand (Cube MeshFilter+MeshRenderer + NetworkedWristPose) → 자식 자체를 RemoteHandRenderer prefab instance 로 교체. 단 NetworkedWristPose 는 RemoteHandRenderer 의 wrist root (`L_Wrist`/`R_Wrist`) 에 부착.
   - 즉 새 PlayerHandRig.prefab 구조:
     ```
     PlayerHandRig (NetworkObject + PlayerHandRig NB)
     ├── LeftHand (RemoteHandRenderer prefab instance)
     │   ├── L_Wrist  (NetworkedWristPose + SkinnedMeshRenderer)
     │   │   └── 25 자식 본 …
     └── RightHand (RemoteHandRenderer prefab instance)
         └── R_Wrist  (NetworkedWristPose + SkinnedMeshRenderer)
             └── 25 자식 본 …
     ```

3. **`NetworkedWristPose` 의 [Networked] 필드 확장**
   - 기존 `WristPosition` (Vector3) + `WristRotation` (Quaternion) 유지
   - 신규 `[Networked, Capacity(25)] NetworkArray<Quaternion> FingerRotations { get; }` — 25개 손가락 본 localRotation
   - Fusion 의 `NetworkArray<T>` 는 [Networked] 의 정식 collection. RPC 송수신 자동.

4. **RPC 시그니처 확장**
   - 기존: `RPC_PushPose(Vector3 wristPos, Quaternion wristRot)`
   - 신규: `RPC_PushPose(Vector3 wristPos, Quaternion wristRot, Quaternion[] fingerRots)` — `fingerRots.Length == 25` 검증
   - server 측 RPC 본문: `WristPosition = wristPos; WristRotation = wristRot; for (i=0; i<25; i++) FingerRotations.Set(i, fingerRots[i]);`

5. **`LocalHandPoseSource` 확장 — 본 transform 25개 read**
   - 신규 헬퍼 메서드 `Quaternion[] ReadFingerRotations(Transform wristRoot)` — wrist 의 자식 본 25개를 이름 기반으로 indexing 후 localRotation 추출
   - 본 이름 list (정렬 고정): `R_IndexMetacarpal, R_IndexProximal, R_IndexIntermediate, R_IndexDistal, R_IndexTip, R_MiddleMetacarpal, ..., R_Palm` (Hands CLAUDE.md §3 박제 순서)
   - LateUpdate 의 WriteWristPose 가 `_cachedRig.LeftHand.RPC_PushPose(leftWristSource.position, leftWristSource.rotation, ReadFingerRotations(leftWristSource))` 호출

6. **`NetworkedWristPose.Render()` 확장 — 본 25개 적용**
   - 기존: `transform.SetPositionAndRotation(WristPosition, WristRotation)` 으로 wrist root 적용
   - 신규: 자기 wrist root 의 자식 본 25개 (RemoteHandRenderer 의 본) 의 localRotation 을 `FingerRotations[i]` 값으로 set. 본 이름 매칭 dictionary 매 Render 호출에서 cache.

7. **본 이름 매칭 정렬 보장**
   - LocalHandPoseSource 의 ReadFingerRotations + NetworkedWristPose.Render 가 **같은 본 이름 순서** 사용. 정렬 array 를 static readonly 로 박제 (`RemoteHandBoneNames.RightSide`, `LeftSide` static arrays).
   - PlayHand prefab 과 RemoteHandRenderer prefab 의 본 이름이 정확히 일치해야 함 (R_Wrist + R_IndexMetacarpal + …). Hands CLAUDE.md 박제 본 이름 사용.

8. **inputAuthority hide 확장**
   - 기존 `Spawned()` 의 `MeshRenderer.enabled = false` → `SkinnedMeshRenderer.enabled = false` 로 변경 (RemoteHand 의 SkinnedMeshRenderer hide).

9. **TestSceneSanyo wiring 추가** (사용자 작업)
   - `LocalHandPoseSource` 컴포넌트의 `leftWristSource`/`rightWristSource` 는 plan #1 에서 wire 됨 (PlayHand 의 wrist root). 그대로 사용.
   - 본 plan 은 LocalHandPoseSource 가 wristSource 의 자식 본 25개를 자동 검색 — Inspector wiring 추가 불요.

10. **컴파일 + manual-hard 재검증**
    - Unity Editor 컴파일 0 error
    - PlayerHandRig.prefab 직렬화 정합 (NetworkObject + 자식 2개 + 각 자식의 NetworkedWristPose + SkinnedMeshRenderer + 본 계층 25)
    - Quest APK 재빌드 + dedicated server 재빌드 + manual-hard

## Deliverables

- `Assets/Multiplayer/Prefabs/LeftRemoteHandRenderer.prefab` (신규) — LeftPlayHand 의 본 계층 + SkinnedMeshRenderer 시각 전용
- `Assets/Multiplayer/Prefabs/RightRemoteHandRenderer.prefab` (신규)
- `Assets/Multiplayer/Prefabs/PlayerHandRig.prefab` (수정) — 자식 LeftHand/RightHand 를 RemoteHandRenderer instance 로 교체. NetworkedWristPose 가 wrist root 에 부착.
- `Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs` (수정) — `[Networked] NetworkArray<Quaternion> FingerRotations` 추가, `RPC_PushPose` 시그니처 확장, `Render()` 의 본 25개 적용 로직 추가.
- `Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs` (수정) — `ReadFingerRotations` 헬퍼 + WriteWristPose 의 RPC 호출 시그니처 확장.
- `Assets/Multiplayer/Scripts/Multiplay/RemoteHandBoneNames.cs` (신규) — 본 이름 정렬 array 박제 (static readonly).
- `docs/specs/multiplayer-hand-midi-sync/specs/01-remote-hand-visualization.md` — Implementation Plans 표에 본 plan 행 추가.

## Acceptance Criteria

- [ ] `[auto-hard]` `NetworkedWristPose.cs` 가 `[Networked] NetworkArray<Quaternion> FingerRotations` 필드 보유 (capacity 25).
  **검증:** `Grep "NetworkArray<Quaternion>" Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs` ≥ 1 매치 + `Grep "Capacity\(25\)" Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs` ≥ 1 매치.

- [ ] `[auto-hard]` `LocalHandPoseSource.cs` 가 `ReadFingerRotations` 메서드 + 본 이름 기반 indexing 로직 보유.
  **검증:** `Grep "ReadFingerRotations" Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs` ≥ 2 매치 (선언 + 호출).

- [ ] `[auto-hard]` `RemoteHandBoneNames.cs` 가 static readonly array 보유 — `RightSide` / `LeftSide` 각 25 element + Hands CLAUDE.md §3 박제 본 이름 일치.
  **검증:** `Grep "R_IndexMetacarpal\|L_IndexMetacarpal" Assets/Multiplayer/Scripts/Multiplay/RemoteHandBoneNames.cs` ≥ 2 매치.

- [ ] `[auto-hard]` `LeftRemoteHandRenderer.prefab` + `RightRemoteHandRenderer.prefab` 가 존재하고 SkinnedMeshRenderer 컴포넌트 1개 + 본 계층 25 자식 보유. Collider/Rigidbody 0건.
  **검증:** `Grep "SkinnedMeshRenderer" Assets/Multiplayer/Prefabs/LeftRemoteHandRenderer.prefab Assets/Multiplayer/Prefabs/RightRemoteHandRenderer.prefab` 각 1 매치 + `Grep -P "Collider|Rigidbody" ...` 0 매치.

- [ ] `[auto-hard]` `PlayerHandRig.prefab` 의 자식 LeftHand/RightHand 가 RemoteHandRenderer prefab instance 로 교체됨. NetworkedWristPose 컴포넌트가 각 wrist root 에 부착.
  **검증:** `Grep "LeftRemoteHandRenderer\|RightRemoteHandRenderer" Assets/Multiplayer/Prefabs/PlayerHandRig.prefab` ≥ 2 매치.

- [ ] `[auto-hard]` Unity Editor 컴파일 0 error. `[Networked]`/RPC weaving 정상 (이전 plan #1 의 weaving fix 적용 상태 유지).
  **검증:** `read_console types=["error"]` 0 매치.

- [ ] `[auto-soft]` 대역폭 측정 — Fusion Statistics 또는 CloudWatch 의 task throughput 으로 8명 룸 가정 시 ~440 KB/s 임계 내. 초과 시 압축 plan 트리거 (ARD 01 재평가).
  **검증:** manual-hard 중 Fusion Statistics HUD (`NetworkRunnerStats`) screenshot 또는 server CloudWatch metric. 임계 초과면 Notes 박제 + 후속 plan 시드.

- [ ] `[manual-hard]` host 가 주먹 쥐기 → client 화면에서 host 의 빨강 placeholder 가 아니라 **SkinnedMesh 손이 보이고 손가락 5개가 동시 굽혀짐**.
  **검증:** 듀얼 Quest 시각 회귀.

- [ ] `[manual-hard]` host 가 drum stick 잡고 grip override 활성 → client 화면에서 그 grip 손 자세 (stick 잡은 모양) 자연 표시.
  **검증:** 듀얼 Quest 시각 회귀 — host stick 자세 ↔ client 화면 손 자세 일치.

- [ ] `[manual-hard]` host 가 V 사인 (검지/중지만 펴기) → client 화면에서 같은 V 사인 표시.
  **검증:** 듀얼 Quest 시각 회귀.

- [ ] `[manual-hard]` host 가 piano 키 누름 (검지 펴서 키 누름) → client 화면에서 같은 동작.
  **검증:** 듀얼 Quest 시각 회귀.

- [ ] `[manual-hard]` plan #1 의 시각 4건 (큐브 보임 / 따라움직임 / 자기 화면 미보임 / Leave 즉시 사라짐) 의 동작이 SkinnedMesh 로 교체된 후에도 회귀.
  **검증:** 듀얼 Quest 시각 회귀 — 큐브가 아니라 SkinnedMesh 손으로 보이고 위치/회전/생사 흐름 모두 유지.

## Out of Scope

- 닉네임 라벨 (`HandNameTag`) — sub-spec 01 의 What 박제이지만 후속 plan 으로 분리. RemoteHandRenderer 의 wrist 위에 worldspace text 부착하는 wiring 만 추가하면 됨.
- 손가락 본 압축 송신 (smallest-three 등) — ARD 01 의 "압축 pose snapshot" 옵션. 대역폭 임계 초과 시점에 후속 plan.
- 다른 사람 손이 자기 악기 collider 를 건드려 NoteOn 트리거 — Tech Spec Invariant: RemoteHandRenderer 는 Collider 0건. 자동 보장.
- PlayHand prefab 본체 수정 — Tech Spec Boundary. RemoteHandRenderer 는 별 자산.
- Ghost/Physics 손 계층 동기화 — sub-spec 01 의 Out of Scope (시각 전용).

## Notes

- **본 매칭 자동화** — Hands CLAUDE.md §3 의 본 이름 list 가 항상 wrist root 직속 자식 형태로 박제 (예: R_Wrist 직속 자식 = R_IndexMetacarpal, R_MiddleMetacarpal, ..., R_Palm; R_IndexMetacarpal 자식 = R_IndexProximal; etc.). LocalHandPoseSource 의 ReadFingerRotations 가 wrist root 를 받아 `GetComponentsInChildren<Transform>` 으로 재귀 수집 + 이름 기반 indexing. 본 plan 의 RemoteHandBoneNames static array 가 그 인덱스 순서를 박제.
- **RemoteHandRenderer prefab 생성 절차** — Unity Editor 에서 PlayHand prefab 을 duplicate → 입력/물리 컴포넌트 (PlayHandPoseDriver, Rigidbody, Collider, …) 제거 → SkinnedMeshRenderer + 본 계층만 유지 → `Assets/Multiplayer/Prefabs/` 로 이동 후 LeftRemoteHandRenderer / RightRemoteHandRenderer 로 rename. **사용자 Editor 작업이 가장 안전** — MCP `manage_prefabs.modify_contents` 가 본 계층 복사를 정확히 처리할지 미지수.
- **대역폭 우려** — 25 quaternion × 16 byte (Fusion Quaternion 압축) ≈ 400 byte/손 + wrist 28 byte. 양손 ~856 byte. 60Hz × 8명 = ~411 KB/s. Fusion 의 SimulationConfig 기본 throughput 임계 1-2 MB/s 이내라 안전 추정. manual-hard 시 NetworkRunnerStats HUD 로 확인.
- **Fusion NetworkArray<Quaternion> Capacity** — `[Networked, Capacity(25)] NetworkArray<Quaternion> FingerRotations { get; }`. capacity 는 컴파일 시점에 fixed. 25 미만 array 보내면 default Quaternion(0,0,0,1) 로 채워짐.
- **RPC payload 크기** — Vector3 + Quaternion + Quaternion[25] = 12 + 16 + 25*16 = 428 byte. 60Hz 송신 시 client→server up ~25 KB/s/플레이어.
- **시각 priority** — sub-spec 01 의 Behavior 5건 (손가락 굽힘, grip 자세, V 사인, piano 키 누름, drum stick) 의 우선순위. drum/piano/trombone 의 grip 자세가 자연 반영되는 게 가장 중요한 demonstration.
- **선행 plan 의 placeholder Cube 정리** — RemoteHandPlaceholder.mat 은 본 plan 후 미사용. Drop 또는 향후 정리 plan.

## Handoff

_본 plan 완료 시 메인 세션이 갱신._
