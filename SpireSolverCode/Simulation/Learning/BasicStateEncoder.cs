using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public sealed class BasicStateEncoder : IStateEncoder
{
    public float[] Encode(
        Player player,
        ICombatState combatState)
    {
        var features = new List<float>();

        /*
         * Player
         */

        features.Add(
            player.Creature.CurrentHp /
            (float)player.Creature.MaxHp
        );

        features.Add(
            player.Creature.Block
        );

        features.Add(
            player.PlayerCombatState.Energy
        );

        /*
         * Combat
         */

        features.Add(
            player.PlayerCombatState.TurnNumber
        );

        features.Add(
            combatState.Enemies.Count(e =>
                e != null &&
                e.IsAlive &&
                e.IsPrimaryEnemy)
        );

        /*
         * Card piles
         */

        features.Add(
            PileType.Hand
                .GetPile(player)
                .Cards
                .Count
        );

        features.Add(
            PileType.Draw
                .GetPile(player)
                .Cards
                .Count
        );

        features.Add(
            PileType.Discard
                .GetPile(player)
                .Cards
                .Count
        );

        features.Add(
            PileType.Exhaust
                .GetPile(player)
                .Cards
                .Count
        );

        /*
         * Aggregate enemy information.
         *
         * We deliberately avoid variable-length enemy vectors for now.
         */

        float totalEnemyHp = combatState.Enemies
            .Where(e =>
                e != null &&
                e.IsAlive &&
                e.IsPrimaryEnemy)
            .Sum(e => (float)e.CurrentHp);

        float totalEnemyMaxHp = combatState.Enemies
            .Where(e =>
                e != null &&
                e.IsAlive &&
                e.IsPrimaryEnemy)
            .Sum(e => (float)e.MaxHp);

        float enemyHpPercent =
            totalEnemyMaxHp > 0
                ? totalEnemyHp / totalEnemyMaxHp
                : 0.0f;

        features.Add(enemyHpPercent);

        return features.ToArray();
    }
}