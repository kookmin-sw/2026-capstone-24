# Base Teleport

**Parent:** [`_index.md`](../_index.md)

## Why

이 피처에서 텔레포트는 *유일한* 이동 수단이다. 따라서 가장 먼저 "왼손에서 라인을 발사해 가리킨 자리로 도착한다"는 기본 텔레포트 동작이 안정적으로 성립해야 한다. 후속 sub-spec(노 텔레포트 존, 악기 anchor)은 모두 이 기본 동작 위에 제약과 분기를 얹는 형태이므로, 이 sub-spec이 base 역할을 한다.

또한 "텔레포트만 쓴다"는 결정을 실제로 적용하려면, 기존에 동시에 존재하던 다른 이동 방식이 제거되어야 한다. 이동 방식이 둘 이상 공존하면 spec의 Why(조작 단순화)가 깨진다.

## What

- 사용자는 **왼손**의 입력으로만 텔레포트 라인을 띄우고, 라인이 가리키는 위치로 이동한다.
- 텔레포트는 **위치 이동만** 담당한다. 회전(Turn)은 이미 별도로 구현된 Snap Turn이 책임지며, 본 sub-spec이 회전을 변경하지 않는다.
- 기존에 존재하던 다른 이동 수단(예: 자유 이동 형태의 locomotion)은 **사용자에게 보이지도, 입력으로 발동되지도 않는 상태로 제거**된다.
- 텔레포트가 가능한 floor/surface와 그렇지 않은 영역의 구분은 본 sub-spec에서 도입한다 — 단, "특정 영역을 명시적으로 차단하는" 노 텔레포트 존 자체는 [`02-no-teleport-zones.md`](02-no-teleport-zones.md)에서 다룬다.

## Behavior

- **Given** 사용자가 가상 공간 안에 있고 왼손 입력이 idle 상태이다
  **When** 사용자가 왼손의 텔레포트 발동 입력을 시작한다
  **Then** 왼손 위치에서 출발하는 텔레포트 라인이 표시된다.

- **Given** 텔레포트 라인이 표시되고 있고 라인 끝이 텔레포트 가능한 surface를 가리킨다
  **When** 사용자가 텔레포트 발동 입력을 종료(확정)한다
  **Then** 사용자(=카메라 리그)는 라인이 가리키던 자리로 이동하며, 라인은 사라진다.

- **Given** 텔레포트 라인이 표시되고 있고 라인 끝이 텔레포트 가능한 surface를 벗어나 있다(허공·금지된 영역 등)
  **When** 사용자가 텔레포트 발동 입력을 종료(확정)한다
  **Then** 이동은 일어나지 않는다. 라인은 사라진다.

- **Given** 사용자가 오른손의 Snap Turn 입력을 사용한다
  **When** 본 sub-spec의 도입 전·후
  **Then** Snap Turn 동작은 동일하게 유지된다.

- **Given** 사용자가 본 피처 도입 이전의 자유 이동 입력을 시도한다
  **When** 도입 이후
  **Then** 어떤 이동도 발생하지 않으며, 해당 입력은 텔레포트와도 무관하다.

## Out of Scope

- 라인이 노 텔레포트 존을 가리켰을 때의 invalid 시각 표시 — [`02-no-teleport-zones.md`](02-no-teleport-zones.md).
- 악기 근처에서 라인이 anchor로 스냅·구별 표시되는 동작 — [`03-instrument-anchors.md`](03-instrument-anchors.md).
- 회전(Snap Turn / Continuous Turn)의 구현·조정. 본 sub-spec은 기존 회전 동작을 보존만 한다.
- 텔레포트 시 페이드 인/아웃 같은 시각 전환 효과의 정책 결정. 필요해지면 별도 plan 또는 sub-spec.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

- [ ] 텔레포트 발동에 사용할 왼손의 정확한 입력(thumbstick push / trigger / grip 등)은 plan 작성 시 결정한다.
- [ ] 텔레포트 직후 시각 전환 효과(즉시 / 페이드)의 기본값을 둘 것인지 여부.
