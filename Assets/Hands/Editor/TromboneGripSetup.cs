using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One-time setup tool that fills Trombone.prefab's LeftGripRoot/RightGripRoot with
// a full hand bone hierarchy copied from LeftPlayHand/RightPlayHand. Also adds a
// preview SkinnedMeshRenderer so the grip pose is visible in the prefab stage.
// Run "Tools/Trombone/Setup Grip Pose Hands".
static class TromboneGripSetup
{
    const string TrombonePrefabPath = "Assets/Instruments/Trombone/Prefabs/Trombone.prefab";
    const string LeftPlayHandPath   = "Assets/Hands/Prefabs/Play/LeftPlayHand.prefab";
    const string RightPlayHandPath  = "Assets/Hands/Prefabs/Play/RightPlayHand.prefab";

    [MenuItem("Tools/Trombone/Setup Grip Pose Hands")]
    static void SetupGripPoseHands()
    {
        var trombone = AssetDatabase.LoadAssetAtPath<GameObject>(TrombonePrefabPath);
        if (trombone == null)
        {
            Debug.LogError($"[TromboneGripSetup] Trombone prefab not found at {TrombonePrefabPath}");
            return;
        }

        using var scope = new PrefabUtility.EditPrefabContentsScope(TrombonePrefabPath);
        var root = scope.prefabContentsRoot;

        var leftGripRoot  = FindRecursive(root.transform, "LeftGripRoot");
        var leftWrist     = FindRecursive(root.transform, "L_Wrist");
        var rightGripRoot = FindRecursive(root.transform, "RightGripRoot");
        var rightWrist    = FindRecursive(root.transform, "R_Wrist");

        if (leftGripRoot == null || leftWrist == null || rightGripRoot == null || rightWrist == null)
        {
            Debug.LogError("[TromboneGripSetup] LeftGripRoot/L_Wrist or RightGripRoot/R_Wrist not found in Trombone.prefab.");
            return;
        }

        FillSide(leftGripRoot, leftWrist, LeftPlayHandPath, "L_Wrist");
        FillSide(rightGripRoot, rightWrist, RightPlayHandPath, "R_Wrist");

        AssetDatabase.SaveAssets();
        Debug.Log("[TromboneGripSetup] Done. LeftGripRoot/RightGripRoot now contain full hand bone hierarchy + preview mesh.");
    }

    static void FillSide(Transform gripRoot, Transform wrist, string playHandPath, string wristName)
    {
        var playHand = AssetDatabase.LoadAssetAtPath<GameObject>(playHandPath);
        if (playHand == null)
        {
            Debug.LogError($"[TromboneGripSetup] Play hand prefab not found: {playHandPath}");
            return;
        }

        var playWrist = FindRecursive(playHand.transform, wristName);
        if (playWrist == null)
        {
            Debug.LogError($"[TromboneGripSetup] '{wristName}' not found in {playHandPath}.");
            return;
        }

        ClearWristChildren(wrist);
        CopyWristChildren(playWrist, wrist);

        RemovePreviewMesh(gripRoot);
        AddPreviewMesh(gripRoot, wrist, playHand);

        if (!gripRoot.gameObject.TryGetComponent<GripPoseHandPreview>(out _))
            gripRoot.gameObject.AddComponent<GripPoseHandPreview>();

        Debug.Log($"[TromboneGripSetup] {gripRoot.name}: copied {CountChildren(wrist)} bone(s) under {wristName} + preview mesh.");
    }

    static void ClearWristChildren(Transform wrist)
    {
        for (int i = wrist.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(wrist.GetChild(i).gameObject);
    }

    static void RemovePreviewMesh(Transform gripRoot)
    {
        var existing = gripRoot.Find("PreviewMesh");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);
    }

    static void CopyWristChildren(Transform sourceWrist, Transform targetWrist)
    {
        for (int i = 0; i < sourceWrist.childCount; i++)
        {
            var child = sourceWrist.GetChild(i);
            if (child.GetComponent<SkinnedMeshRenderer>() != null) continue;
            if (child.name is "RightHand" or "LeftHand") continue;
            CopyBoneHierarchy(child, targetWrist);
        }
    }

    static Transform CopyBoneHierarchy(Transform source, Transform parent)
    {
        var go = new GameObject(source.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = source.localPosition;
        go.transform.localRotation = source.localRotation;
        go.transform.localScale    = source.localScale;

        for (int i = 0; i < source.childCount; i++)
        {
            var child = source.GetChild(i);
            if (child.GetComponent<SkinnedMeshRenderer>() != null) continue;
            if (child.name is "RightHand" or "LeftHand") continue;
            CopyBoneHierarchy(child, go.transform);
        }
        return go.transform;
    }

    // Copies the SkinnedMeshRenderer from playHandPrefab and rebinds its bones
    // to the cloned bone hierarchy under wrist. The copy sits at gripRoot/PreviewMesh
    // so PlayHandPoseDriver.BuildJointMap never sees it (it scans wrist subtree only).
    static void AddPreviewMesh(Transform gripRoot, Transform wrist, GameObject playHandPrefab)
    {
        var sourceSMR = playHandPrefab.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
        if (sourceSMR == null)
        {
            Debug.LogWarning("[TromboneGripSetup] No SkinnedMeshRenderer in play hand — preview mesh skipped.");
            return;
        }

        var boneMap = new Dictionary<string, Transform>(System.StringComparer.Ordinal);
        BuildBoneMap(wrist, boneMap);

        var previewContainer = new GameObject("PreviewMesh");
        previewContainer.transform.SetParent(gripRoot, false);
        previewContainer.tag = "EditorOnly";

        var previewGo = new GameObject("HandMeshPreview");
        previewGo.transform.SetParent(previewContainer.transform, false);

        var smr = previewGo.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh      = sourceSMR.sharedMesh;
        smr.sharedMaterials = sourceSMR.sharedMaterials;

        smr.rootBone = boneMap.TryGetValue(sourceSMR.rootBone != null ? sourceSMR.rootBone.name : "", out var rb)
            ? rb : wrist;

        var newBones = new Transform[sourceSMR.bones.Length];
        for (int i = 0; i < sourceSMR.bones.Length; i++)
        {
            var boneName = sourceSMR.bones[i] != null ? sourceSMR.bones[i].name : "";
            newBones[i] = boneMap.TryGetValue(boneName, out var bt) ? bt : null;
        }
        smr.bones = newBones;
    }

    static void BuildBoneMap(Transform root, Dictionary<string, Transform> map)
    {
        map[root.name] = root;
        for (int i = 0; i < root.childCount; i++)
            BuildBoneMap(root.GetChild(i), map);
    }

    static Transform FindRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    static int CountChildren(Transform root)
    {
        int count = 0;
        for (int i = 0; i < root.childCount; i++)
            count += 1 + CountChildren(root.GetChild(i));
        return count;
    }
}
