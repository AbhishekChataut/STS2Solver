using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using SpireSolver.SpireSolverCode.Simulation.Actions;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

/// <summary>
/// Entry point the recommendation HUD polls for advice.
///
/// <see cref="NeuralActionEvaluator"/> is also used by
/// <see cref="Policies.NeuralCombatPolicy"/> to make real decisions during
/// simulations, so it always evaluates fresh - that's correct there.
/// But the HUD polls on a timer regardless of whether the player has
/// actually done anything, and every evaluation is a blocking pipe round
/// trip to the external inference process per legal action. This wraps
/// the evaluator with a cache keyed on everything that can change the
/// recommendation, so a poll that finds nothing new returns the previous
/// result instead of re-running inference.
/// </summary>
public static class ModelAdvisor
{
    private static readonly NeuralActionEvaluator Evaluator =
        new(new BasicStateEncoder(), new BasicActionEncoder());

    private static Player? _cachedPlayer;
    private static StateSignature? _cachedSignature;

    private static IReadOnlyList<ActionEvaluation> _cachedEvaluations =
        Array.Empty<ActionEvaluation>();

    public static IReadOnlyList<ActionEvaluation> Evaluate(
        Player player,
        ICombatState combatState,
        IReadOnlyList<CombatAction> legalActions)
    {
        var signature = StateSignature.Capture(player, combatState, legalActions);

        if (ReferenceEquals(player, _cachedPlayer) && _cachedSignature == signature)
            return _cachedEvaluations;

        try
        {
            _cachedEvaluations = Evaluator.Evaluate(player, combatState, legalActions);
        }
        catch
        {
            _cachedEvaluations = Array.Empty<ActionEvaluation>();
        }

        _cachedPlayer = player;
        _cachedSignature = signature;

        return _cachedEvaluations;
    }

    /// <summary>
    /// Everything that can change what the model recommends, cheap to
    /// read straight from game state without touching the inference
    /// engine. Two polls with an equal signature are guaranteed to
    /// produce the same evaluations, so the second poll can skip
    /// inference entirely. Mirrors the inputs <see cref="BasicStateEncoder"/>
    /// actually feeds the model, plus the legal action set itself.
    /// </summary>
    private readonly record struct StateSignature(
        int TurnNumber,
        float Energy,
        int PlayerHp,
        int PlayerBlock,
        float EnemyHpPercent,
        int HandCount,
        int DrawCount,
        int DiscardCount,
        int ExhaustCount,
        string ActionsKey)
    {
        public static StateSignature Capture(
            Player player,
            ICombatState combatState,
            IReadOnlyList<CombatAction> legalActions)
        {
            var primaryEnemies = combatState.Enemies
                .Where(e => e != null && e.IsAlive && e.IsPrimaryEnemy)
                .ToList();

            float totalEnemyHp = primaryEnemies.Sum(e => (float)e.CurrentHp);
            float totalEnemyMaxHp = primaryEnemies.Sum(e => (float)e.MaxHp);

            return new StateSignature(
                player.PlayerCombatState.TurnNumber,
                player.PlayerCombatState.Energy,
                player.Creature.CurrentHp,
                player.Creature.Block,
                totalEnemyMaxHp > 0 ? totalEnemyHp / totalEnemyMaxHp : 0f,
                PileType.Hand.GetPile(player).Cards.Count,
                PileType.Draw.GetPile(player).Cards.Count,
                PileType.Discard.GetPile(player).Cards.Count,
                PileType.Exhaust.GetPile(player).Cards.Count,
                BuildActionsKey(legalActions));
        }

        // Reference identity for targets is good enough here: a false
        // cache hit just means the HUD reuses last poll's ranking for
        // one more tick, not a crash or a silently wrong game action.
        private static string BuildActionsKey(IReadOnlyList<CombatAction> legalActions)
        {
            return string.Join('|', legalActions.Select(action => action switch
            {
                CombatAction.PlayCard play =>
                    $"{play.Card.Id.Entry}:{play.Target?.GetHashCode()}",

                CombatAction.EndTurn =>
                    "EndTurn",

                _ =>
                    action.GetType().Name
            }));
        }
    }
}