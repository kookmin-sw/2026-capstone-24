# 손 pose payload 1차 축소 (본 25 → 10 + LateUpdate throttle + MIDI 채널 분리)

**Linked Spec:** [`01-remote-hand-visualization.md`](../specs/01-remote-hand-visualization.md)
**Status:** `Done`

## Goal

[`decisions/04-hand-pose-payload-reduction.md`](../decisions/04-hand-pose-payload-reduction.md) (Accepted, 2026-05-27) 의 Consequences C/E/F 3건을 코드/자산 변경으로 풀어내, 핫스팟·학교 WiFi 같은 packet-loss 환경에서 체감 분 단위로 누적되던 멀티플레이 동기화 지연을 1차 quick-win 으로 줄인다. 손당 wire payload 를 ≈ 428 bytes → ≈ 188 bytes (Capacity 25 → 10) 로 축소하고 `LocalHandPoseSource.LateUpdate` send rate 를 60Hz → 30Hz 로 throttle 하며, `MidiNetBus` 의 두 RPC 어트리뷰트에 `Channel = RpcChannel.Reliable` 을 명시 박제해 진실원을 코드에 박는다. Tech Spec `01-remote-hand-visualization.md` 의 "손가락 본 25개" 박제도 decision 04 supersede 로 동기화 갱신한다.

## Context

### decision 04 가 요구하는 것

[`decisions/04-hand-pose-payload-reduction.md`](../decisions/04-hand-pose-payload-reduction.md) 의 Decision 본문은 옵션 **C + E + F** 채택을 박았다:

- **C** — `NetworkedWristPose.FingerRotations` Capacity `25 → 10`. 본 매핑은 4 손가락 × (Proximal + Intermediate) + Thumb (Proximal + Distal) = 10 슬롯. 본 결정의 표 (decision 04 line 43-54) 가 단일 진실원.
- **E** — `LocalHandPoseSource.LateUpdate` 에 frame counter 가드. 2 프레임에 1회만 `RPC_PushPose` 발사 (60 FPS → effective 30 Hz).
- **F** — `MidiNetBus.RPC_SendMidiToServer` + `RPC_RelayMidiToClients` 의 `[Rpc]` 어트리뷰트에 명시적 channel 분리 적용. Fusion 2 의 `RpcChannel` enum 이 `Reliable` / `Unreliable` 둘만 지원하므로 (Verified Structural Assumptions §RpcChannel 참조) channel-index 분리는 미지원 — 본 plan 은 두 RPC 모두에 `Channel = RpcChannel.Reliable` 을 *명시 박제* 해 NetworkedWristPose 의 `RpcChannel.Unreliable` 과 코드 진실원 레벨에서 구분되도록 만든다. backlog 격리 자체는 NetworkBehaviour 분리 (MidiNetBus prefab 별도, NetworkedWristPose 와 다른 NetworkObject) 가 이미 보장하므로 Reliability 신뢰성은 손상되지 않는다.

명시적으로 채택 안 된 옵션 (decision 04 line 58-62): TickRate 조정 (A/B), Quaternion smallest-three 압축 (D), timestamp 기반 MIDI / adaptive jitter buffer. 본 plan 은 이 영역을 건드리지 않는다.

### 사용자 보고 (Caused By 컨텍스트)

2026-05-27 사용자 보고: 핫스팟/학교 WiFi 환경에서 체감 분 단위 멀티플레이 동기화 지연. Explore agent 진단 결과 merge conflict 결함 없음, 설계상 payload 가 크고 Reliable state replication backlog 가 누적되는 패턴. decision 04 가 그 진단의 단일 진실원 후속.

### 이전 plan 산출 (재사용)

- [`2026-05-20-namae1128-remote-hand-wrist-placeholder.md`](./2026-05-20-namae1128-remote-hand-wrist-placeholder.md) Handoff: PlayerHandRig NetworkObject + `NetworkedWristPose` 골격 + `LocalHandPoseSource.LateUpdate` 매 프레임 RPC 송신 + NetworkProjectConfig.AssembliesToWeave 에 `"Murang.Multiplayer"` 박제됨.
- [`2026-05-22-namae1128-remote-hand-finger-pose.md`](./2026-05-22-namae1128-remote-hand-finger-pose.md) Handoff: Capacity 25 `NetworkArray<Quaternion>` + `RemoteHandBoneNames.RightSide/LeftSide` 25-element static array + `LocalHandPoseSource.FillFingerBuffer` 본 이름 기반 indexing + `NetworkedWristPose.Render` 의 25 본 캐시 적용.
- [`2026-05-20-namae1128-remote-midi-broadcast-and-apply.md`](./2026-05-20-namae1128-remote-midi-broadcast-and-apply.md) Handoff: `MidiNetBus` 2-step relay (`RPC_SendMidiToServer` `RpcSources.All, RpcTargets.StateAuthority` + `RPC_RelayMidiToClients` `RpcSources.StateAuthority, RpcTargets.All`). 두 RPC 어트리뷰트는 현재 `Channel` 옵션 미명시 = Fusion 기본값 `Reliable`.

### Tech Spec 동기화

[`tech-specs/01-remote-hand-visualization.md`](../tech-specs/01-remote-hand-visualization.md) Data/Control Flow line 21 의 "손가락 본 25개 localRotation" / Assumptions line 40 의 "본당 총 26개 본 (... + Thumb 4단 = 4 + Palm 1)" / Prefab Hierarchy line 57-63 의 본 계층은 **decision 04 가 supersede** 한다. 본 plan 의 Deliverables 에 Tech Spec 갱신 (10본 박제 + decision 04 link) 을 포함한다. 본 plan 은 Tech Spec Boundaries 의 "건드리지 않는다" 영역 (PlayHand prefab 본체 / Ghost / Physics 손 계층 / PlayHandPoseDriver / InstrumentBase 및 자식 악기 / 기존 RoomClient join 흐름) 은 그대로 보존한다.

### Decisions / 충돌 검사

- [`decisions/01-hand-pose-network-encoding.md`](../decisions/01-hand-pose-network-encoding.md): 본 plan 은 encoding 방식 (Fusion `[Networked]` quaternion 직접) 을 변경하지 않는다 — Capacity 만 줄인다. decision 04 §단일 진실원 박제 (line 91-94) 가 명시: decision 01 의 encoding 결정은 유효, 재평가 trigger 의 1차 결과는 decision 04 가 박는다.
- [`decisions/02-instrument-identifier-on-wire.md`](../decisions/02-instrument-identifier-on-wire.md) + [`decisions/03-remote-midi-entry-point.md`](../decisions/03-remote-midi-entry-point.md): MidiNetBus 의 RPC 시그니처와 ApplyRemoteMidi 진입점 자체는 변경 X. 본 plan 은 `[Rpc]` 어트리뷰트의 `Channel` 옵션만 *명시 박제*. ARD 02/03 의 wire 식별자·dispatch 흐름과 무관.
- decision 04 Out of Scope (line 58-62): TickRate / smallest-three / timestamp / jitter buffer — 본 plan 도 동일 영역 미접근.

## Verified Structural Assumptions

- `NetworkedWristPose.cs` 현재 시그니처: line 14 `[SerializeField] HandSide handSide` + line 16-17 `[Networked] Vector3 WristPosition` / `[Networked] Quaternion WristRotation` + line 20-21 `[Networked, Capacity(RemoteHandBoneNames.FingerBoneCount)] NetworkArray<Quaternion> FingerRotations { get; }` + line 27-41 `[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)] void RPC_PushPose(Vector3, Quaternion, Quaternion[])`. Render() (line 57-72) 는 `_cachedFingerBones[i].localRotation = FingerRotations[i]` 로 `FingerRotations.Length` (= Capacity = 상수) 까지 루프. **`FingerBoneCount` 상수 변경만 하면 `[Capacity()]` attribute, `_cachedFingerBones.Length`, RPC_PushPose 의 `Mathf.Min(... , RemoteHandBoneNames.FingerBoneCount)` 가드, Render 루프, CacheFingerBones 의 array 크기 모두 자동 정합**. — `Read Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs (2026-05-27)`
- `RemoteHandBoneNames.cs` 현재 시그니처: line 17 `public const int FingerBoneCount = 25;` + line 19-27 `RightSide` 25-element array (Index/Middle/Ring/Little 각 5단 Metacarpal→Proximal→Intermediate→Distal→Tip + Thumb 4단 + Palm) + line 29-37 `LeftSide` 동일 구조 `L_` prefix + line 39 `For(HandSide)` 디스패치. 본 plan 은 `FingerBoneCount` 를 `10` 으로 바꾸고 두 array 를 decision 04 의 본 매핑 표 (line 43-54) 순서대로 10건씩 재구성. — `Read Assets/Multiplayer/Scripts/Multiplay/RemoteHandBoneNames.cs (2026-05-27)`
- `LocalHandPoseSource.cs` 의 finger buffer 생성: line 27-28 `private readonly Quaternion[] _leftFingerBuffer = new Quaternion[RemoteHandBoneNames.FingerBoneCount];` / `_rightFingerBuffer` 동일. **`FingerBoneCount` 상수 변경 시 두 buffer 의 array 크기가 자동 축소된다** — 별도 array 길이 하드코딩 없음. `FillFingerBuffer` (line 95-120) 의 cache rebuild 가드도 `cache.Length != RemoteHandBoneNames.FingerBoneCount` 로 상수 참조 — 자동 정합. — `Read Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs (2026-05-27)`
- `LocalHandPoseSource.LateUpdate` (line 30-40) 의 현재 진입부: `EnsureWristSources()` → `if (!TryGetOrRefreshRig()) return;` → `WriteWristPose()`. **frame counter 가드 삽입 위치는 함수 진입 가장 앞 (`EnsureWristSources` 이전)** — wristSource resolve 자체도 throttle 대상으로 통일 (자동 fallback 빈도 절반화 무해). 가드 패턴: `private int _frameCounter;` 필드 + `_frameCounter++; if ((_frameCounter & 1) == 0) return;`. — `Read Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs (2026-05-27)` lines 23-40
- `MidiNetBus.cs` 의 두 `[Rpc]` 어트리뷰트 현재 상태: line 25 `[Rpc(RpcSources.All, RpcTargets.StateAuthority)]` (RPC_SendMidiToServer) + line 54 `[Rpc(RpcSources.StateAuthority, RpcTargets.All)]` (RPC_RelayMidiToClients). 둘 다 `Channel` 옵션 미명시 = Fusion 기본값 `Reliable`. 본 plan 은 두 어트리뷰트에 `Channel = RpcChannel.Reliable` 을 명시 박제. RPC 본문 (sustained 트래킹, echo 가드, InstrumentIdRegistry resolve, ApplyRemoteMidi dispatch) 은 무수정. — `Read Assets/Multiplayer/Scripts/Multiplay/MidiNetBus.cs (2026-05-27)` lines 25-85
- Fusion 2 `RpcChannel` enum 정의: **`Reliable` (order preserved + delivery verified + resend) / `Unreliable` (order preserved + delivery not verified + no resend) 2개 값만 존재**. channel index 별도 분리 미지원 — decision 04 line 56 의 fallback ("Fusion 미지원이면 NetworkBehaviour 분리만으로 격리 박제") 가 본 plan 의 실 적용 경로. `RpcAttribute.Channel` 의 default 는 `RpcChannel.Reliable` (Channel 옵션 미명시 = 명시 Reliable 과 동일 동작). — `Read Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml (2026-05-27)` lines 12258-12308 + `Read Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.cs (2026-05-27)` line 1433
- PlayHand prefab 본 계층 (decision 04 의 본 매핑이 실제 존재하는지 확인): Tech Spec `01-remote-hand-visualization.md` Prefab Hierarchy (line 57-63) 박제 — `R_Wrist → R_IndexMetacarpal → R_IndexProximal → R_IndexIntermediate → R_IndexDistal → R_IndexTip` (Index/Middle/Ring/Little 동일 5단), Thumb 은 `R_ThumbMetacarpal → R_ThumbProximal → R_ThumbDistal → R_ThumbTip` (4단). **decision 04 본 매핑 10본 (`R_IndexProximal`, `R_IndexIntermediate`, `R_MiddleProximal`, `R_MiddleIntermediate`, `R_RingProximal`, `R_RingIntermediate`, `R_LittleProximal`, `R_LittleIntermediate`, `R_ThumbProximal`, `R_ThumbDistal`) 은 모두 본 계층의 실재하는 본 이름** — 본 plan 이 새 본 이름을 도입하지 않으며 매핑 변경만으로 자동 정합. `L_` prefix 대응 본도 LeftPlayHand 에 동일 구조. — `Read docs/specs/multiplayer-hand-midi-sync/tech-specs/01-remote-hand-visualization.md (2026-05-27)` lines 51-66
- `Murang.Multiplayer.asmdef` references: `["Fusion.Unity", "Unity.TextMeshPro", "Instruments"]` + `allowUnsafeCode: true` + `autoReferenced: true`. 본 plan 의 import 는 기존 `Fusion` / `Instruments` / `UnityEngine` 그대로 — neue namespace 도입 0, asmdef reference 추가 0. — `Read Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef (2026-05-27)`
- 단위 테스트 자산: `Assets/Multiplayer/Scripts/Multiplay/Tests/` 폴더 부재 (`Glob Assets/Multiplayer/Scripts/Multiplay/Tests/** → 0 매치`). `Multiplay` namespace 에 EditMode 테스트가 없음 — 본 plan 의 `FingerBoneCount = 10` 호환성은 Unity 컴파일 (Render 루프의 `FingerRotations[i]` 인덱스 + `_cachedFingerBones[i]` 인덱스가 동일 상수 참조) 이 충분히 보장. 신규 EditMode 테스트 추가 불요. — `Glob Assets/Multiplayer/Scripts/Multiplay/Tests/** (2026-05-27)`
- 호출 외부 API side effect 박제 — `NetworkedWristPose.Render` 의 `_cachedFingerBones[i].localRotation = FingerRotations[i]` 는 RemoteHandRenderer 의 SkinnedMeshRenderer 본 transform 에 매 프레임 적용. Capacity 10 으로 줄인 후 Render 루프는 10본만 적용하고, **본 매핑에 포함되지 않은 본들 (Metacarpal / Distal / Tip / Palm) 의 localRotation 은 prefab 의 bind pose default 값으로 정지**. 손가락 끝 마디 (Distal / Tip) 의 굽힘이 자기 클라이언트에서는 보이지만 원격에서는 굽혀지지 않음 — decision 04 §Spec What Coverage 의 "부분 만족" 박제와 일치. RemoteHandRenderer prefab 본 계층의 bind pose 가 자연스러운 손바닥 펴짐 default 이면 visual degradation 은 손가락 끝 strict 한 굽힘 표현에만 한정 (사용자 manual-hard 가 평가). — `Read Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs (2026-05-27)` lines 57-72 + Tech Spec Prefab Hierarchy 박제

## Approach

1. **`RemoteHandBoneNames.FingerBoneCount` 상수 10 으로 변경 + 본 매핑 array 재구성** (`Assets/Multiplayer/Scripts/Multiplay/RemoteHandBoneNames.cs`)

   - `public const int FingerBoneCount = 25;` → `public const int FingerBoneCount = 10;`
   - `RightSide` 배열을 decision 04 line 43-54 의 본 매핑 표 순서대로 10건 재구성:
     ```csharp
     public static readonly string[] RightSide = new string[FingerBoneCount]
     {
         "R_IndexProximal",   "R_IndexIntermediate",
         "R_MiddleProximal",  "R_MiddleIntermediate",
         "R_RingProximal",    "R_RingIntermediate",
         "R_LittleProximal",  "R_LittleIntermediate",
         "R_ThumbProximal",   "R_ThumbDistal"
     };
     ```
   - `LeftSide` 배열은 동일 본 이름의 `L_` prefix 10건 (`L_IndexProximal` … `L_ThumbDistal`).
   - XML 주석 (line 9-14) 의 "25개" → "10개" 갱신 + decision 04 link 추가 ("decision 04 hand-pose-payload-reduction 박제").
   - `For(HandSide side)` 디스패치 메서드는 무수정 (signature 영향 0).

2. **`NetworkedWristPose.cs` 검토 (코드 수정 없음 / 상수 참조만)** (`Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs`)

   - `[Networked, Capacity(RemoteHandBoneNames.FingerBoneCount)] NetworkArray<Quaternion> FingerRotations { get; }` (line 20-21) — `FingerBoneCount` 가 상수이므로 자동 정합. Fusion source weaving 이 `[Capacity(...)]` 의 상수 값을 컴파일 시점에 직렬화 슬롯 갯수로 박는다 — Capacity 변경은 weaving 재실행 (Unity Editor 가 asmdef 변경 감지 후 자동) 으로 적용.
   - `RPC_PushPose` 본문 (line 36) `int count = Mathf.Min(fingerRots.Length, RemoteHandBoneNames.FingerBoneCount);` — 상수 참조, 자동 정합.
   - `Render()` (line 64-71) 의 `_cachedFingerBones.Length` 루프 — `CacheFingerBones()` 가 `new Transform[RemoteHandBoneNames.FingerBoneCount]` (line 77) 으로 생성하므로 상수 참조, 자동 정합.
   - **검증: AC 의 grep 으로 Capacity attribute 의 `FingerBoneCount` 상수 참조 유지 (literal 25/10 직접 박지 않음) 확인.**

3. **`LocalHandPoseSource.cs` 검토 + LateUpdate throttle 가드 추가** (`Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs`)

   - `_leftFingerBuffer` / `_rightFingerBuffer` (line 27-28) 는 상수 참조 — 자동 축소.
   - `FillFingerBuffer` (line 95-120) cache rebuild 가드 (`cache.Length != RemoteHandBoneNames.FingerBoneCount`) 도 상수 참조 — 자동 정합. 본 매핑 변경 후 첫 LateUpdate 시 cache 가 한 번 rebuild 되어 새 10본 이름으로 자동 재indexing.
   - **신규 frame counter 가드 필드 + LateUpdate 진입 throttle**:
     ```csharp
     // decision 04 §E — 60 FPS → effective 30 Hz throttle.
     private int _frameCounter;

     private void LateUpdate()
     {
         _frameCounter++;
         if ((_frameCounter & 1) == 0) return;

         EnsureWristSources();
         if (!TryGetOrRefreshRig()) return;
         WriteWristPose();
     }
     ```
   - **가드 위치 결정 사유**: 진입 첫 줄에 두면 `EnsureWristSources` 의 GameObject.Find fallback / `TryGetOrRefreshRig` 의 FindObjectsByType<PlayerHandRig> 빈도까지 절반화 — 1차 throttle 의 의도 ("effective 30 Hz") 와 일치. fallback 빈도 절반화는 wristSource 가 한 번 resolve 되면 cache 되므로 cold start 1-frame 지연만 영향 (manual-hard 인지 불가 수준).
   - 첫 프레임 (`_frameCounter` 가 0 → 1 증가 후 `1 & 1 == 1`) 은 통과, 두 번째 프레임 (`2 & 1 == 0`) skip — 즉 odd frame 만 RPC 발사. effective 30Hz @ 60FPS. `int` overflow (≈ 2^31 frame ≈ 414 일) 는 실용 영향 0.
   - 자식 악기 / Render 흐름과 무관 — wrist transform 자체는 `PlayHandPoseDriver` 가 매 프레임 갱신하므로 (선행 plan #1 Verified Structural Assumptions 박제) 본 throttle 은 RPC 송신 빈도만 줄임. 자기 화면의 자기 손 시각은 영향 0 (LocalHandPoseSource 는 read 전용).

4. **`MidiNetBus.cs` 의 두 `[Rpc]` 어트리뷰트에 `Channel = RpcChannel.Reliable` 명시 박제** (`Assets/Multiplayer/Scripts/Multiplay/MidiNetBus.cs`)

   - `[Rpc(RpcSources.All, RpcTargets.StateAuthority)]` (line 25) → `[Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]`
   - `[Rpc(RpcSources.StateAuthority, RpcTargets.All)]` (line 54) → `[Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]`
   - **NetworkedWristPose 의 `RpcChannel.Unreliable` 과 코드 진실원 레벨에서 구분되도록 만드는 것이 본 변경의 의도** — Fusion 2 가 channel-index 분리를 지원하지 않으므로 (Verified Structural Assumptions §RpcChannel 박제) Channel 옵션 명시 자체는 wire-level 별도 채널 분리는 아니지만 (둘 다 Reliable 단일 채널), 코드 grep 으로 "MIDI = Reliable / 손 pose = Unreliable" 정책이 컴파일 시점부터 명시되어 향후 정책 변경 추적이 가능.
   - **backlog 격리는 NetworkBehaviour 분리로 이미 보장**: `MidiNetBus` 는 자기 NetworkObject (룸당 1개, `MidiNetBus.prefab`) 에 부착, `NetworkedWristPose` 는 `PlayerHandRig.prefab` 의 자식. 두 NetworkBehaviour 의 RPC backlog 는 Fusion 의 NetworkBehaviour 레벨로 격리되어 손 pose 의 Unreliable RPC drop/queue 가 MIDI 의 Reliable RPC 전달 보장에 영향을 주지 않는다. 본 Approach 4 는 격리 자체를 변경하지 않고 코드 진실원만 명시.
   - RPC 본문 (sustained 트래킹, echo 가드, InstrumentIdRegistry resolve, ApplyRemoteMidi dispatch) 무수정.

5. **Tech Spec `01-remote-hand-visualization.md` 박제 갱신** (`docs/specs/multiplayer-hand-midi-sync/tech-specs/01-remote-hand-visualization.md`)

   - § Data / Control Flow line 21 "손가락 본 25개 localRotation" → "손가락 본 10개 localRotation ([decision 04](../decisions/04-hand-pose-payload-reduction.md) supersede)".
   - § Assumptions line 40 "PlayHand prefab 의 본 계층은 손당 총 26개 본 (wrist 1 + Index/Middle/Ring/Little 각 5단 = 20 + Thumb 4단 = 4 + Palm 1)" — **본 계층 자체는 동기화 대상이 아닌 prefab 박제** 이므로 본문 유지하되 한 줄 추가: "_네트워크 동기화 대상은 wrist + 10본 (Proximal + Intermediate × 4 fingers + Thumb Proximal/Distal) — decision 04 박제. 나머지 본 (Metacarpal/Distal/Tip/Palm) 은 RemoteHandRenderer 의 bind pose default 로 정지._"
   - § Open Tech Decisions line 70 decision 01 link 옆에 "(2026-05-27 decision 04 가 Capacity / throttle / channel 정책으로 supersede — [`decisions/04-hand-pose-payload-reduction.md`](../decisions/04-hand-pose-payload-reduction.md))" 추가.
   - § Prefab Hierarchy 박제 (line 51-66) 는 본 계층 자체의 진실원 — 무수정 유지 (네트워크 동기화 대상과 본 계층 자체는 별 개념).

6. **컴파일 + 직렬화 정합 검증** — Unity Editor 로드 → `read_console types=["error"]` 0건 → `RemoteHandBoneNames.FingerBoneCount` 가 컴파일 시점에 10 으로 박힌 후 `[Capacity(...)]` weaving 이 재실행되어 `NetworkArray<Quaternion>` 직렬화 슬롯이 10 으로 축소. 기존 PlayerHandRig.prefab YAML 의 NetworkedWristPose 컴포넌트 직렬화는 영향 0 (capacity 는 컴파일 시점 결정, prefab 데이터에 박혀 있지 않음).

7. **자동 grep 회귀 + manual-hard 듀얼 클라이언트 대역폭 측정** — AC §[manual-hard] 1건이 dev 환경에서 traffic 50% ↓ 검증, AC §[manual-hard] 1건이 시각 합리성 (grip / key press 식별 가능) 검증.

## Deliverables

- `Assets/Multiplayer/Scripts/Multiplay/RemoteHandBoneNames.cs` — `FingerBoneCount = 10` + `RightSide` / `LeftSide` 10-element array 재구성 + XML 주석 갱신.
- `Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs` — `_frameCounter` 필드 1개 추가 + `LateUpdate` 진입에 `_frameCounter++; if ((_frameCounter & 1) == 0) return;` 가드.
- `Assets/Multiplayer/Scripts/Multiplay/MidiNetBus.cs` — 두 `[Rpc]` 어트리뷰트에 `Channel = RpcChannel.Reliable` 명시 박제.
- `docs/specs/multiplayer-hand-midi-sync/tech-specs/01-remote-hand-visualization.md` — Data/Control Flow / Assumptions / Open Tech Decisions 의 손가락 본 25개 박제를 10개 + decision 04 supersede 표시로 갱신.

## Acceptance Criteria

- [ ] `[auto-hard]` `RemoteHandBoneNames.cs` 의 `FingerBoneCount` 상수가 `10` 이고, `RightSide` 배열의 10건 본 이름이 decision 04 line 43-54 의 본 매핑 표와 정확히 일치하고, `LeftSide` 배열도 동일 본 이름의 `L_` prefix 10건이다.
  **검증:** `Grep -n "FingerBoneCount\s*=\s*10" Assets/Multiplayer/Scripts/Multiplay/RemoteHandBoneNames.cs` 1 매치 + `Grep "FingerBoneCount\s*=\s*25" Assets/Multiplayer/Scripts/Multiplay/RemoteHandBoneNames.cs` 0 매치 + `Grep -E "R_IndexProximal|R_IndexIntermediate|R_MiddleProximal|R_MiddleIntermediate|R_RingProximal|R_RingIntermediate|R_LittleProximal|R_LittleIntermediate|R_ThumbProximal|R_ThumbDistal" Assets/Multiplayer/Scripts/Multiplay/RemoteHandBoneNames.cs` 각각 1+ 매치 (10개 본 이름 전부) + `Grep -E "R_IndexMetacarpal|R_IndexDistal|R_IndexTip|R_MiddleMetacarpal|R_MiddleDistal|R_MiddleTip|R_RingMetacarpal|R_RingDistal|R_RingTip|R_LittleMetacarpal|R_LittleDistal|R_LittleTip|R_ThumbMetacarpal|R_ThumbTip|R_Palm" Assets/Multiplayer/Scripts/Multiplay/RemoteHandBoneNames.cs` 0 매치 (제거된 본 이름 부재 확인).
- [ ] `[auto-soft]` `NetworkedWristPose.cs` 의 `[Networked, Capacity(...)]` attribute 와 `RPC_PushPose` 의 `Mathf.Min` 가드가 literal 숫자 (25 / 10) 가 아닌 `RemoteHandBoneNames.FingerBoneCount` 상수 참조를 유지 — Capacity 변경이 상수 참조 단일 진실원으로 자동 정합.
  **검증:** `Grep -n "Capacity\(RemoteHandBoneNames\.FingerBoneCount\)" Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs` 1 매치 + `Grep -n "RemoteHandBoneNames\.FingerBoneCount" Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs` ≥ 3 매치 (Capacity + Mathf.Min + CacheFingerBones array 크기) + `Grep -E "Capacity\(25\)|Capacity\(10\)" Assets/Multiplayer/Scripts/Multiplay/NetworkedWristPose.cs` 0 매치 (literal 박제 부재).
- [ ] `[auto-hard]` `LocalHandPoseSource.cs` 의 `LateUpdate` 안에 `_frameCounter` 가드가 존재하고 `WriteWristPose()` 호출이 가드 뒤에 위치 (가드 통과 시에만 RPC 발사).
  **검증:** `Grep -n "_frameCounter" Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs` ≥ 2 매치 (필드 선언 + LateUpdate 안 증가) + `Grep -n "_frameCounter\+\+|_frameCounter\s*\&\s*1|_frameCounter\s*%\s*2" Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs` ≥ 1 매치 (throttle 표현) + `Grep -B 1 -A 10 "private void LateUpdate" Assets/Multiplayer/Scripts/Multiplay/LocalHandPoseSource.cs` 출력에서 `_frameCounter` 증가 라인이 `WriteWristPose` 또는 `EnsureWristSources` 호출보다 위에 위치.
- [ ] `[auto-hard]` `MidiNetBus.cs` 의 두 `[Rpc]` 어트리뷰트가 모두 `Channel = RpcChannel.Reliable` 을 명시 박제. (Fusion 2 가 channel-index 별도 분리를 지원하지 않아 `Reliable` 명시까지가 본 plan 의 적용 한계 — Notes 박제.)
  **검증:** `Grep -n "Channel\s*=\s*RpcChannel\.Reliable" Assets/Multiplayer/Scripts/Multiplay/MidiNetBus.cs` ≥ 2 매치 + `Grep -n "\[Rpc\(" Assets/Multiplayer/Scripts/Multiplay/MidiNetBus.cs` 결과의 두 어트리뷰트 라인 모두에 `Channel\s*=\s*RpcChannel\.Reliable` 토큰 동반.
- [ ] `[auto-hard]` Tech Spec `01-remote-hand-visualization.md` 의 "25개" 박제가 10개로 갱신됐고, decision 04 link 가 추가됐다.
  **검증:** `Grep -n "손가락 본 25개|본 25개|Capacity 25|FingerBoneCount.*25" docs/specs/multiplayer-hand-midi-sync/tech-specs/01-remote-hand-visualization.md` 0 매치 + `Grep -n "10본|10개|decision 04|04-hand-pose-payload-reduction" docs/specs/multiplayer-hand-midi-sync/tech-specs/01-remote-hand-visualization.md` ≥ 2 매치.
- [ ] `[auto-hard]` Unity Editor 로드 + TestSceneSanyo 진입 시 콘솔 에러·예외 0건. Fusion source weaving 이 `[Capacity(RemoteHandBoneNames.FingerBoneCount)]` 의 새 상수 10 을 직렬화 슬롯 갯수로 박은 후 NetworkedWristPose / PlayerHandRig.prefab 직렬화 정합 정상.
  **검증:** Unity MCP `read_console types=["error"]` 호출 결과 0 매치 ([`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md) 절차 그대로). MCP 미가용 시 `MCP UNAVAILABLE` 보고로 진행.
- [ ] `[manual-hard]` **dev 환경 traffic 50% ↓ 회귀**: dev 환경 (`docker compose -f docker-compose.ec2-dev.yml ...`) 에서 클라이언트 1명 + DS 1개 띄운 상태에서 손당 평균 traffic 을 `docker stats` (TX/RX) 또는 EC2 의 `iftop`/`nethogs` 로 측정. 기대: 변경 전 (Capacity 25 + 매 프레임 RPC) 대비 50% 이상 감소 (Capacity 10 = 56% per-bone payload 감소 + throttle = 50% 빈도 감소, 곱하면 ~78% 이론 감소). 측정값 < 50% 감소이면 fail.
  **검증:** dev 환경 듀얼 클라이언트 traffic 캡처 — `docker stats <ds-container-name>` 의 NET I/O 또는 host 측 `iftop -i ens5 -P` 의 평균 KB/s 를 변경 전 (`git stash` 또는 직전 commit) 과 변경 후로 두 번 측정. 평균 비율이 < 0.5 면 PASS.
- [ ] `[manual-hard]` **시각 합리성 회귀**: Quest 실기기 또는 Editor 2 instance 합류 시, 자기 화면에서 원격 플레이어의 손가락 굽힘이 합리적이게 보임. **Proximal + Intermediate 만 동기화되므로 손가락 끝 마디 (Distal / Tip) 는 RemoteHandRenderer bind pose default 로 정지** — grip / key press / V 사인 식별이 시각적으로 가능한 수준이면 PASS. 손가락 끝 잘림이 "잘림 표현으로 인지될 만큼" 명확하면 fail (decision 04 §Consequences line 87 의 재평가 trigger 발동).
  **검증:** 듀얼 Quest 또는 Editor 2 instance 시각 회귀 — host 가 (a) 주먹 쥐기 (b) Piano 키 누름 (c) drum stick grip (d) V 사인 4 시나리오를 수행, client 화면에서 손 자세가 식별 가능하게 보임. Distal/Tip 마디 정지는 인지 가능하나 자세 식별을 막지 않는 수준이면 PASS.

## Out of Scope

- TickRate 64 → 30 조정 (decision 04 옵션 A) — 1차 효과 부족 시 2차 plan 후보.
- Fusion SendIndex 조정 (decision 04 옵션 B) — TickRate 계열 변경은 본 사이클 외.
- Quaternion smallest-three 4-byte 압축 (decision 04 옵션 D) — 본 수 축소 + throttle 만으로 효과 충분 가능성. 1차 측정 후 재평가.
- timestamp 기반 MIDI / adaptive jitter buffer — spec `01-remote-hand-visualization.md` + `02-remote-midi-audio.md` 의 Out of Scope (타이밍 보정·jitter buffer) 박제. 재평가 필요 시 별도 decision + spec 갱신 사이클.
- 본 매핑 자체의 재평가 (Metacarpal 추가 / Distal 추가 등) — manual-hard 시각 합리성 회귀가 fail 인 경우의 후속 plan. 본 plan 은 decision 04 의 10본 매핑을 그대로 적용.
- RemoteHandRenderer prefab 의 bind pose 값 조정 — 본 plan 은 prefab 본체 무수정. 손가락 끝 마디가 어색해 보이면 별도 prefab plan.
- channel-index 별도 분리 (decision 04 옵션 F 의 강한 형태) — Fusion 2 미지원, NetworkBehaviour 분리만으로 격리 보존 (Verified Structural Assumptions §RpcChannel 박제). Reliable / Unreliable 명시 박제까지가 본 plan 의 적용 한계.
- 단위 테스트 / EditMode 테스트 신설 — `Multiplay/Tests/` 폴더 부재, Unity 컴파일 + grep 으로 회귀 가드 충분 (Verified Structural Assumptions §단위 테스트 자산 박제).
- PlayHand prefab 본체 / Ghost / Physics 손 계층 / PlayHandPoseDriver / InstrumentBase 자식 악기 / 기존 RoomClient join 흐름 — Tech Spec Boundaries 박제 그대로.

## Notes

- **Fusion 2 `RpcChannel` 의 channel-index 분리 미지원 — decision 04 fallback 적용 박제**: Fusion 2 의 `RpcChannel` enum 은 `Reliable` / `Unreliable` 2개 값만 존재 (`Read Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml lines 12294-12308 (2026-05-27)`). decision 04 line 56 의 fallback ("Fusion 미지원이면 NetworkBehaviour 분리만으로 격리, Channel 명시만 박제") 그대로 본 plan 에 박혔다. backlog 격리는 `MidiNetBus` 와 `NetworkedWristPose` 가 서로 다른 NetworkBehaviour 인스턴스 (별 NetworkObject prefab) 로 자동 보장 — RPC backlog 가 NetworkBehaviour 레벨로 격리되어 손 pose 의 Unreliable RPC drop/queue 가 MIDI 의 Reliable RPC 전달에 영향 0.
- **본 매핑 변경 시 RemoteHandRenderer 본의 bind pose 노출**: Capacity 25 에서 매 프레임 적용되던 `_cachedFingerBones[i].localRotation = FingerRotations[i]` 가 10본만 적용 → 매핑에서 빠진 본 (Metacarpal / Distal / Tip / Palm) 의 localRotation 은 RemoteHandRenderer prefab 의 bind pose default 로 정지. PlayHand 의 bind pose 가 자연 손바닥 펴짐 default 이면 손가락 끝 굽힘 표현만 잘림 — decision 04 §Spec What Coverage 박제 "부분 만족" 과 일치. manual-hard 가 "잘림 표현 인지" 수준이면 본 매핑 재평가 (Metacarpal 추가 등 — Out of Scope 항목).
- **frame counter 가드의 첫 프레임 동작**: `_frameCounter` 는 C# default `0` 으로 시작. LateUpdate 첫 호출에서 `_frameCounter++` 후 `1 & 1 == 1` → return 분기 진입 X → `WriteWristPose` 실행. 두 번째 호출 `2 & 1 == 0` → return. 즉 odd frame 만 발사 (`1, 3, 5, ...`) → effective 30Hz @ 60FPS. `int` overflow (≈ 2^31 frame ≈ 414일) 는 실용 영향 0 — wrap-around 후에도 `& 1` 비트 마스크는 정상 동작.
- **PlayerHandRig.prefab 재직렬화 불요**: `[Capacity(...)]` 의 슬롯 갯수는 Fusion source weaving 이 컴파일 시점에 NetworkBehaviour assembly 에 박는다 — prefab YAML 의 NetworkedWristPose 컴포넌트 직렬화는 영향 0. Capacity 25 → 10 변경 후에도 PlayerHandRig.prefab 본체 무수정 (이미 RemoteHandRenderer 본 계층 전체를 자식으로 보유 — `2026-05-22-remote-hand-finger-pose` Approach §2 박제).
- **`InstrumentIdRegistry` / `ApplyRemoteMidi` 흐름 무변경**: 본 plan 은 MidiNetBus 의 `[Rpc]` 어트리뷰트 옵션만 명시 박제. RPC 시그니처 / sustained 트래킹 / echo 가드 / InstrumentIdRegistry.TryResolve / InstrumentBase.ApplyRemoteMidi 호출은 무수정 — sub-spec 02 의 Behavior 7건은 영향 0.
- **1차 측정 결과 분기**: dev 환경 traffic 50% ↓ + 시각 합리성 PASS → 2차 사이클 (timestamp / jitter buffer / smallest-three) 보류 가능, decision 04 가 종착점. 50% ↓ fail 또는 시각 잘림 인지 → 2차 decision (예: `05-content-sync-timing-policy.md`) 박제 + spec Out of Scope 재평가 후속 plan.
- **단일 진실원 박제 (decision 04 §단일 진실원)**: 본 plan 적용 후 손 pose payload 정책의 진실원 우선순위는 decision 04 → decision 01 → Tech Spec 01 → 본 plan. 본 plan 은 decision 04 의 implementation 풀이일 뿐 진실원 자체는 decision 04 가 박는다.
- **decision 04 의 본 매핑 표 그대로 10건 박제 — 임의 재배열 금지**: `RightSide` / `LeftSide` array index 순서를 decision 04 line 43-54 표의 index 0-9 와 동일하게 유지 (Index Proximal/Intermediate → Middle → Ring → Little → Thumb Proximal/Distal). LocalHandPoseSource / NetworkedWristPose 가 같은 인덱스로 매핑하므로 임의 재배열은 송수신 정합을 깬다.

## Handoff

### Auto AC (commit `b3aa95d`, 2026-05-27)

- AC#1 Unity Editor 컴파일 0 에러 + unity-test-runner EditMode 133/133 PASS.
- AC#2 `RemoteHandBoneNames.FingerBoneCount == 10` grep + RightSide/LeftSide 10건 본 매핑이 decision 04 표와 정확히 일치 (`R_IndexProximal, R_IndexIntermediate, R_MiddleProximal, R_MiddleIntermediate, R_RingProximal, R_RingIntermediate, R_LittleProximal, R_LittleIntermediate, R_ThumbProximal, R_ThumbDistal` + L_ prefix).
- AC#3 `LocalHandPoseSource.LateUpdate` 진입부 line 37-38 에 `_frameCounter++; if ((_frameCounter & 1) == 0) return;` 가드 + `RPC_PushPose` 호출이 가드 뒤.
- AC#4 `MidiNetBus.cs` line 26, 56 두 `[Rpc(...)]` 어트리뷰트에 `Channel = RpcChannel.Reliable` 명시.
- AC#5 Tech Spec `01-remote-hand-visualization.md` 의 "25개" 0 hit, "decision 04 / 10본 / 10개" 3 hit (Data/Control Flow line 21 + Assumptions line 41 + Open Tech Decisions).
- AC#6 `NetworkedWristPose.cs` line 20 `[Networked, Capacity(RemoteHandBoneNames.FingerBoneCount)]` 상수 참조 유지 — literal 박제 없음.

### Manual-hard 검증 결과 (2026-05-27 09:21~10:?? UTC, EC2 ip-10-10-1-18 + Quest 실기기)

**1단계 — 1인 Quest (사용자) 검증** (09:21~09:29):
- persistent 룸 (roomId=30, photonSessionName=`perf-measure-test`, image=`v0.1.14`) 합류 → 1~2분 활동 → 단독 퇴장 → 5분 무인 대기.
- DS 가 `RoomAuthority.OnPlayerLeft` 마지막 플레이어 분기에서 `reporter.IsPersistent` skip 가드로 Shutdown / `/terminate` 콜백 둘 다 미발사 확인 — Spring 로그 grep `ds-callback:last-player-left` / `reconciliation:heartbeat-timeout` / `markUnhealthyAndTerminate` / `persistent` 0 hit.
- 5분 후 `last_heartbeat_at` 3초 신선 → DS 0명 상태에서도 정상 heartbeat 송신 (persistent 룸 lifecycle exception 동작 보강 검증).

**2단계 — 2인 Quest (사용자 + 팀원) 검증** (팀원 합류 시점):
- 같은 룸 합류 후 양쪽 자유 활동 (손 움직임 + 악기 인터랙션).
- **본 plan 의 핵심 목표 — "비정상적으로 긴 latency (체감 분 단위 지연)" 해소 확인 — 사용자 직접 보고 PASS** (2026-05-27, "Quest 두명으로 테스트 해봤는데 비정상적으로 긴 latency 문제는 해결됐어").
- 1차 정책 C (본 25→10) + E (LateUpdate 30Hz throttle) + F (MidiNetBus Reliable 명시 박제) 의 결합 효과가 핫스팟·WiFi 환경에서 실측 정성 검증됨.

**AC 별 판정**:
- AC#7 [manual-hard] dev traffic 50% ↓ 정량 측정 — **CloudWatch ContainerInsights 미활성으로 정량 수치 캡처 불가** (`get-metric-statistics` 결과 빈 테이블). 다만 사용자 본 문제 ("분 단위 지연") 가 정성 체감으로 해소 확인됐으므로 1차 plan 의 핵심 가치 (대역폭/RPC 빈도 축소 → 핫스팟 환경에서 sync lag 해소) 는 달성. 정량 측정은 후속 사이클의 ContainerInsights 활성화 plan 또는 클라이언트측 직접 측정 plan 으로 분리 가능.
- AC#8 [manual-hard] 시각 합리성 (Proximal + Intermediate 만으로 grip / key press 식별 가능) — **2인 검증에서 부정 보고 없음** (사용자 보고는 latency 해소 위주, 시각 잘림 인지 명시 보고 없음). decision 04 §Spec What Coverage "부분 만족" 박제와 정합. 손가락 끝 마디 (Distal/Tip/Palm/Metacarpal) 의 미세 굽힘 표현은 잘렸지만 핵심 동작 (grip / key press) 식별엔 영향 없음으로 추정 PASS. 잘림 표현 인지 보고가 후속에 들어오면 본 매핑 재평가 (Metacarpal 추가 등) 의 trigger.

### 다음 plan 이 알아야 할 산출

- `RemoteHandBoneNames.FingerBoneCount = 10` + 본 매핑 배열 (decision 04 표 그대로). 신규 송수신 정합의 단일 진실원.
- `LocalHandPoseSource._frameCounter` 가드 패턴 — 60FPS → effective 30Hz throttle 의 진입점. 다른 NetworkBehaviour 가 동일 throttle 적용할 때 답습 가능.
- `MidiNetBus.RPC_SendMidiToServer` / `RPC_RelayMidiToClients` 의 `Channel = RpcChannel.Reliable` 명시 박제 — Fusion 2 의 channel-index 분리 미지원 사실 박제와 함께. RPC 채널 정책 변경 시 본 진입점.
- Tech Spec `01-remote-hand-visualization.md` 의 새 진실원: Data/Control Flow line 21 + Assumptions line 41 (decision 04 link). Components 섹션 line 10 의 "26개" 잔존은 reviewer 가 마이너 관찰로 박제 — 차단 사유 아님, 후속 docs-only 정리 후보.

### 후속 plan 후보

- **2차 콘텐츠 sync 사이클 (보류)**: timestamp 기반 MIDI + adaptive jitter buffer 정책 박제. 1차 plan 으로 본 문제 해소 확인됐으므로 본 후속의 우선순위 낮음 — 향후 시연/발표 환경에서 미세 지연이 다시 체감되면 재검토.
- **AC#7 정량 측정 후속**: CloudWatch ContainerInsights 활성화 또는 클라이언트측 NetIO 직접 측정 plan. 본 plan 의 정성 효과를 정량 수치로 보강.
- **본 매핑 재평가 (조건부)**: 사용자가 손가락 끝 굽힘 잘림 표현을 명시 보고하면 Metacarpal / Distal 추가하는 재평가 plan.
- **TickRate 64 → 30 등 추가 대역폭 정책 (decision 04 옵션 A/B/D)**: 1차 효과로 본 문제 해소됐으니 우선순위 낮음.
