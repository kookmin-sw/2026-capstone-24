# Warning Panel Wiring in RhythmGameSectionController

**Sub-Spec:** [`17-input-mode-readiness-check.md`](../specs/17-input-mode-readiness-check.md)
**From Tech Spec:** `tech-specs/17-input-mode-readiness-check.md §Open Tech Decisions #2`
**Status:** `Accepted`
**Date:** 2026-05-27

## Context

`RhythmGameSectionController`에서 `InputModeWarningPanel`을 Show/Hide 해야 한다.
참조를 어떻게 획득할 것인지 결정해야 한다.

## Options Considered

- **SerializeField 직접 참조** — `RhythmGameSectionController` Inspector 필드로 `InputModeWarningPanel` 참조. 기존 `activeInstrumentProviderObject`, `songCatalogObject` 등의 패턴과 동일.
- **RhythmGameHost 경유** — `RhythmGameHost.ShowInputModeWarning(mode)` / `HideInputModeWarning()` 경유. 간접 호출로 의존 체인이 길어짐.

## Decision

**SerializeField 직접 참조** — 기존 `RhythmGameSectionController`의 Inspector 필드 패턴과
일관되며, 중간 단계 없이 단순하다.

## Consequences

- `RhythmGameSectionController` Inspector에 `InputModeWarningPanel` SerializeField가 추가된다.
- `InputModeWarningPanel` GameObject는 씬에 비활성(`SetActive(false)`) 상태로 미리 배치되어야 한다.
- `InputModeWarningPanel` 필드가 null이면 UI 안내 없이 게임 시작 차단만 된다 (null 안전 처리 필요).
- 이 결정을 변경해야 하는 trigger: 멀티플레이어 환경에서 플레이어별 패널 인스턴스 관리가 필요해질 때.
