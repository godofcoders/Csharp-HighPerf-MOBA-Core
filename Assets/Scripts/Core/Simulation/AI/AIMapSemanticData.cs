using System;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    [Flags]
    public enum AIMapSemanticTag
    {
        None = 0,
        Lane = 1 << 0,
        Choke = 1 << 1,
        CoverCluster = 1 << 2,
        ThrowerSafeZone = 1 << 3,
        DangerCorridor = 1 << 4
    }

    public readonly struct AIMapSemanticZoneInfo
    {
        public readonly int Id;
        public readonly string Name;
        public readonly AIMapSemanticTag Tags;
        public readonly AITeamLaneAssignment Lane;
        public readonly float Influence;

        public AIMapSemanticZoneInfo(
            int id,
            string name,
            AIMapSemanticTag tags,
            AITeamLaneAssignment lane,
            float influence)
        {
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? $"SemanticZone{id}" : name;
            Tags = tags;
            Lane = lane;
            Influence = Mathf.Max(0f, influence);
        }
    }

    public struct AIMapSemanticCell
    {
        public AIMapSemanticTag Tags;
        public AITeamLaneAssignment Lane;
        public int PrimaryZoneId;
        public float Influence;

        public bool HasAny => Tags != AIMapSemanticTag.None;

        public bool HasTag(AIMapSemanticTag tag)
        {
            return (Tags & tag) != 0;
        }
    }
}
