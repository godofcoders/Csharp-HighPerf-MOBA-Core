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

            if (IsBrawler(definition, "Barley"))
            {
                instance = BuildBarley(parent, owner);
                return instance != null;
            }

            if (IsBrawler(definition, "Bo"))
            {
                instance = BuildBo(parent, owner);
                return instance != null;
            }

            if (IsBrawler(definition, "ElPrimo") || IsBrawler(definition, "El Primo"))
            {
                instance = BuildElPrimo(parent, owner);
                return instance != null;
            }

            if (IsBrawler(definition, "Byron"))
            {
                instance = BuildByron(parent, owner);
                return instance != null;
            }

            if (IsBrawler(definition, "Piper"))
            {
                instance = BuildPiper(parent, owner);
                return instance != null;
            }

            if (IsBrawler(definition, "Leon"))
            {
                instance = BuildLeon(parent, owner);
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
            bodyRoot.localScale = new Vector3(0.94f, 0.94f, 0.94f);
            bodyRoot.gameObject.layer = layer;

            CreatePart(bodyRoot, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.56f, 0f), new Vector3(0.52f, 0.18f, 0.34f), Quaternion.identity, pants, layer);
            Transform torso = CreatePart(bodyRoot, "Torso_Jacket", PrimitiveType.Capsule, new Vector3(0f, 0.93f, 0f), new Vector3(0.62f, 0.62f, 0.42f), Quaternion.identity, jacket, layer);
            CreatePart(bodyRoot, "Chest_Shirt", PrimitiveType.Cube, new Vector3(0f, 0.95f, -0.30f), new Vector3(0.32f, 0.46f, 0.045f), Quaternion.identity, shirt, layer);
            CreatePart(bodyRoot, "Cowboy_Belt", PrimitiveType.Cube, new Vector3(0f, 0.66f, -0.30f), new Vector3(0.58f, 0.055f, 0.055f), Quaternion.identity, boots, layer);
            CreatePart(bodyRoot, "Belt_Buckle", PrimitiveType.Cube, new Vector3(0f, 0.66f, -0.35f), new Vector3(0.13f, 0.09f, 0.035f), Quaternion.identity, barrel, layer);
            CreatePart(bodyRoot, "Left_Holster", PrimitiveType.Cube, new Vector3(-0.32f, 0.55f, -0.22f), new Vector3(0.10f, 0.25f, 0.10f), Quaternion.Euler(0f, 0f, -10f), boots, layer);
            CreatePart(bodyRoot, "Right_Holster", PrimitiveType.Cube, new Vector3(0.32f, 0.55f, -0.22f), new Vector3(0.10f, 0.25f, 0.10f), Quaternion.Euler(0f, 0f, 10f), boots, layer);
            CreatePart(bodyRoot, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.29f, 0f), new Vector3(0.16f, 0.10f, 0.16f), Quaternion.identity, skin, layer);
            Transform head = CreatePart(bodyRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.52f, -0.01f), new Vector3(0.42f, 0.46f, 0.38f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Hair_Cap", PrimitiveType.Sphere, new Vector3(0f, 1.67f, -0.03f), new Vector3(0.45f, 0.21f, 0.40f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Hair_Quiff", PrimitiveType.Cube, new Vector3(0.10f, 1.72f, -0.23f), new Vector3(0.28f, 0.18f, 0.24f), Quaternion.Euler(0f, 0f, -18f), hair, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Leg", PrimitiveType.Capsule, new Vector3(-0.17f, 0.30f, 0.02f), new Vector3(0.18f, 0.46f, 0.18f), Quaternion.identity, pants, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Leg", PrimitiveType.Capsule, new Vector3(0.17f, 0.30f, 0.02f), new Vector3(0.18f, 0.46f, 0.18f), Quaternion.identity, pants, layer);
            Transform leftFoot = CreatePart(bodyRoot, "Left_Boot", PrimitiveType.Cube, new Vector3(-0.17f, 0.06f, -0.06f), new Vector3(0.22f, 0.11f, 0.34f), Quaternion.identity, boots, layer);
            Transform rightFoot = CreatePart(bodyRoot, "Right_Boot", PrimitiveType.Cube, new Vector3(0.17f, 0.06f, -0.06f), new Vector3(0.22f, 0.11f, 0.34f), Quaternion.identity, boots, layer);

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
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftFoot, rightFoot, leftWeapon, rightWeapon);

            return root;
        }

        private static GameObject BuildJessie(Transform parent, BrawlerController owner)
        {
            int layer = parent.gameObject.layer;
            Material skin = CreateMaterial("Procedural Jessie Skin", new Color(1.00f, 0.66f, 0.46f, 1f), 0.18f);
            Material hair = CreateMaterial("Procedural Jessie Hair", new Color(0.88f, 0.24f, 0.12f, 1f), 0.26f);
            Material cap = CreateMaterial("Procedural Jessie Cap", new Color(0.98f, 0.77f, 0.16f, 1f), 0.30f);
            Material goggle = CreateMaterial("Procedural Jessie Goggles", new Color(0.03f, 0.23f, 0.65f, 1f), 0.45f);
            Material shirt = CreateMaterial("Procedural Jessie Shirt", new Color(0.95f, 0.24f, 0.17f, 1f), 0.24f);
            Material overalls = CreateMaterial("Procedural Jessie Overalls", new Color(0.18f, 0.72f, 0.94f, 1f), 0.34f);
            Material boots = CreateMaterial("Procedural Jessie Boots", new Color(0.56f, 0.27f, 0.10f, 1f), 0.18f);
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
            bodyRoot.localScale = new Vector3(0.88f, 0.88f, 0.88f);
            bodyRoot.gameObject.layer = layer;

            CreatePart(bodyRoot, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.56f, 0f), new Vector3(0.48f, 0.18f, 0.32f), Quaternion.identity, overalls, layer);
            Transform torso = CreatePart(bodyRoot, "Torso_Overalls", PrimitiveType.Capsule, new Vector3(0f, 0.91f, 0f), new Vector3(0.56f, 0.58f, 0.40f), Quaternion.identity, overalls, layer);
            CreatePart(bodyRoot, "Chest_Shirt", PrimitiveType.Cube, new Vector3(0f, 0.96f, -0.30f), new Vector3(0.34f, 0.34f, 0.045f), Quaternion.identity, shirt, layer);
            CreatePart(bodyRoot, "Overall_Strap_L", PrimitiveType.Cube, new Vector3(-0.12f, 1.02f, -0.34f), new Vector3(0.065f, 0.36f, 0.04f), Quaternion.Euler(0f, 0f, -8f), overalls, layer);
            CreatePart(bodyRoot, "Overall_Strap_R", PrimitiveType.Cube, new Vector3(0.12f, 1.02f, -0.34f), new Vector3(0.065f, 0.36f, 0.04f), Quaternion.Euler(0f, 0f, 8f), overalls, layer);
            CreatePart(bodyRoot, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.27f, 0f), new Vector3(0.15f, 0.09f, 0.15f), Quaternion.identity, skin, layer);
            Transform head = CreatePart(bodyRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.49f, -0.01f), new Vector3(0.40f, 0.43f, 0.37f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Hair_Back", PrimitiveType.Sphere, new Vector3(0f, 1.47f, 0.16f), new Vector3(0.44f, 0.34f, 0.28f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Cap_Crown", PrimitiveType.Sphere, new Vector3(0f, 1.64f, -0.02f), new Vector3(0.45f, 0.18f, 0.40f), Quaternion.identity, cap, layer);
            CreatePart(bodyRoot, "Cap_Brim", PrimitiveType.Cube, new Vector3(0f, 1.60f, -0.26f), new Vector3(0.38f, 0.055f, 0.23f), Quaternion.Euler(-6f, 0f, 0f), cap, layer);
            CreatePart(bodyRoot, "Goggle_Left", PrimitiveType.Cylinder, new Vector3(-0.12f, 1.66f, -0.27f), new Vector3(0.105f, 0.035f, 0.105f), Quaternion.Euler(90f, 0f, 0f), goggle, layer);
            CreatePart(bodyRoot, "Goggle_Right", PrimitiveType.Cylinder, new Vector3(0.12f, 1.66f, -0.27f), new Vector3(0.105f, 0.035f, 0.105f), Quaternion.Euler(90f, 0f, 0f), goggle, layer);
            CreatePart(bodyRoot, "Goggle_Bridge", PrimitiveType.Cube, new Vector3(0f, 1.66f, -0.27f), new Vector3(0.11f, 0.035f, 0.035f), Quaternion.identity, goggle, layer);
            CreatePart(bodyRoot, "Left_Ponytail", PrimitiveType.Sphere, new Vector3(-0.34f, 1.38f, 0.04f), new Vector3(0.20f, 0.28f, 0.20f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Right_Ponytail", PrimitiveType.Sphere, new Vector3(0.34f, 1.38f, 0.04f), new Vector3(0.20f, 0.28f, 0.20f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Left_Pigtail_Tie", PrimitiveType.Cube, new Vector3(-0.27f, 1.40f, -0.07f), new Vector3(0.06f, 0.08f, 0.06f), Quaternion.identity, cap, layer);
            CreatePart(bodyRoot, "Right_Pigtail_Tie", PrimitiveType.Cube, new Vector3(0.27f, 1.40f, -0.07f), new Vector3(0.06f, 0.08f, 0.06f), Quaternion.identity, cap, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Leg", PrimitiveType.Capsule, new Vector3(-0.15f, 0.29f, 0.02f), new Vector3(0.17f, 0.44f, 0.17f), Quaternion.identity, overalls, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Leg", PrimitiveType.Capsule, new Vector3(0.15f, 0.29f, 0.02f), new Vector3(0.17f, 0.44f, 0.17f), Quaternion.identity, overalls, layer);
            Transform leftFoot = CreatePart(bodyRoot, "Left_Boot", PrimitiveType.Cube, new Vector3(-0.15f, 0.06f, -0.06f), new Vector3(0.21f, 0.11f, 0.32f), Quaternion.identity, boots, layer);
            Transform rightFoot = CreatePart(bodyRoot, "Right_Boot", PrimitiveType.Cube, new Vector3(0.15f, 0.06f, -0.06f), new Vector3(0.21f, 0.11f, 0.32f), Quaternion.identity, boots, layer);

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
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftFoot, rightFoot, null, blaster);

            return root;
        }

        private static GameObject BuildBarley(Transform parent, BrawlerController owner)
        {
            int layer = parent.gameObject.layer;
            Material metal = CreateMaterial("Procedural Barley Metal", new Color(0.68f, 0.72f, 0.78f, 1f), 0.55f);
            Material darkMetal = CreateMaterial("Procedural Barley Dark Metal", new Color(0.18f, 0.20f, 0.24f, 1f), 0.45f);
            Material vest = CreateMaterial("Procedural Barley Vest", new Color(0.48f, 0.18f, 0.76f, 1f), 0.34f);
            Material trim = CreateMaterial("Procedural Barley Gold Trim", new Color(0.95f, 0.72f, 0.18f, 1f), 0.35f);
            Material eye = CreateMaterial("Procedural Barley Eye", new Color(0.25f, 0.95f, 1.00f, 1f), 0.50f);
            Material bottleGlass = CreateMaterial("Procedural Barley Bottle Glass", new Color(0.10f, 0.70f, 0.36f, 1f), 0.42f);
            Material cork = CreateMaterial("Procedural Barley Cork", new Color(0.66f, 0.38f, 0.16f, 1f), 0.18f);

            GameObject root = new GameObject("Procedural_Barley_Model");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = layer;

            Transform bodyRoot = new GameObject("BodyRig").transform;
            bodyRoot.SetParent(root.transform, false);
            bodyRoot.localPosition = Vector3.zero;
            bodyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bodyRoot.localScale = new Vector3(0.86f, 0.88f, 0.86f);
            bodyRoot.gameObject.layer = layer;

            CreatePart(bodyRoot, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.50f, 0f), new Vector3(0.34f, 0.12f, 0.34f), Quaternion.identity, darkMetal, layer);
            Transform torso = CreatePart(bodyRoot, "Torso_Canister", PrimitiveType.Capsule, new Vector3(0f, 0.90f, 0f), new Vector3(0.52f, 0.58f, 0.42f), Quaternion.identity, metal, layer);
            CreatePart(bodyRoot, "Vest_Front", PrimitiveType.Cube, new Vector3(0f, 0.92f, -0.31f), new Vector3(0.36f, 0.45f, 0.045f), Quaternion.identity, vest, layer);
            CreatePart(bodyRoot, "Bow_Tie", PrimitiveType.Cube, new Vector3(0f, 1.20f, -0.34f), new Vector3(0.22f, 0.08f, 0.05f), Quaternion.Euler(0f, 0f, 45f), trim, layer);
            CreatePart(bodyRoot, "Neck_Post", PrimitiveType.Cylinder, new Vector3(0f, 1.26f, 0f), new Vector3(0.13f, 0.10f, 0.13f), Quaternion.identity, darkMetal, layer);
            Transform head = CreatePart(bodyRoot, "Robot_Head", PrimitiveType.Cylinder, new Vector3(0f, 1.50f, -0.01f), new Vector3(0.36f, 0.25f, 0.36f), Quaternion.identity, metal, layer);
            CreatePart(bodyRoot, "Eye_Lens", PrimitiveType.Sphere, new Vector3(0f, 1.51f, -0.30f), new Vector3(0.14f, 0.14f, 0.045f), Quaternion.identity, eye, layer);
            CreatePart(bodyRoot, "Hat_Rim", PrimitiveType.Cylinder, new Vector3(0f, 1.72f, -0.01f), new Vector3(0.42f, 0.035f, 0.42f), Quaternion.identity, darkMetal, layer);
            CreatePart(bodyRoot, "Hat_Top", PrimitiveType.Cylinder, new Vector3(0f, 1.84f, -0.01f), new Vector3(0.28f, 0.12f, 0.28f), Quaternion.identity, darkMetal, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Robot_Leg", PrimitiveType.Cylinder, new Vector3(-0.15f, 0.24f, 0.02f), new Vector3(0.10f, 0.30f, 0.10f), Quaternion.identity, darkMetal, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Robot_Leg", PrimitiveType.Cylinder, new Vector3(0.15f, 0.24f, 0.02f), new Vector3(0.10f, 0.30f, 0.10f), Quaternion.identity, darkMetal, layer);
            Transform leftFoot = CreatePart(bodyRoot, "Left_Foot", PrimitiveType.Cube, new Vector3(-0.15f, 0.06f, -0.06f), new Vector3(0.20f, 0.10f, 0.27f), Quaternion.identity, darkMetal, layer);
            Transform rightFoot = CreatePart(bodyRoot, "Right_Foot", PrimitiveType.Cube, new Vector3(0.15f, 0.06f, -0.06f), new Vector3(0.20f, 0.10f, 0.27f), Quaternion.identity, darkMetal, layer);

            Transform leftArm = CreateRiggedPart(bodyRoot, "Left_Robot_Arm", PrimitiveType.Cylinder, new Vector3(-0.42f, 0.92f, -0.08f), new Vector3(0.09f, 0.42f, 0.09f), Quaternion.Euler(44f, 0f, -20f), darkMetal, layer);
            Transform rightArm = CreateRiggedPart(bodyRoot, "Right_Robot_Arm", PrimitiveType.Cylinder, new Vector3(0.44f, 0.88f, -0.13f), new Vector3(0.09f, 0.44f, 0.09f), Quaternion.Euler(66f, 0f, 18f), darkMetal, layer);
            CreatePart(bodyRoot, "Left_Shoulder_Bolt", PrimitiveType.Sphere, new Vector3(-0.31f, 1.08f, -0.01f), new Vector3(0.17f, 0.17f, 0.16f), Quaternion.identity, trim, layer);
            CreatePart(bodyRoot, "Right_Shoulder_Bolt", PrimitiveType.Sphere, new Vector3(0.31f, 1.08f, -0.01f), new Vector3(0.17f, 0.17f, 0.16f), Quaternion.identity, trim, layer);

            Transform bottle = CreateBottle(bodyRoot, "Bottle", new Vector3(0.43f, 0.76f, -0.48f), -10f, bottleGlass, cork, layer, out Transform bottleMouth);
            AttachKeepingWorld(bottle, rightArm);

            Transform castPoint = CreateAnchor(bodyRoot, "CastPoint", new Vector3(0f, 1.08f, -0.52f), layer);
            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(bottleMouth, bottleMouth, castPoint);

            BrawlerProceduralModelAnimator animator = root.AddComponent<BrawlerProceduralModelAnimator>();
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftFoot, rightFoot, null, bottle);

            return root;
        }

        private static GameObject BuildBo(Transform parent, BrawlerController owner)
        {
            int layer = parent.gameObject.layer;
            Material skin = CreateMaterial("Procedural Bo Skin", new Color(0.66f, 0.36f, 0.20f, 1f), 0.18f);
            Material hair = CreateMaterial("Procedural Bo Hair", new Color(0.08f, 0.06f, 0.04f, 1f), 0.24f);
            Material vest = CreateMaterial("Procedural Bo Vest", new Color(0.86f, 0.66f, 0.28f, 1f), 0.26f);
            Material teal = CreateMaterial("Procedural Bo Teal Trim", new Color(0.03f, 0.58f, 0.56f, 1f), 0.32f);
            Material pants = CreateMaterial("Procedural Bo Pants", new Color(0.24f, 0.12f, 0.08f, 1f), 0.24f);
            Material boot = CreateMaterial("Procedural Bo Boots", new Color(0.08f, 0.04f, 0.025f, 1f), 0.18f);
            Material bowWood = CreateMaterial("Procedural Bo Bow Wood", new Color(0.54f, 0.27f, 0.10f, 1f), 0.30f);
            Material bowString = CreateMaterial("Procedural Bo Bow String", new Color(0.88f, 0.88f, 0.78f, 1f), 0.24f);
            Material feather = CreateMaterial("Procedural Bo Feather", new Color(0.94f, 0.20f, 0.12f, 1f), 0.28f);

            GameObject root = new GameObject("Procedural_Bo_Model");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = layer;

            Transform bodyRoot = new GameObject("BodyRig").transform;
            bodyRoot.SetParent(root.transform, false);
            bodyRoot.localPosition = Vector3.zero;
            bodyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bodyRoot.localScale = new Vector3(0.98f, 1.00f, 0.98f);
            bodyRoot.gameObject.layer = layer;

            CreatePart(bodyRoot, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.56f, 0f), new Vector3(0.54f, 0.18f, 0.34f), Quaternion.identity, pants, layer);
            Transform torso = CreatePart(bodyRoot, "Torso_Vest", PrimitiveType.Capsule, new Vector3(0f, 0.94f, 0f), new Vector3(0.58f, 0.62f, 0.40f), Quaternion.identity, vest, layer);
            CreatePart(bodyRoot, "Chest_Trim", PrimitiveType.Cube, new Vector3(0f, 0.98f, -0.30f), new Vector3(0.34f, 0.36f, 0.04f), Quaternion.identity, teal, layer);
            CreatePart(bodyRoot, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.30f, 0f), new Vector3(0.15f, 0.09f, 0.15f), Quaternion.identity, skin, layer);
            Transform head = CreatePart(bodyRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.52f, -0.01f), new Vector3(0.39f, 0.43f, 0.36f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Hair", PrimitiveType.Sphere, new Vector3(0f, 1.68f, 0.00f), new Vector3(0.40f, 0.20f, 0.35f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Headband", PrimitiveType.Cube, new Vector3(0f, 1.62f, -0.29f), new Vector3(0.40f, 0.06f, 0.035f), Quaternion.identity, teal, layer);
            CreatePart(bodyRoot, "Feather", PrimitiveType.Cube, new Vector3(0.18f, 1.84f, -0.07f), new Vector3(0.08f, 0.30f, 0.035f), Quaternion.Euler(0f, 0f, -22f), feather, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Leg", PrimitiveType.Capsule, new Vector3(-0.17f, 0.30f, 0.02f), new Vector3(0.18f, 0.46f, 0.18f), Quaternion.identity, pants, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Leg", PrimitiveType.Capsule, new Vector3(0.17f, 0.30f, 0.02f), new Vector3(0.18f, 0.46f, 0.18f), Quaternion.identity, pants, layer);
            Transform leftFoot = CreatePart(bodyRoot, "Left_Boot", PrimitiveType.Cube, new Vector3(-0.17f, 0.06f, -0.06f), new Vector3(0.22f, 0.11f, 0.32f), Quaternion.identity, boot, layer);
            Transform rightFoot = CreatePart(bodyRoot, "Right_Boot", PrimitiveType.Cube, new Vector3(0.17f, 0.06f, -0.06f), new Vector3(0.22f, 0.11f, 0.32f), Quaternion.identity, boot, layer);

            Transform leftArm = CreateRiggedPart(bodyRoot, "Left_Arm", PrimitiveType.Capsule, new Vector3(-0.45f, 0.92f, -0.12f), new Vector3(0.15f, 0.47f, 0.15f), Quaternion.Euler(62f, 0f, -20f), skin, layer);
            Transform rightArm = CreateRiggedPart(bodyRoot, "Right_Arm", PrimitiveType.Capsule, new Vector3(0.47f, 0.92f, -0.12f), new Vector3(0.15f, 0.47f, 0.15f), Quaternion.Euler(62f, 0f, 20f), skin, layer);
            CreatePart(bodyRoot, "Left_Shoulder", PrimitiveType.Sphere, new Vector3(-0.32f, 1.09f, -0.01f), new Vector3(0.20f, 0.20f, 0.18f), Quaternion.identity, teal, layer);
            CreatePart(bodyRoot, "Right_Shoulder", PrimitiveType.Sphere, new Vector3(0.32f, 1.09f, -0.01f), new Vector3(0.20f, 0.20f, 0.18f), Quaternion.identity, teal, layer);

            Transform bow = CreateBow(bodyRoot, "Bow", new Vector3(0.44f, 0.80f, -0.50f), 6f, bowWood, bowString, layer, out Transform bowMuzzle);
            AttachKeepingWorld(bow, rightArm);

            Transform castPoint = CreateAnchor(bodyRoot, "CastPoint", new Vector3(0f, 1.04f, -0.55f), layer);
            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(bowMuzzle, bowMuzzle, castPoint);

            BrawlerProceduralModelAnimator animator = root.AddComponent<BrawlerProceduralModelAnimator>();
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftFoot, rightFoot, null, bow);

            return root;
        }

        private static GameObject BuildElPrimo(Transform parent, BrawlerController owner)
        {
            int layer = parent.gameObject.layer;
            Material skin = CreateMaterial("Procedural El Primo Skin", new Color(0.93f, 0.50f, 0.27f, 1f), 0.24f);
            Material mask = CreateMaterial("Procedural El Primo Mask", new Color(0.05f, 0.33f, 0.95f, 1f), 0.34f);
            Material maskTrim = CreateMaterial("Procedural El Primo Mask Trim", new Color(0.95f, 0.10f, 0.12f, 1f), 0.32f);
            Material pants = CreateMaterial("Procedural El Primo Pants", new Color(0.04f, 0.20f, 0.84f, 1f), 0.30f);
            Material belt = CreateMaterial("Procedural El Primo Belt", new Color(0.88f, 0.11f, 0.09f, 1f), 0.28f);
            Material gold = CreateMaterial("Procedural El Primo Gold", new Color(1.00f, 0.72f, 0.14f, 1f), 0.36f);
            Material white = CreateMaterial("Procedural El Primo White", new Color(0.96f, 0.96f, 0.90f, 1f), 0.22f);

            GameObject root = new GameObject("Procedural_ElPrimo_Model");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = layer;

            Transform bodyRoot = new GameObject("BodyRig").transform;
            bodyRoot.SetParent(root.transform, false);
            bodyRoot.localPosition = Vector3.zero;
            bodyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bodyRoot.localScale = new Vector3(1.16f, 1.13f, 1.16f);
            bodyRoot.gameObject.layer = layer;

            CreatePart(bodyRoot, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.57f, 0f), new Vector3(0.62f, 0.20f, 0.38f), Quaternion.identity, pants, layer);
            Transform torso = CreatePart(bodyRoot, "Muscle_Torso", PrimitiveType.Capsule, new Vector3(0f, 0.98f, 0f), new Vector3(0.74f, 0.72f, 0.48f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Chest_Highlight", PrimitiveType.Cube, new Vector3(0f, 1.05f, -0.34f), new Vector3(0.36f, 0.38f, 0.035f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Belt", PrimitiveType.Cube, new Vector3(0f, 0.67f, -0.33f), new Vector3(0.66f, 0.09f, 0.06f), Quaternion.identity, belt, layer);
            CreatePart(bodyRoot, "Belt_Emblem", PrimitiveType.Cylinder, new Vector3(0f, 0.68f, -0.40f), new Vector3(0.13f, 0.035f, 0.13f), Quaternion.Euler(90f, 0f, 0f), gold, layer);
            CreatePart(bodyRoot, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.36f, 0f), new Vector3(0.18f, 0.11f, 0.18f), Quaternion.identity, skin, layer);
            Transform head = CreatePart(bodyRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.60f, -0.01f), new Vector3(0.43f, 0.45f, 0.39f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Mask_Front", PrimitiveType.Sphere, new Vector3(0f, 1.61f, -0.08f), new Vector3(0.44f, 0.46f, 0.35f), Quaternion.identity, mask, layer);
            CreatePart(bodyRoot, "Mask_Left_Trim", PrimitiveType.Cube, new Vector3(-0.18f, 1.62f, -0.31f), new Vector3(0.08f, 0.31f, 0.035f), Quaternion.Euler(0f, 0f, -18f), maskTrim, layer);
            CreatePart(bodyRoot, "Mask_Right_Trim", PrimitiveType.Cube, new Vector3(0.18f, 1.62f, -0.31f), new Vector3(0.08f, 0.31f, 0.035f), Quaternion.Euler(0f, 0f, 18f), maskTrim, layer);
            CreatePart(bodyRoot, "Left_Eye", PrimitiveType.Sphere, new Vector3(-0.10f, 1.63f, -0.34f), new Vector3(0.10f, 0.07f, 0.035f), Quaternion.identity, white, layer);
            CreatePart(bodyRoot, "Right_Eye", PrimitiveType.Sphere, new Vector3(0.10f, 1.63f, -0.34f), new Vector3(0.10f, 0.07f, 0.035f), Quaternion.identity, white, layer);
            CreatePart(bodyRoot, "Smile", PrimitiveType.Cube, new Vector3(0f, 1.48f, -0.36f), new Vector3(0.24f, 0.055f, 0.025f), Quaternion.identity, white, layer);
            CreatePart(bodyRoot, "Mask_Crest", PrimitiveType.Sphere, new Vector3(0f, 1.86f, -0.11f), new Vector3(0.11f, 0.12f, 0.05f), Quaternion.identity, gold, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Leg", PrimitiveType.Capsule, new Vector3(-0.20f, 0.30f, 0.02f), new Vector3(0.23f, 0.48f, 0.22f), Quaternion.identity, pants, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Leg", PrimitiveType.Capsule, new Vector3(0.20f, 0.30f, 0.02f), new Vector3(0.23f, 0.48f, 0.22f), Quaternion.identity, pants, layer);
            Transform leftFoot = CreatePart(bodyRoot, "Left_Boot", PrimitiveType.Cube, new Vector3(-0.20f, 0.07f, -0.07f), new Vector3(0.27f, 0.12f, 0.35f), Quaternion.identity, pants, layer);
            Transform rightFoot = CreatePart(bodyRoot, "Right_Boot", PrimitiveType.Cube, new Vector3(0.20f, 0.07f, -0.07f), new Vector3(0.27f, 0.12f, 0.35f), Quaternion.identity, pants, layer);

            Transform leftArm = CreateRiggedPart(bodyRoot, "Left_Muscle_Arm", PrimitiveType.Capsule, new Vector3(-0.55f, 0.98f, -0.10f), new Vector3(0.24f, 0.58f, 0.24f), Quaternion.Euler(44f, 0f, -30f), skin, layer);
            Transform rightArm = CreateRiggedPart(bodyRoot, "Right_Muscle_Arm", PrimitiveType.Capsule, new Vector3(0.55f, 0.98f, -0.10f), new Vector3(0.24f, 0.58f, 0.24f), Quaternion.Euler(44f, 0f, 30f), skin, layer);
            CreatePart(bodyRoot, "Left_Wristband", PrimitiveType.Cylinder, new Vector3(-0.63f, 0.75f, -0.22f), new Vector3(0.13f, 0.055f, 0.13f), Quaternion.Euler(70f, 0f, -30f), mask, layer);
            CreatePart(bodyRoot, "Right_Wristband", PrimitiveType.Cylinder, new Vector3(0.63f, 0.75f, -0.22f), new Vector3(0.13f, 0.055f, 0.13f), Quaternion.Euler(70f, 0f, 30f), mask, layer);
            CreatePart(bodyRoot, "Left_Shoulder", PrimitiveType.Sphere, new Vector3(-0.40f, 1.17f, -0.01f), new Vector3(0.27f, 0.27f, 0.24f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Right_Shoulder", PrimitiveType.Sphere, new Vector3(0.40f, 1.17f, -0.01f), new Vector3(0.27f, 0.27f, 0.24f), Quaternion.identity, skin, layer);

            Transform rightFist = CreateAnchor(rightArm, "Right_Fist_Point", new Vector3(0f, -0.03f, -0.34f), layer);
            Transform leftFist = CreateAnchor(leftArm, "Left_Fist_Point", new Vector3(0f, -0.03f, -0.34f), layer);
            Transform castPoint = CreateAnchor(bodyRoot, "CastPoint", new Vector3(0f, 1.10f, -0.58f), layer);
            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(rightFist, leftFist, castPoint);

            BrawlerProceduralModelAnimator animator = root.AddComponent<BrawlerProceduralModelAnimator>();
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftFoot, rightFoot, null, null);

            return root;
        }

        private static GameObject BuildByron(Transform parent, BrawlerController owner)
        {
            int layer = parent.gameObject.layer;
            Material skin = CreateMaterial("Procedural Byron Skin", new Color(0.93f, 0.78f, 0.62f, 1f), 0.18f);
            Material hair = CreateMaterial("Procedural Byron Hair", new Color(0.82f, 0.80f, 0.94f, 1f), 0.30f);
            Material coat = CreateMaterial("Procedural Byron Coat", new Color(0.02f, 0.42f, 0.34f, 1f), 0.34f);
            Material vest = CreateMaterial("Procedural Byron Vest", new Color(0.84f, 0.90f, 0.36f, 1f), 0.22f);
            Material pants = CreateMaterial("Procedural Byron Pants", new Color(0.18f, 0.10f, 0.34f, 1f), 0.24f);
            Material boot = CreateMaterial("Procedural Byron Boots", new Color(0.05f, 0.04f, 0.05f, 1f), 0.18f);
            Material glass = CreateMaterial("Procedural Byron Glasses", new Color(0.95f, 0.34f, 0.82f, 1f), 0.42f);
            Material staff = CreateMaterial("Procedural Byron Staff", new Color(0.58f, 0.05f, 0.09f, 1f), 0.35f);
            Material staffOrb = CreateMaterial("Procedural Byron Staff Orb", new Color(0.70f, 0.92f, 1.00f, 1f), 0.48f);

            GameObject root = new GameObject("Procedural_Byron_Model");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = layer;

            Transform bodyRoot = new GameObject("BodyRig").transform;
            bodyRoot.SetParent(root.transform, false);
            bodyRoot.localPosition = Vector3.zero;
            bodyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bodyRoot.localScale = new Vector3(0.98f, 1.02f, 0.98f);
            bodyRoot.gameObject.layer = layer;

            CreatePart(bodyRoot, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.56f, 0f), new Vector3(0.50f, 0.18f, 0.34f), Quaternion.identity, pants, layer);
            Transform torso = CreatePart(bodyRoot, "Torso_Coat", PrimitiveType.Capsule, new Vector3(0f, 0.94f, 0f), new Vector3(0.58f, 0.68f, 0.42f), Quaternion.identity, coat, layer);
            CreatePart(bodyRoot, "Vest_Front", PrimitiveType.Cube, new Vector3(0f, 0.98f, -0.31f), new Vector3(0.30f, 0.46f, 0.045f), Quaternion.identity, vest, layer);
            CreatePart(bodyRoot, "Tie", PrimitiveType.Cube, new Vector3(0f, 1.10f, -0.36f), new Vector3(0.10f, 0.20f, 0.035f), Quaternion.identity, glass, layer);
            CreatePart(bodyRoot, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.32f, 0f), new Vector3(0.15f, 0.09f, 0.15f), Quaternion.identity, skin, layer);
            Transform head = CreatePart(bodyRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.55f, -0.01f), new Vector3(0.40f, 0.45f, 0.36f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Hair_Swept_Back", PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0.02f), new Vector3(0.42f, 0.20f, 0.34f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Beard", PrimitiveType.Cube, new Vector3(0f, 1.39f, -0.25f), new Vector3(0.32f, 0.18f, 0.045f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Left_Glass", PrimitiveType.Cylinder, new Vector3(-0.095f, 1.58f, -0.31f), new Vector3(0.075f, 0.025f, 0.075f), Quaternion.Euler(90f, 0f, 0f), glass, layer);
            CreatePart(bodyRoot, "Right_Glass", PrimitiveType.Cylinder, new Vector3(0.095f, 1.58f, -0.31f), new Vector3(0.075f, 0.025f, 0.075f), Quaternion.Euler(90f, 0f, 0f), glass, layer);
            CreatePart(bodyRoot, "Glasses_Bridge", PrimitiveType.Cube, new Vector3(0f, 1.58f, -0.31f), new Vector3(0.07f, 0.025f, 0.025f), Quaternion.identity, glass, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Leg", PrimitiveType.Capsule, new Vector3(-0.16f, 0.30f, 0.02f), new Vector3(0.17f, 0.46f, 0.17f), Quaternion.identity, pants, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Leg", PrimitiveType.Capsule, new Vector3(0.16f, 0.30f, 0.02f), new Vector3(0.17f, 0.46f, 0.17f), Quaternion.identity, pants, layer);
            Transform leftFoot = CreatePart(bodyRoot, "Left_Boot", PrimitiveType.Cube, new Vector3(-0.16f, 0.06f, -0.06f), new Vector3(0.22f, 0.11f, 0.32f), Quaternion.identity, boot, layer);
            Transform rightFoot = CreatePart(bodyRoot, "Right_Boot", PrimitiveType.Cube, new Vector3(0.16f, 0.06f, -0.06f), new Vector3(0.22f, 0.11f, 0.32f), Quaternion.identity, boot, layer);

            Transform leftArm = CreateRiggedPart(bodyRoot, "Left_Arm", PrimitiveType.Capsule, new Vector3(-0.45f, 0.94f, -0.08f), new Vector3(0.14f, 0.45f, 0.14f), Quaternion.Euler(42f, 0f, -20f), coat, layer);
            Transform rightArm = CreateRiggedPart(bodyRoot, "Right_Arm", PrimitiveType.Capsule, new Vector3(0.46f, 0.88f, -0.11f), new Vector3(0.14f, 0.48f, 0.14f), Quaternion.Euler(60f, 0f, 18f), coat, layer);
            CreatePart(bodyRoot, "Left_Shoulder", PrimitiveType.Sphere, new Vector3(-0.33f, 1.10f, -0.01f), new Vector3(0.20f, 0.20f, 0.18f), Quaternion.identity, coat, layer);
            CreatePart(bodyRoot, "Right_Shoulder", PrimitiveType.Sphere, new Vector3(0.33f, 1.10f, -0.01f), new Vector3(0.20f, 0.20f, 0.18f), Quaternion.identity, coat, layer);

            Transform staffProp = CreateStaff(bodyRoot, "Staff", new Vector3(0.46f, 0.68f, -0.40f), 10f, staff, staffOrb, layer, out Transform staffTip);
            AttachKeepingWorld(staffProp, rightArm);

            Transform castPoint = CreateAnchor(bodyRoot, "CastPoint", new Vector3(0f, 1.06f, -0.55f), layer);
            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(staffTip, staffTip, castPoint);

            BrawlerProceduralModelAnimator animator = root.AddComponent<BrawlerProceduralModelAnimator>();
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftFoot, rightFoot, null, staffProp);

            return root;
        }

        private static GameObject BuildPiper(Transform parent, BrawlerController owner)
        {
            int layer = parent.gameObject.layer;
            Material skin = CreateMaterial("Procedural Piper Skin", new Color(1.00f, 0.75f, 0.58f, 1f), 0.18f);
            Material hair = CreateMaterial("Procedural Piper Hair", new Color(1.00f, 0.86f, 0.22f, 1f), 0.28f);
            Material dress = CreateMaterial("Procedural Piper Dress", new Color(0.42f, 0.78f, 1.00f, 1f), 0.30f);
            Material sash = CreateMaterial("Procedural Piper Sash", new Color(1.00f, 0.34f, 0.67f, 1f), 0.30f);
            Material boot = CreateMaterial("Procedural Piper Boots", new Color(0.66f, 0.20f, 0.18f, 1f), 0.20f);
            Material umbrellaCanopy = CreateMaterial("Procedural Piper Umbrella Canopy", new Color(1.00f, 0.45f, 0.75f, 1f), 0.36f);
            Material umbrellaHandle = CreateMaterial("Procedural Piper Umbrella Handle", new Color(0.28f, 0.18f, 0.14f, 1f), 0.30f);
            Material umbrellaTip = CreateMaterial("Procedural Piper Umbrella Tip", new Color(0.72f, 0.85f, 1.00f, 1f), 0.50f);

            GameObject root = new GameObject("Procedural_Piper_Model");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = layer;

            Transform bodyRoot = new GameObject("BodyRig").transform;
            bodyRoot.SetParent(root.transform, false);
            bodyRoot.localPosition = Vector3.zero;
            bodyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bodyRoot.localScale = new Vector3(0.92f, 1.02f, 0.92f);
            bodyRoot.gameObject.layer = layer;

            CreatePart(bodyRoot, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.56f, 0f), new Vector3(0.46f, 0.16f, 0.32f), Quaternion.identity, dress, layer);
            Transform torso = CreatePart(bodyRoot, "Dress_Torso", PrimitiveType.Capsule, new Vector3(0f, 0.92f, 0f), new Vector3(0.50f, 0.58f, 0.36f), Quaternion.identity, dress, layer);
            CreatePart(bodyRoot, "Dress_Skirt", PrimitiveType.Cylinder, new Vector3(0f, 0.62f, 0f), new Vector3(0.36f, 0.13f, 0.36f), Quaternion.identity, sash, layer);
            CreatePart(bodyRoot, "Sash_Front", PrimitiveType.Cube, new Vector3(0f, 0.96f, -0.30f), new Vector3(0.30f, 0.38f, 0.04f), Quaternion.Euler(0f, 0f, -8f), sash, layer);
            CreatePart(bodyRoot, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.29f, 0f), new Vector3(0.14f, 0.08f, 0.14f), Quaternion.identity, skin, layer);
            Transform head = CreatePart(bodyRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.51f, -0.01f), new Vector3(0.38f, 0.43f, 0.35f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Hair_Crown", PrimitiveType.Sphere, new Vector3(0f, 1.67f, -0.02f), new Vector3(0.41f, 0.20f, 0.37f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Long_Hair_Back", PrimitiveType.Capsule, new Vector3(0f, 1.25f, 0.18f), new Vector3(0.34f, 0.58f, 0.22f), Quaternion.identity, hair, layer);
            CreatePart(bodyRoot, "Side_Bang", PrimitiveType.Cube, new Vector3(-0.18f, 1.58f, -0.24f), new Vector3(0.15f, 0.28f, 0.08f), Quaternion.Euler(0f, 0f, -18f), hair, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Leg", PrimitiveType.Capsule, new Vector3(-0.13f, 0.28f, 0.02f), new Vector3(0.14f, 0.42f, 0.14f), Quaternion.identity, skin, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Leg", PrimitiveType.Capsule, new Vector3(0.13f, 0.28f, 0.02f), new Vector3(0.14f, 0.42f, 0.14f), Quaternion.identity, skin, layer);
            Transform leftFoot = CreatePart(bodyRoot, "Left_Boot", PrimitiveType.Cube, new Vector3(-0.13f, 0.06f, -0.06f), new Vector3(0.19f, 0.10f, 0.29f), Quaternion.identity, boot, layer);
            Transform rightFoot = CreatePart(bodyRoot, "Right_Boot", PrimitiveType.Cube, new Vector3(0.13f, 0.06f, -0.06f), new Vector3(0.19f, 0.10f, 0.29f), Quaternion.identity, boot, layer);

            Transform leftArm = CreateRiggedPart(bodyRoot, "Left_Arm", PrimitiveType.Capsule, new Vector3(-0.40f, 0.94f, -0.08f), new Vector3(0.13f, 0.42f, 0.13f), Quaternion.Euler(36f, 0f, -16f), skin, layer);
            Transform rightArm = CreateRiggedPart(bodyRoot, "Right_Arm", PrimitiveType.Capsule, new Vector3(0.42f, 0.89f, -0.12f), new Vector3(0.13f, 0.45f, 0.13f), Quaternion.Euler(60f, 0f, 18f), skin, layer);
            CreatePart(bodyRoot, "Left_Puff_Sleeve", PrimitiveType.Sphere, new Vector3(-0.29f, 1.08f, -0.01f), new Vector3(0.19f, 0.19f, 0.17f), Quaternion.identity, dress, layer);
            CreatePart(bodyRoot, "Right_Puff_Sleeve", PrimitiveType.Sphere, new Vector3(0.29f, 1.08f, -0.01f), new Vector3(0.19f, 0.19f, 0.17f), Quaternion.identity, dress, layer);

            Transform umbrella = CreateUmbrella(bodyRoot, "Umbrella", new Vector3(0.42f, 0.72f, -0.50f), 6f, umbrellaCanopy, umbrellaHandle, umbrellaTip, layer, out Transform umbrellaMuzzle);
            AttachKeepingWorld(umbrella, rightArm);

            Transform castPoint = CreateAnchor(bodyRoot, "CastPoint", new Vector3(0f, 1.05f, -0.54f), layer);
            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(umbrellaMuzzle, umbrellaMuzzle, castPoint);

            BrawlerProceduralModelAnimator animator = root.AddComponent<BrawlerProceduralModelAnimator>();
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftFoot, rightFoot, null, umbrella);

            return root;
        }

        private static GameObject BuildLeon(Transform parent, BrawlerController owner)
        {
            int layer = parent.gameObject.layer;
            Material skin = CreateMaterial("Procedural Leon Skin", new Color(0.48f, 0.28f, 0.18f, 1f), 0.18f);
            Material hoodie = CreateMaterial("Procedural Leon Hoodie", new Color(0.08f, 0.72f, 0.39f, 1f), 0.32f);
            Material hoodAccent = CreateMaterial("Procedural Leon Hood Accent", new Color(0.98f, 0.76f, 0.10f, 1f), 0.30f);
            Material shorts = CreateMaterial("Procedural Leon Shorts", new Color(0.09f, 0.32f, 0.86f, 1f), 0.30f);
            Material shoe = CreateMaterial("Procedural Leon Shoes", new Color(0.18f, 0.10f, 0.06f, 1f), 0.20f);
            Material zipper = CreateMaterial("Procedural Leon Zipper", new Color(0.90f, 0.95f, 0.92f, 1f), 0.28f);
            Material button = CreateMaterial("Procedural Leon Button", new Color(0.05f, 0.10f, 0.15f, 1f), 0.25f);

            GameObject root = new GameObject("Procedural_Leon_Model");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = layer;

            Transform bodyRoot = new GameObject("BodyRig").transform;
            bodyRoot.SetParent(root.transform, false);
            bodyRoot.localPosition = Vector3.zero;
            bodyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bodyRoot.localScale = new Vector3(0.82f, 0.88f, 0.82f);
            bodyRoot.gameObject.layer = layer;

            CreatePart(bodyRoot, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(0.48f, 0.16f, 0.32f), Quaternion.identity, shorts, layer);
            Transform torso = CreatePart(bodyRoot, "Hoodie_Torso", PrimitiveType.Capsule, new Vector3(0f, 0.90f, 0f), new Vector3(0.54f, 0.56f, 0.38f), Quaternion.identity, hoodie, layer);
            CreatePart(bodyRoot, "Zipper", PrimitiveType.Cube, new Vector3(0f, 0.98f, -0.30f), new Vector3(0.055f, 0.42f, 0.035f), Quaternion.identity, zipper, layer);
            CreatePart(bodyRoot, "Hoodie_Pocket", PrimitiveType.Cube, new Vector3(0f, 0.78f, -0.31f), new Vector3(0.28f, 0.12f, 0.035f), Quaternion.identity, hoodAccent, layer);
            CreatePart(bodyRoot, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.25f, 0f), new Vector3(0.13f, 0.08f, 0.13f), Quaternion.identity, skin, layer);
            Transform head = CreatePart(bodyRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.46f, -0.01f), new Vector3(0.36f, 0.39f, 0.34f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Hood", PrimitiveType.Sphere, new Vector3(0f, 1.50f, 0.01f), new Vector3(0.46f, 0.44f, 0.40f), Quaternion.identity, hoodie, layer);
            CreatePart(bodyRoot, "Hood_Face_Open", PrimitiveType.Sphere, new Vector3(0f, 1.47f, -0.19f), new Vector3(0.32f, 0.31f, 0.18f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Hood_Brow", PrimitiveType.Cube, new Vector3(0f, 1.67f, -0.25f), new Vector3(0.36f, 0.07f, 0.055f), Quaternion.identity, hoodAccent, layer);
            CreatePart(bodyRoot, "Hood_Button_Left", PrimitiveType.Cylinder, new Vector3(-0.18f, 1.70f, -0.20f), new Vector3(0.07f, 0.025f, 0.07f), Quaternion.Euler(90f, 0f, 0f), button, layer);
            CreatePart(bodyRoot, "Hood_Button_Right", PrimitiveType.Cylinder, new Vector3(0.18f, 1.70f, -0.20f), new Vector3(0.07f, 0.025f, 0.07f), Quaternion.Euler(90f, 0f, 0f), button, layer);

            Transform leftLeg = CreatePart(bodyRoot, "Left_Leg", PrimitiveType.Capsule, new Vector3(-0.14f, 0.28f, 0.02f), new Vector3(0.14f, 0.40f, 0.14f), Quaternion.identity, skin, layer);
            Transform rightLeg = CreatePart(bodyRoot, "Right_Leg", PrimitiveType.Capsule, new Vector3(0.14f, 0.28f, 0.02f), new Vector3(0.14f, 0.40f, 0.14f), Quaternion.identity, skin, layer);
            Transform leftFoot = CreatePart(bodyRoot, "Left_Shoe", PrimitiveType.Cube, new Vector3(-0.14f, 0.06f, -0.06f), new Vector3(0.20f, 0.10f, 0.29f), Quaternion.identity, shoe, layer);
            Transform rightFoot = CreatePart(bodyRoot, "Right_Shoe", PrimitiveType.Cube, new Vector3(0.14f, 0.06f, -0.06f), new Vector3(0.20f, 0.10f, 0.29f), Quaternion.identity, shoe, layer);

            Transform leftArm = CreateRiggedPart(bodyRoot, "Left_Sleeve", PrimitiveType.Capsule, new Vector3(-0.41f, 0.91f, -0.10f), new Vector3(0.14f, 0.42f, 0.14f), Quaternion.Euler(48f, 0f, -18f), hoodie, layer);
            Transform rightArm = CreateRiggedPart(bodyRoot, "Right_Sleeve", PrimitiveType.Capsule, new Vector3(0.41f, 0.91f, -0.10f), new Vector3(0.14f, 0.42f, 0.14f), Quaternion.Euler(48f, 0f, 18f), hoodie, layer);
            CreatePart(leftArm, "Left_Hand", PrimitiveType.Sphere, new Vector3(0f, -0.03f, -0.30f), new Vector3(0.12f, 0.10f, 0.12f), Quaternion.identity, skin, layer);
            CreatePart(rightArm, "Right_Hand", PrimitiveType.Sphere, new Vector3(0f, -0.03f, -0.30f), new Vector3(0.12f, 0.10f, 0.12f), Quaternion.identity, skin, layer);
            CreatePart(bodyRoot, "Left_Shoulder", PrimitiveType.Sphere, new Vector3(-0.30f, 1.06f, -0.01f), new Vector3(0.18f, 0.18f, 0.16f), Quaternion.identity, hoodie, layer);
            CreatePart(bodyRoot, "Right_Shoulder", PrimitiveType.Sphere, new Vector3(0.30f, 1.06f, -0.01f), new Vector3(0.18f, 0.18f, 0.16f), Quaternion.identity, hoodie, layer);

            Transform rightThrow = CreateAnchor(rightArm, "Right_Throw_Point", new Vector3(0f, -0.03f, -0.40f), layer);
            Transform leftThrow = CreateAnchor(leftArm, "Left_Throw_Point", new Vector3(0f, -0.03f, -0.40f), layer);
            Transform castPoint = CreateAnchor(bodyRoot, "CastPoint", new Vector3(0f, 1.02f, -0.52f), layer);
            BrawlerPresentationAnchors anchors = root.AddComponent<BrawlerPresentationAnchors>();
            anchors.Configure(rightThrow, leftThrow, castPoint);

            BrawlerProceduralModelAnimator animator = root.AddComponent<BrawlerProceduralModelAnimator>();
            animator.Initialize(owner, bodyRoot, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftFoot, rightFoot, null, null);

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

        private static Transform CreateBottle(
            Transform parent,
            string name,
            Vector3 localPosition,
            float yawDegrees,
            Material glassMaterial,
            Material corkMaterial,
            int layer,
            out Transform mouth)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.Euler(-18f, yawDegrees, 0f);
            root.localScale = Vector3.one;
            root.gameObject.layer = layer;

            CreatePart(root, "Bottle_Body", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.06f), new Vector3(0.09f, 0.18f, 0.09f), Quaternion.Euler(90f, 0f, 0f), glassMaterial, layer);
            CreatePart(root, "Bottle_Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.00f, -0.26f), new Vector3(0.045f, 0.10f, 0.045f), Quaternion.Euler(90f, 0f, 0f), glassMaterial, layer);
            CreatePart(root, "Bottle_Cork", PrimitiveType.Cylinder, new Vector3(0f, 0.00f, -0.38f), new Vector3(0.040f, 0.045f, 0.040f), Quaternion.Euler(90f, 0f, 0f), corkMaterial, layer);
            mouth = CreateAnchor(root, "Bottle_Mouth", new Vector3(0f, 0f, -0.47f), layer);
            return root;
        }

        private static Transform CreateStaff(
            Transform parent,
            string name,
            Vector3 localPosition,
            float yawDegrees,
            Material shaftMaterial,
            Material orbMaterial,
            int layer,
            out Transform tip)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.Euler(-8f, yawDegrees, -8f);
            root.localScale = Vector3.one;
            root.gameObject.layer = layer;

            CreatePart(root, "Staff_Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.035f, 0.55f, 0.035f), Quaternion.identity, shaftMaterial, layer);
            CreatePart(root, "Staff_Orb", PrimitiveType.Sphere, new Vector3(0f, 0.82f, -0.03f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity, orbMaterial, layer);
            tip = CreateAnchor(root, "Staff_Tip", new Vector3(0f, 0.88f, -0.08f), layer);
            return root;
        }

        private static Transform CreateUmbrella(
            Transform parent,
            string name,
            Vector3 localPosition,
            float yawDegrees,
            Material canopyMaterial,
            Material handleMaterial,
            Material tipMaterial,
            int layer,
            out Transform muzzle)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.Euler(-12f, yawDegrees, 0f);
            root.localScale = Vector3.one;
            root.gameObject.layer = layer;

            CreatePart(root, "Umbrella_Handle", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.12f), new Vector3(0.035f, 0.33f, 0.035f), Quaternion.Euler(90f, 0f, 0f), handleMaterial, layer);
            CreatePart(root, "Umbrella_Canopy", PrimitiveType.Sphere, new Vector3(0f, 0.02f, -0.45f), new Vector3(0.26f, 0.12f, 0.26f), Quaternion.identity, canopyMaterial, layer);
            CreatePart(root, "Umbrella_Tip", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, -0.69f), new Vector3(0.045f, 0.075f, 0.045f), Quaternion.Euler(90f, 0f, 0f), tipMaterial, layer);
            muzzle = CreateAnchor(root, "Umbrella_Muzzle", new Vector3(0f, 0.02f, -0.80f), layer);
            return root;
        }

        private static Transform CreateBow(
            Transform parent,
            string name,
            Vector3 localPosition,
            float yawDegrees,
            Material woodMaterial,
            Material stringMaterial,
            int layer,
            out Transform muzzle)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            root.localScale = Vector3.one;
            root.gameObject.layer = layer;

            CreatePart(root, "Grip", PrimitiveType.Cube, new Vector3(0f, 0f, -0.06f), new Vector3(0.09f, 0.26f, 0.07f), Quaternion.identity, woodMaterial, layer);
            CreatePart(root, "Upper_Limb", PrimitiveType.Cylinder, new Vector3(0f, 0.30f, -0.07f), new Vector3(0.035f, 0.30f, 0.035f), Quaternion.Euler(0f, 0f, -16f), woodMaterial, layer);
            CreatePart(root, "Lower_Limb", PrimitiveType.Cylinder, new Vector3(0f, -0.30f, -0.07f), new Vector3(0.035f, 0.30f, 0.035f), Quaternion.Euler(0f, 0f, 16f), woodMaterial, layer);
            CreatePart(root, "String", PrimitiveType.Cube, new Vector3(0f, 0f, 0.12f), new Vector3(0.018f, 0.70f, 0.018f), Quaternion.identity, stringMaterial, layer);
            CreatePart(root, "Arrow_Rest", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.25f), new Vector3(0.018f, 0.34f, 0.018f), Quaternion.Euler(90f, 0f, 0f), stringMaterial, layer);
            muzzle = CreateAnchor(root, "Bow_Muzzle", new Vector3(0f, 0f, -0.50f), layer);
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
