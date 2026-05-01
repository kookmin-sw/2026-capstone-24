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
    }
}
