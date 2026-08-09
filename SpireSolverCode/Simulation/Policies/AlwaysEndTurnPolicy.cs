using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;
using SpireSolver.SpireSolverCode.Simulation.Actions;

namespace SpireSolver.SpireSolverCode.Simulation.Policies;

public sealed class AlwaysEndTurnPolicy : ICombatPolicy
{
    public CombatAction ChooseAction(
        Player player,
        ICombatState combatState,
        IReadOnlyList<CombatAction> legalActions,
        Rng random)
    {
        return legalActions
            .OfType<CombatAction.EndTurn>()
            .First();
    }
}