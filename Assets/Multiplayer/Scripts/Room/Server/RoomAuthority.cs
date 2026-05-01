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
        [SerializeField] private RoomServerConfig config;

        public void Initialize(RoomServerConfig roomServerConfig)
        {
            config = roomServerConfig;
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

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer || config == null)
            {
                return;
            }

            string passwordHash = null;
            byte[] connectionToken = runner.GetPlayerConnectionToken(player);
            bool tokenDecoded = RoomConnectionTokenCodec.TryDeserialize(connectionToken, out passwordHash);

            RoomJoinResult validationResult = tokenDecoded
                ? ValidateJoin(runner.ActivePlayers.Count(), config.MaxPlayers, config.PasswordHash, passwordHash)
                : RoomJoinResult.CreateFailure(RoomJoinFailureReason.Other, string.Empty, "입장 토큰을 해석하지 못했습니다.");

            if (validationResult.Success)
            {
                runner.SendReliableDataToPlayer(
                    player,
                    RoomJoinVerdictCodec.ReliableKey,
                    RoomJoinVerdictCodec.Serialize(RoomJoinResult.CreateSuccess(config.RoomName)));
                return;
            }

            runner.SendReliableDataToPlayer(
                player,
                RoomJoinVerdictCodec.ReliableKey,
                RoomJoinVerdictCodec.Serialize(
                    RoomJoinResult.CreateFailure(
                        validationResult.Reason,
                        config.RoomName,
                        validationResult.Message)));
            runner.Disconnect(player, null);
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
    }
}
