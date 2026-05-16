using System;
using Murang.Multiplayer.Room.Client;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// 로비 룸 목록의 한 행. <see cref="MultiplayerLobbyPanel"/> 가 prefab 풀에서
    /// 활성화/비활성화 + <see cref="Bind"/> 호출로 데이터 갱신.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomRowEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text playersLabel;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private Button joinButton;

        public event Action<RoomListEntry> OnJoinRequested;

        private RoomListEntry _entry;

        void Awake()
        {
            if (joinButton != null)
            {
                joinButton.onClick.AddListener(OnJoinButtonClicked);
            }
        }

        void OnDestroy()
        {
            if (joinButton != null)
            {
                joinButton.onClick.RemoveListener(OnJoinButtonClicked);
            }
        }

        public void Bind(RoomListEntry entry)
        {
            _entry = entry;

            if (nameLabel != null)
            {
                nameLabel.text = entry.RoomName;
            }
            if (playersLabel != null)
            {
                playersLabel.text = $"{entry.CurrentPlayers}/{entry.MaxPlayers}";
            }
            if (lockIcon != null)
            {
                lockIcon.SetActive(entry.IsLocked);
            }
            if (joinButton != null)
            {
                joinButton.interactable = entry.CurrentPlayers < entry.MaxPlayers;
            }
        }

        public void SetInteractable(bool value)
        {
            if (joinButton != null)
            {
                joinButton.interactable = value && _entry.CurrentPlayers < _entry.MaxPlayers;
            }
        }

        private void OnJoinButtonClicked()
        {
            OnJoinRequested?.Invoke(_entry);
        }
    }
}
