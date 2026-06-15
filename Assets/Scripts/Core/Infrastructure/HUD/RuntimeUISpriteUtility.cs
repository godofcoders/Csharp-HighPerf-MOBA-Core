using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class RuntimeUISpriteUtility
    {
        private static Texture2D _texture;
        private static Sprite _sprite;

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
    }
}
