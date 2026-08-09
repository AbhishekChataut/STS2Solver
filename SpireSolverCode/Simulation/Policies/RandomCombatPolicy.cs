using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;
using SpireSolver.SpireSolverCode.Simulation.Actions;

namespace SpireSolver.SpireSolverCode.Simulation.Policies;

public sealed class RandomCombatPolicy : ICombatPolicy
{
    public CombatAction ChooseAction(
        Player player,
        ICombatState combatState,
        IReadOnlyList<CombatAction> legalActions,
        Rng random)
    {
        return random.NextItem(legalActions);
    }
}