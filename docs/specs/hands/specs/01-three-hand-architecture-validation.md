# Three-Hand Architecture Validation

**Parent:** [`_index.md`](../_index.md)

## Why

부모 spec의 3-hand 구조(Ghost / Physics / Play)는 형태는 이미 코드에 존재하지만, 시각적으로 어느 핸드가 어떤 신호를 따라가는지 구분이 어렵다. 또한 Physics 핸드가 일시 중단됐을 때 Play 핸드가 의도대로 Ghost로 폴백하는지도 검증된 적이 없다.

본격적인 비통과 물리(non-kinematic) 전환에 들어가기 전에, 이 신호 흐름이 실제로 의도대로 동작하는지 눈으로 확인할 수 있는 검증 단계를 둔다.

## What

검증을 위해 각 핸드의 시각·동작을 다음과 같이 명확히 분리한다.

- **Ghost**: 투명 파랑으로 표시한다.
- **Physics**: 평소 사용자에게 보이지 않게 한다. 다만 디버그용 fallback 시각으로 투명 빨강 머터리얼을 부여해, 의도와 달리 렌더링이 켜졌을 경우 즉시 식별 가능하게 한다.

여기서 정의한 머터리얼은 검증 단계 이후에도 그대로 유지한다. 별도의 출시용 시각으로 다시 다듬지 않는다.
- **Play**: Physics 핸드의 활성/비활성 상태에 따라 추적 소스를 전환한다.
  - Physics 활성 → Play는 Physics를 따라간다.
  - Physics 비활성 → Play는 Ghost를 따라간다.

여기서 "Physics 비활성"은 **Physics 핸드 GameObject 자체가 비활성화된 상태**(SetActive(false))로 정의한다.

## Behavior

- **Given** 모든 손 시스템이 정상 동작하고 Physics 핸드가 활성 상태이다.
  **When** 사용자가 손을 움직인다.
  **Then** Play 핸드는 Physics 핸드를 따라간다. Ghost 핸드는 입력의 raw 위치를 투명 파랑으로 보여준다. Physics 핸드는 화면에 보이지 않는다.

- **Given** Physics 핸드 GameObject가 비활성화된다.
  **When** 사용자가 손을 움직인다.
  **Then** Play 핸드는 Ghost 핸드를 따라간다. Ghost는 그대로 보이고, Physics는 비활성이므로 보이지 않는다.

- **Given** 디버그 도중 의도와 달리 Physics 핸드의 렌더링이 켜져 있는 상황이다.
  **When** 사용자가 화면을 본다.
  **Then** Physics 핸드가 투명 빨강으로 즉시 식별된다. (정상 상태에서는 발생하지 않아야 한다.)

## Out of Scope

- Physics 핸드를 non-kinematic으로 바꾸는 본격 물리 동작은 다루지 않는다. → [`02-instrument-no-penetration.md`](02-instrument-no-penetration.md)에서 다룸.
- Physics 핸드를 **언제·어떤 조건**에서 비활성화할지(트리거 정책)는 본 sub-spec의 책임이 아니다. 본 sub-spec은 "비활성화된 상태에서 Play가 Ghost로 전환된다"는 동작만 보장한다.
- 손 메쉬 형태 등 머터리얼 외의 시각 디테일은 다루지 않는다. 본 sub-spec은 색·투명도와 렌더링 on/off만 정의한다.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
