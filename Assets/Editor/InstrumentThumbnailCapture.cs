// 악기 프리팹을 오프스크린 카메라로 찍어 Resources/Thumbnails/ 에 PNG 저장
// 실행: Tools → Capture Instrument Thumbnails
using UnityEngine;
using UnityEditor;
using System.IO;

public static class InstrumentThumbnailCapture
{
    const int  Width  = 512;
    const int  Height = 512;
    const string OutDir = "Assets/Resources/Thumbnails";

    static readonly (string prefabPath, string id)[] Instruments =
    {
        ("Assets/Instruments/Piano/Prefabs/Piano.prefab",       "piano"),
        ("Assets/Instruments/Drum/Prefabs/DrumKit.prefab",      "drum"),
        ("Assets/Instruments/Trombone/Prefabs/Trombone.prefab", "trombone"),
    };

    [MenuItem("Tools/Capture Instrument Thumbnails")]
    static void Capture()
    {
        if (!Directory.Exists(OutDir))
            Directory.CreateDirectory(OutDir);

        foreach (var (path, id) in Instruments)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[Thumb] Prefab not found: " + path); continue; }

            var png = RenderPrefab(prefab, id);
            if (png == null) continue;

            string outPath = $"{OutDir}/{id}.png";
            File.WriteAllBytes(outPath, png);
            Debug.Log("[Thumb] Saved: " + outPath);
        }

        AssetDatabase.Refresh();
        Debug.Log("[Thumb] Done! Check Assets/Resources/Thumbnails/");
    }

    static byte[] RenderPrefab(GameObject prefab, string id)
    {
        // ── 오프스크린 RenderTexture 준비 ─────────────────────────────────
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;

        // ── 임시 씬 오브젝트 생성 ─────────────────────────────────────────
        var root = new GameObject($"__ThumbRoot_{id}");

        // 악기 인스턴스
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;

        // Renderer 들의 Bounds 계산
        var renderers = inst.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[Thumb] No renderers in " + prefab.name);
            Object.DestroyImmediate(root);
            rt.Release();
            return null;
        }

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        // 카메라 위치: 악기 정면 약간 위에서 바라봄
        var camGo = new GameObject("__ThumbCam");
        camGo.transform.SetParent(root.transform);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.20f, 0.24f, 1f); // 어두운 배경
        cam.orthographic    = false;
        cam.fieldOfView     = 40f;
        cam.targetTexture   = rt;
        cam.nearClipPlane   = 0.01f;
        cam.farClipPlane    = 500f;

        // bounds 에 맞춰 카메라 거리 자동 계산
        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        float dist      = maxExtent / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.6f;
        var   dir       = new Vector3(0.4f, 0.35f, 1f).normalized;
        camGo.transform.position = bounds.center + dir * dist;
        camGo.transform.LookAt(bounds.center);

        // 라이트
        var lightGo = new GameObject("__ThumbLight");
        lightGo.transform.SetParent(root.transform);
        var light = lightGo.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1.4f;
        light.color     = Color.white;
        lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // ── 렌더 ──────────────────────────────────────────────────────────
        cam.Render();

        // RenderTexture → Texture2D → PNG
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        byte[] png = tex.EncodeToPNG();

        // ── 정리 ──────────────────────────────────────────────────────────
        Object.DestroyImmediate(root);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        return png;
    }
}
