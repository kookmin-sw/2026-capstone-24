namespace Murang.Multiplayer.Room.Common
{
    public enum RoomJoinFailureReason
    {
        None = 0,
        RoomFull = 1,
        WrongPassword = 2,
        RoomNotFound = 3,
        ConnectionFailed = 4,
        Other = 5
    }
}
