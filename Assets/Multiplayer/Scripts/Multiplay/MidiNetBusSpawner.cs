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
    /// OnSceneLoadDone 시 룸당 1개의 MidiNetBus NetworkObject 를 spawn 한다.
    /// server-only 가드: client build 는 IsServer=false 이므로 spawn 분기에 진입하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MidiNetBusSpawner : MonoBehaviour, INetworkRunnerCallbacks
    {
        private RoomServerConfig _config;
        private NetworkObject _spawnedBus;

        public void Initialize(RoomServerConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 룸당 1개의 MidiNetBus NetworkObject 를 server 측에서 spawn 한다.
        /// RoomServerBootstrap 이 StartGame 성공 직후 명시적으로 호출.
        /// idempotent — 이미 spawn 됐으면 skip.
        /// </summary>
        public void SpawnNow(NetworkRunner runner)
        {
            Debug.Log($"[MidiNetBus] SpawnNow IsServer={runner.IsServer} alreadySpawned={_spawnedBus != null} prefabValid={_config != null && _config.MidiNetBusPrefab.IsValid}");

            if (!runner.IsServer)
                return;

            if (_spawnedBus != null)
            {
                Debug.Log("[MidiNetBus] MidiNetBus already spawned, skipping");
                return;
            }

            if (_config == null || !_config.MidiNetBusPrefab.IsValid)
            {
                Debug.LogWarning("[MidiNetBus] MidiNetBusPrefab is not set or invalid in RoomServerConfig — skipping MidiNetBus spawn");
                return;
            }

            _spawnedBus = runner.Spawn(
                _config.MidiNetBusPrefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: PlayerRef.None);

            Debug.Log($"[MidiNetBus] MidiNetBus spawned: id={(_spawnedBus != null ? _spawnedBus.Id.ToString() : "null")}");
        }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
        {
            // fallback — RoomServerBootstrap 가 명시적 SpawnNow 를 빠뜨린 경우 대비.
            // idempotent guard 가 SpawnNow 안에 있어 중복 spawn 없음.
            Debug.Log($"[MidiNetBus] OnSceneLoadDone (fallback) IsServer={runner.IsServer}");
            SpawnNow(runner);
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[MidiNetBus] MidiNetBusSpawner.OnPlayerLeft player={player} IsServer={runner.IsServer}");

            if (!runner.IsServer)
                return;

            if (_spawnedBus == null)
                return;

            MidiNetBus bus = _spawnedBus.GetComponent<MidiNetBus>();
            if (bus != null)
            {
                bus.FlushSustainedNotesForPlayer(runner, player);
            }
        }

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

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
