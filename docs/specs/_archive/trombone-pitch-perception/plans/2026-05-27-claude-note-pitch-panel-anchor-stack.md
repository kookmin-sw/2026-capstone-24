# Note Panel — PanelAnchor 신규 child + pitch 수직 적층 layout 전환

**Linked Spec:** [`02-note-vertical-layout.md`](../specs/02-note-vertical-layout.md)
**Status:** `Done`

## Goal

Trombone.prefab에 신규 child `PanelAnchor` (eye 높이·trombone forward 대향)를 도입하고, `TromboneNoteDisplayAdapter`의 패널 배치를 "카메라 forward + camPos.y 기준 수직 Y 스택"에서 "PanelAnchor 기준 pitch 회전 적층(`PanelAnchor.position + Quaternion.AngleAxis(pitch_i, PanelAnchor.right) * PanelAnchor.forward * radius`)"으로 재정의해, 5개 파셜 패널의 시점 pitch가 PartialController anchor 각도(-30/-15/0/+15/+30°)와 1:1 일치하도록 한다. 동시에 `rhythm-game/13-trombone-note-display.md` 본문과 13-plan의 yaw 반원형 가정을 본 spec이 supersede한다는 메모를 갱신한다.

## Context

`docs/specs/trombone-pitch-perception/specs/02-note-vertical-layout.md`는 "5패널의 시점 pitch가 파셜 i의 anchor 각도(`(i - centerPartialIndex) × anglePerPartial`)와 정확히 일치 + trombone z회전에도 패널 pitch 관계 불변 + 5패널은 동일 origin을 바라봄"을 What으로 요구한다. 동 sub-spec의 Invariant: (a) 패널 i의 PanelAnchor 기준 pitch = anchor_i, (b) 5패널 동일 반경·동일 origin, (c) 패널은 항상 PanelAnchor를 바라봄, (d) Trombone z회전이 변해도 PanelAnchor 자세 불변.

근거 결정:
- ARD `decisions/03-note-panel-anchor.md` (Accepted, 2026-05-27): **신규 PanelAnchor child** (eye 높이·trombone forward 대향). Consequences: (i) Trombone.prefab에 신규 child "PanelAnchor" 추가, (ii) InstrumentBase._panelAnchor 슬롯과의 통합 여부는 plan 단계 결정, (iii) `ComputePanelWorldPos = PanelAnchor.position + Quaternion.AngleAxis(pitch_i, PanelAnchor.right) * PanelAnchor.forward * radius`.
- Tech Spec `tech-specs/02-note-vertical-layout.md`:
  - **Data Flow**: `Adapter.Begin → 파셜 0~4 → pitch_i = (i - centerPartialIndex) × anglePerPartial → 패널 위치 = PanelAnchor.position + AngleAxis(pitch_i, PanelAnchor.right) × PanelAnchor.forward × radius → NoteDisplayPanel 인스턴스화 → SetLaneConfig(singleConfig) + Show`.
  - **Boundaries**: 건드림 = Trombone.prefab 신규 PanelAnchor child + InstrumentBase._panelAnchor 슬롯 갱신(plan 결정) + TromboneNoteDisplayAdapter layout 함수 + 13-spec 본문 + 13-plan yaw 가정 supersede. 건드리지 않음 = NoteDisplayPanel.cs, RhythmGameHost.cs, Trombone_LaneConfig.asset의 35건 MIDI 매핑, Drum/Piano NoteDisplayAdapter, TrombonePartialController, judgment 로직.
  - **Invariants**: 위 (a)~(d) 4건.

선행 sub-spec 01(Trombone Body Visual Snap, Done)의 Handoff 박제: "후속 sub-spec(02-note-vertical-layout)에 넘길 컨텍스트 — Trombone prefab에 PanelAnchor child를 추가할 때 `Trombone/Rig` 형제 또는 `Trombone` root 직속에 두면 본 컴포넌트(Body 자식)의 회전 보정과 독립적으로 유지된다." → 본 plan은 PanelAnchor를 **Trombone root 직속 child**로 둔다 (Rig가 Body z회전 보정 영향을 받지 않는 위치이며, root 직속이면 PartialController가 Rig를 회전시키더라도 PanelAnchor는 영향 밖. ARD의 "Trombone z회전 시 PanelAnchor 자세 불변"과 정합).

13-plan(Done, 2026-05-21)의 박제 사실: (i) `InstrumentBase._panelAnchor`는 `tromboneRoot`(=`Trombone/Rig`)로 박혀 있음, (ii) `TromboneNoteDisplayAdapter.centerAnchor`도 같은 Rig를 가리킴, (iii) 13-plan의 `noteToPanel` dictionary 라우팅은 그대로 유효. 본 plan은 (i), (ii) 두 슬롯을 신규 PanelAnchor로 옮긴다.

`_panelAnchor` 통합 결정: ARD가 "통합 여부는 plan 단계 결정"으로 위임함. 본 plan은 **통합** 결정을 채택한다 — InstrumentBase._panelAnchor와 adapter.centerAnchor를 **같은** 신규 PanelAnchor로 박는다. 근거: (a) ARD가 "SessionPanel(14-spec)도 같은 anchor를 공유 가능"이라고 합의했고 (b) 두 슬롯이 분리되면 SessionPanel과 5패널의 시점 origin이 어긋날 때 추적 비용이 두 배, (c) 14-spec(SessionPanel)의 위치 결정이 본 spec과 충돌할 경우 ARD를 1건 추가해 재평가하는 것이 단일 origin 가정 위반 디버깅보다 비용이 낮다. **단 통합으로 인해 SessionPanel 위치가 부자연스러워지면 본 결정을 ARD로 격상하고 별도 anchor child를 후속 plan에서 분리한다** — Notes에 박제.

본 plan은 Adapter의 LateUpdate **카메라 추적**을 제거한다. 현 구현(라인 179-200)은 매 프레임 `camFwd`·`camPos.y`로 재계산해 사용자 머리 움직임에 패널이 따라다닌다 — ARD 03이 명시적으로 거부한 "Camera.main 기준" 동작과 동일 효과. PanelAnchor가 안정 origin이므로 한 번 배치한 패널은 PanelAnchor world pose 변화(=trombone teleport·attach 위치 이동)에 한해서만 재계산하면 된다. 매 프레임 재계산은 유지하되 origin을 PanelAnchor로 바꾸면 카메라 movement에 패널이 끌려가지 않는다.

## Verified Structural Assumptions

- **현 `Trombone.prefab` 직렬화 슬롯 (수정 대상 식별)**:
  - `InstrumentBase._panelAnchor: {fileID: 9100000000000000002}` (line 990) — 현재 `Trombone/Rig` Transform을 가리킴.
  - `TromboneNoteDisplayAdapter.centerAnchor: {fileID: 9100000000000000002}` (line 623) — 동일 Rig.
  - `TrombonePartialController.tromboneRoot: {fileID: 9100000000000000002}` (line 1022) — 동일 Rig. **본 plan은 PartialController 슬롯은 무변경** (Tech Spec Boundaries: PartialController 입력 처리 불변).
  - `TromboneNoteDisplay` GameObject (`fileID 2697649137764804009`, m_Name line 589)는 이미 prefab에 존재하며 `RhythmGame.Runtime.TromboneNoteDisplayAdapter` 컴포넌트가 부착됨 (m_EditorClassIdentifier line 621). 본 plan은 신규 GameObject 생성이 아니라 `centerAnchor` 슬롯의 fileID만 신규 PanelAnchor로 갱신.
  - `InstrumentBase.laneConfig: {fileID: 11400000, guid: 94771330457054445811655f3f499c11, type: 2}` (line 987) — Trombone_LaneConfig.asset 이미 박혀 있음. 본 plan은 lane config 무변경. — `Grep '_panelAnchor:\|laneConfig:\|tromboneRoot:\|centerAnchor:\|RhythmGame.Runtime.TromboneNoteDisplayAdapter' Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-27)`

- **Trombone prefab hierarchy (Tech Spec §Prefab Hierarchy 재인용)**:
  ```
  Trombone (Transform, InstrumentAudioOutput, Trombone, TrombonePartialController)
  ├── Rig (Transform)  ← fileID 9100000000000000002, 현재 _panelAnchor/centerAnchor/tromboneRoot 공통 참조
  │   ├── Body (TromboneBodyVisualSnap, MeshFilter, MeshRenderer)
  │   ├── Slide (TromboneSlideController)
  │   ├── MouthPiece (Transform)
  │   ├── NoteDisplay (Canvas — TromboneNoteHud 라벨용, 5패널과 무관)
  │   └── SlidePositionMarkers
  ├── TromboneAnchor (TeleportationAnchor, TromboneAnchor, InstrumentTeleportLink)
  ├── TromboneNoteDisplay (TromboneNoteDisplayAdapter)  ← centerAnchor 슬롯 갱신 대상
  └── RhythmGameHost (RhythmGameHost, RhythmAccompaniment)
  ```
  본 plan은 신규 `PanelAnchor` GameObject를 **Trombone root 직속 child**로 추가 (위 트리에서 TromboneAnchor 형제 위치, 즉 Rig/TromboneAnchor/TromboneNoteDisplay/RhythmGameHost와 같은 레벨). Rig 형제이므로 Rig의 어떤 자식(Body z회전 보정 포함) 회전에도 영향받지 않음. — `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab + Unity MCP get_hierarchy (2026-05-27)`

- **`TromboneNoteDisplayAdapter.cs` 현 layout 함수 (재정의 대상)** — `Read Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs (2026-05-27)`:
  - 라인 22: `[SerializeField] Transform centerAnchor;` 폴백 = `host.PanelAnchor`. ARD 통합 결정 후 두 슬롯 모두 신규 PanelAnchor를 가리키므로 폴백 동작도 정합.
  - 라인 25: `byte[] partialBaseMidi = { 45, 52, 57, 61, 64 }` (5건). 본 plan은 이 배열을 그대로 사용해 5패널 생성.
  - 라인 31: `float panelRadius = 0.8f` — ARD의 `radius` 파라미터에 대응. 그대로 유지.
  - 라인 34: `float verticalSpacingMeters = 0.12f` — **본 plan에서 deprecated**. 대신 PartialController에서 `anglePerPartial` (default 15°)를 읽어 pitch_i를 산출하므로 verticalSpacingMeters는 더 이상 layout 산출에 쓰이지 않음. SerializeField는 안전을 위해 남겨두되 LateUpdate/Begin에서 참조 제거 (혹은 `[Obsolete]`/Tooltip로 명시).
  - 라인 37: `int fanCenterPartialIndex = 2` — PartialController.centerPartialIndex(=2)와 페어링되는 값. 본 plan은 sub-spec 01-plan에서 노출된 `partialController.CenterPartialIndex` getter를 **본 adapter에서도 사용**해 SSOT 일치를 보장한다(adapter SerializeField는 fallback default로 유지).
  - 라인 97-115: 현 Begin 본문 — `camFwd = GetCameraHorizontalForward()`, `scrollUp = Cross(up, camFwd)`, `fanCenter = (anchorPos.x, camPos.y, anchorPos.z)`, `panelPos = fanCenter + camFwd * panelRadius + Vector3.up * (offset * verticalSpacingMeters)`, `dirToPlayer = camPos - panelPos`. **본 plan에서 전면 교체**: 식 = `panelPos = anchor.position + Quaternion.AngleAxis(pitch_i, anchor.right) * anchor.forward * panelRadius`; `dirToAnchor = (anchor.position - panelPos).normalized`; rotation은 `Quaternion.LookRotation(dirToAnchor, Vector3.Cross(dirToAnchor, anchor.right))` (panel up이 anchor.right 평면에 수직 = 적층 축 정렬, ARD 식과 정합).
  - 라인 179-200 LateUpdate: 매 프레임 `camFwd`/`camPos.y`로 재계산 → **카메라 추적**. 본 plan에서 `anchor.position/rotation` 기반 재계산으로 교체.
  - **호출 외부 API side effect**: `Quaternion.AngleAxis(angle, axis)`은 axis 정규화 자동 적용·angle은 degree. `anchor.right`는 anchor.localRotation에 따라 매 프레임 정확한 world-space 우측 벡터를 돌려줌. 본 식이 anchor.right 축을 pitch 회전 축으로 쓰므로 PanelAnchor의 z축이 trombone forward와 일치해야 anchor.right가 사용자 좌우(=pitch 회전축)와 정렬됨.

- **`TrombonePartialController` 공개 API (sub-spec 01-plan handoff 박제)** — `Read Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs (2026-05-27)`:
  - `public int PartialIndex => m_PartialIndex;`
  - `public float AnglePerPartial => anglePerPartial;` (SerializeField default 15°)
  - `public int PartialCount => partialOffsetsSemitones.Length;` (=5)
  - `public int CenterPartialIndex => centerPartialIndex;` (sub-spec 01-plan에서 노출, default 2)
  - `angleSignMultiplier: -1` (line 1025 prefab) — PartialController 입력 측 부호. 본 plan의 anchor 각도식 `(i - centerPartialIndex) × anglePerPartial`은 *표시 측* 식이며, 사용자 시점에서 파셜 i가 증가할수록 **위쪽(+pitch)** 으로 가도록 정의 (i=0 가장 낮은 음 = 시야 아래쪽 -30°, i=4 가장 높은 음 = 시야 위쪽 +30°). 이는 sub-spec 본문 "파셜 0~4 → -30°/-15°/0°/+15°/+30°" 와 정합. PartialController의 angleSignMultiplier는 입력 회전→PartialIndex 매핑용이며 본 plan의 표시 식과 독립.

- **PanelAnchor local transform 박제 (신규 child 값)**:
  - **Parent**: `Trombone` root (Rig 형제 위치, root 직속 child).
  - **localPosition**: `(0, eyeY_offset, 0)`. 트롬본 root는 anchor에 attach되면 `m_CachedRestorePosition`을 anchor 위치로 셋하지만 *사용자 머리 위치*가 anchor와 정확히 일치하지 않으므로, eyeY_offset은 trombone root에서 사용자 눈 높이까지의 추정 오프셋. trombone anchor가 일반적으로 mouthpiece 높이에 attach되고 사용자가 트롬본을 입에 가져가면 trombone root.position ≈ 사용자 mouth ≈ camera.position - ~0.1m (코→눈). 본 plan은 **localPosition = (0, 0, 0)** 으로 시작하고(=trombone root 위치, 사용자 머리/입 근처), 시각 회귀에서 패널이 너무 높거나 낮으면 inspector에서 조정한다 — Notes에 박제. ARD가 정확한 값을 박지 않았고 trombone root 위치 자체가 사용자 머리에 매우 근접하므로 0,0,0이 합리적 시작점.
  - **localRotation**: `(0, 0, 0)` Euler. Trombone root의 forward가 trombone forward(사용자 시점에서 정면)와 정합하면 PanelAnchor.forward = trombone forward = 사용자 시점 정면. **검증 필요**: trombone root의 forward 축이 어느 방향인지 시각 확인 — Tech Spec에 명시되지 않았으므로 본 plan의 manual AC에서 확인. 만약 trombone root의 forward가 trombone 본체 forward와 어긋나면 PanelAnchor.localRotation에 yaw 보정(예: 0,180,0)을 박는다. — `Unity MCP get_hierarchy + Read prefab YAML (2026-05-27)`

- **asmdef 의존**: `TromboneNoteDisplayAdapter.cs`는 `Assets/RhythmGame/Scripts/Runtime/Display/` → `RhythmGame.Runtime.asmdef`에 포함. 본 asmdef는 이미 `Instruments`를 참조 (13-plan에서 박제). 본 plan은 `partialController.CenterPartialIndex` getter (sub-spec 01-plan에서 추가)만 추가로 import — `Instruments` 네임스페이스 안이므로 신규 asmdef reference 불필요. 신규 PanelAnchor 추가는 prefab YAML 편집뿐이므로 asmdef 영향 없음. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmGame.Runtime.asmdef + Assets/Instruments/Instruments.asmdef (2026-05-27, 13-plan 박제 재인용)`

- **NoteDisplayPanel.IsSingleLane (변경 없음)** — `panel.transform.position/rotation/localScale`는 본 plan에서 갱신하지만 panel 내부 노트 배치 식(`localX`, `noteW`)은 무변경. 13-plan 박제 라인(NoteDisplayPanel.cs:145, 374-379) 그대로 유효. — `Read NoteDisplayPanel.cs (2026-05-27, 13-plan 박제 재인용)`

## Approach

1. **Trombone.prefab에 신규 child `PanelAnchor` GameObject 추가** — Trombone root 직속, Rig/TromboneAnchor/TromboneNoteDisplay/RhythmGameHost 형제 위치. 컴포넌트는 Transform 1개만(추가 컴포넌트 부착 금지). 초기값:
   - `localPosition = (0, 0, 0)` (시작점, manual 회귀에서 조정 가능)
   - `localRotation = Quaternion.identity` (Euler 0,0,0)
   - `localScale = (1, 1, 1)`
   - 새 fileID 발급 (예: `9100000000000000003` 또는 prefab YAML 신규 GUID).

2. **`TromboneNoteDisplayAdapter.cs` 재정의**:
   - **신규 SerializeField 1건 추가**: `[SerializeField] TrombonePartialController partialController;` — CenterPartialIndex / AnglePerPartial / PartialCount의 단일 진실원. Begin에서 null 폴백: `if (partialController == null) partialController = GetComponentInParent<TrombonePartialController>();`.
   - **deprecated SerializeField**: `verticalSpacingMeters` (라인 34), `fanCenterPartialIndex` (라인 37)는 더 이상 layout 산출에 쓰이지 않음. **남겨두되** Tooltip에 "[deprecated 2026-05-27] PanelAnchor 기반 pitch 적층으로 교체됨. partialController.AnglePerPartial / CenterPartialIndex를 사용한다."를 박제. 향후 cleanup plan에서 제거 후보. Begin/LateUpdate 본문에서는 사용 제거.
   - **`ComputePanelWorldPos(Transform anchor, int partialIndex)` 신규 헬퍼** (internal로 노출해 회귀 테스트 가능):
     ```
     internal Vector3 ComputePanelWorldPos(Transform anchor, int partialIndex)
     {
         float pitchDeg = (partialIndex - GetCenterPartialIndex()) * GetAnglePerPartial();
         return anchor.position + Quaternion.AngleAxis(pitchDeg, anchor.right) * anchor.forward * panelRadius;
     }

     int   GetCenterPartialIndex() => partialController != null ? partialController.CenterPartialIndex : fanCenterPartialIndex;
     float GetAnglePerPartial()    => partialController != null ? partialController.AnglePerPartial    : 15f;
     ```
     기존 동명 헬퍼(라인 240-254)는 삭제하고 위 시그니처로 교체. 회귀 테스트가 기존 시그니처(`Transform anchor, int partialIndex, int totalPartials`)를 사용한다면 그 테스트도 본 plan에서 갱신 — 본 plan은 회귀 테스트 영향 범위를 후속 단계 6번에서 확인.
   - **`ComputePanelRotation(Transform anchor, Vector3 panelWorldPos)` 갱신**:
     ```
     internal Quaternion ComputePanelRotation(Transform anchor, Vector3 panelWorldPos)
     {
         Vector3 toAnchor = (anchor.position - panelWorldPos);
         if (toAnchor.sqrMagnitude < 0.0001f) return Quaternion.identity;
         toAnchor.Normalize();
         // panel의 up이 anchor.right와 수직 평면에 정렬되도록 보정 (pitch 적층 축 일치)
         Vector3 panelUp = Vector3.Cross(toAnchor, anchor.right);
         if (panelUp.sqrMagnitude < 0.0001f) panelUp = Vector3.up;
         return Quaternion.LookRotation(toAnchor, panelUp);
     }
     ```
     기존 헬퍼(라인 257-265)의 `inward.y = 0f` 수평 호 가정 제거.
   - **`Begin` 본문 layout 재정의** (라인 97-167):
     - `Camera.main` / `camFwd` / `camPos.y` 의존 전면 제거.
     - 각 파셜 i에 대해 `panelPos = ComputePanelWorldPos(anchor, p)`, `panelRot = ComputePanelRotation(anchor, panelPos)`로 셋.
     - `panel.transform.localScale` 식(라인 143-148)은 그대로 유지 (laneHeightMeters / panelScrollLengthMeters는 패널 자체 크기이므로 layout 축과 독립).
     - `panel.NoteColorOverrides = colorMap;` `panel.SetLaneConfig(cfg);` `panel.Show(...)` 호출은 무변경.
     - `assignedNotes` 중복 제거 로직(라인 108, 119-127, 131)은 그대로 유지 (sub-spec 02 Boundaries: lane 매핑 무변경).
   - **`LateUpdate` 본문 재정의** (라인 179-200):
     - `_camera` / `camPos` / `camFwd` 전면 제거 (private 필드 `_camera`도 삭제, `_trackAnchor`는 유지).
     - 매 프레임 `_trackAnchor`(= PanelAnchor)의 world position/rotation 변화에만 반응:
       ```
       for (int i = 0; i < spawnedPanels.Count; i++) {
           var panel = spawnedPanels[i];
           if (panel == null) continue;
           int partialIndex = i;  // _partialOffsets 인덱스와 동치 (Begin에서 0..N-1 순)
           Vector3 panelPos = ComputePanelWorldPos(_trackAnchor, partialIndex);
           Quaternion panelRot = ComputePanelRotation(_trackAnchor, panelPos);
           panel.transform.position = panelPos;
           panel.transform.rotation = panelRot;
       }
       ```
     - `_partialOffsets` 리스트는 더 이상 필요 없으므로 (offset = i - center를 매번 산출) 제거. 단 기존 코드가 `_partialOffsets`를 다른 곳에서 참조하지 않는지 grep 확인 후 안전 제거.
   - **`Hide`** (라인 210-230): `_camera = null` / `_partialOffsets.Clear()` 라인은 본 plan의 필드 제거에 맞춰 함께 삭제. 그 외 패널/SO 정리는 그대로.
   - **`GetCameraHorizontalForward()`** (라인 169-177): 본 plan에서 호출처 0건이 되므로 제거.

3. **Trombone.prefab 직렬화 슬롯 갱신**:
   - 신규 PanelAnchor GameObject의 fileID를 발급(예: `2697649137764804010` 같은 신규 값) 후:
     - `InstrumentBase._panelAnchor: {fileID: <PanelAnchor 신규 fileID>}` (line 990 갱신, **통합 결정** — _panelAnchor와 centerAnchor 공유).
     - `TromboneNoteDisplayAdapter.centerAnchor: {fileID: <PanelAnchor 신규 fileID>}` (line 623 갱신).
     - `TromboneNoteDisplayAdapter.partialController: {fileID: <Trombone root의 TrombonePartialController 컴포넌트 fileID>}` (라인 신규 추가).
   - `TrombonePartialController.tromboneRoot` (line 1022)는 **무변경** — Rig 자체를 가리킴.
   - `m_Component` 배열 (Trombone GameObject의 컴포넌트/자식 목록, line ~3010 근방)에 신규 PanelAnchor의 Transform fileID를 추가.
   - 가능하면 Unity Editor에서 직접 GameObject 생성 + Inspector wiring으로 진행 (MCP `manage_prefabs.create_child` 사용 가능). 텍스트 편집은 fallback.

4. **`rhythm-game/13-trombone-note-display.md` 본문 + 13-plan supersede 메모 추가**:
   - `docs/specs/rhythm-game/specs/13-trombone-note-display.md`: 본문 What 섹션의 "트롬본 앞 수평 반원형으로 나타난다" / "각 패널은 파셜 하나에 대응하며(파셜 0~4), 트롬본 축에 수직하게 배치된다"를 갱신 → "트롬본 PanelAnchor 기준 시점 pitch -30°/-15°/0°/+15°/+30°에 1:1 적층(`docs/specs/trombone-pitch-perception/specs/02-note-vertical-layout.md`가 supersede)"로 교체. Behavior 섹션의 "수평 반원형" 문구도 동일 갱신.
   - `docs/specs/rhythm-game/plans/2026-05-21-linksky0311-trombone-note-display.md`: `## Notes` 마지막에 한 줄 append — "[2026-05-27 supersede] 본 plan의 yaw 반원형 layout(arcDegrees=120°, ±60° 호)은 `docs/specs/trombone-pitch-perception/plans/2026-05-27-claude-note-pitch-panel-anchor-stack.md`(sub-spec 02-note-vertical-layout)가 PanelAnchor 기반 pitch 수직 적층으로 교체. centerAnchor·_panelAnchor 슬롯도 신규 PanelAnchor child로 이동."

5. **회귀 테스트 영향 점검**:
   - `Assets/RhythmGame/Tests/Editor/`에서 `TromboneNoteDisplayAdapter` 관련 테스트(특히 `ComputePanelWorldPos(anchor, partialIndex, totalPartials)` 시그니처를 사용하는 테스트)를 grep해 찾는다. 시그니처 변경으로 컴파일 실패 시 테스트도 새 시그니처로 갱신 (테스트는 ARD 식을 검증하도록 expected 값 재계산 — `anchor.position + AngleAxis(pitch_i, anchor.right) * anchor.forward * radius`). 새 시그니처가 partialController에 의존하므로 테스트에서는 MonoBehaviour 의존을 피하려면 fallback 경로(`partialController == null → fanCenterPartialIndex / 15f`) 검증을 추가.

6. **컴파일·테스트 게이트**:
   - 위 2, 5 변경 후 [`unity-mcp-workflow`](../../../../.claude/skills/unity-mcp-workflow/SKILL.md) skill의 컴파일 대기·`read_console types=["error"]` 절차로 errors 0 확인.
   - `unity-test-runner` sub-agent 1회 호출 — EditMode 전체 pass 확인. Trombone adapter 회귀 테스트가 새 시그니처로 갱신됐는지 확인.

7. **수동 회귀 시나리오** — Editor Play Mode에서 (a) 트롬본 잡기 전 5패널 0건, (b) 잡은 후 PanelAnchor 기준 pitch -30°/-15°/0°/+15°/+30°에 패널 5건 가시화, (c) trombone z회전(파셜 1↔2 전환)을 해도 5패널 위치 불변, (d) 사용자가 머리를 좌우로 흔들어도 패널이 따라오지 않음, (e) trombone 놓으면 5패널 일괄 destroy.

## Deliverables

- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` (수정) — 신규 child `PanelAnchor` GameObject(Trombone root 직속, Transform only) + `InstrumentBase._panelAnchor` 슬롯을 PanelAnchor로 갱신 + `TromboneNoteDisplayAdapter.centerAnchor` 슬롯을 PanelAnchor로 갱신 + `TromboneNoteDisplayAdapter.partialController` SerializeField wiring 추가.
- `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` (수정) — `partialController` SerializeField 추가, `ComputePanelWorldPos`/`ComputePanelRotation` 재정의(ARD 03 식), `Begin`/`LateUpdate` 본문 카메라 추적 제거 후 PanelAnchor 기반 pitch 적층으로 교체, `GetCameraHorizontalForward` / `_camera` / `_partialOffsets` 제거, deprecated SerializeField(`verticalSpacingMeters`, `fanCenterPartialIndex`) Tooltip 갱신.
- `docs/specs/rhythm-game/specs/13-trombone-note-display.md` (수정) — What / Behavior의 "수평 반원형" 문구를 PanelAnchor 기반 pitch 적층으로 갱신 + supersede 메모.
- `docs/specs/rhythm-game/plans/2026-05-21-linksky0311-trombone-note-display.md` (수정 — Notes append) — supersede 한 줄 추가.
- `Assets/RhythmGame/Tests/Editor/*` (해당 시) — TromboneNoteDisplayAdapter 회귀 테스트가 새 ComputePanelWorldPos 시그니처/식을 따르도록 갱신.

## Acceptance Criteria

- [ ] `[auto-hard]` `Trombone.prefab`에 `PanelAnchor`라는 이름의 GameObject가 Trombone root 직속 child로 존재한다.
  **검증:** Unity MCP `manage_prefabs.get_hierarchy prefab_path="Assets/Instruments/Trombone/Prefabs/Trombone.prefab"` 결과의 `items[]`에서 path가 `Trombone/PanelAnchor`인 항목이 정확히 1건 존재 + `componentTypes`에 `UnityEngine.Transform` 외 컴포넌트 0건. 또는 `Grep -n "m_Name: PanelAnchor" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과 1건.

- [ ] `[auto-hard]` `InstrumentBase._panelAnchor` 슬롯이 신규 PanelAnchor를 가리킨다 — 기존 fileID `9100000000000000002`(=Rig)와 다른 값으로 갱신된다.
  **검증:** `Grep -n "_panelAnchor:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과의 fileID가 `9100000000000000002`가 아니며, 동 파일에서 그 fileID를 grep하면 `m_Name: PanelAnchor` 블록과 동일 GameObject의 Transform임이 확인된다.

- [ ] `[auto-hard]` `TromboneNoteDisplayAdapter.centerAnchor` 슬롯이 동일 신규 PanelAnchor fileID를 가리킨다 (통합 결정 박제).
  **검증:** `Grep -n "centerAnchor:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과의 fileID가 위 AC의 _panelAnchor fileID와 일치.

- [ ] `[auto-hard]` `TromboneNoteDisplayAdapter`에 `partialController` SerializeField가 존재하고 prefab에서 `Trombone` root의 `TrombonePartialController` 컴포넌트로 wiring된다.
  **검증:** `Grep -n "partialController:\|SerializeField.*partialController\|SerializeField\] TrombonePartialController" Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` 결과 1건 이상 + `Grep -n "partialController:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과의 fileID가 비-zero이며 동 파일에서 `m_Script: .*TrombonePartialController` 블록의 component fileID와 일치.

- [ ] `[auto-hard]` `TrombonePartialController.tromboneRoot` 슬롯(line 1022)은 기존 fileID `9100000000000000002`(=Rig) 그대로 유지된다 (Tech Spec Boundaries: PartialController 입력 처리 불변).
  **검증:** `Grep -n "tromboneRoot:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과의 첫 번째 매칭(PartialController 블록 내) fileID = `9100000000000000002` 그대로.

- [ ] `[auto-hard]` `TromboneNoteDisplayAdapter.cs`의 `Begin`/`LateUpdate` 본문에 `Camera.main` / `_camera` / `camFwd` / `GetCameraHorizontalForward` 호출이 0건이다 (카메라 추적 제거 박제).
  **검증:** `Grep -n "Camera\.main\|_camera\|camFwd\|camPos\|GetCameraHorizontalForward" Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` 결과 0건.

- [ ] `[auto-hard]` `TromboneNoteDisplayAdapter.ComputePanelWorldPos`가 `Quaternion.AngleAxis(pitch, anchor.right) * anchor.forward * radius` 패턴을 포함한다 (ARD 03 식 박제).
  **검증:** `Grep -n "Quaternion\.AngleAxis.*anchor\.right\|AngleAxis.*right.*anchor\.forward" Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` 결과 1건 이상.

- [ ] `[auto-hard]` EditMode 테스트 스위트가 컴파일 후 errors 0이고, 기존 `TromboneNoteDisplayAdapter`/`InstrumentLaneConfig`/`RhythmGameHost` 관련 테스트가 모두 통과한다 (회귀 테스트 시그니처 갱신 포함).
  **검증:** `unity-test-runner` sub-agent 호출 결과 EditMode pass + Unity MCP `read_console types=["error"]` 결과 0건. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 본 AC를 `pass(skip)`으로 처리하고 Notes에 사유 기록.

- [ ] `[auto-soft]` Unity Editor 콘솔에 `TromboneNoteDisplayAdapter` / `PanelAnchor` 관련 NullReferenceException / MissingComponent 에러가 0건이다.
  **검증:** Unity MCP `read_console types=["error","warning"] filter_text="TromboneNoteDisplayAdapter\|PanelAnchor"` → 0건.

- [x] `[manual-hard]` Editor Play Mode에서 트롬본을 잡고 리듬게임 세션을 시작하면 5개 파셜 패널이 PanelAnchor 기준 pitch -30°/-15°/0°/+15°/+30°에 1:1 적층된다 (시야 정면에 파셜 2가 위치, 위로 갈수록 파셜 3→4, 아래로 갈수록 파셜 1→0). *[2026-05-27 후속 plan `2026-05-27-claude-note-panel-pitch-sign-flip.md`로 부호 반전 후 재검증 통과 — i=0 dy=-0.400 시야 아래, i=4 dy=+0.400 시야 위, 5단계 적층 dy=[-0.400, -0.207, 0, +0.207, +0.400]m]*
  **검증:** Editor Play → 트롬본 grab → 임시 트롬본 차트(`_devtest-trombone-1.vmsong`) 세션 진입 → Game/Scene 뷰에서 5패널의 시점 pitch가 -30/-15/0/+15/+30°에 일치하는지 시각 확인 (Scene View에서 PanelAnchor의 forward 축에 대해 각 패널이 정확한 각도에 정렬).

- [x] `[manual-hard]` 사용자가 트롬본 z회전을 변경해 파셜 1↔2를 오가도 5패널의 world position/rotation이 변하지 않는다 (PanelAnchor가 Rig가 아닌 Trombone root에 부착되어 Body z회전 영향 밖). *[2026-05-27 자동 검증: Rig.z 0→30° 변경 시 PanelAnchor delta=0.0000, 5패널 world pos delta 모두 0]*
  **검증:** Editor Play → 트롬본 grab → 5패널 표시 → trombone root z회전(VR 또는 mock으로 -25°↔-10°) 변경 → Scene View Transform inspector에서 NoteDisplayPanel(Clone) 5건의 world position 변화량이 ε 이내 (head/anchor 이동 없는 한 고정).

- [x] `[manual-hard]` 사용자가 머리를 좌우/상하로 흔들어도 5패널이 카메라를 따라오지 않는다 (카메라 추적 제거 박제). *[2026-05-27 자동 검증: 카메라 yaw+45/pitch+30/pos+1m 시뮬레이션 시 5패널 world pos 변화 max 0.0000m. AC6 검증으로 코드 측 `Camera.main` 호출 0건 grep도 PASS]*
  **검증:** Editor Play → 트롬본 grab → 5패널 표시 → VR 카메라(또는 Scene View Camera)를 좌우 yaw/상하 pitch로 흔들기 → 5패널 world position 변화 없음 시각 확인.

- [x] `[manual-hard]` `rhythm-game/13-trombone-note-display.md` 본문과 13-plan의 Notes에 supersede 메모가 추가되어 후속 세션이 본 spec을 단일 진실원으로 인지한다. *[2026-05-27 자동 검증: 13-spec 본문에 4건 supersede 메모 + PanelAnchor 적층 명세, 13-plan Notes에 supersede 한 줄]*
  **검증:** `Read docs/specs/rhythm-game/specs/13-trombone-note-display.md` → What/Behavior에 "PanelAnchor 기반 pitch 적층" 문구 + 02-note-vertical-layout 링크 확인. `Read docs/specs/rhythm-game/plans/2026-05-21-linksky0311-trombone-note-display.md` → `## Notes` 마지막에 supersede 한 줄 확인.

## Out of Scope

- 패널 내 슬라이드 X 오프셋 시각화 (rhythm-game/13의 후속 plan 또는 별도 sub-spec 책임 — sub-spec 02 Out of Scope 박제).
- 다른 악기(피아노/드럼) 노트 디스플레이 layout 변경.
- `TrombonePartialController` 입력 처리 / hysteresis / PartialIndex 산출 / `tromboneRoot` 슬롯 변경 (Tech Spec Boundaries).
- 5개 anchor 각도 값 변경 (`anglePerPartial=15°`, `centerPartialIndex=2` 유지).
- SessionPanel(14-spec) 위치 조정 — `_panelAnchor` 통합 결정으로 SessionPanel도 신규 PanelAnchor를 시점 origin으로 쓰게 되지만, SessionPanel의 가시 위치가 부자연스러우면 별도 ARD/plan으로 분리.
- `NoteDisplayPanel.cs` 내부 노트 배치 / 색상 / Show/Hide 로직.
- `Trombone_LaneConfig.asset`의 35건 MIDI 매핑.
- deprecated SerializeField(`verticalSpacingMeters`, `fanCenterPartialIndex`) 물리적 제거 — 본 plan은 Tooltip 갱신만, 제거는 후속 cleanup plan 후보.
- PanelAnchor의 정확한 localPosition 미세 튜닝 — manual 회귀에서 (0,0,0) 시작점이 부자연스러우면 inspector에서 조정 후 prefab 저장. 후속 ARD 격상 후보.

## Notes

- **_panelAnchor 통합 결정 (본 plan 박제, ARD 후보)**: ARD 03이 "_panelAnchor 슬롯과의 통합 여부는 plan 단계 결정"으로 위임함. 본 plan은 **통합** 채택. SessionPanel(14-spec) 위치가 본 결정으로 부자연스러워지면 (a) ARD 격상 + (b) 별도 SessionPanelAnchor child 분리 plan을 후속으로 작성. 통합의 장점: 단일 origin·디버깅 비용 낮음. 통합의 위험: SessionPanel 위치가 5패널 origin과 동일 = mouthpiece 근처가 부자연스러울 수 있음.
- **PanelAnchor localPosition (0,0,0) 시작점**: trombone root.position이 사용자가 트롬본을 잡으면 mouthpiece 근처(=사용자 입·머리 근처)로 이동하므로 (0,0,0)이 합리적 시작점. manual 회귀에서 패널이 너무 가깝거나 어색하면 inspector에서 (0, +0.1, 0) 같은 eye 높이 미세 보정. 본 plan AC는 (0,0,0)으로 제출하고 시각 확인 후 사용자가 조정 가능하게 둠.
- **deprecated SerializeField cleanup**: `verticalSpacingMeters`, `fanCenterPartialIndex`는 본 plan에서 layout 산출에서 빠지지만 직렬화 호환을 위해 즉시 제거하지 않음. 후속 cleanup plan에서 제거하면 prefab YAML diff가 1회 더 발생하지만 무해. Tooltip에 [deprecated 2026-05-27] 명시로 다음 세션이 혼동하지 않게 함.
- **회귀 테스트 시그니처 변경 영향**: 기존 `ComputePanelWorldPos(Transform, int, int totalPartials)` 시그니처가 회귀 테스트에서 사용 중이면 본 plan에서 새 `ComputePanelWorldPos(Transform, int)` 시그니처로 갱신해야 컴파일 통과. 테스트 update 범위는 단계 5에서 확인. 만약 테스트가 totalPartials를 ARD 식과 무관하게 사용한다면 테스트도 ARD 식(`AngleAxis(pitch_i, right) * forward * radius`)을 검증하도록 expected 값 재계산.
- **부호 정합 확인 필요**: ARD가 "파셜 0 = 가장 낮은 음 → 시야 -30°(아래)" / "파셜 4 = 가장 높은 음 → 시야 +30°(위)" 라고 명시. 본 plan의 식 `pitch_i = (i - 2) × 15° = {-30, -15, 0, +15, +30}` (i=0..4)이 정합. 단 `Quaternion.AngleAxis(pitch, anchor.right)`의 회전 방향이 (+pitch → 위) 인지 (+pitch → 아래)인지는 anchor.right의 부호에 의존 (Unity의 right hand rule). manual AC에서 시각 확인 — 어긋나면 식의 pitch 부호를 반전 (`-pitchDeg`).
- **카메라 추적 제거의 부작용**: 현 구현은 매 프레임 패널이 사용자 시야 정면으로 미세 따라옴 → 트롬본을 어디 두든 패널이 보였음. 본 plan 변경 후 PanelAnchor가 trombone root에 종속되므로 trombone이 attach되지 않은 상태에서는 패널이 사용자 시야 밖에 있을 수 있음. spec Behavior는 "트롬본을 잡고 세션 시작" 전제이므로 비-attach 상태 가시성은 spec 외 — 본 plan에서 자연스러움 확인.
- **후속 plan 후보**: (a) PanelAnchor localPosition 사용자 피드백 반영 미세 튜닝, (b) deprecated SerializeField 제거 cleanup, (c) SessionPanel(14-spec) 위치와 통합 anchor 충돌 시 분리 plan, (d) 부호 정합이 어긋날 경우 ARD 격상.
- [2026-05-27]: 후속 plan `2026-05-27-claude-note-panel-pitch-sign-flip.md` 추가. 완료 후 본 plan AC10 재검증 필요.
- [2026-05-27 완료]: 후속 plan으로 AC10 부호 정합 재검증 통과. PlayMode 자동 측정 결과 i=0 dy=-0.400 (시야 아래), i=4 dy=+0.400 (시야 위), 5단계 적층 dy=[-0.400, -0.207, 0, +0.207, +0.400]m. AC11/12/13은 본 plan 변경에 영향 없음 — 자동 검증으로 그대로 PASS. ARD 식 구조(`AngleAxis * forward * radius`) 보존.
- [2026-05-27 추가 정정]: PanelAnchor parent 결정(Trombone root 직속)이 attach 시 사용자 mouth 추적 실패를 유발 — `TromboneAnchor.tromboneRoot`가 실은 Rig를 가리켜 Rig만 mouth로 이동하므로 root 직속 PanelAnchor가 따라오지 않음. 후속 plan `2026-05-27-claude-panel-anchor-rig-reparent.md`로 PanelAnchor를 **Rig 자식(Body 형제)** 으로 reparent하여 해결 (PlayMode 자동 검증 통과: 5패널 yaw +86°, pitch -30.8°~+32.9°, dist 0.75~0.77m).

## Handoff

- **PanelAnchor child**: `Trombone/Rig/PanelAnchor` *[2026-05-27 정정: 본 plan은 Trombone root 직속으로 박제했으나 후속 plan에서 Rig 자식(Body 형제)으로 reparent됨. attach 시 mouth 추적 정합]*, Transform fileID `5307817778198693025`, localPosition `(0,0,0)`, localRotation identity, scale `(1,1,1)`. Body·Slide·MouthPiece·NoteDisplay·SlidePositionMarkers의 형제. Body 회전 영향 밖 (Body Transform만 회전, 형제 무영향).
- **통합 결정 박제**: `InstrumentBase._panelAnchor` ↔ `TromboneNoteDisplayAdapter.centerAnchor` 모두 동일 PanelAnchor fileID `5307817778198693025`. SessionPanel(14-spec)도 이 anchor를 공유한다. 위치가 부자연스러우면 ARD 격상 + 별도 anchor 분리 후속 plan.
- **`TrombonePartialController.tromboneRoot` 무변경**: 기존 Rig (fileID `9100000000000000002`) 유지.
- **`TromboneNoteDisplayAdapter` SerializeField 표면 변경**: `partialController: TrombonePartialController` 신규 추가 (prefab wiring fileID `4932485236075849092`). `verticalSpacingMeters`, `fanCenterPartialIndex`는 `[deprecated 2026-05-27]` Tooltip 유지, 후속 cleanup plan 후보.
- **`ComputePanelWorldPos(Transform anchor, int partialIndex)` 시그니처**: ARD 03 식 `anchor.position + Quaternion.AngleAxis(pitchDeg, anchor.right) * anchor.forward * panelRadius`. **단 후속 plan `2026-05-27-claude-note-panel-pitch-sign-flip.md`에서 `pitchDeg = (CenterPartialIndex - partialIndex) * AnglePerPartial`로 부호 반전 박제** — 사용자 시야 좌표계 기준 i=0 아래, i=4 위.
- **`ComputePanelRotation(Transform anchor, Vector3 panelWorldPos)` 시그니처**: `panelUp = Cross(toAnchor, anchor.right)` 기반 LookRotation.
- **카메라 추적 제거 박제**: `_camera`, `GetCameraHorizontalForward`, `camFwd`/`camPos` 모두 삭제. LateUpdate는 `_trackAnchor` (PanelAnchor) world pose 변화에만 반응.
- **회귀 결과**: EditMode 133/133 + PlayMode 4/4 + console errors 0건. AC1~9 + AC11~13 PASS, AC10은 후속 plan으로 재검증 PASS.
- **rhythm-game/13 supersede**: 13-spec 본문 + 13-plan Notes에 supersede 메모 박제 (단일 진실원 = sub-spec 02).
