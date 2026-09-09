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
            out string error)
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

            if (requested.Count > tree.MaxActiveNodes)
            {
                error = $"Skill tree '{tree.name}' allows {tree.MaxActiveNodes} active nodes, but {requested.Count} were selected.";
                return false;
            }

            HashSet<string> selectedIds = new HashSet<string>();
            for (int i = 0; i < requested.Count; i++)
            {
                BrawlerSkillTreeNodeDefinition node = requested[i];
                if (node == null)
                    continue;

                if (node.UnlockPowerLevel > powerLevel)
                {
                    error = $"Skill node '{node.EffectiveDisplayName}' unlocks at power level {node.UnlockPowerLevel}.";
                    return false;
                }

                selectedIds.Add(node.EffectiveId);
            }

            for (int i = 0; i < requested.Count; i++)
            {
                BrawlerSkillTreeNodeDefinition node = requested[i];
                if (node == null)
                    continue;

                if (node.PrerequisiteNodeIds != null)
                {
                    for (int p = 0; p < node.PrerequisiteNodeIds.Length; p++)
                    {
                        string prerequisite = node.PrerequisiteNodeIds[p];
                        if (!string.IsNullOrWhiteSpace(prerequisite) && !selectedIds.Contains(prerequisite))
                        {
                            error = $"Skill node '{node.EffectiveDisplayName}' requires '{prerequisite}'.";
                            return false;
                        }
                    }
                }

                if (node.MutuallyExclusiveNodeIds != null)
                {
                    for (int e = 0; e < node.MutuallyExclusiveNodeIds.Length; e++)
                    {
                        string exclusive = node.MutuallyExclusiveNodeIds[e];
                        if (!string.IsNullOrWhiteSpace(exclusive) && selectedIds.Contains(exclusive))
                        {
                            error = $"Skill node '{node.EffectiveDisplayName}' conflicts with '{exclusive}'.";
                            return false;
                        }
                    }
                }
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

                if (!resolved.PassiveOptions.Contains(node))
                    resolved.PassiveOptions.Add(node);
            }

            return true;
        }
    }
}
