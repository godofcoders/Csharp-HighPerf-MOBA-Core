using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Visual binding for one brawler-select card. <see cref="Bind"/>
    /// populates the card's widgets from a BrawlerDefinition. The card
    /// prefab assigns one of these on its root + the various sub-widgets
    /// (portrait, name text, archetype text, HP/DMG values).
    ///
    /// All widget refs are optional — leave any unwired and that widget
    /// is silently skipped, so designers can build minimal or rich card
    /// variants without code changes.
    /// </summary>
    public class BrawlerCardView : MonoBehaviour
    {
        [Header("Visuals")]
        [Tooltip("Portrait image. Hidden when the brawler has no Portrait sprite.")]
        [SerializeField] private Image _portraitImage;

        [Tooltip("Optional team-color tint strip / border. Tinted by archetype color.")]
        [SerializeField] private Image _accentImage;

        [Header("Name + role")]
        [SerializeField] private TMP_Text _nameTextTmp;
        [SerializeField] private Text _nameTextLegacy;

        [SerializeField] private TMP_Text _archetypeTextTmp;
        [SerializeField] private Text _archetypeTextLegacy;

        [Header("Stat strip (optional)")]
        [SerializeField] private TMP_Text _healthValueTmp;
        [SerializeField] private Text _healthValueLegacy;

        [SerializeField] private TMP_Text _damageValueTmp;
        [SerializeField] private Text _damageValueLegacy;

        [Header("Archetype palette")]
        [Tooltip("Tint applied to the accent strip per archetype. Order: Tank, Assassin, Sniper, Support, Fighter, Controller, Artillery.")]
        [SerializeField] private Color[] _archetypeColors = new Color[]
        {
            new Color(0.85f, 0.30f, 0.30f), // Tank — red
            new Color(0.55f, 0.20f, 0.65f), // Assassin — purple
            new Color(0.20f, 0.55f, 0.85f), // Sniper — blue
            new Color(0.30f, 0.80f, 0.40f), // Support — green
            new Color(0.85f, 0.60f, 0.20f), // Fighter — orange
            new Color(0.40f, 0.60f, 0.80f), // Controller — steel blue
            new Color(0.85f, 0.45f, 0.20f), // Artillery — burnt orange
        };

        public void Bind(BrawlerDefinition def)
        {
            if (def == null) return;

            // Portrait — hide if no sprite assigned (avoids a default white quad).
            if (_portraitImage != null)
            {
                if (def.Portrait != null)
                {
                    _portraitImage.sprite = def.Portrait;
                    _portraitImage.enabled = true;
                }
                else
                {
                    _portraitImage.enabled = false;
                }
            }

            // Name — prefer BrawlerName field, fall back to asset name.
            string displayName = string.IsNullOrWhiteSpace(def.BrawlerName) ? def.name : def.BrawlerName;
            SetText(_nameTextTmp, _nameTextLegacy, displayName);

            // Archetype — uppercased label.
            SetText(_archetypeTextTmp, _archetypeTextLegacy, def.Archetype.ToString().ToUpperInvariant());

            // Accent tint by archetype.
            if (_accentImage != null)
                _accentImage.color = ResolveArchetypeColor(def.Archetype);

            // Stat strip — integer rendering for snappier glance.
            SetText(_healthValueTmp, _healthValueLegacy, Mathf.RoundToInt(def.BaseHealth).ToString());
            SetText(_damageValueTmp, _damageValueLegacy, Mathf.RoundToInt(def.BaseDamage).ToString());
        }

        private Color ResolveArchetypeColor(BrawlerArchetype a)
        {
            int idx = (int)a;
            if (_archetypeColors == null || idx < 0 || idx >= _archetypeColors.Length)
                return Color.gray;
            return _archetypeColors[idx];
        }

        private void SetText(TMP_Text tmp, Text legacy, string s)
        {
            if (tmp != null) tmp.text = s;
            else if (legacy != null) legacy.text = s;
        }
    }
}
