using MOBA.Core.Definitions;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    /// <summary>Reusable presentation for pooled blaster shots; never owns collision or damage.</summary>
    public sealed class ElementalProjectileView : MonoBehaviour
    {
        private static readonly int RibbonId = Shader.PropertyToID("_Ribbon");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private Material _material;
        private MaterialPropertyBlock _properties;
        private LineRenderer _core;
        private LineRenderer _sheath;
        private TrailRenderer _trail;
        private float _width;
        private float _length;
        private float _elapsed;
        private BrawlerElementType _element;
        private Color _color;

        public void Configure(ProjectilePresentationProfile profile, float powerScale)
        {
            EnsureRenderers();
            Clear();
            _element = profile.ElementType;
            _color = BrawlerElementUtility.ToColor(_element);
            _width = Mathf.Max(0.02f, profile.BoltWidth) * powerScale;
            _length = Mathf.Max(0.1f, profile.BoltLength) * powerScale;
            transform.localPosition = profile.LocalPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);

            SetBolt(_sheath, _width * 2.1f, _color, _length);
            SetBolt(_core, _width * 0.65f, Color.Lerp(_color, Color.white, 0.82f), _length * 0.82f);
            _trail.time = Mathf.Clamp(profile.TrailTime, 0.035f, 0.18f);
            _trail.startWidth = _width * 0.85f;
            _trail.endWidth = 0f;
            _trail.startColor = _color;
            _trail.endColor = new Color(_color.r, _color.g, _color.b, 0f);
            _trail.Clear();
            _trail.emitting = true;
        }

        public void TickVisual(float deltaTime)
        {
            if (!gameObject.activeSelf)
                return;

            _elapsed += Mathf.Max(0f, deltaTime);
            _sheath.widthMultiplier = _width * 2.1f * (1f + Mathf.Sin(_elapsed * 24f) * 0.08f);
        }

        public void Clear()
        {
            _elapsed = 0f;
            if (_trail != null)
            {
                _trail.emitting = false;
                _trail.Clear();
            }
            gameObject.SetActive(false);
        }

        private void EnsureRenderers()
        {
            if (_core != null)
                return;

            _material = Resources.Load<Material>("VFX/ElementalBlaster");
            _properties = new MaterialPropertyBlock();
            _core = CreateRibbon("BlasterCore");
            _sheath = CreateRibbon("ElementSheath");
            _trail = gameObject.AddComponent<TrailRenderer>();
            ConfigureRenderer(_trail, true);
            _trail.minVertexDistance = 0.045f;
            _trail.numCapVertices = 3;
            _trail.numCornerVertices = 2;
            _trail.textureMode = LineTextureMode.Stretch;
            _trail.alignment = LineAlignment.View;
            _trail.emitting = false;
        }

        private LineRenderer CreateRibbon(string label)
        {
            var ribbon = new GameObject(label);
            ribbon.layer = gameObject.layer;
            ribbon.transform.SetParent(transform, false);
            var line = ribbon.AddComponent<LineRenderer>();
            ConfigureRenderer(line, true);
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            return line;
        }

        private void ConfigureRenderer(Renderer renderer, bool ribbon, Texture texture = null)
        {
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _properties.Clear();
            _properties.SetFloat(RibbonId, ribbon ? 1f : 0f);
            _properties.SetTexture(BaseMapId, texture != null ? texture : Texture2D.whiteTexture);
            renderer.SetPropertyBlock(_properties);
        }

        private static void SetBolt(LineRenderer line, float width, Color color, float length)
        {
            line.positionCount = 3;
            line.SetPosition(0, Vector3.back * length * 0.85f);
            line.SetPosition(1, Vector3.zero);
            line.SetPosition(2, Vector3.forward * length * 0.15f);
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f));
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;
        }
    }
}
