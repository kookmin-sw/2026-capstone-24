# Blow & Continuous Pitch Sound

**Parent:** [`_index.md`](../_index.md)

## Why

트럼본의 발음은 호흡(blow)을 시작·종료하는 시점과, 그 동안 슬라이드 위치가 결정하는 연속 음높이로 구성된다. 호흡 입력을 직접 받을 수단이 없는 VR 환경에서는 왼손 Grip 을 호흡 on/off 의 대용으로 묶어, 사용자가 직관적으로 "지금 분다 / 안 분다" 를 결정할 수 있게 한다. 동시에 슬라이드 위치가 어떤 음으로 매핑되는지가 일관되어야 사용자가 음감으로 연주를 학습할 수 있다.

또한 트럼본은 Piano · DrumKit 와 함께 Instruments 도메인의 일원이며, 향후 RhythmGame 연동을 차단하지 않으려면 InstrumentBase 의 MIDI 표면(MidiTriggered 이벤트, instrumentId, instanceVolume 등)을 통과해야 한다. 본 sub-spec 은 이 두 요건(연속 pitch + InstrumentBase 통과)을 동시에 만족시키는 발음 라이프사이클을 책임진다.

## What

- 왼손 Grip 을 누른 순간 트럼본 음이 시작되고, 떼는 순간 음이 멈춘다 (Grip 홀드 동안만 발음).
- 음의 높이는 슬라이드 local x 가 min / max 사이에서 정규화된 비율로 정해지며, 슬라이드 끝까지 밀어 넣음 = C4 (Middle C), 끝까지 뽑음 = F#3 (6반음 하행) 의 연속 매핑이다 — 즉 슬라이드 이동에 따라 글리산도가 그대로 들린다.
- 사용자가 베이스 톤 wav 한 개를 제공하면 그 클립이 트럼본 음의 원천이 된다 — 파일명이 곧 노트 이름(C4) 라벨링.
- 발음 중 슬라이드를 움직이면 (왼손 Grip 유지 + 오른손 Grip 으로 슬라이드 운전 동시) 음높이가 끊김 없이 변화한다.
- 트럼본은 Piano 와 동일한 InstrumentBase 계약(instrumentId, instanceVolume, MidiTriggered 이벤트 표면)을 따른다 — RhythmGame 연동을 위한 표면은 노출되지만 실제 연동은 본 피처 범위 외.
- anchor 진입 상태가 아닐 때는 왼손 Grip 입력이 발음을 트리거하지 않는다.

## Behavior

- **Given** anchor 진입 + 왼손 Grip 미입력
  **When** 사용자가 왼손 Grip 을 누른다
  **Then** 트럼본 음이 발음을 시작하고, 그 순간 슬라이드 위치에 대응하는 음높이로 들린다.

- **Given** 트럼본 음이 발음 중
  **When** 사용자가 오른손 Grip 으로 슬라이드를 좌우 이동
  **Then** 발음이 끊기지 않고 음높이가 슬라이드 위치에 따라 연속적으로 변한다.

- **Given** 트럼본 음이 발음 중
  **When** 사용자가 왼손 Grip 을 뗀다
  **Then** 트럼본 음이 멈춘다.

- **Given** 슬라이드 local x = min
  **When** 왼손 Grip 발음
  **Then** C4 (Middle C, 261.63 Hz) 가 들린다.

- **Given** 슬라이드 local x = max
  **When** 왼손 Grip 발음
  **Then** F#3 (약 185 Hz, C4 에서 6반음 하행) 가 들린다.

- **Given** 트럼본 anchor 외부
  **When** 사용자가 왼손 Grip 을 누른다
  **Then** 트럼본 음은 발음되지 않는다.

## Out of Scope

- 호흡 강도 / 입술 압력 시뮬레이션 (velocity 는 고정 1.0).
- 슬라이드 7 포지션 snap 또는 반음 quantize (continuous 가 본 sub-spec 의 결정).
- 다양한 음정의 베이스 톤 wav 멀티 샘플 (단일 C4 한 개만).
- RhythmGame 의 실제 lane / 판정 연동 (MidiTriggered 이벤트 표면만 노출, lane config SO 채우기는 후속 spec).
- 다른 트럼본 인스턴스의 음 동기화 (멀티플레이어 도메인).

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리.

## Open Questions

_현재 열린 질문 없음._
