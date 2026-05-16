using System;
using System.Text;
using Murang.Multiplayer.Backend.Dto;
using UnityEngine;

namespace Murang.Multiplayer.Auth
{
    public sealed class AuthTokenStore
    {
        public const string AccessTokenKey = "murang.multiplayer.auth.access";
        public const string RefreshTokenKey = "murang.multiplayer.auth.refresh";
        public const string AccessTokenExpiresAtKey = "murang.multiplayer.auth.accessExpiresAt";
        public const string MockAccountIdKey = "murang.multiplayer.auth.mockAccountId";

        public bool TryLoad(out StoredTokens storedTokens)
        {
            string accessToken = PlayerPrefs.GetString(AccessTokenKey, string.Empty);
            string refreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
            long expiresAtUnixSeconds = 0L;
            long.TryParse(PlayerPrefs.GetString(AccessTokenExpiresAtKey, "0"), out expiresAtUnixSeconds);

            if (string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(refreshToken))
            {
                storedTokens = null;
                return false;
            }

            storedTokens = new StoredTokens(accessToken, refreshToken, expiresAtUnixSeconds);
            return true;
        }

        public void Save(MetaLoginResponse response)
        {
            long expiresAtUnixSeconds = ExtractExpirationUnixSeconds(response.accessToken);
            PlayerPrefs.SetString(AccessTokenKey, response.accessToken ?? string.Empty);
            PlayerPrefs.SetString(RefreshTokenKey, response.refreshToken ?? string.Empty);
            PlayerPrefs.SetString(AccessTokenExpiresAtKey, expiresAtUnixSeconds.ToString());
            PlayerPrefs.Save();
        }

        public void DeleteAccessToken()
        {
            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(AccessTokenExpiresAtKey);
            PlayerPrefs.Save();
        }

        public void DeleteAll()
        {
            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(AccessTokenExpiresAtKey);
            PlayerPrefs.Save();
        }

        public void ResetIfMockAccountChanged(string mockAccountId)
        {
            string normalizedAccountId = string.IsNullOrWhiteSpace(mockAccountId)
                ? string.Empty
                : mockAccountId.Trim();
            string previousAccountId = PlayerPrefs.GetString(MockAccountIdKey, string.Empty);

            if (string.Equals(previousAccountId, normalizedAccountId, StringComparison.Ordinal))
            {
                return;
            }

            DeleteAll();
            PlayerPrefs.SetString(MockAccountIdKey, normalizedAccountId);
            PlayerPrefs.Save();
        }

        public bool HasUsableAccessToken(StoredTokens storedTokens)
        {
            if (storedTokens == null || string.IsNullOrEmpty(storedTokens.AccessToken))
            {
                return false;
            }

            long nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return storedTokens.AccessTokenExpiresAtUnixSeconds > nowUnixSeconds + 30L;
        }

        private static long ExtractExpirationUnixSeconds(string jwt)
        {
            if (string.IsNullOrEmpty(jwt))
            {
                throw new InvalidOperationException("Access token is empty.");
            }

            string[] segments = jwt.Split('.');
            if (segments.Length < 2)
            {
                throw new InvalidOperationException("Access token payload is missing.");
            }

            byte[] payloadBytes = DecodeBase64Url(segments[1]);
            string payloadJson = Encoding.UTF8.GetString(payloadBytes);
            JwtPayload payload = JsonUtility.FromJson<JwtPayload>(payloadJson);
            if (payload == null || payload.exp <= 0L)
            {
                throw new InvalidOperationException("Access token expiration could not be resolved.");
            }

            return payload.exp;
        }

        private static byte[] DecodeBase64Url(string value)
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 0:
                    break;
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
                default:
                    throw new FormatException("Base64Url payload has an invalid length.");
            }

            return Convert.FromBase64String(padded);
        }

        [Serializable]
        private sealed class JwtPayload
        {
            public long exp;
        }

        public sealed class StoredTokens
        {
            public StoredTokens(string accessToken, string refreshToken, long accessTokenExpiresAtUnixSeconds)
            {
                AccessToken = accessToken;
                RefreshToken = refreshToken;
                AccessTokenExpiresAtUnixSeconds = accessTokenExpiresAtUnixSeconds;
            }

            public string AccessToken { get; }

            public string RefreshToken { get; }

            public long AccessTokenExpiresAtUnixSeconds { get; }

            public bool HasRefreshToken
            {
                get { return !string.IsNullOrEmpty(RefreshToken); }
            }
        }
    }
}
