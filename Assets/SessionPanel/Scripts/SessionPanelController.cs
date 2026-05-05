using UnityEngine;
using UnityEngine.InputSystem;
using Instruments;

namespace SessionPanel
{
    public class SessionPanelController : MonoBehaviour
    {
        private enum PanelState { Hidden, PinchOpened, InstrumentOpened }

        [SerializeField] private GameObject panelPrefab;
        [SerializeField] private Transform leftHandSpawnTransform;
        [SerializeField] private Vector3 pinchSpawnLocalOffset = new Vector3(0f, 0.05f, 0.15f);
        [SerializeField] private UnityEngine.Object _activeInstrumentProviderObject;
        [SerializeField] private InputActionReference panelToggleAction;

        private PanelState _state = PanelState.Hidden;
        private GameObject _panelInstance;
        private Transform _startMenuContainer;
        private Transform _volumeContainer;
        private IActiveInstrumentProvider _provider;
        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _provider = _activeInstrumentProviderObject as IActiveInstrumentProvider;
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
                TransitionTo(PanelState.InstrumentOpened);
            }
            else
            {
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
                    _panelInstance.SetActive(false);
                    break;

                case PanelState.PinchOpened:
                    PositionAtWrist();
                    _startMenuContainer.gameObject.SetActive(false);
                    _volumeContainer.gameObject.SetActive(true);
                    _panelInstance.SetActive(true);
                    break;

                case PanelState.InstrumentOpened:
                    PositionAtInstrument();
                    _startMenuContainer.gameObject.SetActive(true);
                    _volumeContainer.gameObject.SetActive(true);
                    _panelInstance.SetActive(true);
                    break;
            }
        }

        private void EnsurePanelInstance()
        {
            if (_panelInstance != null) return;

            _panelInstance = Instantiate(panelPrefab);
            _startMenuContainer = FindChildByName(_panelInstance.transform, "StartMenuSectionContainer");
            _volumeContainer = FindChildByName(_panelInstance.transform, "VolumeSectionContainer");
        }

        private void PositionAtWrist()
        {
            if (leftHandSpawnTransform == null) return;

            Vector3 worldPos = leftHandSpawnTransform.TransformPoint(pinchSpawnLocalOffset);
            _panelInstance.transform.position = worldPos;

            if (_mainCamera != null)
            {
                Vector3 camForward = _mainCamera.transform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.001f)
                    _panelInstance.transform.rotation = Quaternion.LookRotation(camForward);
            }
        }

        private void PositionAtInstrument()
        {
            if (_provider?.Current == null) return;

            Transform anchor = _provider.Current.PanelAnchor;
            _panelInstance.transform.position = anchor.position;
            _panelInstance.transform.rotation = anchor.rotation;
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
            }
            return null;
        }
    }
}
