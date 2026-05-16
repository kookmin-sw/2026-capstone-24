using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using Murang.Multiplayer.Auth;
using Murang.Multiplayer.Room.Client;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// World-space 인-룸 패널. <see cref="MultiplayerLobbyPanel.OnEnteredRoom"/>
    /// 이벤트로 활성화되어 (1) 현재 룸 참가자 닉네임 행을 실시간으로 표시하고
    /// (2) Leave 버튼으로 룸을 떠나 로비 패널로 복귀시킨다.
    ///
    /// <para>1차 출시 범위: <see cref="ParticipantDisplayFormatter"/> 의 playerId
    /// prefix 기반 행. nickname 채널은 후속 plan.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MultiplayerInRoomPanel : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Dependencies")]
        [SerializeField] private MultiplayerLobbyPanel lobbyPanel;
        [SerializeField] private MultiplayerAuthGate authGate;
        [SerializeField] private RoomClient roomClient;
        [SerializeField] private NetworkRunner networkRunner;

        [Header("UI — Participant list")]
        [SerializeField] private Transform participantRowParent;
        [SerializeField] private ParticipantRowEntry participantRowPrefab;
        [SerializeField] private TMP_Text roomNameLabel;
        [SerializeField] private TMP_Text participantCountLabel;

        [Header("UI — Status / Root")]
        [SerializeField] private Button leaveButton;
        [SerializeField] private GameObject inRoomRoot;

        private string _currentRoomName;
        private bool _isInRoom;
        private bool _callbacksRegistered;
        private readonly List<ParticipantRowEntry> _rowPool = new List<ParticipantRowEntry>();
        private readonly HashSet<PlayerRef> _knownPlayers = new HashSet<PlayerRef>();

        void Awake()
        {
            if (inRoomRoot != null)
            {
                inRoomRoot.SetActive(false);
            }

            if (lobbyPanel != null)
            {
                lobbyPanel.OnEnteredRoom += HandleEnteredRoom;
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(OnLeaveClicked);
            }
        }

        void OnDestroy()
        {
            if (lobbyPanel != null)
            {
                lobbyPanel.OnEnteredRoom -= HandleEnteredRoom;
            }
            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveListener(OnLeaveClicked);
            }
            UnregisterCallbacks();
        }

        private void HandleEnteredRoom(string roomName)
        {
            _currentRoomName = roomName;
            _isInRoom = true;
            _knownPlayers.Clear();

            if (inRoomRoot != null)
            {
                inRoomRoot.SetActive(true);
            }
            if (roomNameLabel != null)
            {
                roomNameLabel.text = string.IsNullOrEmpty(roomName) ? "(unnamed room)" : roomName;
            }

            RegisterCallbacks();
            RefreshFromRunner();
        }

        private void RegisterCallbacks()
        {
            if (_callbacksRegistered || networkRunner == null)
            {
                return;
            }
            networkRunner.AddCallbacks(this);
            _callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            if (!_callbacksRegistered || networkRunner == null)
            {
                _callbacksRegistered = false;
                return;
            }
            networkRunner.RemoveCallbacks(this);
            _callbacksRegistered = false;
        }

        private void RefreshFromRunner()
        {
            _knownPlayers.Clear();
            if (networkRunner != null && networkRunner.IsRunning)
            {
                foreach (PlayerRef player in networkRunner.ActivePlayers)
                {
                    _knownPlayers.Add(player);
                }
            }
            RebuildRows();
        }

        private void RebuildRows()
        {
            if (participantRowParent == null || participantRowPrefab == null)
            {
                UpdateParticipantCount(_knownPlayers.Count);
                return;
            }

            EnsureRowCapacity(_knownPlayers.Count);

            int index = 0;
            string localPlayerId = authGate != null ? authGate.CurrentPlayerId : null;
            PlayerRef localRef = networkRunner != null ? networkRunner.LocalPlayer : default;

            foreach (PlayerRef player in _knownPlayers)
            {
                ParticipantRowEntry row = _rowPool[index];
                row.gameObject.SetActive(true);
                row.Bind(FormatRow(player, localRef, localPlayerId));
                index++;
            }
            for (int i = index; i < _rowPool.Count; i++)
            {
                _rowPool[i].gameObject.SetActive(false);
            }

            UpdateParticipantCount(_knownPlayers.Count);
        }

        private void EnsureRowCapacity(int needed)
        {
            while (_rowPool.Count < needed)
            {
                ParticipantRowEntry instance = Instantiate(participantRowPrefab, participantRowParent);
                _rowPool.Add(instance);
            }
        }

        private void UpdateParticipantCount(int count)
        {
            if (participantCountLabel == null)
            {
                return;
            }
            int max = (networkRunner != null && networkRunner.SessionInfo != null)
                ? networkRunner.SessionInfo.MaxPlayers
                : 0;
            participantCountLabel.text = max > 0 ? $"{count}/{max}" : count.ToString();
        }

        private string FormatRow(PlayerRef player, PlayerRef localRef, string localPlayerId)
        {
            bool isLocal = player == localRef;
            if (isLocal)
            {
                return ParticipantDisplayFormatter.FormatLocal(localPlayerId);
            }
            // 다른 유저 nickname 채널 없음 — PlayerRef 번호로 fallback
            return ParticipantDisplayFormatter.FormatRemote(player.PlayerId, playerIdOrNull: null);
        }

        private async void OnLeaveClicked()
        {
            if (!_isInRoom || roomClient == null)
            {
                return;
            }

            if (leaveButton != null)
            {
                leaveButton.interactable = false;
            }

            try
            {
                await roomClient.LeaveRoomAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerInRoomPanel] Leave 실패: {ex}");
            }

            HandleLeftRoom();
        }

        private void HandleLeftRoom()
        {
            _isInRoom = false;
            _currentRoomName = null;
            _knownPlayers.Clear();
            for (int i = 0; i < _rowPool.Count; i++)
            {
                _rowPool[i].gameObject.SetActive(false);
            }
            UnregisterCallbacks();

            if (inRoomRoot != null)
            {
                inRoomRoot.SetActive(false);
            }
            if (leaveButton != null)
            {
                leaveButton.interactable = true;
            }
            if (lobbyPanel != null)
            {
                lobbyPanel.ShowLobby();
            }
        }

        // === INetworkRunnerCallbacks ===

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!_isInRoom)
            {
                return;
            }
            _knownPlayers.Add(player);
            RebuildRows();
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!_isInRoom)
            {
                return;
            }
            _knownPlayers.Remove(player);
            RebuildRows();
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
        {
            if (_isInRoom)
            {
                RefreshFromRunner();
            }
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (_isInRoom)
            {
                HandleLeftRoom();
            }
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (_isInRoom)
            {
                HandleLeftRoom();
            }
        }

        // 나머지 콜백 — 본 패널은 관심 없음. RoomClient 가 자체 처리하므로 빈 메서드로 둠.

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<Fusion.Photon.Realtime.SessionInfo> sessionList) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    }
}
