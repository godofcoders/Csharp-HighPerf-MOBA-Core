using System.Collections.Generic;
using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class BrawlerGeneratedAttachmentFactory
    {
        public const string GripPointName = "GripPoint";
        public const string HoldPointName = "HoldPoint";

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
            Material black = CreateMaterial("Generated Pistol Black", new Color(0.035f, 0.04f, 0.055f, 1f));
            Material slide = CreateMaterial("Generated Pistol Silver Slide", new Color(0.76f, 0.78f, 0.82f, 1f));
            Material barrel = CreateMaterial("Generated Pistol Barrel", new Color(0.22f, 0.24f, 0.28f, 1f));
            Material grip = CreateMaterial("Generated Pistol Textured Grip", new Color(0.06f, 0.055f, 0.06f, 1f));
            Material screw = CreateMaterial("Generated Pistol Screw", new Color(0.90f, 0.91f, 0.94f, 1f));

            GameObject root = CreateRoot(id, "Generated_Pistol");
            AddPart(root.transform, "Slide", PrimitiveType.Cube, new Vector3(0f, 0.045f, 0.18f), new Vector3(0.22f, 0.12f, 0.46f), Quaternion.identity, slide);
            AddPart(root.transform, "LowerFrame", PrimitiveType.Cube, new Vector3(0f, -0.045f, 0.08f), new Vector3(0.18f, 0.08f, 0.32f), Quaternion.identity, black);
            AddPart(root.transform, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0.47f), new Vector3(0.045f, 0.24f, 0.045f), Quaternion.Euler(90f, 0f, 0f), barrel);
            AddPart(root.transform, "MuzzleRing", PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0.61f), new Vector3(0.065f, 0.035f, 0.065f), Quaternion.Euler(90f, 0f, 0f), black);
            AddPart(root.transform, "EjectionPort", PrimitiveType.Cube, new Vector3(0f, 0.116f, 0.23f), new Vector3(0.13f, 0.012f, 0.12f), Quaternion.identity, black);
            AddPart(root.transform, "RearSight", PrimitiveType.Cube, new Vector3(0f, 0.13f, -0.04f), new Vector3(0.12f, 0.035f, 0.045f), Quaternion.identity, black);
            AddPart(root.transform, "FrontSight", PrimitiveType.Cube, new Vector3(0f, 0.13f, 0.42f), new Vector3(0.07f, 0.032f, 0.035f), Quaternion.identity, black);
            AddPart(root.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.20f, -0.08f), new Vector3(0.14f, 0.32f, 0.12f), Quaternion.Euler(-18f, 0f, 0f), grip);
            AddPart(root.transform, "GripPanel", PrimitiveType.Cube, new Vector3(0f, -0.20f, -0.145f), new Vector3(0.105f, 0.24f, 0.018f), Quaternion.Euler(-18f, 0f, 0f), black);
            AddPart(root.transform, "TriggerGuard", PrimitiveType.Cube, new Vector3(0f, -0.125f, 0.08f), new Vector3(0.15f, 0.04f, 0.11f), Quaternion.identity, black);
            AddPart(root.transform, "Trigger", PrimitiveType.Cube, new Vector3(0f, -0.15f, 0.08f), new Vector3(0.045f, 0.10f, 0.026f), Quaternion.Euler(0f, 0f, 12f), barrel);
            AddPart(root.transform, "GripScrewTop", PrimitiveType.Sphere, new Vector3(0f, -0.125f, -0.158f), new Vector3(0.028f, 0.028f, 0.010f), Quaternion.identity, screw);
            AddPart(root.transform, "GripScrewBottom", PrimitiveType.Sphere, new Vector3(0f, -0.270f, -0.125f), new Vector3(0.025f, 0.025f, 0.010f), Quaternion.identity, screw);
            AddGripPoint(root.transform, new Vector3(0f, -0.19f, -0.08f), Quaternion.identity);
            return root;
        }

        private static GameObject CreateBottle(string id)
        {
            Material glass = CreateMaterial("Generated Bottle Dark Glass", new Color(0.035f, 0.16f, 0.11f, 1f));
            Material neckGlass = CreateMaterial("Generated Bottle Neck Glass", new Color(0.06f, 0.24f, 0.15f, 1f));
            Material cap = CreateMaterial("Generated Bottle Black Cap", new Color(0.035f, 0.035f, 0.045f, 1f));
            Material label = CreateMaterial("Generated Bottle Aged Label", new Color(0.86f, 0.72f, 0.34f, 1f));
            Material ink = CreateMaterial("Generated Bottle Label Ink", new Color(0.25f, 0.08f, 0.05f, 1f));
            Material wire = CreateMaterial("Generated Bottle Wire", new Color(0.16f, 0.16f, 0.15f, 1f));
            Material rag = CreateMaterial("Generated Bottle Rag", new Color(0.45f, 0.43f, 0.38f, 1f));

            GameObject root = CreateRoot(id, "Generated_Bottle");
            AddPart(root.transform, "Body", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0f), new Vector3(0.16f, 0.42f, 0.16f), Quaternion.identity, glass);
            AddPart(root.transform, "Shoulder", PrimitiveType.Sphere, new Vector3(0f, 0.27f, 0f), new Vector3(0.16f, 0.10f, 0.16f), Quaternion.identity, glass);
            AddPart(root.transform, "BaseScuff", PrimitiveType.Cylinder, new Vector3(0f, -0.48f, 0f), new Vector3(0.165f, 0.055f, 0.165f), Quaternion.identity, wire);
            AddPart(root.transform, "MainLabel", PrimitiveType.Cube, new Vector3(0f, -0.10f, -0.155f), new Vector3(0.235f, 0.24f, 0.018f), Quaternion.identity, label);
            AddPart(root.transform, "LabelTitle", PrimitiveType.Cube, new Vector3(0f, -0.02f, -0.168f), new Vector3(0.18f, 0.030f, 0.010f), Quaternion.identity, ink);
            AddPart(root.transform, "LabelMark", PrimitiveType.Cube, new Vector3(0f, -0.145f, -0.168f), new Vector3(0.12f, 0.022f, 0.010f), Quaternion.identity, ink);
            AddPart(root.transform, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.39f, 0f), new Vector3(0.070f, 0.25f, 0.070f), Quaternion.identity, neckGlass);
            AddPart(root.transform, "NeckLabel", PrimitiveType.Cube, new Vector3(0f, 0.40f, -0.074f), new Vector3(0.12f, 0.115f, 0.014f), Quaternion.identity, label);
            AddPart(root.transform, "Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.58f, 0f), new Vector3(0.078f, 0.10f, 0.078f), Quaternion.identity, cap);
            AddPart(root.transform, "WireUpper", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.172f, 0.012f, 0.172f), Quaternion.identity, wire);
            AddPart(root.transform, "WireLower", PrimitiveType.Cylinder, new Vector3(0f, -0.28f, 0f), new Vector3(0.172f, 0.012f, 0.172f), Quaternion.identity, wire);
            AddPart(root.transform, "SideStickLeft", PrimitiveType.Cylinder, new Vector3(-0.18f, -0.02f, 0f), new Vector3(0.020f, 0.46f, 0.020f), Quaternion.identity, wire);
            AddPart(root.transform, "SideStickRight", PrimitiveType.Cylinder, new Vector3(0.18f, -0.02f, 0f), new Vector3(0.020f, 0.46f, 0.020f), Quaternion.identity, wire);
            AddPart(root.transform, "RagFoldA", PrimitiveType.Cube, new Vector3(0.10f, 0.60f, 0.045f), new Vector3(0.045f, 0.36f, 0.018f), Quaternion.Euler(0f, 0f, -22f), rag);
            AddPart(root.transform, "RagFoldB", PrimitiveType.Cube, new Vector3(0.17f, 0.49f, 0.035f), new Vector3(0.038f, 0.30f, 0.018f), Quaternion.Euler(0f, 0f, -12f), rag);
            AddGripPoint(root.transform, new Vector3(0f, 0.25f, 0f), Quaternion.identity);
            return root;
        }

        private static GameObject CreateStaff(string id)
        {
            Material wood = CreateMaterial("Generated Staff Polished Wood", new Color(0.45f, 0.18f, 0.07f, 1f));
            Material darkWood = CreateMaterial("Generated Staff Dark Carving", new Color(0.18f, 0.08f, 0.04f, 1f));
            Material metal = CreateMaterial("Generated Staff Aged Metal", new Color(0.46f, 0.37f, 0.25f, 1f));
            Material crystal = CreateMaterial("Generated Staff Purple Crystal", new Color(0.95f, 0.16f, 1.00f, 1f));
            Material rune = CreateMaterial("Generated Staff Rune Glow", new Color(0.86f, 0.08f, 1.00f, 1f));

            GameObject root = CreateRoot(id, "Generated_Staff");
            AddPart(root.transform, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0f), new Vector3(0.055f, 0.88f, 0.055f), Quaternion.identity, wood);
            AddPart(root.transform, "BottomCap", PrimitiveType.Cylinder, new Vector3(0f, -0.55f, 0f), new Vector3(0.09f, 0.075f, 0.09f), Quaternion.identity, metal);
            AddPart(root.transform, "BottomKnob", PrimitiveType.Sphere, new Vector3(0f, -0.62f, 0f), new Vector3(0.09f, 0.06f, 0.09f), Quaternion.identity, darkWood);
            AddPart(root.transform, "GripRingA", PrimitiveType.Cylinder, new Vector3(0f, -0.37f, 0f), new Vector3(0.075f, 0.018f, 0.075f), Quaternion.identity, metal);
            AddPart(root.transform, "GripRingB", PrimitiveType.Cylinder, new Vector3(0f, -0.28f, 0f), new Vector3(0.075f, 0.018f, 0.075f), Quaternion.identity, metal);
            AddPart(root.transform, "RuneA", PrimitiveType.Cube, new Vector3(0f, -0.08f, -0.057f), new Vector3(0.055f, 0.018f, 0.010f), Quaternion.Euler(0f, 0f, 45f), rune);
            AddPart(root.transform, "RuneB", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.057f), new Vector3(0.050f, 0.018f, 0.010f), Quaternion.Euler(0f, 0f, -45f), rune);
            AddPart(root.transform, "RuneC", PrimitiveType.Cube, new Vector3(0f, 0.16f, -0.057f), new Vector3(0.060f, 0.018f, 0.010f), Quaternion.Euler(0f, 0f, 45f), rune);
            AddPart(root.transform, "TopRing", PrimitiveType.Cylinder, new Vector3(0f, 0.44f, 0f), new Vector3(0.115f, 0.035f, 0.115f), Quaternion.identity, metal);
            AddPart(root.transform, "CrystalSocket", PrimitiveType.Sphere, new Vector3(0f, 0.53f, 0f), new Vector3(0.16f, 0.11f, 0.16f), Quaternion.identity, darkWood);
            AddPart(root.transform, "PetalLeft", PrimitiveType.Cube, new Vector3(-0.10f, 0.56f, 0f), new Vector3(0.06f, 0.19f, 0.035f), Quaternion.Euler(0f, 0f, -32f), metal);
            AddPart(root.transform, "PetalRight", PrimitiveType.Cube, new Vector3(0.10f, 0.56f, 0f), new Vector3(0.06f, 0.19f, 0.035f), Quaternion.Euler(0f, 0f, 32f), metal);
            AddPart(root.transform, "CrystalBody", PrimitiveType.Cylinder, new Vector3(0f, 0.73f, 0f), new Vector3(0.085f, 0.30f, 0.085f), Quaternion.identity, crystal);
            AddPart(root.transform, "CrystalTip", PrimitiveType.Sphere, new Vector3(0f, 0.92f, 0f), new Vector3(0.08f, 0.10f, 0.08f), Quaternion.identity, crystal);
            AddGripPoint(root.transform, new Vector3(0f, -0.32f, 0f), Quaternion.identity);
            return root;
        }

        private static GameObject CreateShockGun(string id)
        {
            Material white = CreateMaterial("Generated Shock Gun White Casing", new Color(0.82f, 0.85f, 0.90f, 1f));
            Material dark = CreateMaterial("Generated Shock Gun Dark Insert", new Color(0.035f, 0.04f, 0.05f, 1f));
            Material red = CreateMaterial("Generated Shock Gun Red Stripe", new Color(0.82f, 0.10f, 0.08f, 1f));
            Material coil = CreateMaterial("Generated Shock Gun Blue Coil", new Color(0.12f, 0.86f, 1.00f, 1f));
            Material grip = CreateMaterial("Generated Shock Gun Grip", new Color(0.17f, 0.10f, 0.06f, 1f));
            Material metal = CreateMaterial("Generated Shock Gun Metal", new Color(0.30f, 0.32f, 0.36f, 1f));

            GameObject root = CreateRoot(id, "Generated_ShockGun");
            AddPart(root.transform, "LongCasing", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.23f), new Vector3(0.26f, 0.17f, 0.82f), Quaternion.identity, white);
            AddPart(root.transform, "TopRail", PrimitiveType.Cube, new Vector3(0f, 0.14f, 0.23f), new Vector3(0.18f, 0.045f, 0.68f), Quaternion.identity, dark);
            AddPart(root.transform, "RearBlock", PrimitiveType.Cube, new Vector3(0f, 0.00f, -0.24f), new Vector3(0.30f, 0.20f, 0.20f), Quaternion.identity, white);
            AddPart(root.transform, "RedRearStripe", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.10f), new Vector3(0.28f, 0.19f, 0.045f), Quaternion.identity, red);
            AddPart(root.transform, "BarrelCore", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.74f), new Vector3(0.060f, 0.33f, 0.060f), Quaternion.Euler(90f, 0f, 0f), metal);
            AddPart(root.transform, "MuzzleBox", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.93f), new Vector3(0.22f, 0.14f, 0.12f), Quaternion.identity, dark);
            AddPart(root.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.22f, -0.18f), new Vector3(0.15f, 0.34f, 0.13f), Quaternion.Euler(-14f, 0f, 0f), grip);
            AddPart(root.transform, "TriggerGuard", PrimitiveType.Cube, new Vector3(0f, -0.11f, 0.02f), new Vector3(0.17f, 0.045f, 0.13f), Quaternion.identity, dark);
            AddPart(root.transform, "Trigger", PrimitiveType.Cube, new Vector3(0f, -0.155f, 0.02f), new Vector3(0.05f, 0.10f, 0.025f), Quaternion.Euler(0f, 0f, 8f), metal);
            AddPart(root.transform, "CoilA", PrimitiveType.Cylinder, new Vector3(0f, -0.105f, 0.34f), new Vector3(0.070f, 0.035f, 0.070f), Quaternion.Euler(90f, 0f, 0f), coil);
            AddPart(root.transform, "CoilB", PrimitiveType.Cylinder, new Vector3(0f, -0.105f, 0.44f), new Vector3(0.070f, 0.035f, 0.070f), Quaternion.Euler(90f, 0f, 0f), coil);
            AddPart(root.transform, "CoilC", PrimitiveType.Cylinder, new Vector3(0f, -0.105f, 0.54f), new Vector3(0.070f, 0.035f, 0.070f), Quaternion.Euler(90f, 0f, 0f), coil);
            for (int i = 0; i < 5; i++)
            {
                AddPart(root.transform, "Vent" + i, PrimitiveType.Cube, new Vector3(0f, -0.070f, 0.03f + i * 0.08f), new Vector3(0.22f, 0.018f, 0.025f), Quaternion.identity, dark);
            }
            AddGripPoint(root.transform, new Vector3(0f, -0.22f, -0.18f), Quaternion.identity);
            return root;
        }

        private static GameObject CreateBow(string id)
        {
            Material wood = CreateMaterial("Generated Bow Carved Wood", new Color(0.52f, 0.26f, 0.12f, 1f));
            Material darkWood = CreateMaterial("Generated Bow Dark Grain", new Color(0.18f, 0.09f, 0.04f, 1f));
            Material wrap = CreateMaterial("Generated Bow Green Wrap", new Color(0.04f, 0.58f, 0.37f, 1f));
            Material stringMaterial = CreateMaterial("Generated Bow Braided String", new Color(0.92f, 0.82f, 0.52f, 1f));
            GameObject root = CreateRoot(id, "Generated_Bow");
            AddPart(root.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.10f, 0.24f, 0.08f), Quaternion.identity, wrap);
            AddPart(root.transform, "UpperLimbA", PrimitiveType.Cube, new Vector3(0.02f, 0.28f, 0f), new Vector3(0.075f, 0.34f, 0.070f), Quaternion.Euler(0f, 0f, -17f), wood);
            AddPart(root.transform, "UpperLimbB", PrimitiveType.Cube, new Vector3(0.12f, 0.57f, 0f), new Vector3(0.065f, 0.34f, 0.060f), Quaternion.Euler(0f, 0f, -28f), wood);
            AddPart(root.transform, "LowerLimbA", PrimitiveType.Cube, new Vector3(0.02f, -0.28f, 0f), new Vector3(0.075f, 0.34f, 0.070f), Quaternion.Euler(0f, 0f, 17f), wood);
            AddPart(root.transform, "LowerLimbB", PrimitiveType.Cube, new Vector3(0.12f, -0.57f, 0f), new Vector3(0.065f, 0.34f, 0.060f), Quaternion.Euler(0f, 0f, 28f), wood);
            AddPart(root.transform, "UpperTipWrap", PrimitiveType.Cube, new Vector3(0.22f, 0.75f, 0f), new Vector3(0.070f, 0.13f, 0.064f), Quaternion.Euler(0f, 0f, -34f), wrap);
            AddPart(root.transform, "LowerTipWrap", PrimitiveType.Cube, new Vector3(0.22f, -0.75f, 0f), new Vector3(0.070f, 0.13f, 0.064f), Quaternion.Euler(0f, 0f, 34f), wrap);
            AddPart(root.transform, "String", PrimitiveType.Cube, new Vector3(-0.18f, 0f, -0.02f), new Vector3(0.020f, 1.47f, 0.016f), Quaternion.identity, stringMaterial);
            AddPart(root.transform, "GrainA", PrimitiveType.Cube, new Vector3(0.04f, 0.42f, -0.038f), new Vector3(0.022f, 0.22f, 0.010f), Quaternion.Euler(0f, 0f, -22f), darkWood);
            AddPart(root.transform, "GrainB", PrimitiveType.Cube, new Vector3(0.04f, -0.42f, -0.038f), new Vector3(0.022f, 0.22f, 0.010f), Quaternion.Euler(0f, 0f, 22f), darkWood);
            AddGripPoint(root.transform, Vector3.zero, Quaternion.identity);
            return root;
        }

        private static GameObject CreateNinjaStars(string id)
        {
            Material black = CreateMaterial("Generated Ninja Star Black", new Color(0.035f, 0.035f, 0.04f, 1f));
            Material orange = CreateMaterial("Generated Ninja Star Orange Edge", new Color(1.00f, 0.45f, 0.02f, 1f));
            Material yellow = CreateMaterial("Generated Ninja Star Yellow Mark", new Color(1.00f, 0.88f, 0.14f, 1f));
            Material core = CreateMaterial("Generated Ninja Star Core", new Color(0.12f, 0.12f, 0.13f, 1f));

            GameObject root = CreateRoot(id, "Generated_NinjaStar");
            CreateStar(root.transform, "Star", Vector3.zero, 0f, black, orange, yellow, core, 1.15f);
            AddGripPoint(root.transform, Vector3.zero, Quaternion.identity);
            return root;
        }

        private static GameObject CreateUmbrella(string id)
        {
            Material handle = CreateMaterial("Generated Umbrella Black Handle", new Color(0.035f, 0.035f, 0.040f, 1f));
            Material rib = CreateMaterial("Generated Umbrella Silver Rib", new Color(0.70f, 0.74f, 0.78f, 1f));
            Material yellow = CreateMaterial("Generated Umbrella Yellow Panel", new Color(1.00f, 0.82f, 0.08f, 1f));
            Material red = CreateMaterial("Generated Umbrella Red Panel", new Color(0.92f, 0.14f, 0.11f, 1f));

            GameObject root = CreateRoot(id, "Generated_Umbrella");
            AddPart(root.transform, "StraightHandle", PrimitiveType.Cylinder, new Vector3(0f, -0.30f, 0f), new Vector3(0.035f, 0.88f, 0.035f), Quaternion.identity, handle);
            AddPart(root.transform, "Ferrule", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.028f, 0.40f, 0.028f), Quaternion.identity, rib);
            AddPart(root.transform, "CanopyRed", PrimitiveType.Cylinder, new Vector3(0f, 0.54f, 0f), new Vector3(0.55f, 0.050f, 0.55f), Quaternion.identity, red);
            AddPart(root.transform, "CanopyGoldCenter", PrimitiveType.Cylinder, new Vector3(0f, 0.585f, 0f), new Vector3(0.18f, 0.040f, 0.18f), Quaternion.identity, yellow);
            AddPart(root.transform, "TopCap", PrimitiveType.Cylinder, new Vector3(0f, 0.66f, 0f), new Vector3(0.055f, 0.080f, 0.055f), Quaternion.identity, rib);
            AddPart(root.transform, "RibForward", PrimitiveType.Cube, new Vector3(0f, 0.585f, 0.25f), new Vector3(0.018f, 0.014f, 0.50f), Quaternion.identity, rib);
            AddPart(root.transform, "RibSide", PrimitiveType.Cube, new Vector3(0.25f, 0.585f, 0f), new Vector3(0.50f, 0.014f, 0.018f), Quaternion.identity, rib);
            AddPart(root.transform, "GripKnob", PrimitiveType.Sphere, new Vector3(0f, -0.76f, 0f), new Vector3(0.070f, 0.050f, 0.070f), Quaternion.identity, handle);
            AddGripPoint(root.transform, Vector3.zero, Quaternion.identity);
            return root;
        }

        private static void CreateStar(
            Transform parent,
            string name,
            Vector3 localPosition,
            float localRoll,
            Material black,
            Material orange,
            Material yellow,
            Material core,
            float scale)
        {
            Transform starRoot = new GameObject(name).transform;
            starRoot.SetParent(parent, false);
            starRoot.localPosition = localPosition;
            starRoot.localRotation = Quaternion.Euler(0f, 0f, localRoll);
            starRoot.localScale = Vector3.one * scale;

            AddPart(starRoot, "OrangeBladeVertical", PrimitiveType.Cube, new Vector3(0f, 0f, 0.000f), new Vector3(0.070f, 0.42f, 0.020f), Quaternion.Euler(0f, 0f, 45f), orange);
            AddPart(starRoot, "OrangeBladeHorizontal", PrimitiveType.Cube, new Vector3(0f, 0f, 0.001f), new Vector3(0.42f, 0.070f, 0.020f), Quaternion.Euler(0f, 0f, 45f), orange);
            AddPart(starRoot, "BlackBladeVertical", PrimitiveType.Cube, new Vector3(0f, 0f, -0.005f), new Vector3(0.052f, 0.33f, 0.018f), Quaternion.Euler(0f, 0f, 45f), black);
            AddPart(starRoot, "BlackBladeHorizontal", PrimitiveType.Cube, new Vector3(0f, 0f, -0.004f), new Vector3(0.33f, 0.052f, 0.018f), Quaternion.Euler(0f, 0f, 45f), black);
            AddPart(starRoot, "CenterRing", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.090f, 0.026f, 0.090f), Quaternion.Euler(90f, 0f, 0f), core);
            AddPart(starRoot, "CenterHole", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.012f), new Vector3(0.048f, 0.028f, 0.048f), Quaternion.Euler(90f, 0f, 0f), black);
            AddPart(starRoot, "MarkTop", PrimitiveType.Cube, new Vector3(0f, 0.14f, -0.020f), new Vector3(0.042f, 0.042f, 0.010f), Quaternion.Euler(0f, 0f, 45f), yellow);
            AddPart(starRoot, "MarkBottom", PrimitiveType.Cube, new Vector3(0f, -0.14f, -0.020f), new Vector3(0.042f, 0.042f, 0.010f), Quaternion.Euler(0f, 0f, 45f), yellow);
            AddPart(starRoot, "MarkLeft", PrimitiveType.Cube, new Vector3(-0.14f, 0f, -0.020f), new Vector3(0.042f, 0.042f, 0.010f), Quaternion.Euler(0f, 0f, 45f), yellow);
            AddPart(starRoot, "MarkRight", PrimitiveType.Cube, new Vector3(0.14f, 0f, -0.020f), new Vector3(0.042f, 0.042f, 0.010f), Quaternion.Euler(0f, 0f, 45f), yellow);
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

        private static Transform AddGripPoint(
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            Transform grip = new GameObject(GripPointName).transform;
            grip.SetParent(parent, false);
            grip.localPosition = localPosition;
            grip.localRotation = localRotation;
            grip.localScale = Vector3.one;
            return grip;
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
