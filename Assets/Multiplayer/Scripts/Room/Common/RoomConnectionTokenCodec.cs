using System;
using System.Text;
using UnityEngine;

namespace Murang.Multiplayer.Room.Common
{
    internal static class RoomConnectionTokenCodec
    {
        [Serializable]
        private sealed class Payload
        {
            public string passwordHash;
        }

        public static byte[] Serialize(string passwordHash)
        {
            Payload payload = new Payload
            {
                passwordHash = RoomPasswordHasher.NormalizeHash(passwordHash)
            };

            string json = JsonUtility.ToJson(payload);
            return Encoding.UTF8.GetBytes(json);
        }

        public static bool TryDeserialize(byte[] token, out string passwordHash)
        {
            passwordHash = null;

            if (token == null || token.Length == 0)
            {
                return true;
            }

            try
            {
                string json = Encoding.UTF8.GetString(token);
                Payload payload = JsonUtility.FromJson<Payload>(json);
                passwordHash = RoomPasswordHasher.NormalizeHash(payload != null ? payload.passwordHash : null);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
