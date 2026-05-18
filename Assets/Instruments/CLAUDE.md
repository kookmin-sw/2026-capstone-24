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
├── Anchor 자식 (선택)         (TeleportationAnchor + 악기별 AnchorComponent + InstrumentTeleportLink + InstrumentTeleportColliderBinder)
└── 악기 고유 입력 자식들       (충돌·압력·기타 — 도메인 자유)
```

> Anchor 자식은 텔레포트 attach 모델을 쓰는 악기에 한해 추가한다. **scene root 가 아니라 악기 prefab 의 자식**으로 두어 prefab reusability 와 anchor·본체의 hierarchy 결속성을 유지한다.

## 3. Script 추상 계약

| 항목 | 위치 | 책임 |
|---|---|---|
| `IPlayable` | `_Core/Scripts/IPlayable.cs` | `TriggerMidi(MidiEvent)` 단일 진입점 |
| `MidiEvent` (struct) | `_Core/Scripts/MidiEvent.cs` | Note · Velocity · Type · Channel · InstrumentId |
| `MidiEventType` (enum) | `_Core/Scripts/MidiEventType.cs` | NoteOn / NoteOff / Choke / ControlChange |
| `InstrumentBase` (abstract) | `_Core/Scripts/InstrumentBase.cs` | TriggerMidi 분기·볼륨 적용·오디오 위임. 자식 구현 의무: `TryResolveNoteOn(MidiEvent, out NotePlayback)` |
| `InstrumentAudioOutput` | `_Core/Scripts/InstrumentAudioOutput.cs` | Voice Pool · spatialize · release fade · sustain+release one-shot chain (§"Sustained Instrument의 Release Chain 패턴"). 자식이 `AudioSourceSettings` 오버라이드로 튜닝 |
| `InstrumentLaneConfig` (SO) | `_Core/Scripts/InstrumentLaneConfig.cs` | MIDI 노트 ↔ 레인 인덱스 양방향 매핑 |
| `IActiveInstrument` / `IActiveInstrumentProvider` | `_Core/Scripts/IActiveInstrumentProvider.cs` | 세션이 "현재 악기"를 추적하기 위한 계약 |
| `InstanceVolumeStore` | `_Core/Scripts/InstanceVolumeStore.cs` | 악기별 볼륨 영속화 플러그인 (`Active.Load/Persist`) |
| `InstrumentTeleportLink` | `_Core/Scripts/InstrumentTeleportLink.cs` | Anchor 자식에 부착. `BaseTeleportationInteractable.teleporting` 을 잡아 `linkedInstrument` 를 정적 `AnyAnchorTeleported` 이벤트로 발행 — 활성 악기 전환 hub |
| `InstrumentTeleportColliderBinder` | `_Core/Scripts/InstrumentTeleportColliderBinder.cs` | 본체 collider 들을 같은 GameObject 의 `TeleportationAnchor.colliders` 로 흡수해 ray 가 본체 어디에 hit 해도 anchor 가 받음 |

**구현 의무 요약**: 새 악기는 `InstrumentBase`를 상속해 `TryResolveNoteOn`만 구현하면 NoteOn/Off/Choke·볼륨·이벤트 발행이 자동으로 동작한다. 입력 자식(충돌·압력·기타 모달)이 자체 로직으로 `TriggerMidi`를 호출하면 된다.

### Sustained Instrument의 Release Chain 패턴

Sustain loop + NoteOff release one-shot chain을 쓰려면 `TryResolveNoteOn`에서 `NotePlayback`을 만들 때 `sustain: true` + `releaseClip:` 둘 다 set한다. `InstrumentBase`가 자동으로 `InstrumentAudioOutput.PlayNoteSustainedWithRelease` 경로로 라우팅한다.

- **NoteOn**: sustain voice가 loop 재생 + 내부에 release 클립 저장 (`TrackPitch=true`라 slide pitch 추적)
- **NoteOff(`StopNote`)**: sustain voice 즉시 정지 + idle voice에 release 클립을 NoteOff 시점 pitch/volume으로 one-shot 재생 (`TrackPitch=false`라 release 중에는 pitch 고정)
- **Choke / anchor 이탈**: release 건너뛰고 즉시 silence. 진행 중 sustain을 강제 종료할 땐 `NoteOff` 대신 `Choke`로 호출

음원 준비 규약 (사용자가 새 sustained instrument 추가 시):
- **파일 명명**: `<노트명>_sustain.wav`, `<노트명>_release.wav` (예: `C4_sustain.wav`). 악기 폴더(`Trombone/Sound/` 등)가 분리 컨텍스트 제공하므로 악기명 prefix 생략
- **Sustain 클립**: 시작·끝 sample을 zero-crossing에 맞춘 안정 구간 (loop seam click 방지). 0.5~2초 권장
- **Release 클립**: 자체 attack envelope을 가진 one-shot. 시작 음량이 sustain 평균과 비슷해야 transition 시 음량 점프 없음. 끝은 silence로 자연 감쇠
- **부분 할당 동작**: `sustainClip`만 있고 `releaseClip` 누락이면 기존 `PlayNoteSustained`(loop + 볼륨 fade) 경로로 자동 폴백. 안전하지만 release chain 효과는 안 살음

레퍼런스: `Assets/Instruments/Trombone/Scripts/Trombone.cs` (sustain+release chain), `Assets/Instruments/Trombone/Sound/C4_sustain.wav` · `C4_release.wav` (클립 예시)

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

```
[anchor 진입 (TeleportationAnchor)]
        │ BaseTeleportationInteractable.teleporting
        ▼
InstrumentTeleportLink.AnyAnchorTeleported (static event, payload: linkedInstrument)
        │
        ▼
TeleportInstrumentProvider.Current 갱신 → ActiveInstrumentChanged 발행
        │
        ▼
세션 패널 / 외부 구독자가 Current 로 위치·식별·볼륨 UI 재구성
```

## 5. RhythmGame 연동 계약

악기는 RhythmGame에 직접 의존하지 않는다. 다음 3개 표면만 노출하면 자동으로 연결된다.

- **`InstrumentBase.MidiTriggered` 이벤트** (`Action<MidiEvent>`) — 모든 입력/판정 라우팅의 단일 통로. 세션이 구독해 판정기로 전달한다.
- **`InstrumentBase.LaneConfig`** (`InstrumentLaneConfig`) — Note Display 패널이 "MIDI 노트 → 레인 인덱스" 역조회에 사용. SO 미할당이면 패널이 즉시 Completed로 종료된다.
- **`IActiveInstrument` 구현** — `PanelAnchor` / `InstrumentId` / `InstrumentRoot` / `InstanceVolume`. `InstrumentBase`가 이미 구현. 세션 패널이 이 4개로 위치·식별·볼륨 UI를 구성한다.

활성 악기 전환은 `IActiveInstrumentProvider` 구현체 (`TeleportInstrumentProvider` 등) 가 담당한다. 악기 측이 해야 할 일은 anchor 자식에 `InstrumentTeleportLink` 를 부착하고 `linkedInstrument` 필드에 본인 `InstrumentBase` 를 연결하는 한 줄 wiring뿐이다.

## 6. 어셈블리·실행 순서

- `Instruments.asmdef` (런타임), `Instruments.Editor.asmdef` (에디터). 새 악기 스크립트는 별도 asmdef를 만들지 않고 본 어셈블리에 포함한다.
- 입력 자식이 `LateUpdate` 단계에서 상태를 해석해야 할 경우 `[DefaultExecutionOrder]`로 순서를 박제한다. 부모 `InstrumentBase`보다 입력 자식이 먼저 갱신되도록 정렬.

## 7. 더 깊이 보고 싶을 때

본문은 시그니처 수준에서 멈춘다. 구현 디테일이 필요하면 아래만 추가로 Read.

- 입력 → MIDI 변환 분기: `_Core/Scripts/InstrumentBase.cs:109` (`TriggerMidi`)
- 오디오 출력 파이프라인: `_Core/Scripts/InstrumentAudioOutput.cs:82` (`PlayNote`)
- 레인 매핑 SO 구조: `_Core/Scripts/InstrumentLaneConfig.cs`
- 활성 악기 전환 패턴: `_Core/Scripts/TeleportInstrumentProvider.cs`
