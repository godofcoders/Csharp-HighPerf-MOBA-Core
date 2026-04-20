using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Owns the brawler's **equipped loadout** — the configuration of what
    /// they brought into the match, the runtime objects that configuration
    /// spins up, and the passives currently installed against the brawler.
    ///
    /// Specifically this is the home of:
    ///   - CurrentPowerLevel (progression tier)
    ///   - RuntimeBuild  (which gadget / star power / hypercharge / gears are
    ///                    equipped, plus which slots are currently unlocked)
    ///   - RuntimeKit    (the resolved AbilityDefinition/IAbilityLogic pairs
    ///                    for main attack / super / gadget / hypercharge)
    ///   - EquippedHypercharge + HyperchargeModifierSource (the token used
    ///                    for tagging hypercharge-owned stat modifiers)
    ///   - EquippedPassives list (the definitions the player chose)
    ///   - _installedPassives list (the live runtime tuples created when
    ///                    those definitions were installed against a target)
    ///
    /// Cross-substate relationships the coordinator still owns:
    ///   - Health rebalancing after a stat change belongs to BrawlerState
    ///     because it touches Stats + fires OnHealthChanged.
    ///   - Pushing the equipped gadget's MaxCharges into Resources stays on
    ///     BrawlerState (that's the one documented seam between Loadout and
    ///     Resources).
    ///   - Tearing hypercharge-tagged stat modifiers off of MoveSpeed/Damage/
    ///     IncomingDamage when hypercharge ends stays on BrawlerState for the
    ///     same reason — Loadout owns the *token*, Stats owns the modifier
    ///     collections.
    ///   - GetCurrentSuperDefinition composes this POCO (RuntimeKit /
    ///     EquippedHypercharge) with Resources.Hypercharge.IsActive, so it
    ///     lives on the coordinator. The base-case lookup (RuntimeKit.Super
    ///     ?? Definition.SuperAbility) lives here as GetBaseSuperDefinition.
    ///
    /// POCO guarantees: no Unity types, no events, no singletons/services,
    /// no Debug.Log. The install/uninstall/tick methods take the coordinator
    /// as a method parameter because PassiveInstallContext requires it —
    /// that's a "give me my context" call, not a stored back-reference.
    /// </summary>
    public class BrawlerLoadout
    {
        /// <summary>
        /// One installed passive — the definition, the install context we
        /// built for it (so we can hand it back to Uninstall), and the
        /// runtime object the definition created. Kept private because
        /// nothing outside this class needs to see the pairing.
        /// </summary>
        private struct InstalledPassive
        {
            public PassiveDefinition Definition;
            public PassiveInstallContext Context;
            public IPassiveRuntime Runtime;
        }

        public int CurrentPowerLevel { get; private set; }

        public BrawlerRuntimeBuildState RuntimeBuild { get; }
        public BrawlerRuntimeKit RuntimeKit { get; }

        public HyperchargeDefinition EquippedHypercharge { get; private set; }

        /// <summary>
        /// Unique per-brawler token used to tag stat modifiers applied by
        /// the hypercharge system, so they can be removed wholesale when
        /// hypercharge ends. Object identity is all that matters here — two
        /// brawlers must not share the same token or their modifier cleanups
        /// would collide.
        /// </summary>
        public object HyperchargeModifierSource { get; } = new object();

        private readonly List<PassiveDefinition> _equippedPassives =
            new List<PassiveDefinition>(4);
        private readonly List<InstalledPassive> _installedPassives =
            new List<InstalledPassive>(4);

        public IReadOnlyList<PassiveDefinition> EquippedPassives => _equippedPassives;

        public BrawlerLoadout()
        {
            CurrentPowerLevel = 1;
            RuntimeBuild = new BrawlerRuntimeBuildState();
            RuntimeKit = new BrawlerRuntimeKit();
        }

        // ---------- Power level ----------

        /// <summary>Sets the brawler's progression tier. Clamps to a minimum of 1.</summary>
        public void SetPowerLevel(int powerLevel)
        {
            if (powerLevel < 1)
                powerLevel = 1;

            CurrentPowerLevel = powerLevel;
        }

        // ---------- Hypercharge slot ----------

        public void SetEquippedHypercharge(HyperchargeDefinition definition)
        {
            EquippedHypercharge = definition;
        }

        // ---------- Passives ----------

        /// <summary>
        /// Replaces the equipped-passive list with the supplied definitions
        /// (skipping nulls and duplicates). Does NOT install them — callers
        /// pair this with UninstallAll + InstallAll so the coordinator can
        /// bracket the whole swap with health-ratio preservation.
        /// </summary>
        public void SetEquippedPassives(IEnumerable<PassiveDefinition> definitions)
        {
            _equippedPassives.Clear();

            if (definitions == null)
                return;

            foreach (PassiveDefinition definition in definitions)
            {
                if (definition == null)
                    continue;

                if (!_equippedPassives.Contains(definition))
                    _equippedPassives.Add(definition);
            }
        }

        /// <summary>
        /// Installs every equipped passive against the given target, creating
        /// an install context + runtime object per definition. Appends to the
        /// installed list — call UninstallAll first if you're doing a swap.
        /// Takes the coordinator as a parameter because PassiveInstallContext
        /// needs (BrawlerState, BrawlerController, sourceToken).
        /// </summary>
        public void InstallAll(BrawlerState target, BrawlerController owner)
        {
            for (int i = 0; i < _equippedPassives.Count; i++)
            {
                PassiveDefinition definition = _equippedPassives[i];
                object sourceToken = new object();

                PassiveInstallContext context = new PassiveInstallContext(target, owner, sourceToken);
                definition.Install(context);

                IPassiveRuntime runtime = definition.CreateRuntime(context);
                runtime?.OnInstalled(target);

                _installedPassives.Add(new InstalledPassive
                {
                    Definition = definition,
                    Context = context,
                    Runtime = runtime
                });
            }
        }

        /// <summary>
        /// Uninstalls every currently-installed passive against the given
        /// target, in reverse install order, and clears the installed list.
        /// Equipped-definition list is NOT cleared — that's a SetEquippedPassives
        /// operation.
        /// </summary>
        public void UninstallAll(BrawlerState target)
        {
            for (int i = _installedPassives.Count - 1; i >= 0; i--)
            {
                InstalledPassive installed = _installedPassives[i];
                installed.Runtime?.OnUninstalled(target);
                installed.Definition?.Uninstall(installed.Context);
            }

            _installedPassives.Clear();
        }

        /// <summary>Per-tick update for every installed passive's runtime.</summary>
        public void TickPassives(BrawlerState target, uint currentTick)
        {
            for (int i = 0; i < _installedPassives.Count; i++)
            {
                _installedPassives[i].Runtime?.Tick(target, currentTick);
            }
        }

        // ---------- Slot unlock refresh ----------

        /// <summary>
        /// Pushes the "which loadout slots are currently unlocked" flags into
        /// RuntimeBuild, based on the brawler definition's BuildLayout and
        /// the current power level. No-op if the definition or its build
        /// layout is missing.
        /// </summary>
        public void RefreshRuntimeBuildUnlockState(BrawlerDefinition definition)
        {
            if (definition == null || definition.BuildLayout == null)
                return;

            RuntimeBuild.SetUnlockedState(
                definition.BuildLayout.IsSlotUnlocked("gear_1", CurrentPowerLevel),
                definition.BuildLayout.IsSlotUnlocked("gear_2", CurrentPowerLevel),
                definition.BuildLayout.IsSlotUnlocked("gadget_1", CurrentPowerLevel),
                definition.BuildLayout.IsSlotUnlocked("starpower_1", CurrentPowerLevel),
                definition.BuildLayout.IsSlotUnlocked("hypercharge_1", CurrentPowerLevel)
            );
        }

        /// <summary>
        /// Respawn-time cleanup for the *runtime* side of the loadout: wipes
        /// the equipped build slots (gadget / star power / hypercharge / gears)
        /// and the resolved ability kit (main attack / super / gadget /
        /// hypercharge definitions + logics), then recomputes the slot-unlock
        /// flags so the UI reflects the current power level.
        ///
        /// Does NOT touch _equippedPassives, _installedPassives, or
        /// EquippedHypercharge — those are considered "persistent loadout
        /// config" that survives respawn. Callers who want to uninstall
        /// passives on reset should call UninstallAll separately.
        ///
        /// IMPORTANT: after this runs, RuntimeKit.GadgetDefinition is null,
        /// so the brawler has no usable gadget until the caller re-applies
        /// the build. See the TODO in BrawlerState.Reset for the known gap
        /// on the respawn flow.
        /// </summary>
        public void ResetRuntimeState(BrawlerDefinition definition)
        {
            RuntimeBuild.Clear();
            RuntimeKit.Clear();
            RefreshRuntimeBuildUnlockState(definition);
        }

        // ---------- Slot-unlock convenience reads ----------

        public bool HasUnlockedGadgetSlot() => RuntimeBuild.IsGadgetSlotUnlocked;
        public bool HasUnlockedStarPowerSlot() => RuntimeBuild.IsStarPowerSlotUnlocked;
        public bool HasUnlockedHyperchargeSlot() => RuntimeBuild.IsHyperchargeSlotUnlocked;
        public bool HasAnyUnlockedGearSlot() =>
            RuntimeBuild.IsGearSlot1Unlocked || RuntimeBuild.IsGearSlot2Unlocked;

        // ---------- Current ability definition lookups ----------
        //
        // Each of these picks the runtime-configured value from RuntimeKit if
        // one was installed, otherwise falls back to the base brawler
        // definition. The hypercharge-overrides-super logic stays on
        // BrawlerState because it also needs Resources.Hypercharge.IsActive.

        public AbilityDefinition GetCurrentMainAttackDefinition(BrawlerDefinition definition)
        {
            return RuntimeKit?.MainAttackDefinition ?? definition?.MainAttack;
        }

        public AbilityDefinition GetBaseSuperDefinition(BrawlerDefinition definition)
        {
            return RuntimeKit?.SuperDefinition ?? definition?.SuperAbility;
        }

        public GadgetDefinition GetCurrentGadgetDefinition()
        {
            return RuntimeKit?.GadgetDefinition;
        }

        public HyperchargeDefinition GetCurrentHyperchargeDefinition()
        {
            return RuntimeKit?.HyperchargeDefinition ?? EquippedHypercharge;
        }
    }
}
