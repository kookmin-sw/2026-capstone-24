using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
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
        [Tooltip("NearFarInteractor 수집 루트(XR Origin 또는 Camera Offset). 미지정 시 인터랙터 토글 비활성.")]
        [SerializeField] private GameObject nearFarInteractorRoot;
        [Tooltip("true이면 InstrumentOpened 진입 시 1회만 위치 정렬 후 매 프레임 추적을 중단한다. 트롬본처럼 player head를 따라가는 악기에서 SessionPanel이 함께 끌려다니는 것을 방지.")]
        [SerializeField] private bool snapOnce = true;
        [Tooltip("InstrumentOpened 모드에서 패널을 카메라 앞 몇 미터에 배치할지.")]
        [SerializeField] private float instrumentSpawnDistance = 1f;
        // 양 손 NearFarInteractor + 자식 LineRenderer/CurveVisualController (런타임에 자동 수집).
        // gameObject.SetActive 대신 .enabled 토글 — ControllerInputActionManager.OnCancelTeleport 가
        // NearFar.gameObject.SetActive(true) 로 부활시키는 동작과 직교(orthogonal)하기 위함.
        // LineRenderer는 Renderer 계열, CurveVisualController/NearFarInteractor는 Behaviour 계열이라 분리.
        readonly List<Behaviour> _nearFarBehaviours = new List<Behaviour>();
        readonly List<Renderer> _nearFarRenderers = new List<Renderer>();
        readonly List<NearFarInteractor> _nearFarInteractors = new List<NearFarInteractor>();
        readonly HashSet<NearFarInteractor> _hoveringInteractors = new HashSet<NearFarInteractor>();
        private GameObject _hitDot;

        private PanelState _state = PanelState.Hidden;
        private GameObject _panelInstance;
        private IActiveInstrumentProvider _provider;
        private Camera _mainCamera;
        private bool _trackInstrument;
        private VolumeSectionController _volCtrl;
        private RhythmGameSectionController _rhythmCtrl;
        private bool _hiddenByGame;
        private Coroutine _activateInteractorsCoroutine;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _provider = _activeInstrumentProviderObject as IActiveInstrumentProvider;
            CollectInteractors();
            CreateHitDot();
        }

        private void Start()
        {
            // 패널은 시작 시 Hidden → 레이저도 꺼둠
            SetInteractorsActive(false);
        }

        private void CollectInteractors()
        {
            foreach (var nf in _nearFarInteractors)
            {
                if (nf == null) continue;
                nf.uiHoverEntered.RemoveListener(OnUIHoverEntered);
                nf.uiHoverExited.RemoveListener(OnUIHoverExited);
            }
            _nearFarBehaviours.Clear();
            _nearFarRenderers.Clear();
            _nearFarInteractors.Clear();
            _hoveringInteractors.Clear();
            if (nearFarInteractorRoot == null) return;
            // NearFarInteractor (양 손 UI 레이)만 수집 — 텔레포트(XRRayInteractor)는 제외.
            // 명시적 root(XR Origin) 산하만 스캔해 explicit wiring 보장.
            foreach (var nf in nearFarInteractorRoot.GetComponentsInChildren<NearFarInteractor>(true))
            {
                _nearFarBehaviours.Add(nf);
                _nearFarInteractors.Add(nf);
                nf.uiHoverEntered.AddListener(OnUIHoverEntered);
                nf.uiHoverExited.AddListener(OnUIHoverExited);
                var curve = nf.GetComponentInChildren<CurveVisualController>(true);
                if (curve != null) _nearFarBehaviours.Add(curve);
                var line = nf.GetComponentInChildren<LineRenderer>(true);
                if (line != null) _nearFarRenderers.Add(line);
            }
        }

        private void OnUIHoverEntered(UIHoverEventArgs args)
        {
            if (args.interactorObject is NearFarInteractor nf)
                _hoveringInteractors.Add(nf);
        }

        private void OnUIHoverExited(UIHoverEventArgs args)
        {
            if (args.interactorObject is NearFarInteractor nf)
                _hoveringInteractors.Remove(nf);
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

            if (_activateInteractorsCoroutine != null)
            {
                StopCoroutine(_activateInteractorsCoroutine);
                _activateInteractorsCoroutine = null;
            }

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
                    if (_panelInstance.activeSelf)
                    {
                        // 이미 패널이 보이는 상태(PinchOpened에서 전환 등): 즉시 재배치
                        PositionAtInstrument();
                        SetInteractorsActive(true);
                    }
                    else
                    {
                        // Hidden에서 전환: TeleportInteractor가 OnCancelTeleport 후 다음 프레임
                        // Update에서야 SetActive(false)되므로 2프레임 대기 후 패널·레이저를 함께 표시.
                        // 패널만 먼저 보이고 레이저가 없는 불완전한 상태를 방지한다.
                        _activateInteractorsCoroutine = StartCoroutine(ShowPanelAndInteractorsDelayed());
                    }
                    if (_volCtrl != null && _activeInstrumentProviderObject != null)
                        _volCtrl.InjectProvider(_activeInstrumentProviderObject);
                    break;
            }
        }

        private IEnumerator ShowPanelAndInteractorsDelayed()
        {
            yield return null;
            yield return null;
            if (_state != PanelState.InstrumentOpened) yield break;
            PositionAtInstrument();
            _panelInstance.SetActive(true);
            SetInteractorsActive(true);
            _activateInteractorsCoroutine = null;
        }

        private void LateUpdate()
        {
            if (_panelInstance == null || !_panelInstance.activeSelf)
            {
                if (_hitDot != null) _hitDot.SetActive(false);
                return;
            }
            if (_state == PanelState.InstrumentOpened && _trackInstrument)
            {
                PositionAtInstrument();
                if (snapOnce)
                    _trackInstrument = false;
            }
            UpdateHitDot();
        }

        private void OnDestroy()
        {
            foreach (var nf in _nearFarInteractors)
            {
                if (nf == null) continue;
                nf.uiHoverEntered.RemoveListener(OnUIHoverEntered);
                nf.uiHoverExited.RemoveListener(OnUIHoverExited);
            }
            if (_hitDot != null) Destroy(_hitDot);
        }

        private void EnsurePanelInstance()
        {
            if (_panelInstance != null) return;

            _panelInstance = Instantiate(panelPrefab);
            _panelInstance.SetActive(false); // TransitionTo가 가시성 제어. 초기 비활성으로 시작.

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
            foreach (var b in _nearFarBehaviours)
                if (b != null) b.enabled = active;
            foreach (var r in _nearFarRenderers)
                if (r != null) r.enabled = active;
        }

        private void CreateHitDot()
        {
            _hitDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _hitDot.name = "SessionPanelLaserDot";
            _hitDot.transform.localScale = Vector3.one * 0.02f;
            Destroy(_hitDot.GetComponent<SphereCollider>());

            var r = _hitDot.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.15f, 0.45f, 1f);
            mat.SetColor("_EmissionColor", new Color(0.15f, 0.45f, 1f) * 2f);
            mat.EnableKeyword("_EMISSION");
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            _hitDot.SetActive(false);
        }

        private void UpdateHitDot()
        {
            if (_hitDot == null) return;
            bool found = false;
            foreach (var nf in _hoveringInteractors)
            {
                if (nf == null) continue;
                if (nf.TryGetCurveEndPoint(out Vector3 pos) != EndPointType.None)
                {
                    _hitDot.transform.position = pos;
                    found = true;
                    break;
                }
            }
            _hitDot.SetActive(found);
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
            if (_mainCamera == null) return;

            Vector3 horizontalForward = _mainCamera.transform.forward;
            horizontalForward.y = 0f;
            if (horizontalForward.sqrMagnitude < 0.001f)
                horizontalForward = Vector3.forward;
            else
                horizontalForward.Normalize();

            _panelInstance.transform.position = _mainCamera.transform.position
                                                + horizontalForward * instrumentSpawnDistance;
            _panelInstance.transform.rotation = Quaternion.LookRotation(horizontalForward);
        }
    }
}
