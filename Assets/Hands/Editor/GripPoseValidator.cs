using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

static class GripPoseValidator
{
    const string DrumStickRPath = "Assets/Instruments/Drum/Prefabs/drum_stick_R.prefab";
    const string DrumStickLPath = "Assets/Instruments/Drum/Prefabs/drum_stick_L.prefab";
    const string GhostRPath = "Assets/Hands/Prefabs/Ghost/RightControllerGhostHand.prefab";
    const string GhostLPath = "Assets/Hands/Prefabs/Ghost/LeftControllerGhostHand.prefab";
    const string RightPlayHandPath = "Assets/Hands/Prefabs/Play/RightPlayHand.prefab";
    const string LeftPlayHandPath  = "Assets/Hands/Prefabs/Play/LeftPlayHand.prefab";

    [MenuItem("Tools/Hands/Validate Grip Pose Wiring")]
    static void ValidateGripPoseWiring()
    {
        var pass = true;

        pass &= ValidateStickVariant(DrumStickRPath, "R_Wrist", RightPlayHandPath, "R");
        pass &= ValidateStickVariant(DrumStickLPath, "L_Wrist", LeftPlayHandPath, "L");

        if (pass)
            Debug.Log("[GripPoseValidator] Grip Pose Wiring: ALL PASS");
        else
            Debug.LogError("[GripPoseValidator] Grip Pose Wiring: FAILED — see errors above.");
    }

    static bool ValidateStickVariant(string stickPath, string wristName, string playHandPath, string side)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stickPath);
        if (prefab == null)
        {
            Debug.LogError($"[GripPoseValidator] '{stickPath}' not found.");
            return false;
        }

        var pass = true;

        // Check GripPoseProvider fields
        var provider = prefab.GetComponent<GripPoseProvider>();
        if (provider == null)
        {
            Debug.LogError($"[{stickPath}] Missing GripPoseProvider component.");
            pass = false;
        }

        // Check XRGrabInteractable.attachTransform is inside GripPoseHand
        var grab = prefab.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            Debug.LogError($"[{stickPath}] Missing XRGrabInteractable component.");
            pass = false;
        }

        var gripPoseHand = prefab.transform.Find("GripPoseHand");
        if (gripPoseHand == null)
        {
            Debug.LogError($"[{stickPath}] Missing 'GripPoseHand' child.");
            pass = false;
        }
        else
        {
            var wristInGripPose = gripPoseHand.Find(wristName);
            if (wristInGripPose == null)
            {
                Debug.LogError($"[{stickPath}] GripPoseHand is missing '{wristName}'.");
                pass = false;
            }

            if (grab != null && grab.attachTransform == null)
            {
                Debug.LogError($"[{stickPath}] XRGrabInteractable.attachTransform is not assigned.");
                pass = false;
            }
            else if (grab != null && !IsChildOf(grab.attachTransform, gripPoseHand))
            {
                Debug.LogError($"[{stickPath}] XRGrabInteractable.attachTransform is not inside GripPoseHand.");
                pass = false;
            }
        }

        // Check bone names match the play hand
        if (gripPoseHand != null)
        {
            var playHandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playHandPath);
            if (playHandPrefab != null)
            {
                var wristInPlay = playHandPrefab.transform.Find(wristName);
                if (wristInPlay != null)
                {
                    var playNames  = CollectBoneNames(wristInPlay);
                    var gripNames  = CollectBoneNames(gripPoseHand.Find(wristName));
                    var missing    = playNames.Except(gripNames).ToList();
                    var unexpected = gripNames.Except(playNames).ToList();

                    if (missing.Count > 0)
                    {
                        Debug.LogError($"[{stickPath}] GripPoseHand is missing bones from PlayHand: {string.Join(", ", missing)}");
                        pass = false;
                    }
                    if (unexpected.Count > 0)
                    {
                        Debug.LogWarning($"[{stickPath}] GripPoseHand has extra bones not in PlayHand: {string.Join(", ", unexpected)}");
                    }
                }
            }
        }

        // Rigidbody present and enabled
        var rb = prefab.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"[{stickPath}] Missing Rigidbody component.");
            pass = false;
        }

        // Preview mesh checks
        if (gripPoseHand != null)
        {
            var previewContainer = gripPoseHand.Find("PreviewMesh");
            if (previewContainer == null)
            {
                Debug.LogWarning($"[{stickPath}] GripPoseHand is missing 'PreviewMesh' child (preview mesh not set up).");
            }
            else
            {
                if (previewContainer.tag != "EditorOnly")
                    Debug.LogWarning($"[{stickPath}] PreviewMesh tag is not 'EditorOnly' — mesh will be included in builds.");

                var smr = previewContainer.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
                if (smr == null)
                {
                    Debug.LogError($"[{stickPath}] PreviewMesh has no SkinnedMeshRenderer.");
                    pass = false;
                }
                else
                {
                    if (smr.sharedMesh == null)
                    {
                        Debug.LogError($"[{stickPath}] PreviewMesh SkinnedMeshRenderer.sharedMesh is null.");
                        pass = false;
                    }
                    int nullBones = 0;
                    foreach (var b in smr.bones)
                        if (b == null) nullBones++;
                    if (nullBones > 0)
                        Debug.LogWarning($"[{stickPath}] PreviewMesh has {nullBones} null bone(s) — some verts may not deform.");
                }
                if (!gripPoseHand.TryGetComponent<GripPoseHandPreview>(out _))
                    Debug.LogWarning($"[{stickPath}] GripPoseHand is missing GripPoseHandPreview component — preview will be visible at runtime.");
            }
        }

        if (pass)
            Debug.Log($"[GripPoseValidator] {stickPath}: PASS");

        return pass;
    }

    // -----------------------------------------------------------------------

    [MenuItem("Tools/Hands/Validate Physics Layers")]
    static void ValidatePhysicsLayers()
    {
        var pass = true;

        int handPhysicsLayer = LayerMask.NameToLayer("HandPhysics");
        int heldItemLayer    = LayerMask.NameToLayer("HeldItem");

        if (handPhysicsLayer < 0)
        {
            Debug.LogError("[GripPoseValidator] Layer 'HandPhysics' not defined in TagManager.");
            pass = false;
        }
        if (heldItemLayer < 0)
        {
            Debug.LogError("[GripPoseValidator] Layer 'HeldItem' not defined in TagManager.");
            pass = false;
        }

        if (pass && !Physics.GetIgnoreLayerCollision(handPhysicsLayer, heldItemLayer))
        {
            Debug.LogError("[GripPoseValidator] HandPhysics × HeldItem collision is still ENABLED. Check DynamicsManager.");
            pass = false;
        }

        pass &= ValidateColliderLayers(
            "Assets/Hands/Prefabs/Physics/RightPhysicsHand.prefab", handPhysicsLayer);
        pass &= ValidateColliderLayers(
            "Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab", handPhysicsLayer);

        var stickBase = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Instruments/Drum/Prefabs/drum_stick.prefab");
        if (stickBase == null)
        {
            Debug.LogError("[GripPoseValidator] drum_stick.prefab not found.");
            pass = false;
        }
        else if (stickBase.layer != heldItemLayer)
        {
            Debug.LogError($"[GripPoseValidator] drum_stick.prefab root layer is {stickBase.layer}, expected HeldItem ({heldItemLayer}).");
            pass = false;
        }

        if (pass)
            Debug.Log("[GripPoseValidator] Physics Layers: ALL PASS");
        else
            Debug.LogError("[GripPoseValidator] Physics Layers: FAILED — see errors above.");
    }

    static bool ValidateColliderLayers(string prefabPath, int expectedLayer)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[GripPoseValidator] '{prefabPath}' not found.");
            return false;
        }

        bool pass = true;
        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (!t.name.StartsWith("__Collider_", System.StringComparison.Ordinal))
                continue;
            if (t.gameObject.layer != expectedLayer)
            {
                Debug.LogError($"[GripPoseValidator] {prefabPath}: '{t.name}' is on layer {t.gameObject.layer}, expected HandPhysics ({expectedLayer}).");
                pass = false;
            }
        }
        return pass;
    }

    // -----------------------------------------------------------------------

    [MenuItem("Tools/Hands/Validate Ghost Palm Offset")]
    static void ValidateGhostPalmOffset()
    {
        var pass = true;
        pass &= ValidatePalmOffset(GhostRPath, "R_Wrist", "R_Palm");
        pass &= ValidatePalmOffset(GhostLPath,  "L_Wrist", "L_Palm");

        if (pass)
            Debug.Log("[GripPoseValidator] Ghost Palm Offset: ALL PASS");
        else
            Debug.LogError("[GripPoseValidator] Ghost Palm Offset: FAILED — see errors above.");
    }

    static bool ValidatePalmOffset(string ghostPath, string wristName, string palmName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ghostPath);
        if (prefab == null)
        {
            Debug.LogError($"[GripPoseValidator] '{ghostPath}' not found.");
            return false;
        }

        var wrist = prefab.transform.Find(wristName);
        if (wrist == null)
        {
            Debug.LogError($"[{ghostPath}] '{wristName}' not found.");
            return false;
        }

        var palm = wrist.Find(palmName);
        if (palm == null)
        {
            // R_Palm may be a nested child
            palm = FindRecursive(wrist, palmName);
        }
        if (palm == null)
        {
            Debug.LogError($"[{ghostPath}] '{palmName}' not found under '{wristName}'.");
            return false;
        }

        // Compute where palm lands relative to ghost root
        var rootLocalPos = prefab.transform.localPosition;
        var wristLocalPos = wrist.localPosition;
        var wristLocalRot = wrist.localRotation;
        var palmLocalPos  = palm.localPosition;

        var palmInRoot = rootLocalPos + wristLocalPos + wristLocalRot * palmLocalPos;
        var distance   = palmInRoot.magnitude;

        if (distance < 0.001f)
        {
            Debug.Log($"[GripPoseValidator] {ghostPath}: Palm offset PASS ({distance * 1000f:F2} mm from origin).");
            return true;
        }
        else
        {
            Debug.LogWarning($"[GripPoseValidator] {ghostPath}: Palm is {distance * 1000f:F2} mm from controller origin (>1 mm). Consider adjusting Ghost root localPosition.");
            return true; // warning only — not a hard fail since user tunes this in-headset
        }
    }

    static bool IsChildOf(Transform child, Transform parent)
    {
        var t = child;
        while (t != null)
        {
            if (t == parent) return true;
            t = t.parent;
        }
        return false;
    }

    static HashSet<string> CollectBoneNames(Transform root)
    {
        var names = new HashSet<string>();
        CollectRecursive(root, names);
        return names;
    }

    static void CollectRecursive(Transform t, HashSet<string> names)
    {
        if (t == null) return;
        names.Add(t.name);
        for (int i = 0; i < t.childCount; i++)
            CollectRecursive(t.GetChild(i), names);
    }

    static Transform FindRecursive(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return c;
            var found = FindRecursive(c, name);
            if (found != null) return found;
        }
        return null;
    }
}
