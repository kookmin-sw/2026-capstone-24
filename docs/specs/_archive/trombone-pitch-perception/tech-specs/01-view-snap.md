# Tech Spec — Trombone Body Visual Snap

**Sub-Spec:** [`../specs/01-view-snap.md`](../specs/01-view-snap.md)
**Status:** `Draft`
**Date:** 2026-05-27

## Components

- **TrombonePartialController** (기존) — trombone root의 z회전 입력으로 PartialIndex 결정. 본 spec에서 변경 없음.
- **TromboneBodyVisualSnap** (신규) — PartialIndex의 anchor 각도로 Rig/Body Transform의 표시 z회전을 ease 보정.
- **TromboneAnchor** (기존) — IsAttached로 보정 활성/해제 판정.

## Data / Control Flow

- 사용자 입력 (controller/hand) → Trombone root localEulerAngles.z → TrombonePartialController (LateUpdate, ExecOrder 10005) → PartialIndex 결정
- PartialIndex + anglePerPartial + centerPartialIndex → TromboneBodyVisualSnap (LateUpdate, ExecOrder ≥ 10006) → anchor 각도 계산 → Rig/Body.localRotation.z를 ease로 보간
- TromboneAnchor.IsAttached == false → TromboneBodyVisualSnap이 Rig/Body 회전 보정 해제 (원래 localRotation 복원)

## Boundaries

- **건드린다**: `Trombone.prefab`의 Rig/Body Transform.localRotation (z만), 신규 컴포넌트 `TromboneBodyVisualSnap`을 Trombone root 또는 Rig/Body에 부착.
- **건드리지 않는다**: TrombonePartialController의 입력 처리·hysteresis·PartialIndex 산출, Rig/Slide·Rig/MouthPiece·SlidePositionMarkers·NoteDisplay 계층, Trombone root 회전, GripPoseHand·Hands 도메인 grip pose 처리.

## Invariants

- TromboneAnchor.IsAttached가 true인 동안 Rig/Body의 표시 z회전은 항상 5개 anchor 각도 중 PartialIndex에 대응하는 값으로 수렴한다 (ease 진행 중인 짧은 transition 제외).
- Rig/Body의 x·y 회전은 본 컴포넌트가 변경하지 않는다.
- 입력 측 PartialIndex 산출은 본 컴포넌트의 표시 보정에 영향받지 않는다 (단방향: input → display).
- IsAttached=false → Rig/Body localRotation 원복.

## Assumptions

- Trombone.prefab 계층: Trombone root → Rig → {Body, Slide, MouthPiece, NoteDisplay, SlidePositionMarkers} — 출처: Unity MCP manage_prefabs.get_hierarchy (2026-05-27)
- Rig/Body가 트롬본 본체 mesh(MeshFilter + MeshRenderer)를 보유 — 출처: Unity MCP manage_prefabs.get_hierarchy (2026-05-27)
- TrombonePartialController는 Trombone root에 부착되며 SerializeField `tromboneRoot`가 같은 root를 가리킴 — 출처: Read Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs (2026-05-27)
- PartialController의 idx별 anchor 각도 = `(i - centerPartialIndex) × anglePerPartial` (현재 -30/-15/0/+15/+30°) — 출처: Read Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs:68 (2026-05-27)

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
├── TromboneNoteDisplay (RhythmGame.Runtime.TromboneNoteDisplayAdapter)
└── RhythmGameHost (RhythmGameHost, RhythmAccompaniment)
```

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs` | TromboneBodyVisualSnap | Slide는 입력→localPosition 보정 (선형 축), 본 spec은 PartialIndex→localRotation.z hard step 보정 (이산 축). 보정 대상 transform이 다르고(slide vs body), 보정 자유도가 다름(continuous vs step). |

## Open Tech Decisions

- [x] 회전 적용 대상 (root/Body/visual-root) → decisions/01-visual-rotation-target.md
- [x] Hard step 표시각 전환 ease 처리 → decisions/02-hard-step-easing.md
