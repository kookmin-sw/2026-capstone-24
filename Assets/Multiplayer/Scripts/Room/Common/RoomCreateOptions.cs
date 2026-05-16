namespace Murang.Multiplayer.Room.Common
{
    public readonly struct RoomCreateOptions
    {
        public RoomCreateOptions(string playerId, string roomName, string password, int maxPlayers = 8)
        {
            PlayerId = playerId;
            RoomName = roomName;
            Password = password;
            MaxPlayers = maxPlayers;
        }

        public string PlayerId { get; }

        public string RoomName { get; }

        public string Password { get; }

        public int MaxPlayers { get; }
    }
}
