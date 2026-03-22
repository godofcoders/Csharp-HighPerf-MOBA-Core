using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "BrawlerBuildLayout", menuName = "MOBA/Builds/Brawler Build Layout")]
    public class BrawlerBuildLayoutDefinition : ScriptableObject
    {
        public BrawlerBuildSlotDefinition[] Slots;
    }
}