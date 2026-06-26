using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIGemPickupDecision
    {
        public readonly bool HasPickup;
        public readonly bool ShouldPickup;
        public readonly Vector3 Position;
        public readonly int Value;
        public readonly int ClusterValue;
        public readonly float Distance;
        public readonly float Score;
        public readonly float ThreatPressure;
        public readonly bool IsSafe;
        public readonly bool IsThresholdPickup;
        public readonly bool IsDenyPickup;
        public readonly string Reason;

        public AIGemPickupDecision(
            bool hasPickup,
            bool shouldPickup,
            Vector3 position,
            int value,
            int clusterValue,
            float distance,
            float score,
            float threatPressure,
            bool isSafe,
            bool isThresholdPickup,
            bool isDenyPickup,
            string reason)
        {
            HasPickup = hasPickup;
            ShouldPickup = shouldPickup;
            Position = position;
            Value = value;
            ClusterValue = clusterValue;
            Distance = distance;
            Score = score;
            ThreatPressure = threatPressure;
            IsSafe = isSafe;
            IsThresholdPickup = isThresholdPickup;
            IsDenyPickup = isDenyPickup;
            Reason = string.IsNullOrEmpty(reason) ? "none" : reason;
        }

        public static AIGemPickupDecision None(string reason = "none")
        {
            return new AIGemPickupDecision(
                false,
                false,
                Vector3.zero,
                0,
                0,
                0f,
                0f,
                0f,
                false,
                false,
                false,
                reason);
        }

        public string GetDebugSummary()
        {
            if (!HasPickup)
                return $"GemPickup=None Reason={Reason}";

            return
                $"GemPickup={(ShouldPickup ? "Go" : "Hold")} " +
                $"Value={Value}/{ClusterValue} " +
                $"Dist={Distance:0.0} " +
                $"Score={Score:0.0} " +
                $"Threat={ThreatPressure:0.00} " +
                $"Safe={IsSafe} " +
                $"Secure={IsThresholdPickup} " +
                $"Deny={IsDenyPickup} " +
                $"Reason={Reason}";
        }
    }

    public readonly struct AIGemPickupCandidateContext
    {
        public readonly int SelfCarriedGems;
        public readonly int OwnGems;
        public readonly int EnemyGems;
        public readonly int GemsToWin;
        public readonly int GemValue;
        public readonly int ClusterValue;
        public readonly float Distance;
        public readonly float SearchRadius;
        public readonly float HealthRatio;
        public readonly float ThreatPressure;
        public readonly bool OwnCountdown;
        public readonly bool EnemyCountdown;
        public readonly AIGameModeMacroCall MacroCall;
        public readonly float MinimumScore;
        public readonly float WinTimerRemainingSeconds;

        public AIGemPickupCandidateContext(
            int selfCarriedGems,
            int ownGems,
            int enemyGems,
            int gemsToWin,
            int gemValue,
            int clusterValue,
            float distance,
            float searchRadius,
            float healthRatio,
            float threatPressure,
            bool ownCountdown,
            bool enemyCountdown,
            AIGameModeMacroCall macroCall,
            float minimumScore,
            float winTimerRemainingSeconds = 0f)
        {
            SelfCarriedGems = selfCarriedGems;
            OwnGems = ownGems;
            EnemyGems = enemyGems;
            GemsToWin = gemsToWin;
            GemValue = gemValue;
            ClusterValue = clusterValue;
            Distance = distance;
            SearchRadius = searchRadius;
            HealthRatio = healthRatio;
            ThreatPressure = threatPressure;
            OwnCountdown = ownCountdown;
            EnemyCountdown = enemyCountdown;
            MacroCall = macroCall;
            MinimumScore = minimumScore;
            WinTimerRemainingSeconds = winTimerRemainingSeconds;
        }
    }

    public readonly struct AIGemPickupEvaluation
    {
        public readonly bool ShouldPickup;
        public readonly float Score;
        public readonly bool IsSafe;
        public readonly bool IsThresholdPickup;
        public readonly bool IsDenyPickup;
        public readonly string Reason;

        public AIGemPickupEvaluation(
            bool shouldPickup,
            float score,
            bool isSafe,
            bool isThresholdPickup,
            bool isDenyPickup,
            string reason)
        {
            ShouldPickup = shouldPickup;
            Score = score;
            IsSafe = isSafe;
            IsThresholdPickup = isThresholdPickup;
            IsDenyPickup = isDenyPickup;
            Reason = string.IsNullOrEmpty(reason) ? "none" : reason;
        }
    }

    public readonly struct AIGemMineControlContext
    {
        public readonly AIGameModeMacroState MacroState;
        public readonly int SelfCarriedGems;
        public readonly float HealthRatio;
        public readonly float AllyPressure;
        public readonly bool HasLiveTarget;
        public readonly bool HasGemPickup;
        public readonly bool ShouldPickupGem;
        public readonly float GemPickupScore;

        public AIGemMineControlContext(
            AIGameModeMacroState macroState,
            int selfCarriedGems,
            float healthRatio,
            float allyPressure,
            bool hasLiveTarget,
            bool hasGemPickup,
            bool shouldPickupGem,
            float gemPickupScore)
        {
            MacroState = macroState;
            SelfCarriedGems = Mathf.Max(0, selfCarriedGems);
            HealthRatio = Mathf.Clamp01(healthRatio);
            AllyPressure = Mathf.Max(0f, allyPressure);
            HasLiveTarget = hasLiveTarget;
            HasGemPickup = hasGemPickup;
            ShouldPickupGem = shouldPickupGem;
            GemPickupScore = Mathf.Max(0f, gemPickupScore);
        }
    }

    public readonly struct AIGemMineControlEvaluation
    {
        public readonly float Delta;
        public readonly string Reason;

        public AIGemMineControlEvaluation(float delta, string reason)
        {
            Delta = delta;
            Reason = string.IsNullOrEmpty(reason) ? "mine_none" : reason;
        }

        public bool HasDelta => Delta < -0.01f || Delta > 0.01f;

        public static AIGemMineControlEvaluation None =>
            new AIGemMineControlEvaluation(0f, "mine_none");
    }

    public static class AIGemGrabObjectiveUtility
    {
        private const float MaxMineControlDelta = 38f;

        public static bool TryFindBestPickup(
            BrawlerController self,
            BrawlerAIProfile profile,
            AIGameModeMacroState macroState,
            bool hasThreatCenter,
            Vector3 threatCenter,
            float threatCenterPressure,
            bool hasEnemyHotspot,
            Vector3 enemyHotspot,
            float enemyHotspotPressure,
            out AIGemPickupDecision decision)
        {
            decision = AIGemPickupDecision.None();

            if (self == null ||
                self.State == null ||
                self.State.IsDead ||
                profile == null ||
                profile.GemPickupSearchRadius <= 0f)
            {
                return false;
            }

            float searchRadius = Mathf.Max(0.1f, profile.GemPickupSearchRadius);
            float searchRadiusSq = searchRadius * searchRadius;
            bool found = false;
            AIGemPickupDecision best = AIGemPickupDecision.None("no_gems");
            AStarSolver pathfinder = SimulationClock.Pathfinder;

            for (int i = 0; i < Gem.All.Count; i++)
            {
                Gem gem = Gem.All[i];
                if (gem == null || gem.IsPickedUp)
                    continue;

                Vector3 gemPosition = gem.transform.position;
                float dx = gemPosition.x - self.Position.x;
                float dz = gemPosition.z - self.Position.z;
                float distanceSq = dx * dx + dz * dz;
                if (distanceSq > searchRadiusSq)
                    continue;

                if (pathfinder != null &&
                    !pathfinder.IsWalkable(pathfinder.GetGridCoords(gemPosition)))
                {
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSq);
                int clusterValue = CountClusterValue(
                    gem,
                    gemPosition,
                    Mathf.Max(0.1f, profile.GemPickupClusterRadius));
                float threatPressure = CalculateThreatPressure(
                    gemPosition,
                    profile,
                    hasThreatCenter,
                    threatCenter,
                    threatCenterPressure,
                    hasEnemyHotspot,
                    enemyHotspot,
                    enemyHotspotPressure);

                AIGemPickupEvaluation evaluation = EvaluateCandidate(
                    profile,
                    new AIGemPickupCandidateContext(
                        self.State.CarriedGemCount,
                        macroState.OwnScore,
                        macroState.EnemyScore,
                        macroState.ScoreToWin,
                        gem.Value,
                        clusterValue,
                        distance,
                        searchRadius,
                        self.State.CurrentHealth /
                        Mathf.Max(1f, self.State.MaxHealth.Value),
                        threatPressure,
                        macroState.OwnTeamHasCountdown,
                        macroState.EnemyTeamHasCountdown,
                        macroState.Call,
                        profile.GemPickupMinimumScore,
                        macroState.WinTimerRemainingSeconds));

                if (!found || evaluation.Score > best.Score)
                {
                    found = true;
                    best = new AIGemPickupDecision(
                        true,
                        evaluation.ShouldPickup,
                        gemPosition,
                        gem.Value,
                        clusterValue,
                        distance,
                        evaluation.Score,
                        threatPressure,
                        evaluation.IsSafe,
                        evaluation.IsThresholdPickup,
                        evaluation.IsDenyPickup,
                        evaluation.Reason);
                }
            }

            if (!found)
            {
                decision = AIGemPickupDecision.None("no_gems");
                return false;
            }

            decision = best;
            return best.ShouldPickup;
        }

        public static AIGemPickupEvaluation EvaluateCandidate(
            BrawlerAIProfile profile,
            in AIGemPickupCandidateContext context)
        {
            if (profile == null || context.GemValue <= 0)
            {
                return new AIGemPickupEvaluation(
                    false,
                    0f,
                    false,
                    false,
                    false,
                    "invalid");
            }

            int gemsToWin = context.GemsToWin > 0 ? context.GemsToWin : 10;
            int pickupValue = Mathf.Max(context.GemValue, context.ClusterValue);
            float searchRadius = Mathf.Max(0.1f, context.SearchRadius);
            float closeFactor = Mathf.Clamp01(1f - context.Distance / searchRadius);
            bool isThresholdPickup =
                context.OwnGems < gemsToWin &&
                context.OwnGems + pickupValue >= gemsToWin;
            bool isSwingPickup =
                context.OwnGems <= context.EnemyGems &&
                context.OwnGems + pickupValue > context.EnemyGems;
            bool isDenyPickup =
                (context.EnemyGems < gemsToWin &&
                 context.EnemyGems + pickupValue >= gemsToWin) ||
                context.EnemyCountdown;
            bool enemyNearThreshold = context.EnemyGems >= gemsToWin - 2;
            bool isCarrier = context.SelfCarriedGems > 0;
            bool lowHealth = context.HealthRatio <= profile.LowHealthRetreatRatio + 0.10f;
            bool strategicPickup =
                isThresholdPickup ||
                isSwingPickup ||
                isDenyPickup ||
                context.MacroCall == AIGameModeMacroCall.Reset ||
                context.MacroCall == AIGameModeMacroCall.Push;
            float countdownUrgency = context.EnemyCountdown
                ? CalculateCountdownUrgency(context.WinTimerRemainingSeconds)
                : 0f;

            float score =
                profile.GemPickupBaseScore +
                pickupValue * profile.GemPickupValueScore +
                closeFactor * profile.GemPickupCloseRangeBonus;
            string reason = "base";

            if (pickupValue > context.GemValue)
            {
                score += (pickupValue - context.GemValue) * profile.GemPickupValueScore * 0.65f;
                reason += "|cluster";
            }

            if (context.Distance <= 2f)
            {
                score += 12f;
                reason += "|free";
            }

            if (isThresholdPickup)
            {
                score += profile.GemPickupSecureThresholdBonus;
                reason += "|secure";
            }

            if (isSwingPickup && !isThresholdPickup)
            {
                score += 16f;
                reason += "|swing";
            }

            if (isDenyPickup)
            {
                score += profile.GemPickupDenyThresholdBonus;
                reason += context.EnemyCountdown ? "|reset_countdown" : "|deny";
            }
            else if (enemyNearThreshold && pickupValue >= 2)
            {
                score += 12f;
                reason += "|deny_setup";
            }

            if (context.EnemyCountdown)
            {
                score += profile.GemPickupCountdownResetBonus;

                if (countdownUrgency > 0f)
                {
                    float urgentResetBonus = 18f * countdownUrgency;
                    score += urgentResetBonus;
                    reason += $"|urgent_reset_{urgentResetBonus:0.0}";
                }
            }

            if (context.MacroCall == AIGameModeMacroCall.Reset)
                score += 18f;
            else if (context.MacroCall == AIGameModeMacroCall.Push)
                score += 12f;
            else if (context.MacroCall == AIGameModeMacroCall.Hold && !isThresholdPickup)
                score -= 10f;

            float threatPenaltyScale = strategicPickup ? 0.45f : 1f;
            if (context.EnemyCountdown && countdownUrgency >= 0.50f)
                threatPenaltyScale = Mathf.Min(threatPenaltyScale, 0.30f);

            score -= context.ThreatPressure *
                     profile.GemPickupThreatPenalty *
                     threatPenaltyScale;

            if (isCarrier)
            {
                float carrierRisk =
                    context.SelfCarriedGems *
                    profile.GemPickupCarrierSafetyPenalty *
                    (lowHealth ? 1.35f : 1f);

                if (context.OwnCountdown && !isThresholdPickup)
                    carrierRisk *= 1.35f;

                if (strategicPickup && context.Distance <= 2.5f)
                    carrierRisk *= 0.35f;

                score -= carrierRisk;
                reason += "|carrier_risk";
            }

            if (lowHealth && !strategicPickup)
            {
                score -= 18f;
                reason += "|low_hp";
            }

            bool isSafe =
                context.ThreatPressure < 0.55f &&
                (!isCarrier || context.Distance <= 2.5f || strategicPickup) &&
                (!lowHealth || strategicPickup);
            bool shouldPickup =
                score >= Mathf.Max(0f, context.MinimumScore) &&
                (isSafe || strategicPickup || context.Distance <= 1.5f);

            return new AIGemPickupEvaluation(
                shouldPickup,
                score,
                isSafe,
                isThresholdPickup,
                isDenyPickup,
                reason);
        }

        public static AIGemMineControlEvaluation EvaluateMineControl(
            AIActionType actionType,
            in AIGemMineControlContext context)
        {
            if (context.MacroState.Mode != GameModeId.GemGrab)
                return AIGemMineControlEvaluation.None;

            if (context.HasLiveTarget)
                return AIGemMineControlEvaluation.None;

            float delta = 0f;
            string reason = string.Empty;

            if (context.MacroState.OwnTeamHasCountdown &&
                context.SelfCarriedGems > 0)
            {
                switch (actionType)
                {
                    case AIActionType.Objective:
                        return MineResult(-18f, "carrier_countdown_safety");
                    case AIActionType.Search:
                        return MineResult(-12f, "carrier_countdown_safety");
                    default:
                        return AIGemMineControlEvaluation.None;
                }
            }

            if (context.MacroState.EnemyTeamHasCountdown)
            {
                float urgency = CalculateCountdownUrgency(
                    context.MacroState.WinTimerRemainingSeconds);
                if (actionType == AIActionType.Objective)
                    Add(ref delta, ref reason, 28f + urgency * 10f, "reset_countdown_mine");
                else if (actionType == AIActionType.Search)
                    Add(ref delta, ref reason, 16f + urgency * 8f, "reset_countdown_mine");
            }
            else if (context.MacroState.Call == AIGameModeMacroCall.Reset)
            {
                if (actionType == AIActionType.Objective)
                    Add(ref delta, ref reason, 18f, "retake_mine");
                else if (actionType == AIActionType.Search)
                    Add(ref delta, ref reason, 10f, "retake_mine");
            }
            else if (context.MacroState.Call == AIGameModeMacroCall.Push ||
                     context.MacroState.IsBehind)
            {
                int deficit = Mathf.Max(0, context.MacroState.EnemyGems - context.MacroState.OwnGems);
                float pressure = Mathf.Min(10f, deficit * 3f);
                if (actionType == AIActionType.Objective)
                    Add(ref delta, ref reason, 16f + pressure, "mine_pressure");
                else if (actionType == AIActionType.Search)
                    Add(ref delta, ref reason, 8f + pressure * 0.50f, "mine_pressure");
            }
            else if (context.MacroState.Phase == AIGameModeObjectivePhase.Opening ||
                     context.MacroState.Phase == AIGameModeObjectivePhase.Contest)
            {
                if (actionType == AIActionType.Objective)
                    Add(ref delta, ref reason, 10f, "establish_mine");
                else if (actionType == AIActionType.Search)
                    Add(ref delta, ref reason, 6f, "establish_mine");
            }

            if (context.HasGemPickup &&
                context.ShouldPickupGem &&
                actionType == AIActionType.Search)
            {
                Add(
                    ref delta,
                    ref reason,
                    Mathf.Min(18f, context.GemPickupScore * 0.22f),
                    "pickup_window");
            }

            if (context.AllyPressure >= 1.75f &&
                actionType == AIActionType.Objective)
            {
                Add(ref delta, ref reason, -Mathf.Min(12f, context.AllyPressure * 3f), "spread_from_mine");
            }

            if (delta == 0f)
                return AIGemMineControlEvaluation.None;

            return MineResult(delta, reason);
        }

        private static void Add(ref float delta, ref string reason, float value, string part)
        {
            if (Mathf.Abs(value) <= 0.01f)
                return;

            delta += value;
            reason = string.IsNullOrEmpty(reason)
                ? part
                : $"{reason}|{part}";
        }

        private static AIGemMineControlEvaluation MineResult(float delta, string reason)
        {
            return new AIGemMineControlEvaluation(
                Mathf.Clamp(delta, -MaxMineControlDelta, MaxMineControlDelta),
                reason);
        }

        private static int CountClusterValue(
            Gem source,
            Vector3 sourcePosition,
            float radius)
        {
            float radiusSq = radius * radius;
            int value = source != null ? source.Value : 0;

            for (int i = 0; i < Gem.All.Count; i++)
            {
                Gem gem = Gem.All[i];
                if (gem == null || gem == source || gem.IsPickedUp)
                    continue;

                Vector3 position = gem.transform.position;
                float dx = position.x - sourcePosition.x;
                float dz = position.z - sourcePosition.z;
                if (dx * dx + dz * dz <= radiusSq)
                    value += gem.Value;
            }

            return value;
        }

        private static float CalculateThreatPressure(
            Vector3 gemPosition,
            BrawlerAIProfile profile,
            bool hasThreatCenter,
            Vector3 threatCenter,
            float threatCenterPressure,
            bool hasEnemyHotspot,
            Vector3 enemyHotspot,
            float enemyHotspotPressure)
        {
            float radius = Mathf.Max(0.1f, profile.GemPickupThreatRadius);
            float pressure = 0f;

            if (hasThreatCenter)
            {
                float distance = XZDistance(gemPosition, threatCenter);
                pressure = Mathf.Max(
                    pressure,
                    Mathf.Clamp01(1f - distance / radius) *
                    Mathf.Clamp01(threatCenterPressure / 4f));
            }

            if (hasEnemyHotspot)
            {
                float distance = XZDistance(gemPosition, enemyHotspot);
                pressure = Mathf.Max(
                    pressure,
                    Mathf.Clamp01(1f - distance / radius) *
                    Mathf.Clamp01(enemyHotspotPressure / 4f));
            }

            return pressure;
        }

        private static float XZDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float CalculateCountdownUrgency(float remainingSeconds)
        {
            if (remainingSeconds <= 0f)
                return 0.35f;

            return Mathf.Clamp01((10f - remainingSeconds) / 10f);
        }
    }
}
