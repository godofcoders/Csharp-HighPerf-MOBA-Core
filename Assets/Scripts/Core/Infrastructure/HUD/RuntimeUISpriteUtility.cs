using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class RuntimeUISpriteUtility
    {
        private static Texture2D _texture;
        private static Sprite _sprite;
        private static Texture2D _circleTexture;
        private static Sprite _circleSprite;

        public static Sprite GetSolidWhiteSprite()
        {
            if (_sprite != null)
                return _sprite;

            if (_texture == null)
            {
                _texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "RuntimeUISolidWhite",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                _texture.SetPixel(0, 0, Color.white);
                _texture.Apply(false, true);
            }

            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);

            _sprite.name = "RuntimeUISolidWhiteSprite";
            _sprite.hideFlags = HideFlags.HideAndDontSave;
            return _sprite;
        }

        public static Sprite GetSoftCircleSprite()
        {
            if (_circleSprite != null)
                return _circleSprite;

            const int size = 96;
            const float radius = (size - 2f) * 0.5f;
            Vector2 center = new Vector2((size - 1f) * 0.5f, (size - 1f) * 0.5f);

            if (_circleTexture == null)
            {
                _circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "RuntimeUISoftCircle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        float alpha = Mathf.Clamp01(radius + 1.25f - distance);
                        _circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                _circleTexture.Apply(false, true);
            }

            _circleSprite = Sprite.Create(
                _circleTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);

            _circleSprite.name = "RuntimeUISoftCircleSprite";
            _circleSprite.hideFlags = HideFlags.HideAndDontSave;
            return _circleSprite;
        }
    }
}
