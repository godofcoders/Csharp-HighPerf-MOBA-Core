namespace MOBA.Core.Simulation
{
    public interface IAreaHazardService
    {
        void SpawnHazard(in AreaHazardSpawnRequest request);
    }
}