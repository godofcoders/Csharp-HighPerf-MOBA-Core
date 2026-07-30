using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Deterministic fallback/presentation model builder for brawlers that do
    /// not yet have authored character meshes. Recipes are hand-authored by
    /// brawler identity, so a given brawler always produces the same silhouette.
    /// </summary>
    public static class ProceduralBrawlerModelFactory
    {
        public static bool TryCreate(
            BrawlerDefinition definition,
            Transform parent,
            BrawlerController owner,
            out GameObject instance)
        {
            instance = null;

            if (definition == null || parent == null)
                return false;

            if (IsBrawler(definition, "Colt"))
            {
                instance = BuildColt(parent, owner);
                return instance != null;
            }

            return false;
        }

        private static GameObject BuildColt(Transform parent, BrawlerController owner)
        {
            int layer = parent.gameObject.layer;
            Material skin = CreateMaterial("Procedural Colt Skin", new Color(1.00f, 0.67f, 0.48f, 1f), 0.20f);
            Material hair = CreateMaterial("Procedural Colt Hair", new Color(0.82f, 0.18f, 0.08f, 1f), 0.30f);
            Material jacket = CreateMaterial("Procedural Colt Jacket", new Color(0.07f, 0.23f, 0.86f, 1f), 0.35f);
            Material shirt = CreateMaterial("Procedural Colt Shirt", new Color(0.98f, 0.88f, 0.24f, 1f), 0.20f);
            Material pants = CreateMaterial("Procedural Colt Pants", new Color(0.08f, 0.11f, 0.22f, 1f), 0.25f);
            Material boots = CreateMaterial("Procedural Colt Boots", new Color(0.04f, 0.035f, 0.03f, 1f), 0.18f);
            Material metal = CreateMaterial("Procedural Colt Gunmetal", new Color(0.09f, 0.10f, 0.12f, 1f), 0.62f);
            Material barrel = CreateMaterial("Procedural Colt Barrel", new Color(0.72f, 0.77f, 0.84f, 1f), 0.72f);

            GameObject root = new GameObject("Procedural_Colt_Model");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = layer;

            Transform bodyRoot = new GameObject("BodyRig").transform;
            bodyRoot.SetParent(root.transform, false);
            bodyRoot.localPosition = Vector3.zero;
            bodyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bodyRoot.localScale = Vector3.one;
            bodyRoot.gameObject.layer = layer;

            CreatePart(bodyRoot, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.56f, 0f), new Vector3(0.52f, 0.18f, 0.34f), Quaternion.identity, pants, layer);
            Transform torso = CreatePart(bodyRoot, "Torso_Jacket", PrimitiveType.Capsule, new Vector3(0f, 0.93f, 0f), new Vector3(0.62f, 0.62f, 0.42f), Quaternion.identity, jacket, layer);
            CreatePart(bodyRoot, "Chest_Shirt", PrimitiveType.Cube, new Vector3(0f, 0.95f, -0.235f), new Vector3(0.32f, 0.46f, 0.045f), Quaternion.identity, shirt, layer);
            CreatePart(bodyRoot, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.29f, 0f), new Vector3(0.16f, 0.10f, 0.16f), Quaternion.identity, skin, layer);
            Transform head = CreatePart(bodyRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.52f, -0.01f), new Vector3(0.42f, 0.46f, 0.38f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Hair_Cap", PrimitiveType.Sphere, new Vector3(0f, 1.67f, -0.03f), new Vector3(0.45f, 0.21f, 0.40f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Hair_Quiff", PrimitiveType.Cube, new Vector3(0.10f, 1.72f, -0.23f), new Vector3(0.28f, 0.18f, 0.24f), Quaternion.Euler(0f, 0f, -18f), hair, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Leg", PrimitiveType.Capsule, new Vector3(-0.17f, 0.30f, 0.02f), new Vector3(0.18f, 0.46f, 0.18f), Quaternion.identity, pants, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Leg", PrimitiveType.Capsule, new Vector3(0.17f, 0.30f, 0.02f), new Vector3(0.18f, 0.46f, 0.18f), Quaternion.identity, pants, layer);
            CreatePart(bodyRoot, "Left_Boot", PrimitiveType.Cube, new Vector3(-0.17f, 0.06f, -0.06f), new Vector3(0.22f, 0.11f, 0.34f), Quaternion.identity, boots, layer);
            CreatePart(bodyRoot, "Right_Boot", PrimitiveType.Cube, new Vector3(0.17f, 0.06f, -0.06f), new Vector3(0.22f, 0.11f, 0.34f), Quaternion.identity, boots, layer);

            Transform leftArm = CreatePart(bodyRoot, "Left_Arm", PrimitiveType.Capsule, new Vector3(-0.48f, 0.93f, -0.12f), new Vector3(0.16f, 0.48f, 0.16f), Quaternion.Euler(68f, 0f, -20f), skin, layer);
            Transform rightArm = CreatePart(bodyRoot, "Right_Arm", PrimitiveType.Capsule, new Vector3(0.48f, 0.93f, -0.12f), new Vector3(0.16f, 0.48f, 0.16f), Quaternion.Euler(68f, 0f, 20f), skin, layer);
            CreatePart(bodyRoot, "Left_Shoulder", PrimitiveType.Sphere, new Vector3(-0.34f, 1.10f, -0.02f), new Vector3(0.22f, 0.22f, 0.20f), Quaternion.identity, jacket, layer);
            CreatePart(bodyRoot, "Right_Shoulder", PrimitiveType.Sphere, new Vector3(0.34f, 1.10f, -0.02f), new Vector3(0.22f, 0.22f, 0.20f), Quaternion.identity, jacket, layer);

            Transform leftWeapon = CreatePistol(bodyRoot, "Left_Pistol", new Vector3(-0.45f, 0.78f, -0.50f), -7f, metal, barrel, layer, out Transform leftMuzzle);
            Transform rightWeapon = CreatePistol(bodyRoot, "Right_Pistol", new Vector3(0.45f, 0.78f, -0.50f), 7f, metal, barrel, layer, out Transform rightMuzzle);

            Transform castPoint = CreateAnchor(bodyRoot, "CastPoint", new Vector3(0f, 1.02f, -0.55f), layer);
            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(rightMuzzle, leftMuzzle, castPoint);

            BrawlerProceduralModelAnimator animator = root.AddComponent<BrawlerProceduralModelAnimator>();
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftWeapon, rightWeapon);

            return root;
        }

        private static Transform CreatePistol(
            Transform parent,
            string name,
            Vector3 localPosition,
            float yawDegrees,
            Material gripMaterial,
            Material barrelMaterial,
            int layer,
            out Transform muzzle)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            root.localScale = Vector3.one;
            root.gameObject.layer = layer;

            CreatePart(root, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.10f, 0.06f), new Vector3(0.12f, 0.25f, 0.10f), Quaternion.Euler(-18f, 0f, 0f), gripMaterial, layer);
            CreatePart(root, "Body", PrimitiveType.Cube, new Vector3(0f, 0.02f, -0.08f), new Vector3(0.16f, 0.14f, 0.26f), Quaternion.identity, gripMaterial, layer);
            CreatePart(root, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, -0.31f), new Vector3(0.055f, 0.25f, 0.055f), Quaternion.Euler(90f, 0f, 0f), barrelMaterial, layer);
            muzzle = CreateAnchor(root, "Muzzle", new Vector3(0f, 0.04f, -0.58f), layer);
            return root;
        }

        private static Transform CreateAnchor(
            Transform parent,
            string name,
            Vector3 localPosition,
            int layer)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            anchor.transform.localRotation = Quaternion.identity;
            anchor.transform.localScale = Vector3.one;
            anchor.layer = layer;
            return anchor.transform;
        }

        private static Transform CreatePart(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material,
            int layer)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            part.layer = layer;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                DestroyGeneratedObject(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            return part.transform;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard") ??
                Shader.Find("Diffuse");

            Material material = new Material(shader)
            {
                name = name,
                color = color,
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));

            return material;
        }

        private static bool IsBrawler(BrawlerDefinition definition, string brawlerName)
        {
            string assetName = definition.name;
            string displayName = definition.BrawlerName;
            return MatchesName(assetName, brawlerName) || MatchesName(displayName, brawlerName);
        }

        private static bool MatchesName(string candidate, string brawlerName)
        {
            return !string.IsNullOrWhiteSpace(candidate) &&
                   candidate.ToLowerInvariant().Contains(brawlerName.ToLowerInvariant());
        }

        private static void DestroyGeneratedObject(Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}
