#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using TMPro;

/// <summary>
/// Packs individual icon PNGs into a TMP_SpriteAsset atlas.
/// Menu: Tools > StackTower > Build Icon Sprite Asset
/// </summary>
public static class SpriteAssetBuilder
{
    private const string ICONS_FOLDER = "Assets/_Game/Textures/Icons";
    private const string OUTPUT_PATH = "Assets/_Game/Data/UI/IconSpriteAsset.asset";
    private const string ATLAS_PATH = "Assets/_Game/Data/UI/IconAtlas.png";
    private const string MAT_PATH = "Assets/_Game/Data/UI/IconSpriteAsset_Material.mat";

    private static readonly string[] ICON_NAMES =
    {
        "icon_sword",
        "icon_heart",
        "icon_lightning",
        "icon_gear",
        "icon_star",
        "icon_diamond",
        "icon_arrow_up"
    };

    [MenuItem("Tools/StackTower/Build Icon Sprite Asset")]
    public static void Build()
    {
        // Ensure output directory exists
        string outputDir = Path.GetDirectoryName(OUTPUT_PATH);
        if (!AssetDatabase.IsValidFolder(outputDir))
        {
            string parent = Path.GetDirectoryName(outputDir);
            string folder = Path.GetFileName(outputDir);
            AssetDatabase.CreateFolder(parent, folder);
        }

        // Load source textures — ensure readable + sprite type
        var sourceTextures = new List<Texture2D>();
        var sourceNames = new List<string>();

        foreach (string name in ICON_NAMES)
        {
            string path = $"{ICONS_FOLDER}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[SpriteAssetBuilder] Icon not found: {path}");
                continue;
            }

            bool changed = false;
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                sourceTextures.Add(tex);
                sourceNames.Add(name.Replace("icon_", ""));
            }
        }

        if (sourceTextures.Count == 0)
        {
            Debug.LogError("[SpriteAssetBuilder] No icons found!");
            return;
        }

        // ── Pack into atlas ──
        var atlas = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        atlas.filterMode = FilterMode.Bilinear;
        Rect[] rects = atlas.PackTextures(sourceTextures.ToArray(), 2, 512);

        int atlasW = atlas.width;
        int atlasH = atlas.height;

        File.WriteAllBytes(ATLAS_PATH, atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas);
        AssetDatabase.ImportAsset(ATLAS_PATH, ImportAssetOptions.ForceUpdate);

        // Atlas import settings
        var atlasImporter = AssetImporter.GetAtPath(ATLAS_PATH) as TextureImporter;
        if (atlasImporter != null)
        {
            atlasImporter.textureType = TextureImporterType.Sprite;
            atlasImporter.spriteImportMode = SpriteImportMode.Single;
            atlasImporter.isReadable = true;
            atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
            atlasImporter.filterMode = FilterMode.Bilinear;
            atlasImporter.mipmapEnabled = false;
            atlasImporter.alphaIsTransparency = true;
            atlasImporter.SaveAndReimport();
        }

        var savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(ATLAS_PATH);

        // ── Material — always recreate to guarantee texture reference ──
        var shader = Shader.Find("TextMeshPro/Sprite");
        var oldMat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        if (oldMat != null)
            AssetDatabase.DeleteAsset(MAT_PATH);

        var mat = new Material(shader);
        mat.SetTexture("_MainTex", savedAtlas);
        AssetDatabase.CreateAsset(mat, MAT_PATH);

        // ── Create or reuse TMP_SpriteAsset ──
        var spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(OUTPUT_PATH);
        if (spriteAsset == null)
        {
            spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            AssetDatabase.CreateAsset(spriteAsset, OUTPUT_PATH);
        }

        // ── Write ALL data via SerializedObject for reliable persistence ──
        var so = new SerializedObject(spriteAsset);

        so.FindProperty("m_Version").stringValue = "1.1.0";
        so.FindProperty("m_Material").objectReferenceValue = mat;
        so.FindProperty("spriteSheet").objectReferenceValue = savedAtlas;

        // Glyph table
        var glyphTable = so.FindProperty("m_GlyphTable");
        glyphTable.ClearArray();

        for (int i = 0; i < rects.Length; i++)
        {
            int x = (int)(rects[i].x * atlasW);
            int y = (int)(rects[i].y * atlasH);
            int w = (int)(rects[i].width * atlasW);
            int h = (int)(rects[i].height * atlasH);

            glyphTable.InsertArrayElementAtIndex(i);
            var glyph = glyphTable.GetArrayElementAtIndex(i);
            glyph.FindPropertyRelative("m_Index").intValue = i;
            glyph.FindPropertyRelative("m_Scale").floatValue = 2f;

            var metrics = glyph.FindPropertyRelative("m_Metrics");
            metrics.FindPropertyRelative("m_Width").floatValue = w;
            metrics.FindPropertyRelative("m_Height").floatValue = h;
            metrics.FindPropertyRelative("m_HorizontalBearingX").floatValue = 0;
            metrics.FindPropertyRelative("m_HorizontalBearingY").floatValue = h * 0.8f;
            metrics.FindPropertyRelative("m_HorizontalAdvance").floatValue = w;

            var glyphRect = glyph.FindPropertyRelative("m_GlyphRect");
            glyphRect.FindPropertyRelative("m_X").intValue = x;
            glyphRect.FindPropertyRelative("m_Y").intValue = y;
            glyphRect.FindPropertyRelative("m_Width").intValue = w;
            glyphRect.FindPropertyRelative("m_Height").intValue = h;
        }

        // Character table
        var charTable = so.FindProperty("m_SpriteCharacterTable");
        charTable.ClearArray();

        for (int i = 0; i < rects.Length; i++)
        {
            charTable.InsertArrayElementAtIndex(i);
            var ch = charTable.GetArrayElementAtIndex(i);
            ch.FindPropertyRelative("m_Name").stringValue = sourceNames[i];
            ch.FindPropertyRelative("m_Scale").floatValue = 1f;
            ch.FindPropertyRelative("m_GlyphIndex").intValue = i;
            ch.FindPropertyRelative("m_ElementType").intValue = 1; // Sprite
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // Update internal lookup tables
        spriteAsset.UpdateLookupTables();

        EditorUtility.SetDirty(spriteAsset);
        AssetDatabase.SaveAssets();

        // ── Assign as TMP default sprite asset ──
        var tmpSettings = Resources.Load<TMP_Settings>("TMP Settings");
        if (tmpSettings != null)
        {
            var tso = new SerializedObject(tmpSettings);
            tso.FindProperty("m_defaultSpriteAsset").objectReferenceValue = spriteAsset;
            tso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tmpSettings);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpriteAssetBuilder] Set as TMP default sprite asset.");
        }

        AssetDatabase.Refresh();
        Debug.Log($"[SpriteAssetBuilder] Done! {rects.Length} icons packed into {OUTPUT_PATH}");
        Debug.Log("[SpriteAssetBuilder] Sprites: " + string.Join(", ", sourceNames));
    }
}
#endif
