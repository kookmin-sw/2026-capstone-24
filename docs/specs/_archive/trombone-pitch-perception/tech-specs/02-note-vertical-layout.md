# Tech Spec — Note Vertical Layout (Pitch-Aligned 5-Panel)

**Sub-Spec:** [`../specs/02-note-vertical-layout.md`](../specs/02-note-vertical-layout.md)
**Status:** `Draft`
**Date:** 2026-05-27

## Components

- **TromboneNoteDisplayAdapter** (기존) — INoteDisplayController 구현, 5패널 인스턴스 생성·라우팅·정리. 본 spec에서 layout 함수가 yaw 반원형 → pitch 수직 적층으로 재정의됨.
- **PanelAnchor** (Trombone prefab 신규 child Transform) — 5패널의 시점 origin. eye 높이·trombone forward 대향으로 고정.
- **NoteDisplayPanel** (기존) — 단일-레인 패널, 노트 낙하 본체. 본 spec에서 변경 없음.
- **RhythmGameHost** (기존) — Begin/Hide 사이클 트리거. 본 spec에서 변경 없음.

## Data / Control Flow

- RhythmGameHost.StartSession → `instrument.GetComponentInChildren<INoteDisplayController>()` → TromboneNoteDisplayAdapter.Begin
- Adapter.Begin → 파셜 인덱스 0~4 순회 → PanelAnchor 기준 pitch_i = (i - centerPartialIndex) × anglePerPartial → 패널 위치 (PanelAnchor.position + AngleAxis(pitch_i, PanelAnchor.right) × PanelAnchor.forward × radius) 계산 → NoteDisplayPanel 인스턴스화 → SetLaneConfig(singleConfig) + Show
- Adapter.OnJudged → LaneConfig.TryGetLane(midiNote)으로 파셜 i 역조회 → i번째 패널에만 popup
- Adapter.Hide → 5패널 일괄 destroy

## Boundaries

- **건드린다**: Trombone.prefab에 신규 child "PanelAnchor" 추가, InstrumentBase._panelAnchor 슬롯 갱신(plan 단계 결정), TromboneNoteDisplayAdapter의 layout 산출 함수, rhythm-game/13-trombone-note-display.md 본문 yaw→pitch 갱신, 13의 plan(2026-05-21-linksky0311-trombone-note-display.md) yaw 가정 supersede.
- **건드리지 않는다**: NoteDisplayPanel.cs 내부 로직, RhythmGameHost.cs, Trombone_LaneConfig.asset의 35건 MIDI 매핑, Drum/Piano NoteDisplayAdapter, TrombonePartialController, judgment 로직.

## Invariants

- 패널 i의 PanelAnchor 기준 pitch 각도는 anchor_i (= (i - centerPartialIndex) × anglePerPartial)와 정확히 일치한다.
- 5패널은 동일 반경, 동일 시점 origin(PanelAnchor)을 공유한다.
- 패널은 항상 PanelAnchor를 바라본다 (LookAt 또는 동등).
- Trombone z회전이 변해도 PanelAnchor 자세는 변하지 않는다 (PanelAnchor는 Body의 z회전 보정 영향을 받지 않는 위치에 부착).

## Assumptions

- Trombone.prefab의 TromboneNoteDisplay child에 TromboneNoteDisplayAdapter가 이미 부착되어 있음 — 출처: Unity MCP manage_prefabs.get_hierarchy (2026-05-27)
- TromboneNoteDisplayAdapter의 layout 함수가 yaw 기반 `ComputePanelWorldPos(int partialIndex)` 형태로 작성됨 (-60°~+60° 호) — 출처: Read docs/specs/rhythm-game/plans/2026-05-21-linksky0311-trombone-note-display.md (2026-05-27)
- rhythm-game/13-trombone-note-display의 본문 및 13-plan의 yaw 반원형 가정은 본 spec의 후속 plan이 갱신할 책임 — 출처: Read docs/specs/rhythm-game/specs/13-trombone-note-display.md (2026-05-27)
- InstrumentBase._panelAnchor 슬롯은 SessionPanel 위치용으로도 사용되며, 본 spec의 PanelAnchor와 같은 transform을 공유할 수 있음 — 출처: Read docs/specs/rhythm-game/plans/2026-05-21-linksky0311-trombone-note-display.md (2026-05-27, "_panelAnchor 미할당, tromboneRoot로 박는다" 항목)

### Prefab Hierarchy

출처: Unity MCP manage_prefabs.get_hierarchy on `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` (2026-05-27, 76 objects)

```
Trombone (Transform, InstrumentAudioOutput, Trombone, TrombonePartialController)
├── Rig (Transform)
│   ├── Body (MeshFilter, MeshRenderer)
│   │   └── GripPoseHand (왼손, L_Wrist 본 계층 + HandMeshPreview)
│   ├── Slide (MeshFilter, MeshRenderer, TromboneSlideController)
│   │   └── GripPoseHand (오른손, R_Wrist 본 계층 + HandMeshPreview)
│   ├── MouthPiece (Transform)
│   ├── NoteDisplay (Canvas, CanvasScaler, GraphicRaycaster, TromboneNoteHud)
│   │   └── Label (Text)
│   └── SlidePositionMarkers (SlidePositionMarkers + SlideMarker_0..6)
├── TromboneAnchor (TeleportationAnchor, TromboneAnchor, BoxCollider, InstrumentTeleportColliderBinder, InstrumentTeleportLink)
├── TromboneNoteDisplay (RhythmGame.Runtime.TromboneNoteDisplayAdapter)  ← 13-plan으로 이미 부착됨
└── RhythmGameHost (RhythmGameHost, RhythmAccompaniment)
```

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs` | TromboneNoteDisplayAdapter (layout 재정의) | Drum은 zone별 독립 위치(드럼 패드 위), Trombone은 단일 origin에서 pitch 적층. 라이프사이클(Begin/Hide/OnJudged/Completed)·런타임 SO 생성 패턴은 동형. |
| `Assets/RhythmGame/Scripts/Runtime/Display/PianoNoteDisplayAdapter.cs` (존재 가정) | TromboneNoteDisplayAdapter | Piano는 단일 multi-lane 평면(IsSingleLane=false), Trombone은 단일-레인 패널 5개 (IsSingleLane=true) 분리. |
| `docs/specs/rhythm-game/specs/13-trombone-note-display.md` (yaw 반원) | 02-note-vertical-layout (pitch 수직) | layout 축 yaw→pitch, origin tromboneRoot→PanelAnchor child. 13은 본 spec이 supersede. |

## Open Tech Decisions

- [x] 5패널 시점 origin/anchor child 도입 방식 → decisions/03-note-panel-anchor.md
