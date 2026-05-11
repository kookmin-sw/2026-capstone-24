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
                await authBootstrap.EnsureAuthenticatedAsync();

                string nickname = null;
                try
                {
                    var user = await authBootstrap.Session.GetCurrentUserAsync(CancellationToken.None);
                    nickname = user?.nickname;
                }
                catch
                {
                    // nickname 조회 실패 시 null 유지 — 인증 자체는 성공
                }

                SetStatus("Multiplayer Active — " + (nickname ?? "Unknown"));
            }
            catch (System.Exception exception)
            {
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
