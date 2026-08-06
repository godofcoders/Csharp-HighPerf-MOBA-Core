using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class BrawlerVisualModelFactory
    {
        private const float DefaultTargetHeight = 1.72f;
        private const float GroundLocalY = 0f;

        public static bool TryCreate(
            BrawlerDefinition definition,
            Transform parent,
            BrawlerController owner,
            out GameObject instance)
        {
            instance = null;

            if (definition == null || parent == null)
                return false;

            if (definition.ModelPrefab != null)
            {
                instance = Object.Instantiate(definition.ModelPrefab, parent);
                instance.name = ResolveInstanceName(definition, "Authored");
                if (PrepareAuthoredModel(instance, parent, definition, owner))
                    return true;

                Debug.LogWarning(
                    $"[BrawlerVisualModelFactory] Authored model '{definition.ModelPrefab.name}' for '{ResolveBrawlerName(definition)}' has no usable body renderers. Falling back to the procedural model. Check that Git LFS model files are downloaded.");
                DestroyRuntimeObject(instance);
                instance = null;
            }

            if (!ProceduralBrawlerModelFactory.TryCreate(definition, parent, owner, out instance))
                return false;

            ConfigureLayer(instance, parent.gameObject.layer);
            PrepareAttachmentPresentation(instance, definition, owner, installAttachmentProfile: false);
            return true;
        }

        private static bool PrepareAuthoredModel(
            GameObject instance,
            Transform parent,
            BrawlerDefinition definition,
            BrawlerController owner)
        {
            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(parent, false);
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;

            ConfigureLayer(instance, parent.gameObject.layer);
            RemovePhysicsComponents(instance);
            AlignModelForward(instance);

            if (!HasUsableRendererBounds(instance))
                return false;

            NormalizeScaleAndGrounding(instance, parent, definition);
            PrepareAttachmentPresentation(instance, definition, owner, installAttachmentProfile: true);
            return true;
        }

        private static void AlignModelForward(GameObject instance)
        {
            Transform model = FindChildRecursive(instance.transform, "Model");
            if (model == null)
                return;

            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static void NormalizeScaleAndGrounding(
            GameObject instance,
            Transform parent,
            BrawlerDefinition definition)
        {
            Bounds bounds;
            if (!TryCalculateRendererBounds(instance, out bounds))
                return;

            float height = Mathf.Max(0.001f, bounds.size.y);
            float targetHeight = ResolveTargetHeight(definition);
            float scale = Mathf.Clamp(targetHeight / height, 0.01f, 50f);
            instance.transform.localScale = Vector3.one * scale;

            if (!TryCalculateRendererBounds(instance, out bounds))
                return;

            float localBottom = parent.InverseTransformPoint(bounds.min).y;
            Vector3 localPosition = instance.transform.localPosition;
            localPosition.y += GroundLocalY - localBottom;
            instance.transform.localPosition = localPosition;
        }

        private static float ResolveTargetHeight(BrawlerDefinition definition)
        {
            string name = ResolveBrawlerName(definition);

            if (Matches(name, "El Primo") || Matches(name, "ElPrimo"))
                return 1.88f;

            if (Matches(name, "Jessie") || Matches(name, "Jesse") || Matches(name, "Leon") || Matches(name, "Barley"))
                return 1.56f;

            if (Matches(name, "Bo") || Matches(name, "Byron") || Matches(name, "Piper"))
                return 1.70f;

            if (Matches(name, "Colt"))
                return 1.66f;

            return DefaultTargetHeight;
        }

        private static void PrepareAttachmentPresentation(
            GameObject instance,
            BrawlerDefinition definition,
            BrawlerController owner,
            bool installAttachmentProfile)
        {
            BrawlerAnimationRuntime.Ensure(instance, owner);
            BrawlerAuthoredModelAnimator.Ensure(instance, owner, definition);

            BrawlerAttachmentRig rig = BrawlerAttachmentRig.Ensure(instance);
            if (rig != null)
                rig.AutoBindFromModel(instance);

            EnsurePresentationAnchors(instance, rig);

            if (!installAttachmentProfile)
                return;

            BrawlerAttachmentInstaller installer = BrawlerAttachmentInstaller.Ensure(instance);
            if (installer != null)
                installer.Bind(definition, rig);
        }

        private static void EnsurePresentationAnchors(
            GameObject instance,
            BrawlerAttachmentRig rig)
        {
            BrawlerPresentationAnchors anchors =
                instance.GetComponentInChildren<BrawlerPresentationAnchors>(true);
            if (anchors == null)
                anchors = instance.AddComponent<BrawlerPresentationAnchors>();

            Transform primary = rig != null
                ? rig.ResolveSocket(BrawlerAttachmentSocket.PrimaryMuzzle, instance.transform)
                : FindFirst(
                    instance.transform,
                    "Muzzle_Main",
                    "PrimaryFirePoint",
                    "RightHand",
                    "Weapon_Main");
            Transform secondary = rig != null
                ? rig.ResolveSocket(BrawlerAttachmentSocket.SecondaryMuzzle, primary)
                : FindFirst(
                    instance.transform,
                    "Muzzle_Offhand",
                    "SecondaryFirePoint",
                    "LeftHand",
                    "Weapon_Offhand");
            Transform cast = rig != null
                ? rig.ResolveSocket(BrawlerAttachmentSocket.CastPoint, primary)
                : FindFirst(
                    instance.transform,
                    "AimTarget",
                    "CastPoint",
                    "Weapon_Main",
                    "Muzzle_Main");

            anchors.Configure(
                primary != null ? primary : instance.transform,
                secondary != null ? secondary : primary,
                cast != null ? cast : primary);
        }

        private static bool TryCalculateRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static bool HasUsableRendererBounds(GameObject root)
        {
            if (!TryCalculateRendererBounds(root, out Bounds bounds))
                return false;

            return bounds.size.sqrMagnitude > 0.0001f;
        }

        private static void DestroyRuntimeObject(GameObject instance)
        {
            if (instance == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(instance);
            else
                Object.DestroyImmediate(instance);
        }

        private static void RemovePhysicsComponents(GameObject instance)
        {
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                Object.Destroy(colliders[i]);
            }

            Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
                Object.Destroy(bodies[i]);
        }

        private static void ConfigureLayer(GameObject root, int layer)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                transforms[i].gameObject.layer = layer;
        }

        private static Transform FindFirst(Transform root, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = FindChildRecursive(root, names[i]);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool Matches(string value, string expected)
        {
            return string.Equals(value, expected, System.StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveBrawlerName(BrawlerDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(definition.BrawlerName)
                ? definition.BrawlerName
                : definition.name;
        }

        private static string ResolveInstanceName(BrawlerDefinition definition, string source)
        {
            string brawlerName = ResolveBrawlerName(definition);
            if (string.IsNullOrEmpty(brawlerName))
                brawlerName = "Brawler";

            return $"{brawlerName}_{source}_Visual";
        }
    }
}
