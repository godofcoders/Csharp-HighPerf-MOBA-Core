using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    public abstract class GadgetDefinition : ScriptableObject
    {
        public string AbilityName;
        public int MaxCharges = 3;
        public float Cooldown = 5f;

        public abstract IAbilityLogic CreateLogic();
    }
}