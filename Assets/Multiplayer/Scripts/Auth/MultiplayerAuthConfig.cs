using System.Text;
using UnityEngine;

namespace Murang.Multiplayer.Auth
{
    [CreateAssetMenu(fileName = "MultiplayerAuthConfig", menuName = "Multiplayer/Auth Config")]
    public sealed class MultiplayerAuthConfig : ScriptableObject
    {
        [SerializeField] private string editorBackendBaseUrl = "http://localhost:8080";
        [SerializeField] private string deviceBackendBaseUrl = string.Empty;
        [SerializeField] private bool useMockMetaToken = true;
        [SerializeField] private string mockMetaTokenPrefix = "mock-meta:";
        [SerializeField] private string mockAccountId = "quest-user-01";
        [SerializeField] private string defaultNickname = "Murang Quest User";
        [SerializeField] private string nicknameOverride = string.Empty;
        [SerializeField] private int nicknameAccountSuffixLength = 6;

        public string BackendBaseUrl
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return SanitizeBackendBaseUrl(deviceBackendBaseUrl, editorBackendBaseUrl);
#else
                return SanitizeBackendBaseUrl(editorBackendBaseUrl, deviceBackendBaseUrl);
#endif
            }
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

        public string ResolveNickname(MetaAuthenticationResult authenticationResult)
        {
            string configuredNickname = NormalizeNickname(nicknameOverride);
            if (!string.IsNullOrEmpty(configuredNickname))
            {
                return configuredNickname;
            }

            string baseNickname = NormalizeNickname(DefaultNickname);
            if (string.IsNullOrEmpty(baseNickname))
            {
                baseNickname = "Murang Quest User";
            }

            string accountSuffix = BuildAccountSuffix(authenticationResult != null ? authenticationResult.MetaAccountId : string.Empty);
            string generatedNickname = NormalizeNickname(baseNickname + " " + accountSuffix);
            if (generatedNickname.Length <= 32 && generatedNickname.Length >= 2)
            {
                return generatedNickname;
            }

            int allowedBaseLength = Mathf.Max(2, 32 - accountSuffix.Length - 1);
            string trimmedBase = baseNickname.Length > allowedBaseLength
                ? baseNickname.Substring(0, allowedBaseLength).TrimEnd()
                : baseNickname;

            string trimmedNickname = NormalizeNickname(trimmedBase + " " + accountSuffix);
            return trimmedNickname.Length >= 2 ? trimmedNickname : "Murang " + accountSuffix;
        }

        private string BuildAccountSuffix(string metaAccountId)
        {
            int suffixLength = Mathf.Clamp(nicknameAccountSuffixLength, 4, 12);
            StringBuilder builder = new StringBuilder();

            if (!string.IsNullOrEmpty(metaAccountId))
            {
                for (int index = 0; index < metaAccountId.Length; index++)
                {
                    char character = metaAccountId[index];
                    if (char.IsLetterOrDigit(character))
                    {
                        builder.Append(char.ToUpperInvariant(character));
                    }
                }
            }

            if (builder.Length == 0)
            {
                return "USER";
            }

            string normalizedAccountId = builder.ToString();
            return normalizedAccountId.Length <= suffixLength
                ? normalizedAccountId
                : normalizedAccountId.Substring(normalizedAccountId.Length - suffixLength, suffixLength);
        }

        private static string NormalizeNickname(string rawNickname)
        {
            if (string.IsNullOrWhiteSpace(rawNickname))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(rawNickname.Length);
            bool pendingSpace = false;

            for (int index = 0; index < rawNickname.Length; index++)
            {
                char character = rawNickname[index];
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSpace && builder.Length > 0)
                    {
                        builder.Append(' ');
                    }

                    builder.Append(character);
                    pendingSpace = false;
                }
                else if (char.IsWhiteSpace(character))
                {
                    pendingSpace = builder.Length > 0;
                }
            }

            string normalized = builder.ToString().Trim();
            if (normalized.Length > 32)
            {
                normalized = normalized.Substring(0, 32).TrimEnd();
            }

            return normalized;
        }

        private static string SanitizeBackendBaseUrl(string primary, string fallback)
        {
            string sanitizedPrimary = SanitizeBackendBaseUrl(primary);
            if (!string.IsNullOrEmpty(sanitizedPrimary))
            {
                return sanitizedPrimary;
            }

            string sanitizedFallback = SanitizeBackendBaseUrl(fallback);
            return string.IsNullOrEmpty(sanitizedFallback) ? "http://localhost:8080" : sanitizedFallback;
        }

        private static string SanitizeBackendBaseUrl(string candidate)
        {
            return string.IsNullOrWhiteSpace(candidate) ? string.Empty : candidate.Trim().TrimEnd('/');
        }
    }
}
