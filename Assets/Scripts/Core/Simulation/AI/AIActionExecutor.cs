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

        private uint _nextFallbackWanderTick;
        private uint _nextStrafeTick;
        private Vector3 _fallbackWanderPoint;

        public AIActionExecutor(
            BrawlerController brawler,
            BrawlerAIProfile profile,
            NavigationAgent navAgent,
            AIAbilityDecider abilityDecider,
            AISuperDecider superDecider)
        {
            _brawler = brawler;
            _profile = profile;
            _navAgent = navAgent;
            _abilityDecider = abilityDecider;
            _superDecider = superDecider;
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
                    RunHoldRange(targetInfo, currentTick, attackRange, superRange);
                    break;

                case AIActionType.Reposition:
                    RunReposition(targetInfo, currentTick, attackRange, superRange);
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

                default:
                    _navAgent.Stop();
                    break;
            }
        }

        private void RunApproach(AITargetInfo targetInfo, uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (!targetInfo.HasLiveTarget)
                return;

            float desiredArrival = Mathf.Max(0.8f, _profile.GetPreferredAttackRange(idealRange) * 0.9f);
            _navAgent.RequestDestination(targetInfo.Target.Position, desiredArrival);

            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
            _abilityDecider.TryUseGadget(targetInfo.Target, currentTick);
            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
        }

        private void RunHoldRange(AITargetInfo targetInfo, uint currentTick, float attackRange, float superRange)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _navAgent.Stop();
                return;
            }

            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
            _abilityDecider.TryUseGadget(targetInfo.Target, currentTick);
            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);

            if (!_profile.UseStrafe)
            {
                _navAgent.Stop();
                return;
            }

            if (currentTick >= _nextStrafeTick || !_navAgent.HasDestination)
            {
                Vector3 toTarget = (targetInfo.Target.Position - _brawler.Position).normalized;
                Vector3 right = new Vector3(toTarget.z, 0f, -toTarget.x);

                if (right.sqrMagnitude < 0.0001f)
                    right = _brawler.transform.right;

                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 strafePoint = _brawler.Position + (right * side * _profile.StrafeDistance);

                _navAgent.RequestDestination(strafePoint, 0.4f);
                _nextStrafeTick = currentTick + _profile.StrafeRetargetTicks;
            }
        }

        private void RunReposition(AITargetInfo targetInfo, uint currentTick, float attackRange, float superRange)
        {
            if (!targetInfo.HasLiveTarget)
                return;

            Vector3 away = (_brawler.Position - targetInfo.Target.Position).normalized;
            if (away.sqrMagnitude < 0.0001f)
                away = _brawler.transform.forward;

            Vector3 repositionPoint = _brawler.Position + away * _profile.RepositionStepDistance;
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

            Vector3 away = (_brawler.Position - targetInfo.Target.Position).normalized;
            if (away.sqrMagnitude < 0.0001f)
                away = _brawler.transform.forward;

            Vector3 retreatPoint = _brawler.Position + away * _profile.RetreatStepDistance;
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
                return;

            float desiredArrival = Mathf.Max(0.8f, _profile.GetPreferredAttackRange(idealRange) * 0.9f);
            _navAgent.RequestDestination(targetInfo.Target.Position, desiredArrival);

            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
        }
    }
}