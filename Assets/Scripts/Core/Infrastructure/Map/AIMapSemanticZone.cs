using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public class AIMapSemanticZone : MonoBehaviour
    {
        [Header("Semantic")]
        [SerializeField] private string _zoneName;
        [SerializeField] private AIMapSemanticTag _tags = AIMapSemanticTag.Lane;
        [SerializeField] private AITeamLaneAssignment _lane = AITeamLaneAssignment.None;
        [SerializeField, Min(0f)] private float _influence = 1f;

        [Header("Shape")]
        [SerializeField] private Vector3 _center = Vector3.zero;
        [SerializeField] private Vector3 _size = new Vector3(4f, 2f, 4f);

        public string ZoneName => string.IsNullOrWhiteSpace(_zoneName) ? name : _zoneName;
        public AIMapSemanticTag Tags => _tags;
        public AITeamLaneAssignment Lane => _lane;
        public float Influence => Mathf.Max(0f, _influence);

        public bool ContainsWorldPosition(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition) - _center;
            Vector3 half = GetHalfExtents();

            return Mathf.Abs(local.x) <= half.x &&
                   Mathf.Abs(local.y) <= half.y &&
                   Mathf.Abs(local.z) <= half.z;
        }

        private Vector3 GetHalfExtents()
        {
            return new Vector3(
                Mathf.Max(0.01f, _size.x) * 0.5f,
                Mathf.Max(0.01f, _size.y) * 0.5f,
                Mathf.Max(0.01f, _size.z) * 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GetGizmoColor();
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(_center, new Vector3(
                Mathf.Max(0.01f, _size.x),
                Mathf.Max(0.01f, _size.y),
                Mathf.Max(0.01f, _size.z)));
            Gizmos.matrix = previousMatrix;
        }

        private Color GetGizmoColor()
        {
            if ((_tags & AIMapSemanticTag.DangerCorridor) != 0)
                return new Color(1f, 0.18f, 0.12f, 0.65f);

            if ((_tags & AIMapSemanticTag.ThrowerSafeZone) != 0)
                return new Color(0.22f, 0.58f, 1f, 0.65f);

            if ((_tags & AIMapSemanticTag.Choke) != 0)
                return new Color(1f, 0.75f, 0.18f, 0.65f);

            if ((_tags & AIMapSemanticTag.CoverCluster) != 0)
                return new Color(0.12f, 0.9f, 0.45f, 0.65f);

            return new Color(0.72f, 0.45f, 1f, 0.65f);
        }
    }
}
