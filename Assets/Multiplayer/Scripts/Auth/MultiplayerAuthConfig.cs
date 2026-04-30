using UnityEngine;

namespace Murang.Multiplayer.Auth
{
    [CreateAssetMenu(fileName = "MultiplayerAuthConfig", menuName = "Multiplayer/Auth Config")]
    public sealed class MultiplayerAuthConfig : ScriptableObject
    {
        [SerializeField] private string backendBaseUrl = "http://localhost:8080";
        [SerializeField] private bool useMockMetaToken = true;
        [SerializeField] private string mockMetaTokenPrefix = "mock-meta:";
        [SerializeField] private string mockAccountId = "quest-user-01";
        [SerializeField] private string defaultNickname = "Murang Quest User";

        public string BackendBaseUrl
        {
            get { return string.IsNullOrWhiteSpace(backendBaseUrl) ? "http://localhost:8080" : backendBaseUrl.TrimEnd('/'); }
        }

        public bool UseMockMetaToken
        {
            get { return useMockMetaToken; }
        }

        public string MockMetaTokenPrefix
        {
            get { return string.IsNullOrEmpty(mockMetaTokenPrefix) ? "mock-meta:" : mockMetaTokenPrefix; }
        }

        public string MockAccountId
        {
            get { return string.IsNullOrEmpty(mockAccountId) ? "quest-user-01" : mockAccountId; }
        }

        public string DefaultNickname
        {
            get { return string.IsNullOrEmpty(defaultNickname) ? "Murang Quest User" : defaultNickname; }
        }
    }
}
