using System;
using System.IO;
using UnityEngine;

[Serializable]
public class PianoValidationReport
{
    public string pianoName;
    public string generatedAtUtc;
    public float targetAngleAverage;
    public float maxOtherAngleObserved;
    public float averageVisualChangeRatio;
    public int issueCount;
    public bool requiresFixes;
    public float recommendedPressDistanceScale = 1f;
    public float recommendedNonVerticalSizeScale = 1f;
    public float recommendedReleaseSpeedScale = 1f;
    public PianoValidationKeyResult[] keys = Array.Empty<PianoValidationKeyResult>();
}

[Serializable]
public class PianoValidationKeyResult
{
    public int keyIndex;
    public string keyName;
    public float targetAngle;
    public float leftNeighborAngle;
    public float rightNeighborAngle;
    public float maxOtherAngle;
    public int maxOtherKeyIndex;
    public float visualChangeRatio;
    public float releaseResidualAngle;
    public bool targetUnderPressed;
    public bool hasNeighborCrosstalk;
    public bool hasReleaseLag;
    public string screenshotPath;
}

public static class PianoValidationPaths
{
    public static string RootDirectory
    {
        get
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, "Library", "PianoValidation");
        }
    }

    public static string ScreenshotsDirectory => Path.Combine(RootDirectory, "Screenshots");
    public static string ReportPath => Path.Combine(RootDirectory, "report.json");
    public static string BaselineScreenshotPath => Path.Combine(ScreenshotsDirectory, "baseline_rest.png");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ScreenshotsDirectory);
    }
}
