using UnityEditor;
using UnityEngine;

public static class StripMixerGroups
{
    [MenuItem("Tools/Strip All Mixer Groups")]
    public static void Run()
    {
        int count = 0;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            if (StripFromGameObject(prefab))
            {
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                count++;
            }
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (StripFromGameObject(root))
                    changed = true;
            }
            if (changed)
            {
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                count++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log($"[StripMixerGroups] {count}개 에셋에서 AudioMixerGroup 참조를 제거했습니다.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static bool StripFromGameObject(GameObject root)
    {
        bool changed = false;
        foreach (AudioSource src in root.GetComponentsInChildren<AudioSource>(true))
        {
            if (src.outputAudioMixerGroup != null)
            {
                src.outputAudioMixerGroup = null;
                EditorUtility.SetDirty(src);
                changed = true;
            }
        }
        return changed;
    }
}
