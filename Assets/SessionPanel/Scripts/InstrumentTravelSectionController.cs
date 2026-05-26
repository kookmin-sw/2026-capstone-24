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
        [SerializeField] GameObject instrumentGuidePanelPrefab;

        IActiveInstrumentProvider _provider;
        readonly List<InstrumentTravelItem> _items = new List<InstrumentTravelItem>();
        bool _snapshotBuilt;
        InstrumentGuidePanelController _guidePanel;

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
            // 세션 패널이 닫히면 가이드 패널도 함께 비활성 (인스턴스는 보존, 다음 open 시 재사용).
            if (_guidePanel != null) _guidePanel.gameObject.SetActive(false);
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
                    EnsureGuidePanel();
                    BuildSnapshotIfNeeded();
                    RefreshCurrentState(_provider.Current);
                }
            }
        }

        void EnsureGuidePanel()
        {
            if (_guidePanel != null) return;
            if (instrumentGuidePanelPrefab == null) return;

            // 세션 패널 외부의 독립 root — 세션 패널이 닫혀도 가이드 패널이 독립 위치 유지 가능.
            var go = Instantiate(instrumentGuidePanelPrefab);
            _guidePanel = go.GetComponent<InstrumentGuidePanelController>();
            go.SetActive(false);
            PositionGuidePanel(go.transform);
        }

        void PositionGuidePanel(Transform t)
        {
            // SessionPanel 루트(= InstrumentTravelSectionController 의 transform.root)의
            // 오른쪽으로 0.5m 오프셋, 동일 정면 방향 1회 스냅.
            var sessionRoot = transform.root;
            if (sessionRoot == null) return;
            t.position = sessionRoot.position + sessionRoot.right * 0.5f;
            t.rotation = sessionRoot.rotation;
            t.localScale = new Vector3(0.001f, 0.001f, 1f);
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
            if (item == null || item.Instrument == null) return;
            EnsureGuidePanel();
            if (_guidePanel == null) return;
            // 클릭 시점마다 위치 재스냅 — 사용자가 이동/회전했을 수 있다.
            PositionGuidePanel(_guidePanel.transform);
            _guidePanel.Open(item.Instrument);
        }
    }
}
