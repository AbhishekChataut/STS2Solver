using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
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

    // null = no forced outcome
    // true = won
    // false = lost
    public static bool? Outcome { get; private set; }

    public static List<SimulationResult> Simulations { get; } = new();

    public static void Start()
    {
        IsFinished = false;
        IsSimulating = true;
        Outcome = null;
    }

    public static void Finish(bool won)
    {
        Outcome = won;
        IsFinished = true;
    }

    public static void Lose()
    {
        Finish(false);
    }

    public static void Win()
    {
        Finish(true);
    }

    public static SimulationResult Save(Player player)
    {
        bool won = Outcome ??
                   !player.Creature.CombatState.Enemies.Any(e =>
                       e != null &&
                       e.IsAlive &&
                       e.IsPrimaryEnemy);

        var result = new SimulationResult
        {
            Actions = CombatManager.Instance.History.Entries.ToList(),

            HpRemaining = Outcome == false
                ? 0
                : player.Creature.CurrentHp,

            MaxHp = player.Creature.MaxHp,

            TurnsTaken =
                player.PlayerCombatState?.TurnNumber ?? 0,

            Won = won
        };

        Simulations.Add(result);

        GD.Print(
            $"[SpireSolver] Simulation #{Simulations.Count}: " +
            $"HP {result.HpRemaining}/{result.MaxHp}, " +
            $"Turns {result.TurnsTaken}, " +
            $"Won {result.Won}, " +
            $"Actions {result.Actions.Count}"
        );

        return result;
    }
    
    public static void Clear()
    {
        Simulations.Clear();
    }

    public static void Stop()
    {
        IsSimulating = false;
        IsFinished = false;
        Outcome = null;
    }
}