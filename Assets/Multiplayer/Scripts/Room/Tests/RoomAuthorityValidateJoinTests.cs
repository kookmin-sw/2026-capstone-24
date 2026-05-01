using Murang.Multiplayer.Room.Common;
using Murang.Multiplayer.Room.Server;
using NUnit.Framework;

namespace Murang.Multiplayer.Room.Tests
{
    [TestFixture]
    public sealed class RoomAuthorityValidateJoinTests
    {
        [Test]
        public void ValidateJoin_UnderCapacity_AllowsJoin()
        {
            RoomJoinResult result = RoomAuthority.ValidateJoin(
                connectedPlayers: 3,
                maxPlayers: 8,
                expectedPasswordHash: null,
                providedPasswordHash: null);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(RoomJoinFailureReason.None, result.Reason);
        }

        [Test]
        public void ValidateJoin_OverCapacity_ReturnsRoomFull()
        {
            RoomJoinResult result = RoomAuthority.ValidateJoin(
                connectedPlayers: 9,
                maxPlayers: 8,
                expectedPasswordHash: null,
                providedPasswordHash: null);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RoomJoinFailureReason.RoomFull, result.Reason);
        }

        [Test]
        public void ValidateJoin_MatchingPassword_AllowsJoin()
        {
            string hash = RoomPasswordHasher.Hash("murang-secret");
            RoomJoinResult result = RoomAuthority.ValidateJoin(
                connectedPlayers: 1,
                maxPlayers: 8,
                expectedPasswordHash: hash,
                providedPasswordHash: hash);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(RoomJoinFailureReason.None, result.Reason);
        }

        [Test]
        public void ValidateJoin_MismatchedPassword_ReturnsWrongPassword()
        {
            RoomJoinResult result = RoomAuthority.ValidateJoin(
                connectedPlayers: 1,
                maxPlayers: 8,
                expectedPasswordHash: RoomPasswordHasher.Hash("murang-secret"),
                providedPasswordHash: RoomPasswordHasher.Hash("other-secret"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RoomJoinFailureReason.WrongPassword, result.Reason);
        }

        [Test]
        public void ValidateJoin_UnlockedRoom_AllowsProvidedPassword()
        {
            RoomJoinResult result = RoomAuthority.ValidateJoin(
                connectedPlayers: 1,
                maxPlayers: 8,
                expectedPasswordHash: null,
                providedPasswordHash: RoomPasswordHasher.Hash("optional-secret"));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(RoomJoinFailureReason.None, result.Reason);
        }
    }
}
