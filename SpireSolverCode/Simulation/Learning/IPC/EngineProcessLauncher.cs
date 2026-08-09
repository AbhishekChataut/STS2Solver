using System;
using System.Diagnostics;
using System.IO;
using Godot;

namespace SpireSolver.SpireSolverCode.Simulation.Learning.Ipc;

public static class EngineProcessLauncher
{
    private static Process? _process;

    public static string PipeName { get; private set; } = "";

    public static void Start()
    {
        if (_process is { HasExited: false })
            return;

        PipeName = $"sts2solver-{Guid.NewGuid():N}";

        string modDir = Path.GetDirectoryName(
            typeof(EngineProcessLauncher).Assembly.Location)!;

        string exePath = Path.Combine(
            modDir, "engine", "Engine.exe");

        if (!File.Exists(exePath))
        {
            GD.PrintErr(
                $"[SpireSolver] Engine exe not found at {exePath}. " +
                "Did you publish the engine project?");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = PipeName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _process = Process.Start(psi);
        if (_process == null)
        {
            GD.PrintErr("[SpireSolver] Failed to start engine process.");
            return;
        }

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) GD.Print($"[Engine] {e.Data}");
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) GD.PrintErr($"[Engine] {e.Data}");
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();

        GD.Print($"[SpireSolver] Launched engine process (pipe '{PipeName}').");
    }

    public static void Stop()
    {
        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); }
            catch { /* best effort on shutdown */ }
        }
        _process = null;
    }
}