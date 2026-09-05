using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Generates compact, static SDF atlases for the runtime-built loading overlay.
/// Source TTF files remain grouped with loading-screen art, while the generated
/// assets live below a Resources folder so runtime code needs no scene references.
/// </summary>
[InitializeOnLoad]
public static class MythicLoadingFontGenerator
{
    private const string Root = "Assets/UI/MythicLoading";
    private const string ResourceFolder = Root + "/Resources";
    private const string OutputFolder = ResourceFolder + "/MythicLoadingFonts";
    private const string RajdhaniSource = Root + "/Fonts/Rajdhani-SemiBold.ttf";
    private const string CinzelSource = Root + "/Fonts/Cinzel-Bold.ttf";
    private const string RajdhaniOutput = OutputFolder + "/Rajdhani-SemiBold SDF.asset";
    private const string CinzelOutput = OutputFolder + "/Cinzel-Bold SDF.asset";

    // ASCII, Latin-1, and the punctuation used by the lore ticker. This keeps each
    // atlas small while supporting current and likely romanized mythology names.
    private static readonly string CharacterSet = BuildCharacterSet();

    static MythicLoadingFontGenerator()
    {
        EditorApplication.delayCall += GenerateMissingFontAssets;
    }

    [MenuItem("Elementals Fight/Mythic Loading/Generate Missing Font Assets")]
    public static void GenerateMissingFontAssets()
    {
        EnsureFolder(ResourceFolder);
        EnsureFolder(OutputFolder);
        bool created = EnsureFont(RajdhaniSource, RajdhaniOutput);
        created |= EnsureFont(CinzelSource, CinzelOutput);
        if (created)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MythicLoadingFontGenerator: generated static Rajdhani and Cinzel SDF assets.");
        }
    }

    private static bool EnsureFont(string sourcePath, string outputPath)
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath) != null)
        {
            return false;
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
        {
            // Font imports may still be pending on the first domain reload. The menu
            // command remains available, and the next reload will retry automatically.
            return false;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            source,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            false);
        if (fontAsset == null)
        {
            Debug.LogError($"MythicLoadingFontGenerator: failed to create a TMP font from '{sourcePath}'.");
            return false;
        }

        fontAsset.name = System.IO.Path.GetFileNameWithoutExtension(outputPath);
        fontAsset.TryAddCharacters(CharacterSet, out string missingCharacters, true);
        if (!string.IsNullOrEmpty(missingCharacters))
        {
            Debug.LogWarning($"MythicLoadingFontGenerator: '{source.name}' omitted unsupported characters: {missingCharacters}");
        }

        // Static atlases avoid runtime glyph rasterization and behave consistently in
        // WebGL builds, where dynamic font generation is both slower and less reliable.
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(fontAsset, outputPath);
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        EditorUtility.SetDirty(fontAsset);
        return true;
    }

    private static string BuildCharacterSet()
    {
        System.Text.StringBuilder characters = new System.Text.StringBuilder(240);
        for (int codePoint = 32; codePoint <= 126; codePoint++)
        {
            characters.Append((char)codePoint);
        }
        for (int codePoint = 160; codePoint <= 255; codePoint++)
        {
            characters.Append((char)codePoint);
        }
        for (int codePoint = 256; codePoint <= 383; codePoint++)
        {
            characters.Append((char)codePoint);
        }
        for (int codePoint = 0x2010; codePoint <= 0x2027; codePoint++)
        {
            characters.Append((char)codePoint);
        }
        return characters.ToString();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

/// <summary>Guarantees the static font atlases exist before a player build starts.</summary>
public sealed class MythicLoadingFontBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        MythicLoadingFontGenerator.GenerateMissingFontAssets();
    }
}
