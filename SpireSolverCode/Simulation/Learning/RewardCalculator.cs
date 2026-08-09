using SpireSolver.Simulation;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public static class RewardCalculator
{
    public static float Calculate(
        SimulationResult result)
    {
        if (!result.Won)
            return -1.0f;

        if (result.MaxHp <= 0)
            return 1.0f;

        float hpPercent =
            result.HpRemaining /
            (float)result.MaxHp;

        return 1.0f + hpPercent;
    }
}