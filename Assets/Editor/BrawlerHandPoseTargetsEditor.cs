using System;
using MOBA.Core.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace MOBA.EditorTools
{
    [CustomEditor(typeof(BrawlerHandPoseTargets))]
    [CanEditMultipleObjects]
    public sealed class BrawlerHandPoseTargetsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Grip Authoring", EditorStyles.boldLabel);

            if (GUILayout.Button("Create / Refresh Full Grip Rig"))
                ForEachSelectedTarget(targets, poseTargets => poseTargets.CreateFullGripAuthoringRig());

            if (GUILayout.Button("Snap Hand Targets To Humanoid Hands"))
                ForEachSelectedTarget(targets, poseTargets => poseTargets.SnapHandTargetsToHumanoidHands());

            using (new EditorGUI.DisabledScope(targets.Length != 1))
            {
                if (GUILayout.Button("Select Target Root") && target is BrawlerHandPoseTargets poseTargets)
                    poseTargets.SelectTargetRoot();
            }

            EditorGUILayout.HelpBox(
                "Move Right/Left IK targets to place the hands. Move Weapon/Offhand Grip targets to publish attachment sockets. Aim and Muzzle targets show the intended firing line.",
                MessageType.Info);
        }

        private static void ForEachSelectedTarget(
            UnityEngine.Object[] selectedTargets,
            Action<BrawlerHandPoseTargets> action)
        {
            for (int i = 0; i < selectedTargets.Length; i++)
            {
                if (selectedTargets[i] is BrawlerHandPoseTargets poseTargets)
                    action(poseTargets);
            }
        }
    }
}
