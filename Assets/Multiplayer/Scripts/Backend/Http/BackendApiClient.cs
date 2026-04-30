using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Backend.Dto;
using UnityEngine;
using UnityEngine.Networking;

namespace Murang.Multiplayer.Backend.Http
{
    public sealed class BackendApiClient
    {
        private readonly string _backendBaseUrl;

        public BackendApiClient(string backendBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(backendBaseUrl))
            {
                throw new ArgumentException("Backend base URL is required.", nameof(backendBaseUrl));
            }

            _backendBaseUrl = backendBaseUrl.TrimEnd('/');
        }

        public Task<MetaLoginResponse> MetaLoginAsync(string metaIdToken, string nickname, CancellationToken cancellationToken)
        {
            MetaLoginRequest request = new MetaLoginRequest
            {
                metaIdToken = metaIdToken,
                nickname = nickname
            };

            return PostJsonAsync("/api/v1/auth/meta-login", request, ParseMetaLoginEnvelope, null, cancellationToken);
        }

        public Task<MetaLoginResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            RefreshTokenRequest request = new RefreshTokenRequest
            {
                refreshToken = refreshToken
            };

            return PostJsonAsync("/api/v1/auth/refresh", request, ParseMetaLoginEnvelope, null, cancellationToken);
        }

        public Task<UserMeResponse> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
        {
            return GetAsync("/api/v1/users/me", ParseUserMeEnvelope, accessToken, cancellationToken);
        }

        private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
            string path,
            TRequest request,
            Func<string, TResponse> parser,
            string accessToken,
            CancellationToken cancellationToken)
        {
            string requestJson = JsonUtility.ToJson(request);
            string responseJson = await SendAsync(UnityWebRequest.kHttpVerbPOST, path, requestJson, accessToken, cancellationToken);
            return parser(responseJson);
        }

        private async Task<TResponse> GetAsync<TResponse>(
            string path,
            Func<string, TResponse> parser,
            string accessToken,
            CancellationToken cancellationToken)
        {
            string responseJson = await SendAsync(UnityWebRequest.kHttpVerbGET, path, null, accessToken, cancellationToken);
            return parser(responseJson);
        }

        private async Task<string> SendAsync(
            string method,
            string path,
            string requestJson,
            string accessToken,
            CancellationToken cancellationToken)
        {
            string url = BuildUrl(path);

            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Accept", "application/json");

                if (!string.IsNullOrEmpty(accessToken))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + accessToken);
                }

                if (!string.IsNullOrEmpty(requestJson))
                {
                    byte[] body = Encoding.UTF8.GetBytes(requestJson);
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                using (cancellationToken.Register(request.Abort))
                {
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (request.responseCode >= 200 && request.responseCode < 300)
                {
                    return request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                }

                throw CreateApiException(request);
            }
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return _backendBaseUrl;
            }

            return path.StartsWith("/", StringComparison.Ordinal)
                ? _backendBaseUrl + path
                : _backendBaseUrl + "/" + path;
        }

        private static ApiException CreateApiException(UnityWebRequest request)
        {
            string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            ApiErrorResponse errorResponse = TryParse<ApiErrorResponse>(responseBody);

            string code = errorResponse != null && !string.IsNullOrEmpty(errorResponse.code)
                ? errorResponse.code
                : "HTTP_" + request.responseCode;
            string detail = errorResponse != null && !string.IsNullOrEmpty(errorResponse.detail)
                ? errorResponse.detail
                : request.error;

            return new ApiException(request.responseCode, code, detail, responseBody);
        }

        private static MetaLoginResponse ParseMetaLoginEnvelope(string json)
        {
            MetaLoginEnvelope envelope = TryParse<MetaLoginEnvelope>(json);
            if (envelope == null || !envelope.success || envelope.data == null)
            {
                throw new ApiException(0, "CLIENT_PARSE_ERROR", "Meta login response could not be parsed.", json);
            }

            return envelope.data;
        }

        private static UserMeResponse ParseUserMeEnvelope(string json)
        {
            UserMeEnvelope envelope = TryParse<UserMeEnvelope>(json);
            if (envelope == null || !envelope.success || envelope.data == null)
            {
                throw new ApiException(0, "CLIENT_PARSE_ERROR", "Current user response could not be parsed.", json);
            }

            return envelope.data;
        }

        private static T TryParse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        [Serializable]
        private sealed class MetaLoginEnvelope
        {
            public bool success;
            public MetaLoginResponse data;
        }

        [Serializable]
        private sealed class UserMeEnvelope
        {
            public bool success;
            public UserMeResponse data;
        }
    }
}
