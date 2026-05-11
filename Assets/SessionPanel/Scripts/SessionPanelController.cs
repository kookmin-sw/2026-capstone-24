using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.InputSystem;
using Instruments;

namespace SessionPanel
{
    public class SessionPanelController : MonoBehaviour
    {
        private enum PanelState { Hidden, PinchOpened, InstrumentOpened }

        [SerializeField] private GameObject panelPrefab;
        [SerializeField] private Transform leftHandSpawnTransform;
        [SerializeField] private Vector3 pinchSpawnLocalOffset = new Vector3(0f, 0.08f, 0.1f);
        [SerializeField] private Transform headFallbackTransform;
        [SerializeField] private Vector3 fallbackSpawnLocalOffset = new Vector3(0f, 0f, 0.6f);
        [SerializeField] private float trackingEpsilon = 0.001f;
        [SerializeField] private UnityEngine.Object _activeInstrumentProviderObject;
        [SerializeField] private UnityEngine.Object _songCatalogObject;
        [SerializeField] private InputActionReference panelToggleAction;
        // 리듬게임 중 비활성화할 인터랙터 GO 목록 (런타임에 자동 수집)
        readonly List<GameObject> _interactorObjects = new List<GameObject>();

        private PanelState _state = PanelState.Hidden;
        private GameObject _panelInstance;
        private IActiveInstrumentProvider _provider;
        private Camera _mainCamera;
        private bool _trackInstrument;
        private VolumeSectionController _volCtrl;
        private RhythmGameSectionController _rhythmCtrl;
        private bool _hiddenByGame;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _provider = _activeInstrumentProviderObject as IActiveInstrumentProvider;
            CollectInteractors();
        }

        private void Start()
        {
            // 패널은 시작 시 Hidden → 레이저도 꺼둠
            SetInteractorsActive(false);
        }

        private void CollectInteractors()
        {
            _interactorObjects.Clear();
            // NearFarInteractor (양 손 UI 레이)만 수집 — 텔레포트(XRRayInteractor)는 제외
            foreach (var c in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                _interactorObjects.Add(c.gameObject);
        }

        private void OnEnable()
        {
            if (panelToggleAction != null)
            {
                panelToggleAction.action.Enable();
                panelToggleAction.action.performed += OnPanelToggle;
            }

            if (_provider != null)
                _provider.ActiveInstrumentChanged += OnActiveInstrumentChanged;
        }

        private void OnDisable()
        {
            if (panelToggleAction != null)
                panelToggleAction.action.performed -= OnPanelToggle;

            if (_provider != null)
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;
        }

        private void OnPanelToggle(InputAction.CallbackContext ctx)
        {
            switch (_state)
            {
                case PanelState.Hidden:
                    if (_provider == null || _provider.Current == null)
                        TransitionTo(PanelState.PinchOpened);
                    else
                        TransitionTo(PanelState.InstrumentOpened);
                    break;
                case PanelState.PinchOpened:
                case PanelState.InstrumentOpened:
                    TransitionTo(PanelState.Hidden);
                    break;
            }
        }

        private void OnActiveInstrumentChanged(IActiveInstrument instrument)
        {
            if (instrument != null)
            {
                if (_state == PanelState.Hidden)
                {
                    _trackInstrument = true;
                    TransitionTo(PanelState.InstrumentOpened);
                }
                else
                {
                    _trackInstrument = false;
                    _state = PanelState.InstrumentOpened;
                }
            }
            else
            {
                _trackInstrument = false;
                if (_state != PanelState.Hidden)
                    TransitionTo(PanelState.Hidden);
            }
        }

        private void TransitionTo(PanelState next)
        {
            _state = next;
            EnsurePanelInstance();

            switch (next)
            {
                case PanelState.Hidden:
                    _trackInstrument = false;
                    _panelInstance.SetActive(false);
                    SetInteractorsActive(false);
                    break;

                case PanelState.PinchOpened:
                    _trackInstrument = false;
                    PositionAtWrist();
                    _panelInstance.SetActive(true);
                    SetInteractorsActive(true);
                    break;

                case PanelState.InstrumentOpened:
                    _trackInstrument = true;
                    PositionAtInstrument();
                    _panelInstance.SetActive(true);
                    SetInteractorsActive(true);
                    if (_volCtrl != null && _activeInstrumentProviderObject != null)
                        _volCtrl.InjectProvider(_activeInstrumentProviderObject);
                    break;
            }
        }

        private void LateUpdate()
        {
            if (_panelInstance == null || !_panelInstance.activeSelf) return;
            if (_state == PanelState.InstrumentOpened && _trackInstrument)
                PositionAtInstrument();
        }

        private void EnsurePanelInstance()
        {
            if (_panelInstance != null) return;

            _panelInstance = Instantiate(panelPrefab);

            _volCtrl = _panelInstance.GetComponentInChildren<VolumeSectionController>(true);
            if (_volCtrl != null && _activeInstrumentProviderObject != null)
                _volCtrl.InjectProvider(_activeInstrumentProviderObject);

            _rhythmCtrl = _panelInstance.GetComponentInChildren<RhythmGameSectionController>(true);
            if (_rhythmCtrl != null)
            {
                _rhythmCtrl.Inject(_activeInstrumentProviderObject, _songCatalogObject);
                _rhythmCtrl.GameStarted += OnRhythmGameStarted;
                _rhythmCtrl.GameEnded   += OnRhythmGameEnded;
            }
        }

        private void OnRhythmGameStarted()
        {
            if (_panelInstance != null && _panelInstance.activeSelf)
            {
                _hiddenByGame = true;
                _panelInstance.SetActive(false);
            }
            SetInteractorsActive(false);
        }

        private void OnRhythmGameEnded()
        {
            if (_hiddenByGame && _panelInstance != null)
            {
                _hiddenByGame = false;
                _panelInstance.SetActive(true);
                SetInteractorsActive(true);   // 패널이 복원될 때만 레이저 켬
            }
        }

        private void SetInteractorsActive(bool active)
        {
            foreach (var go in _interactorObjects)
                if (go != null) go.SetActive(active);
        }

        private void PositionAtWrist()
        {
            if (_mainCamera == null) return;

            Vector3 horizontalForward = _mainCamera.transform.forward;
            horizontalForward.y = 0f;
            if (horizontalForward.sqrMagnitude < 0.001f)
                horizontalForward = Vector3.forward;
            else
                horizontalForward.Normalize();

            _panelInstance.transform.position = _mainCamera.transform.position
                                                + horizontalForward * fallbackSpawnLocalOffset.z;
            _panelInstance.transform.rotation = Quaternion.LookRotation(horizontalForward);
        }

        private void PositionAtInstrument()
        {
            if (_provider?.Current == null) return;

            Transform anchor = _provider.Current.PanelAnchor;
            _panelInstance.transform.position = anchor.position;

            if (_mainCamera != null)
            {
                Vector3 awayFromCam = anchor.position - _mainCamera.transform.position;
                awayFromCam.y = 0f;
                if (awayFromCam.sqrMagnitude > 0.001f)
                    _panelInstance.transform.rotation = Quaternion.LookRotation(awayFromCam.normalized);
                else
                    _panelInstance.transform.rotation = anchor.rotation;
            }
            else
            {
                _panelInstance.transform.rotation = anchor.rotation;
            }
        }
    }
}
