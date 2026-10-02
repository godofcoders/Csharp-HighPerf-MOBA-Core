using MOBA.Core.Infrastructure;
using NUnit.Framework;

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
    }
}
