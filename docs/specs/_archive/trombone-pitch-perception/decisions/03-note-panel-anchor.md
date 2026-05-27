# Note Panel Anchor — 5패널 시점 origin transform

**Sub-Spec:** [`../specs/02-note-vertical-layout.md`](../specs/02-note-vertical-layout.md)
**From Tech Spec:** [`../tech-specs/02-note-vertical-layout.md`](../tech-specs/02-note-vertical-layout.md) §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-27

## Context

5개 파셜 패널을 사용자 시점 pitch -30°/-15°/0°/+15°/+30°에 1:1 매핑해 수직 적층하려면
5패널의 origin (사용자 시점 기준점)을 어디로 정해야 하는가. Trombone z회전 시
패널 pitch 관계가 흔들리지 않아야 한다.

## Options Considered

- **신규 PanelAnchor child** — Trombone prefab 안 신규 child Transform (eye 높이·trombone forward 대향). 5패널이 안정 origin에 정렬. SessionPanel과도 공유 가능.
- **기존 tromboneRoot 재활용 (13-plan 채택안)** — trombone이 z회전하면 5패널도 함께 돌아 본 spec의 "pitch 1:1 매핑" 의도와 충돌 ⚠️.
- **Camera.main (사용자 머리 기준)** — 머리 움직임에 패널이 따라다녀 어지러움 ⚠️.

## Decision

**신규 PanelAnchor child (eye 높이·trombone forward 대향)** — Trombone이 z회전해도 PanelAnchor는 trombone forward에 고정되어 패널 pitch 관계가 일관되게 유지된다. SessionPanel(14-spec)도 같은 anchor를 공유 가능해 _panelAnchor 슬롯 통합 여지가 있다.

## Spec What Coverage

- 5패널이 시점 -30°/-15°/0°/+15°/+30° pitch에 1:1 매핑: **만족** (PanelAnchor가 안정 origin 제공)
- Trombone z회전 시 패널 pitch 관계 안정: **만족** (PanelAnchor가 z회전 영향 밖)
- 패널이 사용자 시점 origin을 바라봄: **만족**
- 13-plan layout 대체: **만족**

## Consequences

- Trombone.prefab에 신규 child "PanelAnchor" 추가, eye 높이·trombone forward 대향 박제. plan 단계에서 정확한 local position·rotation 결정.
- InstrumentBase._panelAnchor 슬롯과의 통합 여부는 plan 단계 결정. 13-plan은 tromboneRoot 자체를 _panelAnchor로 박았는데, 본 spec 이후 _panelAnchor도 PanelAnchor child로 옮기는 것이 자연스럽다.
- TromboneNoteDisplayAdapter.ComputePanelWorldPos는 `PanelAnchor.position + Quaternion.AngleAxis(pitch_i, PanelAnchor.right) * PanelAnchor.forward * radius` 기반으로 재정의.
- 변경 시 재평가 trigger: SessionPanel(14-spec) 위치와의 충돌, 또는 PanelAnchor 위치가 사용자 자세에 따라 부자연스러워질 경우.
