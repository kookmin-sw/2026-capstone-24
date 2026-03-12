using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PianoValidationFixer
{
    [MenuItem("Tools/Piano/Apply Validation Fixes")]
    static void ApplyValidationFixes()
    {
        if (!File.Exists(PianoValidationPaths.ReportPath))
        {
            Debug.LogError($"Validation report not found: {PianoValidationPaths.ReportPath}");
            return;
        }

        if (!PianoEditorUtility.TryGetPianoRoot(out GameObject pianoRoot))
        {
            Debug.LogError("No piano root found. Select the piano root before applying validation fixes.");
            return;
        }

        PianoValidationReport report = JsonUtility.FromJson<PianoValidationReport>(File.ReadAllText(PianoValidationPaths.ReportPath));
        if (report == null)
        {
            Debug.LogError("Could not parse piano validation report.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Apply Piano Validation Fixes");
        int undoGroup = Undo.GetCurrentGroup();

        PianoKeySensor[] sensors = pianoRoot.GetComponentsInChildren<PianoKeySensor>(true);
        foreach (PianoKeySensor sensor in sensors)
        {
            SerializedObject serializedObject = new SerializedObject(sensor);

            if (report.recommendedPressDistanceScale < 0.999f)
            {
                SerializedProperty pressDistance = serializedObject.FindProperty("pressDistance");
                pressDistance.floatValue = Mathf.Max(0.004f, pressDistance.floatValue * report.recommendedPressDistanceScale);
            }

            if (report.recommendedReleaseSpeedScale > 1.001f)
            {
                SerializedProperty releaseSpeed = serializedObject.FindProperty("releaseSpeed");
                releaseSpeed.floatValue = Mathf.Min(80f, releaseSpeed.floatValue * report.recommendedReleaseSpeedScale);
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (report.recommendedNonVerticalSizeScale < 0.999f && sensor.SensorCollider != null)
            {
                Undo.RecordObject(sensor.SensorCollider, "Shrink Piano Sensor");
                ShrinkNonVerticalAxes(sensor.SensorCollider, sensor.transform, report.recommendedNonVerticalSizeScale);
                EditorUtility.SetDirty(sensor.SensorCollider);
            }

            EditorUtility.SetDirty(sensor);
        }

        if (PrefabUtility.IsPartOfPrefabInstance(pianoRoot))
            PrefabUtility.ApplyPrefabInstance(pianoRoot, InteractionMode.AutomatedAction);

        EditorSceneManager.MarkSceneDirty(pianoRoot.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"Applied validation fixes to '{pianoRoot.name}'. pressDistanceScale={report.recommendedPressDistanceScale:F3}, sizeScale={report.recommendedNonVerticalSizeScale:F3}, releaseScale={report.recommendedReleaseSpeedScale:F3}");
    }

    static void ShrinkNonVerticalAxes(BoxCollider sensorCollider, Transform sensorTransform, float scale)
    {
        Vector3 size = sensorCollider.size;
        int verticalAxis = GetDominantVerticalAxis(sensorTransform);

        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == verticalAxis)
                continue;

            float value = GetAxis(size, axis);
            SetAxis(ref size, axis, Mathf.Max(0.0005f, value * scale));
        }

        sensorCollider.size = size;
    }

    static int GetDominantVerticalAxis(Transform transform)
    {
        float xDot = Mathf.Abs(Vector3.Dot(transform.right, Vector3.up));
        float yDot = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up));
        float zDot = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up));

        if (xDot > yDot && xDot > zDot)
            return 0;

        return yDot > zDot ? 1 : 2;
    }

    static float GetAxis(Vector3 vector, int axis)
    {
        return axis == 0 ? vector.x : axis == 1 ? vector.y : vector.z;
    }

    static void SetAxis(ref Vector3 vector, int axis, float value)
    {
        if (axis == 0)
            vector.x = value;
        else if (axis == 1)
            vector.y = value;
        else
            vector.z = value;
    }
}
