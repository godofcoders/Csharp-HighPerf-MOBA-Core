using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class ShowdownRulesTests
    {
        [TestCase(DuoTeamCommandType.Attack, AIActionType.Evade, 1f, false)]
        [TestCase(DuoTeamCommandType.Attack, AIActionType.Retreat, 1f, false)]
        [TestCase(DuoTeamCommandType.Attack, AIActionType.Approach, 0.34f, false)]
        [TestCase(DuoTeamCommandType.Attack, AIActionType.HoldRange, 0.8f, true)]
        [TestCase(DuoTeamCommandType.TakeCube, AIActionType.Objective, 0.29f, false)]
        [TestCase(DuoTeamCommandType.TakeCube, AIActionType.Objective, 0.8f, true)]
        [TestCase(DuoTeamCommandType.Regroup, AIActionType.Retreat, 0.1f, false)]
        [TestCase(DuoTeamCommandType.Regroup, AIActionType.Regroup, 0.8f, true)]
        [TestCase(DuoTeamCommandType.Retreat, AIActionType.Reposition, 0.8f, true)]
        public void DuoExecution_PreservesSelectedSafetyActions(
            DuoTeamCommandType command, AIActionType action, float health, bool expected)
        {
            Assert.AreEqual(expected, DuoTeamCommandAIUtility.CanExecute(command, action, health));
        }

        [Test]
        public void DuoRoster_AssignsTwoPlayersToEachOfFiveTeams()
        {
            for (int teamIndex = 0; teamIndex < ShowdownRules.DuoTeamCount; teamIndex++)
            {
                TeamType expected = TeamRelationshipUtility.GetSoloTeam(teamIndex);
                Assert.AreEqual(expected, ShowdownRules.GetDuoTeamForRosterIndex(teamIndex * 2));
                Assert.AreEqual(expected, ShowdownRules.GetDuoTeamForRosterIndex(teamIndex * 2 + 1));
            }
        }

        [Test]
        public void DuoSpawnOrdinals_KeepTeammatesAdjacent()
        {
            Assert.AreEqual(0, ShowdownRules.GetDuoSpawnOrdinal(TeamType.Solo1, 0));
            Assert.AreEqual(1, ShowdownRules.GetDuoSpawnOrdinal(TeamType.Solo1, 1));
            Assert.AreEqual(8, ShowdownRules.GetDuoSpawnOrdinal(TeamType.Solo5, 0));
            Assert.AreEqual(9, ShowdownRules.GetDuoSpawnOrdinal(TeamType.Solo5, 1));
        }

        [Test]
        public void DuoSpawnPositions_KeepTeammatesTogetherWithoutOverlap()
        {
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(44f, 0f, 44f));
            Vector3[] positions = new Vector3[ShowdownRules.DuoContestantCount];

            for (int teamIndex = 0; teamIndex < ShowdownRules.DuoTeamCount; teamIndex++)
            {
                int firstIndex = teamIndex * ShowdownRules.DuoPlayersPerTeam;
                positions[firstIndex] = ShowdownRules.ResolveDuoSpawnPosition(
                    bounds,
                    teamIndex,
                    0,
                    3f,
                    ShowdownRules.DuoTeammateSpawnSeparation);
                positions[firstIndex + 1] = ShowdownRules.ResolveDuoSpawnPosition(
                    bounds,
                    teamIndex,
                    1,
                    3f,
                    ShowdownRules.DuoTeammateSpawnSeparation);

                float teammateDistance = Vector3.Distance(
                    positions[firstIndex],
                    positions[firstIndex + 1]);
                Assert.That(teammateDistance, Is.GreaterThanOrEqualTo(2.2f));
                Assert.That(teammateDistance, Is.LessThanOrEqualTo(3f));
            }

            for (int first = 0; first < positions.Length; first++)
            {
                for (int second = first + 1; second < positions.Length; second++)
                {
                    Assert.That(
                        Vector3.Distance(positions[first], positions[second]),
                        Is.GreaterThanOrEqualTo(2.2f));
                }
            }
        }

        [Test]
        public void DuoRules_UseFiveTeamsAndFifteenSecondRespawn()
        {
            Assert.AreEqual(10, ShowdownRules.DuoContestantCount);
            Assert.AreEqual(15f, ShowdownRules.DuoRespawnDelaySeconds);
            Assert.AreEqual(2f, ShowdownRules.DuoSpawnImmunitySeconds);
            Assert.IsTrue(ShowdownRules.IsDuoTeam(TeamType.Solo5));
            Assert.IsFalse(ShowdownRules.IsDuoTeam(TeamType.Solo6));
            Assert.AreEqual("Solo Showdown", ShowdownRules.GetDisplayName(ShowdownVariant.Solo));
            Assert.AreEqual("Duo Showdown", ShowdownRules.GetDisplayName(ShowdownVariant.Duo));
        }

        [Test]
        public void DuoRespawnCountdown_UsesAuthoritativeReadyTimeAndClampsAtZero()
        {
            Assert.AreEqual(15f, ShowdownRules.GetRespawnSecondsRemaining(25f, 10f));
            Assert.AreEqual(4.5f, ShowdownRules.GetRespawnSecondsRemaining(25f, 20.5f));
            Assert.AreEqual(0f, ShowdownRules.GetRespawnSecondsRemaining(25f, 30f));
        }

        [Test]
        public void DuoCohesion_BotFollowsPlayerAndAiPairChoosesOneFollower()
        {
            Assert.IsTrue(ShowdownRules.ShouldUseTeammateAsCohesionAnchor(
                selfIsPlayer: false,
                teammateIsPlayer: true,
                selfEntityId: 10,
                teammateEntityId: 20));
            Assert.IsFalse(ShowdownRules.ShouldUseTeammateAsCohesionAnchor(
                selfIsPlayer: true,
                teammateIsPlayer: false,
                selfEntityId: 10,
                teammateEntityId: 20));
            Assert.IsTrue(ShowdownRules.ShouldUseTeammateAsCohesionAnchor(
                selfIsPlayer: false,
                teammateIsPlayer: false,
                selfEntityId: 20,
                teammateEntityId: 10));
            Assert.IsFalse(ShowdownRules.ShouldUseTeammateAsCohesionAnchor(
                selfIsPlayer: false,
                teammateIsPlayer: false,
                selfEntityId: 10,
                teammateEntityId: 20));
        }

        [Test]
        public void DuoCohesion_AllowsIndependentMovementInsideComfortRadius()
        {
            float closeIdle = ShowdownRules.CalculateDuoCohesionScore(2.5f, false);
            float closeCombat = ShowdownRules.CalculateDuoCohesionScore(2.5f, true);
            float roamingIdle = ShowdownRules.CalculateDuoCohesionScore(5f, false);
            float roamingCombat = ShowdownRules.CalculateDuoCohesionScore(5f, true);
            float separatedCombat = ShowdownRules.CalculateDuoCohesionScore(7f, true);
            float urgentCombat = ShowdownRules.CalculateDuoCohesionScore(12f, true);

            Assert.AreEqual(0f, closeIdle);
            Assert.AreEqual(0f, closeCombat);
            Assert.That(roamingIdle, Is.GreaterThan(0f));
            Assert.That(roamingIdle, Is.LessThan(ShowdownRules.DuoCohesionPriorityScore));
            Assert.That(roamingCombat, Is.GreaterThan(0f));
            Assert.That(roamingCombat, Is.LessThan(ShowdownRules.DuoCohesionPriorityScore));
            Assert.That(separatedCombat, Is.GreaterThanOrEqualTo(ShowdownRules.DuoCohesionPriorityScore));
            Assert.AreEqual(120f, urgentCombat);
        }

        [Test]
        public void DuoCohesion_OnlyForcesRegroupAtHardLeash()
        {
            Assert.IsFalse(ShowdownRules.ShouldForceDuoRegroup(
                ShowdownRules.DuoCohesionHardLeashDistance - 0.01f));
            Assert.IsTrue(ShowdownRules.ShouldForceDuoRegroup(
                ShowdownRules.DuoCohesionHardLeashDistance));
            Assert.IsTrue(ShowdownRules.ShouldForceDuoRegroup(30f));
        }

        [Test]
        public void DuoCohesion_ClampsRoamingDestinationAroundTeammate()
        {
            Vector3 teammate = new Vector3(2f, 0f, -3f);
            Vector3 nearby = new Vector3(5f, 1f, -1f);
            Vector3 distant = new Vector3(30f, 2f, -3f);

            Assert.AreEqual(
                nearby,
                ShowdownRules.ClampDuoDestination(teammate, nearby));

            Vector3 clamped = ShowdownRules.ClampDuoDestination(
                teammate,
                distant);
            Assert.AreEqual(distant.y, clamped.y);
            Assert.AreEqual(
                ShowdownRules.DuoCohesionHardLeashDistance,
                Vector2.Distance(
                    new Vector2(teammate.x, teammate.z),
                    new Vector2(clamped.x, clamped.z)),
                0.001f);
        }

        [Test]
        public void DuoCommandStore_AcceptsThenExpiresCommand()
        {
            var store = new DuoTeamCommandStore();
            uint issuedTick = 100u;

            DuoTeamCommandIssueResult result = store.TryIssue(
                TeamType.Solo1,
                10,
                DuoTeamCommandType.Regroup,
                0,
                new Vector3(3f, 0f, 4f),
                issuedTick,
                issuerAvailable: true,
                teammateAvailable: true,
                targetValid: true,
                out DuoTeamCommandDirective issued);

            Assert.AreEqual(DuoTeamCommandIssueResult.Accepted, result);
            Assert.AreEqual(DuoTeamCommandType.Regroup, issued.CommandType);
            Assert.IsTrue(store.TryGetActive(
                TeamType.Solo1,
                issued.ExpiresTick,
                out _));
            Assert.IsFalse(store.TryGetActive(
                TeamType.Solo1,
                issued.ExpiresTick + 1u,
                out _));
        }

        [Test]
        public void DuoCommandStore_EnforcesCooldownThenReplacesCommand()
        {
            var store = new DuoTeamCommandStore();
            Assert.AreEqual(
                DuoTeamCommandIssueResult.Accepted,
                store.TryIssue(
                    TeamType.Solo2,
                    20,
                    DuoTeamCommandType.Regroup,
                    0,
                    Vector3.zero,
                    50u,
                    true,
                    true,
                    true,
                    out _));

            Assert.AreEqual(
                DuoTeamCommandIssueResult.Cooldown,
                store.TryIssue(
                    TeamType.Solo2,
                    20,
                    DuoTeamCommandType.Retreat,
                    0,
                    Vector3.zero,
                    50u + ShowdownRules.DuoTeamCommandCooldownTicks - 1u,
                    true,
                    true,
                    true,
                    out _));

            uint replacementTick = 50u + ShowdownRules.DuoTeamCommandCooldownTicks;
            Assert.AreEqual(
                DuoTeamCommandIssueResult.Accepted,
                store.TryIssue(
                    TeamType.Solo2,
                    20,
                    DuoTeamCommandType.Retreat,
                    0,
                    new Vector3(1f, 0f, 1f),
                    replacementTick,
                    true,
                    true,
                    true,
                    out DuoTeamCommandDirective replacement));
            Assert.AreEqual(DuoTeamCommandType.Retreat, replacement.CommandType);
        }

        [TestCase(false, true, true, DuoTeamCommandIssueResult.IssuerUnavailable)]
        [TestCase(true, false, true, DuoTeamCommandIssueResult.TeammateUnavailable)]
        [TestCase(true, true, false, DuoTeamCommandIssueResult.InvalidTarget)]
        public void DuoCommandStore_RejectsUnavailableParticipantsAndTargets(
            bool issuerAvailable,
            bool teammateAvailable,
            bool targetValid,
            DuoTeamCommandIssueResult expected)
        {
            var store = new DuoTeamCommandStore();
            DuoTeamCommandIssueResult result = store.TryIssue(
                TeamType.Solo3,
                30,
                DuoTeamCommandType.Attack,
                99,
                Vector3.zero,
                10u,
                issuerAvailable,
                teammateAvailable,
                targetValid,
                out _);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void DuoCommandAI_RegroupAndRetreatPrioritizeSafeActions()
        {
            Assert.AreEqual(
                112f,
                DuoTeamCommandAIUtility.GetActionScoreFloor(
                    DuoTeamCommandType.Regroup,
                    AIActionType.Regroup,
                    1f));
            Assert.AreEqual(
                118f,
                DuoTeamCommandAIUtility.GetActionScoreFloor(
                    DuoTeamCommandType.Retreat,
                    AIActionType.Retreat,
                    1f));
            Assert.AreEqual(
                0f,
                DuoTeamCommandAIUtility.GetActionScorePenalty(
                    DuoTeamCommandType.Retreat,
                    AIActionType.Evade));
            Assert.Less(
                DuoTeamCommandAIUtility.GetActionScorePenalty(
                    DuoTeamCommandType.Retreat,
                    AIActionType.Approach),
                0f);
        }

        [Test]
        public void DuoCommandAI_OffensiveCommandsYieldAtLowHealth()
        {
            Assert.AreEqual(
                0f,
                DuoTeamCommandAIUtility.GetActionScoreFloor(
                    DuoTeamCommandType.Attack,
                    AIActionType.Approach,
                    0.34f));
            Assert.AreEqual(
                0f,
                DuoTeamCommandAIUtility.GetActionScoreFloor(
                    DuoTeamCommandType.TakeCube,
                    AIActionType.Objective,
                    0.29f));
            Assert.Greater(
                DuoTeamCommandAIUtility.GetActionScoreFloor(
                    DuoTeamCommandType.TakeCube,
                    AIActionType.Objective,
                    0.80f),
                100f);
        }

        [Test]
        public void DuoSpawnProtection_BlocksDamageUntilItsModifierIsRemoved()
        {
            object source = new object();
            DamageModifierCollection modifiers = new DamageModifierCollection();
            modifiers.Add(new DamageModifier(
                DamageModifierType.PercentReduction,
                1f,
                source));

            float shield = 0f;
            Assert.AreEqual(0f, modifiers.ApplyIncoming(500f, ref shield));

            modifiers.RemoveBySource(source);
            Assert.AreEqual(500f, modifiers.ApplyIncoming(500f, ref shield));
        }

        [Test]
        public void DuoElimination_RequiresBothTeamMembersToBeDead()
        {
            Assert.IsFalse(ShowdownRules.IsTeamEliminated(2));
            Assert.IsFalse(ShowdownRules.IsTeamEliminated(1));
            Assert.IsTrue(ShowdownRules.IsTeamEliminated(0));
        }

        [Test]
        public void DuoElimination_EndsSessionOnlyForTheLocalTeam()
        {
            Assert.IsFalse(ShowdownRules.ShouldEndLocalDuoSession(
                ShowdownVariant.Duo,
                TeamType.Solo3,
                TeamType.Solo1));
            Assert.IsFalse(ShowdownRules.ShouldEndLocalDuoSession(
                ShowdownVariant.Solo,
                TeamType.Solo1,
                TeamType.Solo1));
            Assert.IsTrue(ShowdownRules.ShouldEndLocalDuoSession(
                ShowdownVariant.Duo,
                TeamType.Solo1,
                TeamType.Solo1));
        }

        [Test]
        public void DuoLocalElimination_RecordsLossWithoutAResolvedWinner()
        {
            try
            {
                MatchResultBoard.Reset();
                MatchResultBoard.Capture(
                    TeamType.Neutral,
                    0,
                    0,
                    TeamType.Solo1);

                Assert.IsFalse(MatchResultBoard.LocalResultKnown);
                MatchResultBoard.CaptureLocalOutcome(false);
                Assert.IsTrue(MatchResultBoard.LocalResultKnown);
                Assert.IsFalse(MatchResultBoard.LocalPlayerWon);
            }
            finally
            {
                MatchResultBoard.Reset();
            }
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(9, 4)]
        public void DefeatDrop_GuaranteesOneThenDropsHalfCarriedCubes(
            int carriedCubeCount,
            int expectedDropCount)
        {
            Assert.AreEqual(
                expectedDropCount,
                ShowdownRules.CalculatePowerCubeDropsOnDefeat(carriedCubeCount));
        }

        [Test]
        public void PowerCubePickup_WorksForAnyTeamAndUpdatesStatsAndCountEvent()
        {
            BrawlerDefinition definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            definition.BaseHealth = 2000f;
            definition.BaseDamage = 400f;
            definition.BaseMoveSpeed = 5f;
            definition.ProgressionBonuses = null;
            definition.SuperChargeSources = null;

            GameObject cubeObject = new GameObject("PowerCubeTest");
            try
            {
                PowerCube cube = cubeObject.AddComponent<PowerCube>();
                BrawlerState carrier = new BrawlerState(definition, TeamType.Solo4);
                int reportedCount = -1;
                carrier.OnPowerCubeCountChanged += count => reportedCount = count;

                Assert.IsTrue(cube.TryPickupBy(carrier));
                Assert.AreEqual(1, carrier.PowerCubeCount);
                Assert.AreEqual(1, reportedCount);
                Assert.AreEqual(2100f, carrier.MaxHealth.Value, 0.001f);
                Assert.AreEqual(378f, carrier.Damage.Value, 0.001f);
            }
            finally
            {
                if (cubeObject != null)
                    Object.DestroyImmediate(cubeObject);
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(DuoTeamCommandType.Regroup)]
        [TestCase(DuoTeamCommandType.Retreat)]
        [TestCase(DuoTeamCommandType.Attack)]
        [TestCase(DuoTeamCommandType.TakeCube)]
        public void DuoCommand_ReadResolvesMovingTargetWithoutExtendingExpiry(DuoTeamCommandType command)
        {
            GameModeId previousMode = SceneSelection.SelectedMode;
            ShowdownVariant previousVariant = SceneSelection.SelectedShowdownVariant;
            var definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            definition.BaseHealth = 2000f;
            definition.BaseMoveSpeed = 5f;
            var modeObject = new GameObject("CommandMode");
            var playerObject = new GameObject("CommandPlayer");
            var botObject = new GameObject("CommandBot");
            var targetObject = new GameObject("CommandTarget");
            BrawlerController enemy = null;
            try
            {
                SceneSelection.SelectedMode = GameModeId.SoloShowdown;
                SceneSelection.SelectedShowdownVariant = ShowdownVariant.Duo;
                var mode = modeObject.AddComponent<SoloShowdownMode>();
                var player = playerObject.AddComponent<BrawlerController>();
                var bot = botObject.AddComponent<BrawlerController>();
                player.InitializeFromMatchmaking(definition, TeamType.Solo1);
                bot.InitializeFromMatchmaking(definition, TeamType.Solo1);
                playerObject.AddComponent<PlayerCommandSource>();
                mode.RegisterBrawler(player);
                mode.RegisterBrawler(bot);
                int targetId = 0;
                if (command == DuoTeamCommandType.Attack)
                {
                    enemy = targetObject.AddComponent<BrawlerController>();
                    enemy.InitializeFromMatchmaking(definition, TeamType.Solo2);
                    CombatRegistry.Register(enemy);
                    targetId = enemy.EntityID;
                }
                else if (command == DuoTeamCommandType.TakeCube)
                {
                    targetObject.AddComponent<PowerCube>();
                    targetId = targetObject.GetInstanceID();
                }
                Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                    mode.TryIssueDuoTeamCommand(player, command, targetId,
                        Vector3.zero, 10u, out var issued));
                Transform movingTarget = ShowdownRules.DuoTeamCommandRequiresTarget(command)
                    ? targetObject.transform : playerObject.transform;
                movingTarget.position = new Vector3(3f, 0f, 4f);
                Assert.IsTrue(mode.TryGetActiveDuoTeamCommand(bot, 11u, out var active));
                Assert.AreEqual(movingTarget.position, active.TargetPosition);
                Assert.AreEqual(issued.ExpiresTick, active.ExpiresTick);
                Assert.IsFalse(mode.TryGetActiveDuoTeamCommand(player, 11u, out _));
            }
            finally
            {
                if (enemy != null) CombatRegistry.Unregister(enemy);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(botObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(modeObject);
                Object.DestroyImmediate(definition);
                SceneSelection.SelectedMode = previousMode;
                SceneSelection.SelectedShowdownVariant = previousVariant;
            }
        }

        [Test]
        public void DuoPowerCubePickup_GrantsCubeToBothLivingTeammates()
        {
            GameModeId previousMode = SceneSelection.SelectedMode;
            ShowdownVariant previousVariant = SceneSelection.SelectedShowdownVariant;
            BrawlerDefinition definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            definition.BaseHealth = 2000f;
            definition.BaseDamage = 400f;
            definition.BaseMoveSpeed = 5f;
            definition.ProgressionBonuses = null;
            definition.SuperChargeSources = null;

            GameObject modeObject = new GameObject("DuoShowdownModeTest");
            GameObject collectorObject = new GameObject("CollectorTest");
            GameObject teammateObject = new GameObject("TeammateTest");
            GameObject cubeObject = new GameObject("SharedPowerCubeTest");

            try
            {
                SceneSelection.SelectedMode = GameModeId.SoloShowdown;
                SceneSelection.SelectedShowdownVariant = ShowdownVariant.Duo;

                SoloShowdownMode mode = modeObject.AddComponent<SoloShowdownMode>();
                BrawlerController collector = collectorObject.AddComponent<BrawlerController>();
                BrawlerController teammate = teammateObject.AddComponent<BrawlerController>();
                collector.InitializeFromMatchmaking(definition, TeamType.Solo1);
                teammate.InitializeFromMatchmaking(definition, TeamType.Solo1);
                mode.RegisterBrawler(collector);
                mode.RegisterBrawler(teammate);

                PowerCube cube = cubeObject.AddComponent<PowerCube>();

                Assert.IsTrue(cube.TryPickupBy(collector));
                Assert.AreEqual(1, collector.State.PowerCubeCount);
                Assert.AreEqual(1, teammate.State.PowerCubeCount);
            }
            finally
            {
                SceneSelection.SelectedMode = previousMode;
                SceneSelection.SelectedShowdownVariant = previousVariant;
                Object.DestroyImmediate(cubeObject);
                Object.DestroyImmediate(teammateObject);
                Object.DestroyImmediate(collectorObject);
                Object.DestroyImmediate(modeObject);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ChargeCube_FillsAvailableMetersWithoutAddingStatPower()
        {
            BrawlerDefinition definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            HyperchargeDefinition hypercharge = ScriptableObject.CreateInstance<HyperchargeDefinition>();
            GameObject cubeObject = new GameObject("ChargePowerCubeTest");

            try
            {
                BrawlerState carrier = new BrawlerState(definition, TeamType.Solo1);
                carrier.SetEquippedHypercharge(hypercharge);
                carrier.Resources.AddSuperCharge(0.25f);
                carrier.Resources.AddHypercharge(0.40f);

                PowerCube cube = cubeObject.AddComponent<PowerCube>();
                cube.SetRewardType(PowerCubeRewardType.ChargeBoost);

                Assert.IsTrue(cube.TryPickupBy(carrier));
                Assert.AreEqual(1f, carrier.SuperCharge.ChargePercent, 0.001f);
                Assert.AreEqual(1f, carrier.Hypercharge.ChargePercent, 0.001f);
                Assert.AreEqual(0, carrier.PowerCubeCount);
            }
            finally
            {
                Object.DestroyImmediate(cubeObject);
                Object.DestroyImmediate(hypercharge);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ChargeCube_RemainsWhenEveryAvailableMeterIsFull()
        {
            BrawlerDefinition definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            HyperchargeDefinition hypercharge = ScriptableObject.CreateInstance<HyperchargeDefinition>();
            GameObject cubeObject = new GameObject("FullChargePowerCubeTest");

            try
            {
                BrawlerState carrier = new BrawlerState(definition, TeamType.Solo1);
                carrier.SetEquippedHypercharge(hypercharge);
                carrier.Resources.AddSuperCharge(1f);
                carrier.Resources.AddHypercharge(1f);

                PowerCube cube = cubeObject.AddComponent<PowerCube>();
                cube.SetRewardType(PowerCubeRewardType.ChargeBoost);

                Assert.IsFalse(cube.TryPickupBy(carrier));
                Assert.IsFalse(cube.IsPickedUp);
                Assert.IsTrue(cubeObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(cubeObject);
                Object.DestroyImmediate(hypercharge);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void SoloResults_ShowOnlyTheLocalPlayer()
        {
            Assert.IsTrue(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Solo,
                TeamType.Solo2,
                TeamType.Solo2,
                true));
            Assert.IsFalse(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Solo,
                TeamType.Solo2,
                TeamType.Solo2,
                false));
        }

        [Test]
        public void DuoResults_ShowLocalPlayerAndTeammateOnly()
        {
            Assert.IsTrue(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Duo,
                TeamType.Solo2,
                TeamType.Solo2,
                true));
            Assert.IsTrue(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Duo,
                TeamType.Solo2,
                TeamType.Solo2,
                false));
            Assert.IsFalse(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Duo,
                TeamType.Solo2,
                TeamType.Solo3,
                false));
        }
    }
}
