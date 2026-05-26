using System;
using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Backend.Dto;
using Murang.Multiplayer.Backend.Http;

namespace Murang.Multiplayer.Auth
{
    public sealed class AuthSession
    {
        private readonly MultiplayerAuthConfig _config;
        private readonly AuthTokenStore _tokenStore;
        private readonly BackendApiClient _backendApiClient;
        private readonly IMetaTokenProvider _metaTokenProvider;

        public AuthSession(
            MultiplayerAuthConfig config,
            AuthTokenStore tokenStore,
            BackendApiClient backendApiClient,
            IMetaTokenProvider metaTokenProvider)
        {
            _config = config;
            _tokenStore = tokenStore;
            _backendApiClient = backendApiClient;
            _metaTokenProvider = metaTokenProvider;
        }

        public async Task<AuthState> EnsureAuthenticatedAsync(CancellationToken cancellationToken)
        {
            if (_config.UseMockMetaToken)
            {
                _tokenStore.ResetIfMockAccountChanged(_config.ResolveMockAccountId());
            }

            if (_tokenStore.TryLoad(out AuthTokenStore.StoredTokens storedTokens))
            {
                if (_tokenStore.HasUsableAccessToken(storedTokens))
                {
                    return new AuthState(AuthSource.CachedAccessToken, storedTokens.AccessToken, storedTokens.RefreshToken);
                }

                if (storedTokens.HasRefreshToken)
                {
                    try
                    {
                        return await RefreshAsync(cancellationToken);
                    }
                    catch (AuthFailedException)
                    {
                    }
                }
            }

            return await LoginAsync(cancellationToken);
        }

        public async Task<UserMeResponse> GetCurrentUserAsync(CancellationToken cancellationToken)
        {
            return await ExecuteAuthorizedAsync(
                accessToken => _backendApiClient.GetCurrentUserAsync(accessToken, cancellationToken),
                cancellationToken);
        }

        public async Task<AuthState> LoginAsync(CancellationToken cancellationToken)
        {
            try
            {
                MetaAuthenticationResult authenticationResult =
                    await _metaTokenProvider.GetAuthenticationResultAsync(cancellationToken);
                string nickname = _config.ResolveNickname(authenticationResult);
                MetaLoginResponse response = await _backendApiClient.MetaLoginAsync(
                    authenticationResult.MetaIdToken,
                    nickname,
                    cancellationToken);
                return SaveAndCreateState(response, AuthSource.MetaLogin);
            }
            catch (ApiException exception)
            {
                throw new AuthFailedException(BuildMetaLoginApiFailureMessage(exception), exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new AuthFailedException(exception.Message, exception);
            }
            catch (Exception exception) when (!(exception is AuthFailedException))
            {
                throw new AuthFailedException("Meta 로그인에 실패했습니다. " + exception.Message, exception);
            }
        }

        public async Task<AuthState> RefreshAsync(CancellationToken cancellationToken)
        {
            if (!_tokenStore.TryLoad(out AuthTokenStore.StoredTokens storedTokens) || !storedTokens.HasRefreshToken)
            {
                throw new AuthFailedException("저장된 Refresh Token이 없습니다.");
            }

            try
            {
                MetaLoginResponse response = await _backendApiClient.RefreshAsync(storedTokens.RefreshToken, cancellationToken);
                return SaveAndCreateState(response, AuthSource.RefreshedToken);
            }
            catch (ApiException exception) when (exception.StatusCode == 401L)
            {
                _tokenStore.DeleteAll();
                throw new AuthFailedException("저장된 Refresh Token이 유효하지 않습니다.", exception);
            }
            catch (Exception exception) when (!(exception is AuthFailedException))
            {
                throw new AuthFailedException("Refresh Token으로 인증을 갱신하지 못했습니다.", exception);
            }
        }

        private async Task<T> ExecuteAuthorizedAsync<T>(
            Func<string, Task<T>> requestFunc,
            CancellationToken cancellationToken)
        {
            AuthState state = await EnsureAuthenticatedAsync(cancellationToken);

            try
            {
                return await requestFunc(state.AccessToken);
            }
            catch (ApiException exception) when (exception.StatusCode == 401L)
            {
                _tokenStore.DeleteAccessToken();

                AuthState recoveredState;
                try
                {
                    recoveredState = await RefreshAsync(cancellationToken);
                }
                catch (AuthFailedException)
                {
                    recoveredState = await LoginAsync(cancellationToken);
                }

                return await requestFunc(recoveredState.AccessToken);
            }
        }

        private AuthState SaveAndCreateState(MetaLoginResponse response, AuthSource source)
        {
            _tokenStore.Save(response);
            return new AuthState(source, response.accessToken, response.refreshToken);
        }

        private string BuildMetaLoginApiFailureMessage(ApiException exception)
        {
            string detail = string.IsNullOrWhiteSpace(exception.Detail) ? exception.Code : exception.Detail;
            if (_config.UseMockMetaToken)
            {
                return "Meta 로그인 API 호출이 실패했습니다. " + detail;
            }

            return "Meta 로그인 API 호출이 실패했습니다. 실 Meta 토큰 경로를 쓰려면 Meta Platform SDK와 Android Quest 실기기, 그리고 해당 토큰을 이해하는 서버 verifier가 함께 준비되어야 합니다. "
                + detail;
        }

        public enum AuthSource
        {
            CachedAccessToken,
            RefreshedToken,
            MetaLogin
        }

        public sealed class AuthState
        {
            public AuthState(AuthSource source, string accessToken, string refreshToken)
            {
                Source = source;
                AccessToken = accessToken;
                RefreshToken = refreshToken;
            }

            public AuthSource Source { get; }

            public string AccessToken { get; }

            public string RefreshToken { get; }
        }
    }
}
