# Input Mode Readiness Check — Tech Spec

**Sub-Spec:** [`17-input-mode-readiness-check.md`](../specs/17-input-mode-readiness-check.md)
**Status:** `Draft`
**Date:** 2026-05-27

## Components

- **`RequiredInputMode`** (신규) — HandTracking / Controller / Any 세 값의 enum. `Instruments/_Core/Scripts/` 배치.
- **`InstrumentBase`** (기존) — `requiredInputMode` 인스펙터 필드 + public getter 추가.
- **`XRInputModeDetector`** (신규) — `XRHandSubsystem` 활성·추적 여부를 조회해 현재 입력 모드를 반환하는 정적 유틸리티.
- **`RhythmGameSectionController`** (기존) — play 버튼 클릭 시 입력 모드 비교, 불일치 시 `InputModeWarningPanel` 활성화 + 매 프레임 재확인 후 자동 시작 로직 추가.
- **`InputModeWarningPanel`** (신규) — 안내 텍스트(`TextMeshProUGUI`) + 이미지 슬롯(`Image`, 비어 있으면 비활성)을 가진 MonoBehaviour. `Show(RequiredInputMode)` / `Hide()` 메서드 노출.

## Data / Control Flow

1. Play 버튼 클릭 → `RhythmGameSectionController.OnPlayButtonClicked()`
2. `currentInstrument.RequiredInputMode` 조회
3. `XRInputModeDetector.CurrentMode` 조회
4. `RequiredInputMode.Any` 또는 `CurrentMode == RequiredInputMode` → 기존 `StartSession()` 직행
5. 불일치 → `InputModeWarningPanel.Show(requiredMode)` + 매 프레임 폴링 시작
6. 매 프레임 Update: `XRInputModeDetector.CurrentMode == RequiredInputMode` 체크
7. 일치 감지 → `InputModeWarningPanel.Hide()` + `StartSession()` + 폴링 중단

## Boundaries

- **건드린다**: `InstrumentBase` (필드 추가), `RhythmGameSectionController` (play 로직 수정), 신규 `XRInputModeDetector`, 신규 `InputModeWarningPanel`
- **건드리지 않는다**: `RhythmGameHost`, `RhythmSession`, `RhythmJudge`, `RhythmClock`, 악기별 입력 자식 컴포넌트(DrumKit 등), 손 3-Layer 시스템

## Invariants

- `RequiredInputMode.Any`인 악기는 항상 즉시 시작된다.
- `InputModeWarningPanel`이 표시 중일 때 `StartSession`은 호출되지 않는다.
- `InputModeWarningPanel.Hide()` 이후에만 `StartSession()`이 호출된다 (순서 보장).
- `XRInputModeDetector`는 입력 모드를 변경하거나 XR 시스템을 건드리지 않는다 — 조회만 한다.

## Assumptions

- `com.unity.xr.hands 1.7.3`이 설치되어 `XRHandSubsystem`을 런타임에 조회 가능하다.  
  — 출처: `Read Assets/Samples/XR Hands/1.7.3/HandVisualizer/Scripts/HandProcessor.cs (2026-05-27)`
- `InstrumentBase`는 모든 악기의 기반 클래스이며 public 필드 추가가 하위 호환된다.  
  — 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-27)`
- `RhythmGameSectionController.OnPlayButtonClicked`가 세션 시작의 단일 진입점이다.  
  — 출처: `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-27)`
- Quest 플랫폼에서 현재 입력 모드는 HandTracking / Controller 둘 중 하나가 반드시 활성이다.

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `docs/specs/rhythm-game/specs/01-session-lead-in.md` | `17-input-mode-readiness-check.md` | 리드인은 게임 시작 후 타이밍 보정, 본 spec은 시작 전 입력 방식 사전 확인 |
| `docs/specs/session-panel/specs/02-start-menu-section.md` | `17-input-mode-readiness-check.md` | 시작 메뉴는 곡/난이도 UI 흐름, 본 spec은 시작 직전 XR 입력 호환성 확인 |

## Open Tech Decisions

- [x] XR 입력 모드 감지 방식 → `decisions/05-xr-input-mode-detection.md`
- [x] `InputModeWarningPanel` 참조 와이어링 방식 → `decisions/06-warning-panel-wiring.md`
