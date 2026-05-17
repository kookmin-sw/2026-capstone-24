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
            WriteVmsong("alpha-piano-easy.vmsong", "alpha_001", "Alpha Title", "piano", channel: 1);
            WriteVmsong("beta-DrumKit-easy.vmsong",  "beta_001",  "Beta Title",  "DrumKit", channel: 10);

            var entries = FolderScanSongCatalog.BuildEntriesFromFolder(_tempDir, "Songs");

            Assert.AreEqual(2, entries.Count);
            var alpha = entries.First(e => e.SongId == "alpha_001");
            var beta  = entries.First(e => e.SongId == "beta_001");
            Assert.IsTrue(alpha.SupportedInstrumentIds.Contains("piano"));
            Assert.IsTrue(beta.SupportedInstrumentIds.Contains("DrumKit"));
            Assert.AreEqual("Songs/alpha-piano-easy.vmsong", alpha.GetChartPath("piano", "easy"));
            Assert.AreEqual("Songs/beta-DrumKit-easy.vmsong",  beta.GetChartPath("DrumKit", "easy"));
        }

        [Test]
        public void ParseFailFile_IsSkippedWithoutException()
        {
            WriteVmsong("good-piano-easy.vmsong", "good_001", "Good Title", "piano", channel: 1);
            File.WriteAllText(Path.Combine(_tempDir, "bad-piano-easy.vmsong"), "this is not a valid vmsong file");

            var entries = FolderScanSongCatalog.BuildEntriesFromFolder(_tempDir, "Songs");

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("good_001", entries[0].SongId);
        }

        [Test]
        public void SameSongname_MultipleFiles_AreGroupedIntoOneEntry()
        {
            WriteVmsong("cool-song-piano-easy.vmsong",  "cs_001", "Cool Song", "piano", channel: 1);
            WriteVmsong("cool-song-piano-hard.vmsong",  "cs_001", "Cool Song", "piano", channel: 1);
            WriteVmsong("cool-song-drum-easy.vmsong",   "cs_001", "Cool Song", "drum",  channel: 2);

            var entries = FolderScanSongCatalog.BuildEntriesFromFolder(_tempDir, "Songs");

            Assert.AreEqual(1, entries.Count, "Three files with same songname must collapse to 1 entry");
            var entry = entries[0];

            Assert.IsTrue(entry.SupportedInstrumentIds.Contains("piano"));
            Assert.IsTrue(entry.SupportedInstrumentIds.Contains("drum"));

            var pianoDiffs = entry.GetDifficultiesFor("piano").Select(d => d.ToLowerInvariant()).ToList();
            Assert.IsTrue(pianoDiffs.Contains("easy"), "piano should have easy");
            Assert.IsTrue(pianoDiffs.Contains("hard"), "piano should have hard");

            var drumDiffs = entry.GetDifficultiesFor("drum").Select(d => d.ToLowerInvariant()).ToList();
            Assert.IsTrue(drumDiffs.Contains("easy"),  "drum should have easy");
            Assert.IsFalse(drumDiffs.Contains("hard"), "drum should NOT have hard");

            Assert.AreEqual("Songs/cool-song-piano-hard.vmsong", entry.GetChartPath("piano", "hard"));
            Assert.IsNull(entry.GetChartPath("drum", "hard"), "GetChartPath(drum, hard) must be null");
        }

        [Test]
        public void HyphenInSongname_IsPreservedAsSongname()
        {
            WriteVmsong("my-cool-song-piano-easy.vmsong", "mcs_001", "My Cool Song", "piano", channel: 1);

            var entries = FolderScanSongCatalog.BuildEntriesFromFolder(_tempDir, "Songs");

            Assert.AreEqual(1, entries.Count);
            var entry = entries[0];
            Assert.IsTrue(entry.SupportedInstrumentIds.Contains("piano"));
            var diffs = entry.GetDifficultiesFor("piano").Select(d => d.ToLowerInvariant()).ToList();
            Assert.AreEqual(1, diffs.Count);
            Assert.AreEqual("easy", diffs[0]);
            Assert.AreEqual("Songs/my-cool-song-piano-easy.vmsong", entry.GetChartPath("piano", "easy"));
        }

        [Test]
        public void MalformedFileName_LessThanTwoHyphens_IsSkipped()
        {
            string validContent =
                "[Meta]\n" +
                "title=x\n" +
                "songid=x\n" +
                "\n" +
                "[Resolution]\n" +
                "ticksPerQuarter=480\n" +
                "\n" +
                "[Tempo]\n" +
                "tick=0  bpm=100  beats=4  beatUnit=4\n" +
                "\n" +
                "[Channels]\n" +
                "channel=1   instrument=piano\n" +
                "\n" +
                "[Track:1]\n" +
                "tick=0  note=60  len=200  vel=90\n";
            File.WriteAllText(Path.Combine(_tempDir, "no_hyphens.vmsong"), validContent);
            File.WriteAllText(Path.Combine(_tempDir, "only-one.vmsong"), validContent);

            var entries = FolderScanSongCatalog.BuildEntriesFromFolder(_tempDir, "Songs");

            Assert.AreEqual(0, entries.Count, "Files with fewer than 2 hyphens must be skipped");
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
