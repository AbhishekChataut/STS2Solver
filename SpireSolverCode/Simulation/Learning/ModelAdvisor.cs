using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using SpireSolver.SpireSolverCode.Simulation.Actions;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public static class ModelAdvisor
{
    private static NeuralActionEvaluator? _evaluator;

    public static bool IsInitialized =>
        _evaluator != null;

    public static void Initialize(
        IStateEncoder stateEncoder,
        IActionEncoder actionEncoder)
    {
        _evaluator = new NeuralActionEvaluator(
            stateEncoder,
            actionEncoder);
    }

    public static IReadOnlyList<ActionEvaluation> Evaluate(
        Player player,
        ICombatState combatState,
        IReadOnlyList<CombatAction> legalActions)
    {
        if (_evaluator == null)
            return Array.Empty<ActionEvaluation>();

        return _evaluator.Evaluate(
            player,
            combatState,
            legalActions);
    }
}