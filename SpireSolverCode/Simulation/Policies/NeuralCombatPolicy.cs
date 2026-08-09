using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;
using SpireSolver.SpireSolverCode.Simulation.Actions;
using SpireSolver.SpireSolverCode.Simulation.Learning;

namespace SpireSolver.SpireSolverCode.Simulation.Policies;

public sealed class NeuralCombatPolicy : ICombatPolicy
{
    private readonly NeuralActionEvaluator _evaluator;
    private readonly float _explorationRate;

    public NeuralCombatPolicy(
        IStateEncoder stateEncoder,
        IActionEncoder actionEncoder,
        float explorationRate = 0.3f)
    {
        _evaluator = new NeuralActionEvaluator(
            stateEncoder,
            actionEncoder);

        _explorationRate = explorationRate;
    }

    public CombatAction ChooseAction(
        Player player,
        ICombatState combatState,
        IReadOnlyList<CombatAction> legalActions,
        Rng random)
    {
        // Exploration belongs to the training/simulation policy,
        // NOT the evaluator.
        if (random.NextFloat() < _explorationRate)
            return random.NextItem(legalActions);

        var evaluations = _evaluator.Evaluate(
            player,
            combatState,
            legalActions);

        // Engine unavailable / evaluation failed.
        if (evaluations.Count == 0)
            return random.NextItem(legalActions);

        // Evaluate() returns best-first.
        return evaluations[0].Action;
    }
}