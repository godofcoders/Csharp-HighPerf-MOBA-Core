using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// Authoring-time data for a single super-charge source. A brawler
    /// definition carries an array of these so the meter can be fed from
    /// multiple sources at once (e.g. damage + auto-over-time + proximity).
    ///
    /// Each concrete subclass pairs with a concrete
    /// <c>SuperChargeSourceRuntime</c> via <see cref="CreateRuntime"/> —
    /// this mirrors the existing <c>PassiveDefinition.CreateRuntime</c>
    /// pattern so the loadout install/tick/uninstall loop looks familiar.
    ///
    /// Assets are authored per-brawler rather than shared, so each brawler
    /// can tune their own charge rate (e.g. damage-per-hit heroes like
    /// Byron charge faster than heavy hitters like Jessie).
    /// </summary>
    public abstract class SuperChargeSourceDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Designer-facing label; purely informational.")]
        public string SourceName;

        [TextArea]
        [Tooltip("What this source does and why it's on this brawler.")]
        public string Description;

        [Header("Global Gates")]
        [Tooltip("If false, this source is ignored at install time. Useful for temporarily disabling a source without deleting the asset.")]
        public bool Enabled = true;

        /// <summary>
        /// Spawns a fresh runtime instance configured from this definition's
        /// fields. Each call must return a new object — runtimes are never
        /// shared across brawlers.
        /// </summary>
        public abstract SuperChargeSourceRuntime CreateRuntime();
    }
}
