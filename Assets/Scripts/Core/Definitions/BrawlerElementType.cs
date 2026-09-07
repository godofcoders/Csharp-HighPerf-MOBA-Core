using UnityEngine;

namespace MOBA.Core.Definitions
{
    // Elemental identity used for presentation, UI labels, and future
    // elemental gameplay rules. Values are serialized into brawler assets.
    public enum BrawlerElementType
    {
        None = 0,
        Fire = 1,
        Water = 2,
        Earth = 3,
        Air = 4,
        Lightning = 5,
        Ice = 6,
        Nature = 7,
        Shadow = 8
    }

    public static class BrawlerElementUtility
    {
        public static string ToDisplayName(BrawlerElementType element)
        {
            switch (element)
            {
                case BrawlerElementType.Fire:
                    return "Fire";
                case BrawlerElementType.Water:
                    return "Water";
                case BrawlerElementType.Earth:
                    return "Earth";
                case BrawlerElementType.Air:
                    return "Air";
                case BrawlerElementType.Lightning:
                    return "Lightning";
                case BrawlerElementType.Ice:
                    return "Ice";
                case BrawlerElementType.Nature:
                    return "Nature";
                case BrawlerElementType.Shadow:
                    return "Shadow";
                default:
                    return "Neutral";
            }
        }

        public static string FormatTypeAndRole(BrawlerDefinition brawler)
        {
            if (brawler == null)
                return "Neutral";

            string role = brawler.Archetype.ToString();
            if (brawler.ElementType == BrawlerElementType.None)
                return role;

            return $"{ToDisplayName(brawler.ElementType)} / {role}";
        }

        public static Color ToColor(BrawlerElementType element)
        {
            switch (element)
            {
                case BrawlerElementType.Fire:
                    return new Color(1f, 0.36f, 0.18f, 1f);
                case BrawlerElementType.Water:
                    return new Color(0.20f, 0.72f, 1f, 1f);
                case BrawlerElementType.Earth:
                    return new Color(0.58f, 0.46f, 0.25f, 1f);
                case BrawlerElementType.Air:
                    return new Color(0.72f, 0.92f, 1f, 1f);
                case BrawlerElementType.Lightning:
                    return new Color(1f, 0.88f, 0.22f, 1f);
                case BrawlerElementType.Ice:
                    return new Color(0.62f, 0.94f, 1f, 1f);
                case BrawlerElementType.Nature:
                    return new Color(0.42f, 0.86f, 0.34f, 1f);
                case BrawlerElementType.Shadow:
                    return new Color(0.62f, 0.45f, 0.95f, 1f);
                default:
                    return Color.gray;
            }
        }
    }
}
