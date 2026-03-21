using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public class AIDebugOverlay : MonoBehaviour
    {
        [SerializeField] private BrawlerController _targetBrawler;
        [SerializeField] private bool _showAllBotsInConsole = false;
        [SerializeField] private KeyCode _cycleKey = KeyCode.F3;

        private BrawlerController[] _allBrawlers;
        private int _currentIndex;

        private void Update()
        {
            if (Input.GetKeyDown(_cycleKey))
            {
                CycleTarget();
            }

            if (_showAllBotsInConsole && Input.GetKeyDown(KeyCode.F4))
            {
                DumpAllSnapshotsToConsole();
            }
        }

        private void OnGUI()
        {
            if (_targetBrawler == null)
                return;

            var snapshot = AIDebugTracker.GetSnapshot(_targetBrawler.EntityID);
            if (snapshot == null)
                return;

            GUILayout.BeginArea(new Rect(20, 20, 420, 520), GUI.skin.box);

            GUILayout.Label($"AI DEBUG: {snapshot.BrawlerName}");
            GUILayout.Label($"Action: {snapshot.CurrentAction}");
            GUILayout.Label($"Target: {snapshot.CurrentTargetName} ({snapshot.CurrentTargetId})");
            GUILayout.Label($"HP: {snapshot.Health:0}/{snapshot.MaxHealth:0}");
            GUILayout.Label($"Position: {snapshot.Position}");
            GUILayout.Label($"Team Tactic: {snapshot.TeamTactic}");
            GUILayout.Label($"Objective: {snapshot.ObjectiveName}");

            GUILayout.Space(8);
            GUILayout.Label("Flags:");
            GUILayout.Label($"Stunned: {snapshot.IsStunned}");
            GUILayout.Label($"Burning: {snapshot.IsBurning}");
            GUILayout.Label($"Slowed: {snapshot.IsSlowed}");
            GUILayout.Label($"Revealed: {snapshot.IsRevealed}");

            GUILayout.Space(8);
            GUILayout.Label("Statuses:");
            for (int i = 0; i < snapshot.ActiveStatuses.Count; i++)
            {
                GUILayout.Label($"- {snapshot.ActiveStatuses[i]}");
            }

            GUILayout.Space(8);
            GUILayout.Label("Action Scores:");
            for (int i = 0; i < snapshot.ActionScores.Count; i++)
            {
                var score = snapshot.ActionScores[i];
                GUILayout.Label($"{score.ActionType}: {score.Score:0.00}");
            }

            GUILayout.EndArea();
        }

        private void CycleTarget()
        {
            _allBrawlers = FindObjectsOfType<BrawlerController>();
            if (_allBrawlers == null || _allBrawlers.Length == 0)
                return;

            _currentIndex++;
            if (_currentIndex >= _allBrawlers.Length)
                _currentIndex = 0;

            _targetBrawler = _allBrawlers[_currentIndex];
        }

        private void DumpAllSnapshotsToConsole()
        {
            foreach (var kvp in AIDebugTracker.GetAll())
            {
                var snapshot = kvp.Value;
                if (snapshot == null)
                    continue;

                Debug.Log(
                    $"[AI DEBUG] {snapshot.BrawlerName} | " +
                    $"Action={snapshot.CurrentAction} | " +
                    $"Target={snapshot.CurrentTargetName} | " +
                    $"HP={snapshot.Health:0}/{snapshot.MaxHealth:0} | " +
                    $"Statuses={string.Join(", ", snapshot.ActiveStatuses)}");
            }
        }
    }
}