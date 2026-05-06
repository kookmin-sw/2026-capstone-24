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
        [SerializeField] private Vector3 pinchSpawnLocalOffset = new Vector3(0f, 0.08f, 0.1f);
        [SerializeField] private Transform headFallbackTransform;
        [SerializeField] private Vector3 fallbackSpawnLocalOffset = new Vector3(0f, 0f, 0.6f);
        [SerializeField] private float trackingEpsilon = 0.001f;
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
                    // 카메라 눈 높이 + 수평 전방 기준으로 1회 spawn 후 world-lock
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

        private void LateUpdate()
        {
            if (_panelInstance == null || !_panelInstance.activeSelf) return;

            // InstrumentOpened만 매 프레임 업데이트 (악기가 움직이면 PanelAnchor를 따라옴)
            // PinchOpened는 spawn 시 1회 위치·각도 고정 → world-lock (업데이트 없음)
            if (_state == PanelState.InstrumentOpened)
                PositionAtInstrument();
        }

        private void EnsurePanelInstance()
        {
            if (_panelInstance != null) return;

            _panelInstance = Instantiate(panelPrefab);
            _startMenuContainer = FindChildByName(_panelInstance.transform, "StartMenuSectionContainer");
            _volumeContainer    = FindChildByName(_panelInstance.transform, "VolumeSectionContainer");
        }

        private void PositionAtWrist()
        {
            // PinchOpened spawn 위치:
            // L_Wrist는 핸드 트래킹 미동작 시 rest position(손목 높이)에 고정돼
            // 눈 높이와 맞지 않으므로 카메라를 기준점으로 사용.
            // 카메라(눈) 높이에서 수평 전방 fallbackSpawnLocalOffset.z(0.5m)에 spawn.
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
