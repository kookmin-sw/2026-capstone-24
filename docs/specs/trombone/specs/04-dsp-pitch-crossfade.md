# DSP Pitch Crossfade — 슬라이드·배음 변경 시 끊김 없는 음 전환

**Parent:** [`_index.md`](../_index.md)

## Why

sub-spec 03 (Blow & Continuous Pitch Sound)은 발음 중 슬라이드 이동 시 "끊김 없는 음높이 변화"를 요건으로 박제했지만, 현재 구현은 슬라이드 또는 배음(Partial) 인덱스가 바뀔 때마다 발음을 즉시 중단(Choke)하고 새로 재생(NoteOn)하는 방식을 사용해 가청 클릭·무음 구간이 발생한다. 추가 오디오 자산 없이 DSP 레이어만으로 이 끊김을 제거해야 한다.

## What

- 왼손 Grip을 유지한 채 슬라이드를 이동하면 발음이 중단되지 않고 음높이만 즉시 변경된다.
- 왼손 Grip을 유지한 채 악기 기울기로 배음(Partial)이 바뀌어도 발음이 중단되지 않고 음높이만 즉시 변경된다.
- 음높이 변경 시 AudioSource 재시작 없이 버퍼 경계에서 짧은 볼륨 crossfade(수 ms)로 클릭 노이즈를 억제한다.
- 왼손 Grip 뗌, 앵커 이탈 등 기존 발음 종료 흐름은 변경하지 않는다.

## Behavior

- **Given** anchor 진입 + 왼손 Grip 유지(발음 중)
  **When** 슬라이드 인덱스가 변경된다
  **Then** 발음이 끊기지 않고 음높이가 새 슬라이드 위치에 대응하는 값으로 변경된다. 클릭 노이즈는 가청 한계 이하다.

- **Given** anchor 진입 + 왼손 Grip 유지(발음 중)
  **When** 악기 기울기로 Partial 인덱스가 변경된다
  **Then** 발음이 끊기지 않고 음높이가 새 Partial에 대응하는 값으로 변경된다.

- **Given** 발음 미시작 (왼손 Grip 미입력)
  **When** 슬라이드 또는 Partial이 변경된다
  **Then** 오디오에 아무런 변화가 없다.

- **Given** 발음 중
  **When** 왼손 Grip을 뗀다
  **Then** 기존 fade-out 흐름이 그대로 동작한다.

- **Given** 발음 중
  **When** 앵커에서 이탈한다
  **Then** 기존 Choke 흐름이 그대로 동작한다.

## Out of Scope

- 포르타멘토(pitch 미끄러짐) — 음높이는 즉시 변경, 선형/지수 보간 없음.
- 발음 중 멀티샘플 자동 교체 — sustain 중에는 발음 시작 시 선택된 클립을 계속 사용.
- 멀티플레이어 pitch 동기화.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리.

## Open Questions

_현재 열린 질문 없음._
