using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SessionPanel.EditorTools
{
    /// <summary>
    /// 빌드 직전에 Assets/StreamingAssets/Songs/index.json 을 자동 생성한다.
    /// Android에서는 StreamingAssets 폴더 스캔이 불가능하므로 런타임이 이 인덱스를 사용한다.
    /// </summary>
    public class SongIndexBuilder : IPreprocessBuildWithReport
    {
        const string SongsRel = "Songs";
        const string IndexFile = "index.json";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            GenerateIndex();
        }

        [MenuItem("Tools/SessionPanel/Regenerate Song Index")]
        public static void GenerateIndex()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, SongsRel);
            if (!Directory.Exists(folder)) return;

            var files = Directory.EnumerateFiles(folder, "*.vmsong", SearchOption.TopDirectoryOnly)
                                 .Select(Path.GetFileName)
                                 .OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase)
                                 .ToArray();

            string json = JsonUtility.ToJson(new IndexJson { files = files });
            string indexPath = Path.Combine(folder, IndexFile);
            File.WriteAllText(indexPath, json);
            AssetDatabase.ImportAsset("Assets/StreamingAssets/" + SongsRel + "/" + IndexFile);
        }

        [System.Serializable]
        class IndexJson { public string[] files; }
    }
}
