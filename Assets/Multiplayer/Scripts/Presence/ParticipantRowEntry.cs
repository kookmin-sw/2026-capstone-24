using TMPro;
using UnityEngine;

namespace Murang.Multiplayer.Presence
{
    /// <summary>인-룸 참가자 리스트의 한 행. <see cref="MultiplayerInRoomPanel"/>
    /// 가 prefab 풀로 활성화/비활성화 + <see cref="Bind"/> 호출로 텍스트 갱신.</summary>
    [DisallowMultipleComponent]
    public sealed class ParticipantRowEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        public void Bind(string display)
        {
            if (label != null)
            {
                label.text = display ?? string.Empty;
            }
        }
    }
}
