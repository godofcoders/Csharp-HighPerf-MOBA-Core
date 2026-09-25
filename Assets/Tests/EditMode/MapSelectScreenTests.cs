using System.Reflection;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Tests.EditMode
{
    public class MapSelectScreenTests
    {
        private GameObject _root;
        private MapCatalog _catalog;
        private MapDefinition _map;

        [TearDown]
        public void TearDown()
        {
            SceneSelection.SelectedMode = GameModeId.GemGrab;
            SceneSelection.SelectedShowdownVariant = ShowdownVariant.Solo;
            SceneSelection.SelectedMap = null;

            if (_root != null)
                Object.DestroyImmediate(_root);
            if (_map != null)
                Object.DestroyImmediate(_map);
            if (_catalog != null)
                Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void ShowdownTab_DisplaysSoloAndDuoSelectorAndUpdatesSelection()
        {
            _root = new GameObject("MapSelect", typeof(RectTransform));
            _catalog = ScriptableObject.CreateInstance<MapCatalog>();
            _map = ScriptableObject.CreateInstance<MapDefinition>();
            _map.DisplayName = "Test Yard";
            _map.SupportedModes = new[] { GameModeId.SoloShowdown };
            _catalog.Maps = new[] { _map };

            SceneSelection.SelectedMode = GameModeId.SoloShowdown;
            SceneSelection.SelectedShowdownVariant = ShowdownVariant.Solo;

            MapSelectScreen screen = _root.AddComponent<MapSelectScreen>();
            SetField(screen, "_catalog", _catalog);
            Invoke(screen, "Start");

            Transform selector = _root.transform.Find(
                "BrawlStyleMapSelect/Header/ShowdownVariantSelector");
            Assert.That(selector, Is.Not.Null);
            Assert.That(selector.gameObject.activeSelf, Is.True);

            Button duo = selector.Find("DuoVariantButton")?.GetComponent<Button>();
            Assert.That(duo, Is.Not.Null);
            duo.onClick.Invoke();

            Assert.AreEqual(ShowdownVariant.Duo, SceneSelection.SelectedShowdownVariant);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }
    }
}
