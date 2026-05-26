using System;
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
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool showDebugOverlay = true;

        private readonly CancellationTokenSource _destroyCancellationTokenSource = new CancellationTokenSource();
        private Task _activeTask;
        private string _statusMessage = "대기 중";
        private string _userSummary = string.Empty;

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

        void Start()
        {
            if (runOnStart)
            {
                RunEnsureAndFetchCurrentUser();
            }
        }

        public void RunEnsureAndFetchCurrentUser()
        {
            _ = StartFlowAsync(RunEnsureAndFetchCurrentUserInternalAsync);
        }

        public void RunFreshLoginAndFetchCurrentUser()
        {
            _ = StartFlowAsync(RunFreshLoginAndFetchCurrentUserInternalAsync);
        }

        public void ClearStoredTokens()
        {
            if (IsBusy)
            {
                return;
            }

            new AuthTokenStore().DeleteAll();
            _userSummary = string.Empty;
            _statusMessage = "저장된 인증 토큰을 삭제했습니다.";
            Debug.Log("[AuthSmokeProbe] Cleared stored auth tokens.");
        }

        private bool IsBusy
        {
            get { return _activeTask != null && !_activeTask.IsCompleted; }
        }

        private Task StartFlowAsync(Func<CancellationToken, Task> flow)
        {
            if (authBootstrap == null)
            {
                _statusMessage = "AuthBootstrap reference is missing.";
                Debug.LogError("[AuthSmokeProbe] " + _statusMessage);
                return Task.CompletedTask;
            }

            if (authBootstrap.Session == null)
            {
                _statusMessage = "Auth session is not ready yet.";
                Debug.LogError("[AuthSmokeProbe] " + _statusMessage);
                return Task.CompletedTask;
            }

            if (IsBusy)
            {
                return _activeTask;
            }

            _activeTask = RunFlowAsync(flow, _destroyCancellationTokenSource.Token);
            return _activeTask;
        }

        private async Task RunFlowAsync(Func<CancellationToken, Task> flow, CancellationToken cancellationToken)
        {
            try
            {
                await flow(cancellationToken);
            }
            catch (ApiException exception)
            {
                _userSummary = string.Empty;
                _statusMessage = "백엔드 요청 실패: " + exception.Code + " - " + exception.Detail;
                Debug.LogError("[AuthSmokeProbe] " + _statusMessage);
            }
            catch (AuthFailedException exception)
            {
                _userSummary = string.Empty;
                _statusMessage = exception.Message;
                Debug.LogError("[AuthSmokeProbe] Authentication flow failed: " + exception.Message);
            }
            catch (TaskCanceledException)
            {
            }
            catch (System.Exception exception)
            {
                _userSummary = string.Empty;
                _statusMessage = "알 수 없는 오류: " + exception.Message;
                Debug.LogError("[AuthSmokeProbe] " + _statusMessage);
            }
            finally
            {
                _activeTask = null;
            }
        }

        private async Task RunEnsureAndFetchCurrentUserInternalAsync(CancellationToken cancellationToken)
        {
            _statusMessage = "저장된 세션을 확인한 뒤 /users/me 를 조회합니다.";
            AuthSession.AuthState state = await authBootstrap.EnsureAuthenticatedAsync();
            await FetchAndReportCurrentUserAsync(state, cancellationToken);
        }

        private async Task RunFreshLoginAndFetchCurrentUserInternalAsync(CancellationToken cancellationToken)
        {
            new AuthTokenStore().DeleteAll();
            _statusMessage = "저장된 토큰을 지우고 새 Meta 로그인을 시작합니다.";

            AuthSession.AuthState state = await authBootstrap.Session.LoginAsync(cancellationToken);
            await FetchAndReportCurrentUserAsync(state, cancellationToken);
        }

        private async Task FetchAndReportCurrentUserAsync(AuthSession.AuthState state, CancellationToken cancellationToken)
        {
            UserMeResponse user = await authBootstrap.Session.GetCurrentUserAsync(cancellationToken);
            _userSummary = user.nickname + " (" + user.playerId + ")";
            _statusMessage = "/users/me 성공: " + _userSummary + " / " + state.Source;
            Debug.Log("[AuthSmokeProbe] /users/me returned " + user.nickname + " (" + user.playerId + ") via " + state.Source + ".");
        }

        void OnGUI()
        {
            if (!showDebugOverlay || !Application.isPlaying)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 420f, 210f), GUI.skin.box);

            if (authBootstrap == null || authBootstrap.Config == null)
            {
                GUILayout.Label("AuthBootstrap 또는 MultiplayerAuthConfig를 찾지 못했습니다.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("Multiplayer Auth Smoke Probe");
            GUILayout.Label("Meta Mode: " + (authBootstrap.Config.UseMockMetaToken ? "Mock" : "Real"));
            GUILayout.Label("Backend: " + authBootstrap.Config.BackendBaseUrl);
            GUILayout.Label("Status: " + _statusMessage);

            if (!string.IsNullOrEmpty(_userSummary))
            {
                GUILayout.Label("User: " + _userSummary);
            }

            GUI.enabled = !IsBusy;

            if (GUILayout.Button("새 로그인 + /users/me"))
            {
                RunFreshLoginAndFetchCurrentUser();
            }

            if (GUILayout.Button("현재 세션으로 /users/me"))
            {
                RunEnsureAndFetchCurrentUser();
            }

            if (GUILayout.Button("토큰 캐시 삭제"))
            {
                ClearStoredTokens();
            }

            GUI.enabled = true;

            if (IsBusy)
            {
                GUILayout.Label("실행 중...");
            }

            GUILayout.EndArea();
        }
    }
}
