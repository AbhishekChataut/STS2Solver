using System.Collections.Generic;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public sealed class SimulationTrajectory
{
    private readonly List<Experience> _experiences = new();

    public IReadOnlyList<Experience> Experiences =>
        _experiences;

    public void Record(
        float[] state,
        float[] action)
    {
        _experiences.Add(
            new Experience
            {
                State = state,
                Action = action,
                Reward = 0.0f
            }
        );
    }

    public void AssignReward(float reward)
    {
        foreach (Experience experience in _experiences)
        {
            experience.Reward = reward;
        }
    }
}