using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Lightweight pose animator for deterministic procedural brawler models.
    /// It reads simulation state but never drives gameplay movement.
    /// </summary>
    public class BrawlerProceduralModelAnimator : MonoBehaviour
    {
        private BrawlerController _owner;
        private Transform _bodyRoot;
        private Transform _torso;
        private Transform _head;
        private Transform _leftArm;
        private Transform _rightArm;
        private Transform _leftLeg;
        private Transform _rightLeg;
        private Transform _leftWeapon;
        private Transform _rightWeapon;

        private Vector3 _bodyBasePosition;
        private Quaternion _bodyBaseRotation;
        private Quaternion _torsoBaseRotation;
        private Quaternion _headBaseRotation;
        private Quaternion _leftArmBaseRotation;
        private Quaternion _rightArmBaseRotation;
        private Quaternion _leftLegBaseRotation;
        private Quaternion _rightLegBaseRotation;
        private Quaternion _leftWeaponBaseRotation;
        private Quaternion _rightWeaponBaseRotation;

        private float _poseTime;
        private float _attackRecoil;
        private float _superRecoil;
        private bool _useUnscaledTime;

        public void Initialize(
            BrawlerController owner,
            Transform bodyRoot,
            Transform torso,
            Transform head,
            Transform leftArm,
            Transform rightArm,
            Transform leftLeg,
            Transform rightLeg,
            Transform leftWeapon,
            Transform rightWeapon)
        {
            _owner = owner;
            _bodyRoot = bodyRoot;
            _torso = torso;
            _head = head;
            _leftArm = leftArm;
            _rightArm = rightArm;
            _leftLeg = leftLeg;
            _rightLeg = rightLeg;
            _leftWeapon = leftWeapon;
            _rightWeapon = rightWeapon;
            _useUnscaledTime = owner == null;

            CaptureBasePose();
        }

        private void OnEnable()
        {
            BrawlerPresentationEventBus.OnEvent += HandlePresentationEvent;
        }

        private void OnDisable()
        {
            BrawlerPresentationEventBus.OnEvent -= HandlePresentationEvent;
        }

        private void Awake()
        {
            if (_owner == null)
                _owner = GetComponentInParent<BrawlerController>();

            CaptureBasePose();
        }

        private void Update()
        {
            if (_bodyRoot == null)
                return;

            float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _poseTime += deltaTime;

            float speed = _owner != null ? _owner.PlanarVelocity.magnitude : 0f;
            float move01 = Mathf.Clamp01(speed / 5.5f);
            float stride = _poseTime * Mathf.Lerp(2.0f, 9.5f, move01);
            float strideSin = Mathf.Sin(stride);
            float strideCos = Mathf.Cos(stride);
            float bob = Mathf.Lerp(0.018f, 0.075f, move01) * Mathf.Abs(strideSin);
            float lean = move01 * 5.5f;

            _attackRecoil = Mathf.MoveTowards(_attackRecoil, 0f, deltaTime * 7.0f);
            _superRecoil = Mathf.MoveTowards(_superRecoil, 0f, deltaTime * 4.5f);

            IdlePose idle = BuildIdlePose(move01);

            _bodyRoot.localPosition = _bodyBasePosition + Vector3.up * (bob + idle.BodyLift);
            _bodyRoot.localRotation = _bodyBaseRotation * Quaternion.Euler(lean + idle.BodyPitch, idle.BodyYaw, strideCos * move01 * 1.8f + idle.BodyRoll);

            if (_torso != null)
                _torso.localRotation = _torsoBaseRotation * Quaternion.Euler(-lean * 0.35f + idle.TorsoPitch, idle.TorsoYaw, -strideCos * move01 * 2.0f + idle.TorsoRoll);

            if (_head != null)
                _head.localRotation = _headBaseRotation * Quaternion.Euler(-_attackRecoil * 2.5f + idle.HeadPitch, idle.HeadYaw, strideCos * 1.2f + idle.HeadRoll);

            float armSwing = move01 * 16f;
            float legSwing = move01 * 12f;
            float recoil = (_attackRecoil * 22f) + (_superRecoil * 36f);

            if (_leftArm != null)
                _leftArm.localRotation = _leftArmBaseRotation * Quaternion.Euler(-strideSin * armSwing - recoil + idle.LeftArmPitch, idle.LeftArmYaw, idle.LeftArmRoll);

            if (_rightArm != null)
                _rightArm.localRotation = _rightArmBaseRotation * Quaternion.Euler(strideSin * armSwing - recoil + idle.RightArmPitch, idle.RightArmYaw, idle.RightArmRoll);

            if (_leftLeg != null)
                _leftLeg.localRotation = _leftLegBaseRotation * Quaternion.Euler(strideSin * legSwing, 0f, 0f);

            if (_rightLeg != null)
                _rightLeg.localRotation = _rightLegBaseRotation * Quaternion.Euler(-strideSin * legSwing, 0f, 0f);

            if (_leftWeapon != null)
                _leftWeapon.localRotation = _leftWeaponBaseRotation * Quaternion.Euler(-_attackRecoil * 7f - _superRecoil * 10f + idle.LeftWeaponPitch, idle.LeftWeaponYaw, idle.LeftWeaponRoll);

            if (_rightWeapon != null)
                _rightWeapon.localRotation = _rightWeaponBaseRotation * Quaternion.Euler(-_attackRecoil * 7f - _superRecoil * 10f + idle.RightWeaponPitch, idle.RightWeaponYaw, idle.RightWeaponRoll);
        }

        private IdlePose BuildIdlePose(float move01)
        {
            float actionLockout = Mathf.Clamp01((_attackRecoil + _superRecoil) * 2f);
            float idleWeight = (1f - move01) * (1f - actionLockout);
            if (idleWeight <= 0.001f)
                return default(IdlePose);

            float breathe = Mathf.Sin(_poseTime * Mathf.PI * 2f * 0.42f);
            float smallSway = Mathf.Sin(_poseTime * Mathf.PI * 2f * 0.27f);
            float cycle = Mathf.Repeat(_poseTime + 0.8f, 13.5f);

            float inspect = SmoothWindow(cycle, 1.1f, 3.4f) * idleWeight;
            float wave = SmoothWindow(cycle, 5.0f, 6.9f) * idleWeight;
            float stretch = SmoothWindow(cycle, 9.0f, 11.1f) * idleWeight;
            float waveFlutter = Mathf.Sin(_poseTime * Mathf.PI * 2f * 2.7f);

            IdlePose pose = default(IdlePose);
            pose.BodyLift = idleWeight * (0.012f + Mathf.Max(0f, breathe) * 0.018f);
            pose.BodyPitch = idleWeight * (smallSway * 0.8f - stretch * 2.2f);
            pose.BodyRoll = idleWeight * smallSway * 1.2f;

            pose.TorsoPitch = idleWeight * (breathe * 1.4f - inspect * 4.5f - stretch * 5.0f);
            pose.TorsoYaw = inspect * -5f;
            pose.TorsoRoll = wave * -3.0f + stretch * 2.0f;

            pose.HeadPitch = idleWeight * (breathe * 0.8f + inspect * 8.0f - stretch * 5.0f);
            pose.HeadYaw = inspect * -22f + wave * 8f;
            pose.HeadRoll = idleWeight * smallSway * 1.6f + inspect * -5f;

            pose.RightArmPitch = inspect * -42f + stretch * -62f;
            pose.RightArmYaw = inspect * -8f;
            pose.RightArmRoll = inspect * 7f + stretch * 30f;
            pose.RightWeaponPitch = inspect * -18f;
            pose.RightWeaponYaw = inspect * -6f;
            pose.RightWeaponRoll = inspect * 6f;

            pose.LeftArmPitch = wave * (-72f + waveFlutter * 10f) + stretch * -60f;
            pose.LeftArmYaw = wave * -12f;
            pose.LeftArmRoll = wave * -35f + stretch * -32f;
            pose.LeftWeaponPitch = wave * -8f + stretch * -12f;
            pose.LeftWeaponRoll = wave * -8f;

            return pose;
        }

        private static float SmoothWindow(float value, float start, float end)
        {
            if (value <= start || value >= end)
                return 0f;

            float mid = (start + end) * 0.5f;
            if (value < mid)
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, mid, value));

            return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(mid, end, value));
        }

        private void HandlePresentationEvent(BrawlerPresentationEvent evt)
        {
            if (_owner == null || evt.Source != _owner)
                return;

            switch (evt.EventType)
            {
                case BrawlerPresentationEventType.MainAttackStarted:
                case BrawlerPresentationEventType.MainAttackSucceeded:
                    _attackRecoil = 1f;
                    break;

                case BrawlerPresentationEventType.SuperStarted:
                case BrawlerPresentationEventType.SuperSucceeded:
                    _superRecoil = 1f;
                    break;
            }
        }

        private void CaptureBasePose()
        {
            if (_bodyRoot != null)
            {
                _bodyBasePosition = _bodyRoot.localPosition;
                _bodyBaseRotation = _bodyRoot.localRotation;
            }

            if (_torso != null)
                _torsoBaseRotation = _torso.localRotation;
            if (_head != null)
                _headBaseRotation = _head.localRotation;
            if (_leftArm != null)
                _leftArmBaseRotation = _leftArm.localRotation;
            if (_rightArm != null)
                _rightArmBaseRotation = _rightArm.localRotation;
            if (_leftLeg != null)
                _leftLegBaseRotation = _leftLeg.localRotation;
            if (_rightLeg != null)
                _rightLegBaseRotation = _rightLeg.localRotation;
            if (_leftWeapon != null)
                _leftWeaponBaseRotation = _leftWeapon.localRotation;
            if (_rightWeapon != null)
                _rightWeaponBaseRotation = _rightWeapon.localRotation;
        }

        private struct IdlePose
        {
            public float BodyLift;
            public float BodyPitch;
            public float BodyYaw;
            public float BodyRoll;

            public float TorsoPitch;
            public float TorsoYaw;
            public float TorsoRoll;

            public float HeadPitch;
            public float HeadYaw;
            public float HeadRoll;

            public float LeftArmPitch;
            public float LeftArmYaw;
            public float LeftArmRoll;
            public float RightArmPitch;
            public float RightArmYaw;
            public float RightArmRoll;

            public float LeftWeaponPitch;
            public float LeftWeaponYaw;
            public float LeftWeaponRoll;
            public float RightWeaponPitch;
            public float RightWeaponYaw;
            public float RightWeaponRoll;
        }
    }
}
