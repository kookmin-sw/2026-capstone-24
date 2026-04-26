# Timing Clock

**Parent:** [`_index.md`](../_index.md)

## Why

이 리듬게임은 오디오 파일을 동반하지 않고 모든 사운드를 MIDI로 발화한다. 그래서 곡 진행을 따라가는 마스터 시계가 외부 오디오에서 오지 않고 시스템 자체에서 만들어져야 한다. 이 시계는 채점(`judgment`)과 자동 반주(`accompaniment`)가 동시에 참조하는 단일 진실원이며, 두 모듈이 다른 시계를 보면 입력 채점과 발화가 어긋나 곡이 깨진다.

## What

세션 동안 곡 시작 시점부터 흐르는 단일 마스터 시계가 존재한다. 이 시계는 차트의 BPM·tick 해상도 정보를 사용해 차트의 tick 위치를 절대 시각(초)으로 변환할 수 있고, 반대로 현재 시각이 차트의 어느 위치에 해당하는지 알려줄 수 있다. 시계는 시작/일시정지/종료 가능한 상태를 가지며, 곡 진행 중 BPM이 바뀌어도 일관되게 흐른다.

## Behavior

- **Given** 세션이 시작됨
  **When** 시계가 출발함
  **Then** 시계는 곡 시작 시각(0초)부터 시작해 단조 증가한다.

- **Given** 차트의 한 노트가 특정 tick 위치에 있음
  **When** 시계에 그 노트의 시각을 묻음
  **Then** BPM과 tick 해상도를 반영한 절대 시각이 반환된다.

- **Given** 곡 진행 중 BPM이 바뀌는 구간이 차트에 정의됨
  **When** 시계가 그 구간을 지남
  **Then** 그 이후 노트들의 시각 계산은 새 BPM 기준으로 누적된다.

- **Given** 채점 모듈과 자동 반주 모듈이 같은 시계를 참조
  **When** 시계가 진행됨
  **Then** 두 모듈은 항상 같은 "지금"을 본다.

- **Given** 시계가 일시정지됨
  **When** 다시 재개됨
  **Then** 일시정지된 시점부터 이어 흐른다.

## Out of Scope

- 노트의 입력 매칭 판정 → `judgment`.
- 비-플레이어 트랙 자동 발화 → `accompaniment`.
- 곡 정지/재개를 트리거하는 사용자 인터랙션 → `session-flow`.
- 차트의 BPM/tick 정의 자체 → `chart-format`.

## Implementation Plans

| 번호 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 003 | Rhythm Clock — Master Time Source | Done | [003-rhythm-clock.md](../plans/003-rhythm-clock.md) |
| 005 | DSP Clock Provider 전환 | Done | [005-dsp-clock-provider.md](../plans/005-dsp-clock-provider.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 번호는 전역 일련번호.

## Open Questions

- [x] 시계의 정밀도 진실원 → **`AudioSettings.dspTime` (DSP 시계) 채택.** Plan 005에서 `DspTimeProvider`로 구현 완료.
- [x] 일시정지 / 시간 신축(연습 모드 슬로우다운 등) → **이번 피처 스코프 제외.** 일시정지(Pause/Resume)는 Plan 003에서 구현됨. 슬로우다운은 다루지 않음.
- [x] 곡 시작 전 카운트인 처리 위치 → **`session-flow` 책임으로 위임.** `RhythmClock`은 `Start()` 호출 시점을 0초로 두고 단조 증가만 유지.
