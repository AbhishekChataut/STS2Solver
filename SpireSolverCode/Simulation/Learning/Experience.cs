namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public sealed class Experience
{
    public required float[] State { get; init; }

    public required float[] Action { get; init; }

    public float Reward { get; set; }
}