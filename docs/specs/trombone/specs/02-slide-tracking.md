# Slide Tracking by Right Grip

**Parent:** [`_index.md`](../_index.md)

## Why

트럼본의 음높이는 슬라이드 위치 변화에 연속적으로 묶인다. 그렇다고 anchor 진입 직후 항상 사용자의 오른손 위치 변화가 슬라이드를 움직이게 두면 사용자가 손을 잠시 풀거나 다른 자세를 취할 때 슬라이드가 의도와 무관하게 흔들린다. 명시적 입력(오른손 Grip 홀드) 동안에만 슬라이드를 운전하도록 묶어, "지금 슬라이드를 조작하고 있다" 의 의도와 슬라이드 위치 변화를 1:1 로 맞춘다. 또한 슬라이드를 잡은 시각적 손은 슬라이드와 함께 움직여야 자연스럽다.

## What

- 오른손 Grip 이 눌리는 순간을 baseline 으로 그 시점의 오른손 위치(트럼본-local x)와 그 시점 슬라이드 위치(local x)가 함께 박제된다.
- Grip 이 눌려 있는 동안 매 프레임 (현재 오른손 trombone-local x − baseline 오른손 x) 만큼이 baseline 슬라이드 x 에 더해져 슬라이드의 새 local x 가 된다.
- 슬라이드의 local x 는 트럼본 prefab 에 박힌 min / max 범위로 clamp 된다 (실제 트럼본 슬라이드 가동 범위 박제).
- Grip 을 떼는 순간 슬라이드는 마지막 위치를 그대로 유지한다 (자동 복귀 없음).
- 슬라이드의 GripPoseHand 가 슬라이드의 자식이라, 슬라이드가 움직이면 시각적 오른손도 슬라이드 위 같은 지점에 함께 보인다.
- 위 모든 동작은 트럼본 anchor 에 진입한 상태(01 sub-spec 의 attach 가 활성)에서만 의미를 가진다 — anchor 밖에서 Grip 을 눌러도 슬라이드는 움직이지 않는다.

## Behavior

- **Given** anchor 진입 + 오른손 Grip 미입력
  **When** 사용자가 오른손을 좌우로 움직임
  **Then** 슬라이드는 움직이지 않고, 시각적 오른손은 슬라이드의 GripPoseHand 위치에 그대로 보인다.

- **Given** anchor 진입 + 오른손 Grip 미입력 + 슬라이드 local x = X0
  **When** 사용자가 오른손 Grip 을 누른다 (그 시점 오른손 trombone-local x = H0)
  **Then** baseline (H0, X0) 가 박제되고, 그 프레임 이후 슬라이드 local x = clamp(X0 + (H − H0), min, max). 시각적 오른손은 슬라이드와 함께 이동한다.

- **Given** 오른손 Grip 홀드 + 슬라이드가 움직이고 있음
  **When** 사용자가 Grip 을 뗀다
  **Then** 슬라이드는 마지막 local x 를 그대로 유지한다.

- **Given** anchor 진입 + 슬라이드 가 min 위치
  **When** 사용자가 Grip 홀드 상태에서 손을 min 보다 더 안쪽으로 이동
  **Then** 슬라이드 local x 는 min 으로 clamp 되고 더 줄지 않는다 (max 도 동일).

## Out of Scope

- 슬라이드 위치에 따른 음높이 매핑·사운드 — 03 sub-spec.
- Grip 입력 외 별도 모드 전환 (예: 트리거로 슬라이드 운전).
- 슬라이드의 물리 시뮬레이션 (관성, 충돌, 진동).

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리.

## Open Questions

_현재 열린 질문 없음._
