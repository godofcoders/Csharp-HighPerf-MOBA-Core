using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class PowerCube : SimulationEntity
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly List<ISpatialEntity> Scratch = new List<ISpatialEntity>(12);
        private static readonly List<PowerCube> AllCubes = new List<PowerCube>(32);

        [Header("Pickup")]
        [SerializeField, Min(1)] private int _value = 1;
        [SerializeField, Min(0.1f)] private float _pickupRadius = 1.05f;

        [Header("Presentation")]
        [SerializeField] private bool _useRuntimeVisual = true;
        [SerializeField] private Color _cubeColor = new Color(0.48f, 1f, 0.16f, 1f);
        [SerializeField] private Color _coreColor = new Color(0.88f, 1f, 0.32f, 1f);
        [SerializeField, Min(0f)] private float _restHeight = 0.55f;
        [SerializeField, Min(0f)] private float _bobHeight = 0.08f;
        [SerializeField, Min(0.1f)] private float _bobSpeed = 3.2f;

        private Transform _visual;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propertyBlock;
        private float _visualSeed;

        public static IReadOnlyList<PowerCube> All => AllCubes;
        public int Value => _value;
        public bool IsPickedUp { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _visualSeed = Random.value * 31f;
            EnsureVisual();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!AllCubes.Contains(this))
                AllCubes.Add(this);

            EnsureVisual();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AllCubes.Remove(this);
        }

        private void Update()
        {
            if (_visual == null)
                return;

            float bob = Mathf.Sin(Time.time * _bobSpeed + _visualSeed) * _bobHeight;
            _visual.localPosition = Vector3.up * Mathf.Max(0f, _restHeight + bob);
            _visual.localRotation = Quaternion.Euler(0f, Time.time * 70f + _visualSeed * 11f, 0f);
        }

        public void SetValue(int value)
        {
            _value = Mathf.Max(1, value);
        }

        public bool TryPickupBy(BrawlerController carrier)
        {
            if (carrier == null)
                return false;

            return TryPickupBy(
                carrier.State,
                ResolvePickupFeedbackPosition(carrier));
        }

        public bool TryPickupBy(BrawlerState carrier)
        {
            return TryPickupBy(carrier, transform.position);
        }

        private bool TryPickupBy(BrawlerState carrier, Vector3 pickupFeedbackPosition)
        {
            if (IsPickedUp || carrier == null || carrier.IsDead)
                return false;

            IsPickedUp = true;
            carrier.AddPowerCubes(_value);
            PowerCubeEventBus.OnPowerCubePickedUp?.Invoke(carrier, _value);
            PowerCubeEventBus.OnPowerCubePickedUpAt?.Invoke(pickupFeedbackPosition, _value);

            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }

        public override void Tick(uint currentTick)
        {
            if (IsPickedUp)
                return;

            SpatialGrid grid = SimulationClock.Grid;
            if (grid == null)
                return;

            Scratch.Clear();
            grid.GetEntitiesInRadiusNonAlloc(transform.position, _pickupRadius + 4f, Scratch);

            float pickupRadiusSq = _pickupRadius * _pickupRadius;
            Vector3 cubePosition = transform.position;
            for (int i = 0; i < Scratch.Count; i++)
            {
                if (!(Scratch[i] is BrawlerController brawler) ||
                    brawler.State == null ||
                    brawler.State.IsDead)
                {
                    continue;
                }

                Vector3 brawlerPosition = brawler.Position;
                float dx = brawlerPosition.x - cubePosition.x;
                float dz = brawlerPosition.z - cubePosition.z;
                if (dx * dx + dz * dz > pickupRadiusSq)
                    continue;

                if (TryPickupBy(brawler))
                    return;
            }
        }

        private void EnsureVisual()
        {
            if (!_useRuntimeVisual)
                return;

            Renderer rootRenderer = GetComponent<Renderer>();
            if (rootRenderer != null)
                rootRenderer.enabled = false;

            Transform visual = transform.Find("PowerCubeVisual");
            if (visual == null)
            {
                GameObject visualGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visualGo.name = "PowerCubeVisual";
                visualGo.layer = gameObject.layer;
                visualGo.transform.SetParent(transform, false);

                Collider visualCollider = visualGo.GetComponent<Collider>();
                if (visualCollider != null)
                    Destroy(visualCollider);

                visual = visualGo.transform;
            }

            _visual = visual;
            _visual.localScale = new Vector3(0.7f, 0.7f, 0.7f);

            _renderers = GetComponentsInChildren<Renderer>(true);
            ApplyVisualColors();
        }

        private void ApplyVisualColors()
        {
            if (_renderers == null || _renderers.Length == 0)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer cubeRenderer = _renderers[i];
                if (cubeRenderer == null)
                    continue;

                cubeRenderer.GetPropertyBlock(_propertyBlock);
                Color color = cubeRenderer.transform == _visual ? _cubeColor : _coreColor;
                _propertyBlock.SetColor(ColorId, color);
                _propertyBlock.SetColor(BaseColorId, color);
                cubeRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private static Vector3 ResolvePickupFeedbackPosition(BrawlerController carrier)
        {
            Vector3 carrierPosition = carrier.Position;
            Transform presentationTarget = carrier.PresentationFollowTarget;
            Vector3 position = presentationTarget != null
                ? presentationTarget.position
                : carrierPosition;

            float minimumHeight = carrierPosition.y + 1.2f;
            if (position.y < minimumHeight)
                position.y = minimumHeight;

            return position + Vector3.up * 0.35f;
        }
    }
}
