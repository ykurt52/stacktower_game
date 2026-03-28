#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Syncs models from _Game/Models/ to _Game/Resources/Models/ for runtime loading.
/// Run via menu: Tools > Sync Models to Resources
/// </summary>
public class ModelSync
{
    [MenuItem("Tools/Sync Models to Resources")]
    public static void SyncModels()
    {
        SyncFolder("Assets/_Game/Models/MainCharacter", "Assets/_Game/Resources/Models/MainCharacter");
        SyncFolder("Assets/_Game/Models/Enemies", "Assets/_Game/Resources/Models/Enemies");

        AssetDatabase.Refresh();
        Debug.Log("[ModelSync] Models synced to Resources successfully");
    }

    private static void SyncFolder(string srcFolder, string dstFolder)
    {
        if (!Directory.Exists(srcFolder))
        {
            Debug.LogWarning($"[ModelSync] Source folder not found: {srcFolder}");
            return;
        }

        // Create destination if needed
        if (!Directory.Exists(dstFolder))
            Directory.CreateDirectory(dstFolder);

        // Find all FBX files in source (including subfolders)
        string[] fbxFiles = Directory.GetFiles(srcFolder, "*.fbx", SearchOption.AllDirectories);

        int copied = 0;
        foreach (string srcPath in fbxFiles)
        {
            string fileName = Path.GetFileName(srcPath);
            string dstPath = Path.Combine(dstFolder, fileName);

            // Use AssetDatabase to copy — preserves import settings and .meta
            string srcUnity = srcPath.Replace("\\", "/");
            string dstUnity = dstPath.Replace("\\", "/");

            if (!File.Exists(dstPath))
            {
                AssetDatabase.CopyAsset(srcUnity, dstUnity);
                copied++;
            }
            else if (File.GetLastWriteTime(srcPath) > File.GetLastWriteTime(dstPath))
            {
                AssetDatabase.DeleteAsset(dstUnity);
                AssetDatabase.CopyAsset(srcUnity, dstUnity);
                copied++;
            }
        }

        Debug.Log($"[ModelSync] {srcFolder}: {copied} files synced, {fbxFiles.Length} total");
    }
}
#endif
