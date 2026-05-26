# Slide Tracking by Right Grip — Tech Spec

**Sub-Spec:** [`02-slide-tracking.md`](../specs/02-slide-tracking.md)
**Status:** `Draft`
**Date:** 2026-05-17

## Components

- **TromboneSlideController** (신규) — Trombone prefab 의 Slide GameObject 또는 root 에 부착. 오른손 Grip 입력 구독, baseline 박제, 매 프레임 Slide.localPosition.x 운전, min / max clamp.
- **Slide (GameObject)** (기존, prefab 자식) — 운전 대상. localPosition.x 만 변경, y / z / rotation / scale 불변.
- **GripPoseHand (Slide 자식)** (신규, 01 에서 추가됨) — Slide 의 자식이라 자동 추종.
- **Ghost right wrist transform** (기존, Hands 도메인) — Grip 입력 + 손 위치 source. AnchoredStickGhostFollower 가 사용하는 동일 source.
- **TromboneAnchor** (기존, 01 에서 신규) — attach 활성 시 TromboneSlideController 에 enable 신호, detach 시 disable.

## Data / Control Flow

- 매 프레임 TromboneSlideController 가 ghost right wrist 의 world position 을 트럼본 root 의 local 좌표로 변환 → currentLocalX.
- 오른손 Grip 누르는 순간: baselineHandX = currentLocalX, baselineSlideX = Slide.localPosition.x 박제.
- Grip 홀드 동안 매 LateUpdate: Slide.localPosition.x = Mathf.Clamp(baselineSlideX + (currentLocalX − baselineHandX), slideMinX, slideMaxX).
- Grip 떼는 순간: baseline 해제. Slide.localPosition.x 는 마지막 값 유지.
- TromboneAnchor.attach 가 false 인 동안 컴포넌트는 enabled=false → 입력 구독·런타임 업데이트 모두 정지.

## Boundaries

- **건드린다**: TromboneSlideController 신규 컴포넌트, Trombone.prefab 의 Slide GameObject 에 컴포넌트 부착, 트럼본 root 에 slideMinX / slideMaxX SerializedField 박제.
- **건드리지 않는다**: PlayHandPoseDriver, Slide 의 mesh / collider / rotation / scale, 왼손 입력, 사운드 시스템, 01 sub-spec 의 attach / detach 운전.

## Invariants

- Slide.localPosition.x 는 항상 [slideMinX, slideMaxX] 안. clamp 가 한 곳에서만 발생 (controller 의 LateUpdate).
- Slide.localPosition.(y, z), localRotation, localScale 은 본 sub-spec 에서 변경되지 않는다.
- baseline 은 Grip 누르는 순간 정확히 1회 캐시, Grip 떼는 순간까지 갱신되지 않는다 (Grip 홀드 중 손을 떼지 않고도 reset 되는 케이스 없음).
- TromboneAnchor.attach == false 동안 Slide.localPosition.x 는 어떤 입력에도 변하지 않는다.

## Assumptions

- 오른손 Ghost wrist transform 이 외부에서 주입 가능한 SerializedField 로 박제될 수 있음. DrumKitStickAnchor 의 rightGhostWristSource 가 동일 패턴. 출처: `Read DrumKitStickAnchor.cs (2026-05-17)`.
- 오른손 Grip 입력은 VR Input system 으로부터 한 곳에서 polling / event subscribe 가능. 구체 API 는 ARD 02 의 사정권 외 — 본 sub-spec 단독으로는 결정 불필요 (plan 단계에서 채택).
- Slide GameObject 의 현재 localPosition 은 prefab 박제 값이며 이 값이 본 sub-spec 의 "초기 Slide x" 가 된다. 출처: `Read Trombone.prefab (2026-05-17, Slide.m_LocalPosition = (-0.59, -0.07, 0.02))`.

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `Assets/Instruments/Drum/Scripts/AnchoredStickGhostFollower.cs` | `TromboneSlideController` | drum-stick 은 ghost wrist 의 world transform 을 그대로 stick 에 복사 (6DoF 추종). trombone 은 ghost wrist 의 local x 변화량만 추출해 Slide.localPosition.x 에 더함 (1DoF). baseline 박제 패턴이 추가. |
| `Assets/Instruments/Piano/Scripts/PianoKeyPressSensor.cs` | (해당 없음) | Piano 는 fingertip 거리 기반 press 판정 — 연속 컨트롤이지만 입력 모달리티가 완전 다름. |

## Open Tech Decisions

_현재 열린 분기 없음._
