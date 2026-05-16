using System.Collections.Generic;
using Fusion;
using Murang.Multiplayer.Room.Common;
using UnityEngine;

namespace Murang.Multiplayer.Room.Server
{
    [CreateAssetMenu(fileName = "RoomServerConfig", menuName = "Multiplayer/Room Server Config")]
    public sealed class RoomServerConfig : ScriptableObject
    {
        [SerializeField] private string roomName = "murang-room";
        [SerializeField] private int maxPlayers = 8;
        [SerializeField] private string passwordHash = string.Empty;
        [SerializeField] private bool isVisible = true;
        [SerializeField] private string customLobbyName = string.Empty;
        [SerializeField] private bool useDefaultPhotonCloudPorts;

        public string RoomName
        {
            get { return string.IsNullOrWhiteSpace(roomName) ? "murang-room" : roomName.Trim(); }
        }

        public int MaxPlayers
        {
            get { return Mathf.Max(1, maxPlayers); }
        }

        public string PasswordHash
        {
            get { return RoomPasswordHasher.NormalizeHash(passwordHash); }
        }

        public bool HasPassword
        {
            get { return !string.IsNullOrEmpty(PasswordHash); }
        }

        public bool IsVisible
        {
            get { return isVisible; }
        }

        public string CustomLobbyName
        {
            get { return string.IsNullOrWhiteSpace(customLobbyName) ? string.Empty : customLobbyName.Trim(); }
        }

        public bool UseDefaultPhotonCloudPorts
        {
            get { return useDefaultPhotonCloudPorts; }
        }

        public Dictionary<string, SessionProperty> BuildSessionProperties()
        {
            return new Dictionary<string, SessionProperty>
            {
                [RoomSessionPropertyKeys.IsLocked] = HasPassword
            };
        }
    }
}
