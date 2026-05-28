# Trombone 슬라이드 링 — grip OFF 상태 기본 dim

**Linked Spec:** [`15-trombone-slide-position-rings.md`](../specs/15-trombone-slide-position-rings.md)
**Caused By:** [`2026-05-27-claude-trombone-slide-rings-dim.md`](./2026-05-27-claude-trombone-slide-rings-dim.md)
**Status:** `Done`

## Goal

선행 cascade plan(dim 방식)에서 grip OFF 상태일 때 링이 기본 Palette 색(밝음)으로 표시됨.
사용자 피드백: "기본적으로 오른손 그랩을 하고 있지 않은 상태에서 모든 색이 어둡게 되어야함".
본 plan은 동작을 **grip OFF = 전체 dim, grip ON = 현재 링만 기본 색 + 나머지 dim** 으로 변경한다.

## Context

선행 plan `ApplyDimExceptActive` 조건 (현재):

```csharp
if (activeIdx < 0 || i == activeIdx)
    target = baseC;  // -1이면 모든 링 기본 색 (밝음)
```

이 조건으로 인해 grip OFF(`desiredIdx = -1`) → `ApplyDimExceptActive(-1)` → 모든 링 밝음. 사용자가 원하는 동작과 반대.

## Approach

### 변경 1 — `ApplyDimExceptActive` 조건 단순화

`SlidePositionMarkers.cs` line 120:

```csharp
// 변경 전
if (activeIdx < 0 || i == activeIdx)
// 변경 후
if (i == activeIdx)
```

`-1` 전달 시 어떤 링 인덱스도 `-1`과 일치하지 않으므로 모든 링이 dim. 주석도 갱신.

### 변경 2 — attach 직후 즉시 dim 적용

`LateUpdate` 내 `attached != _lastIsAttached && attached` 분기에 진입 직후:

```csharp
// 기존: SetActive 토글 후 detach 분기만 처리
// 추가: attach 분기에도 ApplyDimExceptActive(-1) + 캐시 리셋
```

`_spawned`의 `SetActive(true)` 직후 `ApplyDimExceptActive(-1)` + `_lastHighlightedIndex = -1`을 호출.
링이 화면에 나타나는 첫 프레임부터 dim 상태로 시작.

## Deliverables

- `Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` — 조건 1줄 변경 + attach 분기 2줄 추가 + 주석 1줄 갱신

## Acceptance Criteria

- [ ] `[auto-hard]` `ApplyDimExceptActive` 내 `activeIdx < 0 ||` 조건이 제거됨.
  **검증:** `Grep -n "activeIdx < 0" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과 0건.
- [ ] `[auto-hard]` LateUpdate attach 분기(`_lastIsAttached` 토글 직후)에 `ApplyDimExceptActive(-1)` 호출이 존재함.
  **검증:** `Grep -n -A 10 "SetActive(attached)" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과에 `ApplyDimExceptActive(-1)` 포함.
- [ ] `[manual-hard]` 트롬본 attach 직후 grip OFF → 7개 링 모두 dim(어두운) 상태로 표시.
- [ ] `[manual-hard]` grip ON → 현재 SlideIndex 링만 기본 Palette 색(밝음), 나머지 6개는 dim.
- [ ] `[manual-hard]` grip ON → grip OFF → 7개 링 다시 모두 dim (기본 색으로 복원되지 않음).

## Notes

- `detach` 분기의 `ApplyDimExceptActive(-1)` 호출은 그대로 유지 — 링이 `SetActive(false)` 직전에 dim 상태로 정리됨 (재attach 시 잔여 색 없음).
- `OnDisable`의 `ApplyDimExceptActive(-1)` 호출도 그대로 유지 — `ClearSpawned` 직전 정리.
- `_lastHighlightedIndex = -1` 초기값은 유지. attach 분기에서 `ApplyDimExceptActive(-1)` 명시 호출하므로 초기 dim 상태가 보장됨.

## Handoff

`SlidePositionMarkers.cs` — `ApplyDimExceptActive` 조건 `activeIdx < 0 ||` 제거(1줄), attach 분기에 `ApplyDimExceptActive(-1)` + `_lastHighlightedIndex = -1` 추가(2줄). grip OFF = 전체 dim, grip ON = 현재 링만 기본 색 + 나머지 dim. manual-hard 5항목 pass(2026-05-27).
