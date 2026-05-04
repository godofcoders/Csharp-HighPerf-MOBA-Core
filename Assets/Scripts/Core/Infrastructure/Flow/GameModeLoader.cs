using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Match-scene MonoBehaviour that swaps in the right game-mode prefab
    /// at scene start.
    ///
    /// Flow:
    ///   1. On Awake, read SceneSelection.SelectedMode (set by the
    ///      GameModeSelect screen).
    ///   2. Look up the matching GameModeDefinition in the catalog.
    ///   3. Instantiate its ModePrefab as a child of <see cref="_modeRoot"/>
    ///      (or this transform if _modeRoot is null).
    ///   4. The instantiated prefab brings everything mode-specific:
    ///      the coordinator (e.g. GemGrabMode), spawners, win conditions.
    ///
    /// Falls back to the inspector-assigned _fallbackMode if SceneSelection
    /// hasn't been set (e.g. launching the Match scene directly for
    /// testing without going through GameModeSelect).
    ///
    /// Setup:
    ///   1. Add GameModeLoader to a GameObject in the Match scene.
    ///   2. Assign _catalog (the GameModeCatalog asset).
    ///   3. Optionally assign _modeRoot (a child GameObject acting as the
    ///      slot — keeps the spawned mode prefab tidily parented).
    ///   4. Assign _fallbackMode for direct-test launches.
    ///   5. REMOVE the existing GemGrabMode + GemSpawner GameObjects
    ///      from the scene — those now live inside Mode_GemGrab.prefab
    ///      and are spawned by this loader.
    /// </summary>
    public class GameModeLoader : MonoBehaviour
    {
        [Header("Sources")]
        [Tooltip("Catalog listing every available GameModeDefinition.")]
        [SerializeField] private GameModeCatalog _catalog;

        [Tooltip("Slot under which the spawned mode prefab will be parented. If null, this GameObject is used.")]
        [SerializeField] private Transform _modeRoot;

        [Tooltip("Fallback mode used when SceneSelection.SelectedMode hasn't been set (direct Match-scene launches).")]
        [SerializeField] private GameModeDefinition _fallbackMode;

        public GameObject SpawnedModeInstance { get; private set; }

        private void Awake()
        {
            GameModeDefinition def = ResolveMode();
            if (def == null)
            {
                Debug.LogError("[GameModeLoader] Could not resolve a GameModeDefinition. Selected: " + SceneSelection.SelectedMode + ". Catalog has nothing matching, and no fallback assigned.");
                return;
            }

            if (def.ModePrefab == null)
            {
                Debug.LogError($"[GameModeLoader] GameModeDefinition '{def.name}' has no ModePrefab assigned.");
                return;
            }

            Transform parent = _modeRoot != null ? _modeRoot : transform;
            SpawnedModeInstance = Instantiate(def.ModePrefab, parent);
            SpawnedModeInstance.name = def.ModePrefab.name; // strip "(Clone)" for cleaner hierarchy
        }

        private GameModeDefinition ResolveMode()
        {
            if (_catalog != null)
            {
                GameModeDefinition fromCatalog = _catalog.Find(SceneSelection.SelectedMode);
                if (fromCatalog != null) return fromCatalog;
            }
            return _fallbackMode;
        }
    }
}
