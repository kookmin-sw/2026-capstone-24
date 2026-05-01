namespace Murang.Multiplayer.Room.Common
{
    public readonly struct RoomCreateOptions
    {
        public RoomCreateOptions(string roomName, string password, int maxPlayers = 8)
        {
            RoomName = roomName;
            Password = password;
            MaxPlayers = maxPlayers;
        }

        public string RoomName { get; }

        public string Password { get; }

        public int MaxPlayers { get; }
    }
}
