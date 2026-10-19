using System.Collections.Generic;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    public sealed class CombatNumberView : MonoBehaviour
    {
        private const int PoolCapacity = 48;
        private readonly CombatNumberBuffer _buffer = new CombatNumberBuffer(PoolCapacity);
        private readonly List<BrawlerController> _brawlers = new List<BrawlerController>(12);
        private readonly Text[] _labels = new Text[PoolCapacity];
        private readonly BrawlerController[] _targets = new BrawlerController[PoolCapacity];
        private RectTransform _canvasRect;
        private BrawlerController _observer;
        private Camera _camera;
        private float _nextObserverSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Match" && FindObjectOfType<CombatNumberView>() == null)
                new GameObject("CombatNumberView").AddComponent<CombatNumberView>();
        }

        private void Awake()
        {
            var root = new GameObject("CombatNumbersCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 75;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)root.transform;
            for (int i = 0; i < PoolCapacity; i++)
            {
                var go = new GameObject("CombatNumber", typeof(RectTransform), typeof(Text), typeof(Outline));
                go.transform.SetParent(root.transform, false);
                var label = go.GetComponent<Text>();
                label.font = RuntimeUIFontUtility.GetDefaultFont();
                label.fontSize = 34;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                label.raycastTarget = false;
                label.rectTransform.sizeDelta = new Vector2(260f, 60f);
                go.GetComponent<Outline>().effectColor = new Color(0.02f, 0.03f, 0.06f, 0.9f);
                go.GetComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
                _labels[i] = label;
                go.SetActive(false);
            }
        }

        private void OnEnable() => BrawlerPresentationEventBus.OnEvent += OnPresentation;
        private void OnDisable()
        {
            BrawlerPresentationEventBus.OnEvent -= OnPresentation;
            _buffer.Clear();
            for (int i = 0; i < PoolCapacity; i++)
            {
                _targets[i] = null;
                if (_labels[i] != null) _labels[i].gameObject.SetActive(false);
            }
        }

        private void ResolveObserver()
        {
            if (_observer != null || Time.unscaledTime < _nextObserverSearch) return;
            _nextObserverSearch = Time.unscaledTime + 0.5f;
            CombatRegistry.GetBrawlersNonAlloc(_brawlers);
            for (int i = 0; i < _brawlers.Count; i++)
                if (_brawlers[i].GetComponent<PlayerCommandSource>() != null)
                {
                    _observer = _brawlers[i];
                    break;
                }
        }

        private bool CanShow(BrawlerController target)
        {
            return _observer != null && target != null && target.State != null &&
                !target.State.IsHiddenTo(_observer.Team);
        }

        private void OnPresentation(BrawlerPresentationEvent evt)
        {
            bool healing = evt.EventType == BrawlerPresentationEventType.Healed;
            if (!healing && evt.EventType != BrawlerPresentationEventType.DamageTaken) return;
            ResolveObserver();
            if (!CanShow(evt.Source)) return;
            int slot = _buffer.Add(evt.Source.EntityID, healing, evt.Value,
                evt.Position + Vector3.up * 2f, Time.time);
            if (slot < 0) return;
            _targets[slot] = evt.Source;
            float amount = Mathf.Ceil(_buffer.Get(slot).Amount);
            _labels[slot].text = healing ? $"+{amount:0}" : $"{amount:0}";
        }

        private void LateUpdate() => Render(Time.time);

        private void Render(float now)
        {
            if (_camera == null) _camera = Camera.main;
            for (int i = 0; i < PoolCapacity; i++)
            {
                Text label = _labels[i];
                bool visible = _buffer.IsActive(i, now) && _camera != null && CanShow(_targets[i]);
                Vector3 screen = visible ? _camera.WorldToScreenPoint(_buffer.Get(i).Position) : Vector3.zero;
                visible &= screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width &&
                    screen.y >= 0f && screen.y <= Screen.height;
                label.gameObject.SetActive(visible);
                if (!visible) continue;
                var entry = _buffer.Get(i);
                float t = Mathf.Clamp01((now - entry.StartedAt) / CombatNumberBuffer.LifetimeSeconds);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var position);
                float lane = entry.Healing ? -24f : 24f;
                label.rectTransform.anchoredPosition = position + new Vector2(lane, t * 65f);
                label.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.18f, 1f, Mathf.Min(1f, t * 5f));
                Color color = entry.Healing ? new Color(0.3f, 1f, 0.45f) :
                    _targets[i] == _observer ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.86f, 0.3f);
                color.a = 1f - Mathf.InverseLerp(0.55f, 1f, t);
                label.color = color;
            }
        }
    }
}
