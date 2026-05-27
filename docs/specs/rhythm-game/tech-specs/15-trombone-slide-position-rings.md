# Trombone Slide Position Rings — Tech Spec

**Sub-Spec:** [`15-trombone-slide-position-rings.md`](../specs/15-trombone-slide-position-rings.md)
**Status:** `Draft`
**Date:** 2026-05-27 (갱신)

## Components

- **SlidePositionMarkers** (기존) — `Trombone/Rig/SlidePositionMarkers`에 부착.
  `TromboneAnchor.IsAttached` 폴링으로 링 7개를 활성화/비활성화하고,
  `TromboneSlideController.IsGripHeld` + `SlideIndex` 폴링으로 활성 링 HDR 밝기 강조를 처리한다.
- **TromboneSlideController** (기존, API 추가) — `m_IsGripHeld`를 외부에 노출하는
  `IsGripHeld` public property를 추가한다.
- **TromboneAnchor** (기존) — `IsAttached`로 트롬본 보유 상태를 제공한다.

## Data / Control Flow

- LateUpdate 매 프레임: `TromboneAnchor.IsAttached` 변화 감지 → 링 SetActive 토글
- LateUpdate 매 프레임: `TromboneSlideController.IsGripHeld` + `SlideIndex` 폴링
  → activeIndex 변경 시 해당 링 `MeshRenderer.material._BaseColor`를 HDR 값으로 갱신,
  이전 활성 링 및 Grip 해제 시 기본 색상으로 복원

## Boundaries

- **건드린다**: `SlidePositionMarkers.cs` (HDR 강조 로직 추가), `TromboneSlideController.cs` (`IsGripHeld` 프로퍼티 추가)
- **건드리지 않는다**: 링 Mesh/Torus 생성 로직, `TromboneAnchor`, RhythmGame 판정 로직, `NoteDisplayPanel`, `TromboneNoteDisplayAdapter`

## Invariants

- 링 수는 항상 `TromboneSlideController.SlidePositionCount`(=7)와 일치한다.
- 기본 링 색상은 `TromboneSlideColors.Palette[i]`와 동일한 값이어야 한다.
- 강조 링은 동시에 최대 1개다 (현재 `SlideIndex`만 강조).
- `IsGripHeld`가 false이면 활성 링 없음 — 모든 링 기본 밝기.

## Assumptions

**Prefab Hierarchy** (Unity MCP `manage_prefabs.get_hierarchy`, 2026-05-27):

```
Trombone (root)
  └── Rig
      ├── Slide                     ← TromboneSlideController 부착
      └── SlidePositionMarkers      ← SlidePositionMarkers 부착, 자식 SlideMarker_0~6
```

- `SlidePositionMarkers`(Rig 자식)와 `Slide`(Rig 자식)는 sibling 관계. `GetComponentInParent`로 상호 탐색 가능.
- `TromboneSlideColors.Palette`: `static readonly Color[7]`, public — Read `TromboneSlideColors.cs` (2026-05-27)
- `TromboneSlideController.SlidePositionCount` = 7, `SlideIndex` public, `SlideIndexChanged` 이벤트 public — Read `TromboneSlideController.cs` (2026-05-27)
- 링 MeshRenderer의 `sharedMaterial`은 `Rebuild` 시마다 `new Material()`로 교체되므로 HDR 색 갱신은 material 인스턴스 직접 접근 필요 — Read `SlidePositionMarkers.cs` (2026-05-27)

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `TromboneBodyVisualSnap.cs` (기존) | `SlidePositionMarkers` HDR 갱신 | BodySnap은 `TrombonePartialController.PartialIndex` 폴링; 링 강조는 `TromboneSlideController.IsGripHeld` + `SlideIndex` 폴링 — 동일 폴링 패턴 |

## Open Tech Decisions

→ decisions/03-slide-ring-grip-detection.md
