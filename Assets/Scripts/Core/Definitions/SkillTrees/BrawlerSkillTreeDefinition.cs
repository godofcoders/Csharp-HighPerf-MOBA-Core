using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [System.Serializable]
    public sealed class BrawlerSkillTreeChoiceGroupDefinition
    {
        public string GroupId;
        public string DisplayName;
        [Min(1)] public int MaxUnlocked = 2;
        [Min(1)] public int MaxActive = 1;
    }

    [System.Serializable]
    public sealed class BrawlerSkillTreeBranchDefinition
    {
        public string BranchId;
        public string DisplayName;
        public string[] ExclusiveBranchIds;

        public bool IsExclusiveWith(string otherBranchId)
        {
            if (string.IsNullOrWhiteSpace(otherBranchId) || otherBranchId == BranchId)
                return false;

            if (ExclusiveBranchIds == null)
                return false;

            for (int i = 0; i < ExclusiveBranchIds.Length; i++)
            {
                if (ExclusiveBranchIds[i] == otherBranchId)
                    return true;
            }

            return false;
        }
    }

    [CreateAssetMenu(fileName = "BrawlerSkillTree", menuName = "MOBA/Skill Trees/Brawler Skill Tree")]
    public sealed class BrawlerSkillTreeDefinition : ScriptableObject
    {
        [Header("Tree Identity")]
        public string TreeId;
        public string DisplayName = "Resonance Tree";
        public BrawlerElementType ElementType = BrawlerElementType.None;
        [TextArea] public string Description;
        public Color AccentColor = new Color(1f, 0.35f, 0.1f, 1f);

        [Header("Tree Rules")]
        [Min(1)] public int MaxActiveNodes = 8;
        public BrawlerSkillTreeChoiceGroupDefinition[] ChoiceGroups;
        public BrawlerSkillTreeBranchDefinition[] Branches;
        public BrawlerSkillTreeNodeDefinition[] Nodes;
        public BrawlerSkillTreeNodeDefinition[] DefaultActiveNodes;

        public List<BrawlerSkillTreeNodeDefinition> BuildDefaultActiveNodes()
        {
            List<BrawlerSkillTreeNodeDefinition> result = new List<BrawlerSkillTreeNodeDefinition>(8);

            if (DefaultActiveNodes != null && DefaultActiveNodes.Length > 0)
            {
                for (int i = 0; i < DefaultActiveNodes.Length; i++)
                {
                    BrawlerSkillTreeNodeDefinition node = DefaultActiveNodes[i];
                    if (node != null && !result.Contains(node))
                        result.Add(node);
                }

                return result;
            }

            if (Nodes == null)
                return result;

            for (int i = 0; i < Nodes.Length; i++)
            {
                BrawlerSkillTreeNodeDefinition node = Nodes[i];
                if (node != null && node.ActiveByDefault && !result.Contains(node))
                    result.Add(node);
            }

            return result;
        }

        public bool TryGetNode(string nodeId, out BrawlerSkillTreeNodeDefinition node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(nodeId) || Nodes == null)
                return false;

            for (int i = 0; i < Nodes.Length; i++)
            {
                BrawlerSkillTreeNodeDefinition candidate = Nodes[i];
                if (candidate != null && candidate.EffectiveId == nodeId)
                {
                    node = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetChoiceGroup(string groupId, out BrawlerSkillTreeChoiceGroupDefinition group)
        {
            group = null;
            if (string.IsNullOrWhiteSpace(groupId) || ChoiceGroups == null)
                return false;

            for (int i = 0; i < ChoiceGroups.Length; i++)
            {
                BrawlerSkillTreeChoiceGroupDefinition candidate = ChoiceGroups[i];
                if (candidate != null && candidate.GroupId == groupId)
                {
                    group = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetBranch(string branchId, out BrawlerSkillTreeBranchDefinition branch)
        {
            branch = null;
            if (string.IsNullOrWhiteSpace(branchId) || Branches == null)
                return false;

            for (int i = 0; i < Branches.Length; i++)
            {
                BrawlerSkillTreeBranchDefinition candidate = Branches[i];
                if (candidate != null && candidate.BranchId == branchId)
                {
                    branch = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool Validate(out string error)
        {
            error = string.Empty;

            if (Nodes == null || Nodes.Length == 0)
                return true;

            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < Nodes.Length; i++)
            {
                BrawlerSkillTreeNodeDefinition node = Nodes[i];
                if (node == null)
                    continue;

                if (string.IsNullOrWhiteSpace(node.EffectiveId))
                {
                    error = $"Tree '{name}' contains a node without an id.";
                    return false;
                }

                if (!ids.Add(node.EffectiveId))
                {
                    error = $"Tree '{name}' contains duplicate node id '{node.EffectiveId}'.";
                    return false;
                }
            }

            HashSet<string> groupIds = new HashSet<string>();
            if (ChoiceGroups != null)
            {
                for (int i = 0; i < ChoiceGroups.Length; i++)
                {
                    BrawlerSkillTreeChoiceGroupDefinition group = ChoiceGroups[i];
                    if (group == null || string.IsNullOrWhiteSpace(group.GroupId))
                        continue;

                    if (!groupIds.Add(group.GroupId))
                    {
                        error = $"Tree '{name}' contains duplicate choice group id '{group.GroupId}'.";
                        return false;
                    }

                    if (group.MaxActive > group.MaxUnlocked)
                    {
                        error = $"Choice group '{group.GroupId}' cannot equip more nodes than it unlocks.";
                        return false;
                    }
                }
            }

            HashSet<string> branchIds = new HashSet<string>();
            if (Branches != null)
            {
                for (int i = 0; i < Branches.Length; i++)
                {
                    BrawlerSkillTreeBranchDefinition branch = Branches[i];
                    if (branch == null || string.IsNullOrWhiteSpace(branch.BranchId))
                        continue;

                    if (!branchIds.Add(branch.BranchId))
                    {
                        error = $"Tree '{name}' contains duplicate branch id '{branch.BranchId}'.";
                        return false;
                    }

                }
            }

            if (Branches != null)
            {
                for (int i = 0; i < Branches.Length; i++)
                {
                    BrawlerSkillTreeBranchDefinition branch = Branches[i];
                    if (branch == null || branch.ExclusiveBranchIds == null)
                        continue;

                    for (int e = 0; e < branch.ExclusiveBranchIds.Length; e++)
                    {
                        string exclusiveBranchId = branch.ExclusiveBranchIds[e];
                        if (!string.IsNullOrWhiteSpace(exclusiveBranchId) &&
                            exclusiveBranchId != branch.BranchId &&
                            !branchIds.Contains(exclusiveBranchId))
                        {
                            error = $"Branch '{branch.BranchId}' references unknown branch '{exclusiveBranchId}'.";
                            return false;
                        }
                    }
                }
            }

            for (int i = 0; i < Nodes.Length; i++)
            {
                BrawlerSkillTreeNodeDefinition node = Nodes[i];
                if (node == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(node.ChoiceGroupId) &&
                    !groupIds.Contains(node.ChoiceGroupId))
                {
                    error = $"Node '{node.EffectiveId}' references unknown choice group '{node.ChoiceGroupId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(node.BranchId) &&
                    !branchIds.Contains(node.BranchId))
                {
                    error = $"Node '{node.EffectiveId}' references unknown branch '{node.BranchId}'.";
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            if (!Validate(out string error))
                Debug.LogWarning($"[BrawlerSkillTreeDefinition] {error}");
        }
    }

    /// <summary>
    /// Shared skill-tree rules used by the menu and the runtime build resolver.
    /// Keeping these checks in one place prevents an invalid saved loadout from
    /// behaving differently in the match than it did in the selection screen.
    /// </summary>
    public static class BrawlerSkillTreeRules
    {
        public static bool CanUnlockNode(
            BrawlerSkillTreeNodeDefinition node,
            BrawlerSkillTreeDefinition tree,
            int powerLevel,
            IList<string> unlocked,
            out string reason)
        {
            reason = string.Empty;
            if (node == null || tree == null)
            {
                reason = "This skill node is not configured.";
                return false;
            }

            if (powerLevel < node.UnlockPowerLevel)
            {
                reason = $"Requires power level {node.UnlockPowerLevel}.";
                return false;
            }

            if (node.PrerequisiteNodeIds != null)
            {
                for (int i = 0; i < node.PrerequisiteNodeIds.Length; i++)
                {
                    string prerequisite = node.PrerequisiteNodeIds[i];
                    if (!string.IsNullOrWhiteSpace(prerequisite) &&
                        (unlocked == null || !unlocked.Contains(prerequisite)))
                    {
                        reason = $"Unlock {prerequisite} first.";
                        return false;
                    }
                }
            }

            if (TryGetGroupLimit(tree, node, out BrawlerSkillTreeChoiceGroupDefinition group) &&
                CountNodesInGroup(tree, group.GroupId, unlocked) >= group.MaxUnlocked)
            {
                reason = $"{ResolveGroupName(group)} limit reached ({group.MaxUnlocked} unlocks).";
                return false;
            }

            if (HasBranchConflict(tree, node.BranchId, unlocked))
            {
                reason = "This path is locked because another branch was already chosen.";
                return false;
            }

            return true;
        }

        public static bool CanActivateNode(
            BrawlerSkillTreeNodeDefinition node,
            BrawlerSkillTreeDefinition tree,
            int powerLevel,
            IList<string> active,
            IList<string> unlocked,
            out string reason)
        {
            reason = string.Empty;
            if (node == null || tree == null)
            {
                reason = "This skill node is not configured.";
                return false;
            }

            if (powerLevel < node.UnlockPowerLevel)
            {
                reason = $"Requires power level {node.UnlockPowerLevel}.";
                return false;
            }

            if (active == null || active.Count >= tree.MaxActiveNodes)
            {
                reason = $"Active node limit reached ({tree.MaxActiveNodes}).";
                return false;
            }

            if (node.PrerequisiteNodeIds != null)
            {
                for (int i = 0; i < node.PrerequisiteNodeIds.Length; i++)
                {
                    string prerequisite = node.PrerequisiteNodeIds[i];
                    if (!string.IsNullOrWhiteSpace(prerequisite) &&
                        (active == null || !active.Contains(prerequisite)))
                    {
                        reason = $"Equip {prerequisite} first.";
                        return false;
                    }
                }
            }

            if (TryGetGroupLimit(tree, node, out BrawlerSkillTreeChoiceGroupDefinition group) &&
                CountNodesInGroup(tree, group.GroupId, active) >= group.MaxActive)
            {
                reason = $"Equip only {group.MaxActive} {ResolveGroupName(group)} option at a time.";
                return false;
            }

            if (HasDirectConflict(node, tree, active))
            {
                reason = "This node conflicts with the currently equipped build.";
                return false;
            }

            if (HasBranchConflict(tree, node.BranchId, unlocked))
            {
                reason = "This path is locked because another branch was already chosen.";
                return false;
            }

            return true;
        }

        public static bool ValidateActiveNodes(
            BrawlerSkillTreeDefinition tree,
            int powerLevel,
            IList<string> activeNodeIds,
            IList<string> unlockedNodeIds,
            out string error)
        {
            error = string.Empty;
            if (tree == null)
            {
                error = "Skill tree is null.";
                return false;
            }

            List<string> active = activeNodeIds == null
                ? new List<string>()
                : new List<string>(activeNodeIds);
            if (active.Count > tree.MaxActiveNodes)
            {
                error = $"Skill tree '{tree.name}' allows {tree.MaxActiveNodes} active nodes, but {active.Count} were selected.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < active.Count; i++)
            {
                string id = active[i];
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    continue;

                if (!tree.TryGetNode(id, out BrawlerSkillTreeNodeDefinition node))
                {
                    error = $"Skill tree '{tree.name}' has no node '{id}'.";
                    return false;
                }

                if (node.UnlockPowerLevel > powerLevel)
                {
                    error = $"Skill node '{node.EffectiveDisplayName}' unlocks at power level {node.UnlockPowerLevel}.";
                    return false;
                }

                if (unlockedNodeIds != null && !unlockedNodeIds.Contains(id))
                {
                    error = $"Skill node '{node.EffectiveDisplayName}' has not been unlocked.";
                    return false;
                }
            }

            for (int i = 0; i < active.Count; i++)
            {
                if (!tree.TryGetNode(active[i], out BrawlerSkillTreeNodeDefinition node))
                    continue;

                if (node.PrerequisiteNodeIds != null)
                {
                    for (int p = 0; p < node.PrerequisiteNodeIds.Length; p++)
                    {
                        string prerequisite = node.PrerequisiteNodeIds[p];
                        if (!string.IsNullOrWhiteSpace(prerequisite) && !ids.Contains(prerequisite))
                        {
                            error = $"Skill node '{node.EffectiveDisplayName}' requires '{prerequisite}'.";
                            return false;
                        }
                    }
                }

                if (HasDirectConflict(node, tree, active))
                {
                    error = $"Skill node '{node.EffectiveDisplayName}' conflicts with the selected build.";
                    return false;
                }

                if (TryGetGroupLimit(tree, node, out BrawlerSkillTreeChoiceGroupDefinition group) &&
                    CountNodesInGroup(tree, group.GroupId, active) > group.MaxActive)
                {
                    error = $"Choice group '{ResolveGroupName(group)}' allows only {group.MaxActive} active option(s).";
                    return false;
                }

                if (HasBranchConflict(tree, node.BranchId, unlockedNodeIds ?? active))
                {
                    error = $"Skill node '{node.EffectiveDisplayName}' belongs to a locked branch.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetGroupLimit(
            BrawlerSkillTreeDefinition tree,
            BrawlerSkillTreeNodeDefinition node,
            out BrawlerSkillTreeChoiceGroupDefinition group)
        {
            group = null;
            return node != null && tree != null &&
                tree.TryGetChoiceGroup(node.ChoiceGroupId, out group);
        }

        private static int CountNodesInGroup(
            BrawlerSkillTreeDefinition tree,
            string groupId,
            IList<string> nodeIds)
        {
            if (tree == null || string.IsNullOrWhiteSpace(groupId) || nodeIds == null)
                return 0;

            int count = 0;
            for (int i = 0; i < nodeIds.Count; i++)
            {
                if (tree.TryGetNode(nodeIds[i], out BrawlerSkillTreeNodeDefinition node) &&
                    node.ChoiceGroupId == groupId)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasDirectConflict(
            BrawlerSkillTreeNodeDefinition node,
            BrawlerSkillTreeDefinition tree,
            IList<string> active)
        {
            if (node == null || active == null)
                return false;

            if (node.MutuallyExclusiveNodeIds != null)
            {
                for (int i = 0; i < node.MutuallyExclusiveNodeIds.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(node.MutuallyExclusiveNodeIds[i]) &&
                        active.Contains(node.MutuallyExclusiveNodeIds[i]))
                        return true;
                }
            }

            if (tree?.Nodes == null)
                return false;

            for (int i = 0; i < tree.Nodes.Length; i++)
            {
                BrawlerSkillTreeNodeDefinition other = tree.Nodes[i];
                if (other == null || !active.Contains(other.EffectiveId) || other.MutuallyExclusiveNodeIds == null)
                    continue;

                for (int e = 0; e < other.MutuallyExclusiveNodeIds.Length; e++)
                {
                    if (other.MutuallyExclusiveNodeIds[e] == node.EffectiveId)
                        return true;
                }
            }

            return false;
        }

        private static bool HasBranchConflict(
            BrawlerSkillTreeDefinition tree,
            string branchId,
            IList<string> selectedNodeIds)
        {
            if (tree == null || string.IsNullOrWhiteSpace(branchId) || selectedNodeIds == null)
                return false;

            for (int i = 0; i < selectedNodeIds.Count; i++)
            {
                if (!tree.TryGetNode(selectedNodeIds[i], out BrawlerSkillTreeNodeDefinition selected) ||
                    string.IsNullOrWhiteSpace(selected.BranchId) || selected.BranchId == branchId)
                    continue;

                if (tree.TryGetBranch(branchId, out BrawlerSkillTreeBranchDefinition branch) &&
                    branch.IsExclusiveWith(selected.BranchId))
                    return true;

                if (tree.TryGetBranch(selected.BranchId, out BrawlerSkillTreeBranchDefinition selectedBranch) &&
                    selectedBranch.IsExclusiveWith(branchId))
                    return true;
            }

            return false;
        }

        private static string ResolveGroupName(BrawlerSkillTreeChoiceGroupDefinition group)
        {
            return group != null && !string.IsNullOrWhiteSpace(group.DisplayName)
                ? group.DisplayName
                : "this choice group";
        }
    }
}
