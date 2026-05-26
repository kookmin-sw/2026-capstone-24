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
