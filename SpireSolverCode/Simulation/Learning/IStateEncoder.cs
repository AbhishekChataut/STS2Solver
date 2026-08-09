using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public interface IStateEncoder
{
    float[] Encode(
        Player player,
        ICombatState combatState);
}