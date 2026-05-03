using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using Fusion.Sockets;
using Murang.Multiplayer.Room.Common;
using UnityEngine;

namespace Murang.Multiplayer.Room.Server
{
    [DisallowMultipleComponent]
    public sealed class RoomServerAutomationMonitor : MonoBehaviour, INetworkRunnerCallbacks
    {
        private const string ReadyFileArgument = "-roomServerReadyFile";
        private const string ResultPathArgument = "-roomServerResultPath";
        private const string QuitOnShutdownArgument = "-roomServerQuitOnShutdown";

        private string _roomName = "murang-room";
        private int _maxPlayers = 8;
        private string _readyFilePath = string.Empty;
        private string _resultPath = string.Empty;
        private bool _quitOnShutdown;
        private bool _isAutomationEnabled;
        private bool _readySignaled;
        private int _currentPlayers;
        private int _peakPlayers;
        private int _joinEvents;
        private int _leaveEvents;
        private string _status = "booting";
        private string _shutdownReason = string.Empty;
        private string _startedAtUtc = string.Empty;
        private string _readyAtUtc = string.Empty;
        private string _completedAtUtc = string.Empty;

        public bool IsAutomationEnabled
        {
            get { return _isAutomationEnabled; }
        }

        public void Initialize(string roomName, int maxPlayers)
        {
            _roomName = string.IsNullOrWhiteSpace(roomName) ? "murang-room" : roomName.Trim();
            _maxPlayers = Mathf.Max(1, maxPlayers);
            _readyFilePath = RoomAutomationCommandLine.GetString(ReadyFileArgument, string.Empty);
            _resultPath = RoomAutomationCommandLine.GetString(ResultPathArgument, string.Empty);
            _quitOnShutdown = RoomAutomationCommandLine.GetBool(QuitOnShutdownArgument);
            _isAutomationEnabled = !string.IsNullOrEmpty(_readyFilePath)
                || !string.IsNullOrEmpty(_resultPath)
                || _quitOnShutdown;
            _status = "starting";
            _startedAtUtc = DateTime.UtcNow.ToString("O");

            if (_isAutomationEnabled)
            {
                WriteSnapshot();
            }
        }

        private void Update()
        {
            if (!_isAutomationEnabled || _readySignaled)
            {
                return;
            }

            NetworkRunner runner = GetComponent<NetworkRunner>();
            if (runner == null || !runner.IsRunning || !runner.IsServer)
            {
                return;
            }

            _readySignaled = true;
            _status = "running";
            _readyAtUtc = DateTime.UtcNow.ToString("O");
            if (!string.IsNullOrEmpty(_readyFilePath))
            {
                EnsureParentDirectoryExists(_readyFilePath);
                File.WriteAllText(_readyFilePath, "ready");
            }

            WriteSnapshot();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!_isAutomationEnabled)
            {
                return;
            }

            _currentPlayers += 1;
            _peakPlayers = Mathf.Max(_peakPlayers, _currentPlayers);
            _joinEvents += 1;
            WriteSnapshot();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!_isAutomationEnabled)
            {
                return;
            }

            _currentPlayers = Mathf.Max(0, _currentPlayers - 1);
            _leaveEvents += 1;
            WriteSnapshot();
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (!_isAutomationEnabled)
            {
                return;
            }

            _status = "shutdown";
            _shutdownReason = shutdownReason.ToString();
            _completedAtUtc = DateTime.UtcNow.ToString("O");
            WriteSnapshot();

            if (_quitOnShutdown)
            {
                Environment.Exit(0);
            }
        }

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
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

        private void WriteSnapshot()
        {
            if (string.IsNullOrEmpty(_resultPath))
            {
                return;
            }

            EnsureParentDirectoryExists(_resultPath);
            File.WriteAllText(_resultPath, JsonUtility.ToJson(new Snapshot
            {
                roomName = _roomName,
                maxPlayers = _maxPlayers,
                currentPlayers = _currentPlayers,
                peakPlayers = _peakPlayers,
                joinEvents = _joinEvents,
                leaveEvents = _leaveEvents,
                status = _status,
                shutdownReason = _shutdownReason,
                startedAtUtc = _startedAtUtc,
                readyAtUtc = _readyAtUtc,
                completedAtUtc = _completedAtUtc
            }, true));
        }

        private static void EnsureParentDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        [Serializable]
        private sealed class Snapshot
        {
            public string roomName;
            public int maxPlayers;
            public int currentPlayers;
            public int peakPlayers;
            public int joinEvents;
            public int leaveEvents;
            public string status;
            public string shutdownReason;
            public string startedAtUtc;
            public string readyAtUtc;
            public string completedAtUtc;
        }
    }
}
