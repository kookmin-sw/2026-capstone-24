# Volume Section

**Parent:** [`_index.md`](../_index.md)

## Why

VR 환경에서 사용자는 자기 악기 사운드와 전체 마스터 사운드를 즉시 자기 귀에 맞춰야 연주가 편해진다. 이 조작이 별도의 메뉴 깊은 곳에 있으면 연주 흐름이 끊기고, 악기를 잡지 않은 상태에서도 마스터 볼륨에 빠르게 접근할 수단이 필요하다. 또한 멀티플레이가 들어오면 타 사용자 악기와 마이크 사운드까지 같은 곳에서 다룰 수 있어야 사용자는 한 곳만 학습하면 된다.

## What

세션 패널의 한 섹션. 사용자가 들을 사운드의 볼륨을 조절한다. 항상 마스터 볼륨이 노출되며, 사용자가 한 악기를 잡고 있는 동안에는 내 악기 볼륨이 추가된다. 내 악기 볼륨은 잡은 특정 악기 인스턴스에만 적용되며, 동일 종류의 다른 악기 인스턴스에는 영향을 주지 않는다. 볼륨 값은 0.0~1.0 선형 비율로 다루며, 기본값은 0.5(최대의 절반)이다. 마지막 설정값은 사용자 단위로 persist되어 다음 실행 시 복원된다. 멀티플레이가 들어올 때 같은 섹션에 타 사용자 악기 볼륨 슬롯과 마이크 볼륨 슬롯이 동적으로 추가된다 (이번 단계에서는 What 수준만 박제하고 Behavior는 정의하지 않는다).

## Behavior

단일 플레이(현 단계) 동작만 본 sub-spec에서 정의한다. 멀티플레이 슬롯의 Behavior는 별도 후속 spec에서 다룬다.

- **Given** 패널이 노출되어 있음
  **When** 볼륨 섹션을 봄
  **Then** 마스터 볼륨 컨트롤이 항상 노출된다.

- **Given** 사용자가 한 악기를 잡고 있음
  **When** 볼륨 섹션을 봄
  **Then** 마스터 볼륨 컨트롤과 함께 내 악기 볼륨 컨트롤이 노출된다.

- **Given** 패널이 핀치 호출(악기 미잡음)로 떠 있음
  **When** 볼륨 섹션을 봄
  **Then** 마스터 볼륨 컨트롤만 노출되고, 내 악기 볼륨 슬롯은 노출되지 않는다.

- **Given** 마스터 또는 내 악기 볼륨 값이 변경됨
  **When** 변경이 확정됨
  **Then** 이후 출력되는 사운드에 그 값이 반영되며, 그 값은 사용자 단위로 persist되어 다음 실행 시 복원된다.

- **Given** 사용자가 한 악기 인스턴스의 내 악기 볼륨 값을 변경함
  **When** 변경이 확정됨
  **Then** 그 인스턴스의 볼륨만 변경되며, 동일 종류의 다른 악기 인스턴스는 영향받지 않는다.

## Out of Scope

- 멀티플레이용 타 사용자 악기·마이크 볼륨의 Behavior — What 수준만 본 sub-spec에 박제, 동작은 후속 멀티플레이 spec에서 정의.
- 패널의 표시 트리거·위치·컨텍스트별 섹션 가시성 — [`01-anchoring.md`](01-anchoring.md).
- 시작 메뉴 섹션 — [`02-start-menu-section.md`](02-start-menu-section.md).
- 사운드 파이프라인(MIDI 발화, 믹싱) 자체.
- 볼륨 값의 시각·청각 피드백 디자인.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-05 | Session Panel Volume Section | Done | [2026-05-05-linksky0311-session-panel-volume-section.md](../../_archive/session-panel/plans/2026-05-05-linksky0311-session-panel-volume-section.md) |
| 2026-05-07 | Instance Volume Slider Verification | Ready | [2026-05-07-linksky0311-session-panel-instance-volume-verification.md](../plans/2026-05-07-linksky0311-session-panel-instance-volume-verification.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
