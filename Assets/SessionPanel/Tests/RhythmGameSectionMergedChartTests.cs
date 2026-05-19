using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Instruments;
using RhythmGame.Data;

namespace SessionPanel
{
    public class RhythmGameSectionMergedChartTests
    {
        static FieldInfo GetField(string name)
            => typeof(RhythmGameSectionController)
               .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        static T GetFieldValue<T>(RhythmGameSectionController ctrl, string name)
            => (T)GetField(name).GetValue(ctrl);

        static void SetFieldValue(RhythmGameSectionController ctrl, string name, object value)
            => GetField(name).SetValue(ctrl, value);

        static VmSongChart InvokeBuildMergedChart(RhythmGameSectionController ctrl, float bpm)
        {
            var mi = typeof(RhythmGameSectionController)
                .GetMethod("BuildMergedChartForSession",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return (VmSongChart)mi.Invoke(ctrl, new object[] { bpm });
        }

        static Dictionary<int, bool> InvokeBuildAccompanimentDictFromChart(
            RhythmGameSectionController ctrl, int judgedChannel, VmSongChart sourceChart)
        {
            var mi = typeof(RhythmGameSectionController)
                .GetMethod("BuildAccompanimentDictFromChart",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return (Dictionary<int, bool>)mi.Invoke(ctrl, new object[] { judgedChannel, sourceChart });
        }

        static RhythmGameSectionController BuildCtrl(VmSongChart playerChart, Dictionary<int, VmSongChart> otherCharts)
        {
            var go = new GameObject("TestCtrl");
            go.SetActive(false);
            var ctrl = go.AddComponent<RhythmGameSectionController>();
            SetFieldValue(ctrl, "_loadedChart", playerChart);
            SetFieldValue(ctrl, "_otherInstrumentCharts", otherCharts);
            return ctrl;
        }

        static VmSongChart MakeChart(int channel, string instrumentKey, float bpm = 120f)
        {
            var chart = new VmSongChart();
            var seg = new TempoSegment();
            seg.bpm = bpm;
            chart.tempoMap.segments.Add(seg);
            chart.channelMap.entries.Add(new ChannelInstrumentMap.Entry
                { channel = channel, instrumentKey = instrumentKey });
            var track = new ChartTrack();
            track.channel = channel;
            var note = new ChartNote();
            note.tick = 0;
            track.notes.Add(note);
            chart.tracks.Add(track);
            return chart;
        }

        [Test]
        public void BuildMergedChartForSession_ChannelMapMerged_NoDuplicates()
        {
            var playerChart = MakeChart(1, "piano", 120f);
            var drumChart   = MakeChart(2, "drum",  120f);
            var otherCharts = new Dictionary<int, VmSongChart> { [2] = drumChart };
            var ctrl = BuildCtrl(playerChart, otherCharts);
            try
            {
                var merged = InvokeBuildMergedChart(ctrl, 80f);
                Assert.IsNotNull(merged);
                Assert.AreEqual(80f, merged.tempoMap.segments[0].bpm, 0.001f);
                Assert.AreEqual(2, merged.channelMap.entries.Count,
                    "merged channelMap should have piano(1) + drum(2)");
                var channels = new HashSet<int>();
                foreach (var e in merged.channelMap.entries) channels.Add(e.channel);
                Assert.IsTrue(channels.Contains(1));
                Assert.IsTrue(channels.Contains(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(ctrl.gameObject); }
        }

        [Test]
        public void BuildMergedChartForSession_TracksMerged_AllNotesPresent()
        {
            var playerChart = MakeChart(1, "piano", 120f);
            var drumChart   = MakeChart(2, "drum",  120f);
            var otherCharts = new Dictionary<int, VmSongChart> { [2] = drumChart };
            var ctrl = BuildCtrl(playerChart, otherCharts);
            try
            {
                var merged = InvokeBuildMergedChart(ctrl, 80f);
                Assert.AreEqual(2, merged.tracks.Count);
                int totalNotes = 0;
                foreach (var t in merged.tracks) totalNotes += t.notes.Count;
                Assert.AreEqual(2, totalNotes);
            }
            finally { UnityEngine.Object.DestroyImmediate(ctrl.gameObject); }
        }

        [Test]
        public void BuildAccompanimentDictFromChart_OffToggle_PropagatesToMergedChart()
        {
            var playerChart = MakeChart(1, "piano", 120f);
            var drumChart   = MakeChart(2, "drum",  120f);
            var otherCharts = new Dictionary<int, VmSongChart> { [2] = drumChart };
            var ctrl = BuildCtrl(playerChart, otherCharts);
            try
            {
                var merged = InvokeBuildMergedChart(ctrl, 80f);
                var states = GetFieldValue<Dictionary<string, bool>>(ctrl, "_instrumentToggleStates");
                states["drum"] = false;
                var dict = InvokeBuildAccompanimentDictFromChart(ctrl, 1, merged);
                Assert.IsFalse(dict.ContainsKey(1), "judged channel 1 must NOT be in dict");
                Assert.IsTrue(dict.ContainsKey(2),  "drum channel 2 must be in dict");
                Assert.IsFalse(dict[2], "drum must be false (OFF)");
            }
            finally { UnityEngine.Object.DestroyImmediate(ctrl.gameObject); }
        }

        [Test]
        public void BuildMergedChartForSession_DoesNotMutatePlayerChart()
        {
            var playerChart = MakeChart(1, "piano", 100f);
            var otherCharts = new Dictionary<int, VmSongChart>();
            var ctrl = BuildCtrl(playerChart, otherCharts);
            try
            {
                Assert.AreEqual(100f, playerChart.tempoMap.segments[0].bpm, 0.001f);
                InvokeBuildMergedChart(ctrl, 80f);
                Assert.AreEqual(100f, playerChart.tempoMap.segments[0].bpm, 0.001f,
                    "player chart bpm must NOT be mutated");
            }
            finally { UnityEngine.Object.DestroyImmediate(ctrl.gameObject); }
        }
    }
}
