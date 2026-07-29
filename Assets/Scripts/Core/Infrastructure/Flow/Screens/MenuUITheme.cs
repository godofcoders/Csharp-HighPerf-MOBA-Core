using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    public static class MenuUITheme
    {
        public const string ButtonAccentName = "RuntimeButtonAccent";

        public static readonly Color ScreenBackground = new Color(0.018f, 0.034f, 0.075f, 1f);
        public static readonly Color Header = new Color(0.012f, 0.028f, 0.07f, 0.92f);
        public static readonly Color ActionRail = new Color(0.012f, 0.024f, 0.058f, 0.88f);
        public static readonly Color Panel = new Color(0.045f, 0.085f, 0.18f, 0.98f);
        public static readonly Color PanelDark = new Color(0.014f, 0.030f, 0.075f, 0.98f);
        public static readonly Color PanelRaised = new Color(0.065f, 0.105f, 0.22f, 0.96f);
        public static readonly Color PreviewPanel = new Color(0.08f, 0.18f, 0.34f, 1f);

        public static readonly Color Gold = new Color(0.94f, 0.64f, 0.11f, 1f);
        public static readonly Color Cyan = new Color(0.18f, 0.78f, 1f, 1f);
        public static readonly Color PrimaryButton = new Color(0.94f, 0.64f, 0.11f, 1f);
        public static readonly Color SecondaryButton = new Color(0.12f, 0.36f, 0.72f, 1f);
        public static readonly Color QuestButton = new Color(0.38f, 0.22f, 0.78f, 1f);
        public static readonly Color PositiveButton = new Color(0.10f, 0.60f, 0.28f, 1f);
        public static readonly Color DisabledButton = new Color(0.16f, 0.18f, 0.24f, 0.88f);
        public static readonly Color ButtonAccent = new Color(1f, 1f, 1f, 0.18f);
        public static readonly Color TextMuted = new Color(0.78f, 0.88f, 1f, 1f);
        public static readonly Color TextSoft = new Color(0.88f, 0.94f, 1f, 1f);

        public static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            Image image = panel.GetComponent<Image>();
            image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
            image.color = color;
            image.raycastTarget = true;
            return panel;
        }

        public static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.enableWordWrapping = true;
            return label;
        }

        public static void StyleButton(Button button, string label, Color color, float fontSize = 17f)
        {
            if (button == null)
                return;

            Image image = button.targetGraphic as Image;
            if (image == null)
                image = button.GetComponent<Image>();

            if (image != null)
            {
                image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
                image.color = color;
                image.raycastTarget = true;
                button.targetGraphic = image;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.48f, 0.55f, 0.62f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            EnsureButtonAccent(button.transform);
            StyleButtonLabel(button, label, fontSize);
        }

        public static void EnsureButtonAccent(Transform buttonTransform)
        {
            if (buttonTransform == null)
                return;

            Transform accent = buttonTransform.Find(ButtonAccentName);
            if (accent == null)
                accent = CreatePanel(ButtonAccentName, buttonTransform, ButtonAccent).transform;

            RectTransform accentRect = accent.GetComponent<RectTransform>();
            Anchor(accentRect, new Vector2(0f, 0.78f), Vector2.one, Vector2.zero, Vector2.zero);
            accent.SetAsFirstSibling();
        }

        public static void StyleButtonLabel(Button button, string label, float fontSize)
        {
            if (button == null)
                return;

            TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
            Text legacy = button.GetComponentInChildren<Text>(true);
            if (tmp != null)
            {
                tmp.text = label;
                tmp.fontSize = fontSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = Mathf.Min(11f, fontSize);
                tmp.fontSizeMax = fontSize;
                Anchor(tmp.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 3f), new Vector2(-8f, -3f));
                EnsureShadow(tmp.gameObject);
                return;
            }

            if (legacy == null)
                return;

            legacy.text = label;
            legacy.fontSize = Mathf.RoundToInt(fontSize);
            legacy.fontStyle = FontStyle.Bold;
            legacy.alignment = TextAnchor.MiddleCenter;
            legacy.color = Color.white;
            legacy.raycastTarget = false;
            legacy.resizeTextForBestFit = true;
            legacy.resizeTextMinSize = 12;
            legacy.resizeTextMaxSize = Mathf.RoundToInt(fontSize);
            Anchor(legacy.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 3f), new Vector2(-8f, -3f));
            EnsureShadow(legacy.gameObject);
        }

        public static void EnsureShadow(GameObject target)
        {
            if (target == null || target.GetComponent<Shadow>() != null)
                return;

            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;
        }

        public static void Stretch(RectTransform rect)
        {
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        public static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
                return;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
