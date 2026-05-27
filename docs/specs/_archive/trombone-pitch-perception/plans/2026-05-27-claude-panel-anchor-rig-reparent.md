# Panel Anchor — Trombone root → Rig 자식으로 reparent (attach 시 사용자 mouth 추적)

**Linked Spec:** [`../specs/02-note-vertical-layout.md`](../specs/02-note-vertical-layout.md)
**Caused By:** [`2026-05-27-claude-note-pitch-panel-anchor-stack.md`](./2026-05-27-claude-note-pitch-panel-anchor-stack.md)
**Status:** `Done`

## Goal

선행 plan(`2026-05-27-claude-note-pitch-panel-anchor-stack.md`)에서 박제된 PanelAnchor의 parent 결정(Trombone root 직속)을 **Rig 자식(Body 형제)** 으로 정정한다. `TromboneAnchor.tromboneRoot` SerializeField가 실제로는 **Rig**를 가리키며 attach 시 Rig만 사용자 mouth로 이동하므로, root 직속에 둔 PanelAnchor는 사용자가 트롬본을 잡아도 따라오지 않고 원위치(scene placement 위치)에 남아 5패널이 사용자에서 멀리(약 3m 우측 사선) 표시되는 문제를 해소한다.

## Context

선행 plan은 sub-spec 01-plan handoff("Trombone/Rig 형제 또는 Trombone root 직속에 두면 Body z회전 보정과 독립")를 근거로 PanelAnchor를 Trombone root 직속으로 박제했다. 이 결정은 **Body 회전과 독립** 조건은 충족하나, **attach 시 사용자 mouth 추적** 조건을 놓쳤다.

> **선행 plan 결정 (수정 대상)**: PanelAnchor를 Trombone root 직속 child로 추가 (Rig/TromboneAnchor/TromboneNoteDisplay/RhythmGameHost 형제).
> **실패 발견 시점**: 사용자 manual 검증 (실제 PlayMode + `TromboneAnchor.AttachTromboneToMouth()` reflection 호출 + Game View 1인칭 캡처).
> **Evidence (PlayMode 자동 측정)**:
> - Trombone root pos = (2.711, 0.817, 0.888) — 무변경
> - Rig pos = (-0.050, 1.134, 0.043) — attach 시 mouth (0.000, 1.068, 0.061)로 이동
> - PanelAnchor pos = (2.711, 0.817, 0.888) — **root 따라 원위치 유지, mouth로 따라오지 않음**
> - 5패널 → 사용자 거리 3.18~3.22m (의도된 0.8m × 4배)
> - 5패널 yaw +58~60° (우측 사선), pitch 적층 폭 14.4° (의도된 60°의 24%)
> - 사용자가 트롬본을 잡고 정면을 봐도 5패널은 시야 우측 멀리 작은 점들로 보임

**원인 분석**: `TromboneAnchor.tromboneRoot` SerializeField는 Trombone root가 아니라 **`Trombone/Rig`** Transform을 가리킨다 (prefab 직렬화 확인). `AttachTromboneToMouth()`는 이 `tromboneRoot`(=Rig) Transform을 mouth + mouthpiece offset 위치로 `SetPositionAndRotation` 한다. Trombone root 자체와 그 직속 child(=PanelAnchor)는 attach 영향 밖.

**올바른 PanelAnchor 위치**: Rig 자식. Body는 Rig의 자식이고 [`TromboneBodyVisualSnap`](../../../../Assets/Instruments/Trombone/Scripts/TromboneBodyVisualSnap.cs)이 Body의 localRotation.z만 변경하므로 **Body의 형제(=Rig의 다른 자식)는 Body 회전 영향 밖**. 따라서 Rig 자식(Body 형제)에 두면:
- ✓ attach 시 Rig 따라 mouth로 이동 (5패널이 사용자 정면 0.8m에 위치)
- ✓ Body의 z회전 보정 영향 받지 않음 (sub-spec 01의 Out of Scope 유지)
- ✓ Slide/MouthPiece/NoteDisplay/SlidePositionMarkers 형제 위치 (Rig의 자식들)

## Verified Structural Assumptions

- **`TromboneAnchor.tromboneRoot` SerializeField** — 실제 reflection 결과: `name = Rig` (Trombone root가 아닌 `Trombone/Rig` Transform). 출처: PlayMode reflection `typeof(TromboneAnchor).GetField("tromboneRoot", NonPublic|Instance).GetValue(anchor)` (2026-05-27).
- **`AttachTromboneToMouth()` 동작** — `tromboneRoot.SetPositionAndRotation(targetPosition, targetRotation)` 호출로 Rig 본체만 mouth로 이동. Trombone root와 그 직속 child(PanelAnchor, TromboneAnchor, TromboneNoteDisplay, RhythmGameHost)는 영향 밖. 출처: Read `Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs:202` (2026-05-27).
- **`TromboneBodyVisualSnap` 영향 범위** — Body Transform의 localEulerAngles.z만 set. Body의 형제(Slide/MouthPiece/NoteDisplay/SlidePositionMarkers)와 부모(Rig)는 무변경. 출처: sub-spec 01-plan `2026-05-27-claude-body-visual-snap.md` Handoff + Read `TromboneBodyVisualSnap.cs` (2026-05-27).
- **Trombone.prefab Transform fileID 식별** —
  - Trombone root Transform: `&6792978486195072924`, m_Children에 PanelAnchor fileID 포함됨 (변경 대상).
  - Rig Transform: `&9100000000000000002`, m_Children에 Body/Slide/MouthPiece/NoteDisplay/SlidePositionMarkers 포함 (PanelAnchor 추가 대상).
  - PanelAnchor Transform: `&5307817778198693025`, m_Father=Trombone root (변경 대상).
  - 출처: Grep `m_Name:|m_Father:` on `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` (2026-05-27).

## Approach

`Trombone.prefab` YAML 3군데 수정 (단순 reparenting):

1. **PanelAnchor Transform `m_Father` 변경**: `{fileID: 6792978486195072924}` (Trombone root) → `{fileID: 9100000000000000002}` (Rig).
2. **Trombone root Transform `m_Children` 배열에서 PanelAnchor 제거**: `- {fileID: 5307817778198693025}` 라인 삭제 (Trombone root의 m_Children 5→4건).
3. **Rig Transform `m_Children` 배열에 PanelAnchor 추가**: `- {fileID: 5307817778198693025}` 라인 추가 (Rig의 m_Children 5→6건).

코드/asmdef/SerializeField wiring 일절 무변경. PanelAnchor의 localPosition/Rotation/Scale 모두 그대로(=identity, 0,0,0) — Rig의 origin이 trombone root와 동일(localPos 0)이므로 reparent 후에도 world 위치는 동일.

## Deliverables

- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` (수정 — 3줄: m_Father 변경 + 두 m_Children 배열 갱신).

## Acceptance Criteria

- [x] `[auto-hard]` Trombone.prefab의 PanelAnchor path가 `Trombone/Rig/PanelAnchor`로 변경된다 (이전 `Trombone/PanelAnchor`에서 이동).
  **검증:** Unity MCP `manage_prefabs.get_hierarchy` 결과의 `Trombone/Rig/PanelAnchor` path 항목 1건 존재 + `Trombone/PanelAnchor` (root 직속) 항목 0건. *[2026-05-27 확인: `Trombone/Rig/PanelAnchor` 1건, Rig childCount 5→6]*

- [x] `[auto-hard]` PanelAnchor Transform의 m_Father가 Rig fileID(`9100000000000000002`)를 가리킨다.
  **검증:** `Grep -A 10 "m_Name: PanelAnchor" Assets/Instruments/Trombone/Prefabs/Trombone.prefab`의 m_Father 라인 = `{fileID: 9100000000000000002}`. *[2026-05-27 확인]*

- [x] `[auto-hard]` 선행 plan invariant 보존 — (a) `_panelAnchor:`와 `centerAnchor:` 슬롯이 PanelAnchor Transform fileID `5307817778198693025` 그대로 가리킴, (b) `tromboneRoot:` 슬롯은 Rig fileID `9100000000000000002` 그대로.
  **검증:** prefab YAML diff에서 (a) `_panelAnchor: {fileID: 5307817778198693025}` / `centerAnchor: {fileID: 5307817778198693025}` 무변경, (b) `tromboneRoot: {fileID: 9100000000000000002}` 무변경. *[2026-05-27 확인]*

- [x] `[auto-soft]` 컴파일 후 console errors 0건 (prefab YAML 수정은 컴파일에 영향 없음).
  **검증:** Unity MCP `read_console types=["error"]` → 0건. *[2026-05-27 확인]*

- [x] `[manual-hard]` 실제 PlayMode에서 `TromboneAnchor.AttachTromboneToMouth()` 호출 후 PanelAnchor world position이 Rig.position과 동일하게 사용자 mouth 근처로 이동한다. 5패널이 사용자 시점에서 우측 yaw ~+86°, pitch -30°~+30° 폭으로 적층되고 거리 ~0.8m. *[2026-05-27 PlayMode 자동 검증: Rig (-0.05, 1.13, 0.04), PanelAnchor (-0.05, 1.13, 0.04) 동일, 5패널 yaw +86°, pitch -30.8°~+32.9° (폭 63.7°), dist 0.75~0.77m. spec ±30° 의도 충족]*
  **검증:** PlayMode reflection으로 `AttachTromboneToMouth()` 호출 + `RhythmGameHost.StartSession` + 5패널 world pos → camera local 분해 측정.

- [x] `[manual-hard]` 선행 plan `2026-05-27-claude-note-pitch-panel-anchor-stack.md`의 What 조항 "사용자 시점 pitch -30°/-15°/0°/+15°/+30°에 1:1 매핑, 트롬본 z회전 시 PanelAnchor 자세 불변" 이 이 plan 적용 후 재검증에서 통과한다.
  **검증:** 위 manual-hard와 동일 시나리오 + Rig 회전 변경 시 PanelAnchor world rotation 추적. *[2026-05-27 PlayMode 자동 검증 통과 — 5패널 pitch 폭 63.7° (spec 의도 60°), 거리 0.77m (spec 의도 0.8m)]*

## Out of Scope

- PanelAnchor의 localPosition 미세 조정 (현재 identity (0,0,0) — Rig 자식이므로 Rig.position과 동일 world pos. 부자연스러우면 별도 plan).
- TromboneAnchor.tromboneRoot SerializeField 변경 (Tech Spec Boundaries).
- ARD 03 식 / ComputePanelWorldPos 부호 (선행 후속 plan에서 박제됨).
- NoteDisplayPanel localScale (laneHeightMeters / panelScrollLengthMeters) — 패널이 시각적으로 작게 보이는 경우 별도 plan 후보.
- Body의 z회전이 PanelAnchor(=Body 형제)에 영향 없음 확인 — `TromboneBodyVisualSnap`이 Body Transform만 set하고 Rig 자체는 무변경이므로 자명. AC로 박제하지 않음.

## Notes

- **선행 plan handoff 가이드의 한계**: sub-spec 01-plan handoff "Trombone/Rig 형제 또는 Trombone root 직속" 가이드가 Body 회전 독립 조건만 고려했고 attach 시 mouth 추적은 누락. 본 plan은 그 가이드를 정정 — **Rig 자식(Body 형제)** 이 두 조건을 모두 만족하는 유일한 위치.
- **Reparent로 world 위치 보존**: PanelAnchor.localPosition이 (0,0,0)이고 Rig.localPosition도 (0,0,0)이므로 reparent 전후 PanelAnchor world pos는 Trombone root와 동일. attach 후에는 Rig가 mouth로 이동하면서 PanelAnchor world pos도 자동 추적.
- **`TromboneBodyVisualSnap`과의 독립성**: Body Transform의 localEulerAngles.z만 set하는 컴포넌트라 Body의 형제(=Rig의 다른 자식)는 영향 받지 않음. 본 plan reparent 후에도 sub-spec 01의 Boundaries 유지.

## Handoff

- **PanelAnchor 최종 path**: `Trombone/Rig/PanelAnchor` (Body, Slide, MouthPiece, NoteDisplay, SlidePositionMarkers의 형제).
- **선행 plan invariant 모두 보존**: SerializeField wiring(`_panelAnchor`/`centerAnchor` → PanelAnchor fileID `5307817778198693025`), `tromboneRoot` → Rig fileID `9100000000000000002`, ComputePanelWorldPos 부호 반전 식, 카메라 추적 제거, rhythm-game/13 supersede 메모.
- **검증된 사용자 시점 정합** (PlayMode `AttachTromboneToMouth()` 후):
  - 5패널 사용자 우측 (yaw ~+86°)
  - pitch -30.8° ~ +32.9° (폭 63.7° ≈ spec 의도 60°)
  - 거리 0.75~0.77m ≈ panelRadius 0.8m
  - i=0(가장 낮은 음) 시야 가장 아래, i=4(가장 높은 음) 시야 가장 위 (부호 반전 정합 유지)
- **후속 plan 후보**: (a) NoteDisplayPanel localScale 튜닝 — 현재 패널이 캡처에서 작게 보임 (laneHeightMeters / panelScrollLengthMeters 값 점검), (b) PanelAnchor.localPosition 미세 조정으로 패널을 사용자 정면에 가깝게(yaw 86° 대신 yaw 0°에 가깝게), (c) SessionPanel(14-spec) 위치 — 통합 결정에 따라 SessionPanel도 Rig 자식 PanelAnchor로 따라옴, 시각 충돌 확인.
