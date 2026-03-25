using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "PassiveFamily", menuName = "MOBA/Passives/Passive Family")]
    public class PassiveFamilyDefinition : ScriptableObject
    {
        public string FamilyName;
        [TextArea] public string Notes;
    }
}