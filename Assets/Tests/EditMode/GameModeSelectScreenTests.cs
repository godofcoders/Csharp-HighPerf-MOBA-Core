using System.Reflection;
using MOBA.Core.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Tests.EditMode
{
    public class GameModeSelectScreenTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void ShowdownButton_OpensVisibleSoloAndDuoChoices()
        {
            _root = new GameObject("GameModeSelect", typeof(RectTransform));
            _root.SetActive(false);

            Button gemGrab = CreateButton("GemGrabButton", "Gem Grab");
            Button knockout = CreateButton("KnockoutButton", "Knockout");
            Button back = CreateButton("BackButton", "Back");
            GameModeSelectScreen screen = _root.AddComponent<GameModeSelectScreen>();
            SetField(screen, "_gemGrabButton", gemGrab);
            SetField(screen, "_knockoutButton", knockout);
            SetField(screen, "_backButton", back);

            _root.SetActive(true);

            Button showdown = _root.transform.Find("ShowdownButton")?.GetComponent<Button>();
            Assert.That(showdown, Is.Not.Null);

            showdown.onClick.Invoke();

            Transform overlay = _root.transform.Find("ShowdownChoiceOverlay");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.gameObject.activeSelf, Is.True);
            Assert.That(overlay.Find("ShowdownChoicePanel/SoloChoiceButton"), Is.Not.Null);
            Assert.That(overlay.Find("ShowdownChoicePanel/DuoChoiceButton"), Is.Not.Null);
        }

        private Button CreateButton(string name, string label)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(_root.transform, false);

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            labelObject.GetComponent<Text>().text = label;
            return buttonObject.GetComponent<Button>();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
