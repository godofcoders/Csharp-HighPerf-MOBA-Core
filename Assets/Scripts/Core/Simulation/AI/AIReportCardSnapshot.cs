namespace MOBA.Core.Simulation.AI
{
    public struct AIReportCardSnapshot
    {
        public int EntityId;
        public string Name;
        public TeamType Team;
        public int RegisteredBotCount;
        public bool IsTeamSnapshot;

        public int DecisionCount;
        public int TargetedDecisionCount;
        public int TargetlessDecisionCount;
        public int ActionSwitchCount;
        public int InvalidDecisionCount;
        public int LowConfidenceDecisionCount;
        public int ZeroScoreDecisionCount;
        public int EmergencyActionCount;
        public int TeamRoleAdjustedDecisionCount;

        public int ObjectiveDecisionCount;
        public int PeelDecisionCount;
        public int RegroupDecisionCount;
        public int ObjectivePickupCount;
        public int ObjectiveValue;

        public int AbilityCastCount;
        public int MainAttackCastCount;
        public int GadgetCastCount;
        public int SuperCastCount;
        public int FailedCastCount;
        public int BadCastCount;
        public int WastedSuperCount;
        public int SuperImpactCount;

        public int FailureRecoveryCount;
        public int NavigationStallRecoveryCount;
        public int BlockedRouteRecoveryCount;
        public int StaleDestinationRecoveryCount;
        public int FailedCastRecoveryCount;
        public int IdleHesitationRecoveryCount;

        public int Kills;
        public int Deaths;
        public float DamageDealt;
        public float DamageTaken;
        public float HealingDone;

        public uint FirstTick;
        public uint LastTick;
        public float AverageTopScore;
        public float AverageScoreMargin;

        public float TargetedDecisionRatio =>
            DecisionCount > 0 ? (float)TargetedDecisionCount / DecisionCount : 0f;

        public float BadCastRatio =>
            AbilityCastCount > 0 ? (float)BadCastCount / AbilityCastCount : 0f;

        public float WastedSuperRatio =>
            SuperCastCount > 0 ? (float)WastedSuperCount / SuperCastCount : 0f;

        public float CombatUsefulness =>
            DamageDealt + HealingDone - DamageTaken;

        public string GetDebugSummary()
        {
            string label = IsTeamSnapshot
                ? $"Team={Team}"
                : $"Bot={EntityId}";

            return
                $"Report {label} " +
                $"dec={DecisionCount} " +
                $"target={TargetedDecisionCount}/{TargetlessDecisionCount} " +
                $"obj={ObjectiveDecisionCount}+{ObjectiveValue} " +
                $"peel={PeelDecisionCount} " +
                $"regroup={RegroupDecisionCount} " +
                $"casts={AbilityCastCount} " +
                $"bad={BadCastCount} " +
                $"super={SuperCastCount}/{WastedSuperCount} " +
                $"dmg={DamageDealt:0}/{DamageTaken:0} " +
                $"heal={HealingDone:0} " +
                $"K/D={Kills}/{Deaths} " +
                $"rec={FailureRecoveryCount} " +
                $"idle={IdleHesitationRecoveryCount}";
        }
    }
}
