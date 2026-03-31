using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

static class PlayHandPoseDriverSceneWiring
{
    const string LeftPlayHandPath = "VR Player/Camera Offset/Hands/Left/LeftPlayHand";
    const string LeftPhysicsHandPath = "VR Player/Camera Offset/Hands/Left/LeftPhysicsHand";
    const string RightPlayHandPath = "VR Player/Camera Offset/Hands/Right/RightPlayHand";
    const string RightPhysicsHandPath = "VR Player/Camera Offset/Hands/Right/RightPhysicsHand";

    [MenuItem("Tools/Hands/Wire Play Hand Pose Drivers")]
    static void WirePlayHandPoseDrivers()
    {
        var leftPlayHand = FindRequiredTransform(LeftPlayHandPath);
        var leftPhysicsHand = FindRequiredTransform(LeftPhysicsHandPath);
        var rightPlayHand = FindRequiredTransform(RightPlayHandPath);
        var rightPhysicsHand = FindRequiredTransform(RightPhysicsHandPath);

        WireDriver(leftPlayHand.GetComponent<PlayHandPoseDriver>(), leftPhysicsHand, leftPhysicsHand.Find("L_Wrist"));
        WireDriver(rightPlayHand.GetComponent<PlayHandPoseDriver>(), rightPhysicsHand, rightPhysicsHand.Find("R_Wrist"));

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

    static void WireDriver(PlayHandPoseDriver driver, Transform sourceRoot, Transform sourceWristRoot)
    {
        if (driver == null)
            throw new MissingReferenceException("PlayHandPoseDriver is missing from a play hand root.");

        if (sourceWristRoot == null)
            throw new MissingReferenceException($"Could not find wrist under '{sourceRoot.name}'.");

        var serializedObject = new SerializedObject(driver);
        serializedObject.FindProperty("sourceRoot").objectReferenceValue = sourceRoot;
        serializedObject.FindProperty("sourceWristRoot").objectReferenceValue = sourceWristRoot;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
    }
}
