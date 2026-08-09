using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using SpireSolver.SpireSolverCode.Simulation.Actions;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public static class ModelAdvisor
{
    private static readonly NeuralActionEvaluator Evaluator =
        new(
            new BasicStateEncoder(),
            new BasicActionEncoder());

    public static IReadOnlyList<ActionEvaluation> Evaluate(
        Player player,
        ICombatState combatState,
        IReadOnlyList<CombatAction> legalActions)
    {
        try
        {
            return Evaluator.Evaluate(
                player,
                combatState,
                legalActions);
        }
        catch
        {
            return Array.Empty<ActionEvaluation>();
        }
    }
}