namespace MOBA.Core.Simulation.AI
{
    public readonly struct AITeamLaneOwnershipSnapshot
    {
        public readonly bool HasValue;
        public readonly int BotEntityId;
        public readonly uint Tick;
        public readonly AITeamLaneAssignment AssignedLane;
        public readonly AITeamLaneAssignment CurrentLane;
        public readonly AITeamLaneAssignment RecommendedLane;
        public readonly AITeamLaneAssignment UnderOwnedLane;
        public readonly AITeamLaneAssignment OverOwnedLane;
        public readonly int LeftOwners;
        public readonly int MidOwners;
        public readonly int RightOwners;
        public readonly bool AssignedLaneAbandoned;
        public readonly bool CurrentLaneOverOwned;
        public readonly bool ShouldRotate;
        public readonly string Reason;

        public bool HasRecommendedLane =>
            HasValue && RecommendedLane != AITeamLaneAssignment.None;

        public AITeamLaneOwnershipSnapshot(
            int botEntityId,
            uint tick,
            AITeamLaneAssignment assignedLane,
            AITeamLaneAssignment currentLane,
            AITeamLaneAssignment recommendedLane,
            AITeamLaneAssignment underOwnedLane,
            AITeamLaneAssignment overOwnedLane,
            int leftOwners,
            int midOwners,
            int rightOwners,
            bool assignedLaneAbandoned,
            bool currentLaneOverOwned,
            bool shouldRotate,
            string reason)
        {
            HasValue = true;
            BotEntityId = botEntityId;
            Tick = tick;
            AssignedLane = assignedLane;
            CurrentLane = currentLane;
            RecommendedLane = recommendedLane;
            UnderOwnedLane = underOwnedLane;
            OverOwnedLane = overOwnedLane;
            LeftOwners = leftOwners;
            MidOwners = midOwners;
            RightOwners = rightOwners;
            AssignedLaneAbandoned = assignedLaneAbandoned;
            CurrentLaneOverOwned = currentLaneOverOwned;
            ShouldRotate = shouldRotate;
            Reason = string.IsNullOrEmpty(reason) ? "stable" : reason;
        }

        public static AITeamLaneOwnershipSnapshot None(int botEntityId, uint tick)
        {
            return default;
        }

        public int GetOwnerCount(AITeamLaneAssignment lane)
        {
            switch (AILaneDisciplineUtility.ResolveMapLane(lane, BotEntityId))
            {
                case AITeamLaneAssignment.Left:
                    return LeftOwners;

                case AITeamLaneAssignment.Mid:
                    return MidOwners;

                case AITeamLaneAssignment.Right:
                    return RightOwners;

                default:
                    return 0;
            }
        }

        public string GetDebugSummary()
        {
            if (!HasValue)
                return "LaneOwn=None";

            return
                $"LaneOwn={RecommendedLane} " +
                $"Assigned={AssignedLane} " +
                $"Current={CurrentLane} " +
                $"L/M/R={LeftOwners}/{MidOwners}/{RightOwners} " +
                $"Under={UnderOwnedLane} " +
                $"Over={OverOwnedLane} " +
                $"Rotate={ShouldRotate} " +
                $"Reason={Reason}";
        }
    }
}
