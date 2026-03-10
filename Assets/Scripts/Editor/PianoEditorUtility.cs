using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

static class PianoEditorUtility
{
    const int KeyCount = 88;

    public static bool TryGetPianoRoot(out GameObject pianoRoot)
    {
        if (TryGetPianoRootFromSelection(out pianoRoot))
            return true;

        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (IsPianoRoot(rootObject.transform))
            {
                pianoRoot = rootObject;
                return true;
            }
        }

        pianoRoot = null;
        return false;
    }

    public static bool TryGetKeyRigRoot(GameObject pianoRoot, out Transform keyRigRoot)
    {
        keyRigRoot = null;
        if (pianoRoot == null)
            return false;

        Transform root = pianoRoot.transform.Find("Piano_Rig/Root");
        if (root == null)
            root = pianoRoot.transform.Find("PianoModel/Piano_Rig/Root");

        if (root == null)
            root = pianoRoot.transform.Find("Sample Piano/Piano_Rig/Root");

        if (root == null)
            return false;

        int keyBoneCount = 0;
        foreach (Transform child in root)
        {
            if (child.name.StartsWith("key_") && !child.name.EndsWith("_end"))
                keyBoneCount++;
        }

        if (keyBoneCount != KeyCount)
            return false;

        keyRigRoot = root;
        return true;
    }

    static bool TryGetPianoRootFromSelection(out GameObject pianoRoot)
    {
        pianoRoot = null;
        GameObject activeObject = Selection.activeGameObject;
        if (activeObject == null)
            return false;

        Transform current = activeObject.transform;
        while (current != null)
        {
            if (IsPianoRoot(current))
            {
                pianoRoot = current.gameObject;
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    static bool IsPianoRoot(Transform candidate)
    {
        return candidate != null && TryGetKeyRigRoot(candidate.gameObject, out _);
    }
}
