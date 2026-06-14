using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class VisibilitySystemTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>(4);
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>(4);

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            for (int i = _createdAssets.Count - 1; i >= 0; i--)
            {
                if (_createdAssets[i] != null)
                    Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdObjects.Clear();
            _createdAssets.Clear();
        }

        [Test]
        public void UpdateVisibility_PreservesStatusRevealWhenProximityClears()
        {
            MapData map = MakeMap(5, 5);
            map.BushGrid[1, 1] = true;

            BrawlerController hidden = CreateBrawler("Hidden", TeamType.Blue, new Vector3(1.5f, 0f, 1.5f));
            BrawlerController enemy = CreateBrawler("Enemy", TeamType.Red, new Vector3(4.5f, 0f, 4.5f));
            hidden.State.IsStatusRevealed = true;

            var brawlers = new List<BrawlerController> { hidden, enemy };

            VisibilitySystem.UpdateVisibility(brawlers, map);

            Assert.IsTrue(hidden.State.IsInBush);
            Assert.IsFalse(hidden.State.IsProximityRevealed);
            Assert.IsTrue(hidden.State.IsStatusRevealed);
            Assert.IsTrue(hidden.State.IsRevealed);
        }

        [Test]
        public void UpdateVisibility_DoesNotUseProximityRevealByDefault()
        {
            MapData map = MakeMap(5, 5);
            map.BushGrid[1, 1] = true;

            BrawlerController hidden = CreateBrawler("Hidden", TeamType.Blue, new Vector3(1.5f, 0f, 1.5f));
            BrawlerController enemy = CreateBrawler("Enemy", TeamType.Red, new Vector3(2.5f, 0f, 1.5f));

            var brawlers = new List<BrawlerController> { hidden, enemy };

            VisibilitySystem.UpdateVisibility(brawlers, map);

            Assert.IsTrue(hidden.State.IsInBush);
            Assert.IsFalse(hidden.State.IsProximityRevealed);
            Assert.IsFalse(hidden.State.IsRevealed);
        }

        [Test]
        public void UpdateVisibility_UsesNearbyEnemiesWhenProximityRevealIsEnabled()
        {
            MapData map = MakeMap(5, 5);
            map.BushGrid[1, 1] = true;

            BrawlerController hidden = CreateBrawler("Hidden", TeamType.Blue, new Vector3(1.5f, 0f, 1.5f));
            BrawlerController enemy = CreateBrawler("Enemy", TeamType.Red, new Vector3(2.5f, 0f, 1.5f));

            var brawlers = new List<BrawlerController> { hidden, enemy };
            var rules = new VisibilityRuleConfig(true, 2f);

            VisibilitySystem.UpdateVisibility(brawlers, map, rules);

            Assert.IsTrue(hidden.State.IsInBush);
            Assert.IsTrue(hidden.State.IsProximityRevealed);
            Assert.IsTrue(hidden.State.IsRevealed);

            enemy.transform.position = new Vector3(4.5f, 0f, 4.5f);

            VisibilitySystem.UpdateVisibility(brawlers, map, rules);

            Assert.IsFalse(hidden.State.IsProximityRevealed);
            Assert.IsFalse(hidden.State.IsRevealed);
        }

        [Test]
        public void UpdateVisibility_DoesNotProximityRevealOutsideBush()
        {
            MapData map = MakeMap(5, 5);

            BrawlerController visible = CreateBrawler("Visible", TeamType.Blue, new Vector3(1.5f, 0f, 1.5f));
            BrawlerController enemy = CreateBrawler("Enemy", TeamType.Red, new Vector3(2.5f, 0f, 1.5f));

            VisibilitySystem.UpdateVisibility(new List<BrawlerController> { visible, enemy }, map);

            Assert.IsFalse(visible.State.IsInBush);
            Assert.IsFalse(visible.State.IsProximityRevealed);
            Assert.IsFalse(visible.State.IsRevealed);
        }

        private MapData MakeMap(int width, int height)
        {
            MapData map = new MapData(width, height, 1f, Vector3.zero);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    map.WalkabilityGrid[x, y] = true;
            }

            return map;
        }

        private BrawlerController CreateBrawler(string name, TeamType team, Vector3 position)
        {
            GameObject gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            gameObject.transform.position = position;

            BrawlerDefinition definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            definition.BrawlerName = name;
            _createdAssets.Add(definition);

            BrawlerController brawler = gameObject.AddComponent<BrawlerController>();
            brawler.InitializeFromMatchmaking(definition, team);
            return brawler;
        }
    }
}
