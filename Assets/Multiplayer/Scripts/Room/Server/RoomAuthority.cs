using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using Murang.Multiplayer.Room.Common;
using UnityEngine;

namespace Murang.Multiplayer.Room.Server
{
    [DisallowMultipleComponent]
    public sealed class RoomAuthority : MonoBehaviour, INetworkRunnerCallbacks
    {
        private readonly List<PlayerRef> _pendingDisconnectPlayers = new List<PlayerRef>();

        private string _roomName = "murang-room";
        private int _maxPlayers = 8;
        private string _passwordHash;

        public void Initialize(string roomName, int maxPlayers, string passwordHash)
        {
            _roomName = string.IsNullOrWhiteSpace(roomName) ? "murang-room" : roomName.Trim();
            _maxPlayers = Mathf.Max(1, maxPlayers);
            _passwordHash = RoomPasswordHasher.NormalizeHash(passwordHash);
        }

        public static RoomJoinResult ValidateJoin(
            int connectedPlayers,
            int maxPlayers,
            string expectedPasswordHash,
            string providedPasswordHash)
        {
            if (connectedPlayers > maxPlayers)
            {
                return RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.RoomFull,
                    string.Empty,
                    "룸 정원이 가득 찼습니다.");
            }

            if (!RoomPasswordHasher.Matches(expectedPasswordHash, providedPasswordHash))
            {
                return RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.WrongPassword,
                    string.Empty,
                    "비밀번호가 일치하지 않습니다.");
            }

            return RoomJoinResult.CreateSuccess(string.Empty);
        }

        private void Update()
        {
            if (_pendingDisconnectPlayers.Count == 0)
            {
                return;
            }

            NetworkRunner runner = GetComponent<NetworkRunner>();
            if (runner == null || !runner.IsRunning || !runner.IsServer)
            {
                _pendingDisconnectPlayers.Clear();
                return;
            }

            // Reliable verdict를 보낸 직후 같은 프레임에 바로 끊으면 클라이언트가 사유를 못 받을 수 있어,
            // 한 프레임 뒤에 정리한다.
            for (int index = 0; index < _pendingDisconnectPlayers.Count; index++)
            {
                runner.Disconnect(_pendingDisconnectPlayers[index], null);
            }

            _pendingDisconnectPlayers.Clear();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
            {
                return;
            }

            string passwordHash = null;
            byte[] connectionToken = runner.GetPlayerConnectionToken(player);
            bool tokenDecoded = RoomConnectionTokenCodec.TryDeserialize(connectionToken, out passwordHash);

            RoomJoinResult validationResult = tokenDecoded
                ? ValidateJoin(runner.ActivePlayers.Count(), _maxPlayers, _passwordHash, passwordHash)
                : RoomJoinResult.CreateFailure(RoomJoinFailureReason.Other, string.Empty, "입장 토큰을 해석하지 못했습니다.");

            if (validationResult.Success)
            {
                SendJoinVerdict(runner, player, RoomJoinResult.CreateSuccess(_roomName));
                return;
            }

            SendJoinVerdict(
                runner,
                player,
                RoomJoinResult.CreateFailure(
                    validationResult.Reason,
                    _roomName,
                    validationResult.Message));
            QueueDisconnect(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
            {
                return;
            }

            if (!runner.ActivePlayers.Any())
            {
                _ = runner.Shutdown();
            }
        }

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
        }

        void INetworkRunnerCallbacks.OnConnectRequest(
            NetworkRunner runner,
            NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token)
        {
            request.Accept();
        }

        void INetworkRunnerCallbacks.OnConnectFailed(
            NetworkRunner runner,
            NetAddress remoteAddress,
            NetConnectFailedReason reason)
        {
        }

        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        void INetworkRunnerCallbacks.OnReliableDataReceived(
            NetworkRunner runner,
            PlayerRef player,
            ReliableKey key,
            System.ArraySegment<byte> data)
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

        private void SendJoinVerdict(NetworkRunner runner, PlayerRef player, RoomJoinResult result)
        {
            runner.SendReliableDataToPlayer(
                player,
                RoomJoinVerdictCodec.ReliableKey,
                RoomJoinVerdictCodec.Serialize(result));
        }

        private void QueueDisconnect(PlayerRef player)
        {
            if (!_pendingDisconnectPlayers.Contains(player))
            {
                _pendingDisconnectPlayers.Add(player);
            }
        }
    }
}
