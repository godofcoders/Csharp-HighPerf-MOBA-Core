using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public class AICommandSource : IBrawlerCommandSource
    {
        private Vector3 _moveDirection;
        private bool _moveQueued;

        private Vector3 _mainAttackDirection;
        private bool _mainAttackQueued;

        private Vector3 _gadgetDirection;
        private bool _gadgetQueued;

        private Vector3 _superDirection;
        private bool _superQueued;

        private bool _hyperchargeQueued;

        public void QueueMove(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.01f)
            {
                _moveDirection = Vector3.zero;
                _moveQueued = true;
                return;
            }

            _moveDirection = direction.normalized;
            _moveQueued = true;
        }

        public void QueueMainAttack(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.01f)
                return;

            _mainAttackDirection = direction.normalized;
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
            if (direction.sqrMagnitude <= 0.01f)
                return;

            _superDirection = direction.normalized;
            _superQueued = true;
        }

        public void QueueHypercharge()
        {
            _hyperchargeQueued = true;
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
                    Tick = currentTick
                });
                _mainAttackQueued = false;
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
                    Tick = currentTick
                });
                _superQueued = false;
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