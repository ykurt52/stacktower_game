#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Ensures all FBX models in Resources/Models/ have Rig set to Generic (not None).
/// Run via: Tools > Fix Model Rig Settings
/// </summary>
public class ModelRigFixer
{
    [MenuItem("Tools/Fix Model Rig Settings")]
    public static void FixAll()
    {
        int fixed_count = 0;
        string[] folders = {
            "Assets/_Game/Resources/Models/MainCharacter",
            "Assets/_Game/Resources/Models/Enemies"
        };

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;

            string[] fbxFiles = Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly);
            foreach (string path in fbxFiles)
            {
                string unityPath = path.Replace("\\", "/");
                var importer = AssetImporter.GetAtPath(unityPath) as ModelImporter;
                if (importer == null) continue;

                bool changed = false;

                // Fix rig type
                if (importer.animationType != ModelImporterAnimationType.Generic)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    changed = true;
                }

                // Avatar must be created from model
                if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    changed = true;
                }

                // Make sure animations are imported
                if (!importer.importAnimation)
                {
                    importer.importAnimation = true;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    fixed_count++;
                    Debug.Log($"[ModelRigFixer] Fixed: {unityPath}");
                }
                else
                {
                    Debug.Log($"[ModelRigFixer] OK: {unityPath} (already Generic)");
                }
            }
        }

        Debug.Log($"[ModelRigFixer] Done. Fixed {fixed_count} models.");
    }
}
#endif
