using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public static class AITeamCompositionPlanner
    {
        public struct PickOptions
        {
            public bool AvoidDuplicateBrawlersUntilPoolExhausted;
            public int MaxSameBrawlerPerTeam;
            public int MaxSameArchetypePerTeam;
            public bool RandomizeTies;
            public float RandomCandidateScoreBand;

            public static PickOptions Default => new PickOptions
            {
                AvoidDuplicateBrawlersUntilPoolExhausted = true,
                MaxSameBrawlerPerTeam = 1,
                MaxSameArchetypePerTeam = 1,
                RandomizeTies = true,
                RandomCandidateScoreBand = 16f
            };
        }

        public struct PickResult
        {
            public BrawlerDefinition Brawler;
            public float Score;
            public string Reason;
        }

        private struct CandidateScore
        {
            public BrawlerDefinition Brawler;
            public float Score;
            public string Reason;
        }

        private const float BlockedScore = -100000f;
        private const float TieTolerance = 0.01f;

        public static PickResult PickBotBrawler(
            IReadOnlyList<BrawlerDefinition> pool,
            IReadOnlyList<MatchParticipant> roster,
            TeamType team,
            GameModeId mode,
            PickOptions options)
        {
            if (pool == null || pool.Count == 0)
            {
                return new PickResult
                {
                    Brawler = null,
                    Score = 0f,
                    Reason = "empty_pool"
                };
            }

            bool hasNonDuplicateBrawlerOption = HasCandidateUnderBrawlerCap(
                pool,
                roster,
                team,
                Mathf.Max(1, options.MaxSameBrawlerPerTeam));

            CandidateScore best = default;
            bool hasBest = false;
            int tiedBestCount = 0;
            List<CandidateScore> scoredCandidates = new List<CandidateScore>(pool.Count);

            for (int i = 0; i < pool.Count; i++)
            {
                BrawlerDefinition candidate = pool[i];
                if (candidate == null)
                    continue;

                CandidateScore score = ScoreCandidate(
                    candidate,
                    roster,
                    team,
                    mode,
                    options,
                    hasNonDuplicateBrawlerOption);

                if (score.Score <= BlockedScore)
                    continue;

                scoredCandidates.Add(score);

                if (!hasBest || score.Score > best.Score + TieTolerance)
                {
                    best = score;
                    hasBest = true;
                    tiedBestCount = 1;
                    continue;
                }

                if (options.RandomizeTies &&
                    Mathf.Abs(score.Score - best.Score) <= TieTolerance)
                {
                    tiedBestCount++;
                    if (Random.Range(0, tiedBestCount) == 0)
                        best = score;
                }
            }

            if (hasBest)
            {
                if (TryPickFromCandidateBand(
                        scoredCandidates,
                        best,
                        options,
                        out CandidateScore randomizedBest))
                {
                    best = randomizedBest;
                }

                return new PickResult
                {
                    Brawler = best.Brawler,
                    Score = best.Score,
                    Reason = best.Reason
                };
            }

            BrawlerDefinition fallback = FindFirstValid(pool);
            return new PickResult
            {
                Brawler = fallback,
                Score = 0f,
                Reason = fallback != null ? "fallback_first_valid" : "no_valid_candidate"
            };
        }

        private static bool TryPickFromCandidateBand(
            List<CandidateScore> scoredCandidates,
            CandidateScore best,
            PickOptions options,
            out CandidateScore selected)
        {
            selected = best;

            float band = Mathf.Max(
                options.RandomCandidateScoreBand,
                options.RandomizeTies ? TieTolerance : 0f);

            if (scoredCandidates == null || scoredCandidates.Count <= 1 || band <= 0f)
                return false;

            float minScore = best.Score - band;
            float totalWeight = 0f;

            for (int i = 0; i < scoredCandidates.Count; i++)
            {
                CandidateScore candidate = scoredCandidates[i];
                if (candidate.Score < minScore)
                    continue;

                totalWeight += Mathf.Max(1f, candidate.Score - minScore + 1f);
            }

            if (totalWeight <= 0f)
                return false;

            float roll = Random.value * totalWeight;
            for (int i = 0; i < scoredCandidates.Count; i++)
            {
                CandidateScore candidate = scoredCandidates[i];
                if (candidate.Score < minScore)
                    continue;

                roll -= Mathf.Max(1f, candidate.Score - minScore + 1f);
                if (roll <= 0f)
                {
                    selected = candidate;
                    return true;
                }
            }

            return false;
        }

        public static int GetPreferredSpawnIndex(
            BrawlerDefinition brawler,
            int spawnPointCount,
            int teamOrdinal)
        {
            if (spawnPointCount <= 1)
                return 0;

            BrawlerArchetype archetype = brawler != null
                ? brawler.Archetype
                : BrawlerArchetype.Fighter;

            int center = spawnPointCount / 2;
            switch (archetype)
            {
                case BrawlerArchetype.Tank:
                case BrawlerArchetype.Fighter:
                case BrawlerArchetype.Controller:
                case BrawlerArchetype.Support:
                    return center;

                case BrawlerArchetype.Assassin:
                    return teamOrdinal % 2 == 0 ? 0 : spawnPointCount - 1;

                case BrawlerArchetype.Sniper:
                case BrawlerArchetype.Artillery:
                    return teamOrdinal % 2 == 0 ? spawnPointCount - 1 : 0;

                default:
                    return Mathf.Clamp(teamOrdinal, 0, spawnPointCount - 1);
            }
        }

        private static CandidateScore ScoreCandidate(
            BrawlerDefinition candidate,
            IReadOnlyList<MatchParticipant> roster,
            TeamType team,
            GameModeId mode,
            PickOptions options,
            bool hasNonDuplicateBrawlerOption)
        {
            int sameBrawlerOnTeam = CountBrawler(roster, team, candidate);
            int sameBrawlerInMatch = CountBrawler(roster, null, candidate);
            int sameArchetypeOnTeam = CountArchetype(roster, team, candidate.Archetype);

            int brawlerCap = Mathf.Max(1, options.MaxSameBrawlerPerTeam);
            if (options.AvoidDuplicateBrawlersUntilPoolExhausted &&
                hasNonDuplicateBrawlerOption &&
                sameBrawlerOnTeam >= brawlerCap)
            {
                return new CandidateScore
                {
                    Brawler = candidate,
                    Score = BlockedScore,
                    Reason = "blocked_duplicate_brawler"
                };
            }

            float score = 100f;
            string reason = "base";

            float modeWeight = GetModeWeight(candidate.Archetype, mode);
            score += modeWeight;
            reason += $"|mode={modeWeight:0}";

            float coverage = GetCoverageScore(candidate.Archetype, roster, team, mode);
            score += coverage;
            if (coverage > 0f)
                reason += $"|coverage={coverage:0}";

            if (sameBrawlerOnTeam > 0)
            {
                float penalty = sameBrawlerOnTeam * 72f;
                score -= penalty;
                reason += $"|team_dup_brawler=-{penalty:0}";
            }

            if (sameArchetypeOnTeam >= Mathf.Max(1, options.MaxSameArchetypePerTeam))
            {
                float penalty = (sameArchetypeOnTeam - options.MaxSameArchetypePerTeam + 1) * 36f;
                score -= penalty;
                reason += $"|team_dup_role=-{penalty:0}";
            }

            if (sameBrawlerInMatch > sameBrawlerOnTeam)
            {
                float penalty = (sameBrawlerInMatch - sameBrawlerOnTeam) * 10f;
                score -= penalty;
                reason += $"|match_dup=-{penalty:0}";
            }

            return new CandidateScore
            {
                Brawler = candidate,
                Score = score,
                Reason = reason
            };
        }

        private static float GetCoverageScore(
            BrawlerArchetype archetype,
            IReadOnlyList<MatchParticipant> roster,
            TeamType team,
            GameModeId mode)
        {
            float score = 0f;

            if (!TeamHasRoleBucket(roster, team, RoleBucket.Control) &&
                IsRoleBucket(archetype, RoleBucket.Control))
            {
                score += mode == GameModeId.GemGrab ? 24f : 14f;
            }

            if (!TeamHasRoleBucket(roster, team, RoleBucket.LongRange) &&
                IsRoleBucket(archetype, RoleBucket.LongRange))
            {
                score += mode == GameModeId.Knockout ? 24f : 14f;
            }

            if (!TeamHasRoleBucket(roster, team, RoleBucket.Frontline) &&
                IsRoleBucket(archetype, RoleBucket.Frontline))
            {
                score += mode == GameModeId.Knockout ? 8f : 16f;
            }

            return score;
        }

        private static float GetModeWeight(BrawlerArchetype archetype, GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.Knockout:
                    switch (archetype)
                    {
                        case BrawlerArchetype.Sniper:
                            return 24f;
                        case BrawlerArchetype.Artillery:
                            return 18f;
                        case BrawlerArchetype.Controller:
                            return 16f;
                        case BrawlerArchetype.Support:
                            return 10f;
                        case BrawlerArchetype.Assassin:
                        case BrawlerArchetype.Fighter:
                            return 8f;
                        case BrawlerArchetype.Tank:
                            return 4f;
                    }
                    break;

                case GameModeId.GemGrab:
                default:
                    switch (archetype)
                    {
                        case BrawlerArchetype.Support:
                            return 24f;
                        case BrawlerArchetype.Controller:
                            return 22f;
                        case BrawlerArchetype.Fighter:
                            return 14f;
                        case BrawlerArchetype.Sniper:
                            return 12f;
                        case BrawlerArchetype.Artillery:
                            return 10f;
                        case BrawlerArchetype.Tank:
                            return 8f;
                        case BrawlerArchetype.Assassin:
                            return 4f;
                    }
                    break;
            }

            return 0f;
        }

        private enum RoleBucket
        {
            Frontline,
            Control,
            LongRange
        }

        private static bool TeamHasRoleBucket(
            IReadOnlyList<MatchParticipant> roster,
            TeamType team,
            RoleBucket bucket)
        {
            if (roster == null)
                return false;

            for (int i = 0; i < roster.Count; i++)
            {
                MatchParticipant participant = roster[i];
                if (participant == null ||
                    participant.Team != team ||
                    participant.SelectedBrawler == null)
                {
                    continue;
                }

                if (IsRoleBucket(participant.SelectedBrawler.Archetype, bucket))
                    return true;
            }

            return false;
        }

        private static bool IsRoleBucket(BrawlerArchetype archetype, RoleBucket bucket)
        {
            switch (bucket)
            {
                case RoleBucket.Frontline:
                    return archetype == BrawlerArchetype.Tank ||
                           archetype == BrawlerArchetype.Fighter ||
                           archetype == BrawlerArchetype.Assassin;

                case RoleBucket.Control:
                    return archetype == BrawlerArchetype.Controller ||
                           archetype == BrawlerArchetype.Support ||
                           archetype == BrawlerArchetype.Artillery;

                case RoleBucket.LongRange:
                    return archetype == BrawlerArchetype.Sniper ||
                           archetype == BrawlerArchetype.Artillery ||
                           archetype == BrawlerArchetype.Controller;

                default:
                    return false;
            }
        }

        private static bool HasCandidateUnderBrawlerCap(
            IReadOnlyList<BrawlerDefinition> pool,
            IReadOnlyList<MatchParticipant> roster,
            TeamType team,
            int brawlerCap)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                BrawlerDefinition candidate = pool[i];
                if (candidate == null)
                    continue;

                if (CountBrawler(roster, team, candidate) < brawlerCap)
                    return true;
            }

            return false;
        }

        private static int CountBrawler(
            IReadOnlyList<MatchParticipant> roster,
            TeamType? team,
            BrawlerDefinition brawler)
        {
            if (roster == null || brawler == null)
                return 0;

            int count = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                MatchParticipant participant = roster[i];
                if (participant == null || participant.SelectedBrawler != brawler)
                    continue;

                if (team.HasValue && participant.Team != team.Value)
                    continue;

                count++;
            }

            return count;
        }

        private static int CountArchetype(
            IReadOnlyList<MatchParticipant> roster,
            TeamType team,
            BrawlerArchetype archetype)
        {
            if (roster == null)
                return 0;

            int count = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                MatchParticipant participant = roster[i];
                if (participant == null ||
                    participant.Team != team ||
                    participant.SelectedBrawler == null)
                {
                    continue;
                }

                if (participant.SelectedBrawler.Archetype == archetype)
                    count++;
            }

            return count;
        }

        private static BrawlerDefinition FindFirstValid(IReadOnlyList<BrawlerDefinition> pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                    return pool[i];
            }

            return null;
        }
    }
}
