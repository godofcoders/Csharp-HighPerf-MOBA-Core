using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MOBA.EditorTools
{
    public static class BrawlerModelAssetExtractor
    {
        private const string ModelsFolder = "Assets/_Game/Art/Brawlers/Models";
        private const string ExtractedRoot = "Assets/_Game/Art/Brawlers/Extracted";

        [MenuItem("MOBA/Brawler Models/Extract Materials, Textures, Prefabs")]
        public static void ExtractAllBrawlerModelAssets()
        {
            EnsureAssetFolder(ExtractedRoot);

            string[] modelPaths = Directory.GetFiles(ModelsFolder, "*.fbx", SearchOption.TopDirectoryOnly);
            Array.Sort(modelPaths, StringComparer.OrdinalIgnoreCase);

            int processed = 0;
            for (int i = 0; i < modelPaths.Length; i++)
            {
                string modelPath = ToUnityPath(modelPaths[i]);
                if (ExtractModelAssets(modelPath))
                    processed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BrawlerModelAssetExtractor] Processed {processed} brawler model FBX files.");
        }

        private static bool ExtractModelAssets(string modelPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[BrawlerModelAssetExtractor] Skipped non-model asset: {modelPath}");
                return false;
            }

            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            string modelRoot = $"{ExtractedRoot}/{SanitizeAssetName(modelName)}";
            string materialFolder = $"{modelRoot}/Materials";
            string textureFolder = $"{modelRoot}/Textures";
            string prefabFolder = $"{modelRoot}/Prefabs";

            EnsureAssetFolder(modelRoot);
            EnsureAssetFolder(materialFolder);
            EnsureAssetFolder(textureFolder);
            EnsureAssetFolder(prefabFolder);

            NormalizeImporter(importer);
            ExtractEmbeddedTextures(importer, modelPath, textureFolder);
            ExtractEmbeddedMaterials(modelPath, materialFolder);

            importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer != null)
            {
                importer.SearchAndRemapMaterials(
                    ModelImporterMaterialName.BasedOnMaterialName,
                    ModelImporterMaterialSearch.RecursiveUp);
                importer.SaveAndReimport();
            }

            CreateModelPrefab(modelPath, prefabFolder, modelName);
            return true;
        }

        private static void NormalizeImporter(ModelImporter importer)
        {
            bool dirty = false;

            if (importer.importCameras)
            {
                importer.importCameras = false;
                dirty = true;
            }

            if (importer.importLights)
            {
                importer.importLights = false;
                dirty = true;
            }

            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                dirty = true;
            }

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }

            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                dirty = true;
            }

            if (importer.materialLocation != ModelImporterMaterialLocation.External)
            {
                importer.materialLocation = ModelImporterMaterialLocation.External;
                dirty = true;
            }

            if (dirty)
                importer.SaveAndReimport();
        }

        private static void ExtractEmbeddedTextures(
            ModelImporter importer,
            string modelPath,
            string textureFolder)
        {
            try
            {
                importer.ExtractTextures(textureFolder);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[BrawlerModelAssetExtractor] Texture extraction skipped for {modelPath}: {exception.Message}");
            }
        }

        private static void ExtractEmbeddedMaterials(string modelPath, string materialFolder)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not Material material)
                    continue;

                if (AssetDatabase.GetAssetPath(material) != modelPath)
                    continue;

                string materialPath = $"{materialFolder}/{SanitizeAssetName(material.name)}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
                    continue;

                string error = AssetDatabase.ExtractAsset(material, materialPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        $"[BrawlerModelAssetExtractor] Could not extract material {material.name} from {modelPath}: {error}");
                }
            }
        }

        private static void CreateModelPrefab(
            string modelPath,
            string prefabFolder,
            string modelName)
        {
            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (sourceModel == null)
            {
                Debug.LogWarning($"[BrawlerModelAssetExtractor] Could not load model prefab for {modelPath}");
                return;
            }

            string prefabPath = $"{prefabFolder}/{SanitizeAssetName(modelName)}Model.prefab";
            GameObject root = new GameObject($"{modelName}_ModelView");
            GameObject modelInstance = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
            if (modelInstance == null)
                modelInstance = UnityEngine.Object.Instantiate(sourceModel);

            modelInstance.name = "Model";
            modelInstance.transform.SetParent(root.transform, false);

            GameObject sockets = new GameObject("Sockets");
            sockets.transform.SetParent(root.transform, false);
            CreateSocket(sockets.transform, "Muzzle_Main", new Vector3(0f, 1.05f, 0.65f));
            CreateSocket(sockets.transform, "Muzzle_Offhand", new Vector3(0.35f, 1.05f, 0.55f));
            CreateSocket(sockets.transform, "Weapon_Main", new Vector3(0f, 1.00f, 0.35f));
            CreateSocket(sockets.transform, "HealthBarAnchor", new Vector3(0f, 2.15f, 0f));
            CreateSocket(sockets.transform, "AimTarget", new Vector3(0f, 1.25f, 2.50f));

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateSocket(Transform parent, string name, Vector3 localPosition)
        {
            GameObject socket = new GameObject(name);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = localPosition;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string folder = Path.GetFileName(assetPath);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folder))
                return;

            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static string SanitizeAssetName(string value)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value.Replace(' ', '_');
        }

        private static string ToUnityPath(string path)
        {
            return path.Replace('\\', '/');
        }
    }

    public sealed class BrawlerModelImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_Game/Art/Brawlers/Models/", StringComparison.Ordinal))
                return;

            if (assetImporter is not ModelImporter importer)
                return;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.External;
        }
    }
}
