using MegaCrit.Sts2.Core.Combat.History;

namespace SpireSolver.Simulation;

public class SimulationResult
{
    public List<CombatHistoryEntry> Actions { get; init; } = new();

    public int HpRemaining { get; init; }
    public int MaxHp { get; init; }
    public int TurnsTaken { get; init; }

    public bool Won { get; init; }
}