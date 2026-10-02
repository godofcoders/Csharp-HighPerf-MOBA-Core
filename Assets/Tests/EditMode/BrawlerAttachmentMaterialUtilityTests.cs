using MOBA.Core.Infrastructure;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Tests.EditMode
{
    public sealed class BrawlerAttachmentMaterialUtilityTests
    {
        [Test]
        public void RequiresReplacement_ReplacesBuiltInShadersUnderUrp()
        {
            Assert.IsTrue(BrawlerAttachmentMaterialUtility.RequiresReplacement(
                "Standard",
                shaderSupported: true,
                hasScriptableRenderPipeline: true));
            Assert.IsTrue(BrawlerAttachmentMaterialUtility.RequiresReplacement(
                "Legacy Shaders/Diffuse",
                shaderSupported: true,
                hasScriptableRenderPipeline: true));
        }

        [Test]
        public void RequiresReplacement_PreservesSupportedUrpAndCustomShaders()
        {
            Assert.IsFalse(BrawlerAttachmentMaterialUtility.RequiresReplacement(
                "Universal Render Pipeline/Lit",
                shaderSupported: true,
                hasScriptableRenderPipeline: true));
            Assert.IsFalse(BrawlerAttachmentMaterialUtility.RequiresReplacement(
                "MOBA/WeaponGlow",
                shaderSupported: true,
                hasScriptableRenderPipeline: true));
        }

        [Test]
        public void RequiresReplacement_PreservesStandardShaderWithoutSrp()
        {
            Assert.IsFalse(BrawlerAttachmentMaterialUtility.RequiresReplacement(
                "Standard",
                shaderSupported: true,
                hasScriptableRenderPipeline: false));
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("Hidden/InternalErrorShader", true)]
        [TestCase("MOBA/Unsupported", false)]
        public void RequiresReplacement_ReplacesMissingOrUnsupportedShaders(
            string shaderName,
            bool shaderSupported)
        {
            Assert.IsTrue(BrawlerAttachmentMaterialUtility.RequiresReplacement(
                shaderName,
                shaderSupported,
                hasScriptableRenderPipeline: true));
        }

        [Test]
        public void RepairUnsupportedMaterials_UsesUrpAndPreservesWeaponSurface()
        {
            Assert.IsNotNull(GraphicsSettings.currentRenderPipeline);
            Shader standardShader = Shader.Find("Standard");
            Assert.IsNotNull(standardShader);

            GameObject weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Texture2D texture = new Texture2D(2, 2);
            Material source = new Material(standardShader);
            List<Material> generated = new List<Material>();
            Color tint = new Color(0.25f, 0.5f, 0.75f, 1f);

            try
            {
                source.SetTexture("_MainTex", texture);
                source.SetTextureScale("_MainTex", new Vector2(1.5f, 0.75f));
                source.SetTextureOffset("_MainTex", new Vector2(0.1f, 0.2f));
                source.SetColor("_Color", tint);
                source.SetFloat("_Metallic", 0.35f);
                source.SetFloat("_Glossiness", 0.65f);
                weapon.GetComponent<Renderer>().sharedMaterial = source;

                int repaired = BrawlerAttachmentMaterialUtility.RepairUnsupportedMaterials(
                    weapon,
                    generated);

                Material result = weapon.GetComponent<Renderer>().sharedMaterial;
                Assert.AreEqual(1, repaired);
                Assert.AreEqual(1, generated.Count);
                Assert.AreNotSame(source, result);
                Assert.AreEqual("Universal Render Pipeline/Lit", result.shader.name);
                Assert.AreSame(texture, result.GetTexture("_BaseMap"));
                Assert.AreEqual(new Vector2(1.5f, 0.75f), result.GetTextureScale("_BaseMap"));
                Assert.AreEqual(new Vector2(0.1f, 0.2f), result.GetTextureOffset("_BaseMap"));
                Color resultTint = result.GetColor("_BaseColor");
                Assert.AreEqual(tint.r, resultTint.r, 0.001f);
                Assert.AreEqual(tint.g, resultTint.g, 0.001f);
                Assert.AreEqual(tint.b, resultTint.b, 0.001f);
                Assert.AreEqual(tint.a, resultTint.a, 0.001f);
                Assert.AreEqual(0.35f, result.GetFloat("_Metallic"), 0.001f);
                Assert.AreEqual(0.65f, result.GetFloat("_Smoothness"), 0.001f);
            }
            finally
            {
                for (int i = 0; i < generated.Count; i++)
                    Object.DestroyImmediate(generated[i]);

                Object.DestroyImmediate(source);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(weapon);
            }
        }

        [TestCase("Assets/_Game/Art/Weapons/BlasterKit2/Barley/blaster-d.obj", "Assets/_Game/Art/Weapons/BlasterKit2/Barley/Textures/colormap.png")]
        [TestCase("Assets/_Game/Art/Weapons/BlasterKit2/Bo/blaster-e.obj", "Assets/_Game/Art/Weapons/BlasterKit2/Bo/Textures/colormap.png")]
        [TestCase("Assets/_Game/Art/Weapons/BlasterKit2/Byron/blaster-b.obj", "Assets/_Game/Art/Weapons/BlasterKit2/Byron/Textures/colormap.png")]
        [TestCase("Assets/_Game/Art/Weapons/BlasterKit2/Colt/Colt_BlasterA.obj", "Assets/_Game/Art/Weapons/BlasterKit2/Colt/Textures/colormap.png")]
        [TestCase("Assets/_Game/Art/Weapons/BlasterKit2/Jessie/blaster-f.obj", "Assets/_Game/Art/Weapons/BlasterKit2/Jessie/Textures/colormap.png")]
        [TestCase("Assets/_Game/Art/Weapons/BlasterKit2/Leon/blaster-c.obj", "Assets/_Game/Art/Weapons/BlasterKit2/Leon/Textures/colormap.png")]
        [TestCase("Assets/_Game/Art/Weapons/BlasterKit2/Piper/blaster-g.obj", "Assets/_Game/Art/Weapons/BlasterKit2/Piper/Textures/colormap.png")]
        public void ImportedWeapon_ResolvesToSupportedTexturedMaterial(
            string assetPath,
            string texturePath)
        {
            GameObject weaponAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.IsNotNull(weaponAsset, assetPath);
            Texture2D fallbackTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Assert.IsNotNull(fallbackTexture, texturePath);

            GameObject weapon = Object.Instantiate(weaponAsset);
            List<Material> generated = new List<Material>();
            try
            {
                BrawlerAttachmentMaterialUtility.RepairUnsupportedMaterials(
                    weapon,
                    generated,
                    fallbackTexture);

                Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0), assetPath);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Material[] materials = renderers[rendererIndex].sharedMaterials;
                    Assert.That(materials.Length, Is.GreaterThan(0), assetPath);
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        Material material = materials[materialIndex];
                        Assert.IsNotNull(material, assetPath);
                        Assert.IsFalse(
                            BrawlerAttachmentMaterialUtility.RequiresReplacement(
                                material.shader != null ? material.shader.name : null,
                                material.shader != null && material.shader.isSupported,
                                hasScriptableRenderPipeline: true),
                            assetPath);

                        Texture texture = material.HasProperty("_BaseMap")
                            ? material.GetTexture("_BaseMap")
                            : material.HasProperty("_MainTex")
                                ? material.GetTexture("_MainTex")
                                : null;
                        Assert.IsNotNull(texture, assetPath);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < generated.Count; i++)
                    Object.DestroyImmediate(generated[i]);

                Object.DestroyImmediate(weapon);
            }
        }
    }
}
