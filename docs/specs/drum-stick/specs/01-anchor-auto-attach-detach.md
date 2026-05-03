# Anchor Auto Attach/Detach

**Parent:** [`_index.md`](../_index.md)

## Why

드럼킷 anchor 정렬은 이미 텔레포트 시스템이 사용자(=리그)의 위치·각도를 anchor에 맞춰주지만, 도착 직후의 "양손이 비어 있는 상태"는 사용자의 의도(연주 시작)와 어긋난다. 사용자가 매번 두 개의 스틱을 양손에 직접 집는 단계를 끼워 넣으면 anchor가 보장한 즉시 연주 흐름이 깨진다.

또한 사용자가 그 자리를 떠나는 순간 스틱이 손에 그대로 남아 있으면, anchor 외부 공간에서 스틱이 환경과 상호작용하거나 시각적으로 떠다니는 어색한 상태가 만들어진다.

이 sub-spec은 드럼킷 anchor를 "사용자의 양손이 스틱을 가진 상태"의 단일 진실원으로 삼아, anchor 도착·이탈에 따라 스틱의 존재가 자동으로 동기화되도록 한다.

## What

- 드럼킷 anchor에 텔레포트로 도착하는 순간 양손에 동시에 스틱이 attach된다.
- 스틱이 attach된 상태에서 사용자의 grip/trigger 입력이 들어와도 스틱은 손에서 떼어지지 않는다.
- attach된 동안 손의 시각 표현은 미리 정의된 스틱-잡기 포즈를 유지한다.
- 사용자가 드럼킷 anchor 외부로 텔레포트하는 순간 양손에서 스틱이 동시에 detach되어 시각적으로 어디에도 보이지 않는다.
- 같은 드럼킷 anchor로 다시 텔레포트하면 스틱은 새로 attach된다.

## Behavior

- **Given** 사용자가 드럼킷 anchor 외부에 있고 양손에 스틱이 없다.
  **When** 사용자가 텔레포트로 드럼킷 anchor에 도착한다.
  **Then** 도착 직후 양손에 동시에 스틱이 들려 있고, 손은 스틱-잡기 포즈를 유지한다.

- **Given** 사용자의 양손에 스틱이 attach되어 있다.
  **When** 사용자가 grip 또는 trigger 입력으로 손을 펴거나 던지려 한다.
  **Then** 스틱은 손에서 떨어지지 않으며, 손 포즈도 변하지 않는다.

- **Given** 사용자가 드럼킷 anchor에서 양손에 스틱을 든 상태다.
  **When** 사용자가 드럼킷 anchor 외부의 다른 위치로 텔레포트한다.
  **Then** 텔레포트가 확정되는 그 순간 양손에서 스틱이 동시에 사라지며, 사라진 스틱은 어디에도 보이지 않는다.

- **Given** 사용자가 한 번 드럼킷 anchor를 떠나 스틱이 detach된 상태다.
  **When** 사용자가 다시 같은 드럼킷 anchor로 텔레포트해 도착한다.
  **Then** 도착 직후 양손에 스틱이 새로 attach된다.

## Out of Scope

- 스틱으로 드럼을 쳤을 때의 통과 방지·멈춤 동작 → [`02-stick-no-penetration.md`](02-stick-no-penetration.md).
- 텔레포트 라인이 anchor를 가리킬 때의 시각 표현·anchor 정렬 → 이미 박제·구현됨: [`_archive/teleport-locomotion/specs/03-instrument-anchors.md`](../../../_archive/teleport-locomotion/specs/03-instrument-anchors.md).
- 손 자체의 3-hand(Ghost/Physics/Play) 구조와 손-환경 통과 방지 → [`hands/_index.md`](../../hands/_index.md), [`hands/specs/02-instrument-no-penetration.md`](../../hands/specs/02-instrument-no-penetration.md).
- 드럼 외 다른 악기(piano 등)의 도구 자동 부착 — 본 sub-spec은 드럼킷 anchor만 다룬다.
- 사용자가 anchor 영역을 텔레포트가 아닌 방식(걷기, 그립이동 등)으로 빠져나가는 시나리오 — 본 피처의 detach 트리거는 텔레포트 확정뿐이다.
- 드럼 노트 인식 로직(타격 깊이/속도에서 노트가 트리거되는지) — 본 sub-spec은 스틱의 부착·해제와 손 포즈만 책임진다.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-02 | Anchor Auto Attach/Detach | Done | [2026-05-02-sanyoentertain-anchor-auto-attach-detach.md](../../_archive/drum-stick/plans/2026-05-02-sanyoentertain-anchor-auto-attach-detach.md) |
| 2026-05-03 | Stick GripPoseHand 정렬 역산 | Done | [2026-05-03-sanyoentertain-stick-gripposehand-alignment.md](../../_archive/drum-stick/plans/2026-05-03-sanyoentertain-stick-gripposehand-alignment.md) |
| 2026-05-03 | DrumKitAnchor Forward Rotation 보정 | Done | [2026-05-03-sanyoentertain-drum-anchor-forward-rotation.md](../../_archive/drum-stick/plans/2026-05-03-sanyoentertain-drum-anchor-forward-rotation.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
