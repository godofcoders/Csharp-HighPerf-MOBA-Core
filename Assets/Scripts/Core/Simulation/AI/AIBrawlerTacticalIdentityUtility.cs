using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIBrawlerTacticalIdentityContext
    {
        public readonly BrawlerTacticalIdentity Identity;
        public readonly AIDifficultyLevel Difficulty;
        public readonly AIPersonalityType Personality;
        public readonly bool HasTarget;
        public readonly float TargetDistance;
        public readonly float OwnAttackRange;
        public readonly float OwnPreferredRange;
        public readonly float OwnTooCloseDistance;
        public readonly float TargetAttackRange;
        public readonly float TargetHealthRatio;
        public readonly int TargetCarriedGems;
        public readonly int EnemyClusterCount;
        public readonly int WoundedAllyCount;
        public readonly int CriticalAllyCount;
        public readonly bool SuperReady;
        public readonly bool SuperCanReachTarget;
        public readonly bool SelfNearCover;
        public readonly bool TargetNearCover;
        public readonly bool HasCoverBetween;
        public readonly bool DirectFire;
        public readonly bool ObjectivePressure;
        public readonly bool Behind;
        public readonly bool EnemyCountdown;

        public AIBrawlerTacticalIdentityContext(
            BrawlerTacticalIdentity identity,
            AIDifficultyLevel difficulty,
            AIPersonalityType personality,
            bool hasTarget,
            float targetDistance,
            float ownAttackRange,
            float ownPreferredRange,
            float ownTooCloseDistance,
            float targetAttackRange,
            float targetHealthRatio,
            int targetCarriedGems,
            int enemyClusterCount,
            int woundedAllyCount,
            int criticalAllyCount,
            bool superReady,
            bool superCanReachTarget,
            bool selfNearCover,
            bool targetNearCover,
            bool hasCoverBetween,
            bool directFire,
            bool objectivePressure,
            bool behind,
            bool enemyCountdown)
        {
            Identity = identity;
            Difficulty = difficulty;
            Personality = personality;
            HasTarget = hasTarget;
            TargetDistance = targetDistance;
            OwnAttackRange = ownAttackRange;
            OwnPreferredRange = ownPreferredRange;
            OwnTooCloseDistance = ownTooCloseDistance;
            TargetAttackRange = targetAttackRange;
            TargetHealthRatio = targetHealthRatio;
            TargetCarriedGems = targetCarriedGems;
            EnemyClusterCount = enemyClusterCount;
            WoundedAllyCount = woundedAllyCount;
            CriticalAllyCount = criticalAllyCount;
            SuperReady = superReady;
            SuperCanReachTarget = superCanReachTarget;
            SelfNearCover = selfNearCover;
            TargetNearCover = targetNearCover;
            HasCoverBetween = hasCoverBetween;
            DirectFire = directFire;
            ObjectivePressure = objectivePressure;
            Behind = behind;
            EnemyCountdown = enemyCountdown;
        }
    }

    public readonly struct AIBrawlerTacticalTargetContext
    {
        public readonly BrawlerTacticalIdentity Identity;
        public readonly AIDifficultyLevel Difficulty;
        public readonly AIPersonalityType Personality;
        public readonly float Distance;
        public readonly float OwnAttackRange;
        public readonly float TargetHealthRatio;
        public readonly int TargetCarriedGems;
        public readonly int EnemyClusterCount;
        public readonly bool IsCurrentTarget;
        public readonly bool SelfSuperReady;
        public readonly bool SelfSuperCanReachTarget;

        public AIBrawlerTacticalTargetContext(
            BrawlerTacticalIdentity identity,
            AIDifficultyLevel difficulty,
            AIPersonalityType personality,
            float distance,
            float ownAttackRange,
            float targetHealthRatio,
            int targetCarriedGems,
            int enemyClusterCount,
            bool isCurrentTarget,
            bool selfSuperReady,
            bool selfSuperCanReachTarget)
        {
            Identity = identity;
            Difficulty = difficulty;
            Personality = personality;
            Distance = distance;
            OwnAttackRange = ownAttackRange;
            TargetHealthRatio = targetHealthRatio;
            TargetCarriedGems = targetCarriedGems;
            EnemyClusterCount = enemyClusterCount;
            IsCurrentTarget = isCurrentTarget;
            SelfSuperReady = selfSuperReady;
            SelfSuperCanReachTarget = selfSuperCanReachTarget;
        }
    }

    public readonly struct AIBrawlerTacticalIdentityEvaluation
    {
        public readonly bool HasDelta;
        public readonly float Delta;
        public readonly string Reason;

        public AIBrawlerTacticalIdentityEvaluation(float delta, string reason)
        {
            Delta = delta;
            Reason = reason;
            HasDelta = Mathf.Abs(delta) > 0.01f;
        }

        public static AIBrawlerTacticalIdentityEvaluation None =>
            new AIBrawlerTacticalIdentityEvaluation(0f, string.Empty);
    }

    public static class AIBrawlerTacticalIdentityUtility
    {
        public static BrawlerTacticalIdentity ResolveIdentity(BrawlerDefinition definition)
        {
            if (definition == null)
                return BrawlerTacticalIdentity.Auto;

            if (definition.TacticalIdentity != BrawlerTacticalIdentity.Auto)
                return definition.TacticalIdentity;

            string name = definition.BrawlerName;
            if (!string.IsNullOrEmpty(name))
            {
                string lower = name.ToLowerInvariant();
                if (lower.Contains("byron"))
                    return BrawlerTacticalIdentity.Byron;
                if (lower.Contains("jessie") || lower.Contains("jesse"))
                    return BrawlerTacticalIdentity.Jessie;
                if (lower.Contains("primo"))
                    return BrawlerTacticalIdentity.ElPrimo;
                if (lower.Contains("colt"))
                    return BrawlerTacticalIdentity.Colt;
                if (lower == "bo" || lower.Contains(" bo"))
                    return BrawlerTacticalIdentity.Bo;
                if (lower.Contains("barley"))
                    return BrawlerTacticalIdentity.Barley;
                if (lower.Contains("piper"))
                    return BrawlerTacticalIdentity.Piper;
            }

            return BrawlerTacticalIdentity.Auto;
        }

        public static float GetDiscipline(BrawlerAIProfile profile)
        {
            if (profile == null)
                return 0f;

            float difficulty;
            switch (profile.Difficulty)
            {
                case AIDifficultyLevel.Easy:
                    difficulty = 0.28f;
                    break;
                case AIDifficultyLevel.Normal:
                    difficulty = 0.62f;
                    break;
                case AIDifficultyLevel.Hard:
                    difficulty = 1f;
                    break;
                default:
                    difficulty = 0.62f;
                    break;
            }

            float execution =
                1f -
                Mathf.Clamp01(profile.AimErrorDegrees / 14f) * 0.18f -
                Mathf.Clamp01(profile.ReactionDelayTicks / 18f) * 0.12f;

            return Mathf.Clamp01(difficulty * Mathf.Clamp(execution, 0.65f, 1.05f));
        }

        public static AIBrawlerTacticalIdentityEvaluation EvaluateAction(
            AIActionType actionType,
            BrawlerAIProfile profile,
            in AIBrawlerTacticalIdentityContext context)
        {
            float discipline = GetDiscipline(profile);
            if (discipline <= 0.01f || context.Identity == BrawlerTacticalIdentity.Auto)
                return AIBrawlerTacticalIdentityEvaluation.None;

            float delta;
            string reason;
            switch (context.Identity)
            {
                case BrawlerTacticalIdentity.Byron:
                    delta = EvaluateByron(actionType, context, out reason);
                    break;
                case BrawlerTacticalIdentity.Jessie:
                    delta = EvaluateJessie(actionType, context, out reason);
                    break;
                case BrawlerTacticalIdentity.ElPrimo:
                    delta = EvaluateElPrimo(actionType, context, out reason);
                    break;
                case BrawlerTacticalIdentity.Colt:
                    delta = EvaluateColt(actionType, context, out reason);
                    break;
                case BrawlerTacticalIdentity.Bo:
                    delta = EvaluateBo(actionType, context, out reason);
                    break;
                case BrawlerTacticalIdentity.Barley:
                    delta = EvaluateBarley(actionType, context, out reason);
                    break;
                case BrawlerTacticalIdentity.Piper:
                    delta = EvaluatePiper(actionType, context, out reason);
                    break;
                default:
                    return AIBrawlerTacticalIdentityEvaluation.None;
            }

            return new AIBrawlerTacticalIdentityEvaluation(
                Mathf.Clamp(delta * discipline, -70f, 70f),
                reason);
        }

        public static float EvaluateTargetScore(
            BrawlerAIProfile profile,
            in AIBrawlerTacticalTargetContext context,
            out string reason)
        {
            reason = string.Empty;
            float discipline = GetDiscipline(profile);
            if (discipline <= 0.01f || context.Identity == BrawlerTacticalIdentity.Auto)
                return 0f;

            float closePressure =
                1f - Mathf.Clamp01(context.Distance / Mathf.Max(1f, context.OwnAttackRange + 1.5f));
            float lowHealth = 1f - Mathf.Clamp01(context.TargetHealthRatio);
            float inRange = 1f - Mathf.Clamp01(context.Distance / Mathf.Max(1f, context.OwnAttackRange));
            float cluster = Mathf.Max(0, context.EnemyClusterCount - 1);
            float score = 0f;

            switch (context.Identity)
            {
                case BrawlerTacticalIdentity.Byron:
                    score += lowHealth * 22f;
                    score += Mathf.Clamp01(inRange) * 8f;
                    score -= closePressure * 8f;
                    reason = "byron_chip_low";
                    break;

                case BrawlerTacticalIdentity.Jessie:
                    score += cluster * 18f;
                    score += Mathf.Clamp01(inRange) * 8f;
                    score -= closePressure * 6f;
                    reason = "jessie_bounce";
                    break;

                case BrawlerTacticalIdentity.ElPrimo:
                    score += closePressure * 34f;
                    if (context.SelfSuperCanReachTarget)
                        score += 28f;
                    if (context.Distance > context.OwnAttackRange * 2.2f &&
                        !context.SelfSuperCanReachTarget &&
                        context.TargetCarriedGems <= 0 &&
                        context.TargetHealthRatio > 0.32f)
                    {
                        score -= 26f;
                    }
                    reason = "primo_close_or_jump";
                    break;

                case BrawlerTacticalIdentity.Colt:
                    score += lowHealth * 24f;
                    score += Mathf.Clamp01(inRange) * 14f;
                    score += context.TargetCarriedGems * 3f;
                    reason = "colt_damage_lane";
                    break;

                case BrawlerTacticalIdentity.Bo:
                    score += cluster * 12f;
                    score += Mathf.Clamp01(inRange) * 10f;
                    score += context.TargetCarriedGems * 4f;
                    reason = "bo_mid_control";
                    break;

                case BrawlerTacticalIdentity.Barley:
                    score += cluster * 16f;
                    score += lowHealth * 10f;
                    if (context.Distance < context.OwnAttackRange * 0.45f)
                        score -= 12f;
                    reason = "barley_thrower_control";
                    break;

                case BrawlerTacticalIdentity.Piper:
                    float farLane = Mathf.Clamp01(context.Distance / Mathf.Max(1f, context.OwnAttackRange));
                    score += farLane * 24f;
                    score += lowHealth * 22f;
                    score += context.TargetCarriedGems * 3f;
                    if (context.Distance < context.OwnAttackRange * 0.45f)
                        score -= 20f;
                    reason = "piper_long_range_pick";
                    break;
            }

            if (context.IsCurrentTarget && score > 0f)
                score *= 0.75f;

            return Mathf.Clamp(score * discipline, -35f, 55f);
        }

        public static string GetIdentityLabel(BrawlerTacticalIdentity identity)
        {
            switch (identity)
            {
                case BrawlerTacticalIdentity.Byron:
                    return "Byron";
                case BrawlerTacticalIdentity.Jessie:
                    return "Jessie";
                case BrawlerTacticalIdentity.ElPrimo:
                    return "ElPrimo";
                case BrawlerTacticalIdentity.Colt:
                    return "Colt";
                case BrawlerTacticalIdentity.Bo:
                    return "Bo";
                case BrawlerTacticalIdentity.Barley:
                    return "Barley";
                case BrawlerTacticalIdentity.Piper:
                    return "Piper";
                default:
                    return "Auto";
            }
        }

        private static float EvaluateByron(
            AIActionType actionType,
            in AIBrawlerTacticalIdentityContext context,
            out string reason)
        {
            reason = "byron_support_range";
            bool closeThreat = context.HasTarget &&
                               context.TargetDistance <= Mathf.Max(3f, context.OwnTooCloseDistance + 0.75f);
            bool inChipRange = context.HasTarget &&
                               context.TargetDistance <= context.OwnAttackRange + 0.75f &&
                               context.TargetDistance >= context.OwnPreferredRange * 0.65f;
            bool healPriority = context.CriticalAllyCount > 0;

            switch (actionType)
            {
                case AIActionType.HoldRange:
                    return (inChipRange ? 24f : 0f) + (healPriority ? 10f : 0f);
                case AIActionType.Reposition:
                    return (closeThreat ? 34f : 0f) +
                           (!context.SelfNearCover && context.HasTarget ? 8f : 0f);
                case AIActionType.Retreat:
                    return closeThreat ? 12f : 0f;
                case AIActionType.Approach:
                    return closeThreat ? -36f : context.HasTarget && !inChipRange ? -8f : 0f;
                case AIActionType.Peel:
                    return healPriority ? 34f : context.WoundedAllyCount > 0 ? 12f : 0f;
                case AIActionType.Regroup:
                    return healPriority ? 16f : 0f;
                case AIActionType.UseSuper:
                    return (healPriority ? 30f : 0f) +
                           (context.HasTarget && context.TargetHealthRatio <= 0.36f ? 24f : 0f) +
                           (context.HasCoverBetween && context.HasTarget ? 10f : 0f);
                default:
                    return 0f;
            }
        }

        private static float EvaluateJessie(
            AIActionType actionType,
            in AIBrawlerTacticalIdentityContext context,
            out string reason)
        {
            reason = "jessie_bounce_turret";
            bool closeThreat = context.HasTarget && context.TargetDistance <= 3.25f;
            bool midRange = context.HasTarget &&
                            context.TargetDistance >= 3.25f &&
                            context.TargetDistance <= context.OwnAttackRange + 0.75f;
            bool bounceWindow = context.EnemyClusterCount >= 2;

            switch (actionType)
            {
                case AIActionType.HoldRange:
                    return (midRange ? 20f : 0f) + (bounceWindow ? 18f : 0f);
                case AIActionType.Reposition:
                    return closeThreat ? 28f : context.TargetNearCover ? 6f : 0f;
                case AIActionType.Approach:
                    return closeThreat ? -28f : bounceWindow ? 6f : 0f;
                case AIActionType.UseSuper:
                    return context.SuperReady && (bounceWindow || context.ObjectivePressure)
                        ? 30f
                        : context.SuperReady ? 10f : 0f;
                case AIActionType.Objective:
                    return context.ObjectivePressure ? 14f : 0f;
                default:
                    return 0f;
            }
        }

        private static float EvaluateElPrimo(
            AIActionType actionType,
            in AIBrawlerTacticalIdentityContext context,
            out string reason)
        {
            reason = "primo_cover_jump";
            bool close = context.HasTarget &&
                         context.TargetDistance <= context.OwnAttackRange + 0.55f;
            bool farOpenChase = context.HasTarget &&
                                context.TargetDistance > context.OwnAttackRange * 2.0f &&
                                !context.SuperCanReachTarget;
            bool coverApproach = context.HasTarget &&
                                 (context.SelfNearCover || context.HasCoverBetween);

            switch (actionType)
            {
                case AIActionType.Approach:
                    return close ? 34f : farOpenChase ? -46f : coverApproach ? 10f : -8f;
                case AIActionType.Reposition:
                    return farOpenChase ? (context.SelfNearCover ? 24f : 36f) : coverApproach ? 14f : 0f;
                case AIActionType.HoldRange:
                    return farOpenChase ? 14f : close ? -8f : 0f;
                case AIActionType.Search:
                case AIActionType.Objective:
                    return !context.HasTarget || farOpenChase ? 10f : 0f;
                case AIActionType.UseSuper:
                    return context.SuperCanReachTarget
                        ? 44f +
                          (context.EnemyClusterCount >= 2 ? 12f : 0f) +
                          (context.TargetHealthRatio <= 0.42f ? 8f : 0f)
                        : 0f;
                default:
                    return 0f;
            }
        }

        private static float EvaluateColt(
            AIActionType actionType,
            in AIBrawlerTacticalIdentityContext context,
            out string reason)
        {
            reason = "colt_range_damage";
            bool inRange = context.HasTarget &&
                           context.TargetDistance <= context.OwnAttackRange + 0.75f;
            bool tooClose = context.HasTarget && context.TargetDistance <= context.OwnTooCloseDistance + 0.35f;
            bool finisher = context.HasTarget && context.TargetHealthRatio <= 0.36f;

            switch (actionType)
            {
                case AIActionType.HoldRange:
                    return inRange ? 22f : 0f;
                case AIActionType.Approach:
                    return finisher ? 22f : context.HasTarget && !inRange ? 12f : 0f;
                case AIActionType.Reposition:
                    return tooClose ? 18f : 0f;
                case AIActionType.UseSuper:
                    return context.SuperReady &&
                           (finisher || context.EnemyClusterCount >= 2 || context.ObjectivePressure)
                        ? 26f
                        : 0f;
                default:
                    return 0f;
            }
        }

        private static float EvaluateBo(
            AIActionType actionType,
            in AIBrawlerTacticalIdentityContext context,
            out string reason)
        {
            reason = "bo_lane_mines";
            bool closeThreat = context.HasTarget && context.TargetDistance <= 3.2f;
            bool controlWindow = context.ObjectivePressure ||
                                 context.EnemyCountdown ||
                                 context.EnemyClusterCount >= 2 ||
                                 context.TargetCarriedGems >= 2;
            bool inRange = context.HasTarget &&
                           context.TargetDistance <= context.OwnAttackRange + 0.75f;

            switch (actionType)
            {
                case AIActionType.HoldRange:
                    return inRange ? 22f : 0f;
                case AIActionType.Reposition:
                    return closeThreat ? 24f : 6f;
                case AIActionType.Approach:
                    return closeThreat ? -18f : context.HasTarget && !inRange ? -6f : 0f;
                case AIActionType.UseSuper:
                    return context.SuperReady && controlWindow ? 38f : context.SuperReady ? 8f : 0f;
                case AIActionType.Objective:
                    return context.ObjectivePressure ? 16f : 0f;
                case AIActionType.Search:
                    return context.ObjectivePressure ? 8f : 0f;
                default:
                    return 0f;
            }
        }

        private static float EvaluateBarley(
            AIActionType actionType,
            in AIBrawlerTacticalIdentityContext context,
            out string reason)
        {
            reason = "barley_thrower_cover";
            bool closeThreat = context.HasTarget && context.TargetDistance <= 4f;
            bool safeThrow = context.HasTarget &&
                             (context.HasCoverBetween || context.SelfNearCover) &&
                             context.TargetDistance <= context.OwnAttackRange + 0.75f;
            bool exposedLine = context.HasTarget &&
                               !context.HasCoverBetween &&
                               !context.SelfNearCover &&
                               context.TargetDistance <= context.TargetAttackRange + 0.75f;
            bool areaValue = context.EnemyClusterCount >= 2 ||
                             context.ObjectivePressure ||
                             context.TargetCarriedGems >= 2;

            switch (actionType)
            {
                case AIActionType.HoldRange:
                    return safeThrow ? 28f : 0f;
                case AIActionType.Reposition:
                    return closeThreat ? 36f : exposedLine ? 30f : !context.SelfNearCover ? 10f : 0f;
                case AIActionType.Approach:
                    return closeThreat || exposedLine ? -42f : safeThrow ? -8f : 0f;
                case AIActionType.UseSuper:
                    return context.SuperReady && areaValue
                        ? 38f +
                          (context.EnemyClusterCount >= 2 ? 10f : 0f)
                        : 0f;
                case AIActionType.Objective:
                    return context.ObjectivePressure && safeThrow ? 14f : 0f;
                default:
                    return 0f;
            }
        }

        private static float EvaluatePiper(
            AIActionType actionType,
            in AIBrawlerTacticalIdentityContext context,
            out string reason)
        {
            reason = "piper_sniper_lane";
            bool hasTarget = context.HasTarget;
            bool dangerClose = hasTarget && context.TargetDistance <= context.OwnAttackRange * 0.42f;
            bool sweetSpot = hasTarget &&
                             context.TargetDistance >= context.OwnAttackRange * 0.62f &&
                             context.TargetDistance <= context.OwnAttackRange + 0.85f;
            bool farOut = hasTarget && context.TargetDistance > context.OwnAttackRange + 0.85f;
            bool escapeSuper = context.SuperReady && dangerClose;
            bool valuablePick = hasTarget &&
                                (context.TargetHealthRatio <= 0.42f ||
                                 context.TargetCarriedGems >= 2 ||
                                 context.ObjectivePressure);

            switch (actionType)
            {
                case AIActionType.HoldRange:
                    return sweetSpot ? 34f : hasTarget && !dangerClose ? 10f : 0f;
                case AIActionType.Reposition:
                    return dangerClose ? 38f : !context.SelfNearCover && hasTarget ? 16f : 0f;
                case AIActionType.Retreat:
                    return dangerClose ? 22f : 0f;
                case AIActionType.Approach:
                    return dangerClose ? -48f : farOut && valuablePick ? 8f : -8f;
                case AIActionType.UseSuper:
                    return escapeSuper ? 42f : 0f;
                case AIActionType.Objective:
                    return context.ObjectivePressure && sweetSpot ? 12f : 0f;
                default:
                    return 0f;
            }
        }
    }
}
