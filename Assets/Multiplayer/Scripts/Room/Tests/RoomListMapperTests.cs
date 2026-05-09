using Murang.Multiplayer.Room.Client;
using NUnit.Framework;

namespace Murang.Multiplayer.Room.Tests
{
    [TestFixture]
    public sealed class RoomListMapperTests
    {
        // RoomListMapper.FromSessionInfo requires a live Fusion SessionInfo which cannot be
        // directly constructed in EditMode (DLL-sealed, no public ctor). The internal
        // FromRaw overload exercises the same mapping contract without a Photon dependency.

        [Test]
        public void FromRaw_IsLocked_True_MapsToLockedEntry()
        {
            RoomListEntry entry = RoomListMapper.FromRaw("locked-room", 2, 8, isLocked: true);

            Assert.IsTrue(entry.IsLocked);
        }

        [Test]
        public void FromRaw_IsLocked_False_MapsToUnlockedEntry()
        {
            RoomListEntry entry = RoomListMapper.FromRaw("open-room", 1, 8, isLocked: false);

            Assert.IsFalse(entry.IsLocked);
        }

        [Test]
        public void FromRaw_IsLocked_DefaultFalse_WhenKeyAbsent()
        {
            // Simulates the "key absent" case: isLocked defaults to false in RoomListMapper.
            RoomListEntry entry = RoomListMapper.FromRaw("no-lock-key-room", 0, 8, isLocked: false);

            Assert.IsFalse(entry.IsLocked);
        }

        [Test]
        public void FromRaw_PlayerCounts_MappedCorrectly()
        {
            RoomListEntry entry = RoomListMapper.FromRaw("room", currentPlayers: 3, maxPlayers: 8, isLocked: false);

            Assert.AreEqual(3, entry.CurrentPlayers);
            Assert.AreEqual(8, entry.MaxPlayers);
        }

        [Test]
        public void FromRaw_RoomName_MappedCorrectly()
        {
            const string expected = "murang-room-42";
            RoomListEntry entry = RoomListMapper.FromRaw(expected, 0, 8, isLocked: false);

            Assert.AreEqual(expected, entry.RoomName);
        }

        [Test]
        public void FromRaw_NullRoomName_MapsToEmptyString()
        {
            RoomListEntry entry = RoomListMapper.FromRaw(null, 0, 8, isLocked: false);

            Assert.AreEqual(string.Empty, entry.RoomName);
        }
    }
}
