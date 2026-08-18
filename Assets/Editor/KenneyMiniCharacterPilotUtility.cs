using System;
using System.IO;
using MOBA.Core.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace MOBA.EditorTools
{
    public static class KenneyMiniCharacterPilotUtility
    {
        private const string ModelPath =
            "Assets/_Game/Art/Brawlers/KenneyMiniCharacters/Models/Kenney_Mini_Male_A.fbx";
        private const string PrefabFolder =
            "Assets/_Game/Prefabs/Characters/Kenney";
        private const string PrefabPath =
            PrefabFolder + "/Kenney_Mini_Male_A_Test.prefab";

        [MenuItem("MOBA/Brawler Models/Create Kenney Mini Male A Test Prefab")]
        public static void CreatePilotPrefab()
        {
            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (sourceModel == null)
            {
                Debug.LogError(
                    $"[KenneyMiniCharacterPilotUtility] Could not load pilot model at {ModelPath}. Let Unity finish importing the FBX, then try again.");
                return;
            }

            EnsureAssetFolder(PrefabFolder);

            GameObject root = new GameObject("Kenney_Mini_Male_A_ModelView");
            GameObject modelInstance = PrefabUtility.InstantiatePrefab(sourceModel, root.transform) as GameObject;
            if (modelInstance == null)
                modelInstance = UnityEngine.Object.Instantiate(sourceModel, root.transform);

            modelInstance.name = "Model";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            Transform sockets = CreateSocketRoot(root.transform);
            Transform primary = CreateSocket(sockets, "Muzzle_Main", new Vector3(0f, 1.05f, 0.62f));
            Transform secondary = CreateSocket(sockets, "Muzzle_Offhand", new Vector3(0.32f, 1.03f, 0.55f));
            Transform cast = CreateSocket(sockets, "AimTarget", new Vector3(0f, 1.18f, 1.55f));
            CreateSocket(sockets, "Head", new Vector3(0f, 1.55f, 0f));
            CreateSocket(sockets, "Chest", new Vector3(0f, 1.05f, 0f));
            CreateSocket(sockets, "RightHand", new Vector3(0.28f, 0.95f, 0.28f));
            CreateSocket(sockets, "LeftHand", new Vector3(-0.28f, 0.95f, 0.28f));
            CreateSocket(sockets, "Weapon_Main", new Vector3(0.20f, 0.98f, 0.34f));
            CreateSocket(sockets, "Weapon_Offhand", new Vector3(-0.20f, 0.98f, 0.34f));
            CreateSocket(sockets, "HealthBarAnchor", new Vector3(0f, 2.05f, 0f));

            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(primary, secondary, cast);

            BrawlerHandPoseTargets handTargets = root.AddComponent<BrawlerHandPoseTargets>();
            handTargets.CreateFullGripAuthoringRig();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[KenneyMiniCharacterPilotUtility] Created pilot prefab at {PrefabPath}.");
        }

        private static Transform CreateSocketRoot(Transform parent)
        {
            GameObject root = new GameObject("Sockets");
            root.transform.SetParent(parent, false);
            return root.transform;
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
    }

    public sealed class KenneyMiniCharacterImportPostprocessor : AssetPostprocessor
    {
        private const string KenneyModelRoot =
            "Assets/_Game/Art/Brawlers/KenneyMiniCharacters/Models/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(KenneyModelRoot, StringComparison.Ordinal))
                return;

            if (assetImporter is not ModelImporter importer)
                return;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.External;
        }
    }
}
