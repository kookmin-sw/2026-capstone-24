// 한글 TMP 폰트 수정 도구
// 에디터에서 한글 글리프를 아틀라스에 미리 구워 런타임 dynamic write 없이 렌더링
// 실행 후 이 파일은 삭제해도 됩니다.
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;

public static class KoreanFontFixer
{
    const string FontTtfPath   = "Assets/Fonts/NotoSansKR-Regular.ttf";
    const string FontAssetPath = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
    const string TutorialRoot  = "Assets/Resources/Tutorial";

    [MenuItem("Tools/Fix Korean Font Atlas (NotoSansKR)")]
    static void Fix()
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fa == null) { Debug.LogError("[KFix] Asset not found: " + FontAssetPath); return; }

        var srcFont = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
        if (srcFont == null) { Debug.LogError("[KFix] TTF not found: " + FontTtfPath); return; }

        // ── 1. 기존 Texture2D 서브에셋 제거, 1×1 플레이스홀더 추가 ─────────
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FontAssetPath))
            if (obj is Texture2D old)
            {
                AssetDatabase.RemoveObjectFromAsset(old);
                Object.DestroyImmediate(old, true);
            }

        var atlas = new Texture2D(1, 1, TextureFormat.Alpha8, false);
        atlas.name = "Atlas";
        atlas.hideFlags = HideFlags.HideInHierarchy;
        atlas.Apply(false, false);
        AssetDatabase.AddObjectToAsset(atlas, FontAssetPath);

        // ── 2. 폰트 내부 필드 설정 (SerializedObject) ────────────────────
        var so = new SerializedObject(fa);
        so.Update();
        SetObj(so, "m_SourceFontFile",    srcFont);
        SetInt(so, "m_AtlasPopulationMode", 1);   // Dynamic
        SetInt(so, "m_AtlasWidth",          2048);
        SetInt(so, "m_AtlasHeight",         2048);
        SetInt(so, "m_AtlasTextureIndex",   0);
        SetObj(so, "m_AtlasTexture",        atlas);
        var arrProp = so.FindProperty("m_AtlasTextures");
        if (arrProp != null) { arrProp.arraySize = 1; arrProp.GetArrayElementAtIndex(0).objectReferenceValue = atlas; }
        ClearArr(so, "m_GlyphTable");
        ClearArr(so, "m_CharacterTable");
        ClearArr(so, "m_UsedGlyphRects");
        ClearArr(so, "m_FreeGlyphRects");
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(fa);

        // ── 3. 에디터에서 글리프 미리 굽기 ──────────────────────────────
        //   TMP 가 atlas.width<=1 감지 → Reinitialize(2048,2048) → FontEngine 으로 래스터화
        Debug.Log("[KFix] Baking glyphs... (may take a moment)");
        string charset = BuildCharSet();
        bool allAdded = fa.TryAddCharacters(charset);
        int baked = fa.glyphTable.Count;
        Debug.Log("[KFix] TryAddCharacters=" + allAdded + "  baked glyphs=" + baked);

        // 래스터화된 픽셀을 서브에셋에 반영
        var baked_tex = fa.atlasTexture;
        if (baked_tex != null)
        {
            baked_tex.Apply(false, false);
            EditorUtility.SetDirty(baked_tex);
            Debug.Log("[KFix] Atlas texture saved: " + baked_tex.width + "x" + baked_tex.height);
        }
        else
        {
            Debug.LogWarning("[KFix] fa.atlasTexture is null after TryAddCharacters.");
        }

        // ── 4. Material 연결 ──────────────────────────────────────────────
        Material mat = null;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FontAssetPath))
            if (obj is Material m) { mat = m; break; }

        if (mat != null)
        {
            var tex = baked_tex ?? atlas;
            mat.SetTexture("_MainTex",     tex);
            mat.SetFloat("_TextureWidth",  (float)tex.width);
            mat.SetFloat("_TextureHeight", (float)tex.height);
            EditorUtility.SetDirty(mat);

            var so2 = new SerializedObject(fa);
            so2.Update();
            SetObj(so2, "m_Material", mat);
            so2.ApplyModifiedProperties();
            Debug.Log("[KFix] Material linked: " + mat.name);
        }
        else { Debug.LogWarning("[KFix] Material sub-asset not found."); }

        fa.name = "NotoSansKR-Regular SDF";
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[KFix] Done!  glyphs=" + fa.glyphTable.Count
            + "  mat=" + (fa.material != null ? fa.material.name : "NULL"));
    }

    // description.txt 에서 실제 쓰이는 한글 + ASCII 집합
    // (순서 보장: description 파일 문자 → ASCII → 나머지 현대 한글)
    static string BuildCharSet()
    {
        var seen = new HashSet<char>();
        var list = new List<char>();
        void Add(char c) { if (seen.Add(c)) list.Add(c); }

        // 1) description.txt 파일 내 문자 (최우선)
        string absRoot = Path.GetFullPath(TutorialRoot);
        if (Directory.Exists(absRoot))
            foreach (var f in Directory.GetFiles(absRoot, "description.txt", SearchOption.AllDirectories))
                foreach (char c in File.ReadAllText(f, System.Text.Encoding.UTF8))
                    Add(c);

        // 2) ASCII 출력 가능 문자
        for (char c = ' '; c <= '~'; c++) Add(c);

        // 3) 현대 한글 음절 (U+AC00~U+D7A3), atlas 가 허용하는 만큼 포함
        for (int cp = 0xAC00; cp <= 0xD7A3; cp++) Add((char)cp);

        return new string(list.ToArray());
    }

    static void SetInt(SerializedObject so, string n, int v)   { var p = so.FindProperty(n); if (p != null) p.intValue = v; }
    static void SetObj(SerializedObject so, string n, Object v){ var p = so.FindProperty(n); if (p != null) p.objectReferenceValue = v; }
    static void ClearArr(SerializedObject so, string n)        { var p = so.FindProperty(n); if (p != null && p.isArray) p.arraySize = 0; }
}
