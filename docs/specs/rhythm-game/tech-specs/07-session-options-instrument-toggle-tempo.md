# Session Options — Instrument Toggle & Tempo (Tech Spec)

**Sub-Spec:** [`../specs/07-session-options-instrument-toggle-tempo.md`](../specs/07-session-options-instrument-toggle-tempo.md)

## Components

**신규**
- `InstrumentToggleButtonUI` — 한 악기의 on/off 상태를 보유하는 버튼 위젯. parent 컨테이너의 동적 자식. 기존 `AccompanimentToggleUI.cs`를 instrument 단위로 일반화·재활용.

**수정**
- `RhythmGameSectionController` — (a) 곡·난이도 변경 시 토글 컨테이너 빌드, (b) 시작 시 토글 상태→`accompaniment` 사전 매핑, (c) 템포 슬라이더의 초기값을 player 악기·난이도 파일 `[Tempo]`로 갱신.
- `RhythmGameHost` — `StartSession`의 기존 accompaniment 사전이 토글 상태를 그대로 받음. 템포 override는 기존 `tempoMap.segments[0].bpm` 교체 로직 유지 + RhythmAccompaniment에도 같은 effectiveBpm 적용 보장.
- `RhythmAccompaniment` — accompaniment 사전 참조해 OFF 채널의 발화 skip. effective tempo가 RhythmClock과 일치하는지 확인.

**의존**
- `06-multi-file-song-catalog`의 `ISongEntry` 확장 시그니처(파일 경로를 (instrument, difficulty)별로 노출).

## Data/Control Flow

```
User → InstrumentToggleButtonUI.OnClick → RhythmGameSectionController(state)
User → BPM bar(기존) → RhythmGameSectionController(state)
RhythmGameSectionController.OnPlayButtonClicked
  → Build accompaniment dict from toggles
  → Build effectiveBpm from baseBpm + offset
  → RhythmGameHost.StartSession(chart, song, judgedChannel, accompaniment, effectiveBpm)
RhythmGameHost.StartSession
  → Apply effectiveBpm to chart.tempoMap (기존)
  → Spawn RhythmClock + RhythmAccompaniment with accompaniment dict
RhythmAccompaniment per scheduled note
  → Lookup accompaniment[channel]: ON → instrument.TriggerMidi(...) / OFF → skip
```

## Boundaries

- 본 sub-spec은 **NoteDisplayPanel에 자동 반주 노트를 시각화하지 않는다** (`accompaniment` 정책).
- 본 sub-spec은 **세션 도중 토글·템포 변경**을 다루지 않는다.
- 본 sub-spec은 **트랙별 음량/믹스**를 다루지 않는다.

## Invariants

- 곡 → 난이도 → 토글·템포 → 시작 순서. 도중 곡·난이도 변경 시 토글·템포 초기값 재구성.
- 플레이어 악기는 토글 대상이 아니다.
- 같은 곡의 (instrument, difficulty) 조합이 각자 다른 `[Tempo]`를 가질 수 있으나 본 sub-spec은 player 선택 파일의 `[Tempo]`만 초기값으로 사용.

## Assumptions

- 06 sub-spec이 먼저 적용되어 `ISongEntry`가 (instrument, difficulty) 별 파일 경로를 노출.
- `RhythmGameHost.StartSession`이 이미 accompaniment 사전 인자를 받음 (현 코드 `RhythmGameSectionController.OnPlayButtonClicked` L344-349 확인).
- 기존 BPM bar widget이 `RhythmGameSectionController.BuildBpmBar`/`MakeBpmOffsetBtn`로 그려져 있고 `_baseBpm + _bpmOffset` 모델 보유.

## Comparable Siblings

- `Assets/SessionPanel/Scripts/VolumeSectionController.cs` + `SessionVolume.cs` — UI 토글·슬라이더 + 런타임 반영 패턴.
- `Assets/SessionPanel/Scripts/DifficultyButtonUI.cs` — 동적 row 자식 인스턴스화 패턴.
- `Assets/SessionPanel/Scripts/AccompanimentToggleUI.cs` — 이미 존재하는 트랙 on/off 위젯. 본 sub-spec이 instrument 단위로 일반화.

## Open Tech Decisions

_현재 미결 항목 없음._ 모든 결정은 sub-spec What·Behavior 본문에 박제됨.

## Prefab Hierarchy

`SessionPanel.prefab`의 시작 메뉴 섹션은 곡 row 컨테이너 + 곡 선택 시 표시되는 디테일 컨테이너로 구성. 디테일 컨테이너에 이미 `difficultyContainer`·`bpmBar` Transform이 동적 자식의 부모로 사용 중. 본 sub-spec은 그 패턴을 따라 instrument 토글용 컨테이너 Transform을 한 개 추가 또는 기존 패턴을 재사용.
