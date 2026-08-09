using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SpireSolver.SpireSolverCode.Simulation.Learning.Ipc;

namespace SpireSolver.SpireSolverCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SpireSolver"; //Used for resource filepath
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Harmony harmony = new(ModId);
        harmony.PatchAll();

        System.Threading.Tasks.Task.Run(EngineConnection.Initialize);
    }
}

