using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PianoPrefabMaintenance
{
    const string InteractivePianoPrefabPath = "Assets/Prefabs/Interactive Piano.prefab";
    const string SamplePianoPrefabPath = "Assets/Prefabs/Sample Piano.prefab";
    const string LegacyNestedRootName = "Sample Piano";
    const string LocalModelRootName = "PianoModel";
    const string ValidationSessionKey = "VirtualMusicStudio.PianoValidation.Active";
    const string ValidationSummaryKey = "VirtualMusicStudio.PianoValidation.Summary";

    static PianoPrefabMaintenance()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Piano/Flatten Interactive Piano Prefab")]
    public static void FlattenInteractivePianoPrefab()
    {
        if (!File.Exists(InteractivePianoPrefabPath))
        {
            Debug.LogError($"Interactive piano prefab not found: {InteractivePianoPrefabPath}");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(InteractivePianoPrefabPath);
        try
        {
            Transform modelRoot = prefabRoot.transform.Find(LegacyNestedRootName);
            if (modelRoot == null)
            {
                modelRoot = prefabRoot.transform.Find(LocalModelRootName);
                if (modelRoot == null)
                {
                    Debug.LogWarning("Interactive Piano is already flattened or missing a model root.");
                }
            }
            else
            {
                if (PrefabUtility.IsPartOfPrefabInstance(modelRoot.gameObject))
                {
                    PrefabUtility.UnpackPrefabInstance(
                        modelRoot.gameObject,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }

                modelRoot.name = LocalModelRootName;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, InteractivePianoPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.Refresh();

        string[] dependencies = AssetDatabase.GetDependencies(InteractivePianoPrefabPath, true);
        foreach (string dependency in dependencies)
        {
            if (dependency == SamplePianoPrefabPath)
            {
                Debug.LogError("Interactive Piano still depends on Sample Piano prefab after flattening.");
                return;
            }
        }

        if (AssetDatabase.DeleteAsset(SamplePianoPrefabPath))
        {
            Debug.Log("Flattened Interactive Piano and deleted Sample Piano prefab.");
        }
        else if (File.Exists(SamplePianoPrefabPath))
        {
            Debug.LogError("Failed to delete Sample Piano prefab.");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Piano/Run Sequential Validation")]
    public static void RunSequentialValidation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Cannot start piano validation while play mode is active or changing.");
            return;
        }

        if (!PianoEditorUtility.TryGetPianoRoot(out GameObject pianoRoot))
        {
            Debug.LogError("No piano root found in the active scene.");
            return;
        }

        if (!pianoRoot.activeInHierarchy)
        {
            Debug.LogError("The active piano root must be enabled before validation.");
            return;
        }

        PianoValidationPaths.EnsureDirectories();
        if (File.Exists(PianoValidationPaths.ReportPath))
            File.Delete(PianoValidationPaths.ReportPath);

        SessionState.SetBool(ValidationSessionKey, true);
        SessionState.EraseString(ValidationSummaryKey);
        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ValidationSessionKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (!PianoEditorUtility.TryGetPianoRoot(out GameObject pianoRoot))
            {
                SessionState.SetString(ValidationSummaryKey, "Validation failed: piano root not found in play mode.");
                EditorApplication.isPlaying = false;
                return;
            }

            if (pianoRoot.GetComponent<PianoSequentialValidator>() == null)
                pianoRoot.AddComponent<PianoSequentialValidator>();

            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        string summary;
        if (!File.Exists(PianoValidationPaths.ReportPath))
        {
            summary = "Validation failed: report.json was not generated.";
        }
        else
        {
            PianoValidationReport report = JsonUtility.FromJson<PianoValidationReport>(File.ReadAllText(PianoValidationPaths.ReportPath));
            if (report == null)
            {
                summary = "Validation failed: report.json could not be parsed.";
            }
            else if (report.issueCount > 0)
            {
                summary = $"Validation found issues. issueCount={report.issueCount}, avgAngle={report.targetAngleAverage:F2}, maxOther={report.maxOtherAngleObserved:F2}, avgVisualChange={report.averageVisualChangeRatio:F6}";
            }
            else
            {
                summary = $"Validation passed. avgAngle={report.targetAngleAverage:F2}, maxOther={report.maxOtherAngleObserved:F2}, avgVisualChange={report.averageVisualChangeRatio:F6}";
            }
        }

        SessionState.SetBool(ValidationSessionKey, false);
        SessionState.SetString(ValidationSummaryKey, summary);
        Debug.Log(summary);
    }
}
