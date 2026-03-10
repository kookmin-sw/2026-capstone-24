using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PianoPhysicsSetup
{
    const string LegacyPhysicsKeysRootName = "PhysicsKeys";
    const string InteractionRootName = "PianoInteraction";
    const string SensorRootName = "KeySensors";
    const int KeyCount = 88;
    const float SensorTopPaddingWorld = 0.01f;
    const float SensorSideMarginWorld = 0.001f;

    [MenuItem("Tools/Piano/Setup Key Sensors")]
    static void SetupKeySensors()
    {
        if (!TryCollectKeys(out GameObject pianoRoot, out Transform keyRigRoot, out List<Transform> keyBones, out List<BoxCollider> sourceColliders))
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Piano Key Sensors");
        int undoGroup = Undo.GetCurrentGroup();

        DestroyIfExists(LegacyPhysicsKeysRootName);
        RemoveDetachedInteractionRoot(pianoRoot.transform);

        GameObject interactionRoot = FindOrCreateChildRoot(pianoRoot.transform, InteractionRootName);
        interactionRoot.transform.localPosition = Vector3.zero;
        interactionRoot.transform.localRotation = Quaternion.identity;
        interactionRoot.transform.localScale = Vector3.one;

        Transform sensorRoot = interactionRoot.transform.Find(SensorRootName);
        if (sensorRoot != null)
            Undo.DestroyObjectImmediate(sensorRoot.gameObject);

        GameObject newSensorRoot = new GameObject(SensorRootName);
        Undo.RegisterCreatedObjectUndo(newSensorRoot, "Create KeySensors");
        newSensorRoot.transform.SetParent(interactionRoot.transform, false);

        for (int i = 0; i < KeyCount; i++)
        {
            Transform keyBone = keyBones[i];
            BoxCollider sourceCollider = sourceColliders[i];
            Rigidbody keyBody = EnsureKeyRigidbody(keyBone.gameObject);

            GameObject sensor = new GameObject($"KeySensor_{i + 1:000}");
            Undo.RegisterCreatedObjectUndo(sensor, "Create KeySensor");
            sensor.transform.SetParent(newSensorRoot.transform, false);
            sensor.transform.SetPositionAndRotation(keyBone.position, keyBone.rotation);
            sensor.transform.localScale = Vector3.one;

            BoxCollider sensorCollider = Undo.AddComponent<BoxCollider>(sensor);
            CopyCollider(sourceCollider, sensor.transform, sensorCollider);
            sensorCollider.isTrigger = true;
            ApplySensorMargins(sensorCollider, sensor.transform);

            PianoKeySensor keySensor = Undo.AddComponent<PianoKeySensor>(sensor);
            SetSerializedField(keySensor, "targetBone", keyBone);
            SetSerializedField(keySensor, "targetBody", keyBody);
            SetSerializedField(keySensor, "sensorCollider", sensorCollider);
            SetSerializedField(keySensor, "ignoredRoot", interactionRoot.transform);
            SetSerializedField(keySensor, "boneLocalAxis", Vector3.right);
            SetSerializedField(keySensor, "maxPressDegrees", 6f);
            SetSerializedField(keySensor, "pressDistance", 0.01f);
            SetSerializedField(keySensor, "pressSpeed", 18f);
            SetSerializedField(keySensor, "releaseSpeed", 30f);
            SetSerializedField(keySensor, "presserLayers", (LayerMask)(1 << 0));

            Undo.RecordObject(sourceCollider, "Enable Source Collider");
            sourceCollider.enabled = true;
            sourceCollider.isTrigger = false;
            EditorUtility.SetDirty(sourceCollider);
        }

        EditorSceneManager.MarkSceneDirty(pianoRoot.scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Created {KeyCount} key sensors under '{pianoRoot.name}/{InteractionRootName}'.");
    }

    [MenuItem("Tools/Piano/Setup Physics Proxies")]
    static void SetupKeySensorsAlias()
    {
        SetupKeySensors();
    }

    [MenuItem("Tools/Piano/Clear Legacy Physics Keys")]
    static void ClearLegacyPhysicsKeys()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Clear Legacy Piano Physics");
        int undoGroup = Undo.GetCurrentGroup();

        DestroyIfExists(LegacyPhysicsKeysRootName);
        if (PianoEditorUtility.TryGetPianoRoot(out GameObject pianoRoot))
        {
            Transform interactionRoot = pianoRoot.transform.Find(InteractionRootName);
            Transform sensorRoot = interactionRoot != null ? interactionRoot.Find(SensorRootName) : null;
            if (sensorRoot != null)
                Undo.DestroyObjectImmediate(sensorRoot.gameObject);
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    static bool TryCollectKeys(out GameObject pianoRoot, out Transform root, out List<Transform> keyBones, out List<BoxCollider> sourceColliders)
    {
        pianoRoot = null;
        root = null;
        keyBones = new List<Transform>(KeyCount);
        sourceColliders = new List<BoxCollider>(KeyCount);

        if (!PianoEditorUtility.TryGetPianoRoot(out pianoRoot))
        {
            Debug.LogError("No piano root found. Select a piano root or place one in the scene.");
            return false;
        }

        if (!PianoEditorUtility.TryGetKeyRigRoot(pianoRoot, out root))
        {
            Debug.LogError("Could not resolve Piano_Rig/Root on the selected piano.");
            return false;
        }

        List<string> missingBones = new List<string>();
        List<string> missingColliders = new List<string>();

        for (int i = 1; i <= KeyCount; i++)
        {
            string boneName = $"key_{i}";
            Transform keyBone = root.Find(boneName);
            if (keyBone == null)
            {
                missingBones.Add(boneName);
                continue;
            }

            BoxCollider sourceCollider = keyBone.GetComponent<BoxCollider>();
            if (sourceCollider == null)
            {
                missingColliders.Add(boneName);
                continue;
            }

            keyBones.Add(keyBone);
            sourceColliders.Add(sourceCollider);
        }

        if (missingBones.Count > 0 || missingColliders.Count > 0)
        {
            if (missingBones.Count > 0)
                Debug.LogError($"Missing piano key bones: {string.Join(", ", missingBones)}");

            if (missingColliders.Count > 0)
                Debug.LogError($"Missing source BoxColliders on: {string.Join(", ", missingColliders)}");

            return false;
        }

        return true;
    }

    static void DestroyIfExists(string rootName)
    {
        GameObject existing = FindSceneRoot(rootName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);
    }

    static void RemoveDetachedInteractionRoot(Transform pianoRoot)
    {
        GameObject detached = FindSceneRoot(InteractionRootName);
        if (detached == null)
            return;

        if (detached.transform.parent == pianoRoot)
            return;

        Undo.DestroyObjectImmediate(detached);
    }

    static GameObject FindSceneRoot(string name)
    {
        foreach (GameObject rootObject in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObject.name == name)
                return rootObject;
        }

        return null;
    }

    static GameObject FindOrCreateChildRoot(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
        created.transform.SetParent(parent, false);
        return created;
    }

    static Rigidbody EnsureKeyRigidbody(GameObject keyObject)
    {
        Rigidbody body = keyObject.GetComponent<Rigidbody>();
        if (body == null)
            body = Undo.AddComponent<Rigidbody>(keyObject);

        Undo.RecordObject(body, "Configure Key Rigidbody");
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezePosition;
        EditorUtility.SetDirty(body);
        return body;
    }

    static void CopyCollider(BoxCollider source, Transform targetTransform, BoxCollider target)
    {
        Vector3[] localCorners = GetLocalCorners(source.center, source.size * 0.5f);
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < localCorners.Length; i++)
        {
            Vector3 worldCorner = source.transform.TransformPoint(localCorners[i]);
            Vector3 targetLocalCorner = targetTransform.InverseTransformPoint(worldCorner);
            min = Vector3.Min(min, targetLocalCorner);
            max = Vector3.Max(max, targetLocalCorner);
        }

        target.center = (min + max) * 0.5f;
        target.size = max - min;
    }

    static void ApplySensorMargins(BoxCollider sensorCollider, Transform sensorTransform)
    {
        Vector3 size = sensorCollider.size;
        Vector3 center = sensorCollider.center;

        int verticalAxis = GetDominantVerticalAxis(sensorTransform, out float verticalSign);
        float[] axisScale = { sensorTransform.lossyScale.x, sensorTransform.lossyScale.y, sensorTransform.lossyScale.z };
        axisScale[0] = Mathf.Max(axisScale[0], 0.0001f);
        axisScale[1] = Mathf.Max(axisScale[1], 0.0001f);
        axisScale[2] = Mathf.Max(axisScale[2], 0.0001f);

        float topPaddingLocal = SensorTopPaddingWorld / axisScale[verticalAxis];
        AddAxis(ref size, verticalAxis, topPaddingLocal);
        AddAxis(ref center, verticalAxis, topPaddingLocal * 0.5f * verticalSign);

        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == verticalAxis)
                continue;

            float sideMarginLocal = (SensorSideMarginWorld * 2f) / axisScale[axis];
            float shrunkSize = GetAxis(size, axis) - sideMarginLocal;
            SetAxis(ref size, axis, Mathf.Max(0.0005f, shrunkSize));
        }

        sensorCollider.center = center;
        sensorCollider.size = size;
    }

    static int GetDominantVerticalAxis(Transform transform, out float sign)
    {
        float xDot = Vector3.Dot(transform.right, Vector3.up);
        float yDot = Vector3.Dot(transform.up, Vector3.up);
        float zDot = Vector3.Dot(transform.forward, Vector3.up);

        float absX = Mathf.Abs(xDot);
        float absY = Mathf.Abs(yDot);
        float absZ = Mathf.Abs(zDot);

        if (absX > absY && absX > absZ)
        {
            sign = Mathf.Sign(xDot);
            return 0;
        }

        if (absY > absZ)
        {
            sign = Mathf.Sign(yDot);
            return 1;
        }

        sign = Mathf.Sign(zDot);
        return 2;
    }

    static Vector3[] GetLocalCorners(Vector3 center, Vector3 extents)
    {
        return new[]
        {
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y, extents.z),
            center + new Vector3(-extents.x, extents.y, -extents.z),
            center + new Vector3(-extents.x, extents.y, extents.z),
            center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, extents.z),
            center + new Vector3(extents.x, extents.y, -extents.z),
            center + new Vector3(extents.x, extents.y, extents.z),
        };
    }

    static void SetSerializedField(Object target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedField(Object target, string fieldName, Vector3 value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        property.vector3Value = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedField(Object target, string fieldName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedField(Object target, string fieldName, LayerMask value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        property.intValue = value.value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddAxis(ref Vector3 vector, int axis, float value)
    {
        SetAxis(ref vector, axis, GetAxis(vector, axis) + value);
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
