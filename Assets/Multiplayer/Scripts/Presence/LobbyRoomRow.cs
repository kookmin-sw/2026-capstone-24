using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// 방 목록 단일 행 뷰. <see cref="Bind"/> 로 <see cref="LobbyRoomRowData"/>를
    /// 수신해 라벨·아이콘을 갱신하고, JoinButton 클릭 시 <see cref="JoinRequested"/>
    /// 이벤트를 발화한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyRoomRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text playersLabel;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private Button joinButton;

        /// <summary>행 클릭(JoinButton)시 발화. 인자는 현재 바인딩된 rowData.</summary>
        public event Action<LobbyRoomRowData> JoinRequested;

        private LobbyRoomRowData _data;

        private void Awake()
        {
            if (joinButton != null)
            {
                joinButton.onClick.AddListener(OnJoinClicked);
            }
        }

        /// <summary>행에 데이터를 바인딩해 UI를 갱신한다.</summary>
        public void Bind(LobbyRoomRowData data)
        {
            _data = data;

            if (nameLabel != null)
            {
                nameLabel.text = data.RoomName;
            }
            if (playersLabel != null)
            {
                playersLabel.text = $"{data.CurrentPlayers}/{data.MaxPlayers}";
            }
            if (lockIcon != null)
            {
                lockIcon.SetActive(data.HasPassword);
            }
        }

        private void OnJoinClicked()
        {
            JoinRequested?.Invoke(_data);
        }
    }
}
