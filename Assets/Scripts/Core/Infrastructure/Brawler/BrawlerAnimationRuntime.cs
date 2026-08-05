using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Shared presentation-only animation signal layer for authored and procedural brawler models.
    /// It converts simulation movement and presentation events into stable animation weights.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BrawlerAnimationRuntime : MonoBehaviour
    {
        private const float DefaultMaxReadableSpeed = 5.5f;
        private const float MoveDeadZone = 0.02f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int Move01Hash = Animator.StringToHash("Move01");
        private static readonly int Run01Hash = Animator.StringToHash("Run01");
        private static readonly int Idle01Hash = Animator.StringToHash("Idle01");
        private static readonly int MainAttackHash = Animator.StringToHash("MainAttack");
        private static readonly int SuperHash = Animator.StringToHash("Super");
        private static readonly int GadgetHash = Animator.StringToHash("Gadget");
        private static readonly int HitReactHash = Animator.StringToHash("HitReact");
        private static readonly int HealHash = Animator.StringToHash("Heal");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int HyperchargeHash = Animator.StringToHash("Hypercharge");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int IsHyperchargedHash = Animator.StringToHash("IsHypercharged");

        [SerializeField] private BrawlerController _owner;
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _driveAnimatorParameters = true;
        [SerializeField] private float _maxReadableSpeed = DefaultMaxReadableSpeed;

        [Header("Runtime Debug")]
        [SerializeField] private float _debugSpeed;
        [SerializeField] private float _debugMove01;
        [SerializeField] private float _debugMainAttack;
        [SerializeField] private float _debugSuper;
        [SerializeField] private float _debugHitReact;
        [SerializeField] private float _debugDeath;
        [SerializeField] private string _debugLastEvent;

        private Vector3 _moveDirection = Vector3.forward;
        private Vector3 _facingDirection = Vector3.forward;
        private Vector3 _aimDirection = Vector3.forward;
        private bool _hyperchargeActive;
        private int _lastTickFrame = -1;

        private bool _parametersCached;
        private int _cachedControllerId;
        private bool _hasSpeed;
        private bool _hasMove01;
        private bool _hasRun01;
        private bool _hasIdle01;
        private bool _hasMainAttack;
        private bool _hasSuper;
        private bool _hasGadget;
        private bool _hasHitReact;
        private bool _hasHeal;
        private bool _hasDeath;
        private bool _hasHypercharge;
        private bool _hasIsMoving;
        private bool _hasIsDead;
        private bool _hasIsHypercharged;

        public BrawlerController Owner => _owner;
        public float PoseTime { get; private set; }
        public float Speed { get; private set; }
        public float Move01 { get; private set; }
        public float Run01 { get; private set; }
        public float Idle01 { get; private set; }
        public float StridePhase { get; private set; }
        public float StrideSin { get; private set; }
        public float StrideCos { get; private set; }
        public float BodyBob { get; private set; }
        public float BodyLeanDegrees { get; private set; }
        public float MainAttackWeight { get; private set; }
        public float SuperWeight { get; private set; }
        public float GadgetWeight { get; private set; }
        public float HitReactWeight { get; private set; }
        public float HealWeight { get; private set; }
        public float DeathWeight { get; private set; }
        public float HyperchargeWeight { get; private set; }
        public float ActionWeight { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsHypercharged => _hyperchargeActive || HyperchargeWeight > 0.01f;
        public Vector3 MoveDirection => _moveDirection;
        public Vector3 FacingDirection => _facingDirection;
        public Vector3 AimDirection => _aimDirection;
        public string LastEventDebug => _debugLastEvent;

        public static BrawlerAnimationRuntime Ensure(GameObject root, BrawlerController owner)
        {
            if (root == null)
                return null;

            BrawlerAnimationRuntime runtime = root.GetComponent<BrawlerAnimationRuntime>();
            if (runtime == null)
                runtime = root.AddComponent<BrawlerAnimationRuntime>();

            runtime.Bind(owner);
            return runtime;
        }

        public void Bind(BrawlerController owner)
        {
            _owner = owner != null ? owner : GetComponentInParent<BrawlerController>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            _maxReadableSpeed = Mathf.Max(0.1f, _maxReadableSpeed);
            _parametersCached = false;
            RefreshDirections(Vector3.zero);
        }

        private void Awake()
        {
            if (_owner == null)
                _owner = GetComponentInParent<BrawlerController>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            _maxReadableSpeed = Mathf.Max(0.1f, _maxReadableSpeed);
            RefreshDirections(Vector3.zero);
        }

        private void OnEnable()
        {
            BrawlerPresentationEventBus.OnEvent += HandleBrawlerPresentationEvent;
            CombatPresentationEventBus.OnEvent += HandleCombatPresentationEvent;
        }

        private void OnDisable()
        {
            BrawlerPresentationEventBus.OnEvent -= HandleBrawlerPresentationEvent;
            CombatPresentationEventBus.OnEvent -= HandleCombatPresentationEvent;
        }

        private void Update()
        {
            float deltaTime = _owner == null ? Time.unscaledDeltaTime : Time.deltaTime;
            Tick(deltaTime);
        }

        private void OnValidate()
        {
            _maxReadableSpeed = Mathf.Max(0.1f, _maxReadableSpeed);
        }

        public void Tick(float deltaTime)
        {
            if (_lastTickFrame == Time.frameCount)
                return;

            _lastTickFrame = Time.frameCount;
            deltaTime = Mathf.Max(0f, deltaTime);
            PoseTime += deltaTime;

            Vector3 velocity = _owner != null ? _owner.PlanarVelocity : Vector3.zero;
            velocity.y = 0f;
            Speed = velocity.magnitude;
            Move01 = Mathf.Clamp01(Speed / Mathf.Max(0.1f, _maxReadableSpeed));
            Run01 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, Move01));
            Idle01 = 1f - Move01;
            IsMoving = Speed > MoveDeadZone;

            RefreshDirections(velocity);
            UpdateStride(deltaTime);
            DecayWeights(deltaTime);
            DriveAnimatorParameters();
            RefreshDebugFields();
        }

        public void PulseMainAttack(float strength = 1f)
        {
            MainAttackWeight = Mathf.Max(MainAttackWeight, Mathf.Clamp01(strength));
            ActionWeight = Mathf.Max(ActionWeight, MainAttackWeight);
        }

        public void PulseSuper(float strength = 1f)
        {
            SuperWeight = Mathf.Max(SuperWeight, Mathf.Clamp01(strength));
            ActionWeight = Mathf.Max(ActionWeight, SuperWeight);
        }

        public void PulseGadget(float strength = 1f)
        {
            GadgetWeight = Mathf.Max(GadgetWeight, Mathf.Clamp01(strength));
            ActionWeight = Mathf.Max(ActionWeight, GadgetWeight);
        }

        public void PulseHitReact(float strength = 1f)
        {
            HitReactWeight = Mathf.Max(HitReactWeight, Mathf.Clamp01(strength));
        }

        public void PulseHeal(float strength = 1f)
        {
            HealWeight = Mathf.Max(HealWeight, Mathf.Clamp01(strength));
        }

        private void RefreshDirections(Vector3 velocity)
        {
            if (velocity.sqrMagnitude > MoveDeadZone * MoveDeadZone)
                _moveDirection = velocity.normalized;

            Vector3 facing = _owner != null ? _owner.transform.forward : transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
                _facingDirection = facing.normalized;

            _aimDirection = _facingDirection;
        }

        private void UpdateStride(float deltaTime)
        {
            float strideRate = Mathf.Lerp(2.0f, 13.2f, Mathf.Sqrt(Move01));
            StridePhase += strideRate * deltaTime;
            StrideSin = Mathf.Sin(StridePhase);
            StrideCos = Mathf.Cos(StridePhase);
            BodyBob = Mathf.Lerp(0.012f, 0.095f, Run01) * Mathf.Abs(StrideSin) * Move01;
            BodyLeanDegrees = Mathf.Lerp(2.0f, 9.2f, Run01) * Move01;
        }

        private void DecayWeights(float deltaTime)
        {
            MainAttackWeight = Mathf.MoveTowards(MainAttackWeight, 0f, deltaTime * 7.0f);
            SuperWeight = Mathf.MoveTowards(SuperWeight, 0f, deltaTime * 4.5f);
            GadgetWeight = Mathf.MoveTowards(GadgetWeight, 0f, deltaTime * 6.0f);
            HitReactWeight = Mathf.MoveTowards(HitReactWeight, 0f, deltaTime * 7.5f);
            HealWeight = Mathf.MoveTowards(HealWeight, 0f, deltaTime * 4.0f);

            IsDead = _owner != null && _owner.State != null && _owner.State.IsDead;
            DeathWeight = Mathf.MoveTowards(DeathWeight, IsDead ? 1f : 0f, deltaTime * (IsDead ? 8.0f : 5.0f));

            float hyperTarget = _hyperchargeActive ? 1f : 0f;
            HyperchargeWeight = Mathf.MoveTowards(HyperchargeWeight, hyperTarget, deltaTime * (_hyperchargeActive ? 5.0f : 3.0f));

            float attackOrSuper = Mathf.Max(MainAttackWeight, SuperWeight);
            ActionWeight = Mathf.Clamp01(Mathf.Max(attackOrSuper, GadgetWeight));
        }

        private void HandleBrawlerPresentationEvent(BrawlerPresentationEvent evt)
        {
            if (_owner == null || evt.Source != _owner)
                return;

            _debugLastEvent = evt.EventType.ToString();

            switch (evt.EventType)
            {
                case BrawlerPresentationEventType.MainAttackStarted:
                case BrawlerPresentationEventType.MainAttackSucceeded:
                    PulseMainAttack();
                    break;

                case BrawlerPresentationEventType.GadgetStarted:
                case BrawlerPresentationEventType.GadgetSucceeded:
                    PulseGadget();
                    break;

                case BrawlerPresentationEventType.SuperStarted:
                case BrawlerPresentationEventType.SuperSucceeded:
                    PulseSuper();
                    break;

                case BrawlerPresentationEventType.HyperchargeStarted:
                    _hyperchargeActive = true;
                    HyperchargeWeight = 1f;
                    break;

                case BrawlerPresentationEventType.HyperchargeEnded:
                    _hyperchargeActive = false;
                    break;

                case BrawlerPresentationEventType.DamageTaken:
                    PulseHitReact(Mathf.Clamp01(0.45f + evt.Value / 1800f));
                    break;

                case BrawlerPresentationEventType.Healed:
                    PulseHeal(Mathf.Clamp01(0.35f + evt.Value / 1600f));
                    break;

                case BrawlerPresentationEventType.Died:
                    IsDead = true;
                    DeathWeight = 1f;
                    break;
            }
        }

        private void HandleCombatPresentationEvent(CombatPresentationEvent evt)
        {
            if (_owner == null)
                return;

            if (evt.Source == _owner)
                HandleSourceCombatEvent(evt);

            if (evt.Target == _owner)
                HandleTargetCombatEvent(evt);
        }

        private void HandleSourceCombatEvent(CombatPresentationEvent evt)
        {
            _debugLastEvent = evt.EventType.ToString();

            switch (evt.EventType)
            {
                case CombatPresentationEventType.AbilityCastStarted:
                case CombatPresentationEventType.AbilityCastSucceeded:
                case CombatPresentationEventType.ProjectileSpawned:
                    PulseAbilitySlot(evt.SlotType, evt.IsSuper);
                    break;
            }
        }

        private void HandleTargetCombatEvent(CombatPresentationEvent evt)
        {
            _debugLastEvent = evt.EventType.ToString();

            switch (evt.EventType)
            {
                case CombatPresentationEventType.DamageHit:
                case CombatPresentationEventType.StatusApplied:
                    PulseHitReact(Mathf.Clamp01(0.45f + evt.Value / 1800f));
                    break;

                case CombatPresentationEventType.Death:
                    IsDead = true;
                    DeathWeight = 1f;
                    break;

                case CombatPresentationEventType.Respawn:
                    IsDead = false;
                    DeathWeight = 0f;
                    break;
            }
        }

        private void PulseAbilitySlot(AbilitySlotType slotType, bool isSuper)
        {
            if (isSuper || slotType == AbilitySlotType.Super)
            {
                PulseSuper();
                return;
            }

            if (slotType == AbilitySlotType.Gadget)
            {
                PulseGadget();
                return;
            }

            PulseMainAttack();
        }

        private void DriveAnimatorParameters()
        {
            if (!_driveAnimatorParameters || _animator == null)
                return;

            CacheAnimatorParametersIfNeeded();

            if (_hasSpeed)
                _animator.SetFloat(SpeedHash, Speed);
            if (_hasMove01)
                _animator.SetFloat(Move01Hash, Move01);
            if (_hasRun01)
                _animator.SetFloat(Run01Hash, Run01);
            if (_hasIdle01)
                _animator.SetFloat(Idle01Hash, Idle01);
            if (_hasMainAttack)
                _animator.SetFloat(MainAttackHash, MainAttackWeight);
            if (_hasSuper)
                _animator.SetFloat(SuperHash, SuperWeight);
            if (_hasGadget)
                _animator.SetFloat(GadgetHash, GadgetWeight);
            if (_hasHitReact)
                _animator.SetFloat(HitReactHash, HitReactWeight);
            if (_hasHeal)
                _animator.SetFloat(HealHash, HealWeight);
            if (_hasDeath)
                _animator.SetFloat(DeathHash, DeathWeight);
            if (_hasHypercharge)
                _animator.SetFloat(HyperchargeHash, HyperchargeWeight);
            if (_hasIsMoving)
                _animator.SetBool(IsMovingHash, IsMoving);
            if (_hasIsDead)
                _animator.SetBool(IsDeadHash, IsDead);
            if (_hasIsHypercharged)
                _animator.SetBool(IsHyperchargedHash, IsHypercharged);
        }

        private void CacheAnimatorParametersIfNeeded()
        {
            int controllerId = _animator.runtimeAnimatorController != null
                ? _animator.runtimeAnimatorController.GetInstanceID()
                : 0;

            if (_parametersCached && _cachedControllerId == controllerId)
                return;

            _parametersCached = true;
            _cachedControllerId = controllerId;
            _hasSpeed = false;
            _hasMove01 = false;
            _hasRun01 = false;
            _hasIdle01 = false;
            _hasMainAttack = false;
            _hasSuper = false;
            _hasGadget = false;
            _hasHitReact = false;
            _hasHeal = false;
            _hasDeath = false;
            _hasHypercharge = false;
            _hasIsMoving = false;
            _hasIsDead = false;
            _hasIsHypercharged = false;

            if (_animator.runtimeAnimatorController == null)
                return;

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Float)
                {
                    CacheFloatParameter(parameter.nameHash);
                }
                else if (parameter.type == AnimatorControllerParameterType.Bool)
                {
                    CacheBoolParameter(parameter.nameHash);
                }
            }
        }

        private void CacheFloatParameter(int nameHash)
        {
            if (nameHash == SpeedHash) _hasSpeed = true;
            else if (nameHash == Move01Hash) _hasMove01 = true;
            else if (nameHash == Run01Hash) _hasRun01 = true;
            else if (nameHash == Idle01Hash) _hasIdle01 = true;
            else if (nameHash == MainAttackHash) _hasMainAttack = true;
            else if (nameHash == SuperHash) _hasSuper = true;
            else if (nameHash == GadgetHash) _hasGadget = true;
            else if (nameHash == HitReactHash) _hasHitReact = true;
            else if (nameHash == HealHash) _hasHeal = true;
            else if (nameHash == DeathHash) _hasDeath = true;
            else if (nameHash == HyperchargeHash) _hasHypercharge = true;
        }

        private void CacheBoolParameter(int nameHash)
        {
            if (nameHash == IsMovingHash) _hasIsMoving = true;
            else if (nameHash == IsDeadHash) _hasIsDead = true;
            else if (nameHash == IsHyperchargedHash) _hasIsHypercharged = true;
        }

        private void RefreshDebugFields()
        {
            _debugSpeed = Speed;
            _debugMove01 = Move01;
            _debugMainAttack = MainAttackWeight;
            _debugSuper = SuperWeight;
            _debugHitReact = HitReactWeight;
            _debugDeath = DeathWeight;
        }
    }
}
