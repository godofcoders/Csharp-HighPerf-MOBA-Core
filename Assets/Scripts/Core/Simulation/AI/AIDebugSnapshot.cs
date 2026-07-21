using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIDebugSnapshot
    {
        public string BrawlerName;
        public string CurrentAction;
        public string TacticalIntentSummary;
        public string Difficulty;
        public string Personality;
        public uint ReactionDelayTicks;
        public float AimErrorDegrees;
        public string CurrentTargetName;
        public int CurrentTargetId;
        public int CurrentTargetFocusCount;
        public int CurrentTargetAllyFocusCount;
        public float CurrentTargetOverFocusPenalty;

        public float Health;
        public float MaxHealth;

        public bool IsStunned;
        public bool IsBurning;
        public bool IsSlowed;
        public bool IsRevealed;

        public Vector3 Position;
        public Vector3? TargetPosition;

        public string TeamTactic;
        public string TeamSignalDebug;
        public string TeamRoleDebug;
        public string MacroDebug;
        public string PlaybookDebug;
        public string ChaseDebug;
        public string GemPickupDebug;
        public string ReactiveDebug;
        public string DangerDebug;
        public string FailureRecoveryDebug;
        public string HumanizationDebug;
        public string TuningDebug;
        public string OpponentModelDebug;
        public string ObjectiveName;

        public readonly List<AIActionScore> ActionScores = new List<AIActionScore>(16);
        public readonly List<string> ActiveStatuses = new List<string>(8);
        public string ObjectiveDebug;
        public string TacticalMovementDebug;
        public string NavigationDebug;
        public string IncidentDebug;
        public string PerformanceDebug;
        public string ProductionBudgetDebug;
        public string ValidationDebug;
        public string ValidationHealthDebug;
        public string ValidationScenarioDebug;
        public string ValidationGauntletDebug;
        public string ReportCardDebug;
        public string MatchTelemetryReviewDebug;
        public string MatchTelemetryTrendDebug;
        public string BotTelemetryOutlierDebug;
        public string AIReadinessDebug;

        public void ClearLists()
        {
            ActionScores.Clear();
            ActiveStatuses.Clear();
        }
    }
}
