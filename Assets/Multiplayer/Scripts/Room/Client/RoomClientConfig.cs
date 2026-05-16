using Fusion;
using UnityEngine;

namespace Murang.Multiplayer.Room.Client
{
    [CreateAssetMenu(fileName = "RoomClientConfig", menuName = "Multiplayer/Room Client Config")]
    public sealed class RoomClientConfig : ScriptableObject
    {
        [SerializeField] private SessionLobby sessionLobby = SessionLobby.ClientServer;
        [SerializeField] private string customLobbyName = string.Empty;
        [SerializeField] private bool useDefaultPhotonCloudPorts;
        [SerializeField] private float joinVerdictTimeoutSeconds = 5f;

        public SessionLobby SessionLobby
        {
            get { return sessionLobby; }
        }

        public string CustomLobbyName
        {
            get { return string.IsNullOrWhiteSpace(customLobbyName) ? string.Empty : customLobbyName.Trim(); }
        }

        public bool UseDefaultPhotonCloudPorts
        {
            get { return useDefaultPhotonCloudPorts; }
        }

        public float JoinVerdictTimeoutSeconds
        {
            get { return Mathf.Max(1f, joinVerdictTimeoutSeconds); }
        }
    }
}
