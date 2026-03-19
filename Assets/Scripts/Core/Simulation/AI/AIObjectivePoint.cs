using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public class AIObjectivePoint : MonoBehaviour
    {
        public AIObjectiveType ObjectiveType = AIObjectiveType.MidControl;
        public float Weight = 50f;
        public float Radius = 3f;
        public bool TeamSpecific = false;
    }
}