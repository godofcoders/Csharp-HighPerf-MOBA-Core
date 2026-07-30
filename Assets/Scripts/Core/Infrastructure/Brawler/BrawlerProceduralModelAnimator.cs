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

            float deltaTime = Time.deltaTime;
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

            _bodyRoot.localPosition = _bodyBasePosition + Vector3.up * bob;
            _bodyRoot.localRotation = _bodyBaseRotation * Quaternion.Euler(lean, 0f, strideCos * move01 * 1.8f);

            if (_torso != null)
                _torso.localRotation = _torsoBaseRotation * Quaternion.Euler(-lean * 0.35f, 0f, -strideCos * move01 * 2.0f);

            if (_head != null)
                _head.localRotation = _headBaseRotation * Quaternion.Euler(-_attackRecoil * 2.5f, 0f, strideCos * 1.2f);

            float armSwing = move01 * 16f;
            float legSwing = move01 * 12f;
            float recoil = (_attackRecoil * 22f) + (_superRecoil * 36f);

            if (_leftArm != null)
                _leftArm.localRotation = _leftArmBaseRotation * Quaternion.Euler(-strideSin * armSwing - recoil, 0f, 0f);

            if (_rightArm != null)
                _rightArm.localRotation = _rightArmBaseRotation * Quaternion.Euler(strideSin * armSwing - recoil, 0f, 0f);

            if (_leftLeg != null)
                _leftLeg.localRotation = _leftLegBaseRotation * Quaternion.Euler(strideSin * legSwing, 0f, 0f);

            if (_rightLeg != null)
                _rightLeg.localRotation = _rightLegBaseRotation * Quaternion.Euler(-strideSin * legSwing, 0f, 0f);

            if (_leftWeapon != null)
                _leftWeapon.localRotation = _leftWeaponBaseRotation * Quaternion.Euler(-_attackRecoil * 7f - _superRecoil * 10f, 0f, 0f);

            if (_rightWeapon != null)
                _rightWeapon.localRotation = _rightWeaponBaseRotation * Quaternion.Euler(-_attackRecoil * 7f - _superRecoil * 10f, 0f, 0f);
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
    }
}
