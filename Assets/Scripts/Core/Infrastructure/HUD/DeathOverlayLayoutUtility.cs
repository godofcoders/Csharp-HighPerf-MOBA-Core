using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public readonly struct DeathOverlaySpectatorLayout
    {
        public DeathOverlaySpectatorLayout(
            Vector2 cardSize,
            Vector2 anchoredPosition,
            float backdropAlpha)
        {
            CardSize = cardSize;
            AnchoredPosition = anchoredPosition;
            BackdropAlpha = backdropAlpha;
        }

        public Vector2 CardSize { get; }
        public Vector2 AnchoredPosition { get; }
        public float BackdropAlpha { get; }
    }

    public static class DeathOverlayLayoutUtility
    {
        public const float SpectatorMargin = 28f;
        public const float SpectatorCardHeight = 146f;
        public const float SpectatorCardMaxWidth = 480f;
        public const float SpectatorBackdropAlpha = 0.08f;

        public static DeathOverlaySpectatorLayout ResolveSpectatorLayout(
            Rect safeArea,
            Vector2 screenSize,
            float canvasScaleFactor)
        {
            float scale = Mathf.Max(0.01f, canvasScaleFactor);
            float safeLeft = Mathf.Max(0f, safeArea.xMin) / scale;
            float safeTop = Mathf.Max(0f, screenSize.y - safeArea.yMax) / scale;
            float availableWidth = Mathf.Max(0f, safeArea.width / scale - SpectatorMargin * 2f);
            float cardWidth = Mathf.Min(SpectatorCardMaxWidth, availableWidth);

            return new DeathOverlaySpectatorLayout(
                new Vector2(cardWidth, SpectatorCardHeight),
                new Vector2(
                    safeLeft + SpectatorMargin,
                    -(safeTop + SpectatorMargin)),
                SpectatorBackdropAlpha);
        }
    }
}
