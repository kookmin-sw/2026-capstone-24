#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Instruments.EditorTools
{
    /// <summary>
    /// 1회성 에디터 유틸리티: 드럼 8개 조각의 Donut 비주얼을
    /// (1) XR 샘플 토러스 대비 튜브 굵기 절반인 새 메쉬로 교체하고
    /// (2) URP/Unlit material을 반투명(alpha 0.5)으로 전환한다.
    /// Tools/Drum 메뉴에서 1회 실행하면 자산이 영구 갱신된다.
    /// </summary>
    static class DrumDonutSetup
    {
        const string BakedMeshPath = "Assets/Instruments/Drum/Models/DrumDonut.asset";
        const string XrTorusGuid = "f077c919501a44778a0c2edb6eb1a54a";

        const int MajorSegments = 48;
        const int MinorSegments = 12;
        const float TargetAlpha = 0.5f;

        static readonly string[] PiecePrefabPaths =
        {
            "Assets/Instruments/Drum/Prefabs/BassDrum.prefab",
            "Assets/Instruments/Drum/Prefabs/Snare.prefab",
            "Assets/Instruments/Drum/Prefabs/HiHat.prefab",
            "Assets/Instruments/Drum/Prefabs/FloorTom.prefab",
            "Assets/Instruments/Drum/Prefabs/MidTom.prefab",
            "Assets/Instruments/Drum/Prefabs/HighTom.prefab",
            "Assets/Instruments/Drum/Prefabs/CrashCymbal.prefab",
            "Assets/Instruments/Drum/Prefabs/RideCymbal.prefab",
        };

        static readonly string[] MaterialPaths =
        {
            "Assets/Instruments/Drum/Models/Materials/Donut_1_Red.mat",
            "Assets/Instruments/Drum/Models/Materials/Donut_2_Orange.mat",
            "Assets/Instruments/Drum/Models/Materials/Donut_3_Yellow.mat",
            "Assets/Instruments/Drum/Models/Materials/Donut_4_Green.mat",
            "Assets/Instruments/Drum/Models/Materials/Donut_5_Blue.mat",
            "Assets/Instruments/Drum/Models/Materials/Donut_6_Indigo.mat",
            "Assets/Instruments/Drum/Models/Materials/Donut_7_SkyBlue.mat",
            "Assets/Instruments/Drum/Models/Materials/Donut_8_Pink.mat",
        };

        [MenuItem("Tools/Drum/Setup Donuts (thin mesh + transparency)")]
        static void Run()
        {
            Mesh baked = BakeThinTorus();
            if (baked == null)
            {
                Debug.LogError("[DrumDonutSetup] 토러스 메쉬 베이크 실패 — XR 토러스 원본을 찾지 못했습니다.");
                return;
            }

            int repointed = RepointDonutMeshFilters(baked);
            int transparent = MakeMaterialsTransparent();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DrumDonutSetup] 완료 — 메쉬 베이크 1, MeshFilter 재지정 {repointed}/8, material 투명화 {transparent}/8.");
        }

        static Mesh BakeThinTorus()
        {
            string xrPath = AssetDatabase.GUIDToAssetPath(XrTorusGuid);
            if (string.IsNullOrEmpty(xrPath)) return null;

            Mesh source = AssetDatabase.LoadAllAssetsAtPath(xrPath).OfType<Mesh>().FirstOrDefault();
            if (source == null) return null;

            Vector3 size = source.bounds.size;
            float minExtent = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float maxExtent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

            float minorOld = minExtent * 0.5f;        // 튜브 축 방향 두께 = 2·minor
            float outer = maxExtent * 0.5f;            // major + minor
            float minorNew = minorOld * 0.5f;          // 튜브 절반
            float majorNew = Mathf.Max(outer - minorNew, minorNew); // 외경 보존

            Mesh mesh = BuildTorusYUp(majorNew, minorNew, MajorSegments, MinorSegments);
            mesh.name = "DrumDonut";

            if (AssetDatabase.LoadAssetAtPath<Mesh>(BakedMeshPath) != null)
                AssetDatabase.DeleteAsset(BakedMeshPath);
            AssetDatabase.CreateAsset(mesh, BakedMeshPath);
            return mesh;
        }

        static int RepointDonutMeshFilters(Mesh baked)
        {
            int count = 0;
            foreach (string path in PiecePrefabPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform donut = root.transform.Find("Donut");
                    var mf = donut != null ? donut.GetComponent<MeshFilter>() : null;
                    if (mf != null)
                    {
                        mf.sharedMesh = baked;
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        count++;
                    }
                    else
                    {
                        Debug.LogWarning($"[DrumDonutSetup] {path} 에서 Donut/MeshFilter를 찾지 못했습니다.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return count;
        }

        static int MakeMaterialsTransparent()
        {
            int count = 0;
            foreach (string path in MaterialPaths)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    Debug.LogWarning($"[DrumDonutSetup] material 없음: {path}");
                    continue;
                }

                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetShaderPassEnabled("DepthOnly", false);

                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.a = TargetAlpha;
                    mat.SetColor("_BaseColor", c);
                }
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.GetColor("_Color");
                    c.a = TargetAlpha;
                    mat.SetColor("_Color", c);
                }

                EditorUtility.SetDirty(mat);
                count++;
            }
            return count;
        }

        // 링이 XZ 평면(구멍이 +Y)에 놓이는 토러스. major: 링 중심 반경, minor: 튜브 반경.
        static Mesh BuildTorusYUp(float major, float minor, int majorSeg, int minorSeg)
        {
            int vCount = (majorSeg + 1) * (minorSeg + 1);
            var verts = new Vector3[vCount];
            var norms = new Vector3[vCount];
            var uvs = new Vector2[vCount];

            for (int i = 0; i <= majorSeg; i++)
            {
                float u = (float)i / majorSeg;
                float theta = u * Mathf.PI * 2f;       // 링 둘레 (XZ 평면)
                float cx = Mathf.Cos(theta);
                float cz = Mathf.Sin(theta);

                for (int j = 0; j <= minorSeg; j++)
                {
                    float v = (float)j / minorSeg;
                    float phi = v * Mathf.PI * 2f;      // 튜브 단면
                    float r = major + minor * Mathf.Cos(phi);
                    int idx = i * (minorSeg + 1) + j;

                    float px = r * cx;
                    float pz = r * cz;
                    float py = minor * Mathf.Sin(phi);
                    verts[idx] = new Vector3(px, py, pz);

                    Vector3 ringCenter = new Vector3(major * cx, 0f, major * cz);
                    norms[idx] = (verts[idx] - ringCenter).normalized;
                    uvs[idx] = new Vector2(u, v);
                }
            }

            var tris = new int[majorSeg * minorSeg * 6];
            int t = 0;
            for (int i = 0; i < majorSeg; i++)
            {
                for (int j = 0; j < minorSeg; j++)
                {
                    int a = i * (minorSeg + 1) + j;
                    int b = a + 1;
                    int c = a + (minorSeg + 1);
                    int d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            }

            var mesh = new Mesh();
            if (vCount > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
