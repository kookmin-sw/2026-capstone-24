using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Backend.Dto;
using Murang.Multiplayer.Backend.Http;
using UnityEngine;

namespace Murang.Multiplayer.Auth
{
    [DisallowMultipleComponent]
    public sealed class AuthSmokeProbe : MonoBehaviour
    {
        [SerializeField] private AuthBootstrap authBootstrap;

        private readonly CancellationTokenSource _destroyCancellationTokenSource = new CancellationTokenSource();

        void Awake()
        {
            if (authBootstrap == null)
            {
                authBootstrap = GetComponent<AuthBootstrap>();
            }
        }

        void OnDestroy()
        {
            _destroyCancellationTokenSource.Cancel();
            _destroyCancellationTokenSource.Dispose();
        }

        async void Start()
        {
            if (authBootstrap == null)
            {
                Debug.LogError("[AuthSmokeProbe] AuthBootstrap reference is missing.");
                return;
            }

            try
            {
                AuthSession.AuthState state = await authBootstrap.EnsureAuthenticatedAsync();
                UserMeResponse user = await authBootstrap.Session.GetCurrentUserAsync(_destroyCancellationTokenSource.Token);
                Debug.Log("[AuthSmokeProbe] /users/me returned " + user.nickname + " (" + user.metaAccountId + ") via " + state.Source + ".");
            }
            catch (ApiException exception)
            {
                Debug.LogError("[AuthSmokeProbe] Backend request failed: " + exception.Code + " - " + exception.Detail);
            }
            catch (AuthFailedException exception)
            {
                Debug.LogError("[AuthSmokeProbe] Authentication flow failed: " + exception.Message);
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}
