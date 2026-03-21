using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIActionExecutor
    {
        private readonly BrawlerController _brawler;
        private readonly BrawlerAIProfile _profile;
        private readonly NavigationAgent _navAgent;
        private readonly AIAbilityDecider _abilityDecider;
        private readonly AISuperDecider _superDecider;
        private readonly AIObjectiveMemory _objectiveMemory;
        private readonly AITeamCoordinator _teamCoordinator;
        private readonly AISpacingUtility _spacingUtility;

        private uint _nextFallbackWanderTick;
        private uint _nextStrafeTick;
        private Vector3 _fallbackWanderPoint;

        public AIActionExecutor(
            BrawlerController brawler,
            BrawlerAIProfile profile,
            NavigationAgent navAgent,
            AIAbilityDecider abilityDecider,
            AISuperDecider superDecider,
            AIObjectiveMemory objectiveMemory,
            AITeamCoordinator teamCoordinator)
        {
            _brawler = brawler;
            _profile = profile;
            _navAgent = navAgent;
            _abilityDecider = abilityDecider;
            _superDecider = superDecider;
            _objectiveMemory = objectiveMemory;
            _teamCoordinator = teamCoordinator;
            _spacingUtility = new AISpacingUtility(brawler);
        }

        public void Execute(
            AIActionType actionType,
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float idealRange,
            float superRange)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    RunApproach(targetInfo, currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.HoldRange:
                    RunHoldRange(targetInfo, currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.Reposition:
                    RunReposition(targetInfo, currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.Retreat:
                    RunRetreat(targetInfo);
                    break;

                case AIActionType.Search:
                    RunSearch(targetInfo, currentTick);
                    break;

                case AIActionType.Wander:
                    RunFallbackWander(currentTick);
                    break;

                case AIActionType.UseSuper:
                    RunUseSuper(targetInfo, currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.Regroup:
                    RunRegroup(currentTick);
                    break;

                case AIActionType.Peel:
                    RunPeel(currentTick, attackRange, idealRange, superRange);
                    break;

                default:
                    _navAgent.Stop();
                    break;
            }
        }

        private void RunApproach(AITargetInfo targetInfo, uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _navAgent.Stop();
                return;
            }

            float preferredRange = _profile.GetPreferredAttackRange(idealRange) + _profile.PreferredCombatOffset;

            Vector3 destination = _spacingUtility.GetPreferredRangePosition(
                targetInfo.Target.Position,
                preferredRange,
                _profile.AllyAvoidanceRadius,
                _profile.AllyAvoidanceWeight);

            _navAgent.RequestDestination(destination, 0.8f);

            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
            _abilityDecider.TryUseGadget(targetInfo.Target, currentTick);
            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
        }

        private void RunHoldRange(AITargetInfo targetInfo, uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _navAgent.Stop();
                return;
            }

            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
            _abilityDecider.TryUseGadget(targetInfo.Target, currentTick);
            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);

            float preferredRange = _profile.GetPreferredAttackRange(idealRange) + _profile.PreferredCombatOffset;

            if (!_profile.UseStrafe)
            {
                Vector3 preferredPoint = _spacingUtility.GetPreferredRangePosition(
                    targetInfo.Target.Position,
                    preferredRange,
                    _profile.AllyAvoidanceRadius,
                    _profile.AllyAvoidanceWeight);

                _navAgent.RequestDestination(preferredPoint, 0.5f);
                return;
            }

            if (currentTick >= _nextStrafeTick || !_navAgent.HasDestination)
            {
                Vector3 toTarget = (targetInfo.Target.Position - _brawler.Position).normalized;
                Vector3 right = new Vector3(toTarget.z, 0f, -toTarget.x);

                if (right.sqrMagnitude < 0.0001f)
                    right = _brawler.transform.right;

                float side = Random.value < 0.5f ? -1f : 1f;

                Vector3 preferredPoint = _spacingUtility.GetPreferredRangePosition(
                    targetInfo.Target.Position,
                    preferredRange,
                    _profile.AllyAvoidanceRadius,
                    _profile.AllyAvoidanceWeight);

                Vector3 strafePoint = preferredPoint + (right * side * _profile.StrafeDistance);

                _navAgent.RequestDestination(strafePoint, 0.4f);
                _nextStrafeTick = currentTick + _profile.StrafeRetargetTicks;
            }
        }

        private void RunReposition(AITargetInfo targetInfo, uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _navAgent.Stop();
                return;
            }

            float desiredRange = Mathf.Max(idealRange * 0.85f, _profile.GetPreferredAttackRange(idealRange));
            Vector3 repositionPoint = _spacingUtility.GetPreferredRangePosition(
                targetInfo.Target.Position,
                desiredRange,
                _profile.AllyAvoidanceRadius,
                _profile.AllyAvoidanceWeight);

            _navAgent.RequestDestination(repositionPoint, 0.4f);

            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
        }

        private void RunRetreat(AITargetInfo targetInfo)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _navAgent.Stop();
                return;
            }

            Vector3 retreatPoint = _spacingUtility.GetRetreatPosition(
                targetInfo.Target.Position,
                _profile.RetreatStepDistance,
                _profile.AllyAvoidanceRadius,
                _profile.AllyAvoidanceWeight);

            _navAgent.RequestDestination(retreatPoint, 0.5f);
        }

        private void RunSearch(AITargetInfo targetInfo, uint currentTick)
        {
            if (targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
            {
                _navAgent.RequestDestination(targetInfo.LastKnownPosition, 1.0f);
                return;
            }

            if (AITeamMemory.TryGetRecentHotspot(_brawler.Team, currentTick, _profile.SharedHotspotMemoryTicks, out var destination))
            {
                _navAgent.RequestDestination(destination, 1.0f);
                return;
            }

            if (_objectiveMemory != null)
            {
                var objective = _objectiveMemory.GetBestObjective(_brawler.Position, _profile.PreferredObjective);
                if (objective != null)
                {
                    _navAgent.RequestDestination(objective.transform.position, 1.0f);
                    return;
                }
            }

            _navAgent.Stop();
        }

        private void RunFallbackWander(uint currentTick)
        {
            if (currentTick >= _nextFallbackWanderTick || !_navAgent.HasDestination)
            {
                Vector2 random2D = Random.insideUnitCircle * _profile.FallbackWanderRadius;
                _fallbackWanderPoint = _brawler.Position + new Vector3(random2D.x, 0f, random2D.y);
                _navAgent.RequestDestination(_fallbackWanderPoint, 0.5f);
                _nextFallbackWanderTick = currentTick + _profile.FallbackWanderRetargetTicks;
            }
        }

        private void RunUseSuper(AITargetInfo targetInfo, uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _navAgent.Stop();
                return;
            }

            float preferredRange = _profile.GetPreferredAttackRange(idealRange) + _profile.PreferredCombatOffset;

            Vector3 destination = _spacingUtility.GetPreferredRangePosition(
                targetInfo.Target.Position,
                preferredRange,
                _profile.AllyAvoidanceRadius,
                _profile.AllyAvoidanceWeight);

            _navAgent.RequestDestination(destination, 0.8f);

            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
        }

        private void RunRegroup(uint currentTick)
        {
            if (_teamCoordinator != null && _teamCoordinator.TryGetRegroupPoint(currentTick, out var point))
            {
                _navAgent.RequestDestination(point, 1.0f);
                return;
            }

            _navAgent.Stop();
        }

        private void RunPeel(uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (_teamCoordinator == null || !_teamCoordinator.TryGetAllyUnderThreat(currentTick, out var ally) || ally == null)
            {
                _navAgent.Stop();
                return;
            }

            if (ally.State != null && ally.State.ThreatTracker != null)
            {
                int attackerId = ally.State.ThreatTracker.GetHighestThreatTarget(currentTick, 240);

                if (attackerId != 0)
                {
                    var entity = CombatRegistry.GetEntity(attackerId);

                    if (entity is BrawlerController attacker && attacker.State != null && !attacker.State.IsDead)
                    {
                        float preferredRange = _profile.GetPreferredAttackRange(idealRange) + _profile.PreferredCombatOffset;

                        Vector3 destination = _spacingUtility.GetPreferredRangePosition(
                            attacker.Position,
                            preferredRange,
                            _profile.AllyAvoidanceRadius,
                            _profile.AllyAvoidanceWeight);

                        _navAgent.RequestDestination(destination, 1.0f);
                        _abilityDecider.TryUseMainAttack(attacker, currentTick, attackRange);
                        _superDecider.TryUseSuper(attacker, currentTick, superRange);
                        return;
                    }
                }
            }

            _navAgent.RequestDestination(ally.Position, 1.0f);
        }
    }
}