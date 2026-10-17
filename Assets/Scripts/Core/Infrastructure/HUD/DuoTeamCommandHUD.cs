using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>Duo command controls shared by authored and fallback match HUDs.</summary>
    public sealed class DuoTeamCommandHUD : MonoBehaviour
    {
        private static DuoTeamCommandHUD _instance;
        private GameObject _root;
        private GameObject _wheel;
        private Button _toggle;
        private Button[] _commands;
        private Text _status;
        private PlayerCommandSource _source;
        private BrawlerController _player;
        private float _nextSearch;
        private float _feedbackUntil;
        private Vector3 _aimPoint;
        private static readonly string[] Labels = { "REGROUP", "ATTACK", "RETREAT", "TAKE CUBE" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Match") EnsureInstalled();
        }

        public static void EnsureInstalled()
        {
            if (_instance != null || FindObjectOfType<DuoTeamCommandHUD>() != null) return;
            new GameObject("DuoTeamCommandController").AddComponent<DuoTeamCommandHUD>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            var canvasObject = new GameObject("DuoCommandCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 85;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = Rect("DuoCommands", canvasObject.transform, new Vector2(0f, 190f), new Vector2(320f, 260f));
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0f);
            _toggle = MakeButton("CommandToggle", _root.transform, Vector2.zero,
                new Vector2(104f, 60f), "TEAM [T]", Toggle);
            _wheel = Rect("CommandWheel", _root.transform, Vector2.zero, new Vector2(320f, 260f));
            Vector2[] offsets = { new Vector2(0f, 84f), new Vector2(112f, 0f),
                new Vector2(0f, -84f), new Vector2(-112f, 0f) };
            _commands = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                _commands[i] = MakeButton(Labels[i], _wheel.transform, offsets[i],
                    new Vector2(108f, 56f), $"{i + 1}  {Labels[i]}", () => Issue(index));
            }
            _status = MakeText(_root.transform, "Status", new Vector2(0f, 130f),
                new Vector2(450f, 34f), 18);
            _wheel.SetActive(false);
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_player == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 0.5f;
                var sources = FindObjectsOfType<PlayerCommandSource>();
                for (int i = 0; i < sources.Length; i++)
                {
                    var player = sources[i].GetComponent<BrawlerController>();
                    if (player == null) continue;
                    _source = sources[i];
                    _player = player;
                    break;
                }
            }
            var mode = SoloShowdownMode.Instance;
            bool visible = mode != null && mode.IsDuoShowdown && _player != null &&
                _player.State != null && !_player.State.IsDead && MatchStateUtility.IsCombatResolutionOpen();
            _root.SetActive(visible);
            if (!visible)
            {
                _wheel.SetActive(false);
                return;
            }
            // Keep the last world aim while the pointer moves onto UI controls.
            if (!_wheel.activeSelf && !IsPointerOverControls() && _source != null)
                _aimPoint = _source.CommandAimPoint;
            bool available = mode.TryGetDuoCohesionAnchor(_player, out _);
            for (int i = 0; i < _commands.Length; i++) _commands[i].interactable = available;
            if (Time.unscaledTime >= _feedbackUntil)
                _status.text = available ? (_wheel.activeSelf ? "CHOOSE A TEAM COMMAND" : string.Empty)
                    : "TEAMMATE UNAVAILABLE";
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.tKey.wasPressedThisFrame) Toggle();
            if (!_wheel.activeSelf) return;
            if (keyboard.escapeKey.wasPressedThisFrame) _wheel.SetActive(false);
            else if (keyboard.digit1Key.wasPressedThisFrame) Issue(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) Issue(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) Issue(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) Issue(3);
        }

        private void Toggle()
        {
            if (!_root.activeSelf || _source == null) return;
            bool open = !_wheel.activeSelf;
            _wheel.SetActive(open);
        }

        private void Issue(int index)
        {
            if (_source == null || !_root.activeSelf) return;
            var result = _source.IssueDuoTeamCommand((DuoTeamCommandType)(index + 1), _aimPoint);
            _status.text = result == DuoTeamCommandIssueResult.Accepted ? $"{Labels[index]} SENT" :
                result == DuoTeamCommandIssueResult.Cooldown ? "COMMAND COOLING DOWN" :
                result == DuoTeamCommandIssueResult.InvalidTarget ? "NO AVAILABLE TARGET" : "TEAMMATE UNAVAILABLE";
            _feedbackUntil = Time.unscaledTime + 1.5f;
            _wheel.SetActive(false);
        }

        public static bool IsPointerOverControls()
        {
            if (_instance == null || !_instance._root.activeInHierarchy) return false;
            Vector2 point;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                point = Touchscreen.current.primaryTouch.position.ReadValue();
            else if (Mouse.current != null) point = Mouse.current.position.ReadValue();
            else return false;
            RectTransform area = (RectTransform)(_instance._wheel.activeSelf
                ? _instance._wheel.transform : _instance._toggle.transform);
            return RectTransformUtility.RectangleContainsScreenPoint(area, point);
        }

        private static GameObject Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }

        private static Button MakeButton(string name, Transform parent, Vector2 position,
            Vector2 size, string label, UnityEngine.Events.UnityAction clicked)
        {
            var go = Rect(name, parent, position, size);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.025f, 0.09f, 0.19f, 0.96f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.8f, 1f);
            colors.pressedColor = new Color(0.2f, 0.6f, 0.9f);
            button.colors = colors;
            button.onClick.AddListener(clicked);
            MakeText(go.transform, "Label", Vector2.zero, size, 16).text = label;
            return button;
        }

        private static Text MakeText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Rect(name, parent, position, size).AddComponent<Text>();
            text.font = RuntimeUIFontUtility.GetDefaultFont();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }
    }
}
