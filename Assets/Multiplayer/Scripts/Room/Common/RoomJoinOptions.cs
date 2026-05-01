namespace Murang.Multiplayer.Room.Common
{
    public readonly struct RoomJoinOptions
    {
        public RoomJoinOptions(string roomName, string password)
        {
            RoomName = roomName;
            Password = password;
        }

        public string RoomName { get; }

        public string Password { get; }
    }
}
