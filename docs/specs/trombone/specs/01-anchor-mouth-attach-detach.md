# Anchor & Mouth Attach/Detach

**Parent:** [`_index.md`](../_index.md)

## Why

트럼본은 마우스피스가 사용자 입에 정렬되지 않으면 연주 자세 자체가 성립하지 않는다. anchor 도착 직후 사용자가 본체를 직접 들어 입에 가져다 대는 단계는 매번 발생하는 비용이고, 또한 본체 정렬과 양손 GripPose 적용이 시간 차로 일어나면 "잡힌 채로 입에 댄 자세" 가 한 프레임이라도 어긋나 보인다. 이 sub-spec 은 anchor 진입의 한 순간에 본체 정렬 + 양손 GripPose 가 일관되게 함께 발화하고, 이탈 시 동일한 단위로 함께 해제되는 라이프사이클을 책임진다.

## What

- 트럼본 anchor 에 텔레포트로 도착하는 순간 트럼본 본체의 마우스피스가 사용자의 입 위치에 정렬된다 (position + rotation).
- 같은 순간 양손이 트럼본 본체를 잡은 포즈로 전환된다 — 왼손은 Body 의 GripPoseHand 위치에, 오른손은 Slide 의 GripPoseHand 위치에 시각적으로 고정.
- anchor 외부로 텔레포트하는 순간 트럼본 본체는 anchor 진입 직전의 scene 배치 위치(위치 + 회전)로 복귀하고 양손 GripPose 가 해제된다.
- 같은 anchor 로 재텔레포트하는 경우 attach 가 중복 발화하지 않는다 (no-op).
- 트럼본 prefab 에 마우스피스 기준점이 박혀 있고, VR Player rig 에 입 기준점이 박혀 있다 — 둘이 align 의 양 끝.

## Behavior

- **Given** 사용자가 트럼본 anchor 외부에 있음
  **When** anchor 로 텔레포트
  **Then** 트럼본 본체의 마우스피스 기준점이 사용자 입 기준점과 일치하도록 본체 root 가 이동·회전하고, 같은 프레임에 왼손은 Body GripPoseHand 포즈로, 오른손은 Slide GripPoseHand 포즈로 전환된다.

- **Given** 트럼본을 입에 댄 연주 상태
  **When** anchor 외부 다른 지점으로 텔레포트
  **Then** 트럼본 본체는 진입 직전 scene 배치 위치/회전으로 복귀하고, 양손 GripPose 가 해제되어 평상시 손 표시로 돌아간다.

- **Given** 트럼본을 입에 댄 연주 상태
  **When** 같은 anchor 로 다시 텔레포트
  **Then** attach 가 중복 발화하지 않고 입 정렬 상태가 그대로 유지된다.

- **Given** 텔레포트가 cancel 로 종료
  **When** anchor select 가 isCanceled 로 풀림
  **Then** attach / detach 어느 쪽도 발화하지 않는다.

## Out of Scope

- 입 기준점이 정렬된 후 슬라이드 운전·발음 — 02 / 03 sub-spec.
- 본체 충돌 (입 외 신체 또는 환경과의 collision) — 본 피처 전체 범위 외.
- 멀티플레이어로 다른 사용자가 보는 트럼본 본체 동기화.
- anchor 진입 중 손의 free move (잡힌 손이 anchor 안에서 자유롭게 떨어졌다 다시 붙는 모드).

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리.

## Open Questions

_현재 열린 질문 없음._
