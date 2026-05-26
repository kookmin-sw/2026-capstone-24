using System;
using System.Threading;
using Murang.Multiplayer.Presence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murang.Multiplayer.Auth
{
    public sealed class MultiplayerAuthGate : MonoBehaviour
    {
        [SerializeField] private GameObject multiplayerAuthBootstrap;
        [SerializeField] private AuthBootstrap authBootstrap;
        [SerializeField] private Button activateButton;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button confirmButton;

        private bool _activated;
        private string _currentAccessToken;
        private string _currentPlayerId;
        private string _currentNickname;
        private string _pendingNickname;

        private enum Phase
        {
            Idle,
            PrimaryAuth,
            NicknameRequired,
            SecondaryRegister
        }

        private Phase _phase = Phase.Idle;

        /// <summary>인증 성공 직후 발화. 후속 패널(MultiplayerLobbyPanel 등)이 활성화 트리거로 사용.</summary>
        public event Action OnAuthenticationCompleted;

        /// <summary>인증 완료 + access token 보유 상태인지.</summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_currentAccessToken);

        /// <summary>가장 최근에 받은 JWT access token. 후속 backend 호출(예: 룸 생성)에 사용.</summary>
        public string CurrentAccessToken => _currentAccessToken;

        /// <summary>로그인된 유저의 ULID playerId.</summary>
        public string CurrentPlayerId => _currentPlayerId;

        /// <summary>로그인된 유저의 표시 닉네임.</summary>
        public string CurrentNickname => _currentNickname;

        void Awake()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }

            if (activateButton != null)
            {
                activateButton.onClick.AddListener(OnActivateClicked);
            }

            if (nicknameInput != null)
            {
                nicknameInput.gameObject.SetActive(false);
            }

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(false);
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }
        }

        void OnDestroy()
        {
            if (activateButton != null)
            {
                activateButton.onClick.RemoveListener(OnActivateClicked);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }
        }

        private void OnActivateClicked()
        {
            if (_activated)
            {
                return;
            }

            _activated = true;
            _phase = Phase.PrimaryAuth;
            _ = ActivatePrimaryAsync();
        }

        private async System.Threading.Tasks.Task ActivatePrimaryAsync()
        {
            if (multiplayerAuthBootstrap != null)
            {
                multiplayerAuthBootstrap.SetActive(true);
            }

            if (authBootstrap == null)
            {
                SetStatus("Failed: AuthBootstrap reference is missing.");
                _activated = false;
                return;
            }

            try
            {
                AuthSession.AuthState state = await authBootstrap.EnsureAuthenticatedAsync(null);
                await HandleAuthSuccess(state);
            }
            catch (AuthFailedException exception) when (exception.ApiCode == "AUTH_NICKNAME_REQUIRED")
            {
                _activated = false;
                _phase = Phase.NicknameRequired;
                ShowNicknameForm();
            }
            catch (System.Exception exception)
            {
                _currentAccessToken = null;
                _currentPlayerId = null;
                _currentNickname = null;
                _activated = false;
                _phase = Phase.Idle;
                SetStatus("Failed: " + exception.Message);
            }
        }

        private void OnConfirmClicked()
        {
            if (_phase != Phase.NicknameRequired)
            {
                return;
            }

            string raw = nicknameInput != null ? nicknameInput.text : string.Empty;
            var validation = LobbyNicknameInputValidator.Validate(raw);
            if (!validation.Success)
            {
                SetStatus("Invalid: " + validation.ErrorMessage);
                return;
            }

            _pendingNickname = raw.Trim();
            _phase = Phase.SecondaryRegister;
            _ = RegisterAsync();
        }

        private async System.Threading.Tasks.Task RegisterAsync()
        {
            if (authBootstrap == null)
            {
                SetStatus("Failed: AuthBootstrap reference is missing.");
                _phase = Phase.NicknameRequired;
                return;
            }

            try
            {
                AuthSession.AuthState state = await authBootstrap.EnsureAuthenticatedAsync(_pendingNickname);
                HideNicknameForm();
                await HandleAuthSuccess(state);
            }
            catch (System.Exception exception)
            {
                _phase = Phase.NicknameRequired;
                SetStatus("Failed: " + exception.Message);
                // nicknameInput.text 는 보존 (코드에서 건드리지 않음)
                // NicknameInput / ConfirmButton 활성 유지
            }
        }

        private async System.Threading.Tasks.Task HandleAuthSuccess(AuthSession.AuthState state)
        {
            _currentAccessToken = state.AccessToken;

            string nickname = null;
            string playerId = null;
            try
            {
                var user = await authBootstrap.Session.GetCurrentUserAsync(CancellationToken.None);
                nickname = user?.nickname;
                playerId = user?.playerId;
            }
            catch
            {
                // nickname/playerId 조회 실패 시 null 유지 — 인증 자체는 성공
            }

            _currentNickname = nickname;
            _currentPlayerId = playerId;

            SetStatus("Multiplayer Active — " + (nickname ?? "Unknown"));
            OnAuthenticationCompleted?.Invoke();
        }

        private void ShowNicknameForm()
        {
            if (activateButton != null)
            {
                activateButton.gameObject.SetActive(false);
            }

            if (nicknameInput != null)
            {
                nicknameInput.gameObject.SetActive(true);
            }

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
            }

            SetStatus("Enter a nickname (1-16 chars, letters/digits/_/-)");
        }

        private void HideNicknameForm()
        {
            if (nicknameInput != null)
            {
                nicknameInput.gameObject.SetActive(false);
            }

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(false);
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }
    }
}
