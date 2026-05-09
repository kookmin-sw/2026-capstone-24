using System;
using System.Collections.Generic;
using Fusion;
using Murang.Multiplayer.Room.Common;

namespace Murang.Multiplayer.Room.Client
{
    public static class RoomListMapper
    {
        public static RoomListEntry FromSessionInfo(SessionInfo sessionInfo)
        {
            bool isLocked = false;
            if (sessionInfo.Properties != null
                && sessionInfo.Properties.TryGetValue(RoomSessionPropertyKeys.IsLocked, out SessionProperty prop))
            {
                isLocked = ReadBoolFlag(prop);
            }

            return new RoomListEntry(
                sessionInfo.Name,
                sessionInfo.PlayerCount,
                sessionInfo.MaxPlayers,
                isLocked);
        }

        // Photon Cloud lobby는 bool SessionProperty를 lobby 콜백으로 propagate할 때
        // 내부적으로 int(0/1)로 coerce하는 것이 관찰되어, 매퍼는 두 형태를 모두 허용한다.
        private static bool ReadBoolFlag(SessionProperty prop)
        {
            try
            {
                return (bool)prop;
            }
            catch (InvalidCastException)
            {
            }

            try
            {
                return (int)prop != 0;
            }
            catch (InvalidCastException)
            {
            }

            return false;
        }

        internal static RoomListEntry FromRaw(
            string roomName,
            int currentPlayers,
            int maxPlayers,
            bool isLocked)
        {
            return new RoomListEntry(roomName, currentPlayers, maxPlayers, isLocked);
        }

        public static IReadOnlyList<RoomListEntry> FromSessionInfoList(IList<SessionInfo> sessionInfoList)
        {
            if (sessionInfoList == null || sessionInfoList.Count == 0)
            {
                return System.Array.Empty<RoomListEntry>();
            }

            RoomListEntry[] entries = new RoomListEntry[sessionInfoList.Count];
            for (int i = 0; i < sessionInfoList.Count; i++)
            {
                entries[i] = FromSessionInfo(sessionInfoList[i]);
            }

            return entries;
        }
    }
}
