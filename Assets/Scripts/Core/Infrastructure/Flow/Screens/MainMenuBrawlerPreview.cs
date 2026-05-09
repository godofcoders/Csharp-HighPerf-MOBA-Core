using UnityEngine;
using UnityEngine.EventSystems;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// MainMenu 3D brawler showcase. Instantiates the current
    /// SceneSelection.SelectedBrawler's ModelPrefab at an anchor and
    /// supports drag-to-rotate around Y. Falls back to a designer-assigned
    /// _fallbackBrawler when no selection exists yet.
    ///
    /// Setup:
    ///   1. Add a Transform on stage where the model should appear.
    ///   2. Add this component anywhere; assign _modelAnchor + _fallbackBrawler.
    ///   3. Drop an Image (full-screen, transparent) over the showcase area
    ///      that has this component as its drag-event handler — or attach
    ///      this directly to the Image.
    /// </summary>
    public class MainMenuBrawlerPreview : MonoBehaviour, IDragHandler
    {
        [SerializeField] private Transform _modelAnchor;
        [SerializeField] private BrawlerDefinition _fallbackBrawler;
        [Min(0.01f)]
        [SerializeField] private float _rotateSensitivity = 0.4f;
        [Tooltip("Initial Y rotation of the spawned model.")]
        [SerializeField] private float _initialYaw = 180f;

        private GameObject _spawned;
        private BrawlerDefinition _currentDef;

        private void Start() => Refresh();

        /// <summary>Re-spawn the showcased model. Call this after the
        /// player swaps brawlers (e.g. returns from BrawlerSelect).</summary>
        public void Refresh()
        {
            BrawlerDefinition def = SceneSelection.SelectedBrawler ?? _fallbackBrawler;
            if (def == _currentDef && _spawned != null) return;

            if (_spawned != null) Destroy(_spawned);
            _currentDef = def;

            if (def == null || def.ModelPrefab == null || _modelAnchor == null) return;

            _spawned = Instantiate(def.ModelPrefab, _modelAnchor);
            _spawned.transform.localPosition = Vector3.zero;
            _spawned.transform.localRotation = Quaternion.Euler(0f, _initialYaw, 0f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_spawned == null) return;
            float yaw = -eventData.delta.x * _rotateSensitivity;
            _spawned.transform.Rotate(0f, yaw, 0f, Space.World);
        }
    }
}
