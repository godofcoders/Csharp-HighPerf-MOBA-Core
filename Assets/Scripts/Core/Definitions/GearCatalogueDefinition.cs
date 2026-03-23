using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "GearCatalog", menuName = "MOBA/Builds/Gear Catalog")]
    public class GearCatalogDefinition : ScriptableObject
    {
        public GearDefinition[] SharedGearOptions;

        public List<GearDefinition> BuildList()
        {
            List<GearDefinition> result = new List<GearDefinition>(8);

            if (SharedGearOptions == null)
                return result;

            for (int i = 0; i < SharedGearOptions.Length; i++)
            {
                GearDefinition gear = SharedGearOptions[i];
                if (gear == null)
                    continue;

                if (!result.Contains(gear))
                    result.Add(gear);
            }

            return result;
        }
    }
}