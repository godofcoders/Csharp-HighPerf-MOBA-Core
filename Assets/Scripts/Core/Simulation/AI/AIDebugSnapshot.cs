using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIDebugSnapshot
    {
        public string BrawlerName;
        public string CurrentAction;
        public string CurrentTargetName;
        public int CurrentTargetId;

        public float Health;
        public float MaxHealth;

        public bool IsStunned;
        public bool IsBurning;
        public bool IsSlowed;
        public bool IsRevealed;

        public Vector3 Position;
        public Vector3? TargetPosition;

        public string TeamTactic;
        public string ObjectiveName;

        public readonly List<AIActionScore> ActionScores = new List<AIActionScore>(16);
        public readonly List<string> ActiveStatuses = new List<string>(8);

        public void ClearLists()
        {
            ActionScores.Clear();
            ActiveStatuses.Clear();
        }
    }
}