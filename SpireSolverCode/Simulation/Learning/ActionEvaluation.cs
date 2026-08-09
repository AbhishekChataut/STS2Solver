using SpireSolver.SpireSolverCode.Simulation.Actions;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public sealed record ActionEvaluation(
    CombatAction Action,
    float Value);