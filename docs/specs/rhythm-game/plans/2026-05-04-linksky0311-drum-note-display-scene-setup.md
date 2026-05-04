# Drum Note Display — 씬 설정 및 프리팹 생성

**Linked Spec:** [`../specs/04-drum-note-display.md`](../specs/04-drum-note-display.md)
**Status:** `Ready`

## Goal

드럼 노트 디스플레이에 필요한 씬/에셋 설정을 완성한다: `NoteDisplayPanel.prefab` 생성,
DrumKit에 `DrumNoteDisplayAdapter` 컴포넌트 추가, 각 참조 할당.
이 plan 적용 후 드럼을 잡고 리듬게임 세션을 시작하면 각 드럼 파츠 위에 노트 레인 패널이 나타난다.

## Context

C# 코드(`DrumNoteDisplayAdapter`, `DrumHitZone`, `InstrumentLaneConfig`, `DrumKit`,
`RhythmGameHost`)는 이미 완성되어 있다. `RhythmGameHost.StartSession()`은 `instrument is DrumKit`일 때
`instrument.GetComponent<DrumNoteDisplayAdapter>()`를 찾아 `adapter.Init(instrument.LaneConfig, ...)`를 호출한다.

현재 빠진 것은 씬·에셋 설정 3가지다:

1. **`NoteDisplayPanel.prefab` 미존재** — `DrumNoteDisplayAdapter`는 파츠마다
   `Instantiate(noteDisplayPanelPrefab)`을 호출하므로 World Space Canvas + NoteDisplayPanel로
   구성된 프리팹이 있어야 한다.
2. **DrumKit에 `DrumNoteDisplayAdapter` 미부착** — 세션 시작 시 `GetComponent`가 null을 반환해
   드럼 디스플레이가 비활성화 상태다.
3. **`DrumKit.laneConfig` 미할당** — `Drum_LaneConfig.asset`이 이미 존재하나 DrumKit Inspector에
   연결되지 않아 `instrument.LaneConfig`가 null이다.

씬의 실제 `DrumHitZone` MIDI 노트 배치와 `Drum_LaneConfig.asset` 내용이 일치한다(아래 박제).

## Verified Structural Assumptions

- DrumKit GameObject 경로: `DrumKit` (씬 루트), `DrumNoteDisplayAdapter` 미부착 — `unity-scene-reader 보고 (2026-05-04)`
- 씬 내 DrumHitZone 8개, midiNote: 36(Kick), 38(Snare), 42(HiHat-Closed), 43(FloorTom), 45(MidTom), 48(HighTom), 49(Crash), 51(Ride) — `unity-scene-reader 보고 (2026-05-04)`
- `Assets/RhythmGame/Data/Drum_LaneConfig.asset` 존재, lanes: 36/38/42/43/45/48/49/51 (씬 HitZone과 일치) — `Read Assets/RhythmGame/Data/Drum_LaneConfig.asset (2026-05-04)`
- `Assets/RhythmGame/Data/Piano_LaneConfig.asset` 존재(피아노 전용, 이번 plan 무관) — `Glob (2026-05-04)`
- `Assets/RhythmGame/Prefabs/` 폴더 미존재 — 신규 생성 필요 — `Glob (2026-05-04)`

## Approach

1. **`Assets/RhythmGame/Prefabs/` 폴더 생성**
   MCP `manage_asset` 또는 `manage_editor`로 빈 폴더를 만든다.

2. **`NoteDisplayPanel.prefab` 생성 (World Space Canvas + NoteDisplayPanel)**
   - World Space Canvas GameObject를 임시 씬에서 생성한다. 이름: `NoteDisplayPanel`.
   - `RectTransform.sizeDelta`: width=80, height=300 (canvas units, referencePixelsPerUnit=100 기준 → 0.8m × 3.0m; 너무 크면 yOffset 조정).
     > 실제 VR 크기는 `DrumNoteDisplayAdapter`가 패널을 배치할 때 transform.position으로 제어하므로
     > 이 단계에서 정확히 맞추지 않아도 된다. 나중에 튜닝 가능.
   - `Canvas` 컴포넌트: Render Mode = World Space, 적절한 픽셀/유닛 설정.
   - `NoteDisplayPanel` 컴포넌트 추가. Inspector 값:
     - `lookAheadSeconds`: 2
     - `panelHeight`: 300 (sizeDelta.y와 일치)
   - MCP `manage_prefabs`로 `Assets/RhythmGame/Prefabs/NoteDisplayPanel.prefab`에 저장.
   - 임시 씬 인스턴스 삭제.

3. **DrumKit에 `DrumNoteDisplayAdapter` 컴포넌트 추가**
   MCP `manage_components`로 씬의 `DrumKit` GameObject에 `DrumNoteDisplayAdapter`를 추가한다.

4. **`DrumNoteDisplayAdapter` 참조 할당**
   - `noteDisplayPanelPrefab` → `Assets/RhythmGame/Prefabs/NoteDisplayPanel.prefab`
   - `yOffset` → 0.15 (기본값 유지)

5. **`DrumKit.laneConfig` 할당**
   MCP `manage_components`로 DrumKit의 `laneConfig` 직렬화 필드에
   `Assets/RhythmGame/Data/Drum_LaneConfig.asset`을 연결한다.

6. **씬 저장 및 컴파일 확인**
   MCP `manage_scene` save + `read_console`로 에러 0건 확인.

## Deliverables

- `Assets/RhythmGame/Prefabs/NoteDisplayPanel.prefab` — 신규 (World Space Canvas + NoteDisplayPanel)
- `Assets/Scenes/SampleScene.unity` — DrumKit에 DrumNoteDisplayAdapter 추가 + 참조 할당, DrumKit.laneConfig 할당

## Acceptance Criteria

- [ ] `[auto-hard]` 변경 후 Unity 컴파일 에러가 없다.
- [ ] `[auto-hard]` 씬에서 DrumKit GameObject에 `DrumNoteDisplayAdapter` 컴포넌트가 부착되어 있고, `noteDisplayPanelPrefab` 및 `laneConfig` 필드가 null이 아니다 (MCP `find_gameobjects` 또는 `manage_components`로 확인).
- [ ] `[manual-hard]` 드럼을 잡고 'O' 키로 세션을 시작하면 드럼 파츠(킥, 스네어 등) 위에 노트 레인 패널이 각각 나타난다.
- [ ] `[manual-hard]` 세션 중 노트가 각 파츠 판정선 방향으로 낙하하며, 해당 파츠를 치면 판정 팝업이 그 패널 위에 표시된다.

## Out of Scope

- 드럼 패널 시각적 외형(색상, 배경) 상세 커스터마이징
- 패널 법선 방향 자동 회전 개선 (현재 `Quaternion.LookRotation(zone.transform.up, Vector3.up)` 기본 적용)
- 노트 하단 클리핑 (03-note-bottom-clipping plan 담당)
- Hi-Hat Open(46), Tom(47/50) HitZone 추가 (씬에 없음 — 향후 별도 작업)

## Notes

씬 HitZone의 실제 MIDI 노트(36/38/42/43/45/48/49/51)가 spec 표(Kick=36, Snare=38, Hi-Hat=42/46,
Crash=49, Tom=45/47/48/50)와 일부 다르다. Hi-Hat Open(46) 대신 Ride(51)가,
FloorTom(43)이 추가로 존재한다. 이 plan은 씬의 실제 구성을 기준으로 사용하며,
`Drum_LaneConfig.asset`이 이미 씬 HitZone과 일치하므로 에셋 수정은 불필요하다.

## Handoff
