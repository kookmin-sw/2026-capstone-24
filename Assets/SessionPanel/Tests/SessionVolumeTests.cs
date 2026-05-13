using NUnit.Framework;
using UnityEngine;

namespace SessionPanel
{
    [TestFixture]
    public class SessionVolumeTests
    {
        const string MasterKey = "SessionPanel.Volume.Master";
        const string InstanceKeyPrefix = "SessionPanel.Volume.";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(MasterKey);
            PlayerPrefs.DeleteKey(InstanceKeyPrefix + "test-piano");
            PlayerPrefs.DeleteKey(InstanceKeyPrefix + "test-drum");
        }

        [Test]
        public void Master_SetAndGet_RoundTrips()
        {
            SessionVolume.Master = 0.7f;

            Assert.That(SessionVolume.Master, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(PlayerPrefs.GetFloat(MasterKey, -1f), Is.EqualTo(0.7f).Within(1e-4f));
        }

        [Test]
        public void Master_Setter_ClampsToZeroOne()
        {
            SessionVolume.Master = 1.5f;
            Assert.That(SessionVolume.Master, Is.EqualTo(1f).Within(1e-4f));

            SessionVolume.Master = -0.3f;
            Assert.That(SessionVolume.Master, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Instance_PersistAndLoad_RoundTrips()
        {
            SessionVolume.PersistInstance("test-piano", 0.3f);

            Assert.That(SessionVolume.LoadInstance("test-piano", 0.5f), Is.EqualTo(0.3f).Within(1e-4f));
        }

        [Test]
        public void Instance_LoadUnknownId_ReturnsFallback()
        {
            Assert.That(SessionVolume.LoadInstance("test-drum", 0.42f), Is.EqualTo(0.42f).Within(1e-4f));
        }

        [Test]
        public void Instance_PersistEmptyId_NoOp()
        {
            SessionVolume.PersistInstance("", 0.9f);

            Assert.That(SessionVolume.LoadInstance("", 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
        }
    }
}
