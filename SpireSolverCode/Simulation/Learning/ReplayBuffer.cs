using System;
using System.Collections.Generic;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public sealed class ReplayBuffer
{
    private readonly List<Experience> _experiences;
    private readonly int _capacity;
    private readonly Random _random = new();

    public int Count => _experiences.Count;

    public int Capacity => _capacity;

    public ReplayBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Replay buffer capacity must be greater than zero."
            );
        }

        _capacity = capacity;
        _experiences = new List<Experience>(capacity);
    }

    public void Add(Experience experience)
    {
        if (_experiences.Count >= _capacity)
        {
            _experiences.RemoveAt(0);
        }

        _experiences.Add(experience);
    }

    public void AddRange(
        IEnumerable<Experience> experiences)
    {
        foreach (Experience experience in experiences)
        {
            Add(experience);
        }
    }

    public IReadOnlyList<Experience> Sample(
        int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Sample size must be greater than zero."
            );
        }

        if (_experiences.Count == 0)
            return Array.Empty<Experience>();

        int sampleCount =
            Math.Min(count, _experiences.Count);

        var result =
            new List<Experience>(sampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            int index =
                _random.Next(_experiences.Count);

            result.Add(
                _experiences[index]
            );
        }

        return result;
    }

    public void Clear()
    {
        _experiences.Clear();
    }
}