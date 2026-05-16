# Instruments 도메인 가이드

VR 공간에서 사용자 입력을 MIDI 이벤트로 변환하고 오디오로 출력하는 모든 악기의 베이스 인프라가 모인 도메인이다. 본 문서는 악기라면 공통으로 갖춰야 하는 **계약·골격·흐름**을 정의한다. 구현 디테일은 §7의 진입 스크립트를 직접 Read한다.

## 1. 폴더 규약

| 서브폴더 | 책임 |
|---|---|
| `Scripts/` | 런타임/에디터 C# 로직 |
| `Prefabs/` | 악기 본체 및 변형 프리팹 |
| `Models/` | 메쉬 자산(`.fbx` 등) |
| `Sound/` | 오디오 클립 — 파일명이 곧 매핑 키 |

> `_Core/`는 도메인 공용 인프라 전용(인터페이스·베이스 클래스·SO 정의). 각 악기 폴더는 위 4개 서브폴더 골격을 따른다.

## 2. Prefab 골격

```
[악기 Root]
├── InstrumentBase 상속 컴포넌트  (필수, 1개)
│   ├── instrumentId          (string, IActiveInstrument 식별자)
│   ├── instanceVolume        (0~1, 영속화 대상)
│   ├── soundClips            (AudioClip[])
│   ├── laneConfig            (InstrumentLaneConfig SO)
│   └── _panelAnchor          (눈높이 child Transform)
├── InstrumentAudioOutput     (필수, 자식 자동 탐색)
│   └── voiceMixerGroup       (AudioMixerGroup)
├── _panelAnchor              (UI 패널 배치점)
└── 악기 고유 입력 자식들       (충돌·압력·기타 — 도메인 자유)
```

## 3. Script 추상 계약

| 항목 | 위치 | 책임 |
|---|---|---|
| `IPlayable` | `_Core/Scripts/IPlayable.cs` | `TriggerMidi(MidiEvent)` 단일 진입점 |
| `MidiEvent` (struct) | `_Core/Scripts/MidiEvent.cs` | Note · Velocity · Type · Channel · InstrumentId |
| `MidiEventType` (enum) | `_Core/Scripts/MidiEventType.cs` | NoteOn / NoteOff / Choke / ControlChange |
| `InstrumentBase` (abstract) | `_Core/Scripts/InstrumentBase.cs` | TriggerMidi 분기·볼륨 적용·오디오 위임. 자식 구현 의무: `TryResolveNoteOn(MidiEvent, out NotePlayback)` |
| `InstrumentAudioOutput` | `_Core/Scripts/InstrumentAudioOutput.cs` | Voice Pool · spatialize · release fade. 자식이 `AudioSourceSettings` 오버라이드로 튜닝 |
| `InstrumentLaneConfig` (SO) | `_Core/Scripts/InstrumentLaneConfig.cs` | MIDI 노트 ↔ 레인 인덱스 양방향 매핑 |
| `IActiveInstrument` / `IActiveInstrumentProvider` | `_Core/Scripts/IActiveInstrumentProvider.cs` | 세션이 "현재 악기"를 추적하기 위한 계약 |
| `InstanceVolumeStore` | `_Core/Scripts/InstanceVolumeStore.cs` | 악기별 볼륨 영속화 플러그인 (`Active.Load/Persist`) |

**구현 의무 요약**: 새 악기는 `InstrumentBase`를 상속해 `TryResolveNoteOn`만 구현하면 NoteOn/Off/Choke·볼륨·이벤트 발행이 자동으로 동작한다. 입력 자식(충돌·압력·기타 모달)이 자체 로직으로 `TriggerMidi`를 호출하면 된다.

## 4. 데이터 흐름

```
[악기 고유 입력 자식]
        │ velocity 계산
        ▼
InstrumentBase.TriggerMidi(MidiEvent)
        │
        ├── NoteOn  → TryResolveNoteOn (자식 구현)
        │              → InstrumentAudioOutput.PlayNote
        │                  → AudioSource (Voice Pool)
        ├── NoteOff → InstrumentAudioOutput.StopNote   (release fade)
        └── Choke   → InstrumentAudioOutput.StopNoteImmediate
        │
        ▼
MidiTriggered 이벤트 발행 → 외부 구독자
```

## 5. RhythmGame 연동 계약

악기는 RhythmGame에 직접 의존하지 않는다. 다음 3개 표면만 노출하면 자동으로 연결된다.

- **`InstrumentBase.MidiTriggered` 이벤트** (`Action<MidiEvent>`) — 모든 입력/판정 라우팅의 단일 통로. 세션이 구독해 판정기로 전달한다.
- **`InstrumentBase.LaneConfig`** (`InstrumentLaneConfig`) — Note Display 패널이 "MIDI 노트 → 레인 인덱스" 역조회에 사용. SO 미할당이면 패널이 즉시 Completed로 종료된다.
- **`IActiveInstrument` 구현** — `PanelAnchor` / `InstrumentId` / `InstrumentRoot` / `InstanceVolume`. `InstrumentBase`가 이미 구현. 세션 패널이 이 4개로 위치·식별·볼륨 UI를 구성한다.

활성 악기 전환은 `IActiveInstrumentProvider` 구현체가 담당하며 악기 측은 추가 작업이 없다.

## 6. 어셈블리·실행 순서

- `Instruments.asmdef` (런타임), `Instruments.Editor.asmdef` (에디터). 새 악기 스크립트는 별도 asmdef를 만들지 않고 본 어셈블리에 포함한다.
- 입력 자식이 `LateUpdate` 단계에서 상태를 해석해야 할 경우 `[DefaultExecutionOrder]`로 순서를 박제한다. 부모 `InstrumentBase`보다 입력 자식이 먼저 갱신되도록 정렬.

## 7. 더 깊이 보고 싶을 때

본문은 시그니처 수준에서 멈춘다. 구현 디테일이 필요하면 아래만 추가로 Read.

- 입력 → MIDI 변환 분기: `_Core/Scripts/InstrumentBase.cs:109` (`TriggerMidi`)
- 오디오 출력 파이프라인: `_Core/Scripts/InstrumentAudioOutput.cs:82` (`PlayNote`)
- 레인 매핑 SO 구조: `_Core/Scripts/InstrumentLaneConfig.cs`
- 활성 악기 전환 패턴: `_Core/Scripts/TeleportInstrumentProvider.cs`
