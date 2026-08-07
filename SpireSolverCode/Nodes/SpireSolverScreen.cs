using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using SpireSolver.Simulation;
using System;
using System.Collections.Generic;

namespace SpireSolver.SpireSolverCode.Nodes;

public static class SpireSolverScreen
{
    private static Control? _root;
    private static bool _isOpen;

    private static Player? _player;
    private static IRunState? _runState;

    private static CombatLogPanel? _combatLogPanel;
    private static SimulationResultsPanel? _simulationResultsPanel;

    private static Button? _playCombatButton;

    private static readonly List<Control> TabPanels = new();

    public static bool IsOpen() => _isOpen;

    public static void SetContext(Player player, IRunState runState)
    {
        _player = player;
        _runState = runState;

        _combatLogPanel?.SetContext(player, runState);
    }

    public static void SetSimulationResults(
        IReadOnlyCollection<SimulationResult> results)
    {
        _simulationResultsPanel?.SetResults(results);
    }

    public static void Inject(Node parent)
    {
        if (parent.HasNode("SpireSolverScreen"))
            return;

        _root = new Control
        {
            Name = "SpireSolverScreen",
            Visible = false,
            ProcessMode = Node.ProcessModeEnum.Disabled,
            MouseFilter = Control.MouseFilterEnum.Stop
        };

        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        BuildUi(_root);

        parent.CallDeferred("add_child", _root);

        // Wire up simulation events once, for the lifetime of the screen.
        SimulationRunner.SimulationCompleted += OnSimulationCompleted;
        SimulationRunner.BatchCompleted += OnBatchCompleted;

        _root.TreeExiting += () =>
        {
            SimulationRunner.SimulationCompleted -= OnSimulationCompleted;
            SimulationRunner.BatchCompleted -= OnBatchCompleted;
        };
    }

    private static void BuildUi(Control root)
    {
        TabPanels.Clear();

        // Dark backdrop.
        var backdrop = new ColorRect
        {
            Color = new Color(0.02f, 0.02f, 0.05f, 0.92f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };

        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        // Invisible full-screen button so clicking outside the panel closes it.
        var backdropButton = new Button
        {
            Flat = true
        };

        backdropButton.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdropButton.Pressed += Close;

        root.AddChild(backdropButton);

        // Centered 1100x720 main panel.
        var anchor = new Control
        {
            CustomMinimumSize = new Vector2(1100f, 720f),
            Position = new Vector2(-550f, -360f),
            MouseFilter = Control.MouseFilterEnum.Pass
        };

        anchor.SetAnchorsPreset(Control.LayoutPreset.Center);
        root.AddChild(anchor);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        anchor.AddChild(panel);

        var outerVBox = new VBoxContainer();
        outerVBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(outerVBox);

        outerVBox.AddChild(BuildHeader());
        outerVBox.AddChild(new HSeparator());
        outerVBox.AddChild(BuildTabBar());
        outerVBox.AddChild(BuildContentArea());
    }

    private static Control BuildHeader()
    {
        var header = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 48f)
        };

        var title = new Label
        {
            Text = "⚙ SpireSolver",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };

        title.AddThemeFontSizeOverride("font_size", 26);
        header.AddChild(title);

        _playCombatButton = new Button
        {
            Text = "▶ Play Combat",
            TooltipText = "Run a batch of autoplayed combat simulations",
            CustomMinimumSize = new Vector2(130f, 36f)
        };

        _playCombatButton.Pressed += OnPlayCombatPressed;
        header.AddChild(_playCombatButton);

        var refreshButton = new Button
        {
            Text = "🔄 Refresh",
            TooltipText = "Refresh the current tab data",
            CustomMinimumSize = new Vector2(110f, 36f)
        };

        refreshButton.Pressed += Refresh;
        header.AddChild(refreshButton);

        var resetButton = new Button
        {
            Text = "⏮ Reset Combat",
            TooltipText = "Restore to saved combat start state",
            CustomMinimumSize = new Vector2(140f, 36f)
        };

        resetButton.Pressed += () => Restarter.RestartRoom();
        header.AddChild(resetButton);

        var closeButton = new Button
        {
            Text = "✕  Close",
            CustomMinimumSize = new Vector2(80f, 36f)
        };

        closeButton.Pressed += Close;
        header.AddChild(closeButton);

        return header;
    }

    private static Control BuildTabBar()
    {
        var tabBar = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 40f)
        };

        AddTabButton(
            tabBar,
            "This Turn",
            () => ShowTab(_combatLogPanel?.ThisTurnRoot));

        AddTabButton(
            tabBar,
            "Full Combat Log",
            () => ShowTab(_combatLogPanel?.FullLogRoot));

        AddTabButton(
            tabBar,
            "Simulation Results",
            () => ShowTab(_simulationResultsPanel?.Root));

        return tabBar;
    }

    private static Control BuildContentArea()
    {
        var contentArea = new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        _combatLogPanel = new CombatLogPanel();

        if (_player != null && _runState != null)
            _combatLogPanel.SetContext(_player, _runState);

        _simulationResultsPanel = new SimulationResultsPanel();
        _simulationResultsPanel.SetResults(SimulationState.Simulations);

        AddPanel(contentArea, _combatLogPanel.ThisTurnRoot);
        AddPanel(contentArea, _combatLogPanel.FullLogRoot);
        AddPanel(contentArea, _simulationResultsPanel.Root);

        return contentArea;
    }

    private static void AddPanel(
        Control parent,
        Control panel)
    {
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.Visible = false;

        parent.AddChild(panel);
        TabPanels.Add(panel);
    }

    private static void AddTabButton(
        HBoxContainer bar,
        string label,
        Action onPress)
    {
        var button = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(150f, 36f)
        };

        button.Pressed += onPress;
        bar.AddChild(button);
    }

    private static void ShowTab(Control? target)
    {
        foreach (var panel in TabPanels)
            panel.Visible = panel == target;

        // Refresh the combat logs when switching to one of them.
        if (target == _combatLogPanel?.ThisTurnRoot ||
            target == _combatLogPanel?.FullLogRoot)
        {
            _combatLogPanel.Populate();
        }
    }

    public static void Open()
    {
        if (_root == null || !_root.IsInsideTree())
            return;

        _isOpen = true;

        _root.Visible = true;
        _root.ProcessMode = Node.ProcessModeEnum.Inherit;
        _root.MoveToFront();

        // Always open on This Turn.
        ShowTab(_combatLogPanel?.ThisTurnRoot);
    }

    public static void Close()
    {
        if (_root == null)
            return;

        _isOpen = false;

        _root.Visible = false;
        _root.ProcessMode = Node.ProcessModeEnum.Disabled;
    }

    private static void OnPlayCombatPressed()
    {
        if (SimulationRunner.IsRunning)
        {
            GD.Print("[SpireSolver] Stopping simulation run");
            SimulationRunner.Stop();
            UpdatePlayCombatButton();
            return;
        }

        GD.Print("[SpireSolver] Starting simulation run");
        SimulationRunner.Start();
        UpdatePlayCombatButton();
    }

    private static void OnSimulationCompleted()
    {
        // SimulationRunner's loop runs via async continuations; hop back
        // onto the main thread before touching the scene tree.
        // Node.CallDeferred(string) can only target methods on that node
        // itself, so a wrapped Callable is used to defer this static
        // method instead.
        Callable.From(RefreshSimulationResults).CallDeferred();
    }

    private static void OnBatchCompleted()
    {
        Callable.From(RefreshAfterBatch).CallDeferred();
    }

    private static void RefreshSimulationResults()
    {
        SetSimulationResults(SimulationState.Simulations);
    }

    private static void RefreshAfterBatch()
    {
        SetSimulationResults(SimulationState.Simulations);
        UpdatePlayCombatButton();
    }

    private static void UpdatePlayCombatButton()
    {
        if (_playCombatButton == null)
            return;

        _playCombatButton.Text = SimulationRunner.IsRunning
            ? "⏹ Stop"
            : "▶ Play Combat";
    }

    private static void Refresh()
    {
        _combatLogPanel?.Populate();
        SetSimulationResults(SimulationState.Simulations);
    }
}