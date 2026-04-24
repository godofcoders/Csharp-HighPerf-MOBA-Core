using UnityEditor;
using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI.EditorTools
{
    // Custom inspector for BrawlerAIProfile.
    //
    // Primary job: expose ApplyArchetypeDefaults as a deliberate, undo-able
    // designer action. Previously this method was called at runtime by
    // BrawlerAIController, which silently overwrote per-brawler authored
    // values. That runtime call has been removed; the method is now
    // editor-only and surfaces here.
    //
    // Design rationale: ScriptableObject assets are designer-authored truth.
    // Runtime systems read them; they do not write them. Archetype defaults
    // are a one-click starting point, not a live policy.
    [CustomEditor(typeof(BrawlerAIProfile))]
    public class BrawlerAIProfileEditor : UnityEditor.Editor
    {
        private BrawlerArchetype _archetypeToApply;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Archetype defaults (editor-only)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Stamps archetype baseline values into this profile. Overwrites " +
                "the current values for fields that the selected archetype sets. " +
                "Use as a starting point, then hand-tune the fields that deviate " +
                "from the baseline. Undo is supported.",
                MessageType.Info);

            _archetypeToApply = (BrawlerArchetype)
                EditorGUILayout.EnumPopup("Archetype to apply", _archetypeToApply);

            if (GUILayout.Button("Apply archetype defaults"))
            {
                BrawlerAIProfile profile = (BrawlerAIProfile)target;
                Undo.RecordObject(profile, "Apply archetype defaults");
                profile.ApplyArchetypeDefaults(_archetypeToApply);
                EditorUtility.SetDirty(profile);
            }
        }
    }
}
