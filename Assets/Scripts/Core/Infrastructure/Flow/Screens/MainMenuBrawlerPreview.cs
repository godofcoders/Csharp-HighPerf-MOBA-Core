using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
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
    public class MainMenuBrawlerPreview : MonoBehaviour, IDragHandler, IPointerClickHandler
    {
        [SerializeField] private Transform _modelAnchor;
        [SerializeField] private BrawlerDefinition _fallbackBrawler;
        [Min(0.01f)]
        [SerializeField] private float _rotateSensitivity = 0.4f;
        [Tooltip("Initial Y rotation of the spawned model.")]
        [SerializeField] private float _initialYaw = 180f;
        [Tooltip("Drag distance threshold (in screen pixels) above which a pointer-up is treated as a drag instead of a click. Prevents tap-to-open firing during a rotate.")]
        [Min(0f)]
        [SerializeField] private float _clickDragThreshold = 8f;

        [Header("Info display")]
        [SerializeField] private TMP_Text _nameTextTmp;
        [SerializeField] private Text _nameTextLegacy;
        [SerializeField] private TMP_Text _levelTextTmp;
        [SerializeField] private Text _levelTextLegacy;
        [Tooltip("Format string for the power-level display. {0} = level number.")]
        [SerializeField] private string _levelFormat = "Power Level {0}";

        private GameObject _spawned;
        private BrawlerDefinition _currentDef;
        private float _accumulatedDragMagnitude;

        private void Start() => Refresh();

        /// <summary>Re-spawn the showcased model. Call this after the
        /// player swaps brawlers (e.g. returns from BrawlerSelect).</summary>
        public void Refresh()
        {
            Debug.Log("[MMBP] Refresh start. SelectedBrawler=" + (SceneSelection.SelectedBrawler != null ? SceneSelection.SelectedBrawler.name : "null") + " fallback=" + (_fallbackBrawler != null ? _fallbackBrawler.name : "null"));

            BrawlerDefinition def = SceneSelection.SelectedBrawler ?? _fallbackBrawler;
            if (def == _currentDef && _spawned != null) { Debug.Log("[MMBP] same def + spawned, early-out"); return; }

            if (_spawned != null) Destroy(_spawned);
            _currentDef = def;

            if (def == null) { Debug.LogWarning("[MMBP] def null"); return; }
            if (def.ModelPrefab == null) { Debug.LogWarning("[MMBP] def.ModelPrefab null on " + def.name); return; }
            if (_modelAnchor == null) { Debug.LogWarning("[MMBP] _modelAnchor null"); return; }

            _spawned = Instantiate(def.ModelPrefab, _modelAnchor);
            _spawned.transform.localPosition = Vector3.zero;
            _spawned.transform.localRotation = Quaternion.Euler(0f, _initialYaw, 0f);
            Debug.Log("[MMBP] spawned " + _spawned.name + " under " + _modelAnchor.name + " at world " + _spawned.transform.position);

            StripGameplayComponents(_spawned);
            UpdateInfoText(def);
        }

        // The brawler model prefab usually carries gameplay components
        // (BrawlerController, AI controllers, colliders, rigidbodies). On
        // a MainMenu showcase we want pure visuals — strip everything that
        // isn't a renderer / animator / particle / transform.
        private void StripGameplayComponents(GameObject root)
        {
            MonoBehaviour[] scripts = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < scripts.Length; i++)
            {
                MonoBehaviour s = scripts[i];
                if (s == null) continue;
                // Whitelist a couple of pure-visual MonoBehaviours.
                if (s is Animator) continue;
                Destroy(s);
            }
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) Destroy(colliders[i]);
            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++) Destroy(bodies[i]);
        }

        private void UpdateInfoText(BrawlerDefinition def)
        {
            string nm = !string.IsNullOrWhiteSpace(def.BrawlerName) ? def.BrawlerName : def.name;
            if (_nameTextTmp != null) _nameTextTmp.text = nm;
            else if (_nameTextLegacy != null) _nameTextLegacy.text = nm;

            int level = PlayerBrawlerProgress.GetLevel(def);
            string levelStr = string.Format(_levelFormat, level);
            if (_levelTextTmp != null) _levelTextTmp.text = levelStr;
            else if (_levelTextLegacy != null) _levelTextLegacy.text = levelStr;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_spawned == null) return;
            float yaw = -eventData.delta.x * _rotateSensitivity;
            _spawned.transform.Rotate(0f, yaw, 0f, Space.World);
            _accumulatedDragMagnitude += eventData.delta.magnitude;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Distinguish click from drag: if the user moved more than the
            // threshold during the press, treat as a rotate, not a tap.
            if (_accumulatedDragMagnitude > _clickDragThreshold)
            {
                _accumulatedDragMagnitude = 0f;
                return;
            }
            _accumulatedDragMagnitude = 0f;

            // Open BrawlerSelect with the return-to-MainMenu flag set.
            SceneSelection.PickerReturnsToMainMenu = true;
            SceneFlow.Instance?.LoadScene(SceneId.BrawlerSelect);
        }
    }
}
