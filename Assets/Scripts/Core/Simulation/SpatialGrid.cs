using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public class SpatialGrid
    {
        private readonly float _cellSize;
        // Map: Cell Coordinate -> List of Entities in that cell
        private readonly Dictionary<Vector2Int, List<ISpatialEntity>> _cells = new Dictionary<Vector2Int, List<ISpatialEntity>>();
        public SpatialGrid(float cellSize) => _cellSize = cellSize;

        // Converts a 3D world position into a 2D Grid Coordinate
        private Vector2Int GetCellCoords(Vector3 position) => new Vector2Int(
            Mathf.FloorToInt(position.x / _cellSize),
            Mathf.FloorToInt(position.z / _cellSize)
        );

        public void Add(ISpatialEntity entity)
        {
            if (!SpatialEntityUtility.IsAlive(entity))
                return;

            var cell = GetCellCoords(entity.Position);
            if (!_cells.ContainsKey(cell)) _cells[cell] = new List<ISpatialEntity>();
            if (!_cells[cell].Contains(entity))
                _cells[cell].Add(entity);
        }

        public void Remove(ISpatialEntity entity, Vector3 lastKnownPos)
        {
            var cell = GetCellCoords(lastKnownPos);
            if (_cells.TryGetValue(cell, out var list))
            {
                list.Remove(entity);
            }
        }

        public void UpdateEntity(ISpatialEntity entity, Vector3 oldPos, Vector3 newPos)
        {
            if (!SpatialEntityUtility.IsAlive(entity))
            {
                Remove(entity, oldPos);
                return;
            }

            Vector2Int oldCell = GetCellCoords(oldPos);
            Vector2Int newCell = GetCellCoords(newPos);

            if (oldCell != newCell)
            {
                Remove(entity, oldPos);
                Add(entity);
            }
        }

        public List<ISpatialEntity> GetEntitiesInCell(Vector3 position)
        {
            return _cells.TryGetValue(GetCellCoords(position), out var list) ? list : null;
        }

        public ISpatialEntity CheckCollision(
            Vector3 position,
            float radius,
            TeamType attackerTeam,
            ProjectileHitTeamRule hitRule,
            HashSet<int> ignoredBrawlerEntityIds = null)
        {
            Vector2Int cell = GetCellCoords(position);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector2Int neighbor = cell + new Vector2Int(x, y);
                    if (_cells.TryGetValue(neighbor, out var entities))
                    {
                        for (int i = 0; i < entities.Count; i++)
                        {
                            var target = entities[i];
                            if (!SpatialEntityUtility.IsAlive(target))
                            {
                                entities.RemoveAt(i);
                                i--;
                                continue;
                            }

                            if (target is BrawlerController brawler &&
                                (brawler.State == null || brawler.State.IsDead))
                            {
                                continue;
                            }

                            if (target is BrawlerController ignoredBrawler &&
                                ignoredBrawlerEntityIds != null &&
                                ignoredBrawlerEntityIds.Contains(ignoredBrawler.EntityID))
                            {
                                continue;
                            }

                            if (target is DeployableController deployable &&
                                (deployable.State == null || deployable.State.IsDead))
                            {
                                continue;
                            }

                            if (target is BreakableObjectController breakable && breakable.IsDestroyed)
                                continue;

                            if (!CanAffectTarget(target, attackerTeam, hitRule))
                            {
                                continue;
                            }

                            float distSq = PlanarDistanceSq(target.Position, position);
                            float combinedRadius = radius + target.CollisionRadius;

                            if (distSq <= (combinedRadius * combinedRadius))
                            {
                                return target;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public ISpatialEntity CheckCollisionSegment(
            Vector3 start,
            Vector3 end,
            float radius,
            TeamType attackerTeam,
            ProjectileHitTeamRule hitRule,
            out Vector3 impactPosition,
            HashSet<int> ignoredBrawlerEntityIds = null)
        {
            impactPosition = end;

            Vector3 planarMovement = end - start;
            planarMovement.y = 0f;
            if (planarMovement.sqrMagnitude <= 0.0001f)
                return CheckCollision(end, radius, attackerTeam, hitRule, ignoredBrawlerEntityIds);

            Vector2Int minCell = GetCellCoords(new Vector3(
                Mathf.Min(start.x, end.x),
                0f,
                Mathf.Min(start.z, end.z)));
            Vector2Int maxCell = GetCellCoords(new Vector3(
                Mathf.Max(start.x, end.x),
                0f,
                Mathf.Max(start.z, end.z)));

            ISpatialEntity best = null;
            float bestT = float.PositiveInfinity;
            float bestDistanceSq = float.PositiveInfinity;

            for (int x = minCell.x - 1; x <= maxCell.x + 1; x++)
            {
                for (int y = minCell.y - 1; y <= maxCell.y + 1; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!_cells.TryGetValue(cell, out var entities))
                        continue;

                    for (int i = 0; i < entities.Count; i++)
                    {
                        var target = entities[i];
                        if (!SpatialEntityUtility.IsAlive(target))
                        {
                            entities.RemoveAt(i);
                            i--;
                            continue;
                        }

                        if (target is BrawlerController brawler &&
                            (brawler.State == null || brawler.State.IsDead))
                        {
                            continue;
                        }

                        if (target is BrawlerController ignoredBrawler &&
                            ignoredBrawlerEntityIds != null &&
                            ignoredBrawlerEntityIds.Contains(ignoredBrawler.EntityID))
                        {
                            continue;
                        }

                        if (target is DeployableController deployable &&
                            (deployable.State == null || deployable.State.IsDead))
                        {
                            continue;
                        }

                        if (target is BreakableObjectController breakable && breakable.IsDestroyed)
                            continue;

                        if (!CanAffectTarget(target, attackerTeam, hitRule))
                            continue;

                        float distanceSq = PlanarSegmentDistanceSq(start, end, target.Position, out float t);
                        float combinedRadius = radius + target.CollisionRadius;
                        if (distanceSq > combinedRadius * combinedRadius)
                            continue;

                        if (best == null ||
                            t < bestT ||
                            (Mathf.Abs(t - bestT) <= 0.0001f && distanceSq < bestDistanceSq))
                        {
                            best = target;
                            bestT = t;
                            bestDistanceSq = distanceSq;
                        }
                    }
                }
            }

            if (best != null)
                impactPosition = Vector3.Lerp(start, end, Mathf.Clamp01(bestT));

            return best;
        }

        public void GetEntitiesInRadiusNonAlloc(Vector3 position, float radius, List<ISpatialEntity> results)
        {
            results.Clear();

            Vector2Int centerCell = GetCellCoords(position);
            int cellRange = Mathf.CeilToInt(radius / _cellSize);
            float radiusSq = radius * radius;

            for (int x = -cellRange; x <= cellRange; x++)
            {
                for (int y = -cellRange; y <= cellRange; y++)
                {
                    if (_cells.TryGetValue(centerCell + new Vector2Int(x, y), out var entities))
                    {
                        for (int i = 0; i < entities.Count; i++)
                        {
                            var entity = entities[i];
                            if (!SpatialEntityUtility.IsAlive(entity))
                            {
                                entities.RemoveAt(i);
                                i--;
                                continue;
                            }

                            if (entity is BrawlerController brawler &&
                                (brawler.State == null || brawler.State.IsDead))
                            {
                                continue;
                            }

                            if (entity is DeployableController deployable &&
                                (deployable.State == null || deployable.State.IsDead))
                            {
                                continue;
                            }

                            float distSq = PlanarDistanceSq(entity.Position, position);
                            if (distSq <= radiusSq)
                            {
                                results.Add(entity);
                            }
                        }
                    }
                }
            }
        }

        // Optional: keep this only for compatibility with older code.
        // Internally it still allocates, so avoid using it in AI code.
        public List<ISpatialEntity> GetEntitiesInRadius(Vector3 position, float radius)
        {
            List<ISpatialEntity> results = new List<ISpatialEntity>();
            GetEntitiesInRadiusNonAlloc(position, radius, results);
            return results;
        }

        private static bool CanAffectTarget(ISpatialEntity target, TeamType attackerTeam, ProjectileHitTeamRule hitRule)
        {
            return TeamRelationshipUtility.CanAffectTeam(
                hitRule,
                attackerTeam,
                target.Team);
        }

        private static float PlanarDistanceSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static float PlanarSegmentDistanceSq(Vector3 start, Vector3 end, Vector3 point, out float t)
        {
            float sx = start.x;
            float sz = start.z;
            float ex = end.x;
            float ez = end.z;
            float px = point.x;
            float pz = point.z;

            float dx = ex - sx;
            float dz = ez - sz;
            float segmentSq = dx * dx + dz * dz;
            if (segmentSq <= 0.0001f)
            {
                t = 0f;
                float startDx = px - sx;
                float startDz = pz - sz;
                return startDx * startDx + startDz * startDz;
            }

            t = ((px - sx) * dx + (pz - sz) * dz) / segmentSq;
            t = Mathf.Clamp01(t);

            float closestX = sx + dx * t;
            float closestZ = sz + dz * t;
            float closestDx = px - closestX;
            float closestDz = pz - closestZ;
            return closestDx * closestDx + closestDz * closestDz;
        }
    }
}
