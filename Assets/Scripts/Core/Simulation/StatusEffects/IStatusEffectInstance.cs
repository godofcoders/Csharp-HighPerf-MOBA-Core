namespace MOBA.Core.Simulation
{
    public interface IStatusEffectInstance
    {
        StatusEffectType Type { get; }
        uint StartTick { get; }
        uint EndTick { get; }

        void Apply(BrawlerState state, uint currentTick);
        void Tick(BrawlerState state, uint currentTick);
        void Remove(BrawlerState state, uint currentTick);

        bool CanMerge(StatusEffectContext context);
        void Merge(StatusEffectContext context, uint currentTick);
        bool IsExpired(uint currentTick);
    }
}