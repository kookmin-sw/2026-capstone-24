using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// spec 07 로비 패널 UI 컨트롤러. 기존 <c>MultiplayerLobbyPanel</c>과 클래스 이름이
    /// 분리되어 동일 namespace 내에서 공존 가능하다.
    ///
    /// <para>백엔드 연동 없이 UI 구조·상태만 관리하며, 모든 백엔드 호출은
    /// <see cref="IRoomActionSink"/>로 위임한다. nop 구현체 <see cref="StubRoomActionSink"/>를
    /// Inspector에서 와이어해 수동 검증한다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyPanelController : MonoBehaviour
    {
        // IRoomActionSink를 Inspector에서 와이어할 때 UnityEngine.Object로 받아
        // as IRoomActionSink로 캐스팅 (Multiplayer 도메인 패턴).
        [Header("Backend Sink")]
        [SerializeField] private Object _sinkObject;

        [Header("UI — Create Form")]
        [SerializeField] private TMP_InputField _roomNameInput;
        [SerializeField] private Toggle _passwordEnabledToggle;
        [SerializeField] private TMP_InputField _passwordInput;
        [SerializeField] private CanvasGroup _passwordInputCanvasGroup;
        [SerializeField] private TMP_Text _maxPlayersLabel;
        [SerializeField] private Button _plusButton;
        [SerializeField] private Button _minusButton;
        [SerializeField] private Button _createButton;

        [Header("UI — Room List")]
        [SerializeField] private LobbyRoomRow _roomRowPrefab;
        [SerializeField] private Transform _roomRowParent;
        [SerializeField] private TMP_Text _emptyLabel;

        [Header("UI — Password Overlay")]
        [SerializeField] private GameObject _passwordOverlayRoot;
        [SerializeField] private TMP_InputField _overlayPasswordInput;
        [SerializeField] private Button _overlayConfirmButton;
        [SerializeField] private Button _overlayCancelButton;

        [Header("UI — Root / Status")]
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private GameObject _lobbyRoot;

        [Header("Settings")]
        [SerializeField] private int _defaultMaxPlayers = 4;

        // ------------------------------------------------------------------ //
        // 런타임 상태
        // ------------------------------------------------------------------ //

        private IRoomActionSink _sink;
        private int _currentMaxPlayers;
        private LobbyRoomRowData _pendingJoinRow;
        private readonly List<LobbyRoomRow> _rowPool = new List<LobbyRoomRow>();

        // ------------------------------------------------------------------ //
        // Unity 생명주기
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _sink = _sinkObject as IRoomActionSink;

            _currentMaxPlayers = Mathf.Clamp(_defaultMaxPlayers, 1, 32);
            UpdateMaxPlayersUI();

            if (_createButton != null)
                _createButton.onClick.AddListener(OnCreateClicked);

            if (_passwordEnabledToggle != null)
                _passwordEnabledToggle.onValueChanged.AddListener(OnPasswordToggleChanged);

            if (_plusButton != null)
                _plusButton.onClick.AddListener(OnPlusClicked);

            if (_minusButton != null)
                _minusButton.onClick.AddListener(OnMinusClicked);

            if (_overlayConfirmButton != null)
                _overlayConfirmButton.onClick.AddListener(OnOverlayConfirmClicked);

            if (_overlayCancelButton != null)
                _overlayCancelButton.onClick.AddListener(OnOverlayCancelClicked);

            // 초기 상태
            RefreshPasswordInputState(_passwordEnabledToggle != null && _passwordEnabledToggle.isOn);
            UpdateEmptyLabel(0);

            if (_passwordOverlayRoot != null)
                _passwordOverlayRoot.SetActive(false);

            if (_lobbyRoot != null)
                _lobbyRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_sink != null)
                _sink.RoomListUpdated += HandleRoomListUpdated;
        }

        private void OnDisable()
        {
            if (_sink != null)
                _sink.RoomListUpdated -= HandleRoomListUpdated;
        }

        // ------------------------------------------------------------------ //
        // 공개 API
        // ------------------------------------------------------------------ //

        /// <summary>로비 루트를 표시한다. session-panel sub-spec 08에서 호출.</summary>
        public void ShowLobby()
        {
            if (_lobbyRoot != null)
                _lobbyRoot.SetActive(true);
        }

        /// <summary>로비 루트를 숨긴다.</summary>
        public void HideLobby()
        {
            if (_lobbyRoot != null)
                _lobbyRoot.SetActive(false);
        }

        // ------------------------------------------------------------------ //
        // 버튼/토글 핸들러
        // ------------------------------------------------------------------ //

        private void OnPasswordToggleChanged(bool isOn)
        {
            if (!isOn && _passwordInput != null)
                _passwordInput.text = string.Empty;

            RefreshPasswordInputState(isOn);
        }

        private void OnPlusClicked()
        {
            _currentMaxPlayers = Mathf.Clamp(_currentMaxPlayers + 1, 1, 32);
            UpdateMaxPlayersUI();
        }

        private void OnMinusClicked()
        {
            _currentMaxPlayers = Mathf.Clamp(_currentMaxPlayers - 1, 1, 32);
            UpdateMaxPlayersUI();
        }

        private void OnCreateClicked()
        {
            if (_sink == null)
            {
                SetStatus("Error: IRoomActionSink not wired.");
                return;
            }

            string roomName = _roomNameInput != null ? _roomNameInput.text : string.Empty;
            bool passwordEnabled = _passwordEnabledToggle != null && _passwordEnabledToggle.isOn;
            string passwordRaw = _passwordInput != null ? _passwordInput.text : string.Empty;

            LobbyValidationResult result = LobbyInputValidator.ValidateCreate(
                roomName, _currentMaxPlayers, passwordEnabled, passwordRaw);

            if (!result.Success)
            {
                SetStatus("Failed: " + result.ErrorMessage);
                return;
            }

            _sink.RequestCreate(new LobbyCreateRequest(
                roomName,
                _currentMaxPlayers,
                passwordEnabled,
                passwordRaw,
                UnityEngine.Application.version));

            SetStatus(string.Empty);
        }

        private void OnRoomRowJoinRequested(LobbyRoomRowData data)
        {
            if (data.HasPassword)
            {
                _pendingJoinRow = data;
                if (_passwordOverlayRoot != null)
                    _passwordOverlayRoot.SetActive(true);
                if (_overlayPasswordInput != null)
                    _overlayPasswordInput.text = string.Empty;
            }
            else
            {
                _sink?.RequestJoin(new LobbyJoinRequest(
                    data.RoomId,
                    data.RoomName,
                    null,
                    UnityEngine.Application.version));
            }
        }

        private void OnOverlayConfirmClicked()
        {
            if (_pendingJoinRow == null) return;

            _sink?.RequestJoin(new LobbyJoinRequest(
                _pendingJoinRow.RoomId,
                _pendingJoinRow.RoomName,
                _overlayPasswordInput != null ? _overlayPasswordInput.text : string.Empty,
                UnityEngine.Application.version));

            CloseOverlay();
        }

        private void OnOverlayCancelClicked()
        {
            CloseOverlay();
        }

        // ------------------------------------------------------------------ //
        // RoomList 갱신 (풀 패턴)
        // ------------------------------------------------------------------ //

        private void HandleRoomListUpdated(IReadOnlyList<LobbyRoomRowData> rows)
        {
            if (_roomRowPrefab == null || _roomRowParent == null) return;

            // 풀 확장
            while (_rowPool.Count < rows.Count)
            {
                LobbyRoomRow newRow = Instantiate(_roomRowPrefab, _roomRowParent);
                newRow.JoinRequested += OnRoomRowJoinRequested;
                _rowPool.Add(newRow);
            }

            // 활성화 + 바인딩
            for (int i = 0; i < _rowPool.Count; i++)
            {
                bool active = i < rows.Count;
                _rowPool[i].gameObject.SetActive(active);
                if (active)
                    _rowPool[i].Bind(rows[i]);
            }

            UpdateEmptyLabel(rows.Count);
        }

        // ------------------------------------------------------------------ //
        // 내부 헬퍼
        // ------------------------------------------------------------------ //

        private void RefreshPasswordInputState(bool isOn)
        {
            if (_passwordInput != null)
                _passwordInput.interactable = isOn;

            if (_passwordInputCanvasGroup != null)
                _passwordInputCanvasGroup.alpha = isOn ? 1.0f : 0.4f;
        }

        private void UpdateMaxPlayersUI()
        {
            if (_maxPlayersLabel != null)
                _maxPlayersLabel.text = _currentMaxPlayers.ToString();

            if (_minusButton != null)
                _minusButton.interactable = _currentMaxPlayers > 1;

            if (_plusButton != null)
                _plusButton.interactable = _currentMaxPlayers < 32;
        }

        private void UpdateEmptyLabel(int rowCount)
        {
            if (_emptyLabel != null)
                _emptyLabel.gameObject.SetActive(rowCount == 0);
        }

        private void CloseOverlay()
        {
            if (_passwordOverlayRoot != null)
                _passwordOverlayRoot.SetActive(false);
            _pendingJoinRow = default;
        }

        private void SetStatus(string msg)
        {
            if (_statusLabel != null)
                _statusLabel.text = msg;
        }
    }
}
