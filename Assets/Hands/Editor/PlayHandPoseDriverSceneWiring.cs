using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

static class PlayHandPoseDriverSceneWiring
{
    const string LeftPlayHandPath = "VR Player/Camera Offset/Hands/Left/LeftPlayHand";
    const string LeftPhysicsHandPath = "VR Player/Camera Offset/Hands/Left/LeftPhysicsHand";
    const string LeftGhostHandPath = "VR Player/Camera Offset/Hands/Left/LeftHandTrackingHandRoot/LeftHandTrackingGhostHand";
    const string RightPlayHandPath = "VR Player/Camera Offset/Hands/Right/RightPlayHand";
    const string RightPhysicsHandPath = "VR Player/Camera Offset/Hands/Right/RightPhysicsHand";
    const string RightGhostHandPath = "VR Player/Camera Offset/Hands/Right/RightHandTrackingHandRoot/RightHandTrackingGhostHand";

    [MenuItem("Tools/Hands/Wire Play Hand Pose Drivers")]
    static void WirePlayHandPoseDrivers()
    {
        var leftPlayHand = FindRequiredTransform(LeftPlayHandPath);
        var leftPhysicsHand = FindRequiredTransform(LeftPhysicsHandPath);
        var leftGhostHand = FindRequiredTransform(LeftGhostHandPath);
        var rightPlayHand = FindRequiredTransform(RightPlayHandPath);
        var rightPhysicsHand = FindRequiredTransform(RightPhysicsHandPath);
        var rightGhostHand = FindRequiredTransform(RightGhostHandPath);

        WireDriver(
            leftPlayHand.GetComponent<PlayHandPoseDriver>(),
            leftPhysicsHand, leftPhysicsHand.Find("L_Wrist"),
            leftGhostHand, leftGhostHand.Find("L_Wrist"));

        WireDriver(
            rightPlayHand.GetComponent<PlayHandPoseDriver>(),
            rightPhysicsHand, rightPhysicsHand.Find("R_Wrist"),
            rightGhostHand, rightGhostHand.Find("R_Wrist"));

        EditorSceneManager.MarkSceneDirty(leftPlayHand.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
    }

    static Transform FindRequiredTransform(string path)
    {
        var gameObject = GameObject.Find(path);
        if (gameObject != null)
            return gameObject.transform;

        throw new MissingReferenceException($"Could not find scene object at '{path}'.");
    }

    static void WireDriver(
        PlayHandPoseDriver driver,
        Transform sourceRoot, Transform sourceWristRoot,
        Transform fallbackSourceRoot, Transform fallbackSourceWristRoot)
    {
        if (driver == null)
            throw new MissingReferenceException("PlayHandPoseDriver is missing from a play hand root.");

        if (sourceWristRoot == null)
            throw new MissingReferenceException($"Could not find wrist under '{sourceRoot.name}'.");

        if (fallbackSourceWristRoot == null)
            throw new MissingReferenceException($"Could not find wrist under '{fallbackSourceRoot.name}'.");

        var serializedObject = new SerializedObject(driver);
        serializedObject.FindProperty("sourceRoot").objectReferenceValue = sourceRoot;
        serializedObject.FindProperty("sourceWristRoot").objectReferenceValue = sourceWristRoot;
        serializedObject.FindProperty("fallbackSourceRoot").objectReferenceValue = fallbackSourceRoot;
        serializedObject.FindProperty("fallbackSourceWristRoot").objectReferenceValue = fallbackSourceWristRoot;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
    }
}
