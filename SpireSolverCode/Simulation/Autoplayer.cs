using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using SpireSolver.Simulation;

public static class Autoplayer
{
    public static async Task PlayTurn(Rng random, CancellationToken ct)
    {
        if (!CombatManager.Instance.IsInProgress)
            return;

        Player player = LocalContext.GetMe(
            RunManager.Instance.DebugOnlyGetState()
        );

        ICombatState? combatState = player.Creature.CombatState;

        if (combatState == null)
            return;

        // Remember which turn we started on so the autoplayer cannot
        // accidentally continue into the next turn.
        int startTurn = player.PlayerCombatState.TurnNumber;


        /*
         * This is important.
         *
         * Vakuu pushes its selector for the entire autoplay operation.
         * Cards that require card selection can therefore resolve without
         * waiting for normal player input.
         */
        using (CardSelectCmd.PushSelector(new VakuuCardSelector()))
        {
            while (
                CombatManager.Instance.IsInProgress &&
                !CombatManager.Instance.IsOverOrEnding &&
                !CombatManager.Instance.IsPlayerReadyToEndTurn(player) &&
                player.PlayerCombatState != null &&
                player.PlayerCombatState.Phase == PlayerTurnPhase.Play &&
                player.PlayerCombatState.TurnNumber == startTurn)
            {
                ct.ThrowIfCancellationRequested();

                /*
                 * Match Vakuu:
                 *
                 * Don't randomly select from the playable cards.
                 * Walk through the hand and take the first card that the
                 * game currently considers playable.
                 */
                CardModel? card = PileType.Hand
                    .GetPile(player)
                    .Cards
                    .FirstOrDefault(c => c.CanPlay());

                if (card == null)
                    break;

                Creature? target = GetTarget(
                    player,
                    card,
                    combatState,
                    random
                );

                /*
                 * This is also taken from Vakuu's implementation.
                 *
                 * Vakuu explicitly spends the card's resources before
                 * invoking AutoPlay.
                 */
                await card.SpendResources();

                ct.ThrowIfCancellationRequested();

                await CardCmd.AutoPlay(
                    new BlockingPlayerChoiceContext(),
                    card,
                    target,
                    AutoPlayType.Default,
                    true,
                    true
                );
                
                if (SimulationState.IsSimulating &&
                    AreAllPrimaryEnemiesDead(combatState))
                {
                    SimulationState.IsFinished = true;
                    break;
                }
            }
        }

        /*
         * Only end the turn if we're still in the same valid player turn.
         */
        if (!SimulationState.IsFinished &&
            CombatManager.Instance.IsInProgress &&
            player.PlayerCombatState?.Phase == PlayerTurnPhase.Play)
        {
            PlayerCmd.EndTurn(player, false);
        }
    }

    /// <summary>
    /// Vakuu-style targeting.
    ///
    /// AnyEnemy:
    ///     First hittable enemy.
    ///
    /// AnyPlayer:
    ///     The local player.
    ///
    /// AnyAlly:
    ///     Random living player ally other than ourselves.
    ///
    /// Everything else:
    ///     No explicit target.
    /// </summary>
    private static Creature? GetTarget(
        Player player,
        CardModel card,
        ICombatState combatState,
        Rng random)
    {
        switch (card.TargetType)
        {
            case TargetType.AnyEnemy:
                return combatState.HittableEnemies.FirstOrDefault();

            case TargetType.AnyPlayer:
                return player.Creature;

            case TargetType.AnyAlly:
            {
                var allies = combatState.Allies
                    .Where(c =>
                        c != null &&
                        c.IsAlive &&
                        c.IsPlayer &&
                        c != player.Creature)
                    .ToList();

                if (allies.Count == 0)
                    return null;

                return random.NextItem(allies);
            }

            default:
                return null;
        }
    }
    
    private static bool AreAllPrimaryEnemiesDead(ICombatState combatState)
    {
        return !combatState.Enemies.Any(e =>
            e != null &&
            e.IsAlive &&
            e.IsPrimaryEnemy);
    }
}