using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public interface IDeployableRegistry
    {
        void Register(DeployableController controller);
        void Unregister(DeployableController controller);
        DeployableController GetActiveOwnedDeployable(BrawlerController owner, DeployableDefinition definition);
    }

    public sealed class DeployableRegistry : IDeployableRegistry
    {
        private readonly Dictionary<int, List<DeployableController>> _byOwner =
            new Dictionary<int, List<DeployableController>>();

        public void Register(DeployableController controller)
        {
            if (controller == null || controller.Owner == null)
                return;

            int ownerId = controller.Owner.EntityID;

            if (!_byOwner.TryGetValue(ownerId, out List<DeployableController> list))
            {
                list = new List<DeployableController>(4);
                _byOwner.Add(ownerId, list);
            }

            if (!list.Contains(controller))
                list.Add(controller);
        }

        public void Unregister(DeployableController controller)
        {
            if (controller == null || controller.Owner == null)
                return;

            int ownerId = controller.Owner.EntityID;

            if (!_byOwner.TryGetValue(ownerId, out List<DeployableController> list))
                return;

            list.Remove(controller);

            if (list.Count == 0)
                _byOwner.Remove(ownerId);
        }

        public DeployableController GetActiveOwnedDeployable(BrawlerController owner, DeployableDefinition definition)
        {
            if (owner == null || definition == null)
                return null;

            if (!_byOwner.TryGetValue(owner.EntityID, out List<DeployableController> list))
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                DeployableController controller = list[i];
                if (controller == null)
                    continue;

                if (controller.Definition == definition && controller.State != null && !controller.State.IsDead)
                    return controller;
            }

            return null;
        }
    }
}