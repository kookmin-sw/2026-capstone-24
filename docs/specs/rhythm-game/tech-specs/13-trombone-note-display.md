# Tech Spec: Trombone Note Display

**Sub-Spec:** [`13-trombone-note-display.md`](../specs/13-trombone-note-display.md)
**Status:** `Draft`
**Date:** 2026-05-20

## Components

- **TromboneNoteDisplayAdapter** (신규) — INoteDisplayController 구현.
  트롬본 파셜 수(5)만큼 NoteDisplayPanel을 인스턴스화하고 수평 반원형으로 배치.
  RhythmGameHost에 INoteDisplayController 인터페이스로 자동 연결.
- **NoteDisplayPanel** (기존) — 한 파셜에 대응하는 노트 레인 패널.
- **InstrumentLaneConfig** (기존) — MIDI 노트 → 파셜(레인) 인덱스 매핑 SO.
  트롬본 전용 SO 1개를 Trombone 인스펙터에 할당.
- **TromboneSlideController** (기존) — 파셜·슬라이드 포지션 조합의
  이론적 3D 오프셋 계산 참조 소스.
- **TrombonePartialController** (기존) — 현재 파셜 인덱스 제공.

## Data / Control Flow

- 세션 시작 → RhythmGameHost가 `instrument.GetComponentInChildren<INoteDisplayController>()`로
  TromboneNoteDisplayAdapter 발견 → `Begin(chart, judgedChannel, clock)` 호출
- `TromboneNoteDisplayAdapter.Begin` → 파셜별 NoteDisplayPanel 5개 인스턴스화 →
  트롬본 축 기준 수평 반원형 위치 계산 → 각 패널 배치·Show
- 차트 노트 스폰 시: InstrumentLaneConfig.TryGetLane(midiNote) → 파셜 인덱스 결정 →
  해당 파셜+슬라이드 포지션의 이론적 슬라이드 월드 좌표를 스폰 위치로 사용
- `RhythmJudge.Judged` → `TromboneNoteDisplayAdapter.OnJudged` →
  midiNote 기준 패널로 라우팅

## Boundaries

- **건드린다**: TromboneNoteDisplayAdapter 신규 스크립트,
  Trombone prefab에 TromboneNoteDisplayAdapter 컴포넌트 추가,
  InstrumentLaneConfig SO 신규 생성 및 Trombone 인스펙터 연결
- **건드리지 않는다**: NoteDisplayPanel 내부 로직, RhythmGameHost 로직,
  TromboneSlideController·TrombonePartialController public API 이외 수정,
  기존 드럼·피아노 디스플레이

## Invariants

- TromboneNoteDisplayAdapter는 INoteDisplayController를 구현하며
  Trombone prefab의 자식 GameObject에 부착된다
  (RhythmGameHost가 GetComponentInChildren로 탐색)
- 파셜 수(5)와 패널 수는 항상 동일하다
- `Begin` 호출 전 이전 세션 잔여 패널은 완전 정리된다 (Hide 선행)
- Completed 이벤트는 모든 패널이 Completed를 발화한 뒤 정확히 1회 발생한다

## Assumptions

- TrombonePartialController.partialOffsetsSemitones 배열 크기 = 5
  — 출처: `Read Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs (2026-05-20)`
- Trombone prefab에 TromboneAnchor 자식이 존재하고 InstrumentTeleportLink가 부착됨
  — 출처: `unity-scene-reader Pattern A (2026-05-20)`
- RhythmGameHost는 `instrument.GetComponentInChildren<INoteDisplayController>(true)`로
  커스텀 디스플레이를 선택
  — 출처: `Read Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs (2026-05-20)`

**Prefab Hierarchy** (출처: `unity-scene-reader Pattern A (2026-05-20)`)

```
Trombone [InstrumentAudioOutput, Trombone, TrombonePartialController]
├── Rig
│   ├── Body [MeshFilter, MeshRenderer]
│   │   └── GripPoseHand (Left)
│   ├── Slide [MeshFilter, MeshRenderer, TromboneSlideController]
│   │   └── GripPoseHand (Right)  ← 이론적 스폰 위치 계산 기준
│   ├── MouthPiece
│   └── NoteDisplay [Canvas, TromboneNoteHud]  ← 기존 HUD (리듬게임 어댑터와 별개)
└── TromboneAnchor [TeleportationAnchor, TromboneAnchor, BoxCollider,
                    InstrumentTeleportColliderBinder, InstrumentTeleportLink]
```

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `docs/specs/rhythm-game/specs/04-drum-note-display.md` | `13-trombone-note-display.md` | 드럼은 HitZone Transform 기반 패널 배치 + BillboardUI. 트롬본은 파셜 수 고정(5) + 수평 반원형 배치 + 노트 스폰이 슬라이드 이론적 위치 |

## Open Tech Decisions

- [ ] 노트 스폰 3D 위치 결정 방식 (이론적 위치 vs 실제 오른손)
  → `decisions/01-trombone-note-spawn-position.md`
- [ ] 파셜 간 겹치는 MIDI 노트 범위의 패널 할당 정책
  → `decisions/02-trombone-partial-midi-overlap.md`
