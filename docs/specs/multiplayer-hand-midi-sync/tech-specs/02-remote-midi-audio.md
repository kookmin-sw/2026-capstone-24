# 원격 MIDI 오디오 — Tech Spec

**Sub-Spec:** [`02-remote-midi-audio.md`](../specs/02-remote-midi-audio.md)
**Status:** `Draft`
**Date:** 2026-05-19

## Components

- **MidiNetBus** (신규, NetworkBehaviour) — 룸당 1개 spawn. MIDI 이벤트를 RPC 로 송수신하는 hub. 모든 클라이언트가 같은 인스턴스를 참조한다.
- **LocalMidiEmitter** (신규) — 룸 합류 시 자기 클라이언트의 모든 InstrumentBase 의 `MidiTriggered` 이벤트를 구독해 MidiNetBus 의 송신 RPC 를 호출한다.
- **RemoteMidiApplier** (신규) — MidiNetBus 의 수신 RPC 가 호출되었을 때 InstrumentId 로 자기 클라이언트의 해당 InstrumentBase 를 찾아 원격 진입점을 호출한다.
- **InstrumentIdRegistry** (신규) — `ushort` InstrumentId 와 InstrumentBase 인스턴스 간 양방향 매핑. 씬 로드 시 모든 InstrumentBase 가 자기 자신을 등록한다.
- **InstrumentBase.ApplyRemoteMidi** (기존 클래스에 신규 진입점) — 원격에서 받은 MidiEvent 를 적용하는 진입점. 내부적으로 TriggerMidi 와 동일한 dispatch 로직 (NoteOn / NoteOff / Choke, sustained · fade envelope 포함) 을 재사용하지만 MidiTriggered 이벤트는 발행하지 않는다.
- **InstrumentBase / InstrumentAudioOutput / 자식 악기** (기존) — 변경 면적 최소화. MidiTriggered 의 RhythmGame 판정 흐름은 자기 입력에만 작동하도록 보장.
- **MidiNetBusSpawner** (신규) — RoomAuthority 의 룸 ready 시점에 MidiNetBus 1개를 Spawn.

## Data / Control Flow

- **자기 연주 송신:** 자기 손가락 → 자기 악기 collider / sensor → `InstrumentBase.TriggerMidi` → MidiTriggered 이벤트 발행 → `LocalMidiEmitter` 구독자 → MidiNetBus RPC (송신, target = others).
- **로컬 재생:** 위 흐름의 `InstrumentBase.TriggerMidi` 가 동일 프레임에 `InstrumentAudioOutput` 를 호출해 자기 헤드폰에 재생 (기존 흐름 그대로).
- **원격 수신 재생:** 다른 플레이어의 MidiNetBus RPC → 자기 클라이언트의 `RemoteMidiApplier` → InstrumentIdRegistry 로 자기 클라이언트 InstrumentBase 찾기 → `InstrumentBase.ApplyRemoteMidi` 호출 → 내부 dispatch 가 NoteOn / NoteOff / Choke / sustained 를 각각 InstrumentAudioOutput 의 적절한 메서드로 라우팅 → 자기 헤드폰에서 재생.
- **퇴장 시 sustained 정지:** `RoomAuthority.OnPlayerLeft` → MidiNetBus 가 그 플레이어가 보유 중인 sustained note 목록 (서버 측 추적) 을 다른 클라이언트들에 NoteOff 로 flush.

## Boundaries

- **건드린다**: `Assets/Instruments/_Core/Scripts/InstrumentBase.cs` (ApplyRemoteMidi 진입점 1개 추가, dispatch 공유 헬퍼 추출), 새 `Assets/Multiplayer/Scripts/Multiplay/` 폴더, default 씬에 MidiNetBus prefab + InstrumentIdRegistry 초기화 wiring, 각 악기 인스턴스 인스펙터에 ushort InstrumentId 설정.
- **건드리지 않는다**: 자식 악기 클래스 (Piano / Drum / Trombone) 의 입력 로직 / 자체 audio fade 정책 / TryResolveNoteOn 시그니처, RhythmGame 판정 흐름 (MidiTriggered 의 원래 구독자), InstanceVolumeStore.

## Invariants

- 자기가 발신한 MIDI RPC 는 자기 클라이언트에서 재생되지 않는다 (자기 InstrumentBase 가 이미 로컬 재생을 책임짐).
- `ApplyRemoteMidi` 경로에서는 MidiTriggered 이벤트가 발행되지 않는다 (RhythmGame 판정기 등 원래 구독자에 원격 노트가 들어가지 않음).
- 같은 ushort InstrumentId 는 한 씬 안에 단 하나의 InstrumentBase 인스턴스에만 매핑된다.
- 자기 InstanceVolume 설정은 원격 NoteOn 재생에도 동일하게 적용된다 (InstrumentAudioOutput 의 기존 볼륨 라우팅 그대로 사용).

## Assumptions

- `MidiEvent` struct 의 `InstrumentId` (ushort), `Channel` (byte) 필드는 멀티플레이 브로드캐스트 / 리플레이 디스패치용 예약 필드로 박제되어 있다. 출처: `Read Assets/Instruments/_Core/Scripts/MidiEvent.cs (2026-05-19)`.
- 모든 InstrumentBase 는 `MidiTriggered` (Action<MidiEvent>) 이벤트를 단일 입력/판정 라우팅 통로로 노출한다. 출처: `Read Assets/Instruments/CLAUDE.md §5 (2026-05-19)`.
- InstrumentAudioOutput 은 NoteOn / NoteOff / Choke / sustained · fade envelope 의 dispatch 를 InstrumentBase 측 dispatch 로직이 호출하는 구조다. 출처: `Read Assets/Instruments/CLAUDE.md §3,4 (2026-05-19)`.
- 모든 룸은 동일한 default 씬을 사용하고, 모든 클라이언트에서 같은 악기가 같은 좌표에 배치되어 있다. 출처: `docs/specs/multiplayer-network/decisions/01-default-scene.md`.
- 자기 클라이언트가 보유한 InstanceVolumeStore 설정은 자기 클라이언트 로컬이며 다른 플레이어에게 전달되지 않는다. 출처: `Read Assets/Instruments/_Core/Scripts/InstanceVolumeStore.cs (2026-05-19)`.
- dedicated server 는 headless 로 동작하고 AudioListener 가 없거나 비활성이라 본 설계에서 서버는 RPC 라우팅만 하고 직접 InstrumentBase 를 호출하지 않는다. 출처: `docs/specs/_archive/multiplayer-network/plans/2026-05-09-namae1128-dedicated-server-build-openxr-toggle.md`.

## Comparable Siblings

_해당 없음 — 신규 카테고리. multiplayer-network 의 04-presence-ui 가 텍스트 메타데이터 (닉네임 / 룸 상태) 송수신을 다루는 데 비해, 본 sub-spec 은 실시간 frame-rate 오디오 이벤트 송수신을 다룬다 — 데이터 형태와 빈도가 다른 카테고리._

## Open Tech Decisions

- [x] MidiEvent 의 wire 식별자로 ushort InstrumentId 와 string instrumentId 중 무엇을 채택할 것인지. → [decisions/02-instrument-identifier-on-wire.md](../decisions/02-instrument-identifier-on-wire.md)
- [x] 원격 MIDI 의 클라이언트 재생 진입점을 InstrumentBase.ApplyRemoteMidi (신규 메서드) 로 둘 것인지 InstrumentAudioOutput 공개 API 직접 호출로 둘 것인지. → [decisions/03-remote-midi-entry-point.md](../decisions/03-remote-midi-entry-point.md)
