# 악기 이동 목록 섹션 & 항목 [이동] 동작 (sub-spec 10 plan 1/2)

**Linked Spec:** [`10-instrument-travel-list.md`](../specs/10-instrument-travel-list.md)
**Status:** `In Progress`

## Goal

세션 패널에 세 번째 탭/섹션(`InstrumentTravelSection`)을 박제하고, 패널 open 시점에 씬에서 `TeleportationAnchor` + `InstrumentTeleportLink`를 동반한 GameObject들을 수집해 `InstrumentTravelItem` prefab 인스턴스로 한 행 3개 왼쪽 정렬 그리드를 만든다. 각 항목의 [이동] 버튼 클릭은 대응 anchor의 `TeleportationAnchor.RequestTeleport()`를 호출해 페이드/이행 + 기존 attach 시퀀스(예: `TromboneAnchor.AttachTromboneToMouth`)를 자동 발동시킨다. 사용자가 현재 위치한 anchor의 [이동] 버튼은 비활성. 항목 [가이드] 버튼은 prefab에 시각적으로 포함하되 클릭 시 무동작(2/2 plan에서 동작 부착).

## Context

sub-spec 10이 정의한 세 번째 탭/섹션 + [이동] + [가이드] 두 책임 중, **본 plan(1/2)은 [이동] 경로까지만** 책임진다. 후속 plan(2/2)이 [가이드] 클릭 → 독립 가이드 패널 호출 / Tutorial 자산 로딩을 마저 다룬다. 본 plan에서 [가이드] 버튼은 prefab에 자리만 잡고 OnClick은 비어 있다(또는 `Debug.Log` 1줄 + `// TODO sub-spec 10 plan 2/2`).

sub-spec 10 §What·Behavior가 정의한 핵심 시나리오는 모두 본 plan 책임:
- (a) 본 섹션이 처음 보이면 anchor 보유 악기들이 한 행 3개 왼쪽 정렬로 등장
- (b) 항목 [이동] 클릭 → 페이드 + 도착 + (attach형 악기는) 입에 attach
- (c) 잡은 상태에서 다른 항목 [이동] → 자동 detach 후 새 anchor 도착 후 attach
- (d) 현재 anchor의 [이동] 버튼 비활성

후속 plan(2/2)으로 미루는 항목: 항목 [가이드] 버튼 동작, `InstrumentGuidePanel` prefab+컴포넌트, `Tutorial/<악기명>/NN` 페이지 로더, Prev/Next.

`TabPanelController`는 이미 `tabButtons[]` + `tabPanels[]` 인덱스 매핑만 호출하므로(Read `Assets/SessionPanel/Scripts/TabPanelController.cs`), 세 번째 항목 1개씩만 추가하면 된다. `SessionPanelController.EnsurePanelInstance`는 `VolumeSectionController`와 `RhythmGameSectionController`를 `GetComponentInChildren<...>(true)`로 자동 탐색 + `InjectProvider`로 wire하는 패턴이라 — `InstrumentTravelSectionController`도 같은 패턴을 따르면 별도 wiring 없이 SessionPanel.prefab 안에 두는 것만으로 동작한다.

## Verified Structural Assumptions

- **`SessionPanel.prefab` 루트(`SessionPanel`, fileID 1151033334272281024) 자식 4개:** Border(fileID 2026847926404610461, `m_IsActive: 0`) / TabBar(fileID 4251547738239070682) / VolumePanel(fileID 5751264090231694390, `m_IsActive: 1`) / RhythmGamePanel(fileID 7318693547341281361, `m_IsActive: 0`). 루트 RectTransform fileID 4247339568228231875, sizeDelta=(400,300), localScale=(0.001,0.001,1). 출처: `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab` (2026-05-26) 라인 142-170.

- **`TabBar`(fileID 1324976161269021399, RectTransform 4251547738239070682):** anchor (0,1)~(1,1), anchoredPosition=(0,-20), sizeDelta=(0,40). 자식 RectTransform 2개 — VolumeTabButton(RT fileID 585174415138494519) + RhythmTabButton(RT fileID 1370652255947522305). HorizontalLayoutGroup(fileID 5671264380700573185)이 `ChildForceExpandWidth=1` + `ChildControlWidth=1` + `m_Spacing=2`이므로 **자식을 1개 추가하면 폭이 1/3씩 자동 균등분배**된다. 출처: `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab` (2026-05-26) 라인 323-386.

- **`TabPanelController`(fileID 7754837678443973032, GameObject SessionPanel 루트 부착):** 직렬화 필드 `tabButtons[]` = [7387538153525603796(VolumeTab Button), 5857529928549192149(RhythmTab Button)], `tabPanels[]` = [5751264090231694390(VolumePanel RT), 3286453783198637535(RhythmGamePanel GameObject)], `defaultTab: 0`. `Start()`에서 각 인덱스에 클로저 캡쳐 → `SelectTab(idx)`로 같은 index의 tabPanels 1개만 활성, 나머지 비활성. 출처: `Read Assets/SessionPanel/Scripts/TabPanelController.cs` (2026-05-26) + `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab` 라인 291-309.

- **`SessionPanelController.EnsurePanelInstance` 자동 wiring 패턴:** Volume/Rhythm Section Controller를 `GetComponentInChildren<...>(true)`로 panel 인스턴스 안에서 자동 탐색 후 `_volCtrl.InjectProvider(_activeInstrumentProviderObject)` / `_rhythmCtrl.Inject(provider, catalog)` 호출. `InstrumentTravelSectionController`도 같은 자동 탐색 패턴에 동참하면 panel 외부 wiring 추가 없이 동작. 출처: `Read Assets/SessionPanel/Scripts/SessionPanelController.cs` (2026-05-26) 라인 238-256.

- **`VolumeSectionController` lifecycle 패턴(Section Controller 의 표준):** `Awake`에서 self-instantiate(slider prefab) + `OnEnable/OnDisable`에서 provider event 구독/해제 + `InjectProvider(provider)` public method가 provider 교체 후 `OnActiveInstrumentChanged(provider.Current)` 즉시 호출. 출처: `Read Assets/SessionPanel/Scripts/VolumeSectionController.cs` (2026-05-26) 전체.

- **`RhythmGameSectionController` 그리드 인스턴스화 패턴:** `Instantiate(songRowPrefab, songListContent)` → `LayoutElement.preferredHeight=50f` 즉시 부여 → `SongRowUI.Setup(song, supported, callback)` 호출. 본 plan의 `InstrumentTravelItem` 인스턴스화도 동일 패턴을 답습한다. 출처: `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs` (2026-05-26) 라인 118-152.

- **`InstrumentTeleportLink` public API:**
  - public `InstrumentBase LinkedInstrument => linkedInstrument`(get-only property).
  - public `static event System.Action<InstrumentBase> AnyAnchorTeleported`.
  - `OnEnable`에서 `BaseTeleportationInteractable.teleporting`를 구독하고 `OnTeleporting`에서 static event 발화.
  - 본 plan은 `LinkedInstrument` 와 `GetComponent<TeleportationAnchor>()` 만 사용. **수정하지 않는다.**
  - 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentTeleportLink.cs` (2026-05-26) 전체.

- **`TeleportInstrumentProvider` public API:**
  - public `IActiveInstrument Current { get; }`, public `event System.Action<IActiveInstrument> ActiveInstrumentChanged`.
  - `OnEnable`에서 `InstrumentTeleportLink.AnyAnchorTeleported`을 구독, anchor 텔레포트 시 `Current` 자동 갱신 + `ActiveInstrumentChanged` 발화.
  - 비악기 구역(`linkedInstrument == null`) 진입 시 `Current = null`.
  - 본 plan은 `TestSceneSanyo.unity` 라인 6816에 부착돼 있는 `TeleportInstrumentProvider` 인스턴스를 `IActiveInstrumentProvider`로 주입받아 **읽기만** 한다. 수정하지 않는다.
  - 출처: `Read Assets/Instruments/_Core/Scripts/TeleportInstrumentProvider.cs` (2026-05-26) 전체.

- **`IActiveInstrument` 인터페이스 노출:** `Transform PanelAnchor`, `string InstrumentId`, `Transform InstrumentRoot`, `float InstanceVolume { get; set; }`. `InstrumentBase`가 구현. 본 plan은 "현재 위치 anchor 비교"에 `IActiveInstrument.InstrumentId` 또는 `InstrumentRoot` 참조 동등성을 사용한다. 출처: `Read Assets/Instruments/_Core/Scripts/IActiveInstrumentProvider.cs` (2026-05-26) + `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs` (2026-05-26) 라인 56·62-63.

- **`TeleportationAnchor.RequestTeleport()` 동작 (XRI):** 시그니처 `public void RequestTeleport() => SendTeleportRequest(null)`. UnityEvent에서도 호출 가능. `SendTeleportRequest`는 protected, 내부에서 `teleportationProvider`에 텔레포트 요청을 큐잉하고 `teleporting` UnityEvent를 발화 → 같은 GameObject의 `InstrumentTeleportLink.OnTeleporting`이 발화돼 `AnyAnchorTeleported` static event → `TeleportInstrumentProvider.Current` 갱신. 즉 **본 plan은 단일 `RequestTeleport()` 호출만으로 (a) 페이드/이행 (b) 활성 악기 전환 이벤트 (c) 도착 후 attach(예: `TromboneAnchor.OnLocomotionStarted` 페어로 자동) 3개 사슬을 모두 발동**시킨다. 출처: `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/Teleportation/TeleportationAnchor.cs` (2026-05-26) 라인 16-96 + `Read .../BaseTeleportationInteractable.cs` 라인 245(`teleporting`)·293(`SendTeleportRequest`).

- **`TromboneAnchor` attach/detach 동작 (외부 컴포넌트 side-effect 박제):** `Read Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs` (2026-05-26) 전체 — 본 plan이 호출하는 외부 API의 사슬을 박제한다.
  - `[RequireComponent(typeof(TeleportationAnchor))]` + `[DefaultExecutionOrder(10005)]`.
  - `OnEnable`에서 `m_Anchor.selectExited.AddListener(OnAnchorSelectExited)` + lazy `LocomotionProvider.locomotionStarted` 구독.
  - `OnAnchorSelectExited(args)`: `args.isCanceled==true`면 skip. 아니면 `m_PendingAttachFrame = Time.frameCount`. **즉, RequestTeleport 경로에선 selectExited가 발화 안 되므로 attach 발동 안 됨.**
  - `OnLocomotionStarted`: pending 윈도우(`k_PendingAttachWindowFrames=2`) 안이면 `AttachTromboneToMouth()` 호출, 윈도우 밖이면 (anchor 외부로 이동) `Detach()` 호출.
  - **본 plan의 함정: `TeleportationAnchor.RequestTeleport()`는 selectExited를 발화하지 않으므로 trombone attach 자동 트리거 사슬이 끊긴다.** 대응 → 본 plan의 `InstrumentTravelSectionController.RequestTeleport(anchor)`는 단순 `anchor.RequestTeleport()` 외에 `TromboneAnchor.AttachTromboneToMouth()` 같은 악기별 attach API를 직접 호출하지 않는다. 대신 (a) **단일 진입점 단일화 invariant(sub-spec 10 §Invariants 3번)**를 유지하기 위해 본 plan은 안전한 우회 경로를 추가하지 않고, "본 plan은 anchor `selectExited` + `locomotionStarted` 페어가 자동으로 attach를 발동시키는 기존 시퀀스를 우회하지 않는다"는 sub-spec 10 §Boundaries 박제대로 구현한다 — 즉 본 plan에서 attach 자동 트리거가 안 되는 경우는 **Open Issue로 sub-spec 10 §Open Questions에 기록하고 plan 2/2 또는 sub-spec 11에서 별도로 다룬다.**
  - **세부 합의:** 본 plan에서는 (i) `anchor.RequestTeleport()` 호출 → XRI Locomotion 페이드/이행 + `teleporting` UnityEvent → `InstrumentTeleportLink.AnyAnchorTeleported` → `TeleportInstrumentProvider.ActiveInstrumentChanged` 까지의 사슬은 확정 통과한다. (ii) `TromboneAnchor.AttachTromboneToMouth`가 자동 발동되지 않는 케이스는 manual-hard AC에서 검증해 만약 회귀이면 후속 plan/sub-spec으로 분리.

- **`SessionPanel.Runtime.asmdef` references:** `Instruments`, `RhythmGame.Data`, `RhythmGame.Runtime`, `Unity.InputSystem`, `Unity.XR.Interaction.Toolkit`, `Unity.TextMeshPro`. 본 plan이 import할 namespace: `UnityEngine.UI` / `UnityEngine` (autoReferenced), `TMPro` (`Unity.TextMeshPro` 있음), `Instruments` (있음), `UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation` (`Unity.XR.Interaction.Toolkit` 있음). **추가 reference 불필요.** 출처: `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef` (2026-05-26).

- **씬 측 wiring 박제:** `Assets/Scenes/TestSceneSanyo.unity` 라인 6816에 `Instruments::Instruments.TeleportInstrumentProvider` MonoBehaviour 존재. 라인 7298의 `_activeInstrumentProviderObject: {fileID: 1368118598}`이 그 인스턴스를 가리킴. 즉 `SessionPanelController._activeInstrumentProviderObject`는 이미 wired. 본 plan은 **씬을 수정하지 않는다** — `InstrumentTravelSectionController`도 `SessionPanelController`가 이미 wire한 `activeInstrumentProviderObject`를 새 public `Inject(provider)`로 받아 동작한다. 출처: `Grep TeleportInstrumentProvider Assets/Scenes/TestSceneSanyo.unity` (2026-05-26).

- **신규 C# 파일 위치 = `Assets/SessionPanel/Scripts/`:** 본 asmdef(`SessionPanel.Runtime`)가 같은 폴더 root이므로 신규 `InstrumentTravelSectionController.cs` / `InstrumentTravelItem.cs` 2 파일이 본 asmdef에 자동 포함된다. 별도 asmdef 추가 불필요. 출처: `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef` (2026-05-26).

- **신규 prefab 2종 위치 = `Assets/SessionPanel/Prefabs/`:** 기존 `SessionPanel.prefab`/`InstrumentToggleButton.prefab`가 같은 폴더. `InstrumentTravelItem.prefab` 신규 추가. 세 번째 섹션 panel 자체는 **별도 prefab으로 분리하지 않고 `SessionPanel.prefab` 안의 새 GameObject 노드**로 박제 — 이유: VolumePanel/RhythmGamePanel도 SessionPanel.prefab 안의 인라인 노드이며, TabPanelController가 GameObject reference로 wiring하므로 prefab 분리 시 nested prefab 경로 의존이 늘어 회귀 위험이 커진다. 출처: 동일 prefab(`SessionPanel.prefab`) 안 VolumePanel(fileID 5751264090231694390) + RhythmGamePanel(fileID 3286453783198637535)이 nested prefab이 아닌 인라인 자식 — `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab` 라인 1601 / 853.

- **`Tutorial/<악기명>/NN` 폴더 규약 (09 sub-spec 박제 → 10이 흡수):** 본 plan(1/2)에서는 **이 규약을 사용하지 않는다.** plan 2/2가 `InstrumentGuidePanel`을 만들 때 처음 진입점이 생긴다. 자산 폴더가 현재 `Assets/Tutorial`로 존재하지 않음(검증: `find Assets -type d -name Tutorial` 빈 결과). 출처: `Read docs/specs/session-panel/specs/09-instrument-guide-panel.md` (2026-05-26) §What + 디렉토리 부재 확인.

## Approach

### 1. 신규 C# 파일 `Assets/SessionPanel/Scripts/InstrumentTravelItem.cs`

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Instruments;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Instrument Travel Item")]
    public class InstrumentTravelItem : MonoBehaviour
    {
        [SerializeField] Image instrumentImage;          // 항목 상단 큰 식별 이미지
        [SerializeField] TextMeshProUGUI instrumentLabel; // 식별 보조 (instrumentId fallback)
        [SerializeField] Button travelButton;            // [이동]
        [SerializeField] Button guideButton;             // [가이드] (plan 2/2에서 동작 부착)

        InstrumentBase _instrument;
        Action<InstrumentTravelItem> _onTravel;
        Action<InstrumentTravelItem> _onGuide;
        bool _isCurrent;

        public InstrumentBase Instrument => _instrument;
        public bool IsCurrent => _isCurrent;

        public void Setup(InstrumentBase instrument,
                          Action<InstrumentTravelItem> onTravel,
                          Action<InstrumentTravelItem> onGuide)
        {
            _instrument = instrument;
            _onTravel = onTravel;
            _onGuide = onGuide;

            if (instrumentLabel != null)
                instrumentLabel.text = instrument != null ? instrument.InstrumentId : "(unknown)";

            if (travelButton != null)
            {
                travelButton.onClick.RemoveAllListeners();
                travelButton.onClick.AddListener(OnTravelClick);
            }

            if (guideButton != null)
            {
                guideButton.onClick.RemoveAllListeners();
                guideButton.onClick.AddListener(OnGuideClick);
            }
        }

        public void SetCurrent(bool isCurrent)
        {
            _isCurrent = isCurrent;
            if (travelButton != null)
                travelButton.interactable = !isCurrent;
        }

        void OnTravelClick()
        {
            if (_isCurrent) return;
            _onTravel?.Invoke(this);
        }

        void OnGuideClick()
        {
            // TODO sub-spec 10 plan 2/2: InstrumentGuidePanel.Open(_instrument.InstrumentId)
            _onGuide?.Invoke(this);
        }
    }
}
```

### 2. 신규 C# 파일 `Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Instruments;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Instrument Travel Section Controller")]
    public class InstrumentTravelSectionController : MonoBehaviour
    {
        [SerializeField] Transform itemContainer;          // GridLayoutGroup 부착된 자식 Content
        [SerializeField] GameObject instrumentTravelItemPrefab;
        [SerializeField] UnityEngine.Object activeInstrumentProviderObject;

        IActiveInstrumentProvider _provider;
        readonly List<InstrumentTravelItem> _items = new List<InstrumentTravelItem>();
        bool _snapshotBuilt;

        void Awake()
        {
            _provider = activeInstrumentProviderObject as IActiveInstrumentProvider;
        }

        void OnEnable()
        {
            if (_provider == null)
                _provider = activeInstrumentProviderObject as IActiveInstrumentProvider;
            if (_provider != null)
            {
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;
                _provider.ActiveInstrumentChanged += OnActiveInstrumentChanged;
            }
            BuildSnapshotIfNeeded();
            RefreshCurrentState(_provider?.Current);
        }

        void OnDisable()
        {
            if (_provider != null)
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;
            // 본 sub-spec 10 §What: "패널이 열려 있는 동안 변하지 않는다 — 패널을 닫고 다시 열면 새 상태"
            // OnDisable에서 스냅 비움 → OnEnable 시 재구축.
            ClearSnapshot();
        }

        /// <summary>SessionPanelController.EnsurePanelInstance 가 호출.</summary>
        public void Inject(UnityEngine.Object providerObj)
        {
            if (_provider != null)
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;

            activeInstrumentProviderObject = providerObj;
            _provider = providerObj as IActiveInstrumentProvider;

            if (_provider != null)
            {
                _provider.ActiveInstrumentChanged += OnActiveInstrumentChanged;
                if (isActiveAndEnabled)
                {
                    BuildSnapshotIfNeeded();
                    RefreshCurrentState(_provider.Current);
                }
            }
        }

        void BuildSnapshotIfNeeded()
        {
            if (_snapshotBuilt) return;
            if (itemContainer == null || instrumentTravelItemPrefab == null) return;

            ClearSnapshot();

            // sub-spec 10 §What·Invariants:
            // "씬에 존재하면서 TeleportationAnchor + InstrumentTeleportLink 동반"인 GameObject만 수집.
            var links = FindObjectsByType<InstrumentTeleportLink>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var link in links)
            {
                if (link == null) continue;
                var anchor = link.GetComponent<TeleportationAnchor>();
                if (anchor == null) continue;             // anchor 없는 link는 sub-spec 10 §Out of Scope
                var instrument = link.LinkedInstrument;
                if (instrument == null) continue;         // 비악기 구역(null link)도 노출 범위 밖

                var go = Instantiate(instrumentTravelItemPrefab, itemContainer);
                var item = go.GetComponent<InstrumentTravelItem>();
                if (item == null) { Destroy(go); continue; }

                var capturedAnchor = anchor;
                item.Setup(instrument,
                           onTravel: it => OnTravelRequested(it, capturedAnchor),
                           onGuide:  it => OnGuideRequested(it));
                _items.Add(item);
            }
            _snapshotBuilt = true;
        }

        void ClearSnapshot()
        {
            foreach (var item in _items)
                if (item != null) Destroy(item.gameObject);
            _items.Clear();
            _snapshotBuilt = false;
        }

        void OnActiveInstrumentChanged(IActiveInstrument instrument)
        {
            RefreshCurrentState(instrument);
        }

        void RefreshCurrentState(IActiveInstrument current)
        {
            foreach (var item in _items)
            {
                if (item == null || item.Instrument == null) continue;
                bool isCurrent = current != null &&
                                 ReferenceEquals(item.Instrument.InstrumentRoot, current.InstrumentRoot);
                item.SetCurrent(isCurrent);
            }
        }

        void OnTravelRequested(InstrumentTravelItem item, TeleportationAnchor anchor)
        {
            if (item == null || anchor == null) return;
            // sub-spec 10 §Invariants 3: 모든 텔레포트는 RequestTeleport 단일 진입점으로.
            // XRI Locomotion 페이드 + InstrumentTeleportLink.AnyAnchorTeleported + 도착 후 attach 사슬 자동.
            anchor.RequestTeleport();
        }

        void OnGuideRequested(InstrumentTravelItem item)
        {
            // TODO sub-spec 10 plan 2/2: InstrumentGuidePanel 호출 경로 부착.
            // 현재 plan에서는 무동작.
        }
    }
}
```

### 3. 신규 prefab `Assets/SessionPanel/Prefabs/InstrumentTravelItem.prefab`

루트 GameObject `InstrumentTravelItem` (RectTransform 100×120, anchor 그대로 — GridLayoutGroup이 부모에서 cellSize 강제):
- `Image` (배경, 둥근 사각형, raycastTarget=0 또는 1 자유)
- `LayoutElement.preferredWidth=100, preferredHeight=120` (GridLayoutGroup이 cellSize 강제하지만 fallback 안전성)
- `InstrumentTravelItem` MonoBehaviour 부착, 직렬화 필드 4종 (instrumentImage / instrumentLabel / travelButton / guideButton)
- 자식 `InstrumentImage` (Image, anchor (0,0.4)~(1,1), 항목 상단 60% 영역)
- 자식 `InstrumentLabel` (TextMeshProUGUI, anchor (0,0.3)~(1,0.4), `instrumentId` 표시 — 임시 식별)
- 자식 `TravelButton` (Button + Image + 자식 TMP "이동", anchor (0,0)~(0.5,0.3))
- 자식 `GuideButton` (Button + Image + 자식 TMP "가이드", anchor (0.5,0)~(1,0.3), OnClick 비어 있음)

> **시각 detail은 본 plan의 핵심이 아니다.** sub-spec 10 §What·Behavior가 요구하는 식별 가능성·[이동]·[가이드] 두 버튼 분리 + 한 행 3개 왼쪽 정렬만 충족하면 된다. 색상/sprite 정합은 본 plan에서 sub-spec 06 fix plan에서 박제된 sprite GUID(`30dbb1d077de4bb7b01b367491dbae50` ButtonRoundedRect)를 재사용 가능 — 그러나 본 plan은 시각 polish를 명시적 목표로 잡지 않으므로 plain Image fallback도 허용.

### 4. `SessionPanel.prefab` 수정

(a) **TabBar 자식 추가 — TravelTabButton:**
TabBar(GameObject 1324976161269021399) RT(fileID 4251547738239070682)의 `m_Children` 리스트에 신규 `TravelTabButton` 자식 1개 추가. 기존 VolumeTabButton(585174415138494519) / RhythmTabButton(1370652255947522305)과 동일 구조 답습:
- GameObject `TravelTabButton`, 자식 `Label`(TMP "악기")
- RectTransform anchor (0,0)~(0,0) sizeDelta(0,0) pivot(0.5,0.5) — HorizontalLayoutGroup이 폭 강제
- Button + Image (color (0.2, 0.25, 0.4, 0.85)) — 기존 두 탭 사양 동일
- HorizontalLayoutGroup의 `ChildForceExpandWidth=1`이라 자식 3개 균등 분배.

(b) **SessionPanel 루트 자식 추가 — InstrumentTravelPanel:**
SessionPanel(GameObject 1151033334272281024) RT(fileID 4247339568228231875)의 `m_Children` 리스트 끝에 신규 `InstrumentTravelPanel` 자식 1개 추가:
- GameObject `InstrumentTravelPanel`, `m_IsActive: 0` (TabPanelController.SelectTab이 활성 1개만 켜는 invariant 준수 — `defaultTab: 0`이라 Volume 켜짐)
- RectTransform anchor (0,0)~(1,1), anchoredPosition (0,-20), sizeDelta (0,-40) — VolumePanel/RhythmGamePanel과 동일 (TabBar 영역 회피)
- 자식 GameObject `Content` (RectTransform full-stretch sizeDelta(0,0) anchoredPosition(0,0))
  - `GridLayoutGroup` 부착 — `cellSize=(120,140)`(폭 100×아이템 + 좌우 margin 약간), `spacing=(8,8)`, `startCorner=UpperLeft`(enum index 0), `startAxis=Horizontal`(0), `childAlignment=UpperLeft`(0), `constraint=FixedColumnCount`(2), `constraintCount=3`. **enum 박제:** `UnityEngine.UI.GridLayoutGroup.Corner` { UpperLeft=0, UpperRight=1, LowerLeft=2, LowerRight=3 }, `Axis` { Horizontal=0, Vertical=1 }, `Constraint` { Flexible=0, FixedColumnCount=1, FixedRowCount=2 } — `Read Library/PackageCache/com.unity.ugui@*/Runtime/UI/Core/GridLayoutGroup.cs`(공개 UGUI 사양, Unity 6 공식). 본 plan 의도 값: childAlignment=`UpperLeft`(=enum `TextAnchor.UpperLeft`, UnityEngine.TextAnchor.UpperLeft=0), constraint=FixedColumnCount=1, constraintCount=3.
- `Content` GameObject에 `InstrumentTravelSectionController` MonoBehaviour 부착, 직렬화 필드 `itemContainer`={fileID Content RT}, `instrumentTravelItemPrefab`={fileID InstrumentTravelItem prefab root}, `activeInstrumentProviderObject={fileID: 0}` (`SessionPanelController.EnsurePanelInstance`가 Inject로 wiring).

(c) **TabPanelController 직렬화 수정 (fileID 7754837678443973032):**
- `tabButtons` 리스트에 신규 TravelTabButton의 Button MonoBehaviour fileID를 3번째 항목으로 append.
- `tabPanels` 리스트에 신규 InstrumentTravelPanel GameObject fileID를 3번째 항목으로 append.
- `defaultTab: 0` 유지 (시작은 Volume 탭).

### 5. `SessionPanelController` 자동 wiring 부착 (`Assets/SessionPanel/Scripts/SessionPanelController.cs` 수정)

`EnsurePanelInstance()`에 다음 블록 1개 추가 (기존 _volCtrl / _rhythmCtrl wiring 직후):

```csharp
var travelCtrl = _panelInstance.GetComponentInChildren<InstrumentTravelSectionController>(true);
if (travelCtrl != null && _activeInstrumentProviderObject != null)
    travelCtrl.Inject(_activeInstrumentProviderObject);
```

### 6. 검증

- **컴파일 대기:** `editor_state.isCompiling==false` 폴링 후 `read_console types=["error"]` 0건 확인 (`.claude/skills/unity-mcp-workflow/SKILL.md`).
- **시각 검증(manual-hard):** TestSceneSanyo 로드 → SessionPanel pinch open → 세 번째 탭 클릭 → 그리드 표시 확인 + 한 항목 [이동] 클릭 → 페이드 + 도착 확인. Trombone anchor 항목일 경우 attach까지 확인. attach 자동 트리거가 안 되면 sub-spec 10 §Open Questions에 기록.

## Deliverables

- `Assets/SessionPanel/Scripts/InstrumentTravelItem.cs` (신규) — 항목 UI 컴포넌트.
- `Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` (신규) — 섹션 진입 MonoBehaviour, 스냅 + [이동] 라우팅.
- `Assets/SessionPanel/Prefabs/InstrumentTravelItem.prefab` (신규) — 항목 prefab.
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab` (수정) — TabBar에 TravelTabButton 자식 + SessionPanel 루트에 InstrumentTravelPanel 자식(Content + GridLayoutGroup + InstrumentTravelSectionController) + TabPanelController `tabButtons[]`/`tabPanels[]` 3번째 항목 append.
- `Assets/SessionPanel/Scripts/SessionPanelController.cs` (수정) — `EnsurePanelInstance`에 `InstrumentTravelSectionController.Inject` 자동 호출 블록 추가.

## Acceptance Criteria

- [ ] `[auto-hard]` 신규 C# 파일 2종이 컴파일 0 error.
  **검증:** MCP `read_console action=get types=["error"]` 0건. 절차: 파일 추가 후 `editor_state.isCompiling==false` 폴링 → console read.
- [ ] `[auto-hard]` `InstrumentTravelSectionController.cs`가 의도한 public API/serialized fields를 노출한다.
  **검증:** `Grep -n "public class InstrumentTravelSectionController\|public void Inject\|\[SerializeField\] Transform itemContainer\|\[SerializeField\] GameObject instrumentTravelItemPrefab\|\[SerializeField\] UnityEngine\.Object activeInstrumentProviderObject" Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` — 5개 패턴 모두 1건 이상 매치.
- [ ] `[auto-hard]` `InstrumentTravelItem.cs`가 의도한 public API/serialized fields를 노출한다.
  **검증:** `Grep -n "public class InstrumentTravelItem\|public InstrumentBase Instrument\|public void Setup\|public void SetCurrent\|\[SerializeField\] Image instrumentImage\|\[SerializeField\] Button travelButton\|\[SerializeField\] Button guideButton" Assets/SessionPanel/Scripts/InstrumentTravelItem.cs` — 7개 패턴 모두 1건 이상 매치.
- [ ] `[auto-hard]` anchor 스냅 로직이 `TeleportationAnchor` + `InstrumentTeleportLink` 동반 GameObject만 수집한다는 점이 코드에서 보인다.
  **검증:** `Grep -n "FindObjectsByType<InstrumentTeleportLink>\|GetComponent<TeleportationAnchor>" Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` — 두 패턴 모두 매치 + 같은 메서드(`BuildSnapshotIfNeeded` 등) 안에서 `link.GetComponent<TeleportationAnchor>()`의 null 분기로 early `continue`가 들어가 있는지 시각 확인.
- [ ] `[auto-hard]` [이동] 클릭이 `TeleportationAnchor.RequestTeleport()` 단일 호출만으로 처리된다(우회 경로 부재).
  **검증:** `Grep -n "RequestTeleport()" Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` 1건 매치 + `Grep -nE "AttachTromboneToMouth|Detach\(\)|SendTeleportRequest" Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs Assets/SessionPanel/Scripts/InstrumentTravelItem.cs` 0건 (단일 진입점 invariant).
- [ ] `[auto-hard]` `SessionPanel.prefab` TabBar(RT fileID 4251547738239070682) 자식 리스트에 신규 `TravelTabButton` 노드가 추가된다.
  **검증:** `Grep -n "m_Name: TravelTabButton" Assets/SessionPanel/Prefabs/SessionPanel.prefab` 1건 매치. 또한 RT 4251547738239070682 블록(라인 340-360 부근)의 `m_Children` 리스트 길이가 3(`585174415138494519`, `1370652255947522305`, 신규)으로 확장됐는지 확인.
- [ ] `[auto-hard]` `SessionPanel.prefab` 루트 RectTransform(fileID 4247339568228231875) 자식 리스트에 신규 `InstrumentTravelPanel` 노드가 추가되고, 같은 prefab에 `InstrumentTravelSectionController` MonoBehaviour가 직렬화된다.
  **검증:** `Grep -n "m_Name: InstrumentTravelPanel" Assets/SessionPanel/Prefabs/SessionPanel.prefab` 1건 매치 + `Grep -n "SessionPanel.InstrumentTravelSectionController" Assets/SessionPanel/Prefabs/SessionPanel.prefab` 1건 매치 + 같은 prefab에 `UnityEngine.UI::UnityEngine.UI.GridLayoutGroup` 1건 매치 (Content 노드).
- [ ] `[auto-hard]` `TabPanelController` 직렬화의 `tabButtons` / `tabPanels` 리스트가 3 항목으로 확장된다 (`defaultTab: 0` 유지).
  **검증:** `SessionPanel.prefab`의 TabPanelController MonoBehaviour 블록(fileID 7754837678443973032, 라인 291-309 부근)을 Read해 `tabButtons:` 리스트가 3개 항목 + `tabPanels:` 리스트가 3개 항목 + `defaultTab: 0`. fileID 7387538153525603796, 5857529928549192149, (신규 Travel Button fileID) 3종 / 5751264090231694390, 3286453783198637535, (신규 InstrumentTravelPanel GameObject fileID) 3종.
- [ ] `[auto-hard]` `InstrumentTravelItem.prefab` 신규 파일이 존재하고, 루트 GameObject가 `InstrumentTravelItem` 컴포넌트 + `Image` 자식 + `Button` 자식 2개 (TravelButton / GuideButton) 를 가진다.
  **검증:** `ls Assets/SessionPanel/Prefabs/InstrumentTravelItem.prefab` 성공 + `Grep -n "m_Name: TravelButton\|m_Name: GuideButton\|m_Name: InstrumentImage\|SessionPanel.InstrumentTravelItem" Assets/SessionPanel/Prefabs/InstrumentTravelItem.prefab` — 4개 패턴 모두 매치.
- [ ] `[auto-hard]` `SessionPanelController.EnsurePanelInstance` 안에 `InstrumentTravelSectionController` 자동 wiring 블록이 들어 있다.
  **검증:** `Grep -n "InstrumentTravelSectionController\|travelCtrl" Assets/SessionPanel/Scripts/SessionPanelController.cs` — 2개 패턴 모두 1건 이상 매치.
- [ ] `[auto-soft]` `InstrumentTravelPanel`/Content의 GridLayoutGroup 직렬화 값이 의도(`cellSize~=(120,140)` / `spacing=(8,8)` / `startCorner=0` / `startAxis=0` / `childAlignment=0(UpperLeft)` / `constraint=1(FixedColumnCount)` / `constraintCount=3`)와 일치 — Layout 정합(한 행 3개, 왼쪽 정렬).
  **검증:** `Grep -nA 20 "GridLayoutGroup" Assets/SessionPanel/Prefabs/SessionPanel.prefab`에서 블록 내 `m_Constraint: 1`, `m_ConstraintCount: 3`, `m_ChildAlignment: 0`, `m_StartCorner: 0`, `m_StartAxis: 0` 모두 매치. 불일치 시 Notes에 기록하고 다음 plan(2/2 또는 후속 polish plan)에서 보정.
- [ ] `[auto-soft]` 본 plan은 `TromboneAnchor` / `InstrumentTeleportLink` / `TeleportInstrumentProvider` / `InstrumentBase` C# 소스를 수정하지 않는다(읽기만).
  **검증:** `git status` 또는 `git diff --name-only` 결과에 `Assets/Instruments/**/*.cs` 0건. `Assets/Scenes/*.unity` 도 0건.
- [ ] `[manual-hard]` Editor에서 `Assets/Scenes/TestSceneSanyo.unity` 로드 → 패널 호출(왼손 핀치 또는 매핑 input) → 세 번째 탭 클릭 → 그리드에 anchor 보유 악기 N개가 한 행 3개 왼쪽 정렬로 표시된다. 항목 [이동] 클릭 시 페이드 후 그 anchor 위치·각도에 도달하고, **트롬본 anchor 항목의 경우 입에 자동 attach** 된다(or attach가 누락되면 attach 자동 트리거 사슬 끊김으로 Notes에 기록 + sub-spec 10 §Open Questions로 escalation).
  **검증:** TestSceneSanyo 로드 → 시작 위치에서 세 번째 탭 진입 → trombone 항목 [이동] 클릭 → trombone anchor 도착 + 입에 트롬본 attach 확인 + `PlayHandPoseDriver` source override 적용 확인. 1회 스크린샷 + 1회 영상(or 사용자 직접 시뮬레이션) 검토.
- [ ] `[manual-hard]` 잡은 상태에서 다른 항목 [이동] 클릭 시 (i) 새 anchor 도착 (ii) 도착 후 새 anchor가 attach형이면 attach (iii) 떠나는 anchor가 attach형이면 자동 detach (잡고 있던 악기가 원위치로 복귀).
  **검증:** TestSceneSanyo에서 trombone anchor 도착 → 입에 attach 확인 → 본 섹션에서 piano 항목 [이동] 클릭 → trombone 본체가 원위치로 복귀(`TromboneAnchor.Detach` 발화 — `OnLocomotionStarted` pending 윈도우 밖 분기) + piano anchor 도착. 사용자 시각 검증.
- [ ] `[manual-hard]` 현재 위치한 anchor의 항목 [이동] 버튼은 비활성(`travelButton.interactable == false`로 회색)으로 보이고, 클릭해도 무반응. 같은 항목의 [가이드] 버튼은 현재 plan에서는 시각만 존재하고 클릭 시 무동작(또는 콘솔 로그 없이 조용히 종료).
  **검증:** 어느 anchor에 도달한 직후 본 섹션을 재오픈 → 해당 항목 [이동] 버튼이 비활성 회색으로 보임 + 클릭 무반응. 같은 항목 [가이드] 버튼은 활성 회색이 아니나 클릭 시 별다른 변화 없음. 사용자 시각 검증.

## Out of Scope

- 항목 [가이드] 버튼 클릭 동작(독립 가이드 패널 호출) — **plan 2/2**.
- `InstrumentGuidePanel.prefab` / `InstrumentGuidePanel.cs` 신규 — **plan 2/2**.
- `Tutorial/<악기명>/NN` 자산 로딩, 페이지 Prev/Next, 페이지 텍스트 콘텐츠 — **plan 2/2** + 추후 콘텐츠 작업.
- 멀티플레이 환경에서 다른 사용자 위치·점유 상태 항목 반영 — sub-spec 10 §Out of Scope.
- anchor 없는 악기 표현, 동적 악기 추가/제거 즉시 반영 — sub-spec 10 §Out of Scope.
- 항목 시각 polish(악기 이미지 자산, 둥근 사각형 sprite, 색상 톤) — 본 plan은 기능 박제 우선. 시각 polish는 후속 plan 후보로 Notes에 기록.
- `TromboneAnchor`/`PianoAnchor` 등 악기별 attach 로직 변경 — sub-spec 10 §Boundaries "건드리지 않는다".
- 씬(`TestSceneSanyo.unity` 등) 수정 — 본 plan은 prefab 수정 + 신규 스크립트 추가만.

## Notes

- **Trombone attach 자동 트리거의 잠재적 단절:** `TeleportationAnchor.RequestTeleport()`는 `SendTeleportRequest(null)` → `teleporting` UnityEvent 발화 + locomotionStarted 발화는 보장하지만 `selectExited`는 발화하지 않는다(interactor 없이 직접 호출이라). `TromboneAnchor.OnAnchorSelectExited`가 pending 윈도우를 set하지 못하면 `OnLocomotionStarted`가 pending 윈도우 밖 분기로 빠져 detach 경로로 흘러갈 수 있다. **이 케이스는 manual-hard AC에서 실측**한 뒤 (i) 정상 attach: plan 2/2로 진행 (ii) attach 누락: `InstrumentTravelSectionController.OnTravelRequested`에서 `RequestTeleport` 호출 직전 `anchor.GetComponent<TromboneAnchor>()` 같은 악기별 anchor 컴포넌트를 짚어 "직접 attach API" 보조 호출하거나, 별도 sub-spec(11)로 escalation. 본 plan에서는 단일 진입점 invariant 유지가 우선.
- **`FindObjectsByType<InstrumentTeleportLink>`** 사용은 Unity 6의 `FindObjectsByType(FindObjectsInactive, FindObjectsSortMode)` 권장 API다(legacy `FindObjectsOfType`는 deprecated 경고). `FindObjectsInactive.Exclude` + `FindObjectsSortMode.None`로 hot path가 아닌 1회 스냅 호출.
- **GridLayoutGroup vs HorizontalLayoutGroup 행 wrap:** 한 행 3개 왼쪽 정렬 + 행 자동 추가는 GridLayoutGroup의 `Constraint=FixedColumnCount(=1)` + `ConstraintCount=3`로 결정적. HorizontalLayoutGroup만으로는 자동 줄바꿈 없음.
- **항목 식별 텍스트(`instrumentLabel`)** 는 임시 식별 수단이며 본 plan에서는 `instrument.InstrumentId` 문자열 그대로 표시. sub-spec 10 §What이 "악기 이미지가 식별을 겸한다"이므로 후속 polish plan에서 라벨을 숨기거나 보조 텍스트로만 사용 검토 가능.
- **후속 plan 후보:**
  - **plan 2/2 (sub-spec 10):** `InstrumentGuidePanel.prefab/cs` + Tutorial 폴더 로더 + 항목 [가이드] OnClick wiring.
  - **(후속 polish):** 항목 prefab 시각(악기 이미지 자산, sprite 톤, 색상). sub-spec 06 fix plan의 `ButtonRoundedRect`(GUID `30dbb1d077de4bb7b01b367491dbae50`) 재참조 가능.
  - **(잠재적 sub-spec 11):** `RequestTeleport()` 단일 호출에서 attach 자동 트리거가 누락되는 경우 악기별 attach API 직접 호출 보조 경로 또는 `TromboneAnchor`의 pending 윈도우 설정 보조 진입점 신설.

## Handoff

- **2026-05-26 implementer 종료 시점** — 신규 진입점: `InstrumentTravelSectionController.OnGuideRequested(InstrumentTravelItem)` (현재 무동작). plan 2/2가 이 메서드 안에서 `InstrumentGuidePanel.Open(item.Instrument.InstrumentId)` 경로를 연결한다.
- 신규 자산 GUID:
  - `InstrumentTravelItem.prefab`: `2917de9c7dc9f41f3929e478aab1f9f8`
  - `InstrumentTravelSectionController.cs`: `f6fc1b52b4904498cae4940cb97474a7`
  - `InstrumentTravelItem.cs`: `086c88c56ee1d4202bbb310c52a15153`
- 자동 회귀: EditMode 133/133 / PlayMode 4/4 / Unity console error 0건 (2026-05-26 unity-test-runner).
- **manual-hard 미실시(deferred)** — 2026-05-26 시점에 Unity Editor에서 직접 검증 가능한 환경이 아님. M1(세 번째 탭 그리드 + [이동] → 페이드/도착/attach), M2(잡힘 상태에서 다른 항목 [이동] → 자동 detach + 새 attach), M3(현재 anchor 비활성 / [가이드] 무동작) 3건 모두 후속 테스트 환경에서 실시 예정. plan 상태는 `In Progress` 유지, sub-spec 10 표도 `In Progress` 유지. commit 보류.
