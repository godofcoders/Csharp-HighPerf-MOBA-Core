using UnityEngine;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Designer-authored Brawl Ball goal zone. The zone only declares
    /// "which team scores here"; BrawlBallMode owns the actual scoring
    /// and match-end rules.
    /// </summary>
    public sealed class BrawlBallGoalController : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BrawlBallMode _mode;
        [SerializeField] private TeamType _scoringTeam = TeamType.Blue;
        [SerializeField] private Vector3 _zoneSize = new Vector3(4.8f, 1f, 1.2f);

        [Header("Presentation")]
        [SerializeField] private bool _useRuntimeVisual = true;
        [SerializeField] private Color _visualColor = new Color(0.18f, 0.46f, 1f, 0.55f);
        [SerializeField, Min(0.01f)] private float _visualHeight = 0.08f;

        private Transform _visualRoot;
        private MeshRenderer _visualRenderer;
        private MaterialPropertyBlock _propertyBlock;

        public TeamType ScoringTeam => _scoringTeam;

        private void Awake()
        {
            ResolveMode();
            EnsureRuntimeVisual();
        }

        private void OnEnable()
        {
            ResolveMode();
            _mode?.RegisterGoal(this);
        }

        private void OnDisable()
        {
            _mode?.UnregisterGoal(this);
        }

        public bool ContainsBall(Vector3 worldPosition, float extraRadius = 0f)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            Vector3 size = SanitizeZoneSize();

            float halfX = size.x * 0.5f + Mathf.Max(0f, extraRadius);
            float halfZ = size.z * 0.5f + Mathf.Max(0f, extraRadius);

            return Mathf.Abs(local.x) <= halfX &&
                   Mathf.Abs(local.z) <= halfZ;
        }

        public void ConfigureForDebug(TeamType scoringTeam, Vector3 zoneSize)
        {
            _scoringTeam = scoringTeam;
            _zoneSize = zoneSize;
            RefreshRuntimeVisual();
        }

        private void ResolveMode()
        {
            if (_mode != null)
                return;

            _mode = GetComponentInParent<BrawlBallMode>();
            if (_mode == null)
                _mode = BrawlBallMode.Instance;
        }

        private Vector3 SanitizeZoneSize()
        {
            return new Vector3(
                Mathf.Max(0.25f, _zoneSize.x),
                Mathf.Max(0.05f, _zoneSize.y),
                Mathf.Max(0.25f, _zoneSize.z));
        }

        private void EnsureRuntimeVisual()
        {
            if (!_useRuntimeVisual || _visualRoot != null)
                return;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "GoalZoneVisual";
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            _visualRoot = visual.transform;

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(visualCollider);
                else
                    DestroyImmediate(visualCollider);
            }

            _visualRenderer = visual.GetComponent<MeshRenderer>();
            RefreshRuntimeVisual();
        }

        private void RefreshRuntimeVisual()
        {
            if (_visualRoot == null)
                return;

            Vector3 size = SanitizeZoneSize();
            float height = Mathf.Max(0.01f, _visualHeight);
            _visualRoot.localPosition = Vector3.up * (height * 0.5f);
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = new Vector3(size.x, height, size.z);

            if (_visualRenderer == null)
                _visualRenderer = _visualRoot.GetComponent<MeshRenderer>();

            if (_visualRenderer == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            _visualRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, _visualColor);
            _propertyBlock.SetColor(BaseColorId, _visualColor);
            _visualRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
