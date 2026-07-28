using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    public sealed class QuestsPanelView : MonoBehaviour
    {
        private const string RootName = "RuntimeQuestsPanel";

        private static readonly Color BackgroundColor = new Color(0.015f, 0.025f, 0.06f, 0.93f);
        private static readonly Color PanelColor = new Color(0.045f, 0.085f, 0.18f, 0.98f);
        private static readonly Color CardColor = new Color(0.075f, 0.12f, 0.24f, 0.96f);
        private static readonly Color CompletedCardColor = new Color(0.06f, 0.18f, 0.13f, 0.96f);
        private static readonly Color GoldColor = new Color(1f, 0.78f, 0.16f, 1f);
        private static readonly Color CyanColor = new Color(0.18f, 0.78f, 1f, 1f);
        private static readonly Color GreenColor = new Color(0.25f, 0.92f, 0.48f, 1f);

        private RectTransform _root;
        private Transform _cardContent;
        private TMP_Text _summaryText;
        private Button _closeButton;

        public void Show()
        {
            EnsureBuilt();
            Refresh();
            if (_root != null)
                _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_root != null)
                return;

            Transform existing = transform.Find(RootName);
            if (existing != null)
            {
                _root = existing as RectTransform;
                if (_root != null)
                    return;
            }

            GameObject rootObject = CreatePanel(transform, RootName, BackgroundColor);
            _root = rootObject.GetComponent<RectTransform>();
            Stretch(_root);
            _root.SetAsLastSibling();

            GameObject panel = CreatePanel(_root, "QuestWindow", PanelColor);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Anchor(panelRect, new Vector2(0.075f, 0.075f), new Vector2(0.925f, 0.925f), Vector2.zero, Vector2.zero);

            BuildHeader(panel.transform);
            BuildSummary(panel.transform);
            BuildQuestList(panel.transform);
            Hide();
        }

        private void BuildHeader(Transform parent)
        {
            GameObject header = CreatePanel(parent, "Header", new Color(0.02f, 0.05f, 0.13f, 0.98f));
            RectTransform headerRect = header.GetComponent<RectTransform>();
            Anchor(headerRect, new Vector2(0f, 0.86f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            TMP_Text title = CreateText(header.transform, "Title", "QUESTS", 42f, TextAlignmentOptions.Left, Color.white);
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0.035f, 0.38f), new Vector2(0.58f, 0.92f), Vector2.zero, Vector2.zero);

            TMP_Text subtitle = CreateText(
                header.transform,
                "Subtitle",
                "Track brawler mastery, combat goals, and mode objectives",
                17f,
                TextAlignmentOptions.Left,
                new Color(0.78f, 0.88f, 1f, 1f));
            Anchor(subtitle.rectTransform, new Vector2(0.037f, 0.12f), new Vector2(0.72f, 0.42f), Vector2.zero, Vector2.zero);

            _closeButton = CreateButton(header.transform, "CloseButton", "BACK", new Color(0.18f, 0.27f, 0.44f, 1f));
            Anchor(_closeButton.GetComponent<RectTransform>(), new Vector2(0.82f, 0.24f), new Vector2(0.965f, 0.78f), Vector2.zero, Vector2.zero);
            _closeButton.onClick.AddListener(Hide);
        }

        private void BuildSummary(Transform parent)
        {
            GameObject summary = CreatePanel(parent, "Summary", new Color(0.035f, 0.075f, 0.16f, 0.98f));
            RectTransform summaryRect = summary.GetComponent<RectTransform>();
            Anchor(summaryRect, new Vector2(0.035f, 0.75f), new Vector2(0.965f, 0.835f), Vector2.zero, Vector2.zero);

            _summaryText = CreateText(summary.transform, "SummaryText", "", 20f, TextAlignmentOptions.Center, Color.white);
            _summaryText.fontStyle = FontStyles.Bold;
            Anchor(_summaryText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-16f, -4f));
        }

        private void BuildQuestList(Transform parent)
        {
            GameObject listPanel = CreatePanel(parent, "QuestListPanel", new Color(0.018f, 0.038f, 0.095f, 0.98f));
            RectTransform listRect = listPanel.GetComponent<RectTransform>();
            Anchor(listRect, new Vector2(0.035f, 0.055f), new Vector2(0.965f, 0.725f), Vector2.zero, Vector2.zero);

            ScrollRect scrollRect = listPanel.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewport = CreatePanel(listPanel.transform, "Viewport", new Color(0f, 0f, 0f, 0f));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Anchor(viewportRect, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -14f));
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scrollRect.viewport = viewportRect;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            _cardContent = content.transform;
        }

        private void Refresh()
        {
            if (_cardContent == null)
                return;

            ClearChildren(_cardContent);

            QuestProgressSnapshot[] snapshots = PlayerQuestProgress.GetAllSnapshots();
            int completed = 0;
            for (int i = 0; i < snapshots.Length; i++)
            {
                if (snapshots[i].IsComplete)
                    completed++;
            }

            if (_summaryText != null)
            {
                int active = Mathf.Max(0, snapshots.Length - completed);
                _summaryText.text = $"ACTIVE {active}     COMPLETED {completed}     TOTAL {snapshots.Length}";
            }

            for (int i = 0; i < snapshots.Length; i++)
                CreateQuestCard(_cardContent, snapshots[i]);
        }

        private static void CreateQuestCard(Transform parent, QuestProgressSnapshot snapshot)
        {
            QuestDefinition quest = snapshot.Definition;
            if (quest == null)
                return;

            Color cardColor = snapshot.IsComplete ? CompletedCardColor : CardColor;
            GameObject card = CreatePanel(parent, "Quest_" + quest.Id, cardColor);
            LayoutElement layout = card.AddComponent<LayoutElement>();
            layout.preferredHeight = 134f;

            Image accent = CreatePanel(card.transform, "Accent", ResolveCategoryColor(quest.Category)).GetComponent<Image>();
            Anchor(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0.016f, 1f), Vector2.zero, Vector2.zero);

            TMP_Text category = CreateText(card.transform, "Category", quest.Category.ToUpperInvariant(), 13f, TextAlignmentOptions.Left, CyanColor);
            category.fontStyle = FontStyles.Bold;
            Anchor(category.rectTransform, new Vector2(0.035f, 0.72f), new Vector2(0.40f, 0.94f), Vector2.zero, Vector2.zero);

            TMP_Text title = CreateText(card.transform, "Title", quest.Title, 24f, TextAlignmentOptions.Left, Color.white);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 16f;
            title.fontSizeMax = 24f;
            Anchor(title.rectTransform, new Vector2(0.035f, 0.45f), new Vector2(0.62f, 0.74f), Vector2.zero, Vector2.zero);

            TMP_Text description = CreateText(card.transform, "Description", quest.Description, 15f, TextAlignmentOptions.Left, new Color(0.82f, 0.90f, 1f, 1f));
            Anchor(description.rectTransform, new Vector2(0.035f, 0.18f), new Vector2(0.66f, 0.44f), Vector2.zero, Vector2.zero);

            TMP_Text progress = CreateText(card.transform, "Progress", snapshot.ProgressLabel, 22f, TextAlignmentOptions.Right, snapshot.IsComplete ? GreenColor : GoldColor);
            progress.fontStyle = FontStyles.Bold;
            Anchor(progress.rectTransform, new Vector2(0.72f, 0.54f), new Vector2(0.88f, 0.88f), Vector2.zero, Vector2.zero);

            TMP_Text tick = CreateText(card.transform, "Tick", snapshot.IsComplete ? "✓" : "", 34f, TextAlignmentOptions.Center, GreenColor);
            tick.fontStyle = FontStyles.Bold;
            Anchor(tick.rectTransform, new Vector2(0.90f, 0.52f), new Vector2(0.97f, 0.88f), Vector2.zero, Vector2.zero);

            CreateProgressBar(card.transform, snapshot.NormalizedProgress, snapshot.IsComplete);
        }

        private static void CreateProgressBar(Transform parent, float normalized, bool complete)
        {
            GameObject back = CreatePanel(parent, "ProgressBack", new Color(0.012f, 0.022f, 0.05f, 0.95f));
            RectTransform backRect = back.GetComponent<RectTransform>();
            Anchor(backRect, new Vector2(0.70f, 0.24f), new Vector2(0.965f, 0.42f), Vector2.zero, Vector2.zero);

            Image fill = CreatePanel(back.transform, "ProgressFill", complete ? GreenColor : GoldColor).GetComponent<Image>();
            fill.raycastTarget = false;
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        private static Color ResolveCategoryColor(string category)
        {
            if (category == "Combat")
                return new Color(1f, 0.34f, 0.24f, 1f);

            if (category == "Mode Objective")
                return GoldColor;

            if (category == "Brawler Mastery")
                return new Color(0.55f, 0.34f, 1f, 1f);

            return CyanColor;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color color)
        {
            GameObject buttonObject = CreatePanel(parent, name, color);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();

            TMP_Text text = CreateText(buttonObject.transform, "Label", label, 18f, TextAlignmentOptions.Center, Color.white);
            text.fontStyle = FontStyles.Bold;
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));
            return button;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
            image.color = color;
            return panel;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            float size,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect)
        {
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }
    }
}
