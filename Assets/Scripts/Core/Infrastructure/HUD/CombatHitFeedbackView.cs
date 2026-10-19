using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    public sealed class CombatHitFeedbackView : MonoBehaviour
    {
        private static CombatHitFeedbackView _instance;
        private RectTransform _volumeButton;
        private const int MarkerCapacity = 16;
        private const float MarkerSeconds = 0.25f;
        private const float SoundInterval = 0.09f;
        private readonly Marker[] _markers = new Marker[MarkerCapacity];
        private RectTransform _canvasRect;
        private AudioSource _audio;
        private AudioClip _hitClip;
        private AudioClip _shieldClip;
        private Camera _camera;
        private float _nextHitSound;
        private float _nextShieldSound;
        private Text _volumeLabel;
        private GameObject _volumePanel;
        private bool _settingsDirty;
        private float _saveAt;

        private sealed class Marker
        {
            public RectTransform Root;
            public Image[] Arms;
            public Text Label;
            public BrawlerController Target;
            public BrawlerController Observer;
            public Vector3 Position;
            public float StartedAt;
            public bool Active;
            public bool Shield;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Match" && FindObjectOfType<CombatHitFeedbackView>() == null)
                new GameObject("CombatHitFeedbackView").AddComponent<CombatHitFeedbackView>();
        }

        private void Awake()
        {
            _instance = this;
            var canvas = new GameObject("CombatHitCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 76;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)canvas.transform;
            for (int i = 0; i < MarkerCapacity; i++)
            {
                var marker = new Marker { Root = Rect("HitMarker", canvas.transform, Vector2.zero, new Vector2(40f, 40f)),
                    Arms = new Image[4] };
                for (int arm = 0; arm < 4; arm++)
                {
                    var rect = Rect("Arm", marker.Root, Vector2.zero, new Vector2(3f, 9f));
                    marker.Arms[arm] = rect.gameObject.AddComponent<Image>();
                    marker.Arms[arm].raycastTarget = false;
                }
                marker.Label = TextLabel("ShieldLabel", marker.Root, new Vector2(0f, 30f), new Vector2(200f, 28f), 18);
                marker.Root.gameObject.SetActive(false);
                _markers[i] = marker;
            }
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _hitClip = CreateCue(false);
            _shieldClip = CreateCue(true);
            CreateVolumeControls(canvas.transform);
        }

        private void OnEnable()
        {
            DamageEventBus.OnDamageApplied += OnDamage;
            if (_canvasRect != null) _canvasRect.gameObject.SetActive(true);
        }
        private void OnDisable()
        {
            DamageEventBus.OnDamageApplied -= OnDamage;
            foreach (var marker in _markers)
                if (marker != null) { marker.Active = false; marker.Root.gameObject.SetActive(false); }
            if (_audio != null) _audio.Stop();
            if (_canvasRect != null) _canvasRect.gameObject.SetActive(false);
            SaveSettings();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            DestroyClip(_hitClip);
            DestroyClip(_shieldClip);
        }

        public static bool ShouldConfirm(DamageResultContext result)
        {
            var attacker = result.Damage.Attacker;
            var target = result.Damage.Target as BrawlerController;
            return attacker != null && attacker.GetComponent<PlayerCommandSource>() != null &&
                target != null && target.State != null && (!target.State.IsDead || result.WasFatal) &&
                TeamRelationshipUtility.AreEnemies(attacker.Team, target.Team) &&
                (result.HealthDamageApplied > 0f || result.ShieldAbsorbed > 0f) &&
                !target.State.IsHiddenTo(attacker.Team);
        }

        public static bool ShouldShowIncomingShield(DamageResultContext result)
        {
            var target = result.Damage.Target as BrawlerController;
            return result.ShieldAbsorbed > 0f && target != null && target.State != null &&
                (!target.State.IsDead || result.WasFatal) && target.GetComponent<PlayerCommandSource>() != null &&
                (result.Damage.Attacker == null || TeamRelationshipUtility.AreEnemies(target.Team, result.Damage.Attacker.Team));
        }

        private void OnDamage(DamageResultContext result)
        {
            bool outgoing = ShouldConfirm(result);
            if (!outgoing && !ShouldShowIncomingShield(result)) return;
            var target = (BrawlerController)result.Damage.Target;
            bool shield = result.ShieldAbsorbed > 0f;
            float now = Time.unscaledTime;
            int slot = -1;
            int free = -1;
            int oldest = 0;
            for (int i = 0; i < MarkerCapacity; i++)
            {
                if (_markers[i].Active && _markers[i].Target == target) { slot = i; break; }
                if ((!_markers[i].Active || now - _markers[i].StartedAt >= MarkerSeconds) && free < 0) free = i;
                if (_markers[i].StartedAt < _markers[oldest].StartedAt) oldest = i;
            }
            if (slot < 0) slot = free >= 0 ? free : oldest;
            var marker = _markers[slot];
            marker.Target = target;
            marker.Observer = outgoing ? result.Damage.Attacker : target;
            marker.Position = target.Position + Vector3.up * 1.3f;
            marker.StartedAt = now;
            marker.Active = true;
            marker.Shield = shield;
            marker.Label.text = shield ? (result.HealthDamageApplied > 0f ? "SHIELD + HIT" : "SHIELD") : string.Empty;
            for (int i = 0; i < 4; i++)
            {
                float angle = (45f + i * 90f + (shield ? 45f : 0f));
                marker.Arms[i].rectTransform.anchoredPosition =
                    new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 12f;
                marker.Arms[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
            float nextSound = shield ? _nextShieldSound : _nextHitSound;
            if (now < nextSound || CombatFeedbackSettings.EffectsVolume <= 0f) return;
            if (shield) _nextShieldSound = now + SoundInterval;
            else _nextHitSound = now + SoundInterval;
            if (Application.isPlaying)
                _audio.PlayOneShot(shield ? _shieldClip : _hitClip, CombatFeedbackSettings.EffectsVolume);
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            float now = Time.unscaledTime;
            for (int i = 0; i < MarkerCapacity; i++)
            {
                var marker = _markers[i];
                float t = (now - marker.StartedAt) / MarkerSeconds;
                if (marker.Active && t >= 1f) marker.Active = false;
                bool visible = marker.Active && _camera != null && marker.Target != null && marker.Observer != null &&
                    !marker.Target.State.IsHiddenTo(marker.Observer.Team);
                Vector3 screen = visible ? _camera.WorldToScreenPoint(marker.Position) : Vector3.zero;
                visible &= screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width &&
                    screen.y >= 0f && screen.y <= Screen.height;
                marker.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var point);
                marker.Root.anchoredPosition = point;
                Color color = marker.Shield ? new Color(0.25f, 0.9f, 1f) : Color.white;
                color.a = 1f - Mathf.Clamp01(t);
                foreach (var arm in marker.Arms) arm.color = color;
                marker.Label.color = color;
            }
            if (_settingsDirty && now >= _saveAt) SaveSettings();
        }

        private void CreateVolumeControls(Transform parent)
        {
            var toggle = Rect("EffectsVolumeButton", parent, new Vector2(-24f, -110f), new Vector2(122f, 36f));
            toggle.anchorMin = toggle.anchorMax = toggle.pivot = Vector2.one;
            _volumeButton = toggle;
            toggle.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.09f, 0.19f, 0.95f);
            var button = toggle.gameObject.AddComponent<Button>();
            _volumeLabel = TextLabel("VolumeLabel", toggle, Vector2.zero, new Vector2(122f, 36f), 16);
            _volumePanel = Rect("EffectsVolumePanel", parent, new Vector2(-24f, -152f), new Vector2(260f, 64f)).gameObject;
            var panelRect = (RectTransform)_volumePanel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = Vector2.one;
            _volumePanel.AddComponent<Image>().color = new Color(0.025f, 0.09f, 0.19f, 0.97f);
            var sliderRect = Rect("EffectsVolumeSlider", panelRect, Vector2.zero, new Vector2(220f, 28f));
            sliderRect.gameObject.AddComponent<Image>().color = Color.clear;
            var track = Rect("Track", sliderRect, Vector2.zero, new Vector2(220f, 5f));
            track.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.35f, 0.5f);
            var handleArea = Rect("HandleArea", sliderRect, Vector2.zero, new Vector2(200f, 28f));
            var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(18f, 28f));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.25f, 0.9f, 1f);
            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(CombatFeedbackSettings.EffectsVolume);
            slider.onValueChanged.AddListener(value =>
            {
                CombatFeedbackSettings.EffectsVolume = value;
                RefreshVolumeLabel();
                _settingsDirty = true;
                _saveAt = Time.unscaledTime + 0.5f;
            });
            button.onClick.AddListener(() => _volumePanel.SetActive(!_volumePanel.activeSelf));
            RefreshVolumeLabel();
            _volumePanel.SetActive(false);
        }

        public static bool IsPointerOverControls()
        {
            if (_instance == null || !_instance.isActiveAndEnabled || _instance._volumeButton == null) return false;
            Vector2 point;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                point = Touchscreen.current.primaryTouch.position.ReadValue();
            else if (Mouse.current != null) point = Mouse.current.position.ReadValue();
            else return false;
            return RectTransformUtility.RectangleContainsScreenPoint(_instance._volumeButton, point) ||
                (_instance._volumePanel.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)_instance._volumePanel.transform, point));
        }

        private void RefreshVolumeLabel() => _volumeLabel.text = CombatFeedbackSettings.EffectsVolume <= 0f
            ? "SFX MUTED" : $"SFX {Mathf.RoundToInt(CombatFeedbackSettings.EffectsVolume * 100f)}%";
        private void SaveSettings()
        {
            if (!_settingsDirty) return;
            PlayerPrefs.Save();
            _settingsDirty = false;
        }
        private void OnApplicationPause(bool paused) { if (paused) SaveSettings(); }
        private void OnApplicationQuit() => SaveSettings();

        private static RectTransform Rect(string name, Transform parent, Vector2 point, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = point;
            rect.sizeDelta = size;
            return rect;
        }
        private static Text TextLabel(string name, Transform parent, Vector2 point, Vector2 size, int fontSize)
        {
            var label = Rect(name, parent, point, size).gameObject.AddComponent<Text>();
            label.font = RuntimeUIFontUtility.GetDefaultFont();
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }
        private static AudioClip CreateCue(bool shield)
        {
            const int sampleRate = 22050;
            int count = Mathf.RoundToInt(sampleRate * (shield ? 0.075f : 0.045f));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * i / count) * Mathf.Exp(-t * 45f);
                float tone = shield ? Mathf.Sin(2f * Mathf.PI * 1350f * t) * 0.6f +
                    Mathf.Sin(2f * Mathf.PI * 2100f * t) * 0.4f : Mathf.Sin(2f * Mathf.PI * 750f * t);
                samples[i] = tone * envelope * 0.18f;
            }
            var clip = AudioClip.Create(shield ? "ShieldConfirm" : "HitConfirm", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        private static void DestroyClip(AudioClip clip)
        {
            if (clip == null) return;
            if (Application.isPlaying) Destroy(clip); else DestroyImmediate(clip);
        }
    }
}
