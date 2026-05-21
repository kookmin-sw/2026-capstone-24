# Tech Spec: 악기 연주 방법 가이드 패널

**Sub-Spec:** [`09-instrument-guide-panel.md`](../specs/09-instrument-guide-panel.md)
**Status:** `Draft`
**Date:** 2026-05-20

## Components

- **GuidePanelController** (신규) — 가이드 패널 루트 MonoBehaviour.
  패널 열기/닫기, `IActiveInstrumentProvider` 구독, 탭 가시성 조정 담당.
- **InstrumentTabBar** (신규) — 상단 악기 탭 UI.
  활성화할 탭 세트를 외부에서 설정받아 탭 표시/비표시 전환.
- **GuidePageController** (신규) — 현재 선택된 악기의 페이지(이미지 2개 + 텍스트)를 관리.
  Prev/Next 버튼 활성화 상태 유지.
- **IActiveInstrumentProvider** (기존) — 현재 앵커에 부착된 악기 상태를 노출하는 인터페이스.
  `event ActiveInstrumentChanged`, `IActiveInstrument Current { get; }` 보유.

## Data / Control Flow

- 세션 패널 Guide 버튼 클릭
  → GuidePanelController.Open()
  → `IActiveInstrumentProvider.Current` 조회
  → Current != null: 해당 악기 탭만 표시 / Current == null: 전체 탭 표시
  → InstrumentTabBar 탭 가시성 설정 + GuidePageController 첫 페이지 로드

- `IActiveInstrumentProvider.ActiveInstrumentChanged` 이벤트 발생
  → GuidePanelController
  → InstrumentTabBar 탭 가시성 갱신
  → GuidePageController 첫 페이지로 리셋

- InstrumentTabBar 탭 선택
  → GuidePageController.LoadInstrument(instrumentId)
  → 이미지 2개 + 텍스트 갱신

- Next / Prev 버튼 클릭
  → GuidePageController.NextPage() / PrevPage()
  → 이미지·텍스트 갱신 + 버튼 활성화 상태 갱신

## Boundaries

- **건드린다**: GuidePanelController, InstrumentTabBar, GuidePageController (모두 신규),
  `IActiveInstrumentProvider.ActiveInstrumentChanged` 이벤트 구독
- **건드리지 않는다**: DrumKitStickAnchor / TromboneAnchor 내부 로직,
  SessionPanelController 기존 로직, IActiveInstrumentProvider 인터페이스 본문

## Invariants

- 악기가 앵커에 부착된 상태에서는 InstrumentTabBar에 해당 악기 탭 하나만 표시된다.
- GuidePageController가 표시하는 이미지는 항상 정확히 2개 (플레이스홀더 포함).
- Prev 버튼은 첫 페이지에서, Next 버튼은 마지막 페이지에서 항상 비활성화 상태를 유지한다.
- 이미지는 원본 크기와 무관하게 UI 컨테이너에 맞게 스케일링되어 표시된다.

## Assumptions

- `IActiveInstrumentProvider.Current == null`이면 어떤 악기도 앵커에 없음 —
  출처: Explore 에이전트 보고 (2026-05-20)
- `ActiveInstrumentChanged`는 악기 앵커 부착/탈착 시 발행됨 —
  출처: Explore 에이전트 보고 (2026-05-20)
- `SessionPanelController`가 동일한 `IActiveInstrumentProvider` 구독 패턴을 사용 중 →
  GuidePanelController에 동일 패턴 적용 가능 —
  출처: Explore 에이전트 보고 (2026-05-20)
- 이미지는 `Assets/Tutorial/<악기명>/01`, `02` … 형식의 폴더 구조로 저장되며,
  GuidePageController가 번호 순서대로 로드함 — 출처: 사용자 확인 (2026-05-20)

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `session-panel/specs/02-start-menu-section.md` | `09-instrument-guide-panel.md` | 시작 메뉴는 세션 패널 내부 섹션이나, 가이드 패널은 세션 패널 버튼으로 여는 독립 외부 패널 |
| `session-panel/specs/03-volume-section.md` | `09-instrument-guide-panel.md` | 볼륨 섹션도 악기 앵커 상태에 따라 슬롯이 바뀌나, 가이드 패널은 탭 단위 필터링 방식 사용 |

## Open Tech Decisions

- [x] 가이드 페이지 데이터 저장 방식: `Assets/Tutorial/<악기명>/<NN>` 폴더 구조 +
      GuidePageController 동적 로드로 결정. 이미지 크기가 페이지마다 다를 수 있으므로
      UI 컨테이너 기준 스케일링 필수. — 사용자 확인 (2026-05-20)
