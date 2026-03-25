namespace MOBA.Core.Simulation
{
    public struct BrawlerActionStateData
    {
        public BrawlerActionStateType StateType;
        public uint StartTick;
        public uint LockUntilTick;
        public bool AllowMovement;
        public bool AllowActionInput;
        public bool IsInterruptible;

        public bool IsActive(uint currentTick)
        {
            return currentTick < LockUntilTick;
        }
    }
}