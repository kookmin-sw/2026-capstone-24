# Trombone Body Visual Snap

**Parent:** [`_index.md`](../_index.md)
**Tech Spec:** [`../tech-specs/01-view-snap.md`](../tech-specs/01-view-snap.md)

## Why

사용자가 트롬본을 어떤 z회전 각도로 들고 있는지 즉각 인지하기 어려워 본인이 5개
파셜 중 어디에 있는지 모른다. 트롬본 본체 mesh가 항상 5단계 anchor 각도 중 하나로만
표시되면 "지금 파셜 N에 있다 / 다음 파셜은 위·아래 어디다"가 시각적으로 명확해진다.

## What

트롬본을 잡은 동안 트롬본 본체(Rig/Body) mesh의 표시 z회전이 5개 anchor 각도
(-30°/-15°/0°/+15°/+30°) 중 PartialController가 결정한 현재 파셜 인덱스에 대응하는
값으로 고정된다.

- Rig/Body의 표시 회전만 보정한다. 입력 측 회전 각도(PartialController가 읽는 trombone
  root의 z회전)는 보정 없이 그대로 사용된다 — 입력·표시 디커플링.
- anchor 사이 전환은 짧은 ease로 부드럽게 처리되어 시각적 점프를 완화한다.
- 트롬본을 놓으면(IsAttached=false) Rig/Body 회전 보정이 해제되고 입력 그대로 보인다.

## Behavior

- **Given** 사용자가 트롬본을 잡은 상태(IsAttached=true)에서
  **When** trombone root z회전을 -25°로 두면 (PartialController가 PartialIndex=1 결정)
  **Then** Rig/Body의 표시 z회전은 -15° (파셜 1 anchor)로 수렴한다

- **Given** 사용자가 트롬본을 잡고 z회전이 -8°→+8°로 천천히 변할 때 (PartialIndex 2 유지)
  **When** PartialIndex 변화가 발생하지 않을 때
  **Then** Rig/Body 표시 z회전은 0° (파셜 2 anchor)에 고정된다 (입력 변화에도 표시는 정지)

- **Given** 사용자가 z회전을 -10°→-22°로 빠르게 움직일 때
  **When** PartialIndex가 2→1로 전환되면
  **Then** Rig/Body 표시 z회전은 0°→-15°로 짧은 ease 곡선으로 보간되어 전환된다

- **Given** 사용자가 TromboneAnchor에서 이탈해 트롬본을 놓을 때
  **When** IsAttached가 false가 될 때
  **Then** Rig/Body 회전 보정이 해제되어 입력 회전 그대로 보인다

## Out of Scope

- 파셜 결정 입력 각도 자체의 양자화 (PartialController의 hysteresis 로직은 변경 없음)
- VR 카메라/시점 강제 제어 (멀미 위험으로 명시적 제외)
- 손 grip pose와 보정된 mesh 각도 사이의 미세 어긋남 정합 (Hands 도메인 책임으로 이관)
- 5개 anchor 각도 값 변경 (`anglePerPartial=15°` 유지)
- Rig/Slide·Rig/MouthPiece·SlidePositionMarkers의 회전 보정

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
