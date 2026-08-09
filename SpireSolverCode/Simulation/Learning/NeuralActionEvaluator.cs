using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using SpireSolver.SpireSolverCode.Simulation.Actions;
using SpireSolver.SpireSolverCode.Simulation.Learning.Ipc;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public sealed class NeuralActionEvaluator
{
    private readonly IStateEncoder _stateEncoder;
    private readonly IActionEncoder _actionEncoder;

    public NeuralActionEvaluator(
        IStateEncoder stateEncoder,
        IActionEncoder actionEncoder)
    {
        _stateEncoder = stateEncoder;
        _actionEncoder = actionEncoder;
    }

    public IReadOnlyList<ActionEvaluation> Evaluate(
        Player player,
        ICombatState combatState,
        IReadOnlyList<CombatAction> legalActions)
    {
        EngineClient? client = EngineConnection.Client;

        if (client == null || legalActions.Count == 0)
            return Array.Empty<ActionEvaluation>();

        float[] state =
            _stateEncoder.Encode(player, combatState);

        var evaluations =
            new List<ActionEvaluation>(legalActions.Count);

        foreach (CombatAction action in legalActions)
        {
            float[] input = state
                .Concat(_actionEncoder.Encode(action))
                .ToArray();

            float value = client.Infer(input);

            evaluations.Add(
                new ActionEvaluation(action, value));
        }

        return evaluations
            .OrderByDescending(x => x.Value)
            .ToArray();
    }
}