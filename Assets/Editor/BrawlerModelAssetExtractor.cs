using System;
using System.Collections.Generic;
using System.IO;
using MOBA.Core.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace MOBA.EditorTools
{
    public static class BrawlerModelAssetExtractor
    {
        private const string ModelsFolder = "Assets/_Game/Art/Brawlers/Models";
        private const string ModelMaterialsFolder = ModelsFolder + "/Materials";
        private const string ExtractedRoot = "Assets/_Game/Art/Brawlers/Extracted";

        private static readonly Dictionary<string, MaterialTextureBinding> MaterialTextureBindings =
            new Dictionary<string, MaterialTextureBinding>(StringComparer.OrdinalIgnoreCase)
            {
                { "BattalionLeader_MAT", new MaterialTextureBinding("battalion-leader-heraklios_diffuse", "battalion-leader-heraklios_normal", "battalion-leader-heraklios_specular") },
                { "battalion-leader-heraklios_diffuse", new MaterialTextureBinding("battalion-leader-heraklios_diffuse", "battalion-leader-heraklios_normal", "battalion-leader-heraklios_specular") },
                { "battalion-leader-heraklios-body_diffuse", new MaterialTextureBinding("battalion-leader-heraklios-body_diffuse", null, null) },
                { "Ch03_1001_Diffuse", new MaterialTextureBinding("Ch03_1001_Diffuse", "Ch03_1001_Normal", "Ch03_1001_Specular") },
                { "Ch03_Body", new MaterialTextureBinding("Ch03_1001_Diffuse", "Ch03_1001_Normal", "Ch03_1001_Specular") },
                { "Ch15_1001_Diffuse", new MaterialTextureBinding("Ch15_1001_Diffuse", "Ch15_1001_Normal", "Ch15_1001_Specular") },
                { "Ch15_1002_Diffuse", new MaterialTextureBinding("Ch15_1002_Diffuse", "Ch15_1002_Normal", "Ch15_1002_Specular", "Ch15_1002_Emissive") },
                { "Ch15_body", new MaterialTextureBinding("Ch15_1001_Diffuse", "Ch15_1001_Normal", "Ch15_1001_Specular") },
                { "Ch15_body1", new MaterialTextureBinding("Ch15_1002_Diffuse", "Ch15_1002_Normal", "Ch15_1002_Specular", "Ch15_1002_Emissive") },
                { "Ch24_1001_Diffuse", new MaterialTextureBinding("Ch24_1001_Diffuse", "Ch24_1001_Normal", "Ch24_1001_Specular") },
                { "Ch24_Body", new MaterialTextureBinding("Ch24_1001_Diffuse", "Ch24_1001_Normal", "Ch24_1001_Specular") },
                { "Ch43_1001_Diffuse", new MaterialTextureBinding("Ch43_1001_Diffuse", "Ch43_1001_Normal", "Ch43_1001_Specular") },
                { "Ch43_Body", new MaterialTextureBinding("Ch43_1001_Diffuse", "Ch43_1001_Normal", "Ch43_1001_Specular") },
                { "goblin_diffuse", new MaterialTextureBinding("goblin_diffuse", "goblin_normal", "goblin_specular") },
                { "Kachujin_diffuse", new MaterialTextureBinding("Kachujin_diffuse", "Kachujin_normal", "Kachujin_specular") },
                { "Kachujin_diffuse_body", new MaterialTextureBinding("Kachujin_diffuse_body", null, null) },
                { "kachujin_MAT", new MaterialTextureBinding("Kachujin_diffuse", "Kachujin_normal", "Kachujin_specular") },
                { "kachujin_MAT_", new MaterialTextureBinding("Kachujin_diffuse_body", null, null) },
                { "Knight_MAT2", new MaterialTextureBinding("Knight_diffuse", "Knight_normal", "Knight_specular") },
                { "Knight_diffuse", new MaterialTextureBinding("Knight_diffuse", "Knight_normal", "Knight_specular") },
                { "phong1", new MaterialTextureBinding("battalion-leader-heraklios_diffuse", "battalion-leader-heraklios_normal", "battalion-leader-heraklios_specular") },
            };

        [MenuItem("MOBA/Brawler Models/Prepare Imported Model Prefabs")]
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
            string prefabFolder = $"{modelRoot}/Prefabs";

            EnsureAssetFolder(modelRoot);
            EnsureAssetFolder(prefabFolder);

            NormalizeImporter(importer);
            ConfigureTextureImports(modelName);
            WireMaterialTextures(modelName);

            importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer != null)
            {
                importer.SearchAndRemapMaterials(
                    ModelImporterMaterialName.BasedOnMaterialName,
                    ModelImporterMaterialSearch.RecursiveUp);
                importer.SaveAndReimport();
            }

            WireMaterialTextures(modelName);
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

        private static void ConfigureTextureImports(string modelName)
        {
            string textureFolder = $"{ModelsFolder}/{SanitizeAssetName(modelName)}.fbm";
            if (!AssetDatabase.IsValidFolder(textureFolder))
                return;

            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer == null)
                    continue;

                string textureName = Path.GetFileNameWithoutExtension(texturePath);
                bool isNormalMap = textureName.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0;
                bool dirty = false;

                if (isNormalMap && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }

                if (isNormalMap && importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    dirty = true;
                }

                if (dirty)
                    importer.SaveAndReimport();
            }
        }

        private static void WireMaterialTextures(string modelName)
        {
            string textureFolder = $"{ModelsFolder}/{SanitizeAssetName(modelName)}.fbm";
            if (!AssetDatabase.IsValidFolder(textureFolder) || !AssetDatabase.IsValidFolder(ModelMaterialsFolder))
                return;

            Dictionary<string, Texture2D> textures = LoadTexturesByName(textureFolder);
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { ModelMaterialsFolder });
            for (int i = 0; i < materialGuids.Length; i++)
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                    continue;

                if (!TryApplyMaterialBinding(material, textures))
                    continue;

                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
        }

        private static Dictionary<string, Texture2D> LoadTexturesByName(string textureFolder)
        {
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null)
                    continue;

                textures[Path.GetFileNameWithoutExtension(texturePath)] = texture;
            }

            return textures;
        }

        private static bool TryApplyMaterialBinding(
            Material material,
            Dictionary<string, Texture2D> textures)
        {
            if (!MaterialTextureBindings.TryGetValue(material.name, out MaterialTextureBinding binding))
                return false;

            bool changed = false;
            changed |= SetTexture(material, "_BaseMap", textures, binding.AlbedoName);
            changed |= SetTexture(material, "_MainTex", textures, binding.AlbedoName);
            changed |= SetTexture(material, "_BumpMap", textures, binding.NormalName);
            changed |= SetTexture(material, "_SpecGlossMap", textures, binding.SpecularName);
            changed |= SetTexture(material, "_EmissionMap", textures, binding.EmissionName);

            if (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null)
                material.EnableKeyword("_NORMALMAP");

            if (material.HasProperty("_SpecGlossMap") && material.GetTexture("_SpecGlossMap") != null)
                material.EnableKeyword("_SPECGLOSSMAP");

            if (material.HasProperty("_EmissionMap") && material.GetTexture("_EmissionMap") != null)
                material.EnableKeyword("_EMISSION");

            return changed;
        }

        private static bool SetTexture(
            Material material,
            string propertyName,
            Dictionary<string, Texture2D> textures,
            string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || !material.HasProperty(propertyName))
                return false;

            if (!textures.TryGetValue(textureName, out Texture2D texture) || texture == null)
                return false;

            if (material.GetTexture(propertyName) == texture)
                return false;

            material.SetTexture(propertyName, texture);
            return true;
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
            CreateSocket(sockets.transform, "Head", new Vector3(0f, 1.55f, 0f));
            CreateSocket(sockets.transform, "Chest", new Vector3(0f, 1.05f, 0f));
            CreateSocket(sockets.transform, "Back", new Vector3(0f, 1.08f, 0.32f));
            CreateSocket(sockets.transform, "RightHand", new Vector3(0.36f, 0.95f, 0.25f));
            CreateSocket(sockets.transform, "LeftHand", new Vector3(-0.36f, 0.95f, 0.25f));
            Transform primary = CreateSocket(sockets.transform, "Muzzle_Main", new Vector3(0f, 1.05f, 0.65f));
            Transform secondary = CreateSocket(sockets.transform, "Muzzle_Offhand", new Vector3(0.35f, 1.05f, 0.55f));
            CreateSocket(sockets.transform, "Weapon_Main", new Vector3(0f, 1.00f, 0.35f));
            CreateSocket(sockets.transform, "Weapon_Offhand", new Vector3(-0.35f, 1.00f, 0.35f));
            CreateSocket(sockets.transform, "Throwable", new Vector3(0.18f, 1.02f, 0.46f));
            CreateSocket(sockets.transform, "HealthBarAnchor", new Vector3(0f, 2.15f, 0f));
            Transform cast = CreateSocket(sockets.transform, "AimTarget", new Vector3(0f, 1.25f, 2.50f));

            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(primary, secondary, cast);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Transform CreateSocket(Transform parent, string name, Vector3 localPosition)
        {
            GameObject socket = new GameObject(name);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = localPosition;
            return socket.transform;
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

        private readonly struct MaterialTextureBinding
        {
            public readonly string AlbedoName;
            public readonly string NormalName;
            public readonly string SpecularName;
            public readonly string EmissionName;

            public MaterialTextureBinding(
                string albedoName,
                string normalName,
                string specularName,
                string emissionName = null)
            {
                AlbedoName = albedoName;
                NormalName = normalName;
                SpecularName = specularName;
                EmissionName = emissionName;
            }
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
