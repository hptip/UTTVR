using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Clears and regenerates the dynamic TMP font assets (glyphs come back from the source .ttf on demand).
/// Use after a bad merge leaves the serialized glyph tables out of sync with the atlas textures.
/// Menu: Tools/Fonts/Repair Dynamic TMP Fonts, or write "repair" to Temp/TmpFontRepair/request.txt.
/// </summary>
[InitializeOnLoad]
static class TmpDynamicFontRepair
{
    const string Dir = "Temp/TmpFontRepair";
    const string Probe = "Tàu điện ngầm Riyadh Metro ệẫựữởỡặẳẵậấầẩẽếềểễọỏốồổỗớờợụủứừửđĐ";
    static TmpDynamicFontRepair() => EditorApplication.update += Poll;

    static void Poll()
    {
        string request = Path.Combine(Dir, "request.txt");
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        Repair();
    }

    [MenuItem("Tools/Fonts/Repair Dynamic TMP Fonts")]
    static void Repair()
    {
        var log = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/TextMesh Pro/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;
            if (font.sourceFontFile == null) { log.AppendLine($"SKIP {path}: missing source font file"); continue; }

            font.ClearFontAssetData(true);
            bool added = font.TryAddCharacters(Probe, out string missing);
            EditorUtility.SetDirty(font);
            log.AppendLine($"OK {path} | added={added} | glyphs={font.glyphTable.Count} | missing='{missing}'");
        }
        AssetDatabase.SaveAssets();
        TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, null);
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "result.txt"), log.ToString());
        Debug.Log("[TmpDynamicFontRepair]\n" + log);
    }
}
