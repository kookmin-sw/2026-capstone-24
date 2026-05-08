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
        private bool _trackInstrument;
        private VolumeSectionController _volCtrl;

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
                if (_state == PanelState.Hidden)
                {
                    // 패널이 닫혀 있을 때: 악기 위치에 auto-open + 이후 tracking 활성화
                    _trackInstrument = true;
                    TransitionTo(PanelState.InstrumentOpened);
                }
                else
                {
                    // 패널이 이미 열려 있을 때: 위치를 유지하고 콘텐츠만 업데이트
                    // (PinchOpened → InstrumentOpened 상태 승격, 위치 이동 없음)
                    _trackInstrument = false;
                    _state = PanelState.InstrumentOpened;
                    EnsurePanelInstance();
                    if (_startMenuContainer != null)
                        _startMenuContainer.gameObject.SetActive(true);
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
                    break;

                case PanelState.PinchOpened:
                    _trackInstrument = false;
                    // 카메라 눈 높이 + 수평 전방 기준으로 1회 spawn 후 world-lock
                    PositionAtWrist();
                    _startMenuContainer.gameObject.SetActive(false);
                    _volumeContainer.gameObject.SetActive(true);
                    _panelInstance.SetActive(true);
                    break;

                case PanelState.InstrumentOpened:
                    _trackInstrument = true;
                    PositionAtInstrument();
                    _startMenuContainer.gameObject.SetActive(true);
                    _volumeContainer.gameObject.SetActive(true);
                    _panelInstance.SetActive(true);
                    // 패널이 Hidden에서 재활성화될 때 VolumeSectionController가 OnEnable에서 재구독하지만
                    // 이미 이벤트가 지나간 후이므로 현재 악기 정보를 명시적으로 다시 주입한다.
                    if (_volCtrl != null && _activeInstrumentProviderObject != null)
                        _volCtrl.InjectProvider(_activeInstrumentProviderObject);
                    break;
            }
        }

        private void LateUpdate()
        {
            if (_panelInstance == null || !_panelInstance.activeSelf) return;

            // InstrumentOpened만 매 프레임 업데이트 (악기가 움직이면 PanelAnchor를 따라옴)
            // PinchOpened는 spawn 시 1회 위치·각도 고정 → world-lock (업데이트 없음)
            if (_state == PanelState.InstrumentOpened && _trackInstrument)
                PositionAtInstrument();
        }

        private void EnsurePanelInstance()
        {
            if (_panelInstance != null) return;

            _panelInstance = Instantiate(panelPrefab);
            _startMenuContainer = FindChildByName(_panelInstance.transform, "StartMenuSectionContainer");
            _volumeContainer    = FindChildByName(_panelInstance.transform, "VolumeSectionContainer");

            // VolumeSectionController에 provider 주입 (prefab 내 SerializeField는 씬 오브젝트를 참조할 수 없으므로 코드로 주입)
            if (_volumeContainer != null && _activeInstrumentProviderObject != null)
            {
                _volCtrl = _volumeContainer.GetComponentInChildren<VolumeSectionController>(true);
                if (_volCtrl != null)
                    _volCtrl.InjectProvider(_activeInstrumentProviderObject);
            }
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

            // Canvas가 항상 플레이어(카메라) 쪽을 향하도록:
            // World Space Canvas는 local -Z 방향으로 렌더링하므로,
            // local +Z를 카메라 반대 방향으로 설정하면 canvas face가 카메라를 향한다.
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
