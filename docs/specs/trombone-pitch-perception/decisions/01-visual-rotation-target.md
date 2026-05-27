# Visual Rotation Target — Hard Step 스냅 적용 transform

**Sub-Spec:** [`../specs/01-view-snap.md`](../specs/01-view-snap.md)
**From Tech Spec:** [`../tech-specs/01-view-snap.md`](../tech-specs/01-view-snap.md) §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-27

## Context

Hard step 시각 스냅을 trombone prefab의 어느 transform에 적용할지 결정해야 한다.
Rig 아래에 Body(왼손 잡힘)·Slide(오른손 잡힘)·MouthPiece가 형제로 있고 각각 손에 결합되어
있어, 회전 대상 선택에 따라 입력·표시 디커플링 정도와 prefab 골격 영향이 달라진다.

## Options Considered

- **Trombone root 자체 회전** — TrombonePartialController가 읽는 입력 transform과 표시 transform이 같아 디커플링 불가능. PartialController·hysteresis 로직 재설계 필요. spec What "입력 각도는 그대로 보존"을 만족 못 함 ⚠️.
- **Rig/Body 그룹만 회전** — Body mesh만 5단계 표시. Slide·손·MouthPiece는 입력 그대로. 입력·표시 디커플링 명확, prefab 골격 변경 없음. Body와 Slide 사이 시각적 연결이 일시 어긋날 수 있음.
- **신규 visual root child 도입** — Body+Slide+MouthPiece+NoteDisplay를 재배치한 visual root만 회전. 디커플링 깔끔하나 prefab 골격 변경 폭이 크고 grip pose 본 계층과 충돌 위험.

## Decision

**Rig/Body 그룹만 회전** — 입력/표시 디커플링이 명확하고 prefab 골격 변경이 0이며, 손·Slide의 입력 추종이 자연스럽게 유지된다. Body·Slide 사이 일시적 시각 어긋남은 ease와 PartialController의 기존 hysteresis 2°로 완화하며, 손 grip pose 정합 보정은 본 spec의 Out of Scope으로 격리되어 있다.

## Spec What Coverage

- 입력 각도 그대로 PartialController가 읽음: **만족** (Rig/Body 회전은 PartialController 입력 경로에 영향 없음)
- 트롬본 본체 mesh가 5단계 anchor 각도로 표시됨: **만족** (Rig/Body가 본체 mesh 보유)
- 손 grip pose 정합: **본 spec Out of Scope** (Hands 도메인 책임으로 이관)

## Consequences

- TromboneBodyVisualSnap은 Rig/Body Transform.localRotation의 z만 수정한다 (x, y는 원본 localRotation 유지).
- Slide·MouthPiece·SlidePositionMarkers·NoteDisplay는 Rig의 자식이고 Rig는 변경하지 않으므로 입력 회전 그대로 따라간다.
- 변경 시 재평가 trigger: prefab 골격에 신규 visual root child가 도입되거나, Body·Slide 시각 어긋남이 사용자 피드백에서 큰 문제로 보고될 경우.
