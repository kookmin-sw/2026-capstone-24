using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// One-time setup tool that fully configures drum_stick prefabs and Ghost Hand palm offsets.
// Run "Tools/DrumStick/Run All Steps" or run each step individually.
static class DrumStickSetup
{
    const string BasePrefabPath   = "Assets/Instruments/Drum/Prefabs/drum_stick.prefab";
    const string RightVariantPath = "Assets/Instruments/Drum/Prefabs/drum_stick_R.prefab";
    const string LeftVariantPath  = "Assets/Instruments/Drum/Prefabs/drum_stick_L.prefab";
    const string RightPlayHand    = "Assets/Hands/Prefabs/Play/RightPlayHand.prefab";
    const string LeftPlayHand     = "Assets/Hands/Prefabs/Play/LeftPlayHand.prefab";
    const string GhostRight       = "Assets/Hands/Prefabs/Ghost/RightControllerGhostHand.prefab";
    const string GhostLeft        = "Assets/Hands/Prefabs/Ghost/LeftControllerGhostHand.prefab";

    // -----------------------------------------------------------------------

    [MenuItem("Tools/DrumStick/Run All Steps")]
    static void RunAll()
    {
        SetupBasePrefab();
        CreateRightVariant();
        CreateLeftVariant();
        AdjustGhostHandPalmOffsets();
        PlaceInstancesInScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[DrumStickSetup] All setup steps complete.");
    }

    // -----------------------------------------------------------------------

    [MenuItem("Tools/DrumStick/1. Setup Base Prefab")]
    static void SetupBasePrefab()
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(BasePrefabPath);
        var root = scope.prefabContentsRoot;

        // Rigidbody
        if (!root.TryGetComponent<Rigidbody>(out var rb))
            rb = root.AddComponent<Rigidbody>();
        var rbSO = new SerializedObject(rb);
        rbSO.FindProperty("m_Mass").floatValue              = 0.15f;
        rbSO.FindProperty("m_UseGravity").boolValue         = true;
        rbSO.FindProperty("m_Interpolate").intValue         = 1;    // Interpolate
        rbSO.FindProperty("m_CollisionDetection").intValue  = 3;    // ContinuousDynamic
        var linDamp = rbSO.FindProperty("m_LinearDamping");
        if (linDamp != null) linDamp.floatValue = 0f;
        else rbSO.FindProperty("m_Drag")?.SetFloatSafe(0f);
        var angDamp = rbSO.FindProperty("m_AngularDamping");
        if (angDamp != null) angDamp.floatValue = 0.05f;
        else rbSO.FindProperty("m_AngularDrag")?.SetFloatSafe(0.05f);
        rbSO.ApplyModifiedPropertiesWithoutUndo();

        // CapsuleCollider — size from mesh bounds so the collider fits the FBX.
        // User fine-tunes in U1 after seeing it in-headset.
        if (!root.TryGetComponent<CapsuleCollider>(out var cap))
            cap = root.AddComponent<CapsuleCollider>();
        var capSO = new SerializedObject(cap);
        capSO.FindProperty("m_Direction").intValue = 1; // Y
        var mf = root.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var b = mf.sharedMesh.bounds;
            capSO.FindProperty("m_Height").floatValue = b.size.y;
            capSO.FindProperty("m_Radius").floatValue = Mathf.Max(b.size.x, b.size.z) * 0.35f;
            capSO.FindProperty("m_Center").vector3Value = b.center;
        }
        else
        {
            capSO.FindProperty("m_Height").floatValue = 0.014f;
            capSO.FindProperty("m_Radius").floatValue = 0.001f;
        }
        capSO.ApplyModifiedPropertiesWithoutUndo();

        // XRGrabInteractable — common settings shared by L and R variants.
        if (!root.TryGetComponent<XRGrabInteractable>(out var grab))
            grab = root.AddComponent<XRGrabInteractable>();
        var grabSO = new SerializedObject(grab);
        SetIfExists(grabSO, "m_MovementType",            2);     // Instantaneous
        SetIfExists(grabSO, "m_MatchAttachPosition",     true);
        SetIfExists(grabSO, "m_MatchAttachRotation",     true);
        SetIfExists(grabSO, "m_UseDynamicAttach",        false);
        SetIfExists(grabSO, "m_SnapToColliderVolume",    true);
        SetIfExists(grabSO, "m_AttachEaseInTime",        0.15f);
        SetIfExists(grabSO, "m_ThrowOnDetach",           true);
        SetIfExists(grabSO, "m_ThrowVelocityScale",      1.5f);
        SetIfExists(grabSO, "m_ThrowAngularVelocityScale", 1f);
        SetIfExists(grabSO, "m_TrackPosition",           true);
        SetIfExists(grabSO, "m_TrackRotation",           true);
        // attachTransform is null on base; set per-variant below.
        grabSO.ApplyModifiedPropertiesWithoutUndo();

        // GripPoseProvider
        if (!root.TryGetComponent<GripPoseProvider>(out _))
            root.AddComponent<GripPoseProvider>();

        Debug.Log("[DrumStickSetup] Step 1 done: base prefab has Rigidbody/Collider/XRGrabInteractable/GripPoseProvider.");
    }

    // -----------------------------------------------------------------------

    [MenuItem("Tools/DrumStick/2. Create R Variant")]
    static void CreateRightVariant() =>
        CreateVariant(RightVariantPath, "R", RightPlayHand, "R_Wrist");

    [MenuItem("Tools/DrumStick/3. Create L Variant")]
    static void CreateLeftVariant() =>
        CreateVariant(LeftVariantPath, "L", LeftPlayHand, "L_Wrist");

    static void CreateVariant(string variantPath, string side, string playHandPath, string wristName)
    {
        // 1. Create (or overwrite) the Prefab Variant file.
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null)
        {
            Debug.LogError("[DrumStickSetup] Base prefab not found. Run Step 1 first.");
            return;
        }
        var tempGo = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        PrefabUtility.SaveAsPrefabAsset(tempGo, variantPath);
        Object.DestroyImmediate(tempGo);
        AssetDatabase.ImportAsset(variantPath);

        // 2. Read bone transforms from the Play Hand source.
        var playHandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playHandPath);
        var playWrist = FindRecursive(playHandPrefab.transform, wristName);
        if (playWrist == null)
        {
            Debug.LogError($"[DrumStickSetup] '{wristName}' not found in {playHandPath}.");
            return;
        }

        // 3. Edit variant: add GripPoseHand + wire references.
        using var scope = new PrefabUtility.EditPrefabContentsScope(variantPath);
        var root = scope.prefabContentsRoot;

        // GripPoseHand container at identity (drum_stick local space).
        var gripPoseHand = new GameObject("GripPoseHand");
        gripPoseHand.transform.SetParent(root.transform, false);

        // Recursively copy bone transforms from PlayHand.
        var gripWrist = CopyBoneHierarchy(playWrist, gripPoseHand.transform);

        // Wire XRGrabInteractable.attachTransform → GripPoseHand/X_Wrist.
        var grab = root.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            var grabSO = new SerializedObject(grab);
            grabSO.FindProperty("m_AttachTransform").objectReferenceValue = gripWrist;
            grabSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // Wire GripPoseProvider fields for this side.
        var provider = root.GetComponent<GripPoseProvider>();
        if (provider != null)
        {
            var provSO = new SerializedObject(provider);
            if (side == "R")
            {
                provSO.FindProperty("rightGripRoot").objectReferenceValue = gripPoseHand.transform;
                provSO.FindProperty("rightGripWristRoot").objectReferenceValue = gripWrist;
            }
            else
            {
                provSO.FindProperty("leftGripRoot").objectReferenceValue = gripPoseHand.transform;
                provSO.FindProperty("leftGripWristRoot").objectReferenceValue = gripWrist;
            }
            provSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // Preview mesh: copy SkinnedMeshRenderer from PlayHand so the grip pose is
        // visible in the prefab stage. GripPoseHandPreview hides it at runtime.
        AddPreviewMesh(gripPoseHand.transform, gripWrist, playHandPrefab);
        if (!gripPoseHand.TryGetComponent<GripPoseHandPreview>(out _))
            gripPoseHand.AddComponent<GripPoseHandPreview>();

        Debug.Log($"[DrumStickSetup] Step {(side == "R" ? 2 : 3)} done: {variantPath} created with GripPoseHand ({CountBones(gripWrist)} bones).");
    }

    // Copies the SkinnedMeshRenderer from playHandPrefab and rebinds its bones
    // to the cloned bone hierarchy under gripWrist.  The copy is a sibling of
    // gripWrist so PlayHandPoseDriver.BuildJointMap never sees it.
    static void AddPreviewMesh(Transform gripPoseHand, Transform gripWrist, GameObject playHandPrefab)
    {
        // Find the hand mesh child (first SkinnedMeshRenderer in playHandPrefab).
        var sourceSMR = playHandPrefab.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
        if (sourceSMR == null)
        {
            Debug.LogWarning("[DrumStickSetup] No SkinnedMeshRenderer found in play hand — preview mesh skipped.");
            return;
        }

        // Build a name→Transform map for the copied bones under gripWrist.
        var boneMap = new Dictionary<string, Transform>(System.StringComparer.Ordinal);
        BuildBoneMap(gripPoseHand, boneMap);

        // Create the preview container as sibling of gripWrist.
        var previewContainer = new GameObject("PreviewMesh");
        previewContainer.transform.SetParent(gripPoseHand, false);
        previewContainer.tag = "EditorOnly";

        var previewGo = new GameObject("HandMeshPreview");
        previewGo.transform.SetParent(previewContainer.transform, false);

        var smr = previewGo.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh      = sourceSMR.sharedMesh;
        smr.sharedMaterials = sourceSMR.sharedMaterials;

        // Rebind rootBone.
        smr.rootBone = boneMap.TryGetValue(sourceSMR.rootBone != null ? sourceSMR.rootBone.name : "", out var rb)
            ? rb : gripWrist;

        // Rebind bones array.
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

    // Recursively copies Transform-only nodes. Skips SkinnedMeshRenderer children.
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

    static int CountBones(Transform root)
    {
        int count = 1;
        for (int i = 0; i < root.childCount; i++)
            count += CountBones(root.GetChild(i));
        return count;
    }

    // -----------------------------------------------------------------------

    [MenuItem("Tools/DrumStick/4. Adjust Ghost Hand Palm Offsets")]
    static void AdjustGhostHandPalmOffsets()
    {
        AdjustGhostOffset(GhostRight, "R_Wrist", "R_Palm");
        AdjustGhostOffset(GhostLeft,  "L_Wrist", "L_Palm");
    }

    // Moves the Ghost Hand root so that the Palm bone lands at the
    // ControllerHandRoot origin (= the physical controller's tracked point).
    static void AdjustGhostOffset(string ghostPath, string wristName, string palmName)
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(ghostPath);
        var root = scope.prefabContentsRoot;

        var wrist = FindRecursive(root.transform, wristName);
        if (wrist == null) { Debug.LogError($"[DrumStickSetup] {wristName} not found in {ghostPath}."); return; }

        var palm = FindRecursive(wrist, palmName);
        if (palm == null) { Debug.LogError($"[DrumStickSetup] {palmName} not found in {ghostPath}."); return; }

        // Ghost root offset = -(wrist.localRot * palm.localPos)
        // This makes wrist.localRot * palm.localPos + root.localPos = 0,
        // so the palm world position equals the parent (ControllerHandRoot) origin.
        var palmInRoot = wrist.localRotation * palm.localPosition;
        var newOffset  = -palmInRoot;
        root.transform.localPosition = newOffset;

        Debug.Log($"[DrumStickSetup] Step 4: {ghostPath} root offset → {newOffset} (palm now at controller origin).");
    }

    // -----------------------------------------------------------------------

    [MenuItem("Tools/DrumStick/5. Place Instances in Scene")]
    static void PlaceInstancesInScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.Contains("SampleScene"))
        {
            Debug.LogWarning("[DrumStickSetup] Open SampleScene first, then run this step.");
            return;
        }

        // Place near the drum kit in front of the player (approx position).
        PlaceStick(RightVariantPath, new Vector3(1.65f, 0.9f, 1.50f), Quaternion.Euler(0f, -11.5f, 0f));
        PlaceStick(LeftVariantPath,  new Vector3(1.45f, 0.9f, 1.50f), Quaternion.Euler(0f, -11.5f, 0f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[DrumStickSetup] Step 5 done: drum_stick_L and drum_stick_R placed in SampleScene.");
    }

    static void PlaceStick(string prefabPath, Vector3 position, Quaternion rotation)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogError($"[DrumStickSetup] Prefab not found: {prefabPath}"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetPositionAndRotation(position, rotation);
    }

    // -----------------------------------------------------------------------

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

    static void SetIfExists(SerializedObject so, string propName, int value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.intValue = value;
    }

    static void SetIfExists(SerializedObject so, string propName, float value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.floatValue = value;
    }

    static void SetIfExists(SerializedObject so, string propName, bool value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.boolValue = value;
    }
}

static class SerializedPropertyExtensions
{
    public static void SetFloatSafe(this SerializedProperty prop, float value)
    {
        if (prop != null) prop.floatValue = value;
    }
}
