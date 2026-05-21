using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using Murang.Multiplayer.Room.Server;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// RoomServerBootstrap 과 같은 GameObject 에 부착.
    /// INetworkRunnerCallbacks 를 구현해 runner 에 등록되고,
    /// 플레이어 합류/퇴장 시 PlayerHandRig NetworkObject 를 Spawn/Despawn 한다.
    /// server-only 가드: client build (TestSceneSanyo) 는 callback 을 받지만
    /// IsServer 가 false 이므로 spawn/despawn 분기에 진입하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHandRigSpawner : MonoBehaviour, INetworkRunnerCallbacks
    {
        private RoomServerConfig _config;
        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedRigs =
            new Dictionary<PlayerRef, NetworkObject>();

        public void Initialize(RoomServerConfig config)
        {
            _config = config;
        }

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[PlayerHandRigSpawner] OnPlayerJoined player={player} IsServer={runner.IsServer} prefabValid={_config != null && _config.PlayerHandRigPrefab.IsValid}");

            if (!runner.IsServer)
            {
                return;
            }

            NetworkObject rig = runner.Spawn(
                _config.PlayerHandRigPrefab,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                inputAuthority: player);

            Debug.Log($"[PlayerHandRigSpawner] Spawn result player={player} rig={(rig != null ? rig.name : "null")} rigId={(rig != null ? rig.Id.ToString() : "-")}");

            _spawnedRigs[player] = rig;
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[PlayerHandRigSpawner] OnPlayerLeft player={player} IsServer={runner.IsServer} hadRig={_spawnedRigs.ContainsKey(player)}");

            if (!runner.IsServer)
            {
                return;
            }

            if (_spawnedRigs.TryGetValue(player, out NetworkObject rig))
            {
                if (rig != null)
                {
                    runner.Despawn(rig);
                }

                _spawnedRigs.Remove(player);
            }
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }

        void INetworkRunnerCallbacks.OnConnectFailed(
            NetworkRunner runner,
            NetAddress remoteAddress,
            NetConnectFailedReason reason) { }

        void INetworkRunnerCallbacks.OnConnectRequest(
            NetworkRunner runner,
            NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token) { }

        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(
            NetworkRunner runner,
            Dictionary<string, object> data) { }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(
            NetworkRunner runner,
            NetDisconnectReason reason) { }

        void INetworkRunnerCallbacks.OnHostMigration(
            NetworkRunner runner,
            HostMigrationToken hostMigrationToken) { }

        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }

        void INetworkRunnerCallbacks.OnInputMissing(
            NetworkRunner runner,
            PlayerRef player,
            NetworkInput input) { }

        void INetworkRunnerCallbacks.OnObjectEnterAOI(
            NetworkRunner runner,
            NetworkObject obj,
            PlayerRef player) { }

        void INetworkRunnerCallbacks.OnObjectExitAOI(
            NetworkRunner runner,
            NetworkObject obj,
            PlayerRef player) { }

        void INetworkRunnerCallbacks.OnReliableDataProgress(
            NetworkRunner runner,
            PlayerRef player,
            ReliableKey key,
            float progress) { }

        void INetworkRunnerCallbacks.OnReliableDataReceived(
            NetworkRunner runner,
            PlayerRef player,
            ReliableKey key,
            ArraySegment<byte> data) { }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }

        void INetworkRunnerCallbacks.OnSessionListUpdated(
            NetworkRunner runner,
            List<SessionInfo> sessionList) { }

        void INetworkRunnerCallbacks.OnShutdown(
            NetworkRunner runner,
            ShutdownReason shutdownReason) { }

        void INetworkRunnerCallbacks.OnUserSimulationMessage(
            NetworkRunner runner,
            SimulationMessagePtr message) { }
    }
}
