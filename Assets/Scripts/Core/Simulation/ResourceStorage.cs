using UnityEngine;

namespace MOBA.Core.Simulation
{
    public class ResourceStorage
    {
        public int MaxAmmo { get; private set; }
        public float CurrentAmmo { get; private set; }
        public float ReloadSpeed { get; private set; }

        public ResourceStorage(int max, float reloadSpeed)
        {
            MaxAmmo = max;
            CurrentAmmo = max;
            ReloadSpeed = reloadSpeed;
        }

        public bool Consume(int amount)
        {
            if (CurrentAmmo >= amount)
            {
                CurrentAmmo -= amount;
                return true;
            }

            return false;
        }

        public void Tick(float deltaTime)
        {
            if (CurrentAmmo < MaxAmmo)
            {
                CurrentAmmo += ReloadSpeed * deltaTime;

                if (CurrentAmmo > MaxAmmo)
                    CurrentAmmo = MaxAmmo;
            }
        }

        public void Refill()
        {
            CurrentAmmo = MaxAmmo;
        }

        public int AvailableBars => Mathf.FloorToInt(CurrentAmmo);
    }
}