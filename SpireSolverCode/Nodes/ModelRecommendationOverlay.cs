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

public static class ModelRecommendationOverlay
{
    private static Control? _root;
    private static Label? _status;
    private static VBoxContainer? _list;

    private static Player? _player;
    private static IRunState? _runState;

    private static bool _refreshQueued;

    // ----------------------------------------------------------------
    // Injection
    // ----------------------------------------------------------------
    public static void Inject(Node parent)
    {
        try
        {
            // IMPORTANT:
            // A previous RestartRoom may have destroyed the old root.
            if (_root != null)
            {
                if (GodotObject.IsInstanceValid(_root) &&
                    _root.IsInsideTree())
                {
                    // Current HUD is genuinely alive.
                    return;
                }

                // Stale reference from the previous scene.
                _root = null;
                _status = null;
                _list = null;
            }

            Control root = BuildUi();

            Callable.From(() =>
            {
                try
                {
                    if (!GodotObject.IsInstanceValid(parent) ||
                        !GodotObject.IsInstanceValid(root))
                    {
                        return;
                    }

                    parent.AddChild(root);

                    _root = root;

                    GD.Print(
                        "[SpireSolver] Recommendation HUD added");

                    // Combat may not be completely initialized yet.
                    // Start our retry loop.
                    ScheduleRefresh();
                }
                catch (Exception e)
                {
                    GD.PrintErr(
                        "[SpireSolver] Recommendation HUD add failed: " +
                        e.Message);
                }
            }).CallDeferred();

            GD.Print(
                "[SpireSolver] Recommendation HUD queued");
        }
        catch (Exception e)
        {
            GD.PrintErr(
                "[SpireSolver] Recommendation HUD injection failed: " +
                e.Message);
        }
    }
    
    // ----------------------------------------------------------------
    // Context
    // ----------------------------------------------------------------


    public static void SetContext(
        Player player,
        IRunState runState)
    {
        // Always replace these.
        //
        // RestartRoom creates fresh combat/player objects, so we do NOT
        // want to retain references associated with the old room.
        _player = player;
        _runState = runState;

        GD.Print(
            "[SpireSolver] Recommendation HUD received new context");

        ScheduleRefresh();
    }

    private static void ScheduleRefresh()
    {
        if (_refreshQueued)
            return;

        _refreshQueued = true;

        Callable.From(() =>
        {
            _refreshQueued = false;

            Refresh();

            // Continue checking periodically.
            //
            // This makes the HUD recover automatically from:
            // - combat startup
            // - RestartRoom
            // - temporarily disabled nodes
            // - cards being played
            //
            // Refresh itself refuses to infer while simulations run.
            ScheduleNextRefresh();

        }).CallDeferred();
    }
    
    private static void ScheduleNextRefresh()
    {
        try
        {
            if (_root == null ||
                !GodotObject.IsInstanceValid(_root) ||
                !_root.IsInsideTree())
            {
                return;
            }

            SceneTree tree = _root.GetTree();

            if (tree == null)
                return;

            SceneTreeTimer timer =
                tree.CreateTimer(0.25);

            timer.Timeout += ScheduleRefresh;
        }
        catch
        {
            // Scene may currently be rebuilding.
        }
    }

    // ----------------------------------------------------------------
    // UI construction
    // ----------------------------------------------------------------

    private static Control BuildUi()
    {
        var root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        // Top-right corner.
        root.AnchorLeft = 1f;
        root.AnchorRight = 1f;
        root.AnchorTop = 0f;
        root.AnchorBottom = 0f;

        root.OffsetLeft = -390f;
        root.OffsetRight = -20f;

        // Under top bar.
        root.OffsetTop = 80f;
        root.OffsetBottom = 430f;

        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        panel.SetAnchorsPreset(
            Control.LayoutPreset.FullRect);

        var background = new StyleBoxFlat
        {
            BgColor = new Color(
                0.02f,
                0.02f,
                0.04f,
                0.58f),

            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,

            ContentMarginLeft = 12f,
            ContentMarginRight = 12f,
            ContentMarginTop = 10f,
            ContentMarginBottom = 10f
        };

        panel.AddThemeStyleboxOverride(
            "panel",
            background);

        root.AddChild(panel);

        var content = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        panel.AddChild(content);

        var title = new Label
        {
            Text = "SpireSolver",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        title.AddThemeFontSizeOverride(
            "font_size",
            18);

        content.AddChild(title);

        _status = new Label
        {
            Text = "Waiting for combat...",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        content.AddChild(_status);

        var separator = new HSeparator
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        content.AddChild(separator);

        _list = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        content.AddChild(_list);

        // Start visible while debugging.
        root.Visible = true;

        return root;
    }

    // ----------------------------------------------------------------
    // Refresh
    // ----------------------------------------------------------------

    
public static void Refresh()
{
    try
    {
        if (_root == null ||
            !GodotObject.IsInstanceValid(_root) ||
            !_root.IsInsideTree())
        {
            return;
        }

        // --------------------------------------------------------
        // Simulation running
        // --------------------------------------------------------

        if (SimulationRunner.IsRunning)
        {
            _root.Visible = false;
            return;
        }

        // From this point forward we're back in the real game,
        // so make the HUD available again.
        _root.Visible = true;

        // --------------------------------------------------------
        // Player
        // --------------------------------------------------------

        if (_player == null)
        {
            ShowWaiting(
                "Waiting for player...");

            return;
        }

        // --------------------------------------------------------
        // Combat
        // --------------------------------------------------------

        if (!CombatManager.Instance.IsInProgress)
        {
            ShowWaiting(
                "Waiting for combat...");

            return;
        }

        CombatState? combatState =
            CombatManager.Instance.DebugOnlyGetState();

        if (combatState == null)
        {
            ShowWaiting(
                "Waiting for combat state...");

            return;
        }

        // --------------------------------------------------------
        // Legal actions
        // --------------------------------------------------------

        IReadOnlyList<CombatAction> actions =
            CombatActionGenerator.GetLegalActions(
                _player,
                combatState);

        if (actions.Count == 0)
        {
            ShowWaiting(
                "No playable cards");

            return;
        }

        // --------------------------------------------------------
        // Model
        // --------------------------------------------------------

        IReadOnlyList<ActionEvaluation> evaluations =
            ModelAdvisor.Evaluate(
                _player,
                combatState,
                actions);

        if (evaluations.Count == 0)
        {
            ShowWaiting(
                "Model unavailable");

            return;
        }

        ShowEvaluations(evaluations);
    }
    catch (Exception e)
    {
        // Don't hide the entire HUD here.
        //
        // During RestartRoom the state may temporarily be invalid.
        // The next refresh will simply try again.

        if (_root != null &&
            GodotObject.IsInstanceValid(_root))
        {
            _root.Visible = false;
        }

        GD.Print(
            "[SpireSolver] Advisor temporarily unavailable: " +
            e.GetType().Name);
    }
}

    // ----------------------------------------------------------------
    // Display
    // ----------------------------------------------------------------

    private static void ShowWaiting(
        string message)
    {
        if (_root == null ||
            !GodotObject.IsInstanceValid(_root))
        {
            return;
        }

        _root.Visible = true;

        if (_status != null &&
            GodotObject.IsInstanceValid(_status))
        {
            _status.Text = message;
        }

        if (_list != null &&
            GodotObject.IsInstanceValid(_list))
        {
            ClearChildren(_list);
        }
    }

    private static void ShowEvaluations(
        IReadOnlyList<ActionEvaluation> evaluations)
    {
        if (_root == null ||
            _status == null ||
            _list == null)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(_root) ||
            !GodotObject.IsInstanceValid(_status) ||
            !GodotObject.IsInstanceValid(_list))
        {
            return;
        }

        ClearChildren(_list);

        ActionEvaluation best =
            evaluations[0];

        _status.Text =
            $"★ {FormatAction(best.Action)}";

        foreach (ActionEvaluation evaluation
                 in evaluations.Take(8))
        {
            string actionName =
                FormatAction(evaluation.Action);

            var row = new Label
            {
                Text =
                    $"{actionName}    {evaluation.Value:F3}",

                MouseFilter =
                    Control.MouseFilterEnum.Ignore
            };

            _list.AddChild(row);
        }

        _root.Visible = true;
    }

    // ----------------------------------------------------------------
    // Formatting
    // ----------------------------------------------------------------

    private static string FormatAction(
        CombatAction action)
    {
        switch (action)
        {
            case CombatAction.PlayCard play:
            {
                string cardName =
                    play.Card.Id.Entry;

                if (play.Target == null)
                    return cardName;

                return $"{cardName} -> Target";
            }

            case CombatAction.EndTurn:
                return "End Turn";

            default:
                return "Unknown";
        }
    }

    private static void ClearChildren(
        Node parent)
    {
        foreach (Node child in parent.GetChildren())
            child.QueueFree();
    }
}