using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Murang.Multiplayer.Room.Common;
using UnityEngine;

namespace Murang.Multiplayer.Room.Client
{
    [DisallowMultipleComponent]
    public sealed class RoomClient : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private RoomClientConfig config;

        private NetworkRunner _runner;
        private TaskCompletionSource<RoomJoinResult> _pendingJoin;

        public event Action<RoomJoinResult> OnJoinCompleted;

        public Task<RoomJoinResult> CreateRoomAsync(
            RoomCreateOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.Other,
                    options.RoomName,
                    "현재 구현은 기존 룸 입장 흐름부터 연결합니다. 룸 생성 오케스트레이션은 다음 단계에서 이어집니다."));
        }

        public async Task<RoomJoinResult> JoinRoomAsync(
            RoomJoinOptions options,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new InvalidOperationException("RoomClientConfig가 연결되지 않았습니다.");
            }

            EnsureRunner();

            _pendingJoin = new TaskCompletionSource<RoomJoinResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(config.JoinVerdictTimeoutSeconds));
            using CancellationTokenRegistration registration =
                timeoutSource.Token.Register(() => _pendingJoin.TrySetCanceled(timeoutSource.Token));

            StartGameArgs startArgs = new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = options.RoomName,
                ConnectionToken = RoomConnectionTokenCodec.Serialize(RoomPasswordHasher.Hash(options.Password)),
                CustomLobbyName = config.CustomLobbyName,
                UseDefaultPhotonCloudPorts = config.UseDefaultPhotonCloudPorts,
                SceneManager = GetOrAddSceneManager()
            };

            StartGameResult startResult = await _runner.StartGame(startArgs);
            if (!startResult.Ok)
            {
                RoomJoinResult failed = RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.ConnectionFailed,
                    options.RoomName,
                    startResult.ErrorMessage ?? startResult.ShutdownReason.ToString());
                CompleteJoin(failed);
            }

            try
            {
                return await _pendingJoin.Task;
            }
            catch (OperationCanceledException)
            {
                RoomJoinResult canceled = RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.Other,
                    options.RoomName,
                    "서버의 룸 입장 응답을 기다리다 시간 초과되었습니다.");
                CompleteJoin(canceled);
                return canceled;
            }
        }

        public async Task LeaveRoomAsync()
        {
            if (_runner != null && _runner.IsRunning)
            {
                await _runner.Shutdown();
            }
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
                RoomJoinFailureReason.ConnectionFailed,
                string.Empty,
                reason.ToString()));
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (_pendingJoin != null && !_pendingJoin.Task.IsCompleted)
            {
                CompleteJoin(RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.Other,
                    string.Empty,
                    reason.ToString()));
            }
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (_pendingJoin != null && !_pendingJoin.Task.IsCompleted)
            {
                CompleteJoin(RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.Other,
                    string.Empty,
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
            if (_pendingJoin == null || _pendingJoin.Task.IsCompleted)
            {
                return;
            }

            _pendingJoin.TrySetResult(result);
            OnJoinCompleted?.Invoke(result);
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
    }
}
