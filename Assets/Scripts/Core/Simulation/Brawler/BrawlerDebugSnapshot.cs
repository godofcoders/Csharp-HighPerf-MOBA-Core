using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class BrawlerDebugSnapshot
    {
        public string BrawlerName;
        public int EntityId;

        public float CurrentHealth;
        public float MaxHealth;
        public int CurrentPowerLevel;

        public string ActionState;
        public bool CanMove;
        public bool CanUseActionInput;

        public bool MainAttackReady;
        public bool GadgetReady;
        public bool SuperReady;
        public bool HyperchargeReady;

        public string MainAttackBlockReason;
        public string GadgetBlockReason;
        public string SuperBlockReason;
        public string HyperchargeBlockReason;

        public string EquippedGadget;
        public string EquippedStarPower;
        public string EquippedHypercharge;

        public readonly List<string> EquippedGears = new List<string>(2);
        public readonly List<string> EquippedPassives = new List<string>(4);

        public bool Gear1Unlocked;
        public bool Gear2Unlocked;
        public bool GadgetUnlocked;
        public bool StarPowerUnlocked;
        public bool HyperchargeUnlocked;

        public bool HyperchargeActive;
        public float HyperchargeChargePercent;
        public bool SuperCharged;

        public Vector3 Position;

        public void ClearLists()
        {
            EquippedGears.Clear();
            EquippedPassives.Clear();
        }
    }
}