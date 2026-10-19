using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public enum DuoTeamCommandType
    {
        None,
        Regroup,
        Attack,
        Retreat,
        TakeCube
    }

    public enum DuoTeamCommandIssueResult
    {
        Accepted,
        NotDuoShowdown,
        InvalidCommand,
        IssuerUnavailable,
        TeammateUnavailable,
        InvalidTarget,
        Cooldown
    }

    public readonly struct DuoTeamCommandDirective
    {
        public readonly DuoTeamCommandType CommandType;
        public readonly TeamType Team;
        public readonly int IssuerEntityId;
        public readonly int TargetEntityId;
        public readonly Vector3 TargetPosition;
        public readonly uint IssuedTick;
        public readonly uint ExpiresTick;

        public bool IsValid => CommandType != DuoTeamCommandType.None;

        public DuoTeamCommandDirective(
            DuoTeamCommandType commandType,
            TeamType team,
            int issuerEntityId,
            int targetEntityId,
            Vector3 targetPosition,
            uint issuedTick,
            uint expiresTick)
        {
            CommandType = commandType;
            Team = team;
            IssuerEntityId = issuerEntityId;
            TargetEntityId = targetEntityId;
            TargetPosition = targetPosition;
            IssuedTick = issuedTick;
            ExpiresTick = expiresTick;
        }
    }

    public sealed class DuoTeamCommandStore
    {
        private readonly Dictionary<TeamType, DuoTeamCommandDirective> _activeByTeam =
            new Dictionary<TeamType, DuoTeamCommandDirective>();
        private readonly Dictionary<TeamType, uint> _nextIssueTickByTeam =
            new Dictionary<TeamType, uint>();

        public DuoTeamCommandIssueResult TryIssue(
            TeamType team,
            int issuerEntityId,
            DuoTeamCommandType commandType,
            int targetEntityId,
            Vector3 targetPosition,
            uint currentTick,
            bool issuerAvailable,
            bool teammateAvailable,
            bool targetValid,
            out DuoTeamCommandDirective directive)
        {
            directive = default;
            if (!ShowdownRules.IsDuoTeam(team))
                return DuoTeamCommandIssueResult.NotDuoShowdown;
            if (commandType == DuoTeamCommandType.None)
                return DuoTeamCommandIssueResult.InvalidCommand;
            if (!issuerAvailable || issuerEntityId == 0)
                return DuoTeamCommandIssueResult.IssuerUnavailable;
            if (!teammateAvailable)
                return DuoTeamCommandIssueResult.TeammateUnavailable;
            if (ShowdownRules.DuoTeamCommandRequiresTarget(commandType) && !targetValid)
                return DuoTeamCommandIssueResult.InvalidTarget;
            if (_nextIssueTickByTeam.TryGetValue(team, out uint nextTick) &&
                currentTick < nextTick)
            {
                return DuoTeamCommandIssueResult.Cooldown;
            }

            uint durationTicks = ShowdownRules.GetDuoTeamCommandDurationTicks(commandType);
            directive = new DuoTeamCommandDirective(
                commandType,
                team,
                issuerEntityId,
                targetEntityId,
                targetPosition,
                currentTick,
                currentTick + durationTicks);
            _activeByTeam[team] = directive;
            _nextIssueTickByTeam[team] =
                currentTick + ShowdownRules.DuoTeamCommandCooldownTicks;
            return DuoTeamCommandIssueResult.Accepted;
        }

        public bool TryGetActive(
            TeamType team,
            uint currentTick,
            out DuoTeamCommandDirective directive)
        {
            if (!_activeByTeam.TryGetValue(team, out directive))
                return false;

            if (currentTick <= directive.ExpiresTick)
                return true;

            _activeByTeam.Remove(team);
            directive = default;
            return false;
        }

        public uint GetCooldownRemainingTicks(TeamType team, uint currentTick)
        {
            return _nextIssueTickByTeam.TryGetValue(team, out uint readyTick) && readyTick > currentTick
                ? readyTick - currentTick : 0u;
        }

        public void ClearTeam(TeamType team)
        {
            _activeByTeam.Remove(team);
            _nextIssueTickByTeam.Remove(team);
        }

        public void ClearAll()
        {
            _activeByTeam.Clear();
            _nextIssueTickByTeam.Clear();
        }
    }

    public static class ShowdownRules
    {
        public const int DuoTeamCount = 5;
        public const int DuoPlayersPerTeam = 2;
        public const int DuoContestantCount = DuoTeamCount * DuoPlayersPerTeam;
        public const float DuoRespawnDelaySeconds = 15f;
        public const float DuoSpawnImmunitySeconds = 2f;
        public const float DuoTeammateSpawnSeparation = 2.5f;
        public const float DuoCohesionFollowDistance = 4.25f;
        public const float DuoCohesionRegroupDistance = 6.25f;
        public const float DuoCohesionHardLeashDistance = 9f;
        public const float DuoCohesionUrgentDistance = 12f;
        public const float DuoCohesionPriorityScore = 110f;
        public static readonly uint DuoTeamCommandCooldownTicks =
            SimulationClock.SecondsToTicks(1f);

        public static bool IsDuoSelected =>
            SceneSelection.SelectedMode == GameModeId.SoloShowdown &&
            SceneSelection.SelectedShowdownVariant == ShowdownVariant.Duo;

        public static string GetDisplayName(ShowdownVariant variant)
        {
            return variant == ShowdownVariant.Duo
                ? "Duo Showdown"
                : "Solo Showdown";
        }

        public static TeamType GetDuoTeamForRosterIndex(int rosterIndex)
        {
            int safeIndex = UnityEngine.Mathf.Clamp(rosterIndex, 0, DuoContestantCount - 1);
            return TeamRelationshipUtility.GetSoloTeam(safeIndex / DuoPlayersPerTeam);
        }

        public static bool IsDuoTeam(TeamType team)
        {
            int teamIndex = (int)team - (int)TeamType.Solo1;
            return teamIndex >= 0 && teamIndex < DuoTeamCount;
        }

        public static bool DuoTeamCommandRequiresTarget(DuoTeamCommandType commandType)
        {
            return commandType == DuoTeamCommandType.Attack ||
                commandType == DuoTeamCommandType.TakeCube;
        }

        public static uint GetDuoTeamCommandDurationTicks(DuoTeamCommandType commandType)
        {
            float seconds;
            switch (commandType)
            {
                case DuoTeamCommandType.Regroup:
                case DuoTeamCommandType.TakeCube:
                    seconds = 6f;
                    break;
                case DuoTeamCommandType.Attack:
                    seconds = 5f;
                    break;
                case DuoTeamCommandType.Retreat:
                    seconds = 4f;
                    break;
                default:
                    seconds = 0f;
                    break;
            }

            return SimulationClock.SecondsToTicks(seconds);
        }

        public static int GetDuoSpawnOrdinal(TeamType team, int teammateIndex)
        {
            int teamIndex = UnityEngine.Mathf.Clamp(
                (int)team - (int)TeamType.Solo1,
                0,
                DuoTeamCount - 1);
            int safeTeammateIndex = UnityEngine.Mathf.Clamp(
                teammateIndex,
                0,
                DuoPlayersPerTeam - 1);
            return teamIndex * DuoPlayersPerTeam + safeTeammateIndex;
        }

        public static Vector3 ResolveDuoSpawnPosition(
            Bounds bounds,
            int teamIndex,
            int teammateIndex,
            float edgeInset,
            float teammateSeparation,
            int placementAttempt = 0)
        {
            int safeTeamIndex = Mathf.Clamp(teamIndex, 0, DuoTeamCount - 1);
            int safeTeammateIndex = Mathf.Clamp(
                teammateIndex,
                0,
                DuoPlayersPerTeam - 1);
            float safeInset = Mathf.Max(0f, edgeInset);
            float halfPairSpacing = Mathf.Max(0.75f, teammateSeparation * 0.5f);
            float radiusX = Mathf.Max(0f, bounds.extents.x - safeInset - halfPairSpacing);
            float radiusZ = Mathf.Max(0f, bounds.extents.z - safeInset - halfPairSpacing);
            float angleDegrees = -90f +
                                 safeTeamIndex * (360f / DuoTeamCount) +
                                 placementAttempt * 17f;
            float angle = angleDegrees * Mathf.Deg2Rad;

            Vector3 center = bounds.center + new Vector3(
                Mathf.Cos(angle) * radiusX,
                0f,
                Mathf.Sin(angle) * radiusZ);
            Vector3 tangent = new Vector3(
                -Mathf.Sin(angle) * Mathf.Max(0.1f, radiusX),
                0f,
                Mathf.Cos(angle) * Mathf.Max(0.1f, radiusZ));
            if (tangent.sqrMagnitude <= 0.0001f)
                tangent = Vector3.right;
            else
                tangent.Normalize();

            float side = safeTeammateIndex == 0 ? -1f : 1f;
            Vector3 position = center + tangent * (halfPairSpacing * side);
            position.x = Mathf.Clamp(
                position.x,
                bounds.min.x + safeInset,
                bounds.max.x - safeInset);
            position.z = Mathf.Clamp(
                position.z,
                bounds.min.z + safeInset,
                bounds.max.z - safeInset);
            position.y = bounds.center.y;
            return position;
        }

        public static bool IsTeamEliminated(int livingMembers)
        {
            return livingMembers <= 0;
        }

        public static float GetRespawnSecondsRemaining(float readyAt, float currentTime)
        {
            return Mathf.Max(0f, readyAt - currentTime);
        }

        public static bool ShouldUseTeammateAsCohesionAnchor(
            bool selfIsPlayer,
            bool teammateIsPlayer,
            int selfEntityId,
            int teammateEntityId)
        {
            if (selfIsPlayer)
                return false;

            if (teammateIsPlayer)
                return true;

            return selfEntityId > teammateEntityId;
        }

        public static float CalculateDuoCohesionScore(
            float teammateDistance,
            bool hasLiveTarget)
        {
            float distance = Mathf.Max(0f, teammateDistance);
            if (distance <= DuoCohesionFollowDistance)
                return 0f;

            if (!hasLiveTarget)
            {
                float idleSeparation = Mathf.InverseLerp(
                    DuoCohesionFollowDistance,
                    DuoCohesionUrgentDistance,
                    distance);
                return Mathf.Lerp(76f, 120f, idleSeparation);
            }

            if (distance < DuoCohesionRegroupDistance)
            {
                float combatSeparation = Mathf.InverseLerp(
                    DuoCohesionFollowDistance,
                    DuoCohesionRegroupDistance,
                    distance);
                return Mathf.Lerp(24f, 84f, combatSeparation);
            }

            float urgentSeparation = Mathf.InverseLerp(
                DuoCohesionRegroupDistance,
                DuoCohesionUrgentDistance,
                distance);
            return Mathf.Lerp(
                DuoCohesionPriorityScore,
                120f,
                urgentSeparation);
        }

        public static bool ShouldForceDuoRegroup(float teammateDistance)
        {
            return teammateDistance >= DuoCohesionHardLeashDistance;
        }

        public static Vector3 ClampDuoDestination(
            Vector3 teammatePosition,
            Vector3 destination)
        {
            Vector3 offset = destination - teammatePosition;
            offset.y = 0f;
            float maximumDistance = DuoCohesionHardLeashDistance;
            if (offset.sqrMagnitude <= maximumDistance * maximumDistance)
                return destination;

            Vector3 clamped = teammatePosition + offset.normalized * maximumDistance;
            clamped.y = destination.y;
            return clamped;
        }

        public static bool ShouldEndLocalDuoSession(
            ShowdownVariant variant,
            TeamType eliminatedTeam,
            TeamType localPlayerTeam)
        {
            return variant == ShowdownVariant.Duo &&
                   localPlayerTeam != TeamType.Neutral &&
                   eliminatedTeam == localPlayerTeam;
        }

        public static int CalculatePowerCubeDropsOnDefeat(int carriedCubeCount)
        {
            if (carriedCubeCount <= 1)
                return 1;

            return UnityEngine.Mathf.Max(1, carriedCubeCount / 2);
        }

        public static bool ShouldShowResultEntry(
            ShowdownVariant variant,
            TeamType localTeam,
            TeamType candidateTeam,
            bool isLocalPlayer)
        {
            if (isLocalPlayer)
                return true;

            return variant == ShowdownVariant.Duo &&
                   localTeam != TeamType.Neutral &&
                   candidateTeam == localTeam;
        }
    }
}
