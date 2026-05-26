using System;
using System.Threading;
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

        private bool _activated;
        private string _currentAccessToken;
        private string _currentPlayerId;
        private string _currentNickname;

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
        }

        void OnDestroy()
        {
            if (activateButton != null)
            {
                activateButton.onClick.RemoveListener(OnActivateClicked);
            }
        }

        private void OnActivateClicked()
        {
            if (_activated)
            {
                return;
            }

            _activated = true;
            _ = ActivateAsync();
        }

        private async System.Threading.Tasks.Task ActivateAsync()
        {
            if (multiplayerAuthBootstrap != null)
            {
                multiplayerAuthBootstrap.SetActive(true);
            }

            if (authBootstrap == null)
            {
                SetStatus("Failed: AuthBootstrap reference is missing.");
                return;
            }

            try
            {
                AuthSession.AuthState state = await authBootstrap.EnsureAuthenticatedAsync();
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
            catch (System.Exception exception)
            {
                _currentAccessToken = null;
                _currentPlayerId = null;
                _currentNickname = null;
                SetStatus("Failed: " + exception.Message);
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
