using Godot;
using SpireSolver.Simulation;
using SpireSolver.SpireSolverCode.Nodes.Controls;

namespace SpireSolver.SpireSolverCode.Nodes;

public sealed class SimulationResultsPanel
{
    private readonly VBoxContainer _root;
    private readonly Label _summary;

    private readonly HistogramControl _hpHistogram;
    private readonly HistogramControl _turnsHistogram;

    public Control Root => _root;

    public SimulationResultsPanel()
    {
        _root = new VBoxContainer
        {
            Name = "SimulationResults",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        // Summary
        _summary = new Label();
        _root.AddChild(_summary);

        // HP histogram
        var hpTitle = new Label
        {
            Text = "Final HP Distribution"
        };

        hpTitle.AddThemeFontSizeOverride("font_size", 20);
        _root.AddChild(hpTitle);

        _hpHistogram = new HistogramControl();
        _root.AddChild(_hpHistogram.Root);

        // Turns histogram
        var turnsTitle = new Label
        {
            Text = "Turns Taken Distribution"
        };

        turnsTitle.AddThemeFontSizeOverride("font_size", 20);
        _root.AddChild(turnsTitle);

        _turnsHistogram = new HistogramControl();
        _root.AddChild(_turnsHistogram.Root);
    }

    public void SetResults(IReadOnlyCollection<SimulationResult> results)
    {
        if (results.Count == 0)
        {
            _summary.Text = "No simulations have been run.";

            _hpHistogram.SetValues(Array.Empty<int>());
            _turnsHistogram.SetValues(Array.Empty<int>());

            return;
        }

        var hpValues = results
            .Select(result => result.HpRemaining)
            .ToArray();

        var turnValues = results
            .Select(result => result.TurnsTaken)
            .ToArray();

        _summary.Text =
            $"{results.Count:N0} simulations  •  " +
            $"Average final HP: {hpValues.Average():F1}  •  " +
            $"Average turns: {turnValues.Average():F1}";

        _hpHistogram.SetValues(hpValues);
        _turnsHistogram.SetValues(turnValues);
    }
}