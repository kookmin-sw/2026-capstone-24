using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using Murang.Multiplayer.Room.Common;
using UnityEngine;

namespace Murang.Multiplayer.Room.Client
{
    [DisallowMultipleComponent]
    public sealed class RoomClient : MonoBehaviour, INetworkRunnerCallbacks
    {
        private const float ConnectedSuccessFallbackDelaySeconds = 1f;

        private sealed class PendingJoinRequest
        {
            public PendingJoinRequest(string roomName)
            {
                RoomName = roomName;
                Completion = new TaskCompletionSource<RoomJoinResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public TaskCompletionSource<RoomJoinResult> Completion { get; }

            public string RoomName { get; }
        }

        [SerializeField] private RoomClientConfig config;

        private NetworkRunner _runner;
        private PendingJoinRequest _pendingJoin;
        private CancellationTokenSource _pendingJoinFallbackCancellationTokenSource;

        public event Action<RoomJoinResult> OnJoinCompleted;

        public Task<RoomJoinResult> CreateRoomAsync(
            RoomCreateOptions options,
            CancellationToken cancellationToken = default)
        {
            return JoinSessionAsync(
                options.PlayerId,
                options.RoomName,
                options.Password,
                options.MaxPlayers,
                allowClientSessionCreation: true,
                cancellationToken);
        }

        public Task<RoomJoinResult> JoinRoomAsync(
            RoomJoinOptions options,
            CancellationToken cancellationToken = default)
        {
            return JoinSessionAsync(
                options.PlayerId,
                options.RoomName,
                options.Password,
                maxPlayers: null,
                allowClientSessionCreation: false,
                cancellationToken);
        }

        public async Task LeaveRoomAsync()
        {
            if (_pendingJoin != null)
            {
                CompleteJoin(RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.Other,
                    _pendingJoin.RoomName,
                    "룸 입장 시도가 취소되었습니다."));
            }

            if (_runner != null && _runner.IsRunning)
            {
                await _runner.Shutdown();
            }
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
            }

            if (_pendingJoin != null)
            {
                CompleteJoin(RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.Other,
                    _pendingJoin.RoomName,
                    "RoomClient가 파괴되어 룸 입장을 완료하지 못했습니다."));
            }
        }

        private async Task<RoomJoinResult> JoinSessionAsync(
            string playerId,
            string roomName,
            string password,
            int? maxPlayers,
            bool allowClientSessionCreation,
            CancellationToken cancellationToken)
        {
            if (config == null)
            {
                throw new InvalidOperationException("RoomClientConfig가 연결되지 않았습니다.");
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("PlayerId가 비어 있습니다. 백엔드 인증을 먼저 완료해야 합니다.", nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(roomName))
            {
                throw new ArgumentException("RoomName이 비어 있습니다.", nameof(roomName));
            }

            if (_pendingJoin != null)
            {
                throw new InvalidOperationException("이미 진행 중인 룸 입장 요청이 있습니다.");
            }

            if (_runner != null && _runner.IsRunning)
            {
                throw new InvalidOperationException("이미 룸 세션에 연결되어 있습니다. 먼저 LeaveRoomAsync를 호출하세요.");
            }

            string normalizedPlayerId = playerId.Trim();
            string normalizedRoomName = roomName.Trim();
            string passwordHash = RoomPasswordHasher.Hash(password);

            EnsureRunner();

            PendingJoinRequest pendingJoin = new PendingJoinRequest(normalizedRoomName);
            _pendingJoin = pendingJoin;
            _pendingJoinFallbackCancellationTokenSource = new CancellationTokenSource();

            using CancellationTokenSource timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(config.JoinVerdictTimeoutSeconds));
            using CancellationTokenRegistration registration =
                timeoutSource.Token.Register(() => pendingJoin.Completion.TrySetCanceled(timeoutSource.Token));

            StartGameArgs startArgs = CreateStartGameArgs(
                normalizedPlayerId,
                normalizedRoomName,
                passwordHash,
                maxPlayers,
                allowClientSessionCreation);

            StartGameResult startResult = await _runner.StartGame(startArgs);
            if (!startResult.Ok)
            {
                RoomJoinResult failed = RoomJoinResult.CreateFailure(
                    MapShutdownReason(startResult.ShutdownReason),
                    normalizedRoomName,
                    startResult.ErrorMessage ?? startResult.ShutdownReason.ToString());
                CompleteJoin(failed);
                await ShutdownRunnerIfRunningAsync();
                return failed;
            }

            try
            {
                return await pendingJoin.Completion.Task;
            }
            catch (OperationCanceledException)
            {
                RoomJoinResult canceled = RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.Other,
                    normalizedRoomName,
                    "서버의 룸 입장 응답을 기다리다 시간 초과되었습니다.");
                CompleteJoin(canceled);
                await ShutdownRunnerIfRunningAsync();
                return canceled;
            }
        }

        private StartGameArgs CreateStartGameArgs(
            string playerId,
            string roomName,
            string passwordHash,
            int? maxPlayers,
            bool allowClientSessionCreation)
        {
            StartGameArgs startArgs = new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = roomName,
                ConnectionToken = RoomConnectionTokenCodec.Serialize(passwordHash),
                AuthValues = new AuthenticationValues(playerId),
                CustomLobbyName = config.CustomLobbyName,
                UseDefaultPhotonCloudPorts = config.UseDefaultPhotonCloudPorts,
                SceneManager = GetOrAddSceneManager(),
                EnableClientSessionCreation = allowClientSessionCreation
            };

            if (allowClientSessionCreation)
            {
                startArgs.PlayerCount = Mathf.Max(1, maxPlayers ?? 8);
                startArgs.IsVisible = true;
                startArgs.IsOpen = true;
                startArgs.SessionProperties = BuildSessionProperties(passwordHash);
            }

            return startArgs;
        }

        private static Dictionary<string, SessionProperty> BuildSessionProperties(string passwordHash)
        {
            return new Dictionary<string, SessionProperty>
            {
                [RoomSessionPropertyKeys.IsLocked] = !string.IsNullOrEmpty(RoomPasswordHasher.NormalizeHash(passwordHash))
            };
        }

        void INetworkRunnerCallbacks.OnReliableDataReceived(
            NetworkRunner runner,
            PlayerRef player,
            ReliableKey key,
            ArraySegment<byte> data)
        {
            if (RoomJoinVerdictCodec.TryDeserialize(key, data, out RoomJoinResult result))
            {
                CompleteJoin(result);
            }
        }

        void INetworkRunnerCallbacks.OnConnectFailed(
            NetworkRunner runner,
            NetAddress remoteAddress,
            NetConnectFailedReason reason)
        {
            CompleteJoin(RoomJoinResult.CreateFailure(
                MapConnectFailedReason(reason),
                GetPendingRoomName(),
                reason.ToString()));
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (_pendingJoin != null)
            {
                CompleteJoin(RoomJoinResult.CreateFailure(
                    MapDisconnectReason(reason),
                    GetPendingRoomName(),
                    reason.ToString()));
            }
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (_pendingJoin != null)
            {
                CompleteJoin(RoomJoinResult.CreateFailure(
                    MapShutdownReason(shutdownReason),
                    GetPendingRoomName(),
                    shutdownReason.ToString()));
            }
        }

        private void EnsureRunner()
        {
            _runner = GetComponent<NetworkRunner>();
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
            }

            _runner.ProvideInput = false;
            _runner.RemoveCallbacks(this);
            _runner.AddCallbacks(this);
        }

        private NetworkSceneManagerDefault GetOrAddSceneManager()
        {
            NetworkSceneManagerDefault sceneManager = GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
            {
                sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            return sceneManager;
        }

        private void CompleteJoin(RoomJoinResult result)
        {
            if (_pendingJoin == null || _pendingJoin.Completion.Task.IsCompleted)
            {
                return;
            }

            PendingJoinRequest pendingJoin = _pendingJoin;
            _pendingJoin = null;
            if (_pendingJoinFallbackCancellationTokenSource != null)
            {
                _pendingJoinFallbackCancellationTokenSource.Cancel();
                _pendingJoinFallbackCancellationTokenSource.Dispose();
                _pendingJoinFallbackCancellationTokenSource = null;
            }

            pendingJoin.Completion.TrySetResult(result);
            OnJoinCompleted?.Invoke(result);
        }

        private async Task ShutdownRunnerIfRunningAsync()
        {
            if (_runner != null && _runner.IsRunning)
            {
                await _runner.Shutdown();
            }
        }

        private string GetPendingRoomName()
        {
            return _pendingJoin != null ? _pendingJoin.RoomName : string.Empty;
        }

        private static RoomJoinFailureReason MapShutdownReason(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.GameIsFull:
                    return RoomJoinFailureReason.RoomFull;
                case ShutdownReason.GameNotFound:
                    return RoomJoinFailureReason.RoomNotFound;
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed:
                case ShutdownReason.AuthenticationTicketExpired:
                case ShutdownReason.MaxCcuReached:
                case ShutdownReason.InvalidRegion:
                    return RoomJoinFailureReason.ConnectionFailed;
                default:
                    return RoomJoinFailureReason.Other;
            }
        }

        private static RoomJoinFailureReason MapConnectFailedReason(NetConnectFailedReason reason)
        {
            switch (reason)
            {
                case NetConnectFailedReason.ServerFull:
                    return RoomJoinFailureReason.RoomFull;
                case NetConnectFailedReason.Timeout:
                case NetConnectFailedReason.ServerRefused:
                    return RoomJoinFailureReason.ConnectionFailed;
                default:
                    return RoomJoinFailureReason.Other;
            }
        }

        private static RoomJoinFailureReason MapDisconnectReason(NetDisconnectReason reason)
        {
            switch (reason)
            {
                case NetDisconnectReason.Timeout:
                    return RoomJoinFailureReason.ConnectionFailed;
                default:
                    return RoomJoinFailureReason.Other;
            }
        }

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
        }

        void INetworkRunnerCallbacks.OnConnectRequest(
            NetworkRunner runner,
            NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token)
        {
        }

        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        void INetworkRunnerCallbacks.OnReliableDataProgress(
            NetworkRunner runner,
            PlayerRef player,
            ReliableKey key,
            float progress)
        {
        }

        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
        {
            if (_pendingJoin == null || _pendingJoinFallbackCancellationTokenSource == null)
            {
                return;
            }

            _ = CompleteJoinOnConnectedFallbackAsync(
                _pendingJoin.RoomName,
                _pendingJoinFallbackCancellationTokenSource.Token);
        }

        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
        }

        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
        {
        }

        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
        {
        }

        private async Task CompleteJoinOnConnectedFallbackAsync(string roomName, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(ConnectedSuccessFallbackDelaySeconds),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_pendingJoin == null
                || !string.Equals(_pendingJoin.RoomName, roomName, StringComparison.Ordinal)
                || _runner == null
                || !_runner.IsRunning)
            {
                return;
            }

            CompleteJoin(RoomJoinResult.CreateSuccess(roomName));
        }
    }
}
