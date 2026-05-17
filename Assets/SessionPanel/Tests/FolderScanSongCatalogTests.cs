using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SessionPanel.Tests
{
    public class FolderScanSongCatalogTests
    {
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public void TwoSongs_PianoAndDrumKit_AreRegisteredWithCorrectInstruments()
        {
            WriteVmsong("alpha.vmsong", "alpha_001", "Alpha Title", "piano", channel: 1);
            WriteVmsong("beta.vmsong",  "beta_001",  "Beta Title",  "DrumKit", channel: 10);

            var entries = FolderScanSongCatalog.BuildEntriesFromFolder(_tempDir, "Songs");

            Assert.AreEqual(2, entries.Count);
            var alpha = entries.First(e => e.SongId == "alpha_001");
            var beta  = entries.First(e => e.SongId == "beta_001");
            Assert.IsTrue(alpha.SupportedInstrumentIds.Contains("piano"));
            Assert.IsTrue(beta.SupportedInstrumentIds.Contains("DrumKit"));
            Assert.AreEqual("Songs/alpha.vmsong", alpha.GetChartPath("Easy"));
            Assert.AreEqual("Songs/beta.vmsong",  beta.GetChartPath("Normal"));
        }

        [Test]
        public void ParseFailFile_IsSkippedWithoutException()
        {
            WriteVmsong("good.vmsong", "good_001", "Good Title", "piano", channel: 1);
            File.WriteAllText(Path.Combine(_tempDir, "bad.vmsong"), "this is not a valid vmsong file");

            var entries = FolderScanSongCatalog.BuildEntriesFromFolder(_tempDir, "Songs");

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("good_001", entries[0].SongId);
        }

        void WriteVmsong(string fileName, string songId, string title, string instrument, int channel)
        {
            string content =
                "[Meta]\n" +
                "title   = " + title + "\n" +
                "songid  = " + songId + "\n" +
                "\n" +
                "[Resolution]\n" +
                "ticksPerQuarter = 480\n" +
                "\n" +
                "[Tempo]\n" +
                "tick=0  bpm=100  beats=4  beatUnit=4\n" +
                "\n" +
                "[Channels]\n" +
                "channel=" + channel + "   instrument=" + instrument + "\n" +
                "\n" +
                "[Track:" + channel + "]\n" +
                "tick=0  note=60  len=200  vel=90\n";
            File.WriteAllText(Path.Combine(_tempDir, fileName), content);
        }
    }
}
