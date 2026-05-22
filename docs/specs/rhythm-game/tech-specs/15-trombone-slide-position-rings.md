# Trombone Slide Position Rings — Tech Spec

**Sub-Spec:** [`15-trombone-slide-position-rings.md`](../specs/15-trombone-slide-position-rings.md)
**Status:** `Draft`
**Date:** 2026-05-22

## Components

- **TromboneSlidePositionRingController** (신규) — Trombone.prefab 루트에 부착. TromboneAnchor.IsAttached 상태를 LateUpdate에서 폴링해 링 7개의 SetActive를 제어한다.
- **TromboneAnchor** (기존) — IsAttached 프로퍼티로 플레이어의 트롬본 보유 상태를 제공한다.
- **TromboneSlideController** (기존) — SlideMinX / SlideMaxX / SlidePositionCount 공개 멤버로 링 배치 X좌표 계산에 필요한 값을 제공한다.
- **TromboneNoteDisplayAdapter** (기존) — SlideColors[0..6] 배열이 슬라이드 포지션별 색상의 단일 진실원이다.

## Data / Control Flow

- Start: TromboneSlidePositionRingController → SlideMinX / SlideMaxX / SlidePositionCount → 링 7개 Rig-local X 위치 적용 + SlideColors[i] 색상 적용 + 모두 SetActive(false)로 초기화
- LateUpdate 매 프레임: TromboneAnchor.IsAttached 변화 감지 → true면 링 SetActive(true), false면 SetActive(false)

## Boundaries

- **건드린다**: Trombone.prefab (링 7개 자식 GameObject 추가, TromboneSlidePositionRingController 신규 컴포넌트 부착), TromboneNoteDisplayAdapter.cs (SlideColors 접근성 internal 또는 public으로 변경)
- **건드리지 않는다**: TromboneSlideController 로직, RhythmGame 판정 로직, SessionPanel, 파셜 패널 시스템

## Invariants

- 링 수는 항상 TromboneSlideController.SlidePositionCount(=7)와 일치한다.
- 링 색상은 TromboneNoteDisplayAdapter.SlideColors[i]와 동일한 값이어야 한다.
- 링은 Rig 로컬 좌표에서 X = Mathf.Lerp(SlideMinX, SlideMaxX, i / 6f), Y·Z는 Slide Transform의 rest Y·Z와 동일 위치에 고정된다.
- 링 활성 상태는 TromboneAnchor.IsAttached 외에 다른 조건으로 변경되지 않는다.

## Assumptions

**Prefab Hierarchy** (unity-scene-reader Pattern A, 2026-05-22):

```
Trombone (root)               InstrumentAudioOutput, Trombone(InstrumentBase), TrombonePartialController
  ├── Rig                     ← tromboneRoot; 링 7개를 이 아래 자식으로 배치
  │   ├── Body
  │   ├── Slide               ← TromboneSlideController 부착; Rig local X축으로 이동
  │   ├── MouthPiece
  │   └── NoteDisplay
  ├── TromboneAnchor          ← TromboneAnchor 컴포넌트 부착
  ├── TromboneNoteDisplay     ← TromboneNoteDisplayAdapter 부착
  └── RhythmGameHost
```

- Rig가 Slide의 부모 Transform이며 Slide는 Rig 로컬 X축으로만 이동한다 — unity-scene-reader Pattern A (2026-05-22)
- slideMinX = −0.5895388, slideMaxX = −1.165 (prefab 직렬화 값) — unity-scene-reader Pattern A (2026-05-22)
- TromboneSlideController.SlideMinX / SlideMaxX / SlidePositionCount(=7)는 모두 public이다 — Read TromboneSlideController.cs (2026-05-22)
- TromboneAnchor.IsAttached가 플레이어의 트롬본 보유 상태를 정확히 반영한다 — Read TromboneAnchor.cs (2026-05-22)
- TromboneNoteDisplayAdapter.SlideColors는 `static readonly Color[7]`로 선언되며 현재 접근 제한자는 private(기본값)이다. 커밋 daab4d8에서 확정된 색상 값은 아래 표와 같다 — Read TromboneNoteDisplayAdapter.cs (2026-05-22)

  | 인덱스 | 포지션 | 색상 | RGBA |
  |---|---|---|---|
  | 0 | 1 | 보라 | (0.55, 0.00, 0.85, 1) |
  | 1 | 2 | 파랑 | (0.00, 0.00, 1.00, 1) |
  | 2 | 3 | 하늘색 | (0.00, 0.75, 1.00, 1) |
  | 3 | 4 | 초록 | (0.00, 0.80, 0.20, 1) |
  | 4 | 5 | 노랑 | (1.00, 1.00, 0.00, 1) |
  | 5 | 6 | 주황 | (1.00, 0.45, 0.00, 1) |
  | 6 | 7 | 빨강 | (1.00, 0.00, 0.00, 1) |

- `NoteDisplayPanel`은 `NoteColorOverrides (Dictionary<byte, Color>)` 필드를 가지며, 링 색상 매핑과 동일한 `SlideColors` 값을 사용한다 — 커밋 daab4d8 (2026-05-22)

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `docs/specs/rhythm-game/specs/13-trombone-note-display.md` (TromboneNoteDisplayAdapter) | TromboneSlidePositionRingController | 패널은 세션 중에만 Instantiate/Destroy; 링은 트롬본 보유 시 항상 SetActive 토글 |

## Open Tech Decisions

_해당 없음 — 설계 분기 없음._
