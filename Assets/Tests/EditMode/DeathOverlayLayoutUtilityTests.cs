using MOBA.Core.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class DeathOverlayLayoutUtilityTests
    {
        [Test]
        public void ResolveSpectatorLayout_UsesCompactUpperLeftPlacement()
        {
            DeathOverlaySpectatorLayout layout =
                DeathOverlayLayoutUtility.ResolveSpectatorLayout(
                    new Rect(0f, 0f, 1920f, 1080f),
                    new Vector2(1920f, 1080f),
                    1f);

            Assert.That(layout.CardSize, Is.EqualTo(new Vector2(480f, 146f)));
            Assert.That(layout.AnchoredPosition, Is.EqualTo(new Vector2(28f, -28f)));
            Assert.That(layout.BackdropAlpha, Is.LessThan(0.1f));
        }

        [Test]
        public void ResolveSpectatorLayout_AccountsForSafeAreaAndCanvasScale()
        {
            DeathOverlaySpectatorLayout layout =
                DeathOverlayLayoutUtility.ResolveSpectatorLayout(
                    new Rect(100f, 40f, 1720f, 1000f),
                    new Vector2(1920f, 1080f),
                    2f);

            Assert.That(layout.AnchoredPosition, Is.EqualTo(new Vector2(78f, -48f)));
            Assert.That(layout.CardSize.x, Is.EqualTo(480f));
        }

        [Test]
        public void ResolveSpectatorLayout_ShrinksCardOnNarrowScreens()
        {
            DeathOverlaySpectatorLayout layout =
                DeathOverlayLayoutUtility.ResolveSpectatorLayout(
                    new Rect(0f, 0f, 360f, 720f),
                    new Vector2(360f, 720f),
                    1f);

            Assert.That(layout.CardSize.x, Is.EqualTo(304f));
            Assert.That(layout.AnchoredPosition.x, Is.EqualTo(28f));
        }
    }
}
