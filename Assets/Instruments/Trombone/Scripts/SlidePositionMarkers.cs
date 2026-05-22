using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab root 자식에 부착. TromboneSlideController의 slideMinX/Max + SlidePositionCount 기준으로
    /// 7개 procedural torus 마커를 생성해 슬라이드 축(x)을 따라 박제한다. 색상은 TromboneSlideColors.Palette.
    /// 자신은 Trombone root의 자식이므로 TromboneAnchor가 root를 mouthpiece에 정렬할 때 자동으로 따라간다.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(10004)]
    [DisallowMultipleComponent]
    public sealed class SlidePositionMarkers : MonoBehaviour
    {
        [SerializeField] TromboneSlideController slideController;

        [Header("Torus Geometry")]
        [SerializeField, Min(0.0001f)] float majorRadius = 0.04f;
        [SerializeField, Min(0.0001f)] float minorRadius = 0.003f;
        [SerializeField, Range(8, 128)] int majorSegments = 32;
        [SerializeField, Range(3, 32)] int minorSegments = 8;

        [Header("Lateral Offset")]
        [Tooltip("슬라이드 축선 대비 (y, z) 오프셋. 후속 튜닝용. 이번 단계는 (0,0).")]
        [SerializeField] Vector2 lateralOffset = Vector2.zero;

        [Header("Material")]
        [Tooltip("미할당 시 URP/Unlit 기반 fallback 머티리얼을 자동 생성한다.")]
        [SerializeField] Material markerMaterialTemplate;

        readonly List<GameObject> _spawned = new List<GameObject>();
        Mesh _torusMesh;
        Material _fallbackTemplate;

        void OnEnable()
        {
            Rebuild();
        }

        void OnDisable()
        {
            ClearSpawned();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Rebuild();
            };
        }
#endif

        void Rebuild()
        {
            ClearSpawned();

            TromboneSlideController controller = slideController != null
                ? slideController
                : GetComponentInParent<TromboneSlideController>();
            if (controller == null) return;

            int count = TromboneSlideController.SlidePositionCount;
            if (count <= 0) return;

            EnsureTorusMesh();
            Material template = EnsureMaterialTemplate();

            float minX = controller.SlideMinX;
            float maxX = controller.SlideMaxX;

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : (float)i / (count - 1);
                float x = Mathf.Lerp(minX, maxX, t);

                var go = new GameObject($"SlideMarker_{i}");
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = new Vector3(x, lateralOffset.x, lateralOffset.y);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = _torusMesh;

                var mr = go.AddComponent<MeshRenderer>();
                Material mat = new Material(template);
                Color c = TromboneSlideColors.Palette[Mathf.Clamp(i, 0, TromboneSlideColors.Palette.Length - 1)];
                SetMaterialColor(mat, c);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;

                _spawned.Add(go);
            }
        }

        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                GameObject go = _spawned[i];
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        void EnsureTorusMesh()
        {
            if (_torusMesh != null) return;
            _torusMesh = BuildTorusMesh(majorRadius, minorRadius, majorSegments, minorSegments);
            _torusMesh.hideFlags = HideFlags.DontSave;
        }

        Material EnsureMaterialTemplate()
        {
            if (markerMaterialTemplate != null) return markerMaterialTemplate;
            if (_fallbackTemplate != null) return _fallbackTemplate;

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Standard");
            _fallbackTemplate = new Material(sh) { hideFlags = HideFlags.DontSave };
            return _fallbackTemplate;
        }

        static void SetMaterialColor(Material mat, Color c)
        {
            // URP/Unlit은 _BaseColor, Built-in은 _Color
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            mat.color = c;
        }

        static Mesh BuildTorusMesh(float major, float minor, int majorSeg, int minorSeg)
        {
            // 중심축 = x축(슬라이드 축). ring이 yz 평면에 놓이도록 생성.
            // major: 슬라이드 축에서 ring 중심까지 반경. minor: 튜브 두께(반경).
            int vCount = (majorSeg + 1) * (minorSeg + 1);
            var verts = new Vector3[vCount];
            var norms = new Vector3[vCount];
            var uvs = new Vector2[vCount];

            for (int i = 0; i <= majorSeg; i++)
            {
                float u = (float)i / majorSeg;
                float theta = u * Mathf.PI * 2f; // ring 둘레 (yz 평면 위 각도)
                float cy = Mathf.Cos(theta);
                float cz = Mathf.Sin(theta);

                for (int j = 0; j <= minorSeg; j++)
                {
                    float v = (float)j / minorSeg;
                    float phi = v * Mathf.PI * 2f; // 튜브 단면 각도
                    float r = major + minor * Mathf.Cos(phi);
                    int idx = i * (minorSeg + 1) + j;

                    float py = r * cy;
                    float pz = r * cz;
                    float px = minor * Mathf.Sin(phi);
                    verts[idx] = new Vector3(px, py, pz);

                    Vector3 ringCenter = new Vector3(0f, major * cy, major * cz);
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

            var mesh = new Mesh { name = "TromboneSlideMarker_Torus" };
            if (vCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
