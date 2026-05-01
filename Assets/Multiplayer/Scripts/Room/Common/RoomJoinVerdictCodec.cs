using System;
using System.Text;
using Fusion.Sockets;
using UnityEngine;

namespace Murang.Multiplayer.Room.Common
{
    internal static class RoomJoinVerdictCodec
    {
        public static readonly ReliableKey ReliableKey =
            ReliableKey.FromInts(
                unchecked((int)0x4D555241),
                unchecked((int)0x4E47524F),
                unchecked((int)0x4F4D4A4F),
                1);

        [Serializable]
        private sealed class Payload
        {
            public bool success;
            public int reason;
            public string roomName;
            public string message;
        }

        public static byte[] Serialize(RoomJoinResult result)
        {
            Payload payload = new Payload
            {
                success = result.Success,
                reason = (int)result.Reason,
                roomName = result.RoomName,
                message = result.Message
            };

            string json = JsonUtility.ToJson(payload);
            return Encoding.UTF8.GetBytes(json);
        }

        public static bool TryDeserialize(ReliableKey key, ArraySegment<byte> data, out RoomJoinResult result)
        {
            result = default;

            if (key != ReliableKey)
            {
                return false;
            }

            try
            {
                byte[] buffer = new byte[data.Count];
                Array.Copy(data.Array, data.Offset, buffer, 0, data.Count);
                string json = Encoding.UTF8.GetString(buffer);
                Payload payload = JsonUtility.FromJson<Payload>(json);
                if (payload == null)
                {
                    return false;
                }

                result = payload.success
                    ? RoomJoinResult.CreateSuccess(payload.roomName)
                    : RoomJoinResult.CreateFailure(
                        (RoomJoinFailureReason)payload.reason,
                        payload.roomName,
                        payload.message);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
