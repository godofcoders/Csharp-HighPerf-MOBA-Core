using System;

namespace MOBA.Core.Infrastructure
{
    /// <summary>Presentation-only gesture timing, independent of the gameplay random stream.</summary>
    public sealed class BrawlerIdleGesture
    {
        private readonly Random _random;
        private float _wait;
        private float _elapsed;
        private float _duration;
        private bool _playing;
        private bool _blocked;

        public int Variant { get; private set; } = -1;
        public float Weight { get; private set; }
        public float Direction { get; private set; } = 1f;

        public BrawlerIdleGesture(int seed)
        {
            _random = new Random(seed);
            _wait = Range(2f, 5f);
        }

        public void Tick(float deltaTime, bool blocked)
        {
            if (deltaTime <= 0f)
                return;

            if (blocked)
            {
                if (!_blocked)
                    _wait = Range(2f, 5f);
                _blocked = true;
                _playing = false;
                Weight = Math.Max(0f, Weight - deltaTime * 8f);
                return;
            }

            _blocked = false;
            if (!_playing)
            {
                Weight = Math.Max(0f, Weight - deltaTime * 8f);
                _wait -= deltaTime;
                if (_wait > 0f)
                    return;

                // Choose a different gesture each time without synchronizing other characters.
                Variant = Variant < 0 ? _random.Next(3) : (Variant + 1 + _random.Next(2)) % 3;
                Direction = _random.Next(2) == 0 ? -1f : 1f;
                _duration = Range(1.8f, 3f);
                _elapsed = 0f;
                _playing = true;
            }

            _elapsed += deltaTime;
            float fadeIn = Smooth01(_elapsed / 0.45f);
            float fadeOut = Smooth01((_duration - _elapsed) / 0.65f);
            Weight = fadeIn * fadeOut;
            if (_elapsed >= _duration)
            {
                _playing = false;
                Weight = 0f;
                _wait = Range(3f, 7f);
            }
        }

        private float Range(float minimum, float maximum)
        {
            return minimum + (maximum - minimum) * (float)_random.NextDouble();
        }

        private static float Smooth01(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            return value * value * (3f - 2f * value);
        }
    }
}
