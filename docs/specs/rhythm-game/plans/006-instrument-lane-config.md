# 악기 레인 메타데이터

**Linked Spec:** [`note-display.md`](../specs/note-display.md)
**Status:** `Ready`

## Goal

악기별로 레인 수와 레인-MIDI 노트 대응을 정의하는 ScriptableObject를 만들어, 이후 노트 디스플레이 패널(Plan 007)이 레인 구성을 읽을 수 있도록 한다.

## Context

note-display spec은 "레인 수와 레인-MIDI 노트 대응은 악기별로 사전 정의된 악기 메타데이터에 따른다. 차트는 레인 번호로만 참조한다"고 결정했다. 이를 구현하기 위해 Unity ScriptableObject 자산 하나가 악기마다 만들어지며, 각 자산은 (레인 인덱스 → MIDI 노트) 매핑 목록을 보유한다.

기존 코드 현황:
- `InstrumentBase`(MonoBehaviour): 모든 악기의 베이스 클래스. `MidiTriggered` 이벤트 보유.
- `DrumKit`, `Piano`: `InstrumentBase` 서브클래스. 각자 고유 MIDI 노트 구성을 가짐.
- `VmSongChart.tracks`: `ChartTrack` 목록. `ChartTrack.notes`의 각 `ChartNote`는 `midiNote` 필드를 직접 보유. 차트에 레인 인덱스 필드는 없으므로, 패널은 "차트의 midiNote → 해당 레인" 역방향 조회를 이 config로 수행한다.

제약:
- 이 Plan은 C# 스크립트 1~2개만 추가한다. 씬·프리팹은 건드리지 않는다.
- Plan 007이 이 config를 읽는 인터페이스를 설계하므로, 여기서는 데이터 정의만 한다.

## Approach

1. `Assets/RhythmGame/Scripts/Data/InstrumentLaneConfig.cs` 생성.
   - `[CreateAssetMenu]` ScriptableObject.
   - 필드: `List<LaneEntry> lanes` — 각 `LaneEntry`는 `int laneIndex`와 `byte midiNote`를 가진 직렬화 가능한 struct.
   - 헬퍼 메서드: `bool TryGetLane(byte midiNote, out int laneIndex)` — 역방향 조회.
   - 읽기 전용 프로퍼티: `int LaneCount` — `lanes` 리스트 중 최대 laneIndex + 1.

2. 피아노용 `InstrumentLaneConfig` 자산과 드럼용 자산을 각각 `Assets/RhythmGame/Data/` 아래에 생성하고 레인 구성을 Inspector에서 채운다.
   - 피아노: 연주 가능한 건반 수만큼 레인 (첫 구현은 실제 프로젝트 피아노 MIDI 노트 범위 확인 후 결정).
   - 드럼: 스네어·하이햇·킥 등 DrumPiece별로 레인 1개씩.

3. `InstrumentBase`에 `[SerializeField] InstrumentLaneConfig laneConfig` 필드를 추가한다. 공개 프로퍼티 `InstrumentLaneConfig LaneConfig`로 노출.

## Deliverables

- `Assets/RhythmGame/Scripts/Data/InstrumentLaneConfig.cs` — ScriptableObject, 레인-MIDI 매핑 데이터
- `Assets/Instruments/_Core/Scripts/InstrumentBase.cs` — `laneConfig` 필드 추가
- `Assets/RhythmGame/Data/Piano_LaneConfig.asset` — 피아노용 config 자산 (Inspector 입력)
- `Assets/RhythmGame/Data/Drum_LaneConfig.asset` — 드럼용 config 자산 (Inspector 입력)

## Acceptance Criteria

- [ ] `InstrumentLaneConfig` ScriptableObject를 Unity Editor에서 Create 메뉴로 생성할 수 있다.
- [ ] 피아노·드럼 각각에 대해 `LaneConfig` 자산이 존재하고, Inspector에서 레인-MIDI 매핑이 채워져 있다.
- [ ] `TryGetLane(midiNote, out laneIndex)`가 매핑된 노트에 대해 올바른 laneIndex를 반환하고, 미등록 노트에 대해 false를 반환한다.
- [ ] `InstrumentBase` Inspector에서 `laneConfig` 필드에 자산을 연결할 수 있다.

## Out of Scope

- UI 렌더링, 패널 생성 → Plan 007.
- 드럼 파츠별 패널 배치 → Plan 008.

## Notes

- 피아노 MIDI 노트 범위는 `Assets/Instruments/Piano/Scripts/Piano.cs`를 읽고 결정한다.
- 드럼 DrumPiece의 midiNote 구성은 `DrumHitZone.cs`를 읽고 결정한다.
- 자산 저장 경로(`Assets/RhythmGame/Data/`)가 없으면 생성 후 진행한다.
