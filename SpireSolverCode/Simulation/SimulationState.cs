using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Saves;
using SpireSolver.Simulation;

public static class SimulationState
{
    static SimulationState()
    {
        index = 0;
    }

    public static int index { get; set; }
    public static bool IsSimulating { get; set; }
    public static bool IsFinished { get; set; }

    public static List<SimulationResult> Simulations { get; } = new();

    public static void Start()
    {
        IsFinished = false;
        IsSimulating = true;
    }

    public static void Save(Player player)
    {
        var result = new SimulationResult
        {
            Actions = CombatManager.Instance.History.Entries.ToList(),

            HpRemaining = player.Creature.CurrentHp,
            MaxHp = player.Creature.MaxHp,

            TurnsTaken = player.PlayerCombatState?.TurnNumber ?? 0,

            Won = !player.Creature.CombatState.Enemies.Any(e =>
                e != null &&
                e.IsAlive &&
                e.IsPrimaryEnemy)
        };

        Simulations.Add(result);

        GD.Print(
            $"[SpireSolver] Simulation #{Simulations.Count}: " +
            $"HP {result.HpRemaining}/{result.MaxHp}, " +
            $"Turns {result.TurnsTaken}, " +
            $"Won {result.Won}, " +
            $"Actions {result.Actions.Count}"
        );
    }

    public static void Clear()
    {
        Simulations.Clear();
    }

    public static void Stop()
    {
        IsSimulating = false;
        IsFinished = false;
    }
}