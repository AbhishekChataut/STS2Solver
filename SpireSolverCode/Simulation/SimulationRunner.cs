using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SpireSolver.SpireSolverCode.Simulation.Policies;
using System;
using System.Threading;
using System.Threading.Tasks;
using SpireSolver.Simulation;
using SpireSolver.SpireSolverCode.Simulation.Learning;

namespace SpireSolver.SpireSolverCode.Simulation;

/// <summary>
/// Owns the "run N combat simulations" loop.
///
/// The runner owns the combat policy used for a simulation batch and
/// passes that policy to the Autoplayer whenever a player turn needs
/// to be played.
/// </summary>
public static class SimulationRunner
{
    private const int DefaultSimulationCount = 50;

    private static CancellationTokenSource? _cts;
    
    private const int ReplayBufferCapacity = 50_000;

    private static readonly ReplayBuffer _replayBuffer =
        new(ReplayBufferCapacity);

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
    
    public static ReplayBuffer ReplayBuffer =>
        _replayBuffer;

    /// <summary>
    /// Starts a batch of combat simulations.
    ///
    /// If no policy is supplied, simulations use RandomCombatPolicy.
    /// </summary>
    public static void Start(
        int simulationCount = DefaultSimulationCount,
        ICombatPolicy? policy = null)
    {
        Stop();

        policy ??= new RandomCombatPolicy();

        _cts = new CancellationTokenSource();

        TaskHelper.RunSafely(
            RunLoop(
                simulationCount,
                policy,
                _cts.Token
            )
        );
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

    private static async Task RunLoop(
        int simulationCount,
        ICombatPolicy policy,
        CancellationToken token)
    {
        IsRunning = true;
        
        var stateEncoder = new BasicStateEncoder();
        var actionEncoder = new BasicActionEncoder();

        try
        {
            for (int i = 0; i < simulationCount; i++)
            {
                token.ThrowIfCancellationRequested();
                
                var trajectory = new SimulationTrajectory();

                GD.Print(
                    $"[SpireSolver] Simulation {i + 1}/{simulationCount}"
                );

                var rng = new Rng(
                    (ulong)SimulationState.index
                );

                SimulationState.Start();

                Player player = LocalContext.GetMe(
                    RunManager.Instance.DebugOnlyGetState()
                );

                CombatState? combatState = GetCombatState();

                CardPile drawPile =
                    PileType.Draw.GetPile(player);

                drawPile.RandomizeOrderInternal(
                    player,
                    rng,
                    combatState
                );

                while (!SimulationState.IsFinished)
                {
                    token.ThrowIfCancellationRequested();

                    await Autoplayer.PlayTurn(
                        rng,
                        policy,
                        stateEncoder,
                        actionEncoder,
                        trajectory,
                        token
                    );

                    await Task.Delay(5, token);
                }

                SimulationResult result =
                    SimulationState.Save(player);

                float reward =
                    RewardCalculator.Calculate(result);

                trajectory.AssignReward(reward);
                
                _replayBuffer.AddRange(
                    trajectory.Experiences
                );

                GD.Print(
                    $"[SpireSolver] Recorded " +
                    $"{trajectory.Experiences.Count} decisions, " +
                    $"reward {reward:F3}, " +
                    $"replay buffer {_replayBuffer.Count}/" +
                    $"{_replayBuffer.Capacity}"
                );


                SimulationState.index++;

                SimulationCompleted?.Invoke();

                GD.Print(
                    "[SpireSolver] Cleaning up simulation combat"
                );

                await Restarter.RestartRoom(token);
            }

            GD.Print(
                $"[SpireSolver] Completed all {simulationCount} simulations"
            );
        }
        catch (OperationCanceledException)
        {
            GD.Print(
                "[SpireSolver] Simulation run cancelled"
            );
        }
        finally
        {
            SimulationState.Stop();

            IsRunning = false;

            BatchCompleted?.Invoke();
        }
    }
}