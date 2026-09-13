using System.Collections.Generic;

namespace MOBA.Core.Definitions
{
    public static class BrawlerSkillTreeResolver
    {
        public static bool TryResolve(
            BrawlerDefinition brawler,
            BrawlerSkillTreeDefinition tree,
            int powerLevel,
            IEnumerable<string> activeNodeIds,
            out ResolvedBrawlerBuild resolved,
            out string error,
            IEnumerable<string> unlockedNodeIds = null)
        {
            resolved = null;
            error = string.Empty;

            if (brawler == null)
            {
                error = "BrawlerDefinition is null.";
                return false;
            }

            if (tree == null)
            {
                error = $"Brawler '{brawler.name}' has no skill tree.";
                return false;
            }

            List<BrawlerSkillTreeNodeDefinition> requested = new List<BrawlerSkillTreeNodeDefinition>(8);
            if (activeNodeIds == null)
            {
                requested.AddRange(tree.BuildDefaultActiveNodes());
            }
            else
            {
                HashSet<string> ids = new HashSet<string>();
                foreach (string id in activeNodeIds)
                {
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                        continue;

                    if (!tree.TryGetNode(id, out BrawlerSkillTreeNodeDefinition node))
                    {
                        error = $"Skill tree '{tree.name}' has no node '{id}'.";
                        return false;
                    }

                    requested.Add(node);
                }
            }

            List<string> requestedIds = new List<string>(requested.Count);
            for (int i = 0; i < requested.Count; i++)
            {
                if (requested[i] != null)
                    requestedIds.Add(requested[i].EffectiveId);
            }

            IList<string> unlockedIds = unlockedNodeIds == null
                ? null
                : new List<string>(unlockedNodeIds);
            if (!BrawlerSkillTreeRules.ValidateActiveNodes(
                    tree,
                    powerLevel,
                    requestedIds,
                    unlockedIds,
                    out error))
            {
                return false;
            }

            resolved = new ResolvedBrawlerBuild
            {
                MainAttack = brawler.MainAttack,
                SuperAbility = brawler.SuperAbility
            };

            for (int i = 0; i < requested.Count; i++)
            {
                BrawlerSkillTreeNodeDefinition node = requested[i];
                if (node == null)
                    continue;

                resolved.SkillTreeNodes.Add(node);

                if (node.GrantedMainAttack != null)
                    resolved.MainAttack = node.GrantedMainAttack;

                if (node.GrantedSuper != null)
                    resolved.SuperAbility = node.GrantedSuper;

                if (node.GrantedGadget != null && !resolved.Gadgets.Contains(node.GrantedGadget))
                    resolved.Gadgets.Add(node.GrantedGadget);

                if (node.GrantedHypercharge != null)
                    resolved.Hypercharge = node.GrantedHypercharge;

                if (node.GrantedPassive != null && !resolved.PassiveOptions.Contains(node.GrantedPassive))
                    resolved.PassiveOptions.Add(node.GrantedPassive);

                if (node.NodeType != BrawlerSkillTreeNodeType.Nanopower &&
                    !resolved.PassiveOptions.Contains(node))
                {
                    resolved.PassiveOptions.Add(node);
                }
            }

            return true;
        }
    }
}
