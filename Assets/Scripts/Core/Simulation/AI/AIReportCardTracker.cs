using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine.SceneManagement;

namespace MOBA.Core.Simulation.AI
{
    public static class AIReportCardTracker
    {
        private const float LowConfidenceMargin = 5f;
        private const float MeaningfulScore = 0.01f;
        private const uint PendingSuperImpactWindowTicks = 90u;

        private sealed class Record
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

            public int Kills;
            public int Deaths;
            public float DamageDealt;
            public float DamageTaken;
            public float HealingDone;

            public uint FirstTick;
            public uint LastTick;
            public float TopScoreSum;
            public float ScoreMarginSum;

            public bool HasPendingSuperImpact;
            public uint PendingSuperImpactTick;

            public AIReportCardSnapshot ToSnapshot()
            {
                return new AIReportCardSnapshot
                {
                    EntityId = EntityId,
                    Name = Name,
                    Team = Team,
                    RegisteredBotCount = RegisteredBotCount,
                    IsTeamSnapshot = IsTeamSnapshot,
                    DecisionCount = DecisionCount,
                    TargetedDecisionCount = TargetedDecisionCount,
                    TargetlessDecisionCount = TargetlessDecisionCount,
                    ActionSwitchCount = ActionSwitchCount,
                    InvalidDecisionCount = InvalidDecisionCount,
                    LowConfidenceDecisionCount = LowConfidenceDecisionCount,
                    ZeroScoreDecisionCount = ZeroScoreDecisionCount,
                    EmergencyActionCount = EmergencyActionCount,
                    TeamRoleAdjustedDecisionCount = TeamRoleAdjustedDecisionCount,
                    ObjectiveDecisionCount = ObjectiveDecisionCount,
                    PeelDecisionCount = PeelDecisionCount,
                    RegroupDecisionCount = RegroupDecisionCount,
                    ObjectivePickupCount = ObjectivePickupCount,
                    ObjectiveValue = ObjectiveValue,
                    AbilityCastCount = AbilityCastCount,
                    MainAttackCastCount = MainAttackCastCount,
                    GadgetCastCount = GadgetCastCount,
                    SuperCastCount = SuperCastCount,
                    FailedCastCount = FailedCastCount,
                    BadCastCount = BadCastCount,
                    WastedSuperCount = WastedSuperCount,
                    SuperImpactCount = SuperImpactCount,
                    FailureRecoveryCount = FailureRecoveryCount,
                    NavigationStallRecoveryCount = NavigationStallRecoveryCount,
                    BlockedRouteRecoveryCount = BlockedRouteRecoveryCount,
                    StaleDestinationRecoveryCount = StaleDestinationRecoveryCount,
                    FailedCastRecoveryCount = FailedCastRecoveryCount,
                    Kills = Kills,
                    Deaths = Deaths,
                    DamageDealt = DamageDealt,
                    DamageTaken = DamageTaken,
                    HealingDone = HealingDone,
                    FirstTick = FirstTick,
                    LastTick = LastTick,
                    AverageTopScore = DecisionCount > 0 ? TopScoreSum / DecisionCount : 0f,
                    AverageScoreMargin = DecisionCount > 0 ? ScoreMarginSum / DecisionCount : 0f
                };
            }
        }

        private struct BotDecisionRecord
        {
            public AIActionType ActionType;
        }

        private static readonly Dictionary<int, Record> _botRecords =
            new Dictionary<int, Record>(32);
        private static readonly Dictionary<TeamType, Record> _teamRecords =
            new Dictionary<TeamType, Record>(4);
        private static readonly Dictionary<int, BotDecisionRecord> _lastDecisionByBot =
            new Dictionary<int, BotDecisionRecord>(32);

        private static bool _subscribed;
        private static int _sceneHandle = -1;

        public static void RegisterBot(
            int botEntityId,
            TeamType team,
            string name)
        {
            if (botEntityId == 0)
                return;

            EnsureSceneScope();
            EnsureSubscribed();

            bool isNew = !_botRecords.TryGetValue(botEntityId, out Record record);
            if (isNew)
            {
                record = new Record
                {
                    EntityId = botEntityId,
                    Team = team,
                    Name = string.IsNullOrEmpty(name) ? $"Bot {botEntityId}" : name
                };

                _botRecords[botEntityId] = record;
                GetTeamRecord(team).RegisteredBotCount++;
            }
            else if (record.Team != team)
            {
                GetTeamRecord(record.Team).RegisteredBotCount--;
                GetTeamRecord(team).RegisteredBotCount++;
                record.Team = team;
            }

            if (!string.IsNullOrEmpty(name))
                record.Name = name;
        }

        public static void RecordDecision(
            int botEntityId,
            uint currentTick,
            AIActionScore chosenAction,
            bool hasLiveTarget,
            IReadOnlyList<AIActionScore> actionScores,
            string teamRoleDebug)
        {
            if (!_botRecords.TryGetValue(botEntityId, out Record record))
                return;

            CalculateScoreConfidence(
                actionScores,
                out float topScore,
                out float scoreMargin);

            bool zeroScore = chosenAction.Score <= MeaningfulScore;
            bool emergency = IsEmergencyAction(chosenAction.ActionType);
            bool invalid = HasInvalidContext(
                chosenAction.ActionType,
                chosenAction.Score,
                hasLiveTarget);
            bool teamRoleAdjusted = HasTeamRoleAdjustment(teamRoleDebug);
            bool lowConfidence = topScore > MeaningfulScore &&
                                 scoreMargin < LowConfidenceMargin;
            bool switched =
                _lastDecisionByBot.TryGetValue(botEntityId, out BotDecisionRecord previous) &&
                previous.ActionType != chosenAction.ActionType;

            ApplyDecision(
                record,
                chosenAction.ActionType,
                currentTick,
                hasLiveTarget,
                topScore,
                scoreMargin,
                zeroScore,
                emergency,
                invalid,
                teamRoleAdjusted,
                lowConfidence,
                switched);

            ApplyDecision(
                GetTeamRecord(record.Team),
                chosenAction.ActionType,
                currentTick,
                hasLiveTarget,
                topScore,
                scoreMargin,
                zeroScore,
                emergency,
                invalid,
                teamRoleAdjusted,
                lowConfidence,
                switched);

            _lastDecisionByBot[botEntityId] = new BotDecisionRecord
            {
                ActionType = chosenAction.ActionType
            };
        }

        public static void RecordAbilityResult(
            int botEntityId,
            AbilitySlotType slotType,
            AbilityExecutionResult result,
            uint currentTick)
        {
            if (!_botRecords.TryGetValue(botEntityId, out Record record))
                return;

            bool success = result.Success;
            bool noEffectAreaCast = success &&
                                    result.AppliedAreaEffect &&
                                    result.TargetsAffected <= 0;
            bool badCast = !success || noEffectAreaCast;
            bool wastedSuper = slotType == AbilitySlotType.Super && badCast;

            ApplyAbilityResult(
                record,
                slotType,
                success,
                badCast,
                wastedSuper,
                currentTick);

            ApplyAbilityResult(
                GetTeamRecord(record.Team),
                slotType,
                success,
                badCast,
                wastedSuper,
                currentTick);
        }

        public static void RecordFailureRecovery(
            int botEntityId,
            AIFailureRecoveryReason reason,
            uint currentTick)
        {
            if (!_botRecords.TryGetValue(botEntityId, out Record record))
                return;

            ApplyFailureRecovery(record, reason, currentTick);
            ApplyFailureRecovery(GetTeamRecord(record.Team), reason, currentTick);
        }

        public static void RecordObjectiveValue(
            int botEntityId,
            int amount,
            uint currentTick)
        {
            if (amount <= 0 ||
                !_botRecords.TryGetValue(botEntityId, out Record record))
            {
                return;
            }

            ApplyObjectiveValue(record, amount, currentTick);
            ApplyObjectiveValue(GetTeamRecord(record.Team), amount, currentTick);
        }

        public static void RecordHealingDone(
            BrawlerController source,
            BrawlerController target,
            float healingDone,
            bool isSuper,
            uint currentTick)
        {
            if (source == null || target == null || healingDone <= 0f)
                return;

            RecordHealingDone(
                source.EntityID,
                healingDone,
                isSuper,
                currentTick);
        }

        public static void RecordHealingDone(
            int sourceEntityId,
            float healingDone,
            bool isSuper,
            uint currentTick)
        {
            if (healingDone <= 0f ||
                !_botRecords.TryGetValue(sourceEntityId, out Record record))
            {
                return;
            }

            bool resolvedSuperImpact = ApplyHealing(
                record,
                healingDone,
                isSuper,
                currentTick);
            Record team = GetTeamRecord(record.Team);
            ApplyHealing(team, healingDone, false, currentTick);
            if (resolvedSuperImpact)
                team.SuperImpactCount++;
        }

        public static void RecordCombatResult(
            int attackerEntityId,
            int victimEntityId,
            float finalDamageApplied,
            bool wasFatal,
            bool isSuper,
            uint currentTick)
        {
            float damage = ClampMetric(finalDamageApplied);

            if (attackerEntityId != 0 &&
                _botRecords.TryGetValue(attackerEntityId, out Record attackerRecord))
            {
                if (damage > 0f)
                {
                    bool resolvedSuperImpact = ApplyDamageDealt(
                        attackerRecord,
                        damage,
                        isSuper,
                        currentTick);
                    Record team = GetTeamRecord(attackerRecord.Team);
                    ApplyDamageDealt(
                        team,
                        damage,
                        false,
                        currentTick);
                    if (resolvedSuperImpact)
                        team.SuperImpactCount++;
                }

                if (wasFatal && attackerEntityId != victimEntityId)
                {
                    ApplyKill(attackerRecord, currentTick);
                    ApplyKill(GetTeamRecord(attackerRecord.Team), currentTick);
                }
            }

            if (victimEntityId != 0 &&
                _botRecords.TryGetValue(victimEntityId, out Record victimRecord))
            {
                if (damage > 0f)
                {
                    ApplyDamageTaken(victimRecord, damage, currentTick);
                    ApplyDamageTaken(GetTeamRecord(victimRecord.Team), damage, currentTick);
                }

                if (wasFatal)
                {
                    ApplyDeath(victimRecord, currentTick);
                    ApplyDeath(GetTeamRecord(victimRecord.Team), currentTick);
                }
            }
        }

        public static AIReportCardSnapshot GetBotSnapshot(
            int botEntityId,
            uint currentTick = 0u)
        {
            if (!_botRecords.TryGetValue(botEntityId, out Record record))
            {
                return new AIReportCardSnapshot
                {
                    EntityId = botEntityId,
                    Name = $"Bot {botEntityId}"
                };
            }

            AgePendingSuper(record, currentTick);
            return record.ToSnapshot();
        }

        public static AIReportCardSnapshot GetTeamSnapshot(
            TeamType team,
            uint currentTick = 0u)
        {
            foreach (var pair in _botRecords)
                AgePendingSuper(pair.Value, currentTick);

            return GetTeamRecord(team).ToSnapshot();
        }

        public static string GetBotDebugSummary(
            int botEntityId,
            uint currentTick)
        {
            return GetBotSnapshot(botEntityId, currentTick).GetDebugSummary();
        }

        public static string GetTeamDebugSummary(
            TeamType team,
            uint currentTick)
        {
            return GetTeamSnapshot(team, currentTick).GetDebugSummary();
        }

        public static void ResetForTests()
        {
            if (_subscribed)
            {
                AbilityEventBus.OnAbilityEvent -= OnAbilityEvent;
                DamageEventBus.OnDamageApplied -= OnDamageApplied;
                GemEventBus.OnGemPickedUp -= OnGemPickedUp;
                _subscribed = false;
            }

            _botRecords.Clear();
            _teamRecords.Clear();
            _lastDecisionByBot.Clear();
            _sceneHandle = -1;
        }

        private static void EnsureSubscribed()
        {
            if (_subscribed)
                return;

            AbilityEventBus.OnAbilityEvent += OnAbilityEvent;
            DamageEventBus.OnDamageApplied += OnDamageApplied;
            GemEventBus.OnGemPickedUp += OnGemPickedUp;
            _subscribed = true;
        }

        private static void EnsureSceneScope()
        {
            int activeSceneHandle = SceneManager.GetActiveScene().handle;
            if (_sceneHandle < 0)
            {
                _sceneHandle = activeSceneHandle;
                return;
            }

            if (_sceneHandle == activeSceneHandle)
                return;

            _botRecords.Clear();
            _teamRecords.Clear();
            _lastDecisionByBot.Clear();
            _sceneHandle = activeSceneHandle;
        }

        private static void OnAbilityEvent(AbilityExecutionEvent evt)
        {
            if (evt.Source == null ||
                (evt.EventType != AbilityEventType.CastSucceeded &&
                 evt.EventType != AbilityEventType.CastFailed))
            {
                return;
            }

            RecordAbilityResult(
                evt.Source.EntityID,
                evt.SlotType,
                evt.Result,
                evt.Tick);
        }

        private static void OnDamageApplied(DamageResultContext result)
        {
            uint currentTick = GetCurrentTickOrZero();
            BrawlerController attacker = result.Damage.Attacker;
            BrawlerController victim = result.Damage.Target as BrawlerController;

            RecordCombatResult(
                attacker != null ? attacker.EntityID : 0,
                victim != null ? victim.EntityID : 0,
                result.FinalDamageApplied,
                result.WasFatal,
                result.Damage.IsSuper,
                currentTick);
        }

        private static void OnGemPickedUp(BrawlerState carrier, int amount)
        {
            if (carrier == null || carrier.EntityID == 0)
                return;

            RecordObjectiveValue(
                carrier.EntityID,
                amount,
                GetCurrentTickOrZero());
        }

        private static Record GetTeamRecord(TeamType team)
        {
            if (_teamRecords.TryGetValue(team, out Record record))
                return record;

            record = new Record
            {
                Team = team,
                Name = $"Team {team}",
                IsTeamSnapshot = true
            };

            _teamRecords[team] = record;
            return record;
        }

        private static void ApplyDecision(
            Record record,
            AIActionType actionType,
            uint currentTick,
            bool hasLiveTarget,
            float topScore,
            float scoreMargin,
            bool zeroScore,
            bool emergency,
            bool invalid,
            bool teamRoleAdjusted,
            bool lowConfidence,
            bool switched)
        {
            Touch(record, currentTick);
            record.DecisionCount++;
            record.TopScoreSum += topScore;
            record.ScoreMarginSum += scoreMargin;

            if (hasLiveTarget)
                record.TargetedDecisionCount++;
            else
                record.TargetlessDecisionCount++;

            if (zeroScore)
                record.ZeroScoreDecisionCount++;

            if (emergency)
                record.EmergencyActionCount++;

            if (invalid)
                record.InvalidDecisionCount++;

            if (teamRoleAdjusted)
                record.TeamRoleAdjustedDecisionCount++;

            if (lowConfidence)
                record.LowConfidenceDecisionCount++;

            if (switched)
                record.ActionSwitchCount++;

            switch (actionType)
            {
                case AIActionType.Objective:
                    record.ObjectiveDecisionCount++;
                    break;
                case AIActionType.Peel:
                    record.PeelDecisionCount++;
                    break;
                case AIActionType.Regroup:
                    record.RegroupDecisionCount++;
                    break;
            }
        }

        private static void ApplyAbilityResult(
            Record record,
            AbilitySlotType slotType,
            bool success,
            bool badCast,
            bool wastedSuper,
            uint currentTick)
        {
            AgePendingSuper(record, currentTick);
            Touch(record, currentTick);
            record.AbilityCastCount++;

            switch (slotType)
            {
                case AbilitySlotType.MainAttack:
                    record.MainAttackCastCount++;
                    break;

                case AbilitySlotType.Gadget:
                    record.GadgetCastCount++;
                    break;

                case AbilitySlotType.Super:
                    record.SuperCastCount++;
                    if (!record.IsTeamSnapshot && success && !wastedSuper)
                    {
                        record.HasPendingSuperImpact = true;
                        record.PendingSuperImpactTick = currentTick;
                    }
                    break;
            }

            if (!success)
                record.FailedCastCount++;

            if (badCast)
                record.BadCastCount++;

            if (wastedSuper)
            {
                record.WastedSuperCount++;
                record.HasPendingSuperImpact = false;
            }
        }

        private static void ApplyFailureRecovery(
            Record record,
            AIFailureRecoveryReason reason,
            uint currentTick)
        {
            Touch(record, currentTick);
            record.FailureRecoveryCount++;

            switch (reason)
            {
                case AIFailureRecoveryReason.NavigationStall:
                    record.NavigationStallRecoveryCount++;
                    break;

                case AIFailureRecoveryReason.BlockedRoute:
                    record.BlockedRouteRecoveryCount++;
                    break;

                case AIFailureRecoveryReason.StaleDestination:
                    record.StaleDestinationRecoveryCount++;
                    break;

                case AIFailureRecoveryReason.FailedCast:
                    record.FailedCastRecoveryCount++;
                    break;
            }
        }

        private static void ApplyObjectiveValue(
            Record record,
            int amount,
            uint currentTick)
        {
            Touch(record, currentTick);
            record.ObjectivePickupCount++;
            record.ObjectiveValue += amount;
        }

        private static bool ApplyDamageDealt(
            Record record,
            float damage,
            bool isSuper,
            uint currentTick)
        {
            Touch(record, currentTick);
            record.DamageDealt += damage;
            return ResolvePendingSuperImpact(record, isSuper, currentTick);
        }

        private static void ApplyDamageTaken(
            Record record,
            float damage,
            uint currentTick)
        {
            Touch(record, currentTick);
            record.DamageTaken += damage;
        }

        private static bool ApplyHealing(
            Record record,
            float healing,
            bool isSuper,
            uint currentTick)
        {
            Touch(record, currentTick);
            record.HealingDone += healing;
            return ResolvePendingSuperImpact(record, isSuper, currentTick);
        }

        private static void ApplyKill(Record record, uint currentTick)
        {
            Touch(record, currentTick);
            record.Kills++;
        }

        private static void ApplyDeath(Record record, uint currentTick)
        {
            Touch(record, currentTick);
            record.Deaths++;
        }

        private static bool ResolvePendingSuperImpact(
            Record record,
            bool isSuper,
            uint currentTick)
        {
            if (!isSuper || !record.HasPendingSuperImpact)
                return false;

            Touch(record, currentTick);
            record.SuperImpactCount++;
            record.HasPendingSuperImpact = false;
            return true;
        }

        private static void AgePendingSuper(
            Record record,
            uint currentTick)
        {
            if (record == null ||
                record.IsTeamSnapshot ||
                !record.HasPendingSuperImpact ||
                currentTick == 0u ||
                currentTick - record.PendingSuperImpactTick <= PendingSuperImpactWindowTicks)
            {
                return;
            }

            record.WastedSuperCount++;
            record.HasPendingSuperImpact = false;

            Record team = GetTeamRecord(record.Team);
            team.WastedSuperCount++;
        }

        private static void Touch(Record record, uint currentTick)
        {
            if (record.FirstTick == 0u)
                record.FirstTick = currentTick;

            if (currentTick >= record.LastTick)
                record.LastTick = currentTick;
        }

        private static void CalculateScoreConfidence(
            IReadOnlyList<AIActionScore> actionScores,
            out float topScore,
            out float scoreMargin)
        {
            topScore = 0f;
            float secondScore = 0f;

            if (actionScores == null)
            {
                scoreMargin = 0f;
                return;
            }

            for (int i = 0; i < actionScores.Count; i++)
            {
                float score = actionScores[i].Score;
                if (score > topScore)
                {
                    secondScore = topScore;
                    topScore = score;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                }
            }

            scoreMargin = topScore - secondScore;
        }

        private static bool HasInvalidContext(
            AIActionType actionType,
            float score,
            bool hasLiveTarget)
        {
            if (score <= MeaningfulScore)
                return false;

            switch (actionType)
            {
                case AIActionType.Approach:
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                case AIActionType.Retreat:
                case AIActionType.UseSuper:
                    return !hasLiveTarget;

                case AIActionType.Search:
                case AIActionType.Regroup:
                case AIActionType.Objective:
                    return hasLiveTarget;

                default:
                    return false;
            }
        }

        private static bool HasTeamRoleAdjustment(string teamRoleDebug)
        {
            return !string.IsNullOrEmpty(teamRoleDebug) &&
                   teamRoleDebug.Contains("Delta=");
        }

        private static bool IsEmergencyAction(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Retreat:
                case AIActionType.Evade:
                case AIActionType.UseSuper:
                case AIActionType.Peel:
                    return true;

                default:
                    return false;
            }
        }

        private static float ClampMetric(float value)
        {
            return value > 0f ? value : 0f;
        }

        public static uint GetCurrentTickOrZero()
        {
            try
            {
                ISimulationClock clock = ServiceProvider.Get<ISimulationClock>();
                return clock != null ? clock.CurrentTick : 0u;
            }
            catch
            {
                return 0u;
            }
        }
    }
}
