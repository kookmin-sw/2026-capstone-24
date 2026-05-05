# Session Lead-in

**Parent:** [`_index.md`](../_index.md)

## Why

노트의 예약 시각이 세션 시작 직후(t≈0s)이면 스폰 시각(scheduledTime − lookAheadSeconds)이 음수가 된다. 세션 클락이 t=0에서 출발하면 그 음수 시각은 이미 지나간 것으로 처리되어 첫 노트가 패널 하단(판정선 근처)에 나타난다. 플레이어가 첫 노트를 인식하고 자세를 잡을 시간이 전혀 없다.

## What

세션이 시작될 때 고정 리드인 시간(기본값 3초)이 적용된다. 리드인 동안 내부 클락은 음수 시간대(-leadInSeconds)에서 출발해 카운트업하며, 노트는 패널 최상단에서 자연스럽게 낙하하기 시작한다. 첫 노트가 판정선에 도달하는 시각은 항상 세션 시작으로부터 leadInSeconds 이상 이후다.

- 리드인은 테스트 단축키(O키)와 정식 메뉴 진입 모두 동일하게 적용된다.
- 리드인 길이는 설정 가능(기본값 3s).
- leadInSeconds가 lookAheadSeconds보다 작으면 세션 시작 시 lookAheadSeconds로 자동 보정된다 (첫 노트가 반드시 패널 최상단에서 시작하도록 보장).

## Behavior

- **Given** lookAheadSeconds = 2, leadInSeconds = 3, 차트 첫 노트 t = 0s
  **When** 세션이 시작됨
  **Then** 세션 시작 후 1초 뒤 첫 노트가 패널 최상단에 나타나고, 3초 뒤 판정선에 도달한다.

- **Given** 리드인 시간이 lookAheadSeconds보다 긴 임의 값으로 설정됨
  **When** 세션이 시작됨
  **Then** 세션 시작 시점에 패널에 표시되는 노트는 없고, 클락이 (leadInSeconds − lookAheadSeconds)에 도달한 뒤부터 노트가 상단에서 나타나기 시작한다.

- **Given** 세션이 시작됨
  **When** 리드인 시간 동안 판정 입력이 들어옴
  **Then** 아직 판정선에 도달한 노트가 없으므로 Miss/Good/Perfect 중 어떤 판정도 발생하지 않는다.

## Out of Scope

- 카운트다운 시각 UI(3…2…1…) — 별도 피처.
- lookAheadSeconds 값 자체의 변경.
- 리드인 중 자동 반주 발화 여부 — 반주는 해당 트랙의 scheduledTime에 맞춰 기존 로직이 처리한다.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-03 | Session Lead-in 구현 — 클락 음수 시작 오프셋 | Ready | [2026-05-03-linksky0311-session-lead-in-clock-offset.md](../plans/2026-05-03-linksky0311-session-lead-in-clock-offset.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

- [x] leadInSeconds가 lookAheadSeconds보다 짧을 때(예: leadIn=1, lookAhead=2)의 정책 — 첫 노트가 상단이 아닌 중간에서 시작하는 것을 허용할지, 아니면 leadInSeconds ≥ lookAheadSeconds를 강제할지. → lookAheadSeconds로 자동 보정.
