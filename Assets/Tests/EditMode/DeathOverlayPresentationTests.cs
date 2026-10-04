using MOBA.Core.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MOBA.Tests.EditMode
{
    public class DeathOverlayPresentationTests
    {
        private GameObject _canvasObject;

        [TearDown]
        public void TearDown()
        {
            if (_canvasObject != null)
                Object.DestroyImmediate(_canvasObject);
        }

        [Test]
        public void SpectatingPresentation_MovesCardAndClearsBackdropThenRestoresDefaults()
        {
            _canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject root = CreateUiObject("DeathOverlay", _canvasObject.transform, true);
            Image backdrop = root.GetComponent<Image>();
            backdrop.color = new Color(0.01f, 0.02f, 0.04f, 0.68f);

            GameObject cardObject = CreateUiObject("DeathOverlayCard", root.transform, false);
            RectTransform card = cardObject.GetComponent<RectTransform>();
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(640f, 230f);

            GameObject accentObject = CreateUiObject("Accent", card, true);
            Image accent = accentObject.GetComponent<Image>();
            Color defaultAccent = new Color(1f, 0.22f, 0.28f, 0.94f);
            accent.color = defaultAccent;

            Text title = CreateText("Title", card, 50, new Vector2(0f, 62f), new Vector2(580f, 72f));
            Text countdown = CreateText("Countdown", card, 30, new Vector2(0f, 2f), new Vector2(580f, 54f));
            Text killer = CreateText("Killer", card, 22, new Vector2(0f, -54f), new Vector2(580f, 44f));

            GameObject controllerObject = new GameObject("DeathOverlayController");
            controllerObject.transform.SetParent(_canvasObject.transform, false);
            DeathOverlay overlay = controllerObject.AddComponent<DeathOverlay>();
            overlay.BindOverlay(
                root,
                null,
                title,
                null,
                countdown,
                null,
                killer,
                card,
                backdrop,
                accent);

            overlay.SetSpectatingPresentation(true);

            Assert.That(card.anchorMin, Is.EqualTo(Vector2.up));
            Assert.That(card.pivot, Is.EqualTo(Vector2.up));
            Assert.That(card.anchoredPosition.x, Is.GreaterThanOrEqualTo(28f));
            Assert.That(card.anchoredPosition.y, Is.LessThanOrEqualTo(-28f));
            Assert.That(card.sizeDelta.x, Is.LessThanOrEqualTo(480f));
            Assert.That(card.sizeDelta.y, Is.EqualTo(146f));
            Assert.That(backdrop.color.a, Is.EqualTo(0.08f).Within(0.001f));
            Assert.That(title.fontSize, Is.EqualTo(29));
            Assert.That(accent.color.b, Is.GreaterThan(accent.color.r));

            overlay.SetSpectatingPresentation(false);

            Assert.That(card.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(card.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(card.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(card.sizeDelta, Is.EqualTo(new Vector2(640f, 230f)));
            Assert.That(backdrop.color.a, Is.EqualTo(0.68f).Within(0.001f));
            Assert.That(title.fontSize, Is.EqualTo(50));
            Assert.That(accent.color, Is.EqualTo(defaultAccent));
        }

        private static GameObject CreateUiObject(string name, Transform parent, bool withImage)
        {
            GameObject gameObject = withImage
                ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                : new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            int fontSize,
            Vector2 position,
            Vector2 size)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.fontSize = fontSize;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            return text;
        }
    }
}
