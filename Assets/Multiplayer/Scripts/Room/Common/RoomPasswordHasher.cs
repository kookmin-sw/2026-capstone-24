using System;
using System.Security.Cryptography;
using System.Text;

namespace Murang.Multiplayer.Room.Common
{
    public static class RoomPasswordHasher
    {
        public static string Hash(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            byte[] rawBytes = Encoding.UTF8.GetBytes(password.Trim());
            byte[] hashedBytes;
            using (SHA256 sha256 = SHA256.Create())
            {
                hashedBytes = sha256.ComputeHash(rawBytes);
            }

            return Convert.ToBase64String(hashedBytes);
        }

        public static bool Matches(string expectedHash, string providedHash)
        {
            string normalizedExpected = NormalizeHash(expectedHash);
            if (string.IsNullOrEmpty(normalizedExpected))
            {
                return true;
            }

            return string.Equals(normalizedExpected, NormalizeHash(providedHash), StringComparison.Ordinal);
        }

        public static string NormalizeHash(string hash)
        {
            return string.IsNullOrWhiteSpace(hash) ? null : hash.Trim();
        }
    }
}
