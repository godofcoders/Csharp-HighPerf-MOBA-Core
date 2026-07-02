using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public sealed class BackgroundMusicPlayer : MonoBehaviour
    {
        private const string DefaultClipResourcePath = "Audio/Music/retro_action_loop";

        private static BackgroundMusicPlayer _instance;

        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.28f;
        [SerializeField] private bool _playOnAwake = true;
        [SerializeField] private bool _persistAcrossScenes = true;

        private AudioSource _source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimePlayer()
        {
            if (_instance != null)
                return;

            BackgroundMusicPlayer authored = FindObjectOfType<BackgroundMusicPlayer>();
            if (authored != null)
            {
                authored.PlayDefaultIfNeeded();
                return;
            }

            GameObject root = new GameObject("BackgroundMusicPlayer");
            BackgroundMusicPlayer player = root.AddComponent<BackgroundMusicPlayer>();
            player.PlayDefaultIfNeeded();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (_persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            EnsureSource();

            if (_playOnAwake)
                PlayDefaultIfNeeded();
        }

        public void PlayDefaultIfNeeded()
        {
            EnsureSource();

            if (_clip == null)
                _clip = Resources.Load<AudioClip>(DefaultClipResourcePath);

            if (_clip == null)
            {
                Debug.LogWarning($"[Audio] Background music clip missing at Resources/{DefaultClipResourcePath}.");
                return;
            }

            if (_source.clip != _clip)
                _source.clip = _clip;

            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = _volume;

            if (!_source.isPlaying)
                _source.Play();
        }

        private void EnsureSource()
        {
            if (_source == null)
                _source = GetComponent<AudioSource>();

            if (_source == null)
                _source = gameObject.AddComponent<AudioSource>();
        }

        private void OnValidate()
        {
            if (_source != null)
                _source.volume = _volume;
        }
    }
}
