using System.Collections;
using System.Collections.Generic;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Runtime pre-match VS presentation. It uses the spawned match roster so
    /// random bot picks, selected brawler, and generated portraits all stay in
    /// sync without requiring a separate authored loading scene.
    /// </summary>
    public sealed class MatchVersusScreenOverlay : MonoBehaviour
    {
        private const string RootName = "MatchVersusScreen";

        [SerializeField] private float _visibleSeconds = 2.2f;
        [SerializeField] private float _fadeSeconds = 0.28f;
        [SerializeField] private int _maxRosterWaitFrames = 45;

        private static Font _runtimeFont;

        private CanvasGroup _group;
        private GameObject _root;

        private static Font RuntimeFont
        {
            get
            {
                if (_runtimeFont == null)
                    _runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _runtimeFont;
            }
        }

        private void Start()
        {
            StartCoroutine(PresentRoutine());
        }

        private IEnumerator PresentRoutine()
        {
            BuildShell();

            List<BrawlerController> blue = new List<BrawlerController>(3);
            List<BrawlerController> red = new List<BrawlerController>(3);
            List<BrawlerController> solo = new List<BrawlerController>(10);

            for (int i = 0; i < _maxRosterWaitFrames; i++)
            {
                CollectRoster(blue, red, solo);
                if (blue.Count + red.Count + solo.Count > 0)
                    break;

                yield return null;
            }

            PopulateRoster(blue, red, solo);

            yield return new WaitForSecondsRealtime(_visibleSeconds);

            float t = 0f;
            while (t < _fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                if (_group != null)
                    _group.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / _fadeSeconds));
                yield return null;
            }

            if (_root != null)
                Destroy(_root);
            Destroy(this);
        }

        private void BuildShell()
        {
            Transform existing = transform.Find(RootName);
            if (existing != null)
                Destroy(existing.gameObject);

            _root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
            _root.transform.SetParent(transform, false);
            RectTransform rootRect = _root.GetComponent<RectTransform>();
            Stretch(rootRect);

            _group = _root.GetComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = false;

            Sprite backgroundSprite = BrawlerGeneratedArtLibrary.LoadLoadingHomeBackground();
            Image background = CreateImage(_root.transform, "Background", Color.white);
            background.sprite = backgroundSprite != null ? backgroundSprite : RuntimeUISpriteUtility.GetSolidWhiteSprite();
            background.color = backgroundSprite != null ? Color.white : new Color(0.01f, 0.02f, 0.06f, 1f);
            background.raycastTarget = false;
            Stretch(background.rectTransform);

            Image veil = CreateImage(_root.transform, "DarkVeil", new Color(0.005f, 0.010f, 0.028f, 0.54f));
            veil.raycastTarget = false;
            Stretch(veil.rectTransform);

            Text versus = CreateText(
                _root.transform,
                "Versus",
                "VS",
                86,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);
            AddShadow(versus.gameObject, new Vector2(4f, -4f), 0.70f);
            Anchor(versus.rectTransform, new Vector2(0.36f, 0.42f), new Vector2(0.64f, 0.58f));

            _root.transform.SetAsLastSibling();
        }

        private void PopulateRoster(
            List<BrawlerController> blue,
            List<BrawlerController> red,
            List<BrawlerController> solo)
        {
            if (_root == null)
                return;

            if (blue.Count > 0 || red.Count > 0)
            {
                CreateTeamPanel(
                    "RedTeam",
                    "RED TEAM",
                    red,
                    new Vector2(0.19f, 0.64f),
                    new Vector2(0.94f, 0.88f),
                    new Color(1f, 0.12f, 0.18f, 0.88f),
                    true);

                CreateTeamPanel(
                    "BlueTeam",
                    "BLUE TEAM",
                    blue,
                    new Vector2(0.06f, 0.12f),
                    new Vector2(0.81f, 0.36f),
                    new Color(0.12f, 0.36f, 1f, 0.88f),
                    false);
                return;
            }

            CreateTeamPanel(
                "SoloRoster",
                "SOLO SHOWDOWN",
                solo,
                new Vector2(0.08f, 0.30f),
                new Vector2(0.92f, 0.62f),
                new Color(0.76f, 0.26f, 1f, 0.84f),
                false);
        }

        private void CreateTeamPanel(
            string name,
            string label,
            List<BrawlerController> roster,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color teamColor,
            bool alignRight)
        {
            GameObject panel = CreatePanel(_root.transform, name, new Color(teamColor.r, teamColor.g, teamColor.b, 0.82f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Anchor(panelRect, anchorMin, anchorMax);

            CreatePanel(panel.transform, "PanelShadow", new Color(0f, 0f, 0f, 0.30f));
            Transform shadow = panel.transform.Find("PanelShadow");
            if (shadow != null)
            {
                shadow.SetAsFirstSibling();
                RectTransform shadowRect = shadow.GetComponent<RectTransform>();
                shadowRect.anchorMin = Vector2.zero;
                shadowRect.anchorMax = Vector2.one;
                shadowRect.offsetMin = new Vector2(10f, -12f);
                shadowRect.offsetMax = new Vector2(10f, -12f);
            }

            Text header = CreateText(
                panel.transform,
                "Header",
                label,
                25,
                alignRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                Color.white,
                FontStyle.Bold);
            AddShadow(header.gameObject, new Vector2(2f, -2f), 0.55f);
            HeaderAnchor(header.rectTransform, alignRight);

            int count = Mathf.Min(roster != null ? roster.Count : 0, 5);
            if (count <= 0)
                return;

            float cardWidth = count > 3 ? 126f : 156f;
            float cardHeight = 172f;
            float spacing = count > 3 ? 10f : 18f;
            float totalWidth = count * cardWidth + (count - 1) * spacing;
            float startX = -totalWidth * 0.5f + cardWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float x = startX + i * (cardWidth + spacing);
                CreateBrawlerCard(
                    panel.transform,
                    roster[i],
                    new Vector2(x, -8f),
                    new Vector2(cardWidth, cardHeight),
                    teamColor);
            }
        }

        private void CreateBrawlerCard(
            Transform parent,
            BrawlerController brawler,
            Vector2 anchoredPosition,
            Vector2 size,
            Color teamColor)
        {
            GameObject card = CreatePanel(parent, $"Card_{ResolveName(brawler)}", new Color(0.004f, 0.010f, 0.030f, 0.94f));
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            CreatePanel(card.transform, "Accent", teamColor);
            RectTransform accentRect = card.transform.Find("Accent").GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0.92f);
            accentRect.anchorMax = Vector2.one;
            accentRect.offsetMin = Vector2.zero;
            accentRect.offsetMax = Vector2.zero;

            Image portrait = CreateImage(card.transform, "Portrait", Color.white);
            portrait.sprite = brawler != null ? BrawlerGeneratedArtLibrary.LoadPortrait(brawler.Definition) : null;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            Anchor(portrait.rectTransform, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.90f));

            if (portrait.sprite == null)
            {
                portrait.color = new Color(teamColor.r, teamColor.g, teamColor.b, 0.62f);
                Text initial = CreateText(
                    card.transform,
                    "Initial",
                    ResolveInitial(brawler),
                    40,
                    TextAnchor.MiddleCenter,
                    Color.white,
                    FontStyle.Bold);
                Anchor(initial.rectTransform, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.88f));
            }

            Text name = CreateText(
                card.transform,
                "Name",
                ResolveName(brawler).ToUpperInvariant(),
                16,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);
            AddShadow(name.gameObject, new Vector2(1.5f, -1.5f), 0.55f);
            Anchor(name.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.27f));

            int power = brawler != null && brawler.Definition != null
                ? PlayerBrawlerProgress.GetLevel(brawler.Definition)
                : PlayerBrawlerProgress.MinLevel;

            Text powerText = CreateText(
                card.transform,
                "Power",
                $"P{power}",
                14,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.82f, 0.20f, 1f),
                FontStyle.Bold);
            Anchor(powerText.rectTransform, new Vector2(0.70f, 0.76f), new Vector2(0.98f, 0.94f));

            if (brawler != null && brawler.GetComponent<PlayerCommandSource>() != null)
            {
                GameObject badge = CreatePanel(card.transform, "YouBadge", new Color(0.10f, 0.70f, 0.32f, 0.95f));
                Anchor(badge.GetComponent<RectTransform>(), new Vector2(0.04f, 0.76f), new Vector2(0.30f, 0.94f));

                Text you = CreateText(
                    badge.transform,
                    "Label",
                    "YOU",
                    12,
                    TextAnchor.MiddleCenter,
                    Color.white,
                    FontStyle.Bold);
                Anchor(you.rectTransform, Vector2.zero, Vector2.one);
            }
        }

        private static void CollectRoster(
            List<BrawlerController> blue,
            List<BrawlerController> red,
            List<BrawlerController> solo)
        {
            blue.Clear();
            red.Clear();
            solo.Clear();

            BrawlerController[] brawlers = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < brawlers.Length; i++)
            {
                BrawlerController brawler = brawlers[i];
                if (brawler == null || brawler.Definition == null)
                    continue;

                if (brawler.Team == TeamType.Blue)
                    blue.Add(brawler);
                else if (brawler.Team == TeamType.Red)
                    red.Add(brawler);
                else if (brawler.Team != TeamType.Neutral)
                    solo.Add(brawler);
            }

            SortRoster(blue);
            SortRoster(red);
            SortRoster(solo);
        }

        private static void SortRoster(List<BrawlerController> roster)
        {
            roster.Sort((a, b) =>
            {
                bool aLocal = a != null && a.GetComponent<PlayerCommandSource>() != null;
                bool bLocal = b != null && b.GetComponent<PlayerCommandSource>() != null;
                if (aLocal != bLocal)
                    return aLocal ? -1 : 1;

                return string.CompareOrdinal(ResolveName(a), ResolveName(b));
            });
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            Image image = panel.GetComponent<Image>();
            image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            TextAnchor alignment,
            Color color,
            FontStyle style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text label = textObject.GetComponent<Text>();
            label.text = text;
            label.font = RuntimeFont;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(fontSize * 0.58f));
            label.resizeTextMaxSize = fontSize;
            return label;
        }

        private static void AddShadow(GameObject target, Vector2 distance, float alpha)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void HeaderAnchor(RectTransform rect, bool alignRight)
        {
            rect.anchorMin = alignRight ? new Vector2(0.58f, 0.74f) : new Vector2(0.04f, 0.74f);
            rect.anchorMax = alignRight ? new Vector2(0.96f, 0.94f) : new Vector2(0.42f, 0.94f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            Anchor(rect, Vector2.zero, Vector2.one);
        }

        private static string ResolveName(BrawlerController brawler)
        {
            if (brawler == null || brawler.Definition == null)
                return "Brawler";

            string name = BrawlerGeneratedArtLibrary.ResolveDisplayName(brawler.Definition);
            return string.IsNullOrWhiteSpace(name) ? "Brawler" : name;
        }

        private static string ResolveInitial(BrawlerController brawler)
        {
            string name = ResolveName(brawler);
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }
    }
}
