using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Auth;
using Murang.Multiplayer.Backend.Http;
using Murang.Multiplayer.Room.Client;
using Murang.Multiplayer.Room.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// World-space 로비 패널. 인증된 유저가 (1) 새 룸을 생성하거나 (2) admission
    /// 가능한 룸 목록에서 선택해 합류할 수 있게 한다. 룸 생성은 backend
    /// <c>POST /api/v1/rooms</c> 경유 (ECS Fargate task 기동 + ready 폴링 후 Photon
    /// client 모드 합류).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MultiplayerLobbyPanel : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MultiplayerAuthGate authGate;
        [SerializeField] private RoomClient roomClient;
        [SerializeField] private RoomListQuery roomListQuery;
        [SerializeField] private MultiplayerAuthConfig authConfig;

        [Header("UI — Create form")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private Toggle passwordEnabledToggle;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField maxPlayersInput;
        [SerializeField] private Button createButton;

        [Header("UI — Room list")]
        [SerializeField] private Transform roomRowParent;
        [SerializeField] private RoomRowEntry roomRowPrefab;
        [SerializeField] private TMP_Text emptyLabel;

        [Header("UI — Status / Root")]
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private GameObject lobbyRoot;

        /// <summary>룸에 합류 성공한 직후 발화. 인-룸 패널이 listen 해 자기 자신을 활성화.</summary>
        public event Action<string> OnEnteredRoom;

        private RoomProvisioningService _provisioningService;
        private readonly List<RoomRowEntry> _rowPool = new List<RoomRowEntry>();
        private bool _busy;
        private CancellationTokenSource _activeOperationCts;

        void Awake()
        {
            if (lobbyRoot != null)
            {
                lobbyRoot.SetActive(false);
            }

            if (createButton != null)
            {
                createButton.onClick.AddListener(OnCreateClicked);
            }
            if (passwordEnabledToggle != null)
            {
                passwordEnabledToggle.onValueChanged.AddListener(OnPasswordToggleChanged);
            }
            if (roomListQuery != null)
            {
                roomListQuery.OnRoomListUpdated += HandleRoomListUpdated;
            }
            if (authGate != null)
            {
                authGate.OnAuthenticationCompleted += HandleAuthenticationCompleted;
            }

            RefreshPasswordInputState();
            UpdateEmptyLabel(0);
        }

        void OnDestroy()
        {
            if (createButton != null)
            {
                createButton.onClick.RemoveListener(OnCreateClicked);
            }
            if (passwordEnabledToggle != null)
            {
                passwordEnabledToggle.onValueChanged.RemoveListener(OnPasswordToggleChanged);
            }
            if (roomListQuery != null)
            {
                roomListQuery.OnRoomListUpdated -= HandleRoomListUpdated;
            }
            if (authGate != null)
            {
                authGate.OnAuthenticationCompleted -= HandleAuthenticationCompleted;
            }

            _activeOperationCts?.Cancel();
            _activeOperationCts?.Dispose();
        }

        /// <summary>인-룸 패널이 leave 후 로비로 복귀할 때 호출.</summary>
        public void ShowLobby()
        {
            if (lobbyRoot != null)
            {
                lobbyRoot.SetActive(true);
            }
            SetStatus("Ready");
            _busy = false;
            if (createButton != null)
            {
                createButton.interactable = true;
            }
        }

        private void HandleAuthenticationCompleted()
        {
            if (lobbyRoot != null && !lobbyRoot.activeSelf)
            {
                lobbyRoot.SetActive(true);
            }
            SetStatus("Ready");
        }

        private void OnPasswordToggleChanged(bool enabled)
        {
            RefreshPasswordInputState();
        }

        private void RefreshPasswordInputState()
        {
            bool enabled = passwordEnabledToggle != null && passwordEnabledToggle.isOn;
            if (passwordInput != null)
            {
                passwordInput.interactable = enabled;
                if (!enabled)
                {
                    passwordInput.text = string.Empty;
                }
            }
        }

        private async void OnCreateClicked()
        {
            if (_busy)
            {
                return;
            }

            if (!IsReadyForBackendCall(out string reason))
            {
                SetStatus("Failed: " + reason);
                return;
            }

            string roomName = roomNameInput != null ? roomNameInput.text : string.Empty;
            int maxPlayers = ParseMaxPlayers();
            bool passwordEnabled = passwordEnabledToggle != null && passwordEnabledToggle.isOn;
            string passwordRaw = passwordEnabled && passwordInput != null ? passwordInput.text : string.Empty;

            LobbyValidationResult validation = LobbyInputValidator.ValidateCreate(
                roomName,
                maxPlayers,
                passwordEnabled,
                passwordRaw);

            if (!validation.Success)
            {
                SetStatus("Failed: " + validation.ErrorMessage);
                return;
            }

            await RunCreateRoomAsync(roomName.Trim(), maxPlayers, passwordEnabled ? passwordRaw : null);
        }

        private async Task RunCreateRoomAsync(string roomName, int maxPlayers, string passwordOrNull)
        {
            _busy = true;
            if (createButton != null)
            {
                createButton.interactable = false;
            }

            _activeOperationCts?.Cancel();
            _activeOperationCts?.Dispose();
            _activeOperationCts = new CancellationTokenSource();
            CancellationToken ct = _activeOperationCts.Token;

            try
            {
                SetStatus("Creating room…");
                RoomProvisioningService provisioningService = EnsureProvisioningService();

                RoomCreateOptions options = new RoomCreateOptions(
                    authGate.CurrentPlayerId,
                    roomName,
                    passwordOrNull,
                    maxPlayers);

                string runtimeVersion = Application.version;

                RoomJoinResult result = await roomClient.CreateRoomThroughBackendAsync(
                    options,
                    authGate.CurrentAccessToken,
                    provisioningService,
                    runtimeVersion,
                    ct);

                if (result.Success)
                {
                    SetStatus("Joined: " + result.RoomName);
                    if (lobbyRoot != null)
                    {
                        lobbyRoot.SetActive(false);
                    }
                    OnEnteredRoom?.Invoke(result.RoomName);
                }
                else
                {
                    SetStatus(FormatJoinFailure(result));
                }
            }
            catch (RoomProvisioningFailedException ex)
            {
                SetStatus($"Failed: room {ex.RoomId} -> {ex.LastStatus}");
                Debug.LogWarning($"[MultiplayerLobbyPanel] provisioning failed: {ex}");
            }
            catch (TimeoutException ex)
            {
                SetStatus("Failed: room did not reach READY within timeout window.");
                Debug.LogWarning($"[MultiplayerLobbyPanel] provisioning timeout: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Cancelled");
            }
            catch (Exception ex)
            {
                SetStatus("Failed: " + ex.Message);
                Debug.LogError($"[MultiplayerLobbyPanel] unexpected: {ex}");
            }
            finally
            {
                _busy = false;
                if (createButton != null)
                {
                    createButton.interactable = true;
                }
            }
        }

        private void HandleRoomListUpdated(IReadOnlyList<RoomListEntry> entries)
        {
            if (roomRowParent == null || roomRowPrefab == null)
            {
                return;
            }

            EnsureRowCapacity(entries.Count);

            for (int i = 0; i < _rowPool.Count; i++)
            {
                RoomRowEntry row = _rowPool[i];
                if (i < entries.Count)
                {
                    row.gameObject.SetActive(true);
                    row.Bind(entries[i]);
                }
                else
                {
                    row.gameObject.SetActive(false);
                }
            }

            UpdateEmptyLabel(entries.Count);
        }

        private void EnsureRowCapacity(int needed)
        {
            while (_rowPool.Count < needed)
            {
                RoomRowEntry instance = Instantiate(roomRowPrefab, roomRowParent);
                instance.OnJoinRequested += HandleJoinRequested;
                _rowPool.Add(instance);
            }
        }

        private void UpdateEmptyLabel(int count)
        {
            if (emptyLabel != null)
            {
                emptyLabel.gameObject.SetActive(count == 0);
            }
        }

        private async void HandleJoinRequested(RoomListEntry entry)
        {
            if (_busy)
            {
                return;
            }

            if (!IsReadyForBackendCall(out string reason))
            {
                SetStatus("Failed: " + reason);
                return;
            }

            string passwordRaw = passwordInput != null ? passwordInput.text : string.Empty;
            LobbyValidationResult validation = LobbyInputValidator.ValidateJoinPassword(
                entry.IsLocked,
                passwordRaw);
            if (!validation.Success)
            {
                SetStatus("Failed: " + validation.ErrorMessage);
                return;
            }

            await RunJoinRoomAsync(entry, entry.IsLocked ? passwordRaw : null);
        }

        private async Task RunJoinRoomAsync(RoomListEntry entry, string passwordOrNull)
        {
            _busy = true;
            SetRowsInteractable(false);

            _activeOperationCts?.Cancel();
            _activeOperationCts?.Dispose();
            _activeOperationCts = new CancellationTokenSource();
            CancellationToken ct = _activeOperationCts.Token;

            try
            {
                SetStatus("Joining: " + entry.RoomName + " …");

                RoomJoinOptions options = new RoomJoinOptions(
                    authGate.CurrentPlayerId,
                    entry.RoomName,
                    passwordOrNull);

                RoomJoinResult result = await roomClient.JoinRoomAsync(options, ct);

                if (result.Success)
                {
                    SetStatus("Joined: " + result.RoomName);
                    if (lobbyRoot != null)
                    {
                        lobbyRoot.SetActive(false);
                    }
                    OnEnteredRoom?.Invoke(result.RoomName);
                }
                else
                {
                    SetStatus(FormatJoinFailure(result));
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Cancelled");
            }
            catch (Exception ex)
            {
                SetStatus("Failed: " + ex.Message);
                Debug.LogError($"[MultiplayerLobbyPanel] join unexpected: {ex}");
            }
            finally
            {
                _busy = false;
                SetRowsInteractable(true);
            }
        }

        private void SetRowsInteractable(bool value)
        {
            for (int i = 0; i < _rowPool.Count; i++)
            {
                if (_rowPool[i].gameObject.activeSelf)
                {
                    _rowPool[i].SetInteractable(value);
                }
            }
        }

        private RoomProvisioningService EnsureProvisioningService()
        {
            if (_provisioningService != null)
            {
                return _provisioningService;
            }

            string baseUrl = ResolveBackendBaseUrl();
            BackendApiClient http = new BackendApiClient(baseUrl);
            BackendApiClientRoomAdapter adapter = new BackendApiClientRoomAdapter(http);
            _provisioningService = new RoomProvisioningService(adapter);
            return _provisioningService;
        }

        private string ResolveBackendBaseUrl()
        {
            if (authConfig == null)
            {
                throw new InvalidOperationException("MultiplayerAuthConfig is not connected.");
            }
            // BackendBaseUrl resolves Android device build vs Editor with fallback handling.
            string url = authConfig.BackendBaseUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException(
                    "MultiplayerAuthConfig.BackendBaseUrl is empty. Set editorBackendBaseUrl or deviceBackendBaseUrl.");
            }
            return url;
        }

        private bool IsReadyForBackendCall(out string reason)
        {
            if (authGate == null)
            {
                reason = "MultiplayerAuthGate reference is missing.";
                return false;
            }
            if (!authGate.IsAuthenticated)
            {
                reason = "Not authenticated. Press Activate first.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(authGate.CurrentAccessToken))
            {
                reason = "Access token is empty.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(authGate.CurrentPlayerId))
            {
                reason = "playerId is empty. Retry /api/v1/users/me lookup.";
                return false;
            }
            if (roomClient == null)
            {
                reason = "RoomClient reference is missing.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private int ParseMaxPlayers()
        {
            if (maxPlayersInput == null || string.IsNullOrWhiteSpace(maxPlayersInput.text))
            {
                return 0;
            }
            return int.TryParse(maxPlayersInput.text, out int parsed) ? parsed : 0;
        }

        private static string FormatJoinFailure(RoomJoinResult result)
        {
            switch (result.Reason)
            {
                case RoomJoinFailureReason.RoomFull:
                    return "Failed: room is full.";
                case RoomJoinFailureReason.WrongPassword:
                    return "Failed: wrong password.";
                case RoomJoinFailureReason.RoomNotFound:
                    return "Failed: room not found.";
                case RoomJoinFailureReason.ConnectionFailed:
                    return "Failed: connection to room server failed.";
                default:
                    return "Failed: " + (string.IsNullOrEmpty(result.Message) ? "unknown error" : result.Message);
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }
    }
}
