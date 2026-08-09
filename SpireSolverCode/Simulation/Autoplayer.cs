using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SpireSolver.SpireSolverCode.Simulation.Actions;
using SpireSolver.SpireSolverCode.Simulation.Learning;
using SpireSolver.SpireSolverCode.Simulation.Policies;

namespace SpireSolver.SpireSolverCode.Simulation;

public static class Autoplayer
{
    /// <summary>
    /// Plays the current player turn using the supplied combat policy.
    ///
    /// Autoplayer is responsible for:
    /// - determining the current combat/player
    /// - generating legal actions
    /// - asking the policy which action to take
    /// - executing that action
    ///
    /// The policy is responsible only for choosing between the legal
    /// actions it is given.
    /// </summary>
    public static async Task PlayTurn(
            Rng random,
            ICombatPolicy policy,
            IStateEncoder stateEncoder,
            IActionEncoder actionEncoder,
            SimulationTrajectory trajectory,
            CancellationToken ct) {
        if (!CombatManager.Instance.IsInProgress)
            return;

        Player player = LocalContext.GetMe(
            RunManager.Instance.DebugOnlyGetState()
        );

        ICombatState? combatState =
            player.Creature.CombatState;

        if (combatState == null)
            return;

        // Remember which turn we started on so the autoplayer cannot
        // accidentally continue making decisions after the turn changes.
        int startTurn =
            player.PlayerCombatState.TurnNumber;

        /*
         * Vakuu pushes its selector for the entire autoplay operation.
         *
         * Cards that require card selection can therefore resolve without
         * waiting for normal player input.
         */
        using (CardSelectCmd.PushSelector(
                   new VakuuCardSelector()))
        {
            while (
                CombatManager.Instance.IsInProgress &&
                !CombatManager.Instance.IsOverOrEnding &&
                !CombatManager.Instance.IsPlayerReadyToEndTurn(player) &&
                player.PlayerCombatState != null &&
                player.PlayerCombatState.Phase ==
                    PlayerTurnPhase.Play &&
                player.PlayerCombatState.TurnNumber ==
                    startTurn)
            {
                ct.ThrowIfCancellationRequested();

                /*
                 * Autoplayer no longer decides what card/target to use.
                 *
                 * Instead, generate every legal decision and allow the
                 * supplied policy to choose between them.
                 */
                List<CombatAction> legalActions =
                    CombatActionGenerator.GetLegalActions(
                        player,
                        combatState
                    );

                if (legalActions.Count == 0)
                    break;

                CombatAction action =
                    policy.ChooseAction(
                        player,
                        combatState,
                        legalActions,
                        random
                    );
                
                float[] encodedState =
                    stateEncoder.Encode(
                        player,
                        combatState
                    );

                float[] encodedAction =
                    actionEncoder.Encode(action);

                trajectory.Record(
                    encodedState,
                    encodedAction
                );

                /*
                 * Execute exactly the action selected by the policy.
                 */
                bool turnEnded = await ExecuteAction(
                    player,
                    action,
                    ct
                );

                if (turnEnded)
                    return;

                /*
                 * During simulation, stop immediately once all primary
                 * enemies are dead.
                 */
                if (
                    SimulationState.IsSimulating &&
                    AreAllPrimaryEnemiesDead(combatState))
                {
                    SimulationState.IsFinished = true;
                    return;
                }
            }
        }

        /*
         * Fallback.
         *
         * Normally EndTurn should be generated as a CombatAction and the
         * policy can choose it directly.
         *
         * This remains here to safely finish the turn if the action loop
         * exits for some other reason while the player is still in the
         * Play phase.
         */
        if (
            !SimulationState.IsFinished &&
            CombatManager.Instance.IsInProgress &&
            player.PlayerCombatState?.Phase ==
                PlayerTurnPhase.Play)
        {
            PlayerCmd.EndTurn(player, false);
        }
    }

    /// <summary>
    /// Executes a decision that has already been selected by the policy.
    ///
    /// This method contains execution logic only. It should not decide
    /// which action is preferable.
    /// </summary>
    private static async Task<bool> ExecuteAction(
        Player player,
        CombatAction action,
        CancellationToken ct)
    {
        switch (action)
        {
            case CombatAction.PlayCard playCard:
            {
                await playCard.Card.SpendResources();

                ct.ThrowIfCancellationRequested();

                await CardCmd.AutoPlay(
                    new BlockingPlayerChoiceContext(),
                    playCard.Card,
                    playCard.Target,
                    AutoPlayType.Default,
                    true,
                    true
                );

                return false;
            }

            case CombatAction.EndTurn:
            {
                PlayerCmd.EndTurn(
                    player,
                    false
                );

                return true;
            }

            default:
            {
                throw new ArgumentOutOfRangeException(
                    nameof(action),
                    action,
                    "Unknown combat action."
                );
            }
        }
    }

    private static bool AreAllPrimaryEnemiesDead(
        ICombatState combatState)
    {
        return !combatState.Enemies.Any(e =>
            e != null &&
            e.IsAlive &&
            e.IsPrimaryEnemy
        );
    }
}