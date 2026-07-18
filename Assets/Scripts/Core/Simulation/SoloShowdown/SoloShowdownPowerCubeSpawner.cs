using System.Collections;
using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class SoloShowdownPowerCubeSpawner : MonoBehaviour
    {
        private const float DefaultFallbackBoundsSize = 44f;

        [Header("Prefabs")]
        [SerializeField] private PowerCubeCrateController _cratePrefab;
        [SerializeField] private PowerCube _powerCubePrefab;

        [Header("Crates")]
        [SerializeField, Min(0)] private int _crateCount = 12;
        [SerializeField, Min(1f)] private float _crateHealth = 4000f;
        [SerializeField, Min(1)] private int _powerCubeValuePerCrate = 1;

        [Header("Placement")]
        [SerializeField, Min(0.5f)] private float _edgeInset = 3f;
        [SerializeField, Min(0.5f)] private float _minimumSpacing = 3.4f;
        [SerializeField, Min(0.5f)] private float _clusterRadius = 6f;
        [SerializeField, Min(1)] private int _maxExistingCratesInsideCluster = 1;
        [SerializeField, Min(0.5f)] private float _spawnPointAvoidRadius = 4f;
        [SerializeField, Min(0.2f)] private float _obstacleClearanceRadius = 1.1f;
        [SerializeField, Min(1)] private int _maxPlacementAttemptsPerCrate = 80;
        [SerializeField, Min(0.1f)] private float _deathDropScatterRadius = 0.92f;

        private readonly List<Vector3> _placedPositions = new List<Vector3>(16);
        private readonly List<Vector3> _spawnAvoidPositions = new List<Vector3>(16);
        private readonly List<Vector3> _deathDropReservedPositions = new List<Vector3>(12);
        private readonly List<PowerCubeCrateController> _spawnedCrates =
            new List<PowerCubeCrateController>(16);
        private Coroutine _spawnRoutine;
        private bool _hasSpawnedInitialCrates;

        public void SpawnInitialCrates()
        {
            if (_hasSpawnedInitialCrates)
                return;

            if (_spawnRoutine != null)
                StopCoroutine(_spawnRoutine);

            _spawnRoutine = StartCoroutine(SpawnInitialCratesRoutine());
        }

        private IEnumerator SpawnInitialCratesRoutine()
        {
            yield return null;

            if (_hasSpawnedInitialCrates || _crateCount <= 0)
                yield break;

            _hasSpawnedInitialCrates = true;
            _placedPositions.Clear();
            RefreshSpawnAvoidPositions();

            Bounds bounds = ResolvePlayableBounds();
            for (int i = 0; i < _crateCount; i++)
            {
                if (!TryResolvePlacement(bounds, out Vector3 position))
                    continue;

                PowerCubeCrateController crate = SpawnCrate(position);
                if (crate == null)
                    continue;

                _spawnedCrates.Add(crate);
                _placedPositions.Add(position);
            }
        }

        private PowerCubeCrateController SpawnCrate(Vector3 position)
        {
            PowerCubeCrateController crate;
            if (_cratePrefab != null)
            {
                crate = Instantiate(_cratePrefab, position, Quaternion.identity, transform);
            }
            else
            {
                GameObject crateObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crateObject.name = "PowerCubeCrate";
                crateObject.transform.SetParent(transform, true);
                crateObject.transform.position = position;
                crateObject.transform.localScale = new Vector3(1.35f, 1.1f, 1.35f);
                crate = crateObject.AddComponent<PowerCubeCrateController>();
            }

            if (crate != null)
                crate.Configure(_powerCubePrefab, _crateHealth, _powerCubeValuePerCrate);

            return crate;
        }

        public void SpawnDroppedPowerCubes(Vector3 center, int count)
        {
            if (count <= 0)
                return;

            _deathDropReservedPositions.Clear();
            for (int i = 0; i < count; i++)
            {
                int layoutIndex = count > 1 ? i + 1 : 0;
                Vector3 spawnPosition = ResolveReadablePowerCubePosition(
                    center,
                    layoutIndex,
                    _deathDropScatterRadius,
                    _deathDropReservedPositions);

                _deathDropReservedPositions.Add(spawnPosition);
                SpawnPowerCube(spawnPosition, _powerCubeValuePerCrate);
            }

            _deathDropReservedPositions.Clear();
        }

        private PowerCube SpawnPowerCube(Vector3 position, int value)
        {
            PowerCube cube;
            if (_powerCubePrefab != null)
            {
                cube = Instantiate(_powerCubePrefab, position, Quaternion.identity);
            }
            else
            {
                GameObject cubeObject = new GameObject("PowerCube");
                cubeObject.transform.position = position;
                cube = cubeObject.AddComponent<PowerCube>();
            }

            if (cube != null)
                cube.SetValue(Mathf.Max(1, value));

            return cube;
        }

        private bool TryResolvePlacement(Bounds bounds, out Vector3 position)
        {
            float minX = bounds.min.x + _edgeInset;
            float maxX = bounds.max.x - _edgeInset;
            float minZ = bounds.min.z + _edgeInset;
            float maxZ = bounds.max.z - _edgeInset;
            if (minX >= maxX || minZ >= maxZ)
            {
                position = bounds.center;
                return false;
            }

            for (int attempt = 0; attempt < _maxPlacementAttemptsPerCrate; attempt++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(minX, maxX),
                    bounds.center.y,
                    Random.Range(minZ, maxZ));
                candidate = ResolveGroundedPosition(candidate);

                if (!IsPlacementReadable(candidate))
                    continue;

                position = candidate;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private bool IsPlacementReadable(Vector3 candidate)
        {
            if (IsTooCloseToSpawn(candidate))
                return false;

            int clusterCount = 0;
            float minSpacingSq = _minimumSpacing * _minimumSpacing;
            float clusterRadiusSq = _clusterRadius * _clusterRadius;
            for (int i = 0; i < _placedPositions.Count; i++)
            {
                Vector3 delta = _placedPositions[i] - candidate;
                delta.y = 0f;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq < minSpacingSq)
                    return false;

                if (distanceSq <= clusterRadiusSq)
                    clusterCount++;
            }

            if (clusterCount > _maxExistingCratesInsideCluster)
                return false;

            int obstacleMask = ResolveObstacleMask();
            if (obstacleMask == 0)
                return true;

            return !Physics.CheckSphere(
                candidate + Vector3.up * 0.65f,
                _obstacleClearanceRadius,
                obstacleMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool IsTooCloseToSpawn(Vector3 candidate)
        {
            if (_spawnAvoidPositions.Count == 0)
                return false;

            float radiusSq = _spawnPointAvoidRadius * _spawnPointAvoidRadius;
            for (int i = 0; i < _spawnAvoidPositions.Count; i++)
            {
                Vector3 delta = _spawnAvoidPositions[i] - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSq)
                    return true;
            }

            return false;
        }

        private static Vector3 ResolveReadablePowerCubePosition(
            Vector3 center,
            int layoutIndex,
            float spacing,
            IList<Vector3> reservedPositions)
        {
            float safeSpacing = Mathf.Max(0.05f, spacing);
            int startIndex = Mathf.Max(0, layoutIndex);

            for (int attempt = 0; attempt < 48; attempt++)
            {
                Vector3 candidate = center + GemPlacementUtility.GetClusterOffset(
                    startIndex + attempt,
                    safeSpacing);

                if (!OverlapsPowerCube(candidate, safeSpacing, reservedPositions))
                    return ResolveGroundedPosition(candidate);
            }

            return ResolveGroundedPosition(center + GemPlacementUtility.GetClusterOffset(
                startIndex + 48,
                safeSpacing));
        }

        private static bool OverlapsPowerCube(
            Vector3 candidate,
            float spacing,
            IList<Vector3> reservedPositions)
        {
            float spacingSq = spacing * spacing;
            IReadOnlyList<PowerCube> existing = PowerCube.All;
            for (int i = 0; i < existing.Count; i++)
            {
                PowerCube cube = existing[i];
                if (cube == null || cube.IsPickedUp)
                    continue;

                if (XZDistanceSq(candidate, cube.transform.position) < spacingSq)
                    return true;
            }

            if (reservedPositions != null)
            {
                for (int i = 0; i < reservedPositions.Count; i++)
                {
                    if (XZDistanceSq(candidate, reservedPositions[i]) < spacingSq)
                        return true;
                }
            }

            return false;
        }

        private static float XZDistanceSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private void RefreshSpawnAvoidPositions()
        {
            _spawnAvoidPositions.Clear();
            SpawnPointMarker[] markers = FindObjectsOfType<SpawnPointMarker>(false);
            if (markers == null)
                return;

            for (int i = 0; i < markers.Length; i++)
            {
                SpawnPointMarker marker = markers[i];
                if (marker != null)
                    _spawnAvoidPositions.Add(marker.transform.position);
            }
        }

        private Bounds ResolvePlayableBounds()
        {
            if (TryResolveSpawnedMapGroundBounds(out Bounds bounds))
                return bounds;

            if (TryResolveSpawnPointBounds(out bounds))
                return bounds;

            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null)
            {
                float cellSize = Mathf.Max(0.1f, mapGenerator.CellSize);
                return new Bounds(
                    mapGenerator.transform.position,
                    new Vector3(
                        Mathf.Max(1, mapGenerator.Width) * cellSize,
                        0f,
                        Mathf.Max(1, mapGenerator.Height) * cellSize));
            }

            return new Bounds(
                transform.position,
                new Vector3(DefaultFallbackBoundsSize, 0f, DefaultFallbackBoundsSize));
        }

        private static bool TryResolveSpawnedMapGroundBounds(out Bounds bounds)
        {
            bounds = default;

            MapLoader mapLoader = FindObjectOfType<MapLoader>();
            GameObject spawnedMap = mapLoader != null ? mapLoader.SpawnedMapInstance : null;
            if (spawnedMap == null)
                return false;

            int excludedMask = ResolveObstacleMask() |
                               ResolveLayerMask("Bushes") |
                               ResolveLayerMask("Bush");
            bool found = false;

            Collider[] colliders = spawnedMap.GetComponentsInChildren<Collider>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null ||
                    collider.isTrigger ||
                    !IsMapBoundsCandidate(collider.gameObject, excludedMask))
                {
                    continue;
                }

                Encapsulate(collider.bounds, ref bounds, ref found);
            }

            if (found)
                return true;

            Renderer[] renderers = spawnedMap.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !IsMapBoundsCandidate(renderer.gameObject, excludedMask))
                {
                    continue;
                }

                Encapsulate(renderer.bounds, ref bounds, ref found);
            }

            return found;
        }

        private static bool TryResolveSpawnPointBounds(out Bounds bounds)
        {
            SpawnPointMarker[] markers = FindObjectsOfType<SpawnPointMarker>(false);
            if (markers == null || markers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bool found = false;
            bounds = default;
            for (int i = 0; i < markers.Length; i++)
            {
                SpawnPointMarker marker = markers[i];
                if (marker == null)
                    continue;

                Encapsulate(new Bounds(marker.transform.position, Vector3.zero), ref bounds, ref found);
            }

            if (found)
                bounds.Expand(new Vector3(14f, 0f, 16f));

            return found;
        }

        private static Vector3 ResolveGroundedPosition(Vector3 position)
        {
            int groundMask = ResolveGroundMask();
            if (groundMask != 0 &&
                Physics.Raycast(
                    position + Vector3.up * 8f,
                    Vector3.down,
                    out RaycastHit hit,
                    18f,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y + 0.55f;
            }
            else
            {
                position.y += 0.55f;
            }

            return position;
        }

        private static bool IsMapBoundsCandidate(GameObject candidate, int excludedMask)
        {
            if (candidate == null)
                return false;

            int layerMask = 1 << candidate.layer;
            if ((excludedMask & layerMask) != 0)
                return false;

            string objectName = candidate.name;
            return objectName.IndexOf("PowerCube", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                   objectName.IndexOf("ArenaWall", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                   objectName.IndexOf("RuntimeArenaBoundary", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static void Encapsulate(Bounds candidate, ref Bounds bounds, ref bool found)
        {
            if (!found)
            {
                bounds = candidate;
                found = true;
                return;
            }

            bounds.Encapsulate(candidate);
        }

        private static int ResolveGroundMask()
        {
            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null && mapGenerator.GroundLayer.value != 0)
                return mapGenerator.GroundLayer.value;

            int groundLayer = LayerMask.NameToLayer("Ground");
            return groundLayer >= 0 ? 1 << groundLayer : 0;
        }

        private static int ResolveObstacleMask()
        {
            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null && mapGenerator.ObstacleLayer.value != 0)
                return mapGenerator.ObstacleLayer.value;

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            return obstacleLayer >= 0 ? 1 << obstacleLayer : 0;
        }

        private static int ResolveLayerMask(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : 0;
        }
    }
}
