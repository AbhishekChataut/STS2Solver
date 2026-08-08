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
    private static TurnDecisionPanel? _turnDecisionPanel;
    private static SimulationResultsPanel? _simulationResultsPanel;

    private static Button? _playCombatButton;
    private static SpinBox? _simulationCountInput;

    private static readonly List<Control> TabPanels = new();

    // Index into TabPanels (0 = This Turn, 1 = Full Combat Log,
    // 2 = Simulation Results). Tracked separately from TabPanels itself
    // because TabPanels gets rebuilt from scratch every time the screen
    // is re-injected (e.g. after RestartRoom tears down and rebuilds the
    // run's scene tree), but we still want to remember which tab the
    // user was looking at.
    private static int _activeTabIndex;

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

        // RestartRoom() tears down and rebuilds the run's whole scene
        // tree, which destroys the previous NTopBar (and therefore this
        // screen's old root Control) and causes a fresh Inject() call.
        // A brand new Control always starts closed, so without this the
        // screen would silently close on every restart mid-simulation.
        // _isOpen is a static field that survives the old root's
        // destruction, so it still tells us whether the screen should
        // come back up once the new one is ready.
        bool reopenAfterRebuild = _isOpen;

        var root = new Control
        {
            Name = "SpireSolverScreen",
            Visible = false,
            ProcessMode = Node.ProcessModeEnum.Disabled,
            MouseFilter = Control.MouseFilterEnum.Stop
        };

        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        _root = root;

        BuildUi(root);
        UpdatePlayCombatButton();

        if (reopenAfterRebuild)
        {
            void ReopenOnceInTree()
            {
                root.TreeEntered -= ReopenOnceInTree;
                Open();
            }

            root.TreeEntered += ReopenOnceInTree;
        }

        parent.CallDeferred("add_child", root);

        // Wire up simulation events once, for the lifetime of the screen.
        SimulationRunner.SimulationCompleted += OnSimulationCompleted;
        SimulationRunner.BatchCompleted += OnBatchCompleted;

        root.TreeExiting += () =>
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

        _simulationCountInput = new SpinBox
        {
            MinValue = 1,
            MaxValue = 10000,
            Step = 1,
            Value = 50,
            CustomMinimumSize = new Vector2(80f, 36f),
            TooltipText = "Number of combat simulations to run"
        };

        _simulationCountInput.AllowGreater = true;
        _simulationCountInput.AllowLesser = false;

        header.AddChild(_simulationCountInput);

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
            Text = "🔄 Reset",
            TooltipText = "Reset the current tab data",
            CustomMinimumSize = new Vector2(110f, 36f)
        };

        refreshButton.Pressed += ResetData;
        header.AddChild(refreshButton);

        var resetButton = new Button
        {
            Text = "⏮ Reset Combat",
            TooltipText = "Restore to saved combat start state",
            CustomMinimumSize = new Vector2(140f, 36f)
        };

        resetButton.Pressed += async () =>
        {
            SimulationState.Stop();
            SimulationRunner.Stop();
            await Restarter.RestartRoom();
        };
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

        AddTabButton(tabBar, "This Turn", 0);
        AddTabButton(tabBar, "Full Combat Log", 1);
        AddTabButton(tabBar, "Simulation Results", 2);

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

        _turnDecisionPanel = new TurnDecisionPanel();

        _simulationResultsPanel = new SimulationResultsPanel();
        _simulationResultsPanel.SetResults(SimulationState.Simulations);

        AddPanel(contentArea, _turnDecisionPanel.Root);
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
        int tabIndex)
    {
        var button = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(150f, 36f)
        };

        button.Pressed += () => ShowTab(tabIndex);
        bar.AddChild(button);
    }

    private static void ShowTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= TabPanels.Count)
            return;

        _activeTabIndex = tabIndex;

        for (int i = 0; i < TabPanels.Count; i++)
            TabPanels[i].Visible = i == tabIndex;

        // Refresh whichever tab's data just became visible.
        switch (tabIndex)
        {
            case 0:
                _turnDecisionPanel?.Populate();
                break;

            case 1:
                _combatLogPanel?.Populate();
                break;
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

        // Reopen on whichever tab was last active (defaults to This Turn
        // the first time the screen is ever opened).
        ShowTab(_activeTabIndex);
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

        int simulationCount = _simulationCountInput != null
            ? (int)_simulationCountInput.Value
            : 50;

        GD.Print(
            $"[SpireSolver] Starting simulation run ({simulationCount} simulations)"
        );

        SimulationRunner.Start(simulationCount);
        UpdatePlayCombatButton();
    }
    
    private static void UpdatePlayCombatButton()
    {
        if (_playCombatButton == null)
            return;

        _playCombatButton.Text = SimulationRunner.IsRunning
            ? "⏹ Stop"
            : "▶ Play Combat";

        if (_simulationCountInput != null)
            _simulationCountInput.Editable = !SimulationRunner.IsRunning;
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
        _turnDecisionPanel?.Populate();
    }

    private static void RefreshAfterBatch()
    {
        SetSimulationResults(SimulationState.Simulations);
        _turnDecisionPanel?.Populate();
        UpdatePlayCombatButton();
    }
    

    private static void ResetData()
    {
        SimulationState.Clear();
        SetSimulationResults(SimulationState.Simulations);
        _combatLogPanel?.Populate();
        _turnDecisionPanel?.Populate();
    }
}