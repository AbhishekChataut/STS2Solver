using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SpireSolver.Simulation;

/// <summary>
/// Owns the "run N combat simulations" loop.
///
/// Previously this lived inline in TopBarPatch's button handler. It has
/// been extracted so the UI (SpireSolverScreen) can start/stop it and
/// react to progress without needing to know about the top bar at all.
/// </summary>
public static class SimulationRunner
{
    private const int DefaultSimulationCount = 10;

    private static CancellationTokenSource? _cts;

    /// <summary>
    /// Raised after each individual simulation finishes and its result has
    /// been saved, so callers (e.g. the results panel) can refresh live.
    /// </summary>
    public static event Action? SimulationCompleted;

    /// <summary>
    /// Raised once the whole batch (or as much of it as ran before
    /// cancellation) has completed.
    /// </summary>
    public static event Action? BatchCompleted;

    public static bool IsRunning { get; private set; }

    public static void Start(int simulationCount = DefaultSimulationCount)
    {
        Stop();

        _cts = new CancellationTokenSource();
        TaskHelper.RunSafely(RunLoop(simulationCount, _cts.Token));
    }

    public static void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        IsRunning = false;
    }

    private static CombatState? GetCombatState()
    {
        if (!CombatManager.Instance.IsInProgress)
            return null;

        return CombatManager.Instance.DebugOnlyGetState();
    }

    private static async Task RunLoop(int simulationCount, CancellationToken token)
    {
        IsRunning = true;

        try
        {
            for (int i = 0; i < simulationCount; i++)
            {
                token.ThrowIfCancellationRequested();

                GD.Print(
                    $"[SpireSolver] Simulation {i + 1}/{simulationCount}"
                );

                var rng = new Rng((ulong)SimulationState.index);

                SimulationState.Start();

                Player player = LocalContext.GetMe(
                    RunManager.Instance.DebugOnlyGetState()
                );

                CombatState? combatState = GetCombatState();

                CardPile drawPile = PileType.Draw.GetPile(player);
                drawPile.RandomizeOrderInternal(
                    player,
                    rng,
                    combatState
                );

                while (!SimulationState.IsFinished)
                {
                    token.ThrowIfCancellationRequested();

                    await Autoplayer.PlayTurn(rng, token);
                    await Task.Delay(5, token);
                }

                SimulationState.Save(player);
                SimulationState.index++;

                SimulationCompleted?.Invoke();

                // Don't restart after the final simulation.
                if (i < simulationCount - 1)
                {
                    GD.Print("[SpireSolver] Restarting simulation");

                    await Restarter.RestartRoom(token);
                }
            }

            GD.Print($"[SpireSolver] Completed all {simulationCount} simulations");
        }
        catch (OperationCanceledException)
        {
            GD.Print("[SpireSolver] Simulation run cancelled");
        }
        finally
        {
            IsRunning = false;
            BatchCompleted?.Invoke();
        }
    }
}