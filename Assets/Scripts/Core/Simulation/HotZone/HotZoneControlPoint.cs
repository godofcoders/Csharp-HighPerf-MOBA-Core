using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class HotZoneControlPoint : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _controlWeight = 1f;

        private readonly List<BrawlerController> _blueOccupants = new List<BrawlerController>(4);
        private readonly List<BrawlerController> _redOccupants = new List<BrawlerController>(4);

        public float ControlWeight => Mathf.Max(0.1f, _controlWeight);

        public TeamType GetControllingTeam()
        {
            int blueCount = CountLiveOccupants(_blueOccupants);
            int redCount = CountLiveOccupants(_redOccupants);

            if (blueCount > 0 && redCount == 0)
                return TeamType.Blue;

            if (redCount > 0 && blueCount == 0)
                return TeamType.Red;

            return TeamType.Neutral;
        }

        public bool IsControlledBy(TeamType team)
        {
            return GetControllingTeam() == team;
        }

        private void OnTriggerEnter(Collider other)
        {
            BrawlerController brawler = other.GetComponentInParent<BrawlerController>();
            if (brawler == null || brawler.Team == TeamType.Neutral)
                return;

            List<BrawlerController> occupants = GetOccupants(brawler.Team);
            if (!occupants.Contains(brawler))
                occupants.Add(brawler);
        }

        private void OnTriggerExit(Collider other)
        {
            BrawlerController brawler = other.GetComponentInParent<BrawlerController>();
            if (brawler == null || brawler.Team == TeamType.Neutral)
                return;

            GetOccupants(brawler.Team).Remove(brawler);
        }

        private List<BrawlerController> GetOccupants(TeamType team)
        {
            return team == TeamType.Blue ? _blueOccupants : _redOccupants;
        }

        private static int CountLiveOccupants(List<BrawlerController> occupants)
        {
            int count = 0;
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                BrawlerController brawler = occupants[i];
                if (brawler == null || brawler.State == null || brawler.State.IsDead)
                {
                    occupants.RemoveAt(i);
                    continue;
                }

                count++;
            }

            return count;
        }
    }
}
