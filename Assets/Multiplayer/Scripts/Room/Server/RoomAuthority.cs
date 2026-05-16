using System;
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
        private const float RejectedPlayerDisconnectDelaySeconds = 0.5f;

        private struct PendingJoinVerdict
        {
            public PlayerRef Player;
            public RoomJoinResult Result;
            public bool DisconnectAfterSend;
        }

        private struct PendingDisconnect
        {
            public PlayerRef Player;
            public float ExecuteAtRealtime;
        }

        private readonly List<PendingJoinVerdict> _pendingJoinVerdicts = new List<PendingJoinVerdict>();
        private readonly List<PendingDisconnect> _pendingDisconnectPlayers = new List<PendingDisconnect>();

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
                    "Room is already full.");
            }

            if (!RoomPasswordHasher.Matches(expectedPasswordHash, providedPasswordHash))
            {
                return RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.WrongPassword,
                    string.Empty,
                    "Password does not match.");
            }

            return RoomJoinResult.CreateSuccess(string.Empty);
        }

        private void Update()
        {
            if (_pendingJoinVerdicts.Count == 0 && _pendingDisconnectPlayers.Count == 0)
            {
                return;
            }

            NetworkRunner runner = GetComponent<NetworkRunner>();
            if (runner == null || !runner.IsRunning || !runner.IsServer)
            {
                _pendingJoinVerdicts.Clear();
                _pendingDisconnectPlayers.Clear();
                return;
            }

            for (int index = 0; index < _pendingJoinVerdicts.Count; index++)
            {
                PendingJoinVerdict pendingVerdict = _pendingJoinVerdicts[index];
                SendJoinVerdict(runner, pendingVerdict.Player, pendingVerdict.Result);
                if (pendingVerdict.DisconnectAfterSend)
                {
                    QueueDisconnect(pendingVerdict.Player, Time.realtimeSinceStartup + RejectedPlayerDisconnectDelaySeconds);
                }
            }

            _pendingJoinVerdicts.Clear();

            for (int index = _pendingDisconnectPlayers.Count - 1; index >= 0; index--)
            {
                PendingDisconnect pendingDisconnect = _pendingDisconnectPlayers[index];
                if (pendingDisconnect.ExecuteAtRealtime > Time.realtimeSinceStartup)
                {
                    continue;
                }

                runner.Disconnect(pendingDisconnect.Player, null);
                _pendingDisconnectPlayers.RemoveAt(index);
            }
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
                : RoomJoinResult.CreateFailure(
                    RoomJoinFailureReason.Other,
                    string.Empty,
                    "Connection token could not be parsed.");

            if (validationResult.Success)
            {
                QueueJoinVerdict(player, RoomJoinResult.CreateSuccess(_roomName), disconnectAfterSend: false);
                return;
            }

            QueueJoinVerdict(
                player,
                RoomJoinResult.CreateFailure(
                    validationResult.Reason,
                    _roomName,
                    validationResult.Message),
                disconnectAfterSend: true);
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
            ArraySegment<byte> data)
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

        private void QueueDisconnect(PlayerRef player, float executeAtRealtime)
        {
            for (int index = 0; index < _pendingDisconnectPlayers.Count; index++)
            {
                if (_pendingDisconnectPlayers[index].Player != player)
                {
                    continue;
                }

                PendingDisconnect updatedDisconnect = _pendingDisconnectPlayers[index];
                updatedDisconnect.ExecuteAtRealtime = Mathf.Max(updatedDisconnect.ExecuteAtRealtime, executeAtRealtime);
                _pendingDisconnectPlayers[index] = updatedDisconnect;
                return;
            }

            _pendingDisconnectPlayers.Add(new PendingDisconnect
            {
                Player = player,
                ExecuteAtRealtime = executeAtRealtime
            });
        }

        private void QueueJoinVerdict(PlayerRef player, RoomJoinResult result, bool disconnectAfterSend)
        {
            _pendingJoinVerdicts.Add(new PendingJoinVerdict
            {
                Player = player,
                Result = result,
                DisconnectAfterSend = disconnectAfterSend
            });
        }
    }
}
