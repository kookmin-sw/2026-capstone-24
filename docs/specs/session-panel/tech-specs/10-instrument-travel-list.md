# 악기 이동 목록 섹션

**Sub-Spec:** [`10-instrument-travel-list.md`](../specs/10-instrument-travel-list.md)
**Status:** `Draft`
**Date:** 2026-05-26

## Components

- **`InstrumentTravelSectionController`** (신규) — 본 섹션의 진입 MonoBehaviour. 패널 open 시점에 씬 anchor 스냅 → 항목 prefab 생성/배치, 항목의 [이동]/[가이드] 콜백 라우팅, "현재 위치" 상태 갱신.
- **`InstrumentTravelItem`** (신규, prefab + 컴포넌트) — 한 악기를 표현하는 항목 UI. 큰 악기 이미지 + [이동] · [가이드] 두 버튼을 묶는다. 자체 상태(`isCurrent`)에 따라 [이동] 버튼 활성/비활성을 결정.
- **`InstrumentGuidePanel`** (신규, prefab + 컴포넌트) — 세션 패널 외부에 떠 있는 독립 가이드 패널. 한 악기의 가이드 페이지(이미지 2개 가로 + 텍스트 + Prev/Next)를 표시. Section Controller가 항목 [가이드] 클릭 시 "어떤 악기를 보여줄지" 위임.
- **Anchor 스냅 헬퍼** (신규, Section Controller 내부 또는 같은 파일의 헬퍼) — 씬에서 `TeleportationAnchor` + `InstrumentTeleportLink`를 가진 GameObject 수집, 각 anchor의 `linkedInstrument`(InstrumentBase)에서 이름·이미지 자원·`IActiveInstrument` 식별자를 추출.
- **`TabPanelController`** (기존) — 본 섹션 탭 노출/전환을 담당. 본 sub-spec에서 세 번째 탭 항목을 등록.
- **`TeleportationAnchor`** / **`BaseTeleportationInteractable`** (기존, XRI) — 항목 [이동] 클릭 시 `RequestTeleport(...)` 호출 대상. 본 sub-spec은 이 API만 호출한다.
- **`InstrumentTeleportLink`** + **`TeleportInstrumentProvider`** (기존) — anchor 텔레포트 결과로 활성 악기 전환 이벤트(`ActiveInstrumentChanged`)를 발행. Section Controller가 구독해 "현재 위치" 항목 시각 갱신.

## Data / Control Flow

- 세션 패널 open → `TabPanelController`가 본 섹션 탭으로 전환 → `InstrumentTravelSectionController.OnEnable` → 씬 anchor 스냅 → 각 anchor당 `InstrumentTravelItem` 인스턴스를 컨테이너 안에 생성, 한 행 3개 왼쪽 정렬로 배치.
- 항목 [이동] 클릭 → `InstrumentTravelItem` → Section Controller → 대응 anchor의 `TeleportationAnchor.RequestTeleport(...)` → XRI Locomotion이 페이드/이행 수행 → anchor `selectExited` + `locomotionStarted` 페어 → 기존 attach 시스템(예: `TromboneAnchor.AttachTromboneToMouth`) 자동 발동.
- 텔레포트 후 `InstrumentTeleportLink.AnyAnchorTeleported`(static) 발화 → `TeleportInstrumentProvider.Current` 갱신 → `ActiveInstrumentChanged` 이벤트 → Section Controller가 구독해 "현재 위치" 항목 시각·[이동] 비활성 상태 갱신.
- 사용자가 잡은 악기를 둔 채 다른 항목의 [이동]을 눌렀을 때, 새 anchor의 `selectExited` → `locomotionStarted` 페어가 기존 anchor의 `TromboneAnchor.OnLocomotionStarted`를 "anchor 외부로 이동" 분기로 발화시켜 `Detach()`가 자동 호출된다.
- 항목 [가이드] 클릭 → `InstrumentTravelItem` → Section Controller → `InstrumentGuidePanel.Open(instrumentId)` → 패널이 해당 악기 첫 페이지로 활성화/포지셔닝 → 사용자가 Prev/Next로 페이지 탐색.

## Boundaries

- **건드린다**:
  - `Assets/SessionPanel/Prefabs/SessionPanel.prefab` — 세 번째 탭 버튼 추가, 세 번째 섹션 패널 노드 추가.
  - `Assets/SessionPanel/Scripts/` — Section Controller / Item 컴포넌트 / Guide Panel 컴포넌트 신규.
  - `Assets/SessionPanel/Prefabs/` 또는 `Assets/SessionPanel/UI/` — `InstrumentTravelItem.prefab`, `InstrumentGuidePanel.prefab` 신규.
  - 가이드 페이지 자산 경로 (`Tutorial/<악기명>/NN`) — 09 규약을 그대로 승계, 본 sub-spec에서 처음 실제 로딩 진입점이 생긴다.
- **건드리지 않는다**:
  - XRI Locomotion/Teleport 페이드 표현, anchor의 `RequestTeleport` 내부 로직.
  - 악기별 anchor 컴포넌트(예: `TromboneAnchor`)의 attach/detach 구현. 본 sub-spec은 anchor `selectExited` + `locomotionStarted` 페어가 자동으로 그 경로를 발화시키도록 기존 시퀀스를 우회하지 않는다.
  - `InstrumentTeleportLink` / `TeleportInstrumentProvider`의 이벤트 발행 로직. 구독만 한다.
  - 다른 sub-spec의 섹션(`VolumePanel`, `RhythmGamePanel`) 내부 콘텐츠.

## Invariants

- 한 시점에 사용자가 위치한 anchor는 0 또는 1개. 1개일 때 그 anchor에 대응하는 항목의 [이동] 버튼은 비활성이며, [가이드] 버튼은 활성을 유지한다.
- 목록 항목 수 = 패널 open 스냅 시점의 씬 내 (`TeleportationAnchor` + `InstrumentTeleportLink`) 보유 GameObject 수. 패널이 열려 있는 동안 변하지 않는다.
- [이동] 버튼 클릭 → 텔레포트 발동은 모두 `TeleportationAnchor.RequestTeleport` 경로로 단일화된다. 새로운 텔레포트 진입점/우회 경로를 만들지 않는다 (기존 attach/detach·active instrument 갱신을 우회하지 않기 위해).
- [가이드] 버튼 클릭 → 항상 `InstrumentGuidePanel.Open(...)` 1회 호출. 같은 패널 인스턴스가 다른 악기 콘텐츠로 재바인딩되며, 동시에 2개 가이드 패널이 떠 있지 않는다.

## Assumptions

- 모든 텔레포트 대상 악기는 `InstrumentBase` 상속체이며 `IActiveInstrument`를 통해 식별자(`InstrumentId`)·`InstrumentRoot`를 노출한다. 출처: `Read Assets/Instruments/CLAUDE.md` (2026-05-26) §3, §5.
- anchor 자식은 항상 악기 prefab 내부에 있으며 `InstrumentTeleportLink.linkedInstrument`가 본인 `InstrumentBase`로 연결되어 있다. 출처: `Read Assets/Instruments/CLAUDE.md` (2026-05-26) §2 Prefab 골격, §5 RhythmGame 연동 계약.
- 트롬본 같은 attach형 악기는 anchor의 `selectExited` + `locomotionStarted` 이벤트 페어로 attach가 발동되며, 같은 페어가 anchor 외부로 이동할 때 `Detach()`를 자동 발화한다. 출처: `Read Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs` (2026-05-26).
- 두 악기의 anchor 반경이 사용 시점에 겹치지 않도록 씬이 구성된다. 출처: `Read docs/specs/_archive/teleport-locomotion/specs/03-instrument-anchors.md` (2026-05-26) Behavior 마지막 시나리오.
- 09 sub-spec이 박았던 `Tutorial/<악기명>/NN` 폴더 규약은 본 sub-spec이 그대로 사용한다. 출처: `Read docs/specs/session-panel/specs/09-instrument-guide-panel.md` (2026-05-26) §What.
- 현재 `SessionPanel.prefab` 계층은 root(`SessionPanel`) 아래 `Border` / `TabBar`(`VolumeTabButton`, `RhythmTabButton`) / `VolumePanel`(`VolumeSectionController`) / `RhythmGamePanel`(`RhythmGameSectionController` + `SongListPanel`, `SongDetailPanel`)로 구성되어 있다. 본 sub-spec은 `TabBar`에 세 번째 탭 버튼, `SessionPanel` 직속 자식으로 세 번째 섹션 패널 노드를 추가한다. 출처: Unity MCP `manage_prefabs.get_hierarchy` (2026-05-26).

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| [`02-start-menu-section.md`](../specs/02-start-menu-section.md) (`RhythmGamePanel` + `RhythmGameSectionController`) | 본 섹션 (`InstrumentTravelSectionController`) | 02는 곡·난이도·반주를 선택해 **세션을 시작**. 본 섹션은 곡과 무관하게 **공간 이동·악기 전환**. 노출 조건도 다르다(02는 잡힘 상태일 때만, 본 섹션은 양쪽 모두). |
| [`03-volume-section.md`](../specs/03-volume-section.md) (`VolumePanel` + `VolumeSectionController`) | 본 섹션 | 03은 활성 악기 + 마스터 볼륨을 슬라이더로 조절. 본 섹션은 활성 악기를 **결정**하는 쪽. 본 섹션의 [이동] 결과로 활성 악기가 바뀌면 03의 "내 악기" 슬롯이 그 악기로 교체된다 (상호 보완). |
| [`09-instrument-guide-panel.md`](../specs/09-instrument-guide-panel.md) (`Abandoned`) | 본 섹션 + `InstrumentGuidePanel` | 09는 패널 상단의 단일 "가이드" 버튼 → 독립 패널 → 상단 탭으로 악기 선택. 본 sub-spec은 가이드 진입점을 목록 항목별 [가이드] 버튼으로 분산시키고, 외부 독립 패널은 09의 페이지 UI 패턴(이미지 2개 + 텍스트 + Prev/Next)을 그대로 승계해 본 sub-spec의 자산으로 통합한다. |
| `Assets/Instruments/_Core/Scripts/InstrumentTeleportLink.cs` 경유 anchor 흐름 (`/_archive/teleport-locomotion/03-instrument-anchors`) | 본 섹션의 [이동] 버튼 → `RequestTeleport` 흐름 | archive는 라인 끝이 anchor 반경 안에 들어왔을 때 시각 표현을 전환하고 확정 입력으로 발동. 본 sub-spec은 라인·반경 판정 없이 항목 클릭만으로 같은 anchor에 `RequestTeleport`를 보낸다. 도착 이후 attach/active 갱신 파이프라인은 동일. |

## Open Tech Decisions

_현재 열린 결정 없음._
