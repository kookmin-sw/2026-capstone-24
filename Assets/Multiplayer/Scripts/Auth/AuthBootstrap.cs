using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Backend.Http;
using UnityEngine;
using UnityEngine.Events;

namespace Murang.Multiplayer.Auth
{
    [DisallowMultipleComponent]
    public sealed class AuthBootstrap : MonoBehaviour
    {
        [SerializeField] private MultiplayerAuthConfig config;
        [SerializeField] private bool authenticateOnStart = true;
        [SerializeField] private UnityEvent authenticated;
        [SerializeField] private UnityEvent authenticationFailed;

        private readonly CancellationTokenSource _destroyCancellationTokenSource = new CancellationTokenSource();
        private AuthSession _session;
        private Task<AuthSession.AuthState> _authenticationTask;

        public AuthSession Session
        {
            get { return _session; }
        }

        public MultiplayerAuthConfig Config
        {
            get { return config; }
        }

        void Awake()
        {
            if (config == null)
            {
                config = Resources.Load<MultiplayerAuthConfig>("MultiplayerAuthConfig");
            }

            if (config == null)
            {
                Debug.LogError("[AuthBootstrap] MultiplayerAuthConfig could not be loaded.");
                return;
            }

            IMetaTokenProvider metaTokenProvider = config.UseMockMetaToken
                ? new MockMetaTokenProvider(config)
                : new RealMetaTokenProvider();

            _session = new AuthSession(
                config,
                new AuthTokenStore(),
                new BackendApiClient(config.BackendBaseUrl),
                metaTokenProvider);
        }

        void Start()
        {
            if (authenticateOnStart)
            {
                _ = EnsureAuthenticatedAsync();
            }
        }

        void OnDestroy()
        {
            _destroyCancellationTokenSource.Cancel();
            _destroyCancellationTokenSource.Dispose();
        }

        public Task<AuthSession.AuthState> EnsureAuthenticatedAsync()
        {
            if (_session == null)
            {
                return Task.FromException<AuthSession.AuthState>(
                    new AuthFailedException("Authentication session is not ready."));
            }

            if (_authenticationTask != null && !_authenticationTask.IsCompleted)
            {
                return _authenticationTask;
            }

            _authenticationTask = EnsureAuthenticatedInternalAsync(_destroyCancellationTokenSource.Token);
            return _authenticationTask;
        }

        private async Task<AuthSession.AuthState> EnsureAuthenticatedInternalAsync(CancellationToken cancellationToken)
        {
            try
            {
                AuthSession.AuthState state = await _session.EnsureAuthenticatedAsync(cancellationToken);
                Debug.Log("[AuthBootstrap] Authenticated via " + state.Source + ". PlayerPrefs auth cache is ready.");
                if (authenticated != null)
                {
                    authenticated.Invoke();
                }

                return state;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[AuthBootstrap] Authentication failed: " + exception.Message);
                if (authenticationFailed != null)
                {
                    authenticationFailed.Invoke();
                }

                throw;
            }
            finally
            {
                _authenticationTask = null;
            }
        }
    }
}
