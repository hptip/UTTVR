using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps lecture slides in Resources/Lectures sharp on the classroom board: Sprite, no power-of-two stretching,
/// up to 4096 px, high-quality compression (ASTC 4x4 on Quest) and trilinear + anisotropic mipmaps for VR viewing.
/// Uses OnPostprocessAllAssets rather than OnPreprocessTexture so adding this script does not reimport every texture.
/// </summary>
sealed class LectureSlideImportSettings : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Lectures/";
    const int MaxSize = 4096;

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        var slides = imported.Concat(moved).Where(p => p.StartsWith(Folder)).ToArray();
        if (slides.Length > 0) EditorApplication.delayCall += () => Apply(slides);
    }

    [InitializeOnLoadMethod]
    static void ApplyOnLoad() => EditorApplication.delayCall += ApplyAll;

    [MenuItem("Tools/Lectures/Optimize Slide Images")]
    static void ApplyAll() =>
        Apply(AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).ToArray());

    static void Apply(string[] paths)
    {
        foreach (string path in paths)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || IsOptimized(importer)) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = MaxSize,
                format = TextureImporterFormat.ASTC_4x4, textureCompression = TextureImporterCompression.CompressedHQ,
                compressionQuality = 100
            });
            importer.SaveAndReimport();
            Debug.Log("[LectureSlides] Optimized " + path);
        }
    }

    static bool IsOptimized(TextureImporter importer)
    {
        var android = importer.GetPlatformTextureSettings("Android");
        return importer.textureType == TextureImporterType.Sprite && importer.npotScale == TextureImporterNPOTScale.None &&
               importer.mipmapEnabled && importer.filterMode == FilterMode.Trilinear && importer.anisoLevel >= 8 &&
               importer.maxTextureSize >= MaxSize && importer.textureCompression == TextureImporterCompression.CompressedHQ &&
               android.overridden && android.format == TextureImporterFormat.ASTC_4x4 && android.maxTextureSize >= MaxSize;
    }
}
