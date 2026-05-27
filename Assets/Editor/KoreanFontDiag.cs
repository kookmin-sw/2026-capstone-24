using UnityEngine;
using UnityEditor;
using TMPro;

public static class KoreanFontDiag
{
    const string FontAssetPath = "Assets/Fonts/NotoSansKR-Regular SDF.asset";

    [MenuItem("Tools/Korean Font Diag")]
    static void Diag()
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fa == null) { Debug.LogError("[Diag] Font asset is NULL"); return; }

        Debug.Log("[Diag] AtlasPopulationMode = " + fa.atlasPopulationMode);
        Debug.Log("[Diag] Atlas = " + fa.atlasWidth + "x" + fa.atlasHeight);
        Debug.Log("[Diag] SourceFont = " + (fa.sourceFontFile != null ? fa.sourceFontFile.name : "NULL"));
        Debug.Log("[Diag] GlyphTable count = " + fa.glyphTable.Count);
        Debug.Log("[Diag] CharTable count = " + fa.characterTable.Count);

        if (fa.material != null)
        {
            var tex = fa.material.GetTexture("_MainTex");
            Debug.Log("[Diag] Material = " + fa.material.name
                + "  _MainTex = " + (tex != null ? tex.name + " " + tex.width + "x" + tex.height : "NULL"));
        }
        else
        {
            Debug.LogWarning("[Diag] material is NULL");
        }

        // 서브에셋 목록
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FontAssetPath))
            Debug.Log("[Diag] SubAsset: " + obj.GetType().Name + " \"" + obj.name + "\"");

        // 한글 3자 추가 시도
        bool added = fa.TryAddCharacters("가나다");
        Debug.Log("[Diag] TryAddCharacters('가나다') = " + added
            + "  glyphs after = " + fa.glyphTable.Count);
    }
}
