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

            if (IsBrawler(definition, "Jessie") || IsBrawler(definition, "Jesse"))
            {
                instance = BuildJessie(parent, owner);
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

            Transform leftArm = CreateRiggedPart(bodyRoot, "Left_Arm", PrimitiveType.Capsule, new Vector3(-0.48f, 0.93f, -0.12f), new Vector3(0.16f, 0.48f, 0.16f), Quaternion.Euler(68f, 0f, -20f), skin, layer);
            Transform rightArm = CreateRiggedPart(bodyRoot, "Right_Arm", PrimitiveType.Capsule, new Vector3(0.48f, 0.93f, -0.12f), new Vector3(0.16f, 0.48f, 0.16f), Quaternion.Euler(68f, 0f, 20f), skin, layer);
            CreatePart(bodyRoot, "Left_Shoulder", PrimitiveType.Sphere, new Vector3(-0.34f, 1.10f, -0.02f), new Vector3(0.22f, 0.22f, 0.20f), Quaternion.identity, jacket, layer);
            CreatePart(bodyRoot, "Right_Shoulder", PrimitiveType.Sphere, new Vector3(0.34f, 1.10f, -0.02f), new Vector3(0.22f, 0.22f, 0.20f), Quaternion.identity, jacket, layer);

            Transform leftWeapon = CreatePistol(bodyRoot, "Left_Pistol", new Vector3(-0.45f, 0.78f, -0.50f), -7f, metal, barrel, layer, out Transform leftMuzzle);
            Transform rightWeapon = CreatePistol(bodyRoot, "Right_Pistol", new Vector3(0.45f, 0.78f, -0.50f), 7f, metal, barrel, layer, out Transform rightMuzzle);
            AttachKeepingWorld(leftWeapon, leftArm);
            AttachKeepingWorld(rightWeapon, rightArm);

            Transform castPoint = CreateAnchor(bodyRoot, "CastPoint", new Vector3(0f, 1.02f, -0.55f), layer);
            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(rightMuzzle, leftMuzzle, castPoint);

            BrawlerProceduralModelAnimator animator = root.AddComponent<BrawlerProceduralModelAnimator>();
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftWeapon, rightWeapon);

            return root;
        }

        private static GameObject BuildJessie(Transform parent, BrawlerController owner)
        {
            int layer = parent.gameObject.layer;
            Material skin = CreateMaterial("Procedural Jessie Skin", new Color(1.00f, 0.66f, 0.46f, 1f), 0.18f);
            Material hair = CreateMaterial("Procedural Jessie Hair", new Color(0.88f, 0.24f, 0.12f, 1f), 0.26f);
            Material cap = CreateMaterial("Procedural Jessie Cap", new Color(0.98f, 0.77f, 0.16f, 1f), 0.30f);
            Material shirt = CreateMaterial("Procedural Jessie Shirt", new Color(0.95f, 0.24f, 0.17f, 1f), 0.24f);
            Material overalls = CreateMaterial("Procedural Jessie Overalls", new Color(0.08f, 0.27f, 0.92f, 1f), 0.34f);
            Material boots = CreateMaterial("Procedural Jessie Boots", new Color(0.10f, 0.06f, 0.04f, 1f), 0.18f);
            Material metal = CreateMaterial("Procedural Jessie Blaster Metal", new Color(0.20f, 0.22f, 0.27f, 1f), 0.55f);
            Material energy = CreateMaterial("Procedural Jessie Blaster Energy", new Color(0.16f, 0.88f, 1.00f, 1f), 0.42f);

            GameObject root = new GameObject("Procedural_Jessie_Model");
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

            CreatePart(bodyRoot, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.56f, 0f), new Vector3(0.48f, 0.18f, 0.32f), Quaternion.identity, overalls, layer);
            Transform torso = CreatePart(bodyRoot, "Torso_Overalls", PrimitiveType.Capsule, new Vector3(0f, 0.91f, 0f), new Vector3(0.56f, 0.58f, 0.40f), Quaternion.identity, overalls, layer);
            CreatePart(bodyRoot, "Chest_Shirt", PrimitiveType.Cube, new Vector3(0f, 0.96f, -0.23f), new Vector3(0.34f, 0.34f, 0.045f), Quaternion.identity, shirt, layer);
            CreatePart(bodyRoot, "Overall_Strap_L", PrimitiveType.Cube, new Vector3(-0.12f, 1.02f, -0.265f), new Vector3(0.065f, 0.36f, 0.04f), Quaternion.Euler(0f, 0f, -8f), overalls, layer);
            CreatePart(bodyRoot, "Overall_Strap_R", PrimitiveType.Cube, new Vector3(0.12f, 1.02f, -0.265f), new Vector3(0.065f, 0.36f, 0.04f), Quaternion.Euler(0f, 0f, 8f), overalls, layer);
            CreatePart(bodyRoot, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.27f, 0f), new Vector3(0.15f, 0.09f, 0.15f), Quaternion.identity, skin, layer);
            Transform head = CreatePart(bodyRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.49f, -0.01f), new Vector3(0.40f, 0.43f, 0.37f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Hair_Back", PrimitiveType.Sphere, new Vector3(0f, 1.47f, 0.16f), new Vector3(0.44f, 0.34f, 0.28f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Cap_Crown", PrimitiveType.Sphere, new Vector3(0f, 1.64f, -0.02f), new Vector3(0.45f, 0.18f, 0.40f), Quaternion.identity, cap, layer);
            CreatePart(bodyRoot, "Cap_Brim", PrimitiveType.Cube, new Vector3(0f, 1.60f, -0.26f), new Vector3(0.38f, 0.055f, 0.23f), Quaternion.Euler(-6f, 0f, 0f), cap, layer);
            CreatePart(bodyRoot, "Side_Ponytail", PrimitiveType.Sphere, new Vector3(-0.34f, 1.38f, 0.04f), new Vector3(0.20f, 0.28f, 0.20f), Quaternion.identity, hair, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Leg", PrimitiveType.Capsule, new Vector3(-0.15f, 0.29f, 0.02f), new Vector3(0.17f, 0.44f, 0.17f), Quaternion.identity, overalls, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Leg", PrimitiveType.Capsule, new Vector3(0.15f, 0.29f, 0.02f), new Vector3(0.17f, 0.44f, 0.17f), Quaternion.identity, overalls, layer);
            CreatePart(bodyRoot, "Left_Boot", PrimitiveType.Cube, new Vector3(-0.15f, 0.06f, -0.06f), new Vector3(0.21f, 0.11f, 0.32f), Quaternion.identity, boots, layer);
            CreatePart(bodyRoot, "Right_Boot", PrimitiveType.Cube, new Vector3(0.15f, 0.06f, -0.06f), new Vector3(0.21f, 0.11f, 0.32f), Quaternion.identity, boots, layer);

            Transform leftArm = CreateRiggedPart(bodyRoot, "Left_Arm", PrimitiveType.Capsule, new Vector3(-0.43f, 0.94f, -0.08f), new Vector3(0.15f, 0.42f, 0.15f), Quaternion.Euler(44f, 0f, -18f), skin, layer);
            Transform rightArm = CreateRiggedPart(bodyRoot, "Right_Arm", PrimitiveType.Capsule, new Vector3(0.46f, 0.92f, -0.14f), new Vector3(0.15f, 0.45f, 0.15f), Quaternion.Euler(66f, 0f, 18f), skin, layer);
            CreatePart(bodyRoot, "Left_Shoulder", PrimitiveType.Sphere, new Vector3(-0.31f, 1.08f, -0.01f), new Vector3(0.20f, 0.20f, 0.18f), Quaternion.identity, shirt, layer);
            CreatePart(bodyRoot, "Right_Shoulder", PrimitiveType.Sphere, new Vector3(0.31f, 1.08f, -0.01f), new Vector3(0.20f, 0.20f, 0.18f), Quaternion.identity, shirt, layer);

            Transform blaster = CreateBlaster(bodyRoot, "Shock_Blaster", new Vector3(0.42f, 0.78f, -0.53f), 5f, metal, energy, layer, out Transform muzzle);
            AttachKeepingWorld(blaster, rightArm);

            Transform castPoint = CreateAnchor(bodyRoot, "CastPoint", new Vector3(0f, 1.03f, -0.52f), layer);
            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(muzzle, muzzle, castPoint);

            BrawlerProceduralModelAnimator animator = root.AddComponent<BrawlerProceduralModelAnimator>();
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, null, blaster);

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

        private static Transform CreateBlaster(
            Transform parent,
            string name,
            Vector3 localPosition,
            float yawDegrees,
            Material bodyMaterial,
            Material energyMaterial,
            int layer,
            out Transform muzzle)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            root.localScale = Vector3.one;
            root.gameObject.layer = layer;

            CreatePart(root, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.11f, 0.06f), new Vector3(0.12f, 0.25f, 0.10f), Quaternion.Euler(-16f, 0f, 0f), bodyMaterial, layer);
            CreatePart(root, "Body", PrimitiveType.Cube, new Vector3(0f, 0.02f, -0.10f), new Vector3(0.22f, 0.18f, 0.30f), Quaternion.identity, bodyMaterial, layer);
            CreatePart(root, "Core", PrimitiveType.Sphere, new Vector3(0f, 0.04f, -0.22f), new Vector3(0.15f, 0.15f, 0.10f), Quaternion.identity, energyMaterial, layer);
            CreatePart(root, "Nozzle", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, -0.39f), new Vector3(0.075f, 0.20f, 0.075f), Quaternion.Euler(90f, 0f, 0f), energyMaterial, layer);
            muzzle = CreateAnchor(root, "Muzzle", new Vector3(0f, 0.04f, -0.62f), layer);
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

        private static Transform CreateRiggedPart(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPosition,
            Vector3 visualScale,
            Quaternion localRotation,
            Material material,
            int layer)
        {
            GameObject rig = new GameObject(name);
            rig.transform.SetParent(parent, false);
            rig.transform.localPosition = localPosition;
            rig.transform.localRotation = localRotation;
            rig.transform.localScale = Vector3.one;
            rig.layer = layer;

            CreatePart(rig.transform, name + "_Mesh", type, Vector3.zero, visualScale, Quaternion.identity, material, layer);
            return rig.transform;
        }

        private static void AttachKeepingWorld(Transform child, Transform parent)
        {
            if (child == null || parent == null)
                return;

            child.SetParent(parent, true);
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
