using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Auth;
using Murang.Multiplayer.Backend.Dto;
using Murang.Multiplayer.Backend.Http;
using Murang.Multiplayer.Room.Common;
using UnityEngine;

namespace Murang.Multiplayer.Room.Client
{
    [DisallowMultipleComponent]
    public sealed class RoomClientSmokeProbe : MonoBehaviour
    {
        private const string AutomationActionArgument = "-roomAutomationAction";
        private const string AutomationRoomNameArgument = "-roomName";
        private const string AutomationPasswordArgument = "-roomPassword";
        private const string AutomationResultPathArgument = "-roomResultPath";
        private const string AutomationJoinedSignalPathArgument = "-roomJoinedSignalPath";
        private const string AutomationHoldSecondsArgument = "-roomHoldSeconds";
        private const string AutomationLeaveAfterHoldArgument = "-roomLeaveAfterHold";
        private const string AutomationMaxPlayersArgument = "-roomMaxPlayers";

        [SerializeField] private AuthBootstrap authBootstrap;
        [SerializeField] private RoomClient roomClient;
        [SerializeField] private string defaultRoomName = "murang-room";
        [SerializeField] private string defaultPassword = string.Empty;

        private readonly CancellationTokenSource _destroyCancellationTokenSource = new CancellationTokenSource();
        private AutomationAction _automationAction;
        private bool _isAutomationEnabled;
        private string _automationResultPath = string.Empty;
        private string _automationJoinedSignalPath = string.Empty;
        private float _automationHoldSeconds;
        private bool _automationLeaveAfterHold;
        private int _automationMaxPlayers = 8;
        private string _roomNameInput = "murang-room";
        private string _passwordInput = string.Empty;
        private string _statusMessage = "Idle";
        private string _playerIdSummary = string.Empty;
        private Task _activeTask;

        void Awake()
        {
            if (authBootstrap == null)
            {
                authBootstrap = GetComponent<AuthBootstrap>();
            }

            if (roomClient == null)
            {
                roomClient = GetComponent<RoomClient>();
            }

            _roomNameInput = string.IsNullOrEmpty(defaultRoomName) ? "murang-room" : defaultRoomName;
            _passwordInput = defaultPassword ?? string.Empty;
            ConfigureAutomationMode();
        }

        async void Start()
        {
            if (!_isAutomationEnabled)
            {
                return;
            }

            await RunAutomationAsync(_destroyCancellationTokenSource.Token);
        }

        void OnDestroy()
        {
            _destroyCancellationTokenSource.Cancel();
            _destroyCancellationTokenSource.Dispose();
        }

        public void TriggerJoin()
        {
            _ = StartFlowAsync(JoinFlowAsync);
        }

        public void TriggerCreate()
        {
            _ = StartFlowAsync(CreateFlowAsync);
        }

        public void TriggerLeave()
        {
            _ = StartFlowAsync(LeaveFlowAsync);
        }

        private bool IsBusy
        {
            get { return _activeTask != null && !_activeTask.IsCompleted; }
        }

        private Task StartFlowAsync(Func<CancellationToken, Task> flow)
        {
            if (authBootstrap == null || roomClient == null)
            {
                _statusMessage = "AuthBootstrap or RoomClient reference is missing.";
                return Task.CompletedTask;
            }

            if (IsBusy)
            {
                return _activeTask;
            }

            _activeTask = RunFlowAsync(flow, _destroyCancellationTokenSource.Token);
            return _activeTask;
        }

        private async Task RunFlowAsync(Func<CancellationToken, Task> flow, CancellationToken cancellationToken)
        {
            try
            {
                await flow(cancellationToken);
            }
            catch (ApiException exception)
            {
                _statusMessage = "Backend request failed: " + exception.Code + " - " + exception.Detail;
            }
            catch (AuthFailedException exception)
            {
                _statusMessage = exception.Message;
            }
            catch (TaskCanceledException)
            {
                _statusMessage = "Operation canceled.";
            }
            catch (Exception exception)
            {
                _statusMessage = "Unexpected error: " + exception.Message;
            }
            finally
            {
                _activeTask = null;
            }
        }

        private async Task<(UserMeResponse user, RoomJoinResult result)> JoinRoomInternalAsync(CancellationToken cancellationToken)
        {
            _statusMessage = "Authenticating...";
            await authBootstrap.EnsureAuthenticatedAsync();

            UserMeResponse user = await authBootstrap.Session.GetCurrentUserAsync(cancellationToken);
            _playerIdSummary = user.nickname + " (" + user.playerId + ")";
            _statusMessage = "Joining room: " + _roomNameInput;

            RoomJoinOptions options = new RoomJoinOptions(user.playerId, _roomNameInput, _passwordInput);
            RoomJoinResult result = await roomClient.JoinRoomAsync(options, cancellationToken);
            _statusMessage = result.Success
                ? "Join succeeded: " + result.RoomName
                : "Join failed: " + result.Reason + " - " + result.Message;

            return (user, result);
        }

        private async Task<(UserMeResponse user, RoomJoinResult result)> CreateRoomInternalAsync(CancellationToken cancellationToken)
        {
            _statusMessage = "Authenticating...";
            await authBootstrap.EnsureAuthenticatedAsync();

            UserMeResponse user = await authBootstrap.Session.GetCurrentUserAsync(cancellationToken);
            _playerIdSummary = user.nickname + " (" + user.playerId + ")";
            _statusMessage = "Creating room: " + _roomNameInput;

            RoomCreateOptions options = new RoomCreateOptions(
                user.playerId,
                _roomNameInput,
                _passwordInput,
                _automationMaxPlayers);
            RoomJoinResult result = await roomClient.CreateRoomAsync(options, cancellationToken);
            _statusMessage = result.Success
                ? "Create succeeded: " + result.RoomName
                : "Create failed: " + result.Reason + " - " + result.Message;

            return (user, result);
        }

        private async Task JoinFlowAsync(CancellationToken cancellationToken)
        {
            await JoinRoomInternalAsync(cancellationToken);
        }

        private async Task CreateFlowAsync(CancellationToken cancellationToken)
        {
            await CreateRoomInternalAsync(cancellationToken);
        }

        private async Task LeaveFlowAsync(CancellationToken cancellationToken)
        {
            _statusMessage = "Leaving room...";
            cancellationToken.ThrowIfCancellationRequested();
            await roomClient.LeaveRoomAsync();
            _statusMessage = "Leave completed.";
        }

        void OnGUI()
        {
            if (!Application.isPlaying || _isAutomationEnabled)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 240f, 420f, 220f), GUI.skin.box);
            GUILayout.Label("Room Client Smoke Probe");
            GUILayout.Label("Player: " + (string.IsNullOrEmpty(_playerIdSummary) ? "Not authenticated" : _playerIdSummary));

            GUILayout.Label("Room name");
            _roomNameInput = GUILayout.TextField(_roomNameInput);
            GUILayout.Label("Password (optional)");
            _passwordInput = GUILayout.TextField(_passwordInput);

            GUILayout.Label("Status: " + _statusMessage);

            GUI.enabled = !IsBusy;

            if (GUILayout.Button("Create Room"))
            {
                TriggerCreate();
            }

            if (GUILayout.Button("Join Room"))
            {
                TriggerJoin();
            }

            if (GUILayout.Button("Leave Room"))
            {
                TriggerLeave();
            }

            GUI.enabled = true;

            if (IsBusy)
            {
                GUILayout.Label("Running...");
            }

            GUILayout.EndArea();
        }

        private void ConfigureAutomationMode()
        {
            string rawAction = RoomAutomationCommandLine.GetString(AutomationActionArgument, string.Empty);
            if (!TryParseAutomationAction(rawAction, out _automationAction))
            {
                _isAutomationEnabled = false;
                return;
            }

            _isAutomationEnabled = true;
            _automationResultPath = RoomAutomationCommandLine.GetString(AutomationResultPathArgument, string.Empty);
            _automationJoinedSignalPath = RoomAutomationCommandLine.GetString(AutomationJoinedSignalPathArgument, string.Empty);
            _automationHoldSeconds = Mathf.Max(0f, RoomAutomationCommandLine.GetFloat(AutomationHoldSecondsArgument, 0f));
            _automationLeaveAfterHold = RoomAutomationCommandLine.GetBool(AutomationLeaveAfterHoldArgument, false);
            _automationMaxPlayers = Mathf.Max(1, RoomAutomationCommandLine.GetInt(AutomationMaxPlayersArgument, 8));
            _roomNameInput = RoomAutomationCommandLine.GetString(AutomationRoomNameArgument, _roomNameInput);
            _passwordInput = RoomAutomationCommandLine.GetString(AutomationPasswordArgument, _passwordInput);
        }

        private async Task RunAutomationAsync(CancellationToken cancellationToken)
        {
            AutomationResultPayload payload = new AutomationResultPayload
            {
                action = _automationAction.ToString(),
                phase = "starting",
                requestedRoomName = _roomNameInput,
                startedAtUtc = DateTime.UtcNow.ToString("O")
            };

            try
            {
                (UserMeResponse user, RoomJoinResult result) = _automationAction == AutomationAction.Create
                    ? await CreateRoomInternalAsync(cancellationToken)
                    : await JoinRoomInternalAsync(cancellationToken);

                payload.playerId = user != null ? user.playerId : string.Empty;
                payload.nickname = user != null ? user.nickname : string.Empty;
                payload.success = result.Success;
                payload.reason = result.Reason.ToString();
                payload.roomName = result.RoomName;
                payload.message = result.Message;
                payload.statusMessage = _statusMessage;
                payload.phase = result.Success ? "joined" : "completed";

                if (result.Success)
                {
                    WriteJoinedSignal();

                    if (_automationHoldSeconds > 0f)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_automationHoldSeconds), cancellationToken);
                    }

                    if (_automationLeaveAfterHold)
                    {
                        payload.leaveAttempted = true;
                        await LeaveFlowAsync(cancellationToken);
                        payload.leaveCompleted = true;
                    }
                }
            }
            catch (Exception exception)
            {
                payload.success = false;
                payload.reason = RoomJoinFailureReason.Other.ToString();
                payload.message = exception.Message;
                payload.error = exception.ToString();
                payload.phase = "completed";
                payload.statusMessage = _statusMessage;
                payload.completedAtUtc = DateTime.UtcNow.ToString("O");
                WriteAutomationResult(payload);
                Environment.Exit(2);
                return;
            }

            payload.phase = "completed";
            payload.statusMessage = _statusMessage;
            payload.completedAtUtc = DateTime.UtcNow.ToString("O");
            WriteAutomationResult(payload);
            Environment.Exit(0);
        }

        private void WriteJoinedSignal()
        {
            if (string.IsNullOrEmpty(_automationJoinedSignalPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(_automationJoinedSignalPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_automationJoinedSignalPath, "joined");
        }

        private void WriteAutomationResult(AutomationResultPayload payload)
        {
            if (string.IsNullOrEmpty(_automationResultPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(_automationResultPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_automationResultPath, JsonUtility.ToJson(payload, true));
        }

        private static bool TryParseAutomationAction(string rawAction, out AutomationAction action)
        {
            switch ((rawAction ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "join":
                    action = AutomationAction.Join;
                    return true;
                case "create":
                    action = AutomationAction.Create;
                    return true;
                default:
                    action = AutomationAction.None;
                    return false;
            }
        }

        private enum AutomationAction
        {
            None = 0,
            Join = 1,
            Create = 2
        }

        [Serializable]
        private sealed class AutomationResultPayload
        {
            public string action;
            public string phase;
            public string requestedRoomName;
            public string roomName;
            public string playerId;
            public string nickname;
            public bool success;
            public string reason;
            public string message;
            public bool leaveAttempted;
            public bool leaveCompleted;
            public string statusMessage;
            public string error;
            public string startedAtUtc;
            public string completedAtUtc;
        }
    }
}
