using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace SpireSolver.SpireSolverCode.Simulation.Actions;

public abstract record CombatAction
{
    public sealed record PlayCard(
        CardModel Card,
        Creature? Target
    ) : CombatAction;

    public sealed record EndTurn : CombatAction;
}