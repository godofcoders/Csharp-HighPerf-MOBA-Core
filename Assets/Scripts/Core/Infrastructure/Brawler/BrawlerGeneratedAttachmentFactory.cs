using System.Collections.Generic;
using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class BrawlerGeneratedAttachmentFactory
    {
        private static readonly Dictionary<string, Material> MaterialCache =
            new Dictionary<string, Material>(16);

        public static bool TryCreate(
            BrawlerGeneratedAttachmentType type,
            string id,
            out GameObject attachment)
        {
            attachment = null;
            switch (type)
            {
                case BrawlerGeneratedAttachmentType.Pistol:
                    attachment = CreatePistol(id);
                    return true;
                case BrawlerGeneratedAttachmentType.Bottle:
                    attachment = CreateBottle(id);
                    return true;
                case BrawlerGeneratedAttachmentType.Staff:
                    attachment = CreateStaff(id);
                    return true;
                case BrawlerGeneratedAttachmentType.ShockGun:
                    attachment = CreateShockGun(id);
                    return true;
                case BrawlerGeneratedAttachmentType.Bow:
                    attachment = CreateBow(id);
                    return true;
                case BrawlerGeneratedAttachmentType.NinjaStars:
                    attachment = CreateNinjaStars(id);
                    return true;
                case BrawlerGeneratedAttachmentType.Umbrella:
                    attachment = CreateUmbrella(id);
                    return true;
                default:
                    return false;
            }
        }

        private static GameObject CreatePistol(string id)
        {
            Material gunmetal = CreateMaterial("Generated Pistol Gunmetal", new Color(0.08f, 0.09f, 0.12f, 1f));
            Material barrel = CreateMaterial("Generated Pistol Barrel", new Color(0.70f, 0.75f, 0.82f, 1f));
            Material grip = CreateMaterial("Generated Pistol Grip", new Color(0.20f, 0.12f, 0.07f, 1f));
            Material accent = CreateMaterial("Generated Pistol Accent", new Color(1.00f, 0.74f, 0.18f, 1f));

            GameObject root = CreateRoot(id, "Generated_Pistol");
            AddPart(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0f, 0.10f), new Vector3(0.18f, 0.12f, 0.34f), Quaternion.identity, gunmetal);
            AddPart(root.transform, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.36f), new Vector3(0.055f, 0.30f, 0.055f), Quaternion.Euler(90f, 0f, 0f), barrel);
            AddPart(root.transform, "Muzzle", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.54f), new Vector3(0.07f, 0.04f, 0.07f), Quaternion.Euler(90f, 0f, 0f), gunmetal);
            AddPart(root.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.17f, -0.04f), new Vector3(0.12f, 0.28f, 0.10f), Quaternion.Euler(-18f, 0f, 0f), grip);
            AddPart(root.transform, "Sight", PrimitiveType.Cube, new Vector3(0f, 0.09f, 0.20f), new Vector3(0.07f, 0.025f, 0.12f), Quaternion.identity, accent);
            return root;
        }

        private static GameObject CreateBottle(string id)
        {
            Material glass = CreateMaterial("Generated Bottle Glass", new Color(0.10f, 0.63f, 0.36f, 1f));
            Material liquid = CreateMaterial("Generated Bottle Liquid", new Color(0.86f, 0.16f, 0.72f, 1f));
            Material cork = CreateMaterial("Generated Bottle Cork", new Color(0.66f, 0.38f, 0.16f, 1f));
            Material label = CreateMaterial("Generated Bottle Label", new Color(0.96f, 0.86f, 0.52f, 1f));

            GameObject root = CreateRoot(id, "Generated_Bottle");
            AddPart(root.transform, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.36f, 0.13f), Quaternion.identity, glass);
            AddPart(root.transform, "Liquid", PrimitiveType.Cylinder, new Vector3(0f, -0.07f, 0f), new Vector3(0.115f, 0.20f, 0.115f), Quaternion.identity, liquid);
            AddPart(root.transform, "Label", PrimitiveType.Cube, new Vector3(0f, -0.03f, -0.125f), new Vector3(0.18f, 0.12f, 0.018f), Quaternion.identity, label);
            AddPart(root.transform, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 0f), new Vector3(0.07f, 0.20f, 0.07f), Quaternion.identity, glass);
            AddPart(root.transform, "Cork", PrimitiveType.Cylinder, new Vector3(0f, 0.43f, 0f), new Vector3(0.075f, 0.08f, 0.075f), Quaternion.identity, cork);
            return root;
        }

        private static GameObject CreateStaff(string id)
        {
            Material wood = CreateMaterial("Generated Staff Wood", new Color(0.48f, 0.14f, 0.08f, 1f));
            Material metal = CreateMaterial("Generated Staff Metal", new Color(0.76f, 0.80f, 0.88f, 1f));
            Material vial = CreateMaterial("Generated Staff Vial", new Color(0.88f, 0.13f, 0.66f, 1f));
            Material cap = CreateMaterial("Generated Staff Cap", new Color(0.10f, 0.42f, 0.36f, 1f));

            GameObject root = CreateRoot(id, "Generated_Staff");
            AddPart(root.transform, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.055f, 0.82f, 0.055f), Quaternion.identity, wood);
            AddPart(root.transform, "BottomCap", PrimitiveType.Sphere, new Vector3(0f, -0.45f, 0f), new Vector3(0.11f, 0.11f, 0.11f), Quaternion.identity, metal);
            AddPart(root.transform, "TopRing", PrimitiveType.Cylinder, new Vector3(0f, 0.48f, 0f), new Vector3(0.12f, 0.045f, 0.12f), Quaternion.identity, metal);
            AddPart(root.transform, "PotionBulb", PrimitiveType.Sphere, new Vector3(0f, 0.64f, 0f), new Vector3(0.16f, 0.18f, 0.16f), Quaternion.identity, vial);
            AddPart(root.transform, "Stopper", PrimitiveType.Cylinder, new Vector3(0f, 0.78f, 0f), new Vector3(0.075f, 0.075f, 0.075f), Quaternion.identity, cap);
            return root;
        }

        private static GameObject CreateShockGun(string id)
        {
            Material frame = CreateMaterial("Generated Shock Gun Frame", new Color(0.16f, 0.20f, 0.26f, 1f));
            Material plate = CreateMaterial("Generated Shock Gun Plate", new Color(0.38f, 0.58f, 0.92f, 1f));
            Material coil = CreateMaterial("Generated Shock Gun Coil", new Color(0.15f, 0.88f, 1.00f, 1f));
            Material grip = CreateMaterial("Generated Shock Gun Grip", new Color(0.42f, 0.22f, 0.10f, 1f));

            GameObject root = CreateRoot(id, "Generated_ShockGun");
            AddPart(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0f, 0.10f), new Vector3(0.24f, 0.22f, 0.34f), Quaternion.identity, frame);
            AddPart(root.transform, "SidePlate", PrimitiveType.Cube, new Vector3(0f, 0.01f, -0.085f), new Vector3(0.28f, 0.16f, 0.035f), Quaternion.identity, plate);
            AddPart(root.transform, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.42f), new Vector3(0.085f, 0.34f, 0.085f), Quaternion.Euler(90f, 0f, 0f), frame);
            AddPart(root.transform, "CoilA", PrimitiveType.Sphere, new Vector3(-0.10f, 0.12f, 0.26f), new Vector3(0.07f, 0.07f, 0.07f), Quaternion.identity, coil);
            AddPart(root.transform, "CoilB", PrimitiveType.Sphere, new Vector3(0.10f, 0.12f, 0.26f), new Vector3(0.07f, 0.07f, 0.07f), Quaternion.identity, coil);
            AddPart(root.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.20f, -0.03f), new Vector3(0.13f, 0.30f, 0.12f), Quaternion.Euler(-14f, 0f, 0f), grip);
            return root;
        }

        private static GameObject CreateBow(string id)
        {
            Material wood = CreateMaterial("Generated Bow Wood", new Color(0.58f, 0.27f, 0.11f, 1f));
            Material wrap = CreateMaterial("Generated Bow Grip", new Color(0.10f, 0.54f, 0.75f, 1f));
            Material stringMaterial = CreateMaterial("Generated Bow String", new Color(0.92f, 0.90f, 0.82f, 1f));
            Material arrow = CreateMaterial("Generated Bow Arrow", new Color(0.95f, 0.90f, 0.62f, 1f));

            GameObject root = CreateRoot(id, "Generated_Bow");
            AddPart(root.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.10f, 0.24f, 0.08f), Quaternion.identity, wrap);
            AddPart(root.transform, "UpperLimb", PrimitiveType.Cube, new Vector3(0f, 0.34f, 0.02f), new Vector3(0.075f, 0.48f, 0.07f), Quaternion.Euler(0f, 0f, -13f), wood);
            AddPart(root.transform, "LowerLimb", PrimitiveType.Cube, new Vector3(0f, -0.34f, 0.02f), new Vector3(0.075f, 0.48f, 0.07f), Quaternion.Euler(0f, 0f, 13f), wood);
            AddPart(root.transform, "String", PrimitiveType.Cube, new Vector3(-0.17f, 0f, -0.02f), new Vector3(0.025f, 0.92f, 0.018f), Quaternion.identity, stringMaterial);
            AddPart(root.transform, "ReadyArrow", PrimitiveType.Cylinder, new Vector3(0.02f, 0f, 0.25f), new Vector3(0.025f, 0.48f, 0.025f), Quaternion.Euler(90f, 0f, 0f), arrow);
            AddPart(root.transform, "ArrowTip", PrimitiveType.Cylinder, new Vector3(0.02f, 0f, 0.57f), new Vector3(0.06f, 0.09f, 0.06f), Quaternion.Euler(90f, 0f, 0f), stringMaterial);
            return root;
        }

        private static GameObject CreateNinjaStars(string id)
        {
            Material metal = CreateMaterial("Generated Ninja Star Metal", new Color(0.70f, 0.75f, 0.82f, 1f));
            Material edge = CreateMaterial("Generated Ninja Star Edge", new Color(0.95f, 0.12f, 0.14f, 1f));

            GameObject root = CreateRoot(id, "Generated_NinjaStars");
            CreateStar(root.transform, "StarA", new Vector3(-0.08f, 0.02f, 0f), 0f, metal, edge);
            CreateStar(root.transform, "StarB", new Vector3(0.08f, 0f, 0.02f), 22f, metal, edge);
            CreateStar(root.transform, "StarC", new Vector3(0.00f, -0.09f, -0.02f), -18f, metal, edge);
            return root;
        }

        private static GameObject CreateUmbrella(string id)
        {
            Material handle = CreateMaterial("Generated Umbrella Handle", new Color(0.35f, 0.18f, 0.10f, 1f));
            Material canopy = CreateMaterial("Generated Umbrella Canopy", new Color(0.98f, 0.36f, 0.66f, 1f));
            Material rim = CreateMaterial("Generated Umbrella Rim", new Color(0.20f, 0.38f, 0.92f, 1f));
            Material tip = CreateMaterial("Generated Umbrella Tip", new Color(0.92f, 0.82f, 0.46f, 1f));

            GameObject root = CreateRoot(id, "Generated_Umbrella");
            AddPart(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, -0.20f, 0f), new Vector3(0.04f, 0.58f, 0.04f), Quaternion.identity, handle);
            AddPart(root.transform, "Hook", PrimitiveType.Cylinder, new Vector3(0.08f, -0.56f, 0f), new Vector3(0.06f, 0.12f, 0.06f), Quaternion.Euler(0f, 0f, 90f), handle);
            AddPart(root.transform, "Canopy", PrimitiveType.Sphere, new Vector3(0f, 0.25f, 0f), new Vector3(0.52f, 0.18f, 0.52f), Quaternion.identity, canopy);
            AddPart(root.transform, "Rim", PrimitiveType.Cylinder, new Vector3(0f, 0.17f, 0f), new Vector3(0.54f, 0.035f, 0.54f), Quaternion.identity, rim);
            AddPart(root.transform, "Tip", PrimitiveType.Cylinder, new Vector3(0f, 0.43f, 0f), new Vector3(0.08f, 0.14f, 0.08f), Quaternion.identity, tip);
            AddPart(root.transform, "RibA", PrimitiveType.Cube, new Vector3(0f, 0.23f, 0.24f), new Vector3(0.025f, 0.025f, 0.44f), Quaternion.identity, rim);
            AddPart(root.transform, "RibB", PrimitiveType.Cube, new Vector3(0.24f, 0.23f, 0f), new Vector3(0.44f, 0.025f, 0.025f), Quaternion.identity, rim);
            return root;
        }

        private static void CreateStar(
            Transform parent,
            string name,
            Vector3 localPosition,
            float localRoll,
            Material metal,
            Material edge)
        {
            Transform starRoot = new GameObject(name).transform;
            starRoot.SetParent(parent, false);
            starRoot.localPosition = localPosition;
            starRoot.localRotation = Quaternion.Euler(0f, 0f, localRoll);
            starRoot.localScale = Vector3.one;

            AddPart(starRoot, "Core", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.015f, 0.07f), Quaternion.Euler(90f, 0f, 0f), metal);
            AddPart(starRoot, "BladeTop", PrimitiveType.Cube, new Vector3(0f, 0.09f, 0f), new Vector3(0.04f, 0.16f, 0.015f), Quaternion.Euler(0f, 0f, 45f), edge);
            AddPart(starRoot, "BladeBottom", PrimitiveType.Cube, new Vector3(0f, -0.09f, 0f), new Vector3(0.04f, 0.16f, 0.015f), Quaternion.Euler(0f, 0f, 45f), edge);
            AddPart(starRoot, "BladeLeft", PrimitiveType.Cube, new Vector3(-0.09f, 0f, 0f), new Vector3(0.16f, 0.04f, 0.015f), Quaternion.Euler(0f, 0f, 45f), edge);
            AddPart(starRoot, "BladeRight", PrimitiveType.Cube, new Vector3(0.09f, 0f, 0f), new Vector3(0.16f, 0.04f, 0.015f), Quaternion.Euler(0f, 0f, 45f), edge);
        }

        private static GameObject CreateRoot(string id, string fallbackName)
        {
            string cleanId = string.IsNullOrWhiteSpace(id) ? fallbackName : id.Trim().Replace(' ', '_');
            GameObject root = new GameObject(cleanId);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static Transform AddPart(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            PrimitiveType resolvedType = ResolveSupportedPrimitive(primitiveType);
            GameObject part = GameObject.CreatePrimitive(resolvedType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            return part.transform;
        }

        private static PrimitiveType ResolveSupportedPrimitive(PrimitiveType primitiveType)
        {
            if (primitiveType == PrimitiveType.Cylinder ||
                primitiveType == PrimitiveType.Cube ||
                primitiveType == PrimitiveType.Sphere ||
                primitiveType == PrimitiveType.Capsule ||
                primitiveType == PrimitiveType.Quad)
            {
                return primitiveType;
            }

            return PrimitiveType.Cylinder;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            if (MaterialCache.TryGetValue(name, out Material cached) && cached != null)
                return cached;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Unlit/Color");

            Material material = new Material(shader);
            material.name = name;
            material.hideFlags = HideFlags.DontSave;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            MaterialCache[name] = material;
            return material;
        }
    }
}
