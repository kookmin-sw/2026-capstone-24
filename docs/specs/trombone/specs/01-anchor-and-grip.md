# Trombone — 앵커·그립

**Parent:** [`_index.md`](../_index.md)

## Why

트럼본 연주의 전제 조건을 구성한다. 앵커 진입·이탈을 감지하고, 트럼본을 플레이어 입 앞에
실시간으로 유지하며, 양손을 연주에 적합한 GripPose 상태로 교체한다.

## What

텔레포트 기반 앵커 진입·이탈을 감지하는 컴포넌트를 제공한다.

- 진입 시: 트럼본이 VR Player의 Mouth Transform에 맞춰 정렬되고, 이후 매 프레임 추적한다.
- 진입 시: 왼손 PhysicsHand를 Deactivate시키고 트럼본 바디 고정 위치에 GripPose 모델을 표시한다.
- 진입 시: 오른손 PhysicsHand를 Deactivate시키고 슬라이드 초기 위치에 GripPose 모델을 자동 연결한다.
- 이탈 시: 트럼본이 추적을 멈추고 원래 테이블 위치로 돌아가며, 양손 GripPose 제거·PhysicsHand Activate 복구.

## Behavior

- **Given** 플레이어가 트럼본 앵커 근처에 있음
  **When** 앵커에 텔레포트
  **Then** 트럼본이 Mouth Transform에 위치·회전 정렬되고, 이후 매 프레임 Mouth를 추적.
          왼손·오른손 PhysicsHand가 Deactivate되고 각 GripPose 모델이 표시됨.

- **Given** 플레이어가 앵커 안에서 연주 중
  **When** 플레이어가 다른 곳으로 텔레포트
  **Then** 트럼본이 추적을 중단하고 원래 테이블 위치·회전으로 복귀.
          GripPose 모델 제거, PhysicsHand Activate 복원.

- **Given** 앵커 안에서 플레이어가 고개를 돌리거나 이동
  **When** 매 프레임
  **Then** 트럼본 전체(바디·슬라이드 포함)가 Mouth Transform을 따라 이동·회전.

## Out of Scope

- 슬라이드 X 축 이동 처리 및 MIDI 노트 발음 (→ 02-slide-midi)
- 버튼 트리거 기반 발음 (후속 피처)
- SampleScene 프리팹 셋업 (TestSceneSanyo에서만 개발)
- 손 포즈 세부 블렌딩 튜닝

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
