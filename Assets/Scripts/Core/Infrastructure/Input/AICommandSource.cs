using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Simulation
{
    public class AICommandSource : IBrawlerCommandSource
    {
        private readonly BrawlerAIProfile _profile;
        private Vector3 _moveDirection;
        private Vector3 _smoothedMoveDirection;
        private bool _moveQueued;
        private bool _hasSmoothedMoveDirection;

        private Vector3 _mainAttackDirection;
        private Vector3 _mainAttackTargetPoint;
        private bool _mainAttackHasTargetPoint;
        private bool _mainAttackQueued;

        private Vector3 _gadgetDirection;
        private bool _gadgetQueued;

        private Vector3 _superDirection;
        private Vector3 _superTargetPoint;
        private bool _superHasTargetPoint;
        private bool _superQueued;

        private bool _hyperchargeQueued;

        public AICommandSource(BrawlerAIProfile profile = null)
        {
            _profile = profile;
        }

        public void QueueMove(Vector3 direction)
        {
            QueueMove(direction, highPriority: false);
        }

        public void QueueMove(Vector3 direction, bool highPriority)
        {
            if (direction.sqrMagnitude <= 0.01f)
            {
                _moveDirection = Vector3.zero;
                _moveQueued = true;
                return;
            }

            Vector3 smoothedDirection = ResolveSmoothedMoveDirection(direction, highPriority);
            float speedScale = GetMoveSpeedScale(highPriority);
            _moveDirection = smoothedDirection * speedScale;
            _moveQueued = true;
        }

        public void QueueMainAttack(Vector3 direction)
        {
            QueueMainAttack(direction, Vector3.zero, false);
        }

        public void QueueMainAttack(Vector3 direction, Vector3 targetPoint, bool hasTargetPoint)
        {
            if (direction.sqrMagnitude <= 0.01f)
                return;

            _mainAttackDirection = direction.normalized;
            _mainAttackTargetPoint = targetPoint;
            _mainAttackHasTargetPoint = hasTargetPoint;
            _mainAttackQueued = true;
        }

        public void QueueGadget(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.01f)
                return;

            _gadgetDirection = direction.normalized;
            _gadgetQueued = true;
        }

        public void QueueSuper(Vector3 direction)
        {
            QueueSuper(direction, Vector3.zero, false);
        }

        public void QueueSuper(Vector3 direction, Vector3 targetPoint, bool hasTargetPoint)
        {
            if (direction.sqrMagnitude <= 0.01f)
                return;

            _superDirection = direction.normalized;
            _superTargetPoint = targetPoint;
            _superHasTargetPoint = hasTargetPoint;
            _superQueued = true;
        }

        public void QueueHypercharge()
        {
            _hyperchargeQueued = true;
        }

        public void ClearQueuedCommands()
        {
            _moveDirection = Vector3.zero;
            _smoothedMoveDirection = Vector3.zero;
            _moveQueued = false;
            _hasSmoothedMoveDirection = false;

            _mainAttackDirection = Vector3.zero;
            _mainAttackTargetPoint = Vector3.zero;
            _mainAttackHasTargetPoint = false;
            _mainAttackQueued = false;

            _gadgetDirection = Vector3.zero;
            _gadgetQueued = false;

            _superDirection = Vector3.zero;
            _superTargetPoint = Vector3.zero;
            _superHasTargetPoint = false;
            _superQueued = false;

            _hyperchargeQueued = false;
        }

        private Vector3 ResolveSmoothedMoveDirection(Vector3 direction, bool highPriority)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return Vector3.zero;

            Vector3 targetDirection = direction.normalized;
            if (!_hasSmoothedMoveDirection)
            {
                _smoothedMoveDirection = targetDirection;
                _hasSmoothedMoveDirection = true;
                return targetDirection;
            }

            float turnRateDegrees = highPriority
                ? GetHighPriorityTurnRateDegrees()
                : GetTurnRateDegrees();
            float maxRadians = Mathf.Max(1f, turnRateDegrees) * Mathf.Deg2Rad;

            Vector3 smoothed = Vector3.RotateTowards(
                _smoothedMoveDirection,
                targetDirection,
                maxRadians,
                0f);

            if (smoothed.sqrMagnitude <= 0.01f)
                smoothed = targetDirection;

            _smoothedMoveDirection = smoothed.normalized;
            return _smoothedMoveDirection;
        }

        private float GetTurnRateDegrees()
        {
            return _profile != null && _profile.AIMoveInputTurnRateDegreesPerTick > 0f
                ? _profile.AIMoveInputTurnRateDegreesPerTick
                : 22f;
        }

        private float GetHighPriorityTurnRateDegrees()
        {
            return _profile != null && _profile.AIHighPriorityMoveInputTurnRateDegreesPerTick > 0f
                ? _profile.AIHighPriorityMoveInputTurnRateDegreesPerTick
                : 54f;
        }

        private float GetMoveSpeedScale(bool highPriority)
        {
            float scale = highPriority
                ? _profile != null && _profile.AIHighPriorityMoveSpeedScale > 0f
                    ? _profile.AIHighPriorityMoveSpeedScale
                    : 0.90f
                : _profile != null && _profile.AIMoveSpeedScale > 0f
                    ? _profile.AIMoveSpeedScale
                    : 0.86f;

            return Mathf.Clamp(scale, 0.35f, 1f);
        }

        public void CollectCommands(List<BrawlerCommand> output, uint currentTick)
        {
            if (_moveQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Move,
                    Direction = _moveDirection,
                    Tick = currentTick
                });
                _moveQueued = false;
            }

            if (_mainAttackQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.MainAttack,
                    Direction = _mainAttackDirection,
                    TargetPoint = _mainAttackTargetPoint,
                    HasTargetPoint = _mainAttackHasTargetPoint,
                    Tick = currentTick
                });
                _mainAttackQueued = false;
                _mainAttackTargetPoint = Vector3.zero;
                _mainAttackHasTargetPoint = false;
            }

            if (_gadgetQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Gadget,
                    Direction = _gadgetDirection,
                    Tick = currentTick
                });
                _gadgetQueued = false;
            }

            if (_superQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Super,
                    Direction = _superDirection,
                    TargetPoint = _superTargetPoint,
                    HasTargetPoint = _superHasTargetPoint,
                    Tick = currentTick
                });
                _superQueued = false;
                _superTargetPoint = Vector3.zero;
                _superHasTargetPoint = false;
            }

            if (_hyperchargeQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Hypercharge,
                    Direction = Vector3.forward,
                    Tick = currentTick
                });
                _hyperchargeQueued = false;
            }
        }
    }
}
