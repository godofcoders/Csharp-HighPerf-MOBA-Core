using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Definitions
{
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

            return true;
        }

        private void OnValidate()
        {
            if (!Validate(out string error))
                Debug.LogWarning($"[BrawlerSkillTreeDefinition] {error}");
        }
    }
}
