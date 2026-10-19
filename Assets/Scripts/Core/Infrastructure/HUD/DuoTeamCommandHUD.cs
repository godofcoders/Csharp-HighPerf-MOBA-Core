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
        private Text _activeCommand;
        private Text _cooldown;
        private Text _teammateMarker;
        private Text _targetMarker;
        private Image _durationFill;
        private RectTransform _canvasRect;
        private static readonly Color CommandBlue = new Color(0.025f, 0.09f, 0.19f, 0.96f);
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
            _canvasRect = (RectTransform)canvasObject.transform;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = Rect("DuoCommands", canvasObject.transform, new Vector2(0f, 190f), new Vector2(340f, 260f));
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0f);
            _toggle = MakeButton("CommandToggle", _root.transform, Vector2.zero,
                new Vector2(104f, 60f), "TEAM [T]", Toggle);
            _wheel = Rect("CommandWheel", _root.transform, Vector2.zero, new Vector2(340f, 260f));
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
            _activeCommand = MakeText(_root.transform, "ActiveCommand", new Vector2(0f, 170f),
                new Vector2(450f, 32f), 20);
            _cooldown = MakeText(_root.transform, "Cooldown", new Vector2(0f, -130f),
                new Vector2(360f, 30f), 16);
            var duration = Rect("CommandDuration", _root.transform, new Vector2(0f, 151f), new Vector2(240f, 4f));
            _durationFill = duration.AddComponent<Image>();
            _durationFill.color = new Color(0.16f, 0.78f, 1f);
            _durationFill.raycastTarget = false;
            _teammateMarker = MakeText(canvasObject.transform, "TeammateCommandMarker", Vector2.zero,
                new Vector2(260f, 38f), 19);
            _targetMarker = MakeText(canvasObject.transform, "CommandTargetMarker", Vector2.zero,
                new Vector2(220f, 38f), 19);
            _teammateMarker.gameObject.SetActive(false);
            _targetMarker.gameObject.SetActive(false);
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
                HideMarkers();
                _feedbackUntil = 0f;
                return;
            }
            // Keep the last world aim while the pointer moves onto UI controls.
            if (!_wheel.activeSelf && !IsPointerOverControls() && _source != null)
                _aimPoint = _source.CommandAimPoint;
            bool available = mode.TryGetLivingDuoTeammate(_player, out _);
            uint tick = ServiceProvider.TryGet<ISimulationClock>(out var clock) ? clock.CurrentTick : 0u;
            bool active = mode.TryGetIssuedDuoTeamCommand(_player, tick, out var teammate, out var directive);
            uint cooldownTicks = mode.GetDuoTeamCommandCooldownTicks(_player, tick);
            for (int i = 0; i < _commands.Length; i++)
            {
                _commands[i].interactable = available && cooldownTicks == 0u;
                _commands[i].targetGraphic.color = active && (int)directive.CommandType == i + 1
                    ? new Color(0.08f, 0.5f, 0.65f, 1f) : CommandBlue;
            }
            _cooldown.text = cooldownTicks > 0u
                ? $"READY IN {cooldownTicks * SimulationClock.TickDeltaTime:0.0}s" : string.Empty;
            _durationFill.gameObject.SetActive(active);
            _activeCommand.text = active
                ? $"{GetLabel(directive.CommandType)}  {(directive.ExpiresTick - tick) * SimulationClock.TickDeltaTime:0.0}s"
                : string.Empty;
            if (active)
            {
                float duration = Mathf.Max(1f, directive.ExpiresTick - directive.IssuedTick);
                _durationFill.rectTransform.localScale = new Vector3((directive.ExpiresTick - tick) / duration, 1f, 1f);
                bool acknowledging = tick - directive.IssuedTick < SimulationClock.SecondsToTicks(1.5f);
                _teammateMarker.text = acknowledging ? $"ON IT: {GetLabel(directive.CommandType)}"
                    : $"TEAM: {GetLabel(directive.CommandType)}";
                PositionMarker(_teammateMarker, teammate.Position + Vector3.up * 2.4f);
                bool targeted = ShowdownRules.DuoTeamCommandRequiresTarget(directive.CommandType);
                _targetMarker.text = $"▼ {GetLabel(directive.CommandType)}";
                _targetMarker.color = directive.CommandType == DuoTeamCommandType.Attack
                    ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.8f, 0.2f);
                if (targeted) PositionMarker(_targetMarker, directive.TargetPosition + Vector3.up * 1.2f);
                else _targetMarker.gameObject.SetActive(false);
            }
            else HideMarkers();
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

        private static string GetLabel(DuoTeamCommandType command)
        {
            int index = (int)command - 1;
            return index >= 0 && index < Labels.Length ? Labels[index] : string.Empty;
        }

        private void PositionMarker(Text marker, Vector3 worldPosition)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                marker.gameObject.SetActive(false);
                return;
            }
            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            bool visible = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width &&
                screen.y >= 0f && screen.y <= Screen.height;
            marker.gameObject.SetActive(visible);
            if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screen, null, out Vector2 local))
                marker.rectTransform.anchoredPosition = local;
        }

        private void HideMarkers()
        {
            _teammateMarker.gameObject.SetActive(false);
            _targetMarker.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_root != null) _root.SetActive(false);
            if (_teammateMarker != null) HideMarkers();
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
