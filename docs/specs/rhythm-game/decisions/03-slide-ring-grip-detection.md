# Slide Ring Grip Detection Method

**Sub-Spec:** [`15-trombone-slide-position-rings.md`](../specs/15-trombone-slide-position-rings.md)
**Status:** `Accepted`
**Date:** 2026-05-27
**From Tech Spec:** `tech-specs/15-trombone-slide-position-rings.md §Open Tech Decisions`

## Context

`SlidePositionMarkers`가 오른손 Grip 상태를 알아야 활성 링을 강조할 수 있다.
`TromboneSlideController`가 이미 grip 입력을 관리하므로 두 컴포넌트가
같은 `InputAction`을 중복 읽지 않도록 상태 공유 방식을 결정한다.

## Options Considered

- **IsGripHeld 프로퍼티 폴링** — `TromboneSlideController`에 `public bool IsGripHeld` 추가; `SlidePositionMarkers`가 `Update`에서 폴링. `TromboneBodyVisualSnap`의 `TrombonePartialController.PartialIndex` 폴링 패턴과 동일.
- **GripStarted/GripReleased 이벤트** — `TromboneSlideController`에 이벤트 2개 추가; `SlidePositionMarkers`가 구독. 이벤트 드리븐이지만 추가 코드 증가.

## Decision

**IsGripHeld 프로퍼티 폴링** — `TromboneBodyVisualSnap` 패턴과 일치하고 단순함.

## Consequences

- planner는 `TromboneSlideController`에 `public bool IsGripHeld => m_IsGripHeld;` 프로퍼티를 추가해야 한다.
- planner는 `SlidePositionMarkers`가 `TromboneSlideController` 참조를 획득해 매 프레임 `IsGripHeld` + `SlideIndex`를 폴링하도록 수정해야 한다.
- 이벤트 기반으로 전환할 경우 이 ARD를 재평가한다.
