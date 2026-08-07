// Patches/TopBarPatch.cs

using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Replay;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SpireSolver.Simulation;
using SpireSolver.SpireSolverCode.Nodes;


[HarmonyPatch]
public static class TopBarPatch
{
private static CancellationTokenSource? _autoCts;
    
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
        btn.Pressed += OnModButtonPressed;  // ← wired to the corrected handler below

        var btn2 = new Button();
        btn2.Name = "MyModButton2";
        btn2.Text = "Play Turn";
        btn2.TooltipText = "Play Turn";
        btn2.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        btn2.SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter;
        btn2.Pressed += OnPlayTurnButtonPressed;
        
        var btn3 = new Button();
        btn3.Name = "MyModButton3";
        btn3.Text = "Play Combat";
        btn3.TooltipText = "Play Combat";
        btn3.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        btn3.SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter;
        btn3.Pressed += OnPlayCombatButtonPressed;

        parent.AddChild(btn3);
        parent.MoveChild(btn3, insertIndex);
        
        parent.AddChild(btn2);
        parent.MoveChild(btn2, insertIndex + 1);
        
        parent.AddChild(btn);
        parent.MoveChild(btn, insertIndex + 2);
    }

    private static void OnModButtonPressed()
    {
        GD.Print("[SpireSolver] Solve button pressed");

        // Use static methods - no .Instance needed anymore
        if (SpireSolverScreen.IsOpen())
            SpireSolverScreen.Close();
        else
            SpireSolverScreen.Open();
    }

    private static async void OnPlayTurnButtonPressed()
    {
        GD.Print("[SpireSolver] Play turn button pressed");
        _autoCts?.Cancel();
        _autoCts = new CancellationTokenSource();

        var rng = new Rng(12345);
        SimulationState.Start();

        await Autoplayer.PlayTurn(rng, _autoCts.Token);
        if (!SimulationState.IsFinished) return;
        GD.Print("[SpireSolver] Restarting simulation");

        await Restarter.RestartRoom();
    }

    private static void OnPlayCombatButtonPressed()
    {
        GD.Print("[SpireSolver] Play combat button pressed");
        _autoCts?.Cancel();
        _autoCts = new CancellationTokenSource();
        
        TaskHelper.RunSafely(PlayCombatLoop(_autoCts.Token));
    }
    
    private static CombatState? GetCombatState()
    {
        if (!CombatManager.Instance.IsInProgress)
            return null;

        return CombatManager.Instance.DebugOnlyGetState();
    }

    private static async Task PlayCombatLoop(CancellationToken token)
    {
        const int simulationCount = 10;

        for (int i = 0; i < simulationCount; i++)
        {
            token.ThrowIfCancellationRequested();

            GD.Print(
                $"[SpireSolver] Simulation {i + 1}/{simulationCount}"
            );

            var rng = new Rng((ulong)SimulationState.index);

            SimulationState.Start();

            Player player = LocalContext.GetMe(
                RunManager.Instance.DebugOnlyGetState()
            );

            CombatState? combatState = GetCombatState();

            CardPile drawPile = PileType.Draw.GetPile(player);
            drawPile.RandomizeOrderInternal(
                player,
                rng,
                combatState
            );

            while (!SimulationState.IsFinished)
            {
                token.ThrowIfCancellationRequested();

                await Autoplayer.PlayTurn(rng, token);
                await Task.Delay(5, token);
            }

            SimulationState.Save(player);
            SimulationState.index++;

            // Don't restart after the final simulation.
            if (i < simulationCount - 1)
            {
                GD.Print("[SpireSolver] Restarting simulation");

                await Restarter.RestartRoom(token);
            }
        }

        GD.Print("[SpireSolver] Completed all 50 simulations");
    }
}