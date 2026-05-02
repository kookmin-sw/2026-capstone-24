namespace Murang.Multiplayer.Room.Common
{
    public readonly struct RoomJoinOptions
    {
        public RoomJoinOptions(string playerId, string roomName, string password)
        {
            PlayerId = playerId;
            RoomName = roomName;
            Password = password;
        }

        public string PlayerId { get; }

        public string RoomName { get; }

        public string Password { get; }
    }
}
