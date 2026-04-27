# Hand Physics Interaction

**Parent:** [`_index.md`](../_index.md)

## Why

현재 Physics Hand는 Kinematic이라 외부 물체를 그대로 관통한다. 드럼을 두드려도 손이 지나가 버려 무게감이 없고, 무중력감이 깨진다. 동시에 Play Hand가 항상 Physics를 따라가기 때문에 Physics가 흔들리거나 비정상 상태일 때 시각적으로도 어색해진다. Drumstick을 잡았을 때도 손 포즈가 도구의 grip 포즈와 어긋나 "도구를 잡았다"는 신뢰감이 떨어진다.

이 sub-spec은 Physics Hand를 진짜 물리 객체로 전환하면서, 그 부작용(이동/턴 시 분리, Play Hand 어색함)까지 포함해 손 시스템의 동작을 한 번에 재정의한다.

## What

다음 다섯 가지 동작 변화를 묶어 다룬다.

- **Physics Hand의 비-Kinematic 전환** — 외부 물체와 실제로 충돌·반작용한다.
- **Play Hand의 동적 follow** — Physics Hand가 활성 상태면 Physics를, 비활성 상태면 Ghost를 따라간다.
- **Drumstick grip 즉시 스냅** — 잡는 순간 Play Hand 포즈가 스틱에 사전 정의된 grip 포즈로 즉시 전환된다.
- **이동/스냅 턴 중 Physics Hand 일시 비활성화** — 이동·턴 중에는 Physics를 끄고, 종료 직후 새 위치에서 즉시 복귀한다.
- **Ghost Hand 시각화 변경** — 디버깅을 위해 파란색 투명 머티리얼로 교체해 다른 레이어와 구분한다.

## Behavior

- **Given** Physics Hand가 활성 상태이고 외부 물체가 손 경로에 있음
  **When** 사용자가 손을 그 방향으로 움직임
  **Then** Physics Hand가 물체에 막혀 진행을 멈추고, Play Hand는 Physics를 따라가 같은 위치에서 정지한다. Ghost Hand는 입력 위치 그대로 진행해 시각적으로 분리된다.

- **Given** 플레이어가 이동 또는 스냅 턴을 시작
  **When** 이동/턴이 진행 중
  **Then** Physics Hand가 비활성화되고, Play Hand는 Ghost를 따라가 즉시 새 위치에서 자연스럽게 보인다. 이동/턴이 끝나는 즉시 Physics Hand가 새 위치에서 복귀해 다시 Play Hand의 follow 대상이 된다.

- **Given** 사용자의 손이 Drumstick에 닿음
  **When** Grab 트리거 발동
  **Then** Play Hand의 포즈가 즉시 스틱에 사전 정의된 grip 포즈로 스냅된다. 보간 없음.

- **Given** 사용자가 Drumstick을 놓음
  **When** Release 트리거 발동
  **Then** Play Hand가 grip 포즈에서 풀려나 다시 일반 follow 동작으로 돌아간다.

- **Given** 씬이 실행 중
  **When** 어떤 시점이든
  **Then** Ghost Hand는 파란색 투명 머티리얼로 렌더링되어 Play/Physics와 시각적으로 즉시 구분 가능하다.

## Out of Scope

- Drumstick 외 다른 악기·도구의 grip 포즈 정의 (각 도구별로 별도 작업)
- 양손 협조 동작 (한 손으로 잡은 도구를 다른 손이 함께 잡는 경우 등)
- 핸드 트래킹 인식 실패·복구 시 레이어 동기화 정책
- 이동/턴 모드 외의 다른 XR locomotion 방식(텔레포트, 부드러운 이동 등)에 대한 별도 처리
- Ghost Hand의 디버깅 외 시각화 (예: 시연용 강조 표시)

## Implementation Plans

| 번호 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 006 | Physics Hand 충돌·반작용 + Play Hand 동적 follow + Ghost 시각화 | Ready | [006-physics-hand-collision-and-follow.md](../plans/006-physics-hand-collision-and-follow.md) |
| 007 | Locomotion·Snap Turn 중 Physics Hand 일시 비활성화 | Ready | [007-locomotion-physics-hand-pause.md](../plans/007-locomotion-physics-hand-pause.md) |
| 008 | Drumstick grip 즉시 스냅 (보간 제거 + 충돌 무시) | Ready | [008-drumstick-grip-pose-snap.md](../plans/008-drumstick-grip-pose-snap.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 번호는 전역 일련번호.

## Open Questions

- [ ] Physics Hand 비활성화 판단 트리거의 구체적 출처(XR locomotion provider 이벤트 vs 다른 방식)는 plan 단계에서 결정
- [ ] 이동/턴 종료 후 Physics Hand "즉시 복귀"가 1프레임 jump로 보일 수 있는지, 보간이 필요한지는 구현 후 검증
