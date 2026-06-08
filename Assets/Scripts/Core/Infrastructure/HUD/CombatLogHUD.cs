using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Screen-space combat feed for high-signal match events. The simulation
    /// records every combat entry; this view intentionally filters that stream
    /// down to readable kill-feed moments so it does not become damage spam.
    /// </summary>
    public class CombatLogHUD : MonoBehaviour
    {
        [Header("Text targets")]
        [Tooltip("TextMeshPro target. Preferred if both TMP and legacy text are wired.")]
        [SerializeField] private TMP_Text _tmpText;

        [Tooltip("Legacy UnityEngine.UI.Text fallback.")]
        [SerializeField] private Text _legacyText;

        [Tooltip("Optional visual root for the feed panel. Do not assign this same GameObject if this component should keep updating while empty.")]
        [SerializeField] private GameObject _feedRoot;

        [Header("Filtering")]
        [Min(1)]
        [SerializeField] private int _maxLines = 5;

        [Min(8)]
        [SerializeField] private int _maxEntriesToScan = 128;

        [SerializeField] private bool _showAssists = true;
        [SerializeField] private bool _showHeals = true;
        [SerializeField] private bool _showFatalDamage = false;
        [SerializeField] private bool _showStatusEvents = false;
        [SerializeField] private bool _includeTick = false;
        [SerializeField] private bool _useRichText = true;
        [SerializeField] private bool _useLocalPerspectiveLabels = true;

        [Tooltip("0 keeps entries until the combat log service drops them. Non-zero hides older feed lines by simulation tick age.")]
        [Min(0)]
        [SerializeField] private int _maxEntryAgeTicks = 0;

        [Header("Performance")]
        [Min(0.05f)]
        [SerializeField] private float _refreshIntervalSeconds = 0.12f;

        [Min(0.25f)]
        [SerializeField] private float _labelRefreshIntervalSeconds = 1f;

        private readonly Dictionary<int, string> _entityLabels =
            new Dictionary<int, string>(16);

        private readonly Dictionary<int, TeamType> _entityTeams =
            new Dictionary<int, TeamType>(16);

        private readonly StringBuilder _builder = new StringBuilder(256);
        private Func<int, string> _entityLabelResolver;
        private ICombatLogService _combatLogService;
        private ISimulationClock _clock;
        private TeamType _localTeam = TeamType.Neutral;
        private int _localEntityId;
        private string _lastRenderedText = string.Empty;
        private float _nextRefreshTime;
        private float _nextLabelRefreshTime;

        private void Awake()
        {
            _entityLabelResolver = ResolveEntityLabel;
            AutoBindTextTargets();
        }

        public void BindTextTargets(TMP_Text tmpText, Text legacyText, GameObject feedRoot)
        {
            _tmpText = tmpText;
            _legacyText = legacyText;
            _feedRoot = feedRoot;
            AutoBindTextTargets();
            ConfigureTextTargets();
        }

        private void AutoBindTextTargets()
        {
            if (_tmpText == null)
                _tmpText = GetComponent<TMP_Text>();

            if (_legacyText == null)
                _legacyText = GetComponent<Text>();

            ConfigureTextTargets();
        }

        private void ConfigureTextTargets()
        {
            if (_tmpText != null)
                _tmpText.richText = _useRichText;

            if (_legacyText != null)
                _legacyText.supportRichText = _useRichText;
        }

        private void OnEnable()
        {
            ResolveServices();
            _nextRefreshTime = 0f;
            _nextLabelRefreshTime = 0f;
            _lastRenderedText = null;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + Mathf.Max(0.05f, _refreshIntervalSeconds);

            if (!ResolveServices())
            {
                RenderText(string.Empty);
                SetFeedVisible(false);
                return;
            }

            RefreshEntityLabelsIfDue(now);
            RebuildFeedText(_combatLogService.GetRecentEntries(), GetCurrentTick());

            string nextText = _builder.ToString();
            RenderText(nextText);
            SetFeedVisible(nextText.Length > 0);
        }

        private bool ResolveServices()
        {
            if (_combatLogService == null)
                ServiceProvider.TryGet(out _combatLogService);

            if (_clock == null)
                ServiceProvider.TryGet(out _clock);

            return _combatLogService != null;
        }

        private uint GetCurrentTick()
        {
            return _clock != null ? _clock.CurrentTick : 0u;
        }

        private void RebuildFeedText(IReadOnlyList<CombatLogEntry> entries, uint currentTick)
        {
            _builder.Length = 0;

            if (entries == null || entries.Count == 0)
                return;

            int written = 0;
            int scanned = 0;
            uint maxEntryAgeTicks = (uint)Mathf.Max(0, _maxEntryAgeTicks);

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (scanned >= _maxEntriesToScan || written >= _maxLines)
                    break;

                scanned++;
                CombatLogEntry entry = entries[i];

                if (!CombatLogHUDFormatter.ShouldDisplay(
                        entry,
                        _showAssists,
                        _showFatalDamage,
                        _showStatusEvents,
                        _showHeals))
                {
                    continue;
                }

                if (maxEntryAgeTicks > 0u &&
                    currentTick > entry.Tick &&
                    currentTick - entry.Tick > maxEntryAgeTicks)
                {
                    continue;
                }

                if (written > 0)
                    _builder.AppendLine();

                _builder.Append(CombatLogHUDFormatter.FormatFeedLine(
                    entry,
                    _entityLabelResolver,
                    ResolveEntityTeam,
                    _localTeam,
                    _localEntityId,
                    _includeTick,
                    _useLocalPerspectiveLabels,
                    _useRichText));

                written++;
            }
        }

        private void RefreshEntityLabelsIfDue(float now)
        {
            if (now < _nextLabelRefreshTime)
                return;

            _nextLabelRefreshTime = now + Mathf.Max(0.25f, _labelRefreshIntervalSeconds);

            bool foundLocalPlayer = false;

            BrawlerController[] brawlers = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < brawlers.Length; i++)
            {
                BrawlerController brawler = brawlers[i];
                if (brawler == null)
                    continue;

                int entityId = brawler.EntityID;
                if (entityId == 0)
                    continue;

                _entityLabels[entityId] = BuildBrawlerLabel(brawler);
                _entityTeams[entityId] = brawler.Team;

                if (!foundLocalPlayer && brawler.GetComponent<PlayerCommandSource>() != null)
                {
                    foundLocalPlayer = true;
                    _localTeam = brawler.Team;
                    _localEntityId = entityId;
                }
            }

            if (!foundLocalPlayer && _localEntityId == 0)
                _localTeam = TeamType.Neutral;
        }

        private static string BuildBrawlerLabel(BrawlerController brawler)
        {
            string brawlerName = null;

            if (brawler.Definition != null &&
                !string.IsNullOrWhiteSpace(brawler.Definition.BrawlerName))
            {
                brawlerName = brawler.Definition.BrawlerName;
            }

            return string.IsNullOrWhiteSpace(brawlerName)
                ? brawler.gameObject.name
                : brawlerName;
        }

        private string ResolveEntityLabel(int entityId)
        {
            return _entityLabels.TryGetValue(entityId, out string label)
                ? label
                : null;
        }

        private TeamType ResolveEntityTeam(int entityId)
        {
            return _entityTeams.TryGetValue(entityId, out TeamType team)
                ? team
                : TeamType.Neutral;
        }

        private void RenderText(string text)
        {
            if (string.Equals(_lastRenderedText, text, StringComparison.Ordinal))
                return;

            _lastRenderedText = text;

            if (_tmpText != null)
                _tmpText.text = text;
            else if (_legacyText != null)
                _legacyText.text = text;
        }

        private void SetFeedVisible(bool visible)
        {
            if (_feedRoot == null || _feedRoot == gameObject)
                return;

            if (_feedRoot.activeSelf != visible)
                _feedRoot.SetActive(visible);
        }
    }
}
