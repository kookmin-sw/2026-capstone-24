# Instrument Anchors

**Parent:** [`_index.md`](../_index.md)

## Why

이 피처의 핵심 동기 중 하나는 "악기 위치 정확도"다. 피아노·드럼처럼 사용자의 몸과 악기 사이의 정확한 위치·각도가 연주감을 결정하는 고정형 악기에서는, 사용자가 텔레포트 라인을 정확한 자리에 정확한 방향으로 정렬하는 것에 매번 인지 자원을 쓰면 안 된다.

[`01-base-teleport.md`](../../_archive/teleport-locomotion/specs/01-base-teleport.md)의 기본 텔레포트는 "라인이 가리키는 그 자리"로 보낸다. 하지만 악기 앞이라는 맥락에서는, 사용자가 의도하는 결과는 "이 악기를 연주할 수 있는 정확한 자세"이지 "라인 끝점"이 아니다. 따라서 악기 근처에서는 라인 끝점을 미리 정의된 자세로 **고정**하는 것이 사용자의 의도와 더 일치한다.

## What

- 피아노·드럼처럼 위치가 고정된 악기는 **악기당 단일 텔레포트 anchor**를 가진다. anchor는 사용자가 그 악기를 연주할 때의 **위치 + 향하는 각도** 를 정의한다.
- 텔레포트 라인의 끝점이 어떤 악기의 **anchor 반경 안에 들어오면**, 라인의 시각 표현이 일반 텔레포트와는 **구별되는 형태**로 전환된다(예: 색·끝점 마커가 anchor를 가리키는 형태로 변경).
- 그 상태에서 사용자가 텔레포트를 확정하면, 라인 끝의 *실제 좌표*가 어디였는지에 관계없이 사용자는 **anchor의 위치·각도로 정확히** 텔레포트된다.
- 라인 끝이 어떤 악기의 anchor 반경 밖에 있으면, 텔레포트는 [`01-base-teleport.md`](../../_archive/teleport-locomotion/specs/01-base-teleport.md)의 기본 동작 그대로다.

## Behavior

- **Given** 사용자가 텔레포트 라인을 표시 중이고, 라인 끝이 어떤 악기의 anchor 반경 밖에 있다
  **When** 라인 끝이 그 악기의 anchor 반경 안으로 들어온다
  **Then** 라인의 시각 표현이 anchor용으로 구별되는 형태로 즉시 전환되며, 끝점은 anchor 자리를 가리키는 것으로 보인다.

- **Given** 라인 끝이 어떤 악기의 anchor 반경 안에 있고 anchor 표현이 적용되어 있다
  **When** 라인 끝이 그 반경 밖으로 다시 나간다
  **Then** 라인의 시각 표현은 일반 텔레포트 표현으로 즉시 복구된다.

- **Given** 라인 끝이 어떤 악기의 anchor 반경 안에 있다
  **When** 사용자가 텔레포트 확정 입력을 한다
  **Then** 사용자는 라인 끝의 실제 좌표와 무관하게 그 악기 anchor의 위치·각도로 텔레포트된다.

- **Given** 라인 끝이 모든 악기의 anchor 반경 밖에 있다
  **When** 사용자가 텔레포트 확정 입력을 한다
  **Then** 텔레포트는 [`01-base-teleport.md`](../../_archive/teleport-locomotion/specs/01-base-teleport.md)의 기본 규칙(가리킨 자리로 이동 또는 invalid 시 무시)을 그대로 따른다.

- **Given** 두 악기의 anchor 반경이 사용 시점에 겹치는 일이 *없도록* 씬이 구성되었다(설계 가정)
  **When** 라인 끝이 한 악기의 반경 안에 있다
  **Then** 그 악기의 anchor가 모호함 없이 선택된다.

## Out of Scope

- 텔레포트 라인 자체의 도입과 기본 동작 — [`01-base-teleport.md`](../../_archive/teleport-locomotion/specs/01-base-teleport.md)의 책임.
- 라인이 노 텔레포트 존을 가리킬 때의 invalid 표현 — [`02-no-teleport-zones.md`](02-no-teleport-zones.md)의 책임. 본 sub-spec은 anchor 표현이 invalid 표현과 시각적으로 혼동되지 않도록 한다는 정도까지만 고려한다.
- 한 악기에 여러 anchor를 두거나 후보 anchor 중 사용자가 선택하는 형태. 본 sub-spec은 **악기당 단일 anchor**만 다룬다.
- 이동형 / 휴대형 악기. anchor는 *고정형* 악기에만 정의된다.
- anchor에 도착한 사용자의 손 자세를 악기 연주에 맞춰 추가 보정하는 동작. 본 sub-spec은 사용자(=리그)의 위치·각도까지만 책임진다.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-30 | Instrument Anchors via TeleportationAnchor | Ready | [2026-04-30-sanyoentertain-instrument-anchors-via-teleportation-anchor.md](../plans/2026-04-30-sanyoentertain-instrument-anchors-via-teleportation-anchor.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

- [ ] anchor 반경의 기본 크기·시각 표현(반경 가시화 여부)은 plan 또는 시각 작업에서 결정한다.
- [ ] 향후 한 악기에 여러 anchor를 두는 요구가 생길 경우 본 sub-spec을 확장할지, 별도 sub-spec으로 분리할지.
