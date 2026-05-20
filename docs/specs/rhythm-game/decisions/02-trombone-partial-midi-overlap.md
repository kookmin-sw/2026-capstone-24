# Trombone Partial MIDI Overlap Resolution

**Sub-Spec:** [`13-trombone-note-display.md`](../specs/13-trombone-note-display.md)
**From Tech Spec:** `tech-specs/13-trombone-note-display.md §Open Tech Decisions #2`
**Status:** `Accepted`
**Date:** 2026-05-20

## Context

baseTone(MIDI 33) + partialOffsets(12,19,24,28,31) + slideIndex(0~6) 조합으로
산출되는 MIDI 범위가 인접 파셜 간 겹친다
(파셜 1–2: MIDI 51–52, 파셜 2–3: MIDI 55–57, 파셜 3–4: MIDI 58–61).
InstrumentLaneConfig SO에서 겹치는 MIDI 노트를 어느 파셜(레인)에 할당할지
정책이 필요하다.

## Options Considered

- **낮은 파셜 우선 자동** — 겹치는 MIDI 노트를 포함하는 가장 낮은 파셜 인덱스에
  자동 할당. 결정론적이며 설정 비용 없음
- **InstrumentLaneConfig 수동 설정** — SO를 수동으로 편집해 per-note 파셜 할당.
  유연하지만 설정 비용 높음

## Decision

**낮은 파셜 우선 자동** — 설정 비용 없이 결정론적으로 동작.
사용자가 인터뷰에서 선택.

## Consequences

- planner는 Trombone LaneConfig SO 생성 시 파셜 0부터 순서대로 슬라이드 포지션
  범위를 채워나가되, 이미 할당된 MIDI 노트는 상위 파셜에서 중복 할당하지 않는
  방식으로 InstrumentLaneConfig를 구성해야 한다
- 결과적 매핑: MIDI 51–52는 파셜 1, MIDI 55–57은 파셜 2, MIDI 58–61은 파셜 3에 할당
