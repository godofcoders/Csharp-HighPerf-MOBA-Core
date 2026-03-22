using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "BrawlerBuild", menuName = "MOBA/Builds/Brawler Build")]
    public class BrawlerBuildDefinition : ScriptableObject
    {
        public BrawlerBuildSlotSelection[] Selections;
    }
}