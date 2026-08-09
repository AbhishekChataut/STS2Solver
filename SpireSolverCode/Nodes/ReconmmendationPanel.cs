using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using SpireSolver.SpireSolverCode.Simulation;
using SpireSolver.SpireSolverCode.Simulation.Actions;
using SpireSolver.SpireSolverCode.Simulation.Learning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpireSolver.SpireSolverCode.Nodes;

/// <summary>
/// Small always-on HUD in the top-right corner showing the model's
/// current top recommendation and its ranking of all legal actions.
///
/// Refreshes on a Timer child node rather than a manual polling loop, so
/// it starts/stops with the node's own lifecycle for free - no extra
/// bookkeeping needed to recover from RestartRoom or a disabled tree.
/// The actual cost of a refresh (whether it needs to call the inference
/// engine at all) is decided by <see cref="ModelAdvisor"/>, not here.
/// </summary>
public sealed class RecommendationPanel
{
    public const string RootName = "SpireSolverRecommendationHud";

    private const float RefreshIntervalSeconds = 0.5f;
    private const int MaxRowsShown = 8;

    private readonly Control _root;
    private readonly Label _status;
    private readonly VBoxContainer _list;

    private Player? _player;
    private IRunState? _runState;

    public Control Root => _root;

    public RecommendationPanel()
    {
        _root = new Control
        {
            Name = RootName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        // Top-right corner, under the top bar.
        _root.AnchorLeft = 1f;
        _root.AnchorRight = 1f;
        _root.OffsetLeft = -390f;
        _root.OffsetRight = -20f;
        _root.OffsetTop = 80f;
        _root.OffsetBottom = 430f;

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddThemeStyleboxOverride("panel", BuildBackground());
        _root.AddChild(panel);

        var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(content);

        var title = new Label
        {
            Text = "SpireSolver",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        content.AddChild(title);

        _status = new Label
        {
            Text = "Waiting for combat...",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        content.AddChild(_status);

        content.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });

        _list = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddChild(_list);

        var refreshTimer = new Godot.Timer
        {
            WaitTime = RefreshIntervalSeconds,
            Autostart = true
        };
        refreshTimer.Timeout += Refresh;
        _root.AddChild(refreshTimer);
    }

    private static StyleBoxFlat BuildBackground()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.02f, 0.04f, 0.58f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12f,
            ContentMarginRight = 12f,
            ContentMarginTop = 10f,
            ContentMarginBottom = 10f
        };
    }

    // ----------------------------------------------------------------
    // Context
    // ----------------------------------------------------------------

    public void SetContext(Player player, IRunState runState)
    {
        // RestartRoom creates fresh combat/player objects, so always
        // replace rather than merge with whatever context we had before.
        _player = player;
        _runState = runState;

        Refresh();
    }

    // ----------------------------------------------------------------
    // Refresh
    // ----------------------------------------------------------------

    private void Refresh()
    {
        if (!GodotObject.IsInstanceValid(_root) || !_root.IsInsideTree())
            return;

        try
        {
            RefreshUnsafe();
        }
        catch (Exception e)
        {
            // During RestartRoom the state may briefly be invalid.
            // Hide rather than show something stale; the next tick
            // will try again.
            _root.Visible = false;

            GD.Print($"[SpireSolver] Advisor temporarily unavailable: {e.GetType().Name}");
        }
    }

    private void RefreshUnsafe()
    {
        if (SimulationRunner.IsRunning)
        {
            _root.Visible = false;
            return;
        }

        _root.Visible = true;

        if (_player == null)
        {
            ShowWaiting("Waiting for player...");
            return;
        }

        if (!CombatManager.Instance.IsInProgress)
        {
            ShowWaiting("Waiting for combat...");
            return;
        }

        CombatState? combatState = CombatManager.Instance.DebugOnlyGetState();

        if (combatState == null)
        {
            ShowWaiting("Waiting for combat state...");
            return;
        }

        IReadOnlyList<CombatAction> actions =
            CombatActionGenerator.GetLegalActions(_player, combatState);

        if (actions.Count == 0)
        {
            ShowWaiting("No playable cards");
            return;
        }

        IReadOnlyList<ActionEvaluation> evaluations =
            ModelAdvisor.Evaluate(_player, combatState, actions);

        if (evaluations.Count == 0)
        {
            ShowWaiting("Model unavailable");
            return;
        }

        ShowEvaluations(evaluations);
    }

    // ----------------------------------------------------------------
    // Display
    // ----------------------------------------------------------------

    private void ShowWaiting(string message)
    {
        _status.Text = message;
        ClearChildren(_list);
    }

    private void ShowEvaluations(IReadOnlyList<ActionEvaluation> evaluations)
    {
        ClearChildren(_list);

        _status.Text = $"★ {FormatAction(evaluations[0].Action)}";

        foreach (ActionEvaluation evaluation in evaluations.Take(MaxRowsShown))
        {
            _list.AddChild(new Label
            {
                Text = $"{FormatAction(evaluation.Action)}    {evaluation.Value:F3}",
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
        }
    }

    private static string FormatAction(CombatAction action)
    {
        return action switch
        {
            CombatAction.PlayCard { Target: null } play =>
                play.Card.Id.Entry,

            CombatAction.PlayCard play =>
                $"{play.Card.Id.Entry} -> Target",

            CombatAction.EndTurn =>
                "End Turn",

            _ =>
                "Unknown"
        };
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
            child.QueueFree();
    }
}