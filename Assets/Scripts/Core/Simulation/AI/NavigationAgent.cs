using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

public class NavigationAgent
{
    private BrawlerController _brawler;
    private List<PathNode> _path;
    private int _index;
    public Vector3 Position => _brawler.Position;

    public NavigationAgent(BrawlerController brawler)
    {
        _brawler = brawler;
    }

    public void SetDestination(Vector3 target)
    {
        var start = SimulationClock.Pathfinder.GetGridCoords(_brawler.Position);
        var end = SimulationClock.Pathfinder.GetGridCoords(target);

        _path = SimulationClock.Pathfinder.FindPath(start.x, start.y, end.x, end.y);
        _index = 0;
    }

    public void Tick()
    {
        if (_path == null || _index >= _path.Count) return;

        Vector3 targetPos = SimulationClock.Pathfinder.GetWorldPos(_path[_index].X, _path[_index].Y);
        Vector3 dir = (targetPos - _brawler.Position).normalized;

        _brawler.SetMoveInput(dir);

        if ((targetPos - _brawler.Position).sqrMagnitude < 0.25f)
            _index++;
    }
}