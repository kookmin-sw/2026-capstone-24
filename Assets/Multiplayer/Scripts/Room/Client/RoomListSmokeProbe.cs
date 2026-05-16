using System.Collections.Generic;
using UnityEngine;

namespace Murang.Multiplayer.Room.Client
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RoomListQuery))]
    public sealed class RoomListSmokeProbe : MonoBehaviour
    {
        [SerializeField] private RoomListQuery query;

        private void Reset()
        {
            query = GetComponent<RoomListQuery>();
        }

        private void OnEnable()
        {
            if (query == null)
            {
                query = GetComponent<RoomListQuery>();
            }

            if (query != null)
            {
                query.OnRoomListUpdated += LogList;
            }
        }

        private void OnDisable()
        {
            if (query != null)
            {
                query.OnRoomListUpdated -= LogList;
            }
        }

        private void LogList(IReadOnlyList<RoomListEntry> list)
        {
            Debug.Log($"[RoomListSmokeProbe] OnRoomListUpdated count={list.Count}");
            for (int i = 0; i < list.Count; i++)
            {
                Debug.Log($"[RoomListSmokeProbe]   [{i}] {list[i]}");
            }
        }
    }
}
