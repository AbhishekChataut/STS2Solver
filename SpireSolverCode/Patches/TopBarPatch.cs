// Patches/TopBarPatch.cs

using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Multiplayer.Replay;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Runs;
using SpireSolver.SpireSolverCode.Nodes;

[HarmonyPatch]
public static class TopBarPatch
{
    [HarmonyPatch(typeof(NTopBar), nameof(NTopBar._Ready))]
    [HarmonyPostfix]
    public static void Postfix(NTopBar __instance)
    {
        InjectButton(__instance);

        Node topBarParent = __instance.GetParent();
        if (topBarParent != null)
            SpireSolverScreen.Inject(topBarParent);
    }

    [HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
    [HarmonyPostfix]
    public static void InitPostfix(NTopBar __instance, IRunState runState)
    {
        var player = LocalContext.GetMe((IPlayerCollection)runState);
        SpireSolverScreen.SetContext(player, runState);
    }

    private static void InjectButton(NTopBar instance)
    {
        if (instance.Map.GetParent().HasNode("MyModButton")) return;

        Node parent = instance.Map.GetParent();
        if (parent == null) return;

        int insertIndex = instance.Map.GetIndex();

        var btn = new Button();
        btn.Name = "MyModButton";
        btn.Text = "Solve";
        btn.TooltipText = "Open Spire Solver";
        btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        btn.SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter;
        btn.Pressed += OnModButtonPressed;

        parent.AddChild(btn);
        parent.MoveChild(btn, insertIndex);
    }

    private static void OnModButtonPressed()
    {
        GD.Print("[SpireSolver] Solve button pressed");

        if (SpireSolverScreen.IsOpen())
            SpireSolverScreen.Close();
        else
            SpireSolverScreen.Open();
    }
}