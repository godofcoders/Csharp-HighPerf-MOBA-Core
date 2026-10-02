using MOBA.Core.Infrastructure;
using NUnit.Framework;
using System.Collections.Generic;
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
    }
}
