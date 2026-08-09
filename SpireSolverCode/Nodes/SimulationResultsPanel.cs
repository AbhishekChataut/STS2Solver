using Godot;
using SpireSolver.Simulation;
using SpireSolver.SpireSolverCode.Nodes.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpireSolver.SpireSolverCode.Nodes;

public sealed class SimulationResultsPanel
{
    private const int CohortSize = 100;

    private readonly VBoxContainer _root;
    private readonly Label _summary;

    private readonly HBoxContainer _hpCohorts;
    private readonly HBoxContainer _turnCohorts;

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

        // ------------------------------------------------------------
        // Final HP cohorts
        // ------------------------------------------------------------

        var hpTitle = new Label
        {
            Text = $"Final HP Distribution — {CohortSize}-Simulation Cohorts"
        };

        hpTitle.AddThemeFontSizeOverride("font_size", 20);
        _root.AddChild(hpTitle);

        var hpScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };

        _hpCohorts = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        hpScroll.AddChild(_hpCohorts);
        _root.AddChild(hpScroll);

        // ------------------------------------------------------------
        // Turns cohorts
        // ------------------------------------------------------------

        var turnsTitle = new Label
        {
            Text = $"Turns Taken Distribution — {CohortSize}-Simulation Cohorts"
        };

        turnsTitle.AddThemeFontSizeOverride("font_size", 20);
        _root.AddChild(turnsTitle);

        var turnsScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };

        _turnCohorts = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        turnsScroll.AddChild(_turnCohorts);
        _root.AddChild(turnsScroll);
    }

    public void SetResults(IReadOnlyCollection<SimulationResult> results)
    {
        ClearChildren(_hpCohorts);
        ClearChildren(_turnCohorts);

        if (results.Count == 0)
        {
            _summary.Text = "No simulations have been run.";
            return;
        }

        // SimulationState.Simulations is currently ordered by completion,
        // so preserve that order before dividing it into cohorts.
        var orderedResults = results.ToList();

        var hpValues = orderedResults
            .Select(result => result.HpRemaining)
            .ToArray();

        var turnValues = orderedResults
            .Select(result => result.TurnsTaken)
            .ToArray();

        _summary.Text =
            $"{orderedResults.Count:N0} simulations  •  " +
            $"Average final HP: {hpValues.Average():F1}  •  " +
            $"Average turns: {turnValues.Average():F1}";

        BuildCohorts(
            orderedResults,
            _hpCohorts,
            result => result.HpRemaining,
            "HP");

        BuildCohorts(
            orderedResults,
            _turnCohorts,
            result => result.TurnsTaken,
            "Turns");
    }

    private static void BuildCohorts(
        IReadOnlyList<SimulationResult> results,
        HBoxContainer container,
        Func<SimulationResult, int> valueSelector,
        string metricName)
    {
        int cohortCount =
            (results.Count + CohortSize - 1) / CohortSize;

        for (int cohortIndex = 0;
             cohortIndex < cohortCount;
             cohortIndex++)
        {
            int startIndex = cohortIndex * CohortSize;

            int count = Math.Min(
                CohortSize,
                results.Count - startIndex);

            var cohort = results
                .Skip(startIndex)
                .Take(count)
                .ToArray();

            var values = cohort
                .Select(valueSelector)
                .ToArray();

            int firstSimulation = startIndex + 1;
            int lastSimulation = startIndex + count;

            var cohortPanel = BuildCohortPanel(
                firstSimulation,
                lastSimulation,
                values,
                metricName);

            container.AddChild(cohortPanel);
        }
    }

    private static Control BuildCohortPanel(
        int firstSimulation,
        int lastSimulation,
        int[] values,
        string metricName)
    {
        var panel = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(350f, 0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        var title = new Label
        {
            Text = $"Simulations {firstSimulation:N0}–{lastSimulation:N0}",
            HorizontalAlignment = HorizontalAlignment.Center
        };

        title.AddThemeFontSizeOverride("font_size", 16);
        panel.AddChild(title);

        var average = new Label
        {
            Text = $"Avg {metricName}: {values.Average():F1}",
            HorizontalAlignment = HorizontalAlignment.Center
        };

        panel.AddChild(average);

        // Different color for each cohort
        Color[] colors =
        {
            new Color(0.35f, 0.75f, 0.90f),
            new Color(0.90f, 0.45f, 0.35f),
            new Color(0.45f, 0.85f, 0.50f),
            new Color(0.80f, 0.55f, 0.90f)
        };

        // 1–100   -> 0
        // 101–200 -> 1
        // 201–300 -> 2
        // etc.
        int cohortIndex = (firstSimulation - 1) / 100;

        // % makes the colors repeat if there are more cohorts than colors
        Color thisColor = colors[cohortIndex % colors.Length];

        var histogram = new HistogramControl(thisColor);

        histogram.Root.CustomMinimumSize =
            new Vector2(350f, 300f);

        histogram.SetValues(values);

        panel.AddChild(histogram.Root);

        return panel;
    }
    
    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
            child.QueueFree();
    }
}
