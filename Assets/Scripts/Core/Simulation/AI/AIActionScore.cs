namespace MOBA.Core.Simulation.AI
{
    public struct AIActionScore
    {
        public AIActionType ActionType;
        public float Score;

        public AIActionScore(AIActionType actionType, float score)
        {
            ActionType = actionType;
            Score = score;
        }
    }
}