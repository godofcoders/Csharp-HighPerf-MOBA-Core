using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public sealed class MapDesertThemePresentation : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Color _groundColor = new Color(0.78f, 0.63f, 0.41f, 1f);
        [SerializeField] private Color _sandstoneObstacleColor = new Color(0.71f, 0.49f, 0.29f, 1f);
        [SerializeField] private Color _sandRockBreakableColor = new Color(0.65f, 0.48f, 0.30f, 1f);

        private MaterialPropertyBlock _propertyBlock;

        public static void InstallUnder(GameObject root)
        {
            if (root == null)
                return;

            MapDesertThemePresentation presentation =
                root.GetComponent<MapDesertThemePresentation>();
            if (presentation == null)
                presentation = root.AddComponent<MapDesertThemePresentation>();

            presentation.ApplyTheme();
        }

        private void Awake()
        {
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            int bushLayer = LayerMask.NameToLayer("Bushes");

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || ShouldSkipRenderer(renderer, bushLayer))
                    continue;

                if (TryResolveThemeColor(renderer, obstacleLayer, out Color color))
                    ApplyColor(renderer, color);
            }
        }

        private static bool ShouldSkipRenderer(Renderer renderer, int bushLayer)
        {
            GameObject go = renderer.gameObject;
            if (go == null)
                return true;

            if (bushLayer >= 0 && go.layer == bushLayer)
                return true;

            string objectName = go.name;
            return objectName.IndexOf("Bush", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("GrassPatchVisual", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool TryResolveThemeColor(
            Renderer renderer,
            int obstacleLayer,
            out Color color)
        {
            GameObject go = renderer.gameObject;
            string objectName = go != null ? go.name : string.Empty;

            if (renderer.GetComponentInParent<BreakableObjectController>() != null)
            {
                color = _sandRockBreakableColor;
                return true;
            }

            if (objectName.IndexOf("Plane", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                color = _groundColor;
                return true;
            }

            if ((obstacleLayer >= 0 && go != null && go.layer == obstacleLayer) ||
                objectName.IndexOf("Obstacle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                color = _sandstoneObstacleColor;
                return true;
            }

            color = default;
            return false;
        }

        private void ApplyColor(Renderer renderer, Color color)
        {
            EnsurePropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }
    }
}
