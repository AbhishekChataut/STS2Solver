using SpireSolver.SpireSolverCode.Simulation.Actions;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public interface IActionEncoder
{
    float[] Encode(CombatAction action);
}