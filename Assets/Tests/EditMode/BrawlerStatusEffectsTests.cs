using System.Collections.Generic;
using NUnit.Framework;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerStatusEffects — the active-status list owner.
    //
    // Status effects are the single biggest source of "ghost bug" reports in
    // live MOBAs: stuns that overstay, slows that stack wrong, debuffs that
    // never expire because the wrong effect got removed. This suite locks in
    // the contract on the half of that pipeline that lives here:
    //   - the Active list management
    //   - HasStatus + the convenience wrappers
    //   - TickAndCollectExpired's caller-owned-buffer protocol
    //   - the reverse-iteration removal safety inside the tick loop
    //
    // What this suite does NOT test: the effect's own Tick/Apply/Remove side
    // effects (those live in concrete IStatusEffectInstance implementations
    // like SlowEffect, BurnEffect, etc.) and the event-bus / combat-log
    // notifications BrawlerState fires after walking the removedOut buffer
    // (those are coordinator-level and want a different test fixture).
    public class BrawlerStatusEffectsTests
    {
        // ---------- Test fakes ----------

        // A controllable fake effect: caller decides type, when it expires,
        // and the fake records how many times each lifecycle hook was called.
        private sealed class FakeStatusEffect : IStatusEffectInstance
        {
            public FakeStatusEffect(StatusEffectType type, uint startTick, uint endTick)
            {
                Type = type;
                StartTick = startTick;
                EndTick = endTick;
            }

            public StatusEffectType Type { get; }
            public uint StartTick { get; }
            public uint EndTick { get; }
            public int TickCallCount { get; private set; }
            public int RemoveCallCount { get; private set; }
            public int ApplyCallCount { get; private set; }

            public void Apply(IStatusTarget target, uint currentTick) { ApplyCallCount++; }
            public void Tick(IStatusTarget target, uint currentTick) { TickCallCount++; }
            public void Remove(IStatusTarget target, uint currentTick) { RemoveCallCount++; }

            public bool CanMerge(StatusEffectContext context) => false;
            public void Merge(StatusEffectContext context, uint currentTick) { }

            // Half-open semantics: effect is expired the tick its EndTick is reached.
            public bool IsExpired(uint currentTick) => currentTick >= EndTick;
        }

        // Minimal IStatusTarget fake. BrawlerStatusEffects itself never calls
        // any of these methods — only the effect's Tick/Remove might — so a
        // no-op sink is enough to satisfy the type system.
        private sealed class FakeStatusTarget : IStatusTarget
        {
            public int EntityID => 1;
            public TeamType Team => TeamType.Blue;
            public bool IsDead => false;
            public bool CanReceiveStatusEffects() => true;
            public List<IStatusEffectInstance> ActiveStatusEffects { get; } = new List<IStatusEffectInstance>();

            public void AddIncomingMovementModifier(MovementModifier modifier) { }
            public void RemoveIncomingMovementModifiersFromSource(object source) { }
            public void AddIncomingDamageModifier(DamageModifier modifier) { }
            public void RemoveIncomingDamageModifiersFromSource(object source) { }
            public bool HasStatus(StatusEffectType type) => false;
        }

        // ---------- Construction ----------

        [Test]
        public void Construction_StartsEmpty()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();

            Assert.IsNotNull(effects.Active);
            Assert.AreEqual(0, effects.Active.Count);
        }

        // ---------- HasStatus ----------

        [Test]
        public void HasStatus_ReturnsFalse_WhenNoEffects()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();

            Assert.IsFalse(effects.HasStatus(StatusEffectType.Stun));
        }

        [Test]
        public void HasStatus_ReturnsTrue_AfterMatchingEffectAdded()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.Stun, 0, 100));

            Assert.IsTrue(effects.HasStatus(StatusEffectType.Stun));
        }

        [Test]
        public void HasStatus_ReturnsFalse_ForUnmatchedType()
        {
            // We have a Slow but not a Stun. Don't false-positive.
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.Slow, 0, 100));

            Assert.IsFalse(effects.HasStatus(StatusEffectType.Stun));
        }

        // ---------- Convenience wrappers ----------
        // These look trivial but they're the public read API for every
        // ability cast site ("can I fire?", "can I gadget?"). Lock in the
        // mapping so a typo in StatusEffectType changes can't silently wire
        // the wrong check.

        [Test]
        public void HasSilence_DelegatesToHasStatus_Silence()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.Silence, 0, 100));

            Assert.IsTrue(effects.HasSilence());
        }

        [Test]
        public void HasAttackLock_DelegatesToHasStatus_AttackLock()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.AttackLock, 0, 100));

            Assert.IsTrue(effects.HasAttackLock());
        }

        [Test]
        public void HasGadgetLock_DelegatesToHasStatus_GadgetLock()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.GadgetLock, 0, 100));

            Assert.IsTrue(effects.HasGadgetLock());
        }

        [Test]
        public void HasSuperLock_DelegatesToHasStatus_SuperLock()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.SuperLock, 0, 100));

            Assert.IsTrue(effects.HasSuperLock());
        }

        [Test]
        public void HasMovementLockStatus_ReturnsTrueOnly_ForMovementLockType()
        {
            // HasMovementLockStatus is the pure status half. The full
            // movement-lock query on BrawlerState ALSO OR's in the IsStunned
            // flag — that composition lives on the coordinator and is NOT
            // tested here.
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.Stun, 0, 100));

            Assert.IsFalse(effects.HasMovementLockStatus(),
                "stun alone must not satisfy HasMovementLockStatus — that's the coordinator's job");

            effects.Active.Add(new FakeStatusEffect(StatusEffectType.MovementLock, 0, 100));

            Assert.IsTrue(effects.HasMovementLockStatus());
        }

        // ---------- TickAndCollectExpired ----------

        [Test]
        public void TickAndCollectExpired_TicksAllActiveEffects()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            FakeStatusEffect a = new FakeStatusEffect(StatusEffectType.Slow, 0, 100);
            FakeStatusEffect b = new FakeStatusEffect(StatusEffectType.Burn, 0, 100);
            effects.Active.Add(a);
            effects.Active.Add(b);
            List<IStatusEffectInstance> removed = new List<IStatusEffectInstance>();

            effects.TickAndCollectExpired(new FakeStatusTarget(), currentTick: 5, removed);

            Assert.AreEqual(1, a.TickCallCount);
            Assert.AreEqual(1, b.TickCallCount);
        }

        [Test]
        public void TickAndCollectExpired_RemovesExpired_FromActiveList()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            FakeStatusEffect alive = new FakeStatusEffect(StatusEffectType.Slow, 0, 100);
            FakeStatusEffect expired = new FakeStatusEffect(StatusEffectType.Burn, 0, 5);
            effects.Active.Add(alive);
            effects.Active.Add(expired);
            List<IStatusEffectInstance> removed = new List<IStatusEffectInstance>();

            effects.TickAndCollectExpired(new FakeStatusTarget(), currentTick: 10, removed);

            Assert.AreEqual(1, effects.Active.Count);
            Assert.AreSame(alive, effects.Active[0]);
        }

        [Test]
        public void TickAndCollectExpired_AppendsRemovedToBuffer()
        {
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            FakeStatusEffect expired = new FakeStatusEffect(StatusEffectType.Burn, 0, 5);
            effects.Active.Add(expired);
            List<IStatusEffectInstance> removed = new List<IStatusEffectInstance>();

            effects.TickAndCollectExpired(new FakeStatusTarget(), currentTick: 10, removed);

            Assert.AreEqual(1, removed.Count);
            Assert.AreSame(expired, removed[0]);
        }

        [Test]
        public void TickAndCollectExpired_DoesNotClearBuffer_BetweenCalls()
        {
            // Caller-owned-buffer contract. The coordinator decides when to
            // clear the buffer; this method only appends. Verifying this
            // pins the API in place — if someone "helpfully" adds a Clear()
            // here later, we want the suite to scream.
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            FakeStatusEffect a = new FakeStatusEffect(StatusEffectType.Burn, 0, 5);
            FakeStatusEffect b = new FakeStatusEffect(StatusEffectType.Burn, 0, 5);
            List<IStatusEffectInstance> removed = new List<IStatusEffectInstance>();
            FakeStatusTarget target = new FakeStatusTarget();

            effects.Active.Add(a);
            effects.TickAndCollectExpired(target, currentTick: 10, removed);
            // Caller did NOT clear `removed`. Add another expired one and tick again.
            effects.Active.Add(b);
            effects.TickAndCollectExpired(target, currentTick: 11, removed);

            Assert.AreEqual(2, removed.Count, "buffer accumulates across calls — caller is owner");
        }

        [Test]
        public void TickAndCollectExpired_NullBuffer_DoesNotThrow()
        {
            // Defensive contract: passing null shouldn't crash. The implementation
            // uses `removedOut?.Add(...)` and we want that null-tolerance pinned
            // so a future refactor doesn't accidentally NRE on a code path that
            // legitimately doesn't care which effects expired.
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            FakeStatusEffect expired = new FakeStatusEffect(StatusEffectType.Burn, 0, 5);
            effects.Active.Add(expired);

            Assert.DoesNotThrow(() =>
                effects.TickAndCollectExpired(new FakeStatusTarget(), 10, removedOut: null));
            Assert.AreEqual(0, effects.Active.Count, "expired effect still removed even with null buffer");
        }

        [Test]
        public void TickAndCollectExpired_MultipleExpiredEffects_AllRemoved()
        {
            // Reverse-iteration safety. If we naively forward-iterated and
            // RemoveAt'd, we'd skip the next neighbour and it'd survive past
            // its expiry. Three-effect setup with all expiring on the same
            // tick should clear the whole list.
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.Slow, 0, 5));
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.Burn, 0, 5));
            effects.Active.Add(new FakeStatusEffect(StatusEffectType.Stun, 0, 5));
            List<IStatusEffectInstance> removed = new List<IStatusEffectInstance>();

            effects.TickAndCollectExpired(new FakeStatusTarget(), currentTick: 10, removed);

            Assert.AreEqual(0, effects.Active.Count);
            Assert.AreEqual(3, removed.Count);
        }

        [Test]
        public void TickAndCollectExpired_CallsRemoveHook_OnExpiredEffects()
        {
            // The Remove() lifecycle hook must fire before the effect is
            // dropped from the list. This is the seam where an effect can
            // undo whatever it Apply'd (e.g. a slow restoring move speed).
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            FakeStatusEffect expired = new FakeStatusEffect(StatusEffectType.Slow, 0, 5);
            effects.Active.Add(expired);

            effects.TickAndCollectExpired(new FakeStatusTarget(), 10, new List<IStatusEffectInstance>());

            Assert.AreEqual(1, expired.RemoveCallCount);
        }

        // ---------- Clear ----------

        [Test]
        public void Clear_RemovesAllEffects_WithoutFiringRemoveHooks()
        {
            // Clear() is the respawn path — it's a hard wipe with NO
            // lifecycle hooks (a respawning brawler isn't "having its slow
            // expire", it's just a fresh entity). The no-hooks behaviour is
            // intentional and we lock it in here.
            BrawlerStatusEffects effects = new BrawlerStatusEffects();
            FakeStatusEffect e = new FakeStatusEffect(StatusEffectType.Slow, 0, 100);
            effects.Active.Add(e);

            effects.Clear();

            Assert.AreEqual(0, effects.Active.Count);
            Assert.AreEqual(0, e.RemoveCallCount, "Clear must NOT call Remove — that's TickAndCollectExpired's job");
        }
    }
}
