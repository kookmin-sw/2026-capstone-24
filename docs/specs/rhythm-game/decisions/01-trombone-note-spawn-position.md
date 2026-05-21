# Trombone Note Spawn Position

**Sub-Spec:** [`13-trombone-note-display.md`](../specs/13-trombone-note-display.md)
**From Tech Spec:** `tech-specs/13-trombone-note-display.md §Open Tech Decisions #1`
**Status:** `Accepted`
**Date:** 2026-05-20

## Context

차트 노트가 스폰될 때 패널 위의 3D 출발 좌표를 어떻게 결정할 것인가.
트롬본 슬라이드는 7포지션을 가지므로, 같은 파셜이라도 요구 슬라이드 포지션에 따라
이론적 오른손 위치(Slide Transform의 월드 오프셋)가 달라진다.

## Options Considered

- **이론적 위치** — 차트 노트가 요구하는 슬라이드 포지션의 수학적 3D 위치에서 스폰.
  "어디에 손을 둬야 하는가"의 시각적 단서를 제공
- **실제 오른손 현재 위치** — 스폰 시점 슬라이드 끝의 실제 월드 좌표.
  타이밍 정보만 전달하고 위치 안내 없음

## Decision

**이론적 위치** — 플레이어에게 올바른 슬라이드 포지션을 시각적으로 안내하는 것이
핵심 리듬게임 UX. 사용자가 인터뷰에서 선택.

## Consequences

- planner는 TromboneSlideController의 슬라이드 포지션 인덱스(0~6)에 해당하는
  이론적 3D 오프셋을 계산하는 로직을 Approach에 포함해야 한다
- 노트의 스폰 위치는 요구 슬라이드 포지션에 고정되므로,
  실제 오른손 위치와 무관하게 결정된다
