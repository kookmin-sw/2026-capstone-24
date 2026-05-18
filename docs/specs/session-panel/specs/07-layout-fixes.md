# Layout Fixes

**Parent:** [`_index.md`](../_index.md)
**Tech Spec:** skipped

## Why

sub-spec 06 스타일 적용 후 수동 검증에서 두 가지 레이아웃 문제가 발견됐다. (1) InstrumentToggleButton이 100×100 정사각형 안에 악기명만 표시돼 시각적으로 불편하고 토글 상태가 한눈에 구분되지 않는다. (2) VolumeSection의 슬라이더들이 SessionPanel 경계 밖에 떠서 표시된다. 두 문제 모두 현재 레이아웃/배치 설정에서 비롯된 것으로, 실제 서비스 품질 기준을 충족하려면 수정이 필요하다.

## What

- InstrumentToggleButton을 수직 2단 레이아웃으로 재구성한다: 상단에 악기명 텍스트, 하단에 소형 토글 인디케이터(둥근 사각형 버튼).
- VolumeSection의 슬라이더(Master, 악기별 슬라이더)가 SessionPanel의 시각적 경계 내에 표시되도록 레이아웃/위치를 수정한다.

## Behavior

- **Given** 곡이 선택되어 InstrumentToggleButton들이 표시됨
  **When** 사용자가 패널을 봄
  **Then** 각 버튼에서 악기명(텍스트)이 위에 표시되고, 아래에 토글 상태를 나타내는 소형 인디케이터가 표시된다.

- **Given** InstrumentToggleButton의 `isOn=true`
  **When** 사용자가 버튼을 봄
  **Then** 하단 토글 인디케이터가 액센트색(#4A9EFF)으로, `isOn=false`이면 반투명 흰색으로 표시된다.

- **Given** 악기를 잡고 있어 SessionPanel이 표시됨
  **When** 사용자가 왼손 핀치 또는 패널을 호출해 Volume 섹션을 봄
  **Then** Master 슬라이더와 악기별 슬라이더가 SessionPanel의 시각적 경계(패널 배경) 안에 표시된다.

- **Given** VolumeSection이 SessionPanel 안에 배치됨
  **When** 슬라이더를 조작함
  **Then** 슬라이더 동작(값 변경)은 기존과 동일하게 유지된다.

## Out of Scope

- InstrumentToggleButton 및 VolumeSlider의 색상·스프라이트·애니메이션 변경 — sub-spec 06에서 완료.
- InstrumentToggleButton의 크기(현행 100×100) 변경 — 레이아웃 내부 재배치만 다룬다.
- VolumeSection에 새 슬라이더 항목 추가 — sub-spec 03 범위.
- 상호작용 로직(슬라이더 값 바인딩, 토글 콜백) 변경.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-19 | Layout Fixes (sub-spec 07 단일 plan) | Ready | [`2026-05-19-linksky0311-layout-fixes.md`](../plans/2026-05-19-linksky0311-layout-fixes.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
