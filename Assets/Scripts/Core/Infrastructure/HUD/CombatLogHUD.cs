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
        [SerializeField] private int _maxLines = 3;

        [Min(8)]
        [SerializeField] private int _maxEntriesToScan = 128;

        [SerializeField] private bool _killFeedOnly = true;
        [SerializeField] private bool _showAssists = false;
        [SerializeField] private bool _showHeals = false;
        [SerializeField] private bool _showFatalDamage = false;
        [SerializeField] private bool _showStatusEvents = false;
        [SerializeField] private bool _includeTick = false;
        [SerializeField] private bool _useRichText = true;
        [SerializeField] private bool _useLocalPerspectiveLabels = true;

        [Tooltip("0 keeps entries until the combat log service drops them. Non-zero hides older feed lines by simulation tick age.")]
        [Min(0)]
        [SerializeField] private int _maxEntryAgeTicks = 180;

        [Header("Graphic rows")]
        [SerializeField] private GameObject[] _rowRoots;
        [SerializeField] private Image[] _rowAccentImages;
        [SerializeField] private Image[] _rowBadgeImages;
        [SerializeField] private Text[] _rowIconTexts;
        [SerializeField] private Text[] _rowLineTexts;

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
        private readonly List<string> _renderedLines = new List<string>(5);
        private readonly List<TeamType> _renderedSourceTeams = new List<TeamType>(5);
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

        public void BindGraphicRows(
            GameObject[] rowRoots,
            Image[] rowAccentImages,
            Image[] rowBadgeImages,
            Text[] rowIconTexts,
            Text[] rowLineTexts)
        {
            _rowRoots = rowRoots;
            _rowAccentImages = rowAccentImages;
            _rowBadgeImages = rowBadgeImages;
            _rowIconTexts = rowIconTexts;
            _rowLineTexts = rowLineTexts;
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
            RenderGraphicRows();
            SetFeedVisible(_renderedLines.Count > 0);
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
            _renderedLines.Clear();
            _renderedSourceTeams.Clear();

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

                if (_killFeedOnly && entry.EventType != CombatLogEventType.Kill)
                    continue;

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

                string line = CombatLogHUDFormatter.FormatFeedLine(
                    entry,
                    _entityLabelResolver,
                    ResolveEntityTeam,
                    _localTeam,
                    _localEntityId,
                    _includeTick,
                    _useLocalPerspectiveLabels,
                    _useRichText);

                _builder.Append(line);
                _renderedLines.Add(line);
                _renderedSourceTeams.Add(ResolveEntityTeam(entry.SourceEntityId));

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
            if (HasGraphicRows())
                text = string.Empty;

            if (string.Equals(_lastRenderedText, text, StringComparison.Ordinal))
                return;

            _lastRenderedText = text;

            if (_tmpText != null)
                _tmpText.text = text;
            else if (_legacyText != null)
                _legacyText.text = text;
        }

        private void RenderGraphicRows()
        {
            if (!HasGraphicRows())
                return;

            int rowCount = _rowRoots != null ? _rowRoots.Length : 0;
            for (int i = 0; i < rowCount; i++)
            {
                GameObject row = _rowRoots[i];
                bool show = i < _renderedLines.Count;

                if (row != null && row.activeSelf != show)
                    row.SetActive(show);

                if (!show)
                    continue;

                Color rowColor = ResolveKillRowColor(_renderedSourceTeams[i]);

                if (_rowAccentImages != null && i < _rowAccentImages.Length && _rowAccentImages[i] != null)
                    _rowAccentImages[i].color = rowColor;

                if (_rowBadgeImages != null && i < _rowBadgeImages.Length && _rowBadgeImages[i] != null)
                    _rowBadgeImages[i].color = new Color(rowColor.r, rowColor.g, rowColor.b, 0.88f);

                if (_rowIconTexts != null && i < _rowIconTexts.Length && _rowIconTexts[i] != null)
                    _rowIconTexts[i].text = "KO";

                if (_rowLineTexts != null && i < _rowLineTexts.Length && _rowLineTexts[i] != null)
                    _rowLineTexts[i].text = _renderedLines[i];
            }
        }

        private bool HasGraphicRows()
        {
            return _rowRoots != null && _rowRoots.Length > 0;
        }

        private Color ResolveKillRowColor(TeamType sourceTeam)
        {
            if (_localTeam == TeamType.Neutral || sourceTeam == TeamType.Neutral)
                return new Color(1f, 0.80f, 0.22f, 0.96f);

            return TeamRelationshipUtility.AreAllies(sourceTeam, _localTeam)
                ? new Color(0.18f, 0.72f, 1f, 0.96f)
                : new Color(1f, 0.22f, 0.30f, 0.96f);
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
