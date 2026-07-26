using System.Collections.Generic;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class BombLeapPresentationView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static Material _sharedMaterial;

        private readonly List<Renderer> _renderers = new List<Renderer>(6);
        private readonly List<Color> _baseColors = new List<Color>(6);
        private MaterialPropertyBlock _propertyBlock;
        private float _startTime;
        private float _durationSeconds;
        private float _radius;
        private bool _isHypercharged;

        public static void SpawnBombMarker(
            Vector3 position,
            TeamType team,
            float radius,
            float durationSeconds,
            bool isHypercharged)
        {
            GameObject marker = new GameObject("BombLeapMarker");
            marker.transform.position = position;

            BombLeapPresentationView view = marker.AddComponent<BombLeapPresentationView>();
            view.Initialize(team, radius, durationSeconds, isHypercharged);
        }

        private void Initialize(
            TeamType team,
            float radius,
            float durationSeconds,
            bool isHypercharged)
        {
            _startTime = Time.time;
            _radius = Mathf.Max(0.35f, radius);
            _durationSeconds = Mathf.Max(0.08f, durationSeconds);
            _isHypercharged = isHypercharged;

            Color teamColor = ResolveTeamColor(team);
            Color bombCore = isHypercharged
                ? new Color(0.36f, 0.08f, 0.78f, 0.94f)
                : new Color(0.18f, 0.15f, 0.13f, 0.96f);
            Color fuseColor = isHypercharged
                ? new Color(0.82f, 0.18f, 1f, 0.88f)
                : new Color(1f, 0.26f, 0.10f, 0.86f);
            Color warningColor = isHypercharged
                ? new Color(0.62f, 0.16f, 1f, 0.62f)
                : new Color(1f, 0.18f, 0.06f, 0.58f);

            CreatePrimitive(
                "BombWarningRing",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.035f, 0f),
                new Vector3(_radius * 1.22f, 0.012f, _radius * 1.22f),
                Quaternion.identity,
                warningColor);

            CreatePrimitive(
                "BombTeamPip",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.055f, 0f),
                new Vector3(_radius * 0.34f, 0.010f, _radius * 0.34f),
                Quaternion.identity,
                teamColor.WithAlpha(0.58f));

            CreatePrimitive(
                "BombCore",
                PrimitiveType.Sphere,
                new Vector3(0f, 0.23f, 0f),
                new Vector3(0.34f, 0.26f, 0.34f),
                Quaternion.identity,
                bombCore);

            CreatePrimitive(
                "BombFuse",
                PrimitiveType.Cylinder,
                new Vector3(0.15f, 0.48f, 0f),
                new Vector3(0.045f, 0.18f, 0.045f),
                Quaternion.Euler(0f, 0f, -32f),
                fuseColor);

            CreatePrimitive(
                "BombBlink",
                PrimitiveType.Sphere,
                new Vector3(0.26f, 0.61f, 0f),
                new Vector3(0.10f, 0.10f, 0.10f),
                Quaternion.identity,
                fuseColor);
        }

        private void Update()
        {
            float duration = Mathf.Max(0.08f, _durationSeconds);
            float t = Mathf.Clamp01((Time.time - _startTime) / duration);
            float pulse = 0.72f + Mathf.PingPong(Time.time * (_isHypercharged ? 10.5f : 8.5f), 0.28f);
            float urgency = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.12f, pulse * urgency);

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                Color color = _baseColors[i];
                float blink = i == _renderers.Count - 1
                    ? Mathf.Lerp(0.30f, 1f, pulse)
                    : Mathf.Lerp(0.72f, 1f, pulse * urgency);
                color.a *= blink;
                ApplyRendererColor(renderer, color);
            }

            if (t >= 1f)
                Destroy(gameObject);
        }

        private void CreatePrimitive(
            string objectName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Color color)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = objectName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer == null)
                return;

            renderer.sharedMaterial = ResolveSharedMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _renderers.Add(renderer);
            _baseColors.Add(color);
            ApplyRendererColor(renderer, color);
        }

        private void ApplyRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(EmissionColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private static Material ResolveSharedMaterial()
        {
            if (_sharedMaterial != null)
                return _sharedMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            _sharedMaterial = new Material(shader)
            {
                name = "Bomb Leap Marker Material",
                color = Color.white,
                enableInstancing = true
            };
            _sharedMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _sharedMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _sharedMaterial.SetInt("_ZWrite", 0);
            _sharedMaterial.DisableKeyword("_ALPHATEST_ON");
            _sharedMaterial.EnableKeyword("_ALPHABLEND_ON");
            _sharedMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _sharedMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _sharedMaterial;
        }

        private static Color ResolveTeamColor(TeamType team)
        {
            switch (team)
            {
                case TeamType.Blue:
                    return new Color(0.16f, 0.44f, 1f, 1f);
                case TeamType.Red:
                    return new Color(1f, 0.20f, 0.18f, 1f);
                default:
                    return new Color(1f, 0.78f, 0.18f, 1f);
            }
        }
    }
}
