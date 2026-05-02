using Murang.Multiplayer.Room.Common;
using NUnit.Framework;

namespace Murang.Multiplayer.Room.Tests
{
    [TestFixture]
    public sealed class RoomPasswordHasherTests
    {
        [Test]
        public void Hash_EmptyInput_ReturnsNull()
        {
            Assert.IsNull(RoomPasswordHasher.Hash(null));
            Assert.IsNull(RoomPasswordHasher.Hash(string.Empty));
            Assert.IsNull(RoomPasswordHasher.Hash("   "));
        }

        [Test]
        public void Hash_SamePlaintext_ReturnsSameHash()
        {
            string first = RoomPasswordHasher.Hash("murang-secret");
            string second = RoomPasswordHasher.Hash("murang-secret");

            Assert.AreEqual(first, second);
        }

        [Test]
        public void Hash_DifferentPlaintext_ReturnsDifferentHash()
        {
            string first = RoomPasswordHasher.Hash("murang-secret");
            string second = RoomPasswordHasher.Hash("murang-secret-2");

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void Matches_EmptyExpectedHash_TreatsRoomAsUnlocked()
        {
            Assert.IsTrue(RoomPasswordHasher.Matches(null, null));
            Assert.IsTrue(RoomPasswordHasher.Matches(string.Empty, RoomPasswordHasher.Hash("optional-secret")));
        }

        [Test]
        public void NormalizeHash_TrimmedWhitespace_ReturnsCanonicalValue()
        {
            string hash = RoomPasswordHasher.Hash("murang-secret");

            Assert.AreEqual(hash, RoomPasswordHasher.NormalizeHash("  " + hash + "  "));
        }
    }
}
