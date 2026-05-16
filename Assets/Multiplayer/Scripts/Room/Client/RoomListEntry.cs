using System;

namespace Murang.Multiplayer.Room.Client
{
    public readonly struct RoomListEntry : IEquatable<RoomListEntry>
    {
        public RoomListEntry(string roomName, int currentPlayers, int maxPlayers, bool isLocked)
        {
            RoomName = roomName ?? string.Empty;
            CurrentPlayers = currentPlayers;
            MaxPlayers = maxPlayers;
            IsLocked = isLocked;
        }

        public string RoomName { get; }

        public int CurrentPlayers { get; }

        public int MaxPlayers { get; }

        public bool IsLocked { get; }

        public bool Equals(RoomListEntry other)
        {
            return string.Equals(RoomName, other.RoomName, StringComparison.Ordinal)
                && CurrentPlayers == other.CurrentPlayers
                && MaxPlayers == other.MaxPlayers
                && IsLocked == other.IsLocked;
        }

        public override bool Equals(object obj)
        {
            return obj is RoomListEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RoomName, CurrentPlayers, MaxPlayers, IsLocked);
        }

        public override string ToString()
        {
            return $"RoomListEntry(name={RoomName}, players={CurrentPlayers}/{MaxPlayers}, locked={IsLocked})";
        }
    }
}
