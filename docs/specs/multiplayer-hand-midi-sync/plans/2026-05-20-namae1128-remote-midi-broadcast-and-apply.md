# 원격 MIDI 브로드캐스트 + ApplyRemoteMidi 진입점 — 사운드 회귀

**Linked Spec:** [`02-remote-midi-audio.md`](../specs/02-remote-midi-audio.md)
**Status:** `Ready`

## Goal

같은 룸의 다른 클라이언트가 친 MIDI 이벤트가 자기 헤드폰에서 들리도록 만든다. sub-spec 02 의 Behavior 7개 — 다른 사람 악기 소리 들림 / Trombone sustained release / 드럼 velocity 강약 / 자기 echo 중복 없음 / 자기 InstanceVolume 원격 적용 / late-join 후 발음 들림 / 떠난 후 sustained 정지 — 의 **청각 회귀** 단계까지 본 plan 하나로 닫는다 (Trombone 케이스는 §Notes 의 클래스 부재 사유로 conditional).

## Context

`docs/specs/multiplayer-hand-midi-sync/specs/02-remote-midi-audio.md` 의 Spec What 전 범위를 본 plan 하나로 책임진다. sub-spec 01 의 첫 plan ([2026-05-20-namae1128-remote-hand-wrist-placeholder.md](./2026-05-20-namae1128-remote-hand-wrist-placeholder.md)) 이 같은 working tree 에 적용되어 있고, manual-hard 시각 회귀는 본 plan 의 청각 회귀와 **같은 Unity Linux Dedicated Server build → ECR push → Fargate task 사이클**에서 함께 검증한다. fail 사유는 sub-spec 01 의 시각 회귀 (큐브 4건) 과 sub-spec 02 의 청각 회귀 (Behavior 7건) 가 **로그 prefix** 와 **컴포넌트 이름 + 시나리오 분해**로 분리 가능해야 한다.

본 plan 은 두 ARD 의 결정을 그대로 implementation 한다:

- **ARD 02 — `MidiEvent.InstrumentId` (ushort)**: `MidiEvent` struct 의 예약 ushort 필드를 wire 식별자로 사용. `InstrumentIdRegistry` 가 씬 로드 시 ushort ↔ InstrumentBase 매핑을 보관 ([decisions/02-instrument-identifier-on-wire.md](../decisions/02-instrument-identifier-on-wire.md)).
- **ARD 03 — `InstrumentBase.ApplyRemoteMidi` 진입점 채택**: InstrumentBase 에 public `ApplyRemoteMidi(MidiEvent)` 진입점 추가. 내부적으로 `TriggerMidi` 와 같은 dispatch 로직 (NoteOn / NoteOff / Choke) 을 공유 헬퍼로 재사용하되 `MidiTriggered` 이벤트는 발행하지 않음. RhythmGame 판정기는 자기 입력만 받음 ([decisions/03-remote-midi-entry-point.md](../decisions/03-remote-midi-entry-point.md)).

Tech Spec Boundaries: 자식 악기 (Piano / DrumKit) 의 입력 로직 / fade 정책 / TryResolveNoteOn 시그니처는 변경 X. RhythmGame 판정 흐름 (RhythmSession.cs 의 MidiTriggered 구독) 무수정. InstanceVolumeStore 무수정. RoomAuthority 본체 무수정 — `RoomServerBootstrap.EnsureRunner` 의 callback 등록 패턴에 1개만 추가.

본 plan 은 sub-spec 01 plan #1 이 박제한 사실 (NetworkProjectConfig.AssembliesToWeave 에 `"Murang.Multiplayer"` 가 추가됨, `PlayerHandRigSpawner` 패턴) 을 그대로 사용 — `MidiNetBus` 의 `[Networked]`·`[Rpc]` 어트리뷰트가 자동 weave 된다.

## Verified Structural Assumptions

- **`MidiEvent` struct 의 ushort InstrumentId + byte Channel 필드 박제** — `MidiEvent(int note, float velocity, MidiEventType type, byte channel = 0, ushort instrumentId = 0)` ctor 가 line 7-14 에 존재. 본 plan 은 무수정으로 사용. — `Read Assets/Instruments/_Core/Scripts/MidiEvent.cs (2026-05-20)`
- **`MidiEventType` enum 정의 (byte underlying)** — `NoteOn=0 / NoteOff=1 / Choke=2 / ControlChange=3`. wire 직렬화 시 `(byte)MidiEventType.NoteOn` → `0` 같은 인덱스 함정을 확인. 본 plan 의 RPC 시그니처는 `byte type` 으로 받아 `(MidiEventType)type` 으로 캐스트, ControlChange 는 본 단계 미사용 — `Read Assets/Instruments/_Core/Scripts/MidiEventType.cs (2026-05-20)`
- **`InstrumentBase.TriggerMidi` 의 NoteOn 분기 동작 (라인 109-135)** — `NoteOn` 은 `TryResolveNoteOn` 호출 후 `finalVolume = playback.Volume * instanceVolume` (line 119) 으로 자기 InstanceVolume 을 곱해 `audioOutput.PlayNote` 호출. `NoteOff` 는 `OnNoteOff` (가상) + `audioOutput.StopNote` (release fade). `Choke` 는 `OnChoke` (가상) — 자식이 `StopNoteImmediate` 를 호출하는 DrumKit 패턴. **마지막 줄 (line 134) 의 `MidiTriggered?.Invoke(midiEvent)` 는 모든 케이스 공통 발행** — ApplyRemoteMidi 경로에서는 이 발행 한 줄을 우회해야 한다. — `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-20)`
- **`MidiTriggered` 구독자 분포** — `Grep MidiTriggered Assets/` 결과: `InstrumentBase.cs` (선언+발행), `RhythmSession.cs` (구독, 판정기), `DrumKitHitTests.cs` (EditMode 테스트 구독). 원격 노트가 RhythmSession 으로 흘러가면 판정기가 자기 입력으로 오인 — ApplyRemoteMidi 경로에서 발행 X invariant 필수. — `Grep MidiTriggered Assets/ (2026-05-20)`
- **InstanceVolume 영속화 분리** — `InstanceVolumeStore.Active` 는 정적 plugin 패턴, default = `NullStore` (load=fallback / persist=no-op). 자기 클라이언트가 자기 InstrumentBase 인스턴스의 `instanceVolume` 값을 보유. 원격 노트를 자기 InstrumentBase 의 `ApplyRemoteMidi` 로 라우팅하면 자기 `instanceVolume` 이 자동 곱해진다 — 별도 wiring 불요. — `Read Assets/Instruments/_Core/Scripts/InstanceVolumeStore.cs (2026-05-20)` + `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs §InstanceVolume getter (2026-05-20)`
- **`InstrumentAudioOutput` public API 표면** — `PlayNote(int, AudioClip, float, float)` / `StopNote(int)` / `StopNoteImmediate(int)` / `StopAllVoices()` / `InitializePoolSettings(AudioSourceSettings)`. **`OnDisable()` 가 자동으로 `StopAllVoices()` 호출 (line 80)** — 룸을 떠날 때 sustained 자연 정지의 client-side 안전망 (별도 명시적 NoteOff 불요 케이스). — `Read Assets/Instruments/_Core/Scripts/InstrumentAudioOutput.cs (2026-05-20)`
- **`Murang.Multiplayer.asmdef` references 부족 — `Instruments` 미포함** — 현 references = `["Fusion.Unity", "Unity.TextMeshPro"]`. 본 plan 의 신규 4종 (`MidiNetBus`, `LocalMidiEmitter`, `RemoteMidiApplier`, `InstrumentIdRegistry`) 이 `Murang.Multiplayer.Multiplay` namespace 산하라면 `Instruments` namespace 의 `InstrumentBase`/`MidiEvent`/`MidiEventType` 을 import 해야 한다 — **asmdef 에 `"Instruments"` reference 추가 필수**. — `Read Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef (2026-05-20)` + `Read Assets/Instruments/Instruments.asmdef (2026-05-20)` (autoReferenced=true 지만 asmdef references 미명시면 컴파일 실패)
- **NetworkProjectConfig.AssembliesToWeave 에 `"Murang.Multiplayer"` 박제됨** — line 65-72 의 `AssembliesToWeave` 가 `["Fusion.Unity", "Assembly-CSharp", "Assembly-CSharp-firstpass", "Fusion.Addons.Physics", "Fusion.Addons.FSM", "Murang.Multiplayer"]`. 본 plan 의 신규 `MidiNetBus` (`NetworkBehaviour` + `[Rpc]`) 가 자동 weave. NetworkProjectConfig 무수정. — `Read Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion (2026-05-20)`
- **TestSceneSanyo 의 InstrumentBase 자식 인스턴스 분포** — `DrumKit` (m_EditorClassIdentifier line 499) + `Piano` (line 7119) 만 존재. **`Trombone : InstrumentBase` 클래스는 base (`Assets/Instruments/Trombone/Scripts/Trombone.cs`) 에 존재하지만 TestSceneSanyo 씬에 Trombone 인스턴스가 wired 되어 있지 않다** (`Grep m_EditorClassIdentifier:.*Trombone Assets/Scenes/TestSceneSanyo.unity` → 0 매치). sub-spec 02 의 Trombone sustained note (Behavior #2) 검증은 씬 wiring 후속 작업 시점에만 가능 — 본 plan 의 manual-hard 시나리오는 Piano + DrumKit 만으로 진행. — `Glob Assets/Instruments/Trombone/Scripts/*.cs (2026-05-20)` + `Grep m_EditorClassIdentifier:.*Trombone Assets/Scenes/TestSceneSanyo.unity (2026-05-20)`
- **`RoomServerBootstrap.EnsureRunner` 의 callback 등록 4-블록 패턴 (line 88-138)** — `_authority` / `_automationMonitor` / `_callbackReporter` / `_handRigSpawner` 의 4개 블록. 신규 `MidiNetBusSpawner` 는 5번째 블록으로 동일 패턴 (`GetComponent ?? AddComponent` → `Initialize(config)` → `RemoveCallbacks` → `AddCallbacks`) 추가. RoomAuthority 본체 무수정 (Tech Spec Boundary). — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs (2026-05-20)`
- **`RoomServerConfig.cs` 의 [SerializeField] 7개 패턴** — `roomName / maxPlayers / passwordHash / isVisible / customLobbyName / useDefaultPhotonCloudPorts / playerHandRigPrefab` 7개. 신규 `midiNetBusPrefab` (NetworkPrefabRef) 1개를 8번째 [SerializeField] + getter 로 추가. `RoomServerConfig.asset` 인스턴스에 신규 field 1개 추가는 Unity 직렬화 정합 안전 (sub-spec 01 plan #1 이 `playerHandRigPrefab` 추가로 동일 검증 완료). — `Read Assets/Multiplayer/Scripts/Room/Server/RoomServerConfig.cs (2026-05-20)` + `Read Assets/Multiplayer/Resources/RoomServerConfig.asset (2026-05-20)`
- **`RhythmSession.MidiTriggered` 구독 위치 — RhythmGame 보호 invariant evidence** — `Grep MidiTriggered Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs` 가 매치. RhythmGame 판정기가 InstrumentBase.MidiTriggered 의 직접 구독자. ApplyRemoteMidi 가 MidiTriggered 를 발행하면 RhythmGame 판정기가 원격 노트를 자기 입력으로 오인 → judgement 동기화 깨짐. — `Grep MidiTriggered Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs (2026-05-20)`

## Approach

1. **`MidiEvent.MidiEventType` 의 byte cast wire 패턴 박제** — `MidiEvent.Type` 은 enum (`MidiEventType : byte`). RPC 시그니처는 `byte type` 으로 받아 `(MidiEventType)type` 캐스트. `ControlChange` (값 3) 는 본 plan 미사용 — `MidiNetBus.Rpc_BroadcastMidi` 진입 시 `type ∈ {0,1,2}` 만 dispatch, 그 외 silent skip 로깅.

2. **InstrumentBase 에 ushort InstrumentId + ApplyRemoteMidi 진입점 추가** (`Assets/Instruments/_Core/Scripts/InstrumentBase.cs`)

   - `[SerializeField] ushort instrumentNetId = 0;` 신규 — Inspector 노출. `0` = "원격 동기화 비활성" 의미 (등록 거부 — InstrumentIdRegistry §4 가드).
   - `public ushort InstrumentNetId => instrumentNetId;` getter.
   - `public virtual void ApplyRemoteMidi(MidiEvent midiEvent)` 신규 진입점. 내부적으로 `DispatchAudio(midiEvent)` private 헬퍼 호출만. **`MidiTriggered?.Invoke(...)` 발행 X.**
   - 기존 `TriggerMidi(MidiEvent)` (line 109-135) 의 NoteOn/NoteOff/Choke switch 본체를 `DispatchAudio(MidiEvent)` private 헬퍼로 추출. `TriggerMidi` 는 `DispatchAudio` 호출 + `MidiTriggered?.Invoke` 발행 2줄로 축소.
   - **Behavior 보존**: `DispatchAudio` 안에서 NoteOn 의 `finalVolume = playback.Volume * instanceVolume` 곱셈 그대로. 자기 InstanceVolume 이 원격 노트에도 자동 적용 (Tech Spec Invariant 4 만족).
   - 자식 악기 (Piano / DrumKit) 코드 무수정 — TryResolveNoteOn / OnChoke / OnNoteOff override 만 사용하고 `DispatchAudio` 는 부모 측 helper 라 자식 시그니처 영향 0.

3. **`InstrumentIdRegistry` 신규** (`Assets/Multiplayer/Scripts/Multiplay/InstrumentIdRegistry.cs`)

   - `static class InstrumentIdRegistry` (process-global). `Dictionary<ushort, InstrumentBase>` 1개.
   - `public static void Register(InstrumentBase inst)` — `inst.InstrumentNetId == 0` 면 skip + `Debug.LogWarning("[InstrumentIdRegistry] skip {name}: InstrumentNetId=0 (not synced)")`. 같은 ushort 가 이미 등록되어 있으면 reject + `Debug.LogError("[InstrumentIdRegistry] duplicate {id} reject {newName} (existing={oldName})")`.
   - `public static void Unregister(InstrumentBase inst)`.
   - `public static bool TryResolve(ushort id, out InstrumentBase inst)`.
   - **씬 로드 시 자동 등록**: InstrumentBase `OnEnable` / `OnDisable` 에서 Register / Unregister 호출. (Awake 보다 OnEnable 이 안전 — Domain reload 후 hot-reload 도 cover.)

4. **`MidiNetBus` 신규 NetworkBehaviour** (`Assets/Multiplayer/Scripts/Multiplay/MidiNetBus.cs`)

   - `public sealed class MidiNetBus : NetworkBehaviour, IPlayerLeftListener` (Fusion ISL — `IPlayerLeftListener` 사용 가능 시) — sustained note 정지 트래킹용. 없으면 server 측 `INetworkRunnerCallbacks` 동 GameObject 의 spawner 에서 처리.
   - `[Rpc(RpcSources.All, RpcTargets.All)]` (또는 `RpcSources.InputAuthority, RpcTargets.Proxies` — Fusion 2 의 정확한 시그니처는 implementation 시 점검) — `public void Rpc_BroadcastMidi(ushort instrumentId, int note, float velocity, byte type, byte channel, RpcInfo info = default)`.
   - **server-side**: RPC 자동 forwarding (Fusion 2 RPC routing). server 의 onbus 측에서 sustained note 보유자 트래킹 — `Dictionary<PlayerRef, List<(ushort instId, int note)>>` 으로 NoteOn → 추가, NoteOff/Choke → 제거. 단, server 자체는 `runner.IsServer` 분기 안에서 audio dispatch 호출 0건 (Tech Spec Assumption — server headless).
   - **client-side 수신**: 자기 발신 echo 가드 — `if (info.Source == Runner.LocalPlayer) return;` (Fusion RpcInfo). 가드 통과 후 `InstrumentIdRegistry.TryResolve(instrumentId, out var inst)` → `inst.ApplyRemoteMidi(new MidiEvent(note, velocity, (MidiEventType)type, channel, instrumentId))` 호출.
   - **server-side player-left flush**: `IPlayerLeftListener.PlayerLeft(PlayerRef p)` 또는 spawner 의 `OnPlayerLeft` 가 sustained 보유자 리스트의 각 entry 에 대해 `Rpc_BroadcastMidi(instId, note, 0f, (byte)MidiEventType.NoteOff, 0)` 추가 호출 → 다른 클라이언트들이 자연 release.

5. **`MidiNetBusSpawner` 신규** (`Assets/Multiplayer/Scripts/Multiplay/MidiNetBusSpawner.cs`)

   - `PlayerHandRigSpawner` 패턴을 그대로 답습. `MonoBehaviour, INetworkRunnerCallbacks`.
   - `Initialize(RoomServerConfig)` — config 의 `MidiNetBusPrefab` 보유.
   - **룸당 1개 spawn**: `OnSceneLoadDone` 또는 `OnConnectedToServer` (server-only) 에서 `runner.Spawn(config.MidiNetBusPrefab, Vector3.zero, Quaternion.identity, inputAuthority: PlayerRef.None)` 호출. 이미 spawn 된 인스턴스가 있으면 skip (`runner.GetNetworkObject(...)` 또는 자기 cached ref 체크). server-only 가드 (`runner.IsServer == false` 면 즉시 return).
   - `OnPlayerJoined` — **본 plan 에서는 별도 처리 불요** (MidiNetBus 는 룸당 1개, late-join client 는 spawn 된 NetworkObject 의 [Networked] state 가 없어 RPC 만 받으므로 별도 `Rpc_BroadcastMidi` history replay 도 본 plan 범위 외). Behavior #6 (late-join 후 발음 들림) 은 **late-join 이후 발생한** NoteOn 에 한정하면 RPC routing 만으로 자연 만족 — late-join 전 sustained note 의 mid-stream join 은 sub-spec out of scope ("타이밍 보정·지연 보상" 항목).
   - `OnPlayerLeft` — server-only, MidiNetBus 측 sustained flush 트리거.
   - 다른 13개 callback 메서드는 empty body (PlayerHandRigSpawner 와 동일).

6. **`LocalMidiEmitter` 신규** (`Assets/Multiplayer/Scripts/Multiplay/LocalMidiEmitter.cs`)

   - `MonoBehaviour, [DisallowMultipleComponent]`. TestSceneSanyo 의 별도 GameObject 1개에 부착.
   - `Start` (또는 NetworkRunner ready 콜백) — `InstrumentBase[] all = Object.FindObjectsByType<InstrumentBase>(FindObjectsSortMode.None);` 로 씬의 모든 InstrumentBase 발견 → 각각의 `MidiTriggered += OnLocalMidi` 구독. 구독자 리스트 보유.
   - `OnLocalMidi(MidiEvent e)` — `if (Runner == null || !Runner.IsRunning || Runner.IsServer) return;` 가드 (client-only). MidiNetBus 인스턴스를 cache (`FindObjectsByType<MidiNetBus>` 또는 `MidiNetBus.Instance` static singleton — implementation 시 결정, 단순화 우선) → `bus.Rpc_BroadcastMidi((ushort)e.InstrumentId == 0 ? sender.InstrumentNetId : e.InstrumentId, e.Note, e.Velocity, (byte)e.Type, e.Channel)` 호출.
   - **자기 발신 InstrumentId 채움**: `MidiTriggered` 발화 시 InstrumentBase 가 자기 `instrumentNetId` 를 MidiEvent.InstrumentId 에 자동 채워주는 변경은 본 plan 의 InstrumentBase 변경에 추가 — `TriggerMidi` 가 caller 가 넘긴 MidiEvent 의 InstrumentId 가 0 이면 자기 `instrumentNetId` 로 채워 넣어 `MidiTriggered` 에 전달. (caller = Piano.NoteOn 등은 ctor 에서 InstrumentId 미지정 → 0 → 자동 채움.)
   - **자기 InstanceVolume 영향 0 확인**: LocalMidiEmitter 는 wire 전송만, 자기 audio 출력은 InstrumentBase.TriggerMidi 의 `DispatchAudio` 가 이미 처리.
   - `OnDestroy` — 구독 해제 (안전).

7. **`MidiNetBus.prefab` 신규 자산** (`Assets/Multiplayer/Prefabs/MidiNetBus.prefab`)

   - Root GameObject `MidiNetBus` + `NetworkObject` + `MidiNetBus` (NetworkBehaviour) 컴포넌트. 자식 0, Collider/Rigidbody 0건.
   - server-only spawn 대상 — client 측에서는 RPC routing 시점에 NetworkObject 가 자동 동기화.

8. **`RoomServerConfig.cs` 에 `midiNetBusPrefab` 슬롯 추가** (`Assets/Multiplayer/Scripts/Room/Server/RoomServerConfig.cs`)

   - `[SerializeField] private NetworkPrefabRef midiNetBusPrefab;` + `public NetworkPrefabRef MidiNetBusPrefab => midiNetBusPrefab;` 2줄 추가.
   - 결정 사유 (Notes): `RoomServerConfig` SO 는 build-time fact 의 단일 진실원 — sub-spec 01 plan #1 의 `playerHandRigPrefab` 추가와 동일 패턴 그대로.

9. **`RoomServerBootstrap.EnsureRunner` 에 spawner 등록 1블록 추가** (`Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs`)

   - `private MidiNetBusSpawner _midiNetBusSpawner;` 필드 추가.
   - `EnsureRunner` 마지막에 `_midiNetBusSpawner = GetComponent<MidiNetBusSpawner>() ?? gameObject.AddComponent<MidiNetBusSpawner>(); _midiNetBusSpawner.Initialize(config); _runner.RemoveCallbacks(_midiNetBusSpawner); _runner.AddCallbacks(_midiNetBusSpawner);` 4줄 추가.

10. **`Murang.Multiplayer.asmdef` references 확장** — `["Fusion.Unity", "Unity.TextMeshPro"]` → `["Fusion.Unity", "Unity.TextMeshPro", "Instruments"]`. 신규 4종 (`MidiNetBus`, `MidiNetBusSpawner`, `LocalMidiEmitter`, `InstrumentIdRegistry`) 이 `Instruments` namespace 의 `InstrumentBase`/`MidiEvent`/`MidiEventType` 을 import 하려면 필수.

11. **TestSceneSanyo wiring** (client-side, `Assets/Scenes/TestSceneSanyo.unity`)

    - 새 GameObject `LocalMidiEmitter` 1개 + `LocalMidiEmitter` 컴포넌트 부착. Inspector wired field 0 (씬 안 모든 InstrumentBase 를 FindObjectsByType 으로 자동 발견).
    - **Piano 인스턴스 (line 7119 부근) 의 `instrumentNetId` 인스펙터 값 = `1`** 박제.
    - **DrumKit 인스턴스 (line 499 부근) 의 `instrumentNetId` 인스펙터 값 = `2`** 박제.
    - Trombone 인스턴스 부재 — InstrumentNetId 박제 대상 0 (§Notes 참조).

12. **`RoomServerConfig.asset` 의 `midiNetBusPrefab` 슬롯 wiring** — `Assets/Multiplayer/Resources/RoomServerConfig.asset` 의 신규 `midiNetBusPrefab.RawGuidValue` 에 `MidiNetBus.prefab` GUID 박제 (`playerHandRigPrefab` 슬롯과 동일 패턴).

13. **컴파일 + 직렬화 검증** — Unity Editor 로드 → `read_console types=["error"]` 0건 → MidiNetBus.prefab YAML grep 으로 `NetworkObject` + `MidiNetBus` 컴포넌트 1쌍 + Collider 0건 확인.

14. **EditMode 테스트 회귀** — `Instruments.Tests.PlayMode` 어셈블리의 `DrumKitHitTests` 4건 (`ValidDownwardStrike_FiresMidiTriggered` / `BelowMinImpactSpeed_DoesNotFire` / `UpwardStrike_DoesNotFire` / `RetriggerCooldown_BlocksSecondHit`) 이 InstrumentBase 의 `TriggerMidi` → `DispatchAudio` 추출 리팩토링 후에도 통과. MidiTriggered 발행 1회 / 0회 자명 검증.

15. **manual-hard 듀얼 클라이언트 청각 회귀** — sub-spec 01 plan #1 과 같은 Unity Linux Dedicated Server build → ECR push → Fargate task 사이클. 두 클라이언트 (host = Editor Play + standalone build) 가 같은 룸에 합류 후 sub-spec 02 의 Behavior 7건을 §AC 의 manual-hard 항목으로 분해 검증. 로그 prefix (`[MidiNetBus]`, `[LocalMidiEmitter]`, `[InstrumentIdRegistry]`) 로 fail 사유를 sub-spec 01 의 `[PlayerHandRigSpawner]` 와 구분.

## Deliverables

- `Assets/Instruments/_Core/Scripts/InstrumentBase.cs` — `[SerializeField] ushort instrumentNetId` + `InstrumentNetId` getter + `ApplyRemoteMidi(MidiEvent)` public 메서드 + private `DispatchAudio(MidiEvent)` 헬퍼 추출 + `TriggerMidi` 의 InstrumentId 자동 채움 + `OnEnable/OnDisable` 에서 `InstrumentIdRegistry.Register/Unregister`.
- `Assets/Multiplayer/Scripts/Multiplay/InstrumentIdRegistry.cs` — static class, ushort ↔ InstrumentBase 매핑, 중복 거부 가드, Debug.Log prefix `[InstrumentIdRegistry]`.
- `Assets/Multiplayer/Scripts/Multiplay/MidiNetBus.cs` — `NetworkBehaviour`, `[Rpc] Rpc_BroadcastMidi(ushort, int, float, byte, byte)`, server-side sustained note tracking, client-side echo guard + ApplyRemoteMidi dispatch, Debug.Log prefix `[MidiNetBus]`.
- `Assets/Multiplayer/Scripts/Multiplay/MidiNetBusSpawner.cs` — `MonoBehaviour, INetworkRunnerCallbacks`, server-only spawn / player-left sustained flush, Debug.Log prefix `[MidiNetBus]`.
- `Assets/Multiplayer/Scripts/Multiplay/LocalMidiEmitter.cs` — `MonoBehaviour`, 씬의 모든 InstrumentBase 의 MidiTriggered 구독 → MidiNetBus RPC 송신, client-only 가드, Debug.Log prefix `[LocalMidiEmitter]`.
- `Assets/Multiplayer/Prefabs/MidiNetBus.prefab` — NetworkObject + MidiNetBus 컴포넌트, Collider/Rigidbody 0건.
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerConfig.cs` — `midiNetBusPrefab` [SerializeField] + getter 추가.
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs` — `_midiNetBusSpawner` 필드 + `EnsureRunner` 의 5번째 callback 등록 블록 추가.
- `Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef` — references 에 `"Instruments"` 추가.
- `Assets/Multiplayer/Resources/RoomServerConfig.asset` — `midiNetBusPrefab.RawGuidValue` 에 MidiNetBus.prefab GUID 박제.
- `Assets/Scenes/TestSceneSanyo.unity` — `LocalMidiEmitter` GameObject + 컴포넌트 신설, Piano 인스턴스 `instrumentNetId=1`, DrumKit 인스턴스 `instrumentNetId=2` 박제.

## Acceptance Criteria

- [ ] `[auto-hard]` `InstrumentBase.cs` 에 `public virtual void ApplyRemoteMidi(MidiEvent midiEvent)` 진입점이 추가됐고, **`MidiTriggered?.Invoke(...)` 호출은 `TriggerMidi` 메서드 본문에만 존재한다** (ApplyRemoteMidi 본문에는 없음 — ARD 03 invariant).
  **검증:** `Grep -n "ApplyRemoteMidi|MidiTriggered\?.Invoke" Assets/Instruments/_Core/Scripts/InstrumentBase.cs` 결과로 `ApplyRemoteMidi` 매치 1건 이상 + `MidiTriggered?.Invoke` 매치 정확히 1건. 추가로 `Grep -A 20 "public virtual void ApplyRemoteMidi" Assets/Instruments/_Core/Scripts/InstrumentBase.cs` 의 출력 안에 `MidiTriggered` 토큰 0건 (invariant 직접 inspection).
- [ ] `[auto-hard]` `InstrumentBase.cs` 에 `[SerializeField]` ushort `instrumentNetId` + public getter `InstrumentNetId` 추가 (ARD 02 wire 식별자).
  **검증:** `Grep -n "instrumentNetId|InstrumentNetId" Assets/Instruments/_Core/Scripts/InstrumentBase.cs` 결과 ≥ 3 매치 (선언 + getter + ApplyRemoteMidi/Register 호출 중 최소 1건).
- [ ] `[auto-hard]` `Instruments.Tests.PlayMode` 어셈블리의 `DrumKitHitTests` 4건이 본 plan 의 InstrumentBase 리팩토링 (DispatchAudio 헬퍼 추출) 후에도 모두 통과 — TriggerMidi 의 MidiTriggered 발행 횟수 (1 / 0) 보존을 회귀 가드한다.
  **검증:** `unity-test-runner` 서브에이전트 호출 결과 `DrumHitZone_ValidDownwardStrike_FiresMidiTriggered` / `DrumHitZone_BelowMinImpactSpeed_DoesNotFire` / `DrumHitZone_UpwardStrike_DoesNotFire` / `DrumHitZone_RetriggerCooldown_BlocksSecondHit` 4건 PASS. MCP 미가용 시 `MCP UNAVAILABLE` 보고로 진행.
- [ ] `[auto-hard]` `InstrumentIdRegistry.cs` 가 신규로 존재하고 중복 등록 거부 + InstrumentNetId=0 등록 거부 가드 보유.
  **검증:** `Grep -n "duplicate|InstrumentNetId\s*==\s*0|return" Assets/Multiplayer/Scripts/Multiplay/InstrumentIdRegistry.cs` 결과에 중복 거부 분기 + zero-id skip 분기 각각 ≥ 1건.
- [ ] `[auto-hard]` `MidiNetBus.cs` 가 `NetworkBehaviour` 상속 + `[Rpc]` 어트리뷰트 부착 메서드 1개 (`Rpc_BroadcastMidi`) 보유 + 자기 발신 echo 가드 (`info.Source == Runner.LocalPlayer` 또는 동등 표현).
  **검증:** `Grep -n "NetworkBehaviour|\[Rpc|info\.Source|LocalPlayer" Assets/Multiplayer/Scripts/Multiplay/MidiNetBus.cs` 결과에 NetworkBehaviour 1건 + `[Rpc` 1건 + LocalPlayer 또는 `info.Source` echo 가드 ≥ 1건.
- [ ] `[auto-hard]` `LocalMidiEmitter.cs` 가 모든 InstrumentBase 의 MidiTriggered 구독 + server-only build 에서 RPC 송신 skip 가드 (`runner.IsServer` true 면 return) 보유.
  **검증:** `Grep -n "MidiTriggered|IsServer|FindObjectsByType.*InstrumentBase" Assets/Multiplayer/Scripts/Multiplay/LocalMidiEmitter.cs` 결과에 MidiTriggered 구독 라인 ≥ 1건 + IsServer 가드 ≥ 1건 + InstrumentBase 검색 ≥ 1건.
- [ ] `[auto-hard]` `Murang.Multiplayer.asmdef` 의 references 에 `"Instruments"` 추가됨 (asmdef 의존 박제 룰).
  **검증:** `Grep -n "\"Instruments\"" Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef` 결과 ≥ 1 매치.
- [ ] `[auto-hard]` `RoomServerConfig.cs` 에 `midiNetBusPrefab` 필드 + `MidiNetBusPrefab` getter 추가. `RoomServerBootstrap.EnsureRunner` 에 `_midiNetBusSpawner` AddCallbacks 호출 추가.
  **검증:** `Grep -n "midiNetBusPrefab|MidiNetBusPrefab" Assets/Multiplayer/Scripts/Room/Server/RoomServerConfig.cs` ≥ 2 매치 + `Grep -n "_midiNetBusSpawner|AddCallbacks\(_midiNetBusSpawner\)" Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs` ≥ 2 매치.
- [ ] `[auto-hard]` `MidiNetBus.prefab` 가 존재하고 `NetworkObject` + `MidiNetBus` 컴포넌트 1쌍 직렬화 보유. Collider / Rigidbody 0건.
  **검증:** `Grep "NetworkObject|MidiNetBus" Assets/Multiplayer/Prefabs/MidiNetBus.prefab` ≥ 2 매치 + `Grep -P "Collider|Rigidbody" Assets/Multiplayer/Prefabs/MidiNetBus.prefab` 결과 0 매치.
- [ ] `[auto-hard]` `RoomServerConfig.asset` 의 `midiNetBusPrefab.RawGuidValue` 가 `MidiNetBus.prefab.meta` 의 guid 와 일치.
  **검증:** `Grep "midiNetBusPrefab" Assets/Multiplayer/Resources/RoomServerConfig.asset` 결과의 RawGuidValue 가 `Read Assets/Multiplayer/Prefabs/MidiNetBus.prefab.meta` 의 guid 와 동일 (parent 가 cross-check).
- [ ] `[auto-hard]` TestSceneSanyo 의 Piano + DrumKit 인스턴스에 `instrumentNetId` 가 박제됨 (Piano=1, DrumKit=2 또는 ushort > 0 고유값 각 1건).
  **검증:** `Grep -n "instrumentNetId:" Assets/Scenes/TestSceneSanyo.unity` 결과 ≥ 2 매치 + 각 값이 0 이 아닌 ushort.
- [ ] `[auto-hard]` Unity Editor 로드 + TestSceneSanyo 진입 시 콘솔 에러·예외 0건 (asmdef 의존 누락 / NetworkBehaviour weave 실패 / 컴파일 에러 0).
  **검증:** Unity MCP `read_console types=["error"]` 호출 결과 0 매치 ([`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md) 절차 그대로).
- [ ] `[manual-hard]` **Behavior #1 — 다른 사람 악기 소리 들림**: host (Piano 앞) + client (DrumKit 앞) 가 같은 룸에 합류 후, host 가 Piano 키를 치면 client 헤드폰에서 Piano 소리가 들린다. client 가 DrumKit 을 치면 host 헤드폰에서 Drum 소리가 들린다.
  **검증:** 듀얼 클라이언트 청각 회귀 — host 시점에서 자기가 친 Piano 소리만 들리고 client 가 친 Drum 소리도 들림 (각각 자기 spatial 위치). CloudWatch 의 `[MidiNetBus]` Rpc_BroadcastMidi 호출 로그 + 양쪽 client 의 `[LocalMidiEmitter]`/`[MidiNetBus]` 수신 로그 정합.
- [ ] `[manual-hard]` **Behavior #3 — 드럼 velocity 강약 구분**: client 가 DrumKit 을 강하게 (velocity ≥ 0.8) + 약하게 (velocity ≤ 0.3) 번갈아 치면 host 헤드폰에서 들리는 두 노트의 음량 차이가 명확히 구분된다.
  **검증:** 청각 회귀 — host 시점의 DrumKit 노트 두 종류의 음량 차이가 들리고, CloudWatch 의 `[MidiNetBus] Rpc_BroadcastMidi velocity=...` 로그가 0.8 / 0.3 같은 서로 다른 float 값.
- [ ] `[manual-hard]` **Behavior #4 — 자기 발신 echo 중복 없음**: host 가 자기 Piano 를 칠 때 host 헤드폰에서 그 소리가 정확히 한 번만 들린다. RPC 가 자기 자신에게 라우팅되어 두 번 재생되는 일 없음.
  **검증:** 청각 회귀 — host 시점의 자기 Piano 노트 1회 onset 이 1번만 청취 (double-trigger 없음). CloudWatch 의 host 측 `[MidiNetBus]` Rpc 수신 로그에 자기 발신 echo 가드 활성화 로그 (`echo-guarded` 또는 동등 표현) 1건 이상.
- [ ] `[manual-hard]` **Behavior #5 — 자기 InstanceVolume 원격 적용**: host 가 자기 Piano 의 InstanceVolume 을 0.1 로 낮춘 상태에서 client 가 Piano 를 친다 (client 측 Piano InstanceVolume 은 default 0.5 유지). host 헤드폰에서 들리는 원격 Piano 노트가 host 가 설정한 0.1 볼륨으로 작게 들리고, client 헤드폰에서는 자기 0.5 볼륨으로 들린다.
  **검증:** 청각 회귀 — host 와 client 의 같은 노트가 서로 다른 음량으로 들림 (host=quiet / client=normal). InstanceVolume 이 자기 클라이언트 로컬임이 입증.
- [ ] `[manual-hard]` **Behavior #6 — late-join 후 발음 들림**: host 가 먼저 룸에 들어와 있고 client 가 나중에 합류. 합류 직후 client 가 DrumKit 을 치면 host 헤드폰에서 그 소리가 들리고, host 가 Piano 를 치면 client 헤드폰에서 그 소리가 들린다. (late-join 이전 sustained note 의 mid-stream replay 는 sub-spec out of scope.)
  **검증:** 청각 회귀 — client 의 ECR task 진입 직후 (Photon Fusion ready 직후) 양방향 첫 노트가 자연 들림. CloudWatch 의 `[MidiNetBus]` RPC 수신 로그가 양방향 1건 이상.
- [ ] `[manual-hard]` **Behavior #7 — 떠난 후 sustained 정지**: host (Piano) 가 noteOn 발화 후 즉시 룸을 떠난다 (NoteOff 미발화 상태). 잠시 후 client 헤드폰의 Piano 발음이 자연 release (InstrumentAudioOutput 의 OnDisable 안전망 또는 server-side flush) 로 정지한다. **Trombone sustained 케이스는 Trombone InstrumentBase 자식 클래스 부재로 본 plan 시점에 검증 불가 — §Notes 참조, 후속 plan 책임.**
  **검증:** 청각 회귀 — host Leave 후 1~2초 안에 client 측 Piano 발음이 무음으로 전환. server CloudWatch 의 `[MidiNetBus] OnPlayerLeft flush` 로그 또는 `[InstrumentAudioOutput] StopAllVoices` (OnDisable) 로그 1건 이상.

## Out of Scope

- **Trombone sustained release 청각 회귀 (sub-spec 02 Behavior #2)** — Trombone `InstrumentBase` 자식 클래스는 base 에 존재하나 (`Assets/Instruments/Trombone/Scripts/Trombone.cs`) TestSceneSanyo 씬에 Trombone 인스턴스가 wired 되어 있지 않아 본 plan 의 manual-hard 청각 회귀 시점에 검증 불가. 후속 plan 에서 TestSceneSanyo 에 Trombone 인스턴스 추가 + `instrumentNetId=3` 박제 후 Behavior #2 재검증. 본 plan 의 `ApplyRemoteMidi` + `DispatchAudio` 추출은 Trombone 도입 시 자동 호환되도록 설계됐다 — 별도 InstrumentBase 변경 0.
- **late-join 시점의 mid-stream sustained note replay** — sub-spec out-of-scope ("타이밍 보정·예측·지연 보상" 항목) + Tech Spec Assumption ("server 는 NoteOff flush 만, history replay 없음").
- **한 악기 동시 점유** — sub-spec out-of-scope (teleport anchor 단일 점유 모델 보장).
- **음성 채팅 / 텍스트 채팅 / 채널 / 마스터 mix 정책** — sub-spec out-of-scope.
- **InstanceVolume 자체의 클라이언트 간 동기화** — sub-spec out-of-scope ("각자의 InstanceVolume 은 클라이언트 로컬").
- **wire payload 대역폭 측정 AC** — sub-spec 02 의 Behavior 가 자명 동작 위주라 NoteOn/Off ushort + int + float + 2byte 의 단순 RPC 가 임계 측정 의미 없음. 필요하면 후속 plan.
- **자식 악기 (Piano / DrumKit) 의 입력 로직 / TryResolveNoteOn / fade 정책 변경** — Tech Spec Boundary.
- **RhythmGame 판정 흐름의 MidiTriggered 구독자 변경** — Tech Spec Boundary + ARD 03 invariant.
- **InstanceVolumeStore 자체 변경** — Tech Spec Boundary.
- **`RoomAuthority.cs` 본체 수정** — Tech Spec Boundary (RoomServerBootstrap 에 callback 등록 블록 1개만 추가).
- **sub-spec 01 plan #1 의 컴포넌트 (PlayerHandRig / NetworkedWristPose / LocalHandPoseSource / PlayerHandRigSpawner / PlayerHandRig.prefab) 일체** — sub-spec 01 의 책임.
- **VR Editor + standalone build + dedicated server build 의 3-process manual-hard 절차 문서화** — sub-spec 01 plan #1 의 Notes 와 Handoff 가 다루는 운영 단계. 본 plan 은 같은 build 사이클에 piggyback.

## Notes

- **시스템 MIDI 필터링 분리 (2026-05-21, manual-hard 직전 사용자 진단)** — 1차 구현의 `LocalMidiEmitter` 는 모든 InstrumentBase.MidiTriggered 를 구독해 RPC 송신. 그러나 `RhythmAccompaniment.cs:136` + `Dev/ChartAutoPlayer.cs:115` 도 동일하게 `InstrumentBase.TriggerMidi` 호출 → MidiTriggered 발행 → wire 전송. 결과: 자동 반주·디버그 오토플레이의 시스템 노트가 원격에 중복 전송되어 다른 클라이언트는 (자기 자동반주 로컬 재생 + 원격 자동반주 RPC 수신) 이중 사운드. 사용자 진단은 spec 본문의 "사람이 친 노트" 의도와 충돌. 수정: `InstrumentBase` 에 `TriggerSystemMidi(MidiEvent)` public 메서드 신규 — `DispatchAudio` 만 호출, MidiTriggered 발행 X. RhythmAccompaniment + ChartAutoPlayer 의 caller 2건을 `TriggerMidi` → `TriggerSystemMidi` 로 변경. 자식 악기 입력 caller (Piano collider / DrumKit StickHitSweeper 등) 는 TriggerMidi 그대로 유지 — 사람 입력 흐름만 wire 전송. sub-spec 02 의 Behavior 4 (자기 echo 중복 없음) 의 implementation 가드 강화.
- **Trombone instrumentNetId=3 wiring (2026-05-21)** — 사용자 진단으로 TestSceneSanyo line 7849 의 Trombone prefab instance 발견 (이전 `m_EditorClassIdentifier:.*Trombone` grep 이 stripped MonoBehaviour 매치 실패). Trombone 인스턴스에 `instrumentNetId=3` 박제 (line 7860). 본 plan 의 manual-hard Behavior #2 (sustained release) 검증이 Trombone 으로 실제 가능해짐 — 위의 Trombone 인스턴스 부재 박제는 부분 outdated, Behavior #2 manual-hard 가능으로 격상.
- **Trombone 인스턴스 부재 발견 (2026-05-20, base rebase 후)** — Trombone `InstrumentBase` 자식 클래스 자체는 base (origin/main = followup PR #32 merge 후) 에 존재하나 (`Assets/Instruments/Trombone/Scripts/Trombone.cs` + `TromboneAnchor.cs` 등 5개), **TestSceneSanyo 씬에 Trombone 인스턴스가 wired 되어 있지 않다** (`Grep m_EditorClassIdentifier:.*Trombone Assets/Scenes/TestSceneSanyo.unity` → 0 매치). 본 plan 의 manual-hard 청각 회귀는 Piano + DrumKit 만으로 진행하고, Behavior #2 (Trombone sustained release) 검증은 후속 plan 에서 TestSceneSanyo 에 Trombone 인스턴스 추가 + `instrumentNetId=3` 박제 후 회귀. 본 plan 의 `ApplyRemoteMidi` + `DispatchAudio` 추출은 Trombone 도입 시 자동 호환되도록 설계됐다 — Trombone 측 InstrumentBase 자식 코드 변경 0. Behavior #7 (떠난 후 sustained 정지) 는 Piano sustained note 의 `InstrumentAudioOutput.OnDisable → StopAllVoices` 안전망으로 본 plan 시점에 부분 검증 가능 (host Leave 시점에 마지막 NoteOn 발화 직후라면 release fade 가 자연 발생).
- **MidiNetBus 의 spawn 시점** — `OnConnectedToServer` 또는 `OnSceneLoadDone` 둘 다 후보. `OnSceneLoadDone` 이 더 안전 — Fusion scene load 가 끝나야 NetworkObject prefab 등록도 완료된 상태 보장. implementation 시 server-only 가드 + 이미 spawn 된 인스턴스 체크 (중복 spawn 방지) 필수.
- **자기 발신 echo 가드 패턴** — Fusion 2 의 `RpcInfo.Source` 가 RpcSources.InputAuthority / RpcTargets.All 패턴에서 자기 자신 호출자를 식별 가능. implementation 시 `if (info.Source == Runner.LocalPlayer) return;` 또는 동등 표현으로 자기 client 의 RPC 자체 수신 skip. **단, server (IsServer=true) 는 audio dispatch 자체를 안 하므로 echo 가드 미실행 분기로 진입 — server 측 audio 호출 0건 invariant 가 별도로 만족됨.**
- **InstrumentBase.OnEnable/OnDisable Register/Unregister 정책** — Awake 가 아닌 OnEnable 을 쓰는 이유는 Domain reload 후 hot-reload + 씬 비활성 → 활성 전환 시에도 자동 재등록. OnDisable Unregister 는 룸 떠나기 시 자동 정리.
- **InstrumentBase 자체에 `instrumentNetId` 부착 vs InstrumentIdRegistry 측에서 `[SerializeField]` 별도 컴포넌트로 분리** — InstrumentBase 본체에 [SerializeField] 추가가 ARD 02 의 Consequences ("각 악기 InstrumentBase 인스펙터에 ushort InstrumentId 필드를 노출") 와 직접 부합. 별도 컴포넌트 분리는 인스펙터 두 컴포넌트 wiring 부담을 일으켜 ARD 02 의 의도와 충돌.
- **DispatchAudio 추출이 자식 악기 (DrumKit.OnChoke 같은 override) 에 미치는 영향** — 자식의 `OnChoke` / `OnNoteOff` virtual override 는 부모 `DispatchAudio` 의 switch 안에서 호출되므로 추출 위치는 부모 측만. 자식 시그니처 영향 0 — DrumKitHitTests 4건이 통과해야 회귀 가드.
- **로그 prefix 의무 (메인 추론 한 줄)** — `[MidiNetBus]` / `[LocalMidiEmitter]` / `[InstrumentIdRegistry]` 사용. sub-spec 01 plan #1 의 `[PlayerHandRigSpawner]` / `[RoomAuthority]` 와 구분되어 CloudWatch 와 Editor Console 에서 fail 진단 분리 가능.
- **Fusion source weaving** — NetworkProjectConfig.AssembliesToWeave 에 `"Murang.Multiplayer"` 가 이미 박제됨 (sub-spec 01 plan #1 의 Notes 참조). 본 plan 의 신규 `MidiNetBus` (NetworkBehaviour + `[Rpc]`) 가 자동 weave. 별도 NetworkProjectConfig 변경 불요.
- **client-only build (RoomClient + TestSceneSanyo) 의 MidiNetBus 인스턴스 부재 시점** — server-only spawn 이므로 client 가 룸 합류 직후에는 MidiNetBus NetworkObject 가 아직 자기 NetworkRunner 에 동기화 안 됐을 수 있다. `LocalMidiEmitter` 는 매 OnLocalMidi 호출 시점에 `FindObjectsByType<MidiNetBus>` (또는 cache 가 null 이면 재검색) 으로 lazy 검색. 미발견이면 silent skip + Debug.Log `"[LocalMidiEmitter] MidiNetBus not found, skipping send"`.

## Handoff

_본 plan 완료 시 doc-updater 가 갱신._
