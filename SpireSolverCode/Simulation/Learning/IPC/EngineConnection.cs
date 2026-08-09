using System;
using Godot;

namespace SpireSolver.SpireSolverCode.Simulation.Learning.Ipc;

public static class EngineConnection
{
    public static EngineClient? Client { get; private set; }

    public static void Initialize()
    {
        EngineProcessLauncher.Start();
        if (string.IsNullOrEmpty(EngineProcessLauncher.PipeName)) return;

        try
        {
            var client = new EngineClient(EngineProcessLauncher.PipeName);
            client.Connect();
            Client = client;
            GD.Print("[SpireSolver] Engine connected.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireSolver] Engine connection failed: {ex}");
        }
    }
}