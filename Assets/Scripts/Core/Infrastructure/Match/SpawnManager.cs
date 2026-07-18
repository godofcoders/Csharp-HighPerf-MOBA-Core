using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public class SpawnManager : MonoBehaviour
    {
        public static SpawnManager Instance { get; private set; }

        [Header("Spawn Points")]
        [SerializeField] private GameObject _brawlerBasePrefab;
        [SerializeField] private List<Transform> _blueSpawnPoints;
        [SerializeField] private List<Transform> _redSpawnPoints;
        [SerializeField] private List<Transform> _soloSpawnPoints;
        [SerializeField] private float _respawnDelay = 5.0f;

        [Header("Spawn Spacing")]
        [SerializeField, Min(0.5f)] private float _minimumInitialSpawnSeparation = 4.25f;
        [SerializeField, Min(0.5f)] private float _runtimeSpawnEdgeInset = 3f;
        [SerializeField, Min(0.1f)] private float _runtimeSpawnObstacleClearanceRadius = 0.9f;
        [SerializeField, Min(1)] private int _runtimeSpawnPlacementAttempts = 120;
        [SerializeField, Min(0f)] private float _spawnGroundOffset = 0.05f;

        /// <summary>Seconds between death and respawn. Read by the death
        /// overlay HUD to drive the countdown display.</summary>
        public float RespawnDelaySeconds => _respawnDelay;
        [SerializeField] private CameraController _mainCameraController;

        private readonly Dictionary<BrawlerController, Coroutine> _pendingRespawns =
            new Dictionary<BrawlerController, Coroutine>();
        private readonly List<Transform> _preparedBlueSpawnPoints = new List<Transform>(8);
        private readonly List<Transform> _preparedRedSpawnPoints = new List<Transform>(8);
        private readonly List<Transform> _preparedSoloSpawnPoints = new List<Transform>(12);
        private readonly List<Transform> _runtimeSpawnPoints = new List<Transform>(16);
        private readonly List<Transform> _spawnPreparationReservedPoints = new List<Transform>(16);
        private Transform _runtimeSpawnRoot;
        private int _blueRespawnCursor;
        private int _redRespawnCursor;
        private int _soloRespawnCursor;

        /// <summary>
        /// Replace the spawn-point lists at runtime. Called by MapLoader
        /// after it instantiates a map prefab and discovers
        /// SpawnPointMarker components within it. The inspector-assigned
        /// fields stay as a fallback for direct Match-scene launches that
        /// skip MapLoader.
        /// </summary>
        public void SetSpawnPoints(System.Collections.Generic.List<Transform> blue, System.Collections.Generic.List<Transform> red)
        {
            if (blue != null) _blueSpawnPoints = blue;
            if (red != null) _redSpawnPoints = red;
        }

        public void SetSpawnPoints(
            System.Collections.Generic.List<Transform> blue,
            System.Collections.Generic.List<Transform> red,
            System.Collections.Generic.List<Transform> solo)
        {
            SetSpawnPoints(blue, red);
            if (solo != null) _soloSpawnPoints = solo;
        }

        public void SetPlayerTarget(Transform playerTransform)
        {
            if (_mainCameraController != null)
            {
                _mainCameraController.SetTarget(playerTransform);
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>When false, RequestRespawn is a no-op. Set to false by
        /// modes that suppress mid-round respawns (e.g. Knockout) and
        /// re-enabled at round transitions.</summary>
        public bool AllowAutoRespawn = true;

        public void RequestRespawn(BrawlerController brawler, TeamType team)
        {
            if (!AllowAutoRespawn) return;

            if (brawler == null)
                return;

            CancelPendingRespawn(brawler);
            _pendingRespawns[brawler] = StartCoroutine(RespawnRoutine(brawler, team));
        }

        /// <summary>Force-respawn a brawler at one of their team's spawn
        /// points, bypassing the AllowAutoRespawn gate. Used by Knockout
        /// at round start.</summary>
        public void ForceRespawn(BrawlerController brawler, TeamType team)
        {
            if (brawler == null) return;

            CancelPendingRespawn(brawler);

            Transform pt = ResolveRotatingRespawnPoint(team);
            if (pt == null) return;
            brawler.gameObject.SetActive(true);
            brawler.Respawn(pt.position);
        }

        public void ForceRespawn(BrawlerController brawler, TeamType team, int teamOrdinal)
        {
            if (brawler == null) return;

            CancelPendingRespawn(brawler);

            var list = ResolveSpawnListForTeam(team);
            if (list == null || list.Count == 0) return;

            int index = Mathf.Max(0, teamOrdinal) % list.Count;
            Transform pt = list[index];
            if (pt == null) return;

            brawler.gameObject.SetActive(true);
            brawler.Respawn(pt.position);
        }

        private IEnumerator RespawnRoutine(BrawlerController brawler, TeamType team)
        {
            yield return new WaitForSeconds(_respawnDelay);
            if (brawler == null)
                yield break;

            _pendingRespawns.Remove(brawler);

            Transform spawnPoint = ResolveRotatingRespawnPoint(team);
            if (spawnPoint != null)
                brawler.Respawn(spawnPoint.position);
        }

        private void CancelPendingRespawn(BrawlerController brawler)
        {
            if (brawler == null)
                return;

            if (!_pendingRespawns.TryGetValue(brawler, out Coroutine routine) ||
                routine == null)
            {
                _pendingRespawns.Remove(brawler);
                return;
            }

            StopCoroutine(routine);
            _pendingRespawns.Remove(brawler);
        }

        public void PrepareMatch(List<MatchParticipant> roster)
        {
            PrepareSpawnLists(roster);

            int blueIdx = 0;
            int redIdx = 0;
            int soloIdx = 0;
            List<Transform> blueSpawnPoints = _preparedBlueSpawnPoints.Count > 0
                ? _preparedBlueSpawnPoints
                : _blueSpawnPoints;
            List<Transform> redSpawnPoints = _preparedRedSpawnPoints.Count > 0
                ? _preparedRedSpawnPoints
                : _redSpawnPoints;
            List<Transform> soloSpawnPoints = _preparedSoloSpawnPoints.Count > 0
                ? _preparedSoloSpawnPoints
                : BuildSoloSpawnPointList();
            bool[] blueSpawnClaims = blueSpawnPoints != null
                ? new bool[blueSpawnPoints.Count]
                : new bool[0];
            bool[] redSpawnClaims = redSpawnPoints != null
                ? new bool[redSpawnPoints.Count]
                : new bool[0];
            bool[] soloSpawnClaims = soloSpawnPoints != null
                ? new bool[soloSpawnPoints.Count]
                : new bool[0];

            foreach (var participant in roster)
            {
                // 1. Determine spawn location
                int teamOrdinal;
                List<Transform> spawnList;
                bool[] spawnClaims;

                if (participant.Team == TeamType.Blue)
                {
                    teamOrdinal = blueIdx++;
                    spawnList = blueSpawnPoints;
                    spawnClaims = blueSpawnClaims;
                }
                else if (participant.Team == TeamType.Red)
                {
                    teamOrdinal = redIdx++;
                    spawnList = redSpawnPoints;
                    spawnClaims = redSpawnClaims;
                }
                else
                {
                    teamOrdinal = soloIdx++;
                    spawnList = soloSpawnPoints;
                    spawnClaims = soloSpawnClaims;
                }

                Transform spawnPoint = ResolveSpawnPoint(
                    participant,
                    spawnList,
                    spawnClaims,
                    teamOrdinal);

                if (spawnPoint == null)
                {
                    Debug.LogError($"[SpawnManager] Missing spawn point for {participant.Name} on {participant.Team}.");
                    continue;
                }

                // 2. Instantiate the Brawler Bridge
                GameObject go = Instantiate(_brawlerBasePrefab, spawnPoint.position, spawnPoint.rotation);
                BrawlerController controller = go.GetComponent<BrawlerController>();

                // 3. Inject the specific Brawler Definition
                controller.InitializeFromMatchmaking(
                    participant.SelectedBrawler,
                    participant.Team,
                    participant.SelectedBuild,
                    participant.PowerLevel);

                // 4. If participant is AI, attach the Brain
                if (participant.IsAI)
                {
                    // 1. Add/Get the AI Brain
                    var aiBrain = go.GetComponent<BrawlerAIController>();
                    if (aiBrain == null) aiBrain = go.AddComponent<BrawlerAIController>();

                    // 2. INJECT the reference (Assuming you made the field public or added a setter)
                    aiBrain.SetTarget(controller);
                }
                else
                {
                    var playerSource = go.GetComponent<PlayerCommandSource>();
                    if (playerSource == null)
                        playerSource = go.AddComponent<PlayerCommandSource>();

                    controller.SetCommandSource(playerSource);

                    if (controller.DebugReadySuperAndHyperchargeForPlayer)
                        controller.GrantTestingReadyCharge();

                    SetPlayerTarget(controller.PresentationFollowTarget);
                }
            }
        }

        private List<Transform> ResolveSpawnListForTeam(TeamType team)
        {
            if (team == TeamType.Blue)
                return _preparedBlueSpawnPoints.Count > 0
                    ? _preparedBlueSpawnPoints
                    : _blueSpawnPoints;

            if (team == TeamType.Red)
                return _preparedRedSpawnPoints.Count > 0
                    ? _preparedRedSpawnPoints
                    : _redSpawnPoints;

            if (_preparedSoloSpawnPoints.Count > 0)
                return _preparedSoloSpawnPoints;

            return BuildSoloSpawnPointList();
        }

        private void PrepareSpawnLists(List<MatchParticipant> roster)
        {
            ClearPreparedSpawnLists();
            ClearRuntimeSpawnPoints();
            ResetRespawnCursors();

            int blueRequired = CountParticipantsForTeam(roster, TeamType.Blue);
            int redRequired = CountParticipantsForTeam(roster, TeamType.Red);
            int soloRequired = CountSoloParticipants(roster);

            _spawnPreparationReservedPoints.Clear();
            BuildSeparatedSpawnPointList(
                _blueSpawnPoints,
                blueRequired,
                TeamType.Blue,
                _preparedBlueSpawnPoints,
                _spawnPreparationReservedPoints);
            AddSpawnPoints(_spawnPreparationReservedPoints, _preparedBlueSpawnPoints);

            BuildSeparatedSpawnPointList(
                _redSpawnPoints,
                redRequired,
                TeamType.Red,
                _preparedRedSpawnPoints,
                _spawnPreparationReservedPoints);
            AddSpawnPoints(_spawnPreparationReservedPoints, _preparedRedSpawnPoints);

            BuildSeparatedSpawnPointList(
                _soloSpawnPoints,
                soloRequired,
                TeamType.Neutral,
                _preparedSoloSpawnPoints,
                _spawnPreparationReservedPoints);
            AddSpawnPoints(_spawnPreparationReservedPoints, _preparedSoloSpawnPoints);
        }

        private void ResetRespawnCursors()
        {
            _blueRespawnCursor = 0;
            _redRespawnCursor = 0;
            _soloRespawnCursor = 0;
        }

        private Transform ResolveRotatingRespawnPoint(TeamType team)
        {
            List<Transform> spawnList = ResolveSpawnListForTeam(team);
            if (spawnList == null || spawnList.Count == 0)
                return null;

            int cursor = GetRespawnCursor(team);
            for (int offset = 0; offset < spawnList.Count; offset++)
            {
                int index = (cursor + offset) % spawnList.Count;
                Transform spawnPoint = spawnList[index];
                if (spawnPoint == null)
                    continue;

                SetRespawnCursor(team, index + 1);
                return spawnPoint;
            }

            return null;
        }

        private int GetRespawnCursor(TeamType team)
        {
            if (team == TeamType.Blue)
                return _blueRespawnCursor;

            if (team == TeamType.Red)
                return _redRespawnCursor;

            return _soloRespawnCursor;
        }

        private void SetRespawnCursor(TeamType team, int nextCursor)
        {
            if (team == TeamType.Blue)
            {
                _blueRespawnCursor = nextCursor;
                return;
            }

            if (team == TeamType.Red)
            {
                _redRespawnCursor = nextCursor;
                return;
            }

            _soloRespawnCursor = nextCursor;
        }

        private void ClearPreparedSpawnLists()
        {
            _preparedBlueSpawnPoints.Clear();
            _preparedRedSpawnPoints.Clear();
            _preparedSoloSpawnPoints.Clear();
        }

        private void ClearRuntimeSpawnPoints()
        {
            for (int i = 0; i < _runtimeSpawnPoints.Count; i++)
            {
                Transform spawnPoint = _runtimeSpawnPoints[i];
                if (spawnPoint != null)
                    Destroy(spawnPoint.gameObject);
            }

            _runtimeSpawnPoints.Clear();
        }

        private static int CountParticipantsForTeam(
            List<MatchParticipant> roster,
            TeamType team)
        {
            if (roster == null)
                return 0;

            int count = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                MatchParticipant participant = roster[i];
                if (participant != null && participant.Team == team)
                    count++;
            }

            return count;
        }

        private static int CountSoloParticipants(List<MatchParticipant> roster)
        {
            if (roster == null)
                return 0;

            int count = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                MatchParticipant participant = roster[i];
                if (participant != null &&
                    TeamRelationshipUtility.IsSoloTeam(participant.Team))
                {
                    count++;
                }
            }

            return count;
        }

        private void BuildSeparatedSpawnPointList(
            List<Transform> authoredPoints,
            int requiredCount,
            TeamType team,
            List<Transform> output,
            List<Transform> reservedPoints)
        {
            if (output == null)
                return;

            output.Clear();

            if (requiredCount <= 0)
                return;

            AddSeparatedAuthoredSpawnPoints(authoredPoints, output, reservedPoints);
            EnsureRuntimeSpawnRoot();

            float spacing = Mathf.Max(0.5f, _minimumInitialSpawnSeparation);
            Bounds bounds = ResolvePlayableBounds();
            for (int spacingPass = 0; spacingPass < 3 && output.Count < requiredCount; spacingPass++)
            {
                float passSpacing = spacing * Mathf.Lerp(1f, 0.65f, spacingPass / 2f);
                int attempts = Mathf.Max(1, _runtimeSpawnPlacementAttempts) *
                               Mathf.Max(1, requiredCount - output.Count);

                for (int attempt = 0; attempt < attempts && output.Count < requiredCount; attempt++)
                {
                    if (!TryResolveRuntimeSpawnPosition(
                            bounds,
                            team,
                            output.Count,
                            attempt,
                            output,
                            reservedPoints,
                            passSpacing,
                            out Vector3 position))
                    {
                        continue;
                    }

                    output.Add(CreateRuntimeSpawnPoint(team, output.Count, position));
                }
            }

            // Last-resort fallback: if the map is too tight or has missing
            // ground data, still create unique radial positions instead of
            // reusing an occupied marker.
            while (output.Count < requiredCount)
            {
                Vector3 position = ResolveSeparatedFallbackSpawnPosition(
                    bounds,
                    team,
                    output,
                    reservedPoints,
                    spacing);
                output.Add(CreateRuntimeSpawnPoint(team, output.Count, position));
            }
        }

        private void AddSeparatedAuthoredSpawnPoints(
            List<Transform> authoredPoints,
            List<Transform> output,
            List<Transform> reservedPoints)
        {
            if (authoredPoints == null || output == null)
                return;

            float minSeparationSq = Mathf.Max(0.5f, _minimumInitialSpawnSeparation);
            minSeparationSq *= minSeparationSq;
            for (int i = 0; i < authoredPoints.Count; i++)
            {
                Transform candidate = authoredPoints[i];
                if (candidate == null ||
                    IsTooCloseToExistingSpawn(candidate.position, output, minSeparationSq) ||
                    IsTooCloseToExistingSpawn(candidate.position, reservedPoints, minSeparationSq))
                {
                    continue;
                }

                output.Add(candidate);
            }
        }

        private bool TryResolveRuntimeSpawnPosition(
            Bounds bounds,
            TeamType team,
            int ordinal,
            int attempt,
            List<Transform> existing,
            List<Transform> reservedPoints,
            float minSeparation,
            out Vector3 position)
        {
            float minSeparationSq = minSeparation * minSeparation;
            Vector3 candidate = ResolveRuntimeSpawnCandidate(bounds, team, ordinal, attempt);
            candidate = ResolveGroundedPosition(candidate);

            if (IsTooCloseToExistingSpawn(candidate, existing, minSeparationSq) ||
                IsTooCloseToExistingSpawn(candidate, reservedPoints, minSeparationSq) ||
                IsBlockedSpawnPosition(candidate))
            {
                position = default;
                return false;
            }

            position = candidate;
            return true;
        }

        private Vector3 ResolveSeparatedFallbackSpawnPosition(
            Bounds bounds,
            TeamType team,
            List<Transform> existing,
            List<Transform> reservedPoints,
            float spacing)
        {
            float fallbackSpacing = Mathf.Max(1.5f, spacing * 0.6f);
            float fallbackSpacingSq = fallbackSpacing * fallbackSpacing;
            int baseOrdinal = existing != null ? existing.Count : 0;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                Vector3 candidate = ResolveFallbackRingSpawnPosition(
                    bounds,
                    team,
                    baseOrdinal + attempt,
                    fallbackSpacing);

                if (!IsTooCloseToExistingSpawn(candidate, existing, fallbackSpacingSq) &&
                    !IsTooCloseToExistingSpawn(candidate, reservedPoints, fallbackSpacingSq) &&
                    !IsBlockedSpawnPosition(candidate))
                {
                    return candidate;
                }
            }

            return ResolveFallbackRingSpawnPosition(
                bounds,
                team,
                baseOrdinal + 32,
                Mathf.Max(0.75f, fallbackSpacing * 0.5f));
        }

        private Vector3 ResolveRuntimeSpawnCandidate(
            Bounds bounds,
            TeamType team,
            int ordinal,
            int attempt)
        {
            float inset = Mathf.Max(0.5f, _runtimeSpawnEdgeInset);
            float minX = bounds.min.x + inset;
            float maxX = bounds.max.x - inset;
            float minZ = bounds.min.z + inset;
            float maxZ = bounds.max.z - inset;
            if (minX >= maxX)
            {
                minX = bounds.min.x;
                maxX = bounds.max.x;
            }

            if (minZ >= maxZ)
            {
                minZ = bounds.min.z;
                maxZ = bounds.max.z;
            }

            bool teamMode = team == TeamType.Blue || team == TeamType.Red;
            if (!teamMode)
            {
                return new Vector3(
                    Random.Range(minX, maxX),
                    bounds.center.y,
                    Random.Range(minZ, maxZ));
            }

            float widthT = ((ordinal + attempt * 2) % 7 + 1f) / 8f;
            float x = Mathf.Lerp(minX, maxX, widthT);
            float depth = Mathf.Max(0.1f, maxZ - minZ);
            float sideBand = Mathf.Max(inset, depth * 0.22f);
            float z = team == TeamType.Blue
                ? Random.Range(minZ, Mathf.Min(maxZ, minZ + sideBand))
                : Random.Range(Mathf.Max(minZ, maxZ - sideBand), maxZ);

            return new Vector3(x, bounds.center.y, z);
        }

        private Vector3 ResolveFallbackRingSpawnPosition(
            Bounds bounds,
            TeamType team,
            int ordinal,
            float radius)
        {
            bool teamMode = team == TeamType.Blue || team == TeamType.Red;
            Vector3 center = bounds.center;
            if (teamMode)
            {
                float zOffset = Mathf.Max(2f, bounds.size.z * 0.35f);
                center.z += team == TeamType.Blue ? -zOffset : zOffset;
            }

            float angle = ordinal * 137.50777f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) *
                             Mathf.Max(0.5f, radius);
            Vector3 position = center + offset;
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            return ResolveGroundedPosition(position);
        }

        private Transform CreateRuntimeSpawnPoint(
            TeamType team,
            int ordinal,
            Vector3 position)
        {
            EnsureRuntimeSpawnRoot();

            GameObject spawnObject = new GameObject(
                $"RuntimeSpawn_{team}_{ordinal + 1:00}");
            spawnObject.transform.SetParent(_runtimeSpawnRoot, false);
            spawnObject.transform.position = position;
            spawnObject.transform.rotation = Quaternion.identity;

            _runtimeSpawnPoints.Add(spawnObject.transform);
            return spawnObject.transform;
        }

        private void EnsureRuntimeSpawnRoot()
        {
            if (_runtimeSpawnRoot != null)
                return;

            GameObject root = new GameObject("RuntimeSpawnPoints");
            root.transform.SetParent(transform, false);
            _runtimeSpawnRoot = root.transform;
        }

        private bool IsBlockedSpawnPosition(Vector3 position)
        {
            int obstacleMask = ResolveObstacleMask();
            if (obstacleMask == 0)
                return false;

            return Physics.CheckSphere(
                position + Vector3.up * 0.75f,
                Mathf.Max(0.1f, _runtimeSpawnObstacleClearanceRadius),
                obstacleMask,
                QueryTriggerInteraction.Ignore);
        }

        private static bool IsTooCloseToExistingSpawn(
            Vector3 candidate,
            List<Transform> existing,
            float minSeparationSq)
        {
            if (existing == null || existing.Count == 0)
                return false;

            for (int i = 0; i < existing.Count; i++)
            {
                Transform spawnPoint = existing[i];
                if (spawnPoint == null)
                    continue;

                Vector3 delta = spawnPoint.position - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < minSeparationSq)
                    return true;
            }

            return false;
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

            return new Bounds(transform.position, new Vector3(44f, 0f, 44f));
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

        private Vector3 ResolveGroundedPosition(Vector3 position)
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
                position.y = hit.point.y + Mathf.Max(0f, _spawnGroundOffset);
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
            if (groundLayer >= 0)
                return 1 << groundLayer;

            return Physics.DefaultRaycastLayers & ~ResolveObstacleMask();
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

        private List<Transform> BuildSoloSpawnPointList()
        {
            if (_soloSpawnPoints != null && _soloSpawnPoints.Count > 0)
                return _soloSpawnPoints;

            List<Transform> combined = new List<Transform>(
                (_blueSpawnPoints != null ? _blueSpawnPoints.Count : 0) +
                (_redSpawnPoints != null ? _redSpawnPoints.Count : 0));

            AddSpawnPoints(combined, _blueSpawnPoints);
            AddSpawnPoints(combined, _redSpawnPoints);
            return combined;
        }

        private static void AddSpawnPoints(List<Transform> target, List<Transform> source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null && !target.Contains(source[i]))
                    target.Add(source[i]);
            }
        }

        private static Transform ResolveSpawnPoint(
            MatchParticipant participant,
            List<Transform> spawnList,
            bool[] spawnClaims,
            int teamOrdinal)
        {
            if (spawnList == null || spawnList.Count == 0)
                return null;

            int preferredIndex = AITeamCompositionPlanner.GetPreferredSpawnIndex(
                participant != null ? participant.SelectedBrawler : null,
                spawnList.Count,
                teamOrdinal);

            int selectedIndex = FindNearestFreeSpawnIndex(
                spawnList,
                spawnClaims,
                preferredIndex);

            if (selectedIndex < 0)
                selectedIndex = Mathf.Clamp(preferredIndex, 0, spawnList.Count - 1);

            if (spawnClaims != null && selectedIndex < spawnClaims.Length)
                spawnClaims[selectedIndex] = true;

            return spawnList[selectedIndex];
        }

        private static int FindNearestFreeSpawnIndex(
            List<Transform> spawnList,
            bool[] spawnClaims,
            int preferredIndex)
        {
            if (spawnClaims == null || spawnClaims.Length == 0)
                return Mathf.Clamp(preferredIndex, 0, spawnList.Count - 1);

            int clampedPreferred = Mathf.Clamp(preferredIndex, 0, spawnClaims.Length - 1);
            if (!spawnClaims[clampedPreferred] && spawnList[clampedPreferred] != null)
                return clampedPreferred;

            for (int offset = 1; offset < spawnClaims.Length; offset++)
            {
                int left = clampedPreferred - offset;
                if (left >= 0 && !spawnClaims[left] && spawnList[left] != null)
                    return left;

                int right = clampedPreferred + offset;
                if (right < spawnClaims.Length && !spawnClaims[right] && spawnList[right] != null)
                    return right;
            }

            return -1;
        }
    }
}
