using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace SpireSolver.SpireSolverCode.Simulation.Actions;

public static class CombatActionGenerator
{
    public static List<CombatAction> GetLegalActions(
        Player player,
        ICombatState combatState)
    {
        var actions = new List<CombatAction>();

        foreach (CardModel card in PileType.Hand
                     .GetPile(player)
                     .Cards
                     .Where(c => c.CanPlay()))
        {
            AddCardActions(actions, player, card, combatState);
        }

        // Ending the turn is a decision, even if playable cards remain.
        /*
        actions.Add(new CombatAction.EndTurn());
        */

        return actions;
    }

    private static void AddCardActions(
        List<CombatAction> actions,
        Player player,
        CardModel card,
        ICombatState combatState)
    {
        switch (card.TargetType)
        {
            case TargetType.AnyEnemy:
                foreach (Creature enemy in combatState.HittableEnemies)
                {
                    actions.Add(
                        new CombatAction.PlayCard(card, enemy)
                    );
                }

                break;

            case TargetType.AnyPlayer:
                actions.Add(
                    new CombatAction.PlayCard(
                        card,
                        player.Creature
                    )
                );

                break;

            case TargetType.AnyAlly:
                foreach (Creature ally in combatState.Allies.Where(c =>
                             c != null &&
                             c.IsAlive &&
                             c.IsPlayer &&
                             c != player.Creature))
                {
                    actions.Add(
                        new CombatAction.PlayCard(card, ally)
                    );
                }

                break;

            default:
                actions.Add(
                    new CombatAction.PlayCard(card, null)
                );

                break;
        }
    }
}