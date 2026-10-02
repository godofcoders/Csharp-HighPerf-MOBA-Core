using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public static class BrawlerAttachmentMaterialUtility
    {
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string UrpSimpleLitShaderName = "Universal Render Pipeline/Simple Lit";
        private const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";
        private const string BuiltInStandardShaderName = "Standard";
        private const string InternalErrorShaderName = "Hidden/InternalErrorShader";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

        public static bool RequiresReplacement(
            string shaderName,
            bool shaderSupported,
            bool hasScriptableRenderPipeline)
        {
            if (!shaderSupported ||
                string.IsNullOrWhiteSpace(shaderName) ||
                shaderName == InternalErrorShaderName)
            {
                return true;
            }

            if (!hasScriptableRenderPipeline)
                return false;

            return shaderName == BuiltInStandardShaderName ||
                   shaderName.StartsWith("Legacy Shaders/");
        }

        public static int RepairUnsupportedMaterials(
            GameObject attachmentRoot,
            ICollection<Material> generatedMaterials)
        {
            if (attachmentRoot == null)
                return 0;

            Renderer[] renderers = attachmentRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return 0;

            bool hasScriptableRenderPipeline = GraphicsSettings.currentRenderPipeline != null;
            Dictionary<Material, Material> replacements = new Dictionary<Material, Material>();
            Material nullReplacement = null;
            int repairedSlots = 0;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    Shader sourceShader = source != null ? source.shader : null;
                    if (!RequiresReplacement(
                            sourceShader != null ? sourceShader.name : null,
                            sourceShader != null && sourceShader.isSupported,
                            hasScriptableRenderPipeline))
                    {
                        continue;
                    }

                    Material replacement;
                    if (source == null)
                    {
                        replacement = nullReplacement;
                        if (replacement == null)
                        {
                            replacement = CreateCompatibleMaterial(null);
                            nullReplacement = replacement;
                            RegisterGeneratedMaterial(replacement, generatedMaterials);
                        }
                    }
                    else if (!replacements.TryGetValue(source, out replacement))
                    {
                        replacement = CreateCompatibleMaterial(source);
                        replacements[source] = replacement;
                        RegisterGeneratedMaterial(replacement, generatedMaterials);
                    }

                    if (replacement == null)
                        continue;

                    materials[materialIndex] = replacement;
                    repairedSlots++;
                    changed = true;
                }

                if (changed)
                    renderer.sharedMaterials = materials;
            }

            return repairedSlots;
        }

        public static Material CreateCompatibleMaterial(Material source)
        {
            Shader shader =
                Shader.Find(UrpLitShaderName) ??
                Shader.Find(UrpSimpleLitShaderName) ??
                Shader.Find(UrpUnlitShaderName) ??
                Shader.Find("Unlit/Texture") ??
                Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            Material material = new Material(shader)
            {
                name = source != null
                    ? source.name + "_RuntimeCompatible"
                    : "Weapon_RuntimeCompatible",
                hideFlags = HideFlags.DontSave
            };

            if (source == null)
                return material;

            CopyTexture(source, material);
            CopyColor(source, material);
            CopyFloat(source, material, MetallicId);
            CopySmoothness(source, material);
            return material;
        }

        private static void CopyTexture(Material source, Material destination)
        {
            const string baseMap = "_BaseMap";
            const string mainTexture = "_MainTex";
            string sourceProperty = source.HasProperty(baseMap)
                ? baseMap
                : source.HasProperty(mainTexture)
                    ? mainTexture
                    : null;
            if (sourceProperty == null)
                return;

            Texture texture = source.GetTexture(sourceProperty);
            Vector2 scale = source.GetTextureScale(sourceProperty);
            Vector2 offset = source.GetTextureOffset(sourceProperty);
            SetTexture(destination, baseMap, texture, scale, offset);
            SetTexture(destination, mainTexture, texture, scale, offset);
        }

        private static void SetTexture(
            Material destination,
            string property,
            Texture texture,
            Vector2 scale,
            Vector2 offset)
        {
            if (!destination.HasProperty(property))
                return;

            destination.SetTexture(property, texture);
            destination.SetTextureScale(property, scale);
            destination.SetTextureOffset(property, offset);
        }

        private static void CopyColor(Material source, Material destination)
        {
            Color color = source.HasProperty(BaseColorId)
                ? source.GetColor(BaseColorId)
                : source.HasProperty(ColorId)
                    ? source.GetColor(ColorId)
                    : Color.white;

            if (destination.HasProperty(BaseColorId))
                destination.SetColor(BaseColorId, color);
            if (destination.HasProperty(ColorId))
                destination.SetColor(ColorId, color);
        }

        private static void CopyFloat(Material source, Material destination, int propertyId)
        {
            if (source.HasProperty(propertyId) && destination.HasProperty(propertyId))
                destination.SetFloat(propertyId, source.GetFloat(propertyId));
        }

        private static void CopySmoothness(Material source, Material destination)
        {
            if (!destination.HasProperty(SmoothnessId))
                return;

            if (source.HasProperty(SmoothnessId))
                destination.SetFloat(SmoothnessId, source.GetFloat(SmoothnessId));
            else if (source.HasProperty(GlossinessId))
                destination.SetFloat(SmoothnessId, source.GetFloat(GlossinessId));
        }

        private static void RegisterGeneratedMaterial(
            Material material,
            ICollection<Material> generatedMaterials)
        {
            if (material != null && generatedMaterials != null)
                generatedMaterials.Add(material);
        }
    }
}
