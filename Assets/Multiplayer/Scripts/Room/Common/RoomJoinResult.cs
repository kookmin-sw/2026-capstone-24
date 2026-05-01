using System;

namespace Murang.Multiplayer.Room.Common
{
    public readonly struct RoomJoinResult : IEquatable<RoomJoinResult>
    {
        public RoomJoinResult(bool success, RoomJoinFailureReason reason, string roomName, string message)
        {
            Success = success;
            Reason = reason;
            RoomName = roomName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }

        public RoomJoinFailureReason Reason { get; }

        public string RoomName { get; }

        public string Message { get; }

        public static RoomJoinResult CreateSuccess(string roomName)
        {
            return new RoomJoinResult(true, RoomJoinFailureReason.None, roomName, string.Empty);
        }

        public static RoomJoinResult CreateFailure(RoomJoinFailureReason reason, string roomName, string message)
        {
            return new RoomJoinResult(false, reason, roomName, message);
        }

        public bool Equals(RoomJoinResult other)
        {
            return Success == other.Success
                && Reason == other.Reason
                && string.Equals(RoomName, other.RoomName, StringComparison.Ordinal)
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RoomJoinResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Success, Reason, RoomName, Message);
        }
    }
}
