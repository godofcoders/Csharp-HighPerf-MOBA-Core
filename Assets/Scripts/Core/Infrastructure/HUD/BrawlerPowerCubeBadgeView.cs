using MOBA.Core.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// World-space Solo Showdown power-cube counter. It follows the brawler's
    /// health-bar canvas, hides for dead/hidden enemies, and only shows once a
    /// brawler has at least one cube.
    /// </summary>
    public sealed class BrawlerPowerCubeBadgeView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BrawlerController _brawlerController;
        [SerializeField] private GameObject _badgeRoot;
        [SerializeField] private Image _cubeIcon;
        [SerializeField] private TMP_Text _countTmp;
        [SerializeField] private Text _countLegacy;
        [SerializeField] private Canvas _canvas;

        [Header("Display rules")]
        [SerializeField] private bool _hideWhileDead = true;

        [Header("Colors")]
        [SerializeField] private Color _cubeIconColor = new Color(0.58f, 1f, 0.16f);

        private Camera _camera;
        private BrawlerState _subscribedState;
        private int _lastCount = -1;

        public void Bind(
            BrawlerController brawlerController,
            GameObject badgeRoot,
            Image cubeIcon,
            TMP_Text countTmp,
            Text countLegacy,
            Canvas canvas)
        {
            UnsubscribeFromPowerCubeEvents();

            _brawlerController = brawlerController;
            _badgeRoot = badgeRoot;
            _cubeIcon = cubeIcon;
            _countTmp = countTmp;
            _countLegacy = countLegacy;
            _canvas = canvas;
            _lastCount = -1;

            ApplyStaticColors();
            SubscribeToPowerCubeEvents();
            RefreshImmediate();
        }

        private void Awake()
        {
            AutoBindReferences();
            ApplyStaticColors();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            ApplyStaticColors();
            SubscribeToPowerCubeEvents();
            RefreshImmediate();
        }

        private void OnDisable()
        {
            UnsubscribeFromPowerCubeEvents();
        }

        private void LateUpdate()
        {
            if (_brawlerController == null || _brawlerController.State == null)
            {
                SetVisible(false);
                return;
            }

            BrawlerState state = _brawlerController.State;
            SubscribeToPowerCubeEvents();

            if ((_hideWhileDead && state.IsDead) || IsHiddenFromLocalObserver(state))
            {
                SetVisible(false);
                return;
            }

            int count = ResolvePowerCubeCount(state);
            if (count <= 0)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            UpdateCountText(count);
            BillboardToCamera();
        }

        private void AutoBindReferences()
        {
            if (_brawlerController == null)
                _brawlerController = GetComponentInParent<BrawlerController>();

            if (_canvas == null)
                _canvas = GetComponent<Canvas>() ?? GetComponentInChildren<Canvas>();

            if (_badgeRoot == null)
            {
                Transform root = transform.Find("PowerCubeBadge");
                if (root != null)
                    _badgeRoot = root.gameObject;
            }

            if (_cubeIcon == null)
                _cubeIcon = FindChildImage("PowerCubeIcon");

            if (_countLegacy == null)
                _countLegacy = FindChildText("PowerCubeCount");
        }

        private Image FindChildImage(string childName)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.gameObject.name == childName)
                    return image;
            }

            return null;
        }

        private Text FindChildText(string childName)
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text != null && text.gameObject.name == childName)
                    return text;
            }

            return null;
        }

        private void SubscribeToPowerCubeEvents()
        {
            BrawlerState state = _brawlerController != null
                ? _brawlerController.State
                : null;

            if (_subscribedState == state)
                return;

            UnsubscribeFromPowerCubeEvents();

            if (state == null)
                return;

            _subscribedState = state;
            _subscribedState.OnPowerCubeCountChanged += HandlePowerCubeCountChanged;
        }

        private void UnsubscribeFromPowerCubeEvents()
        {
            if (_subscribedState == null)
                return;

            _subscribedState.OnPowerCubeCountChanged -= HandlePowerCubeCountChanged;
            _subscribedState = null;
        }

        private void HandlePowerCubeCountChanged(int count)
        {
            RefreshImmediate();
        }

        private void RefreshImmediate()
        {
            if (_brawlerController == null || _brawlerController.State == null)
            {
                SetVisible(false);
                return;
            }

            BrawlerState state = _brawlerController.State;
            if ((_hideWhileDead && state.IsDead) || IsHiddenFromLocalObserver(state))
            {
                SetVisible(false);
                return;
            }

            int count = ResolvePowerCubeCount(state);
            if (count <= 0)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            UpdateCountText(count);
        }

        private static int ResolvePowerCubeCount(BrawlerState state)
        {
            return state != null && state.PowerCubes != null
                ? state.PowerCubeCount
                : 0;
        }

        private bool IsHiddenFromLocalObserver(BrawlerState state)
        {
            if (_brawlerController == null ||
                state == null ||
                !BrawlerController.TryGetLocalObserverTeam(out TeamType observerTeam))
            {
                return false;
            }

            return state.IsHiddenTo(observerTeam);
        }

        private void UpdateCountText(int count)
        {
            if (count == _lastCount)
                return;

            string text = count.ToString();
            if (_countTmp != null)
                _countTmp.text = text;
            else if (_countLegacy != null)
                _countLegacy.text = text;

            _lastCount = count;
        }

        private void SetVisible(bool visible)
        {
            if (_badgeRoot != null && _badgeRoot.activeSelf != visible)
                _badgeRoot.SetActive(visible);

            if (!visible)
                _lastCount = -1;
        }

        private void ApplyStaticColors()
        {
            if (_cubeIcon != null)
                _cubeIcon.color = _cubeIconColor;
        }

        private void BillboardToCamera()
        {
            if (_canvas == null)
                return;

            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
                return;

            Transform canvasTransform = _canvas.transform;
            canvasTransform.rotation = Quaternion.LookRotation(
                canvasTransform.position - _camera.transform.position,
                Vector3.up);
        }
    }
}
