using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SpireSolver.SpireSolverCode.Nodes.Controls;

/// <summary>
/// Same bottom-up growth-anchor technique as HistogramControl, but each bar
/// is a stack of colored segments (one per group present at that bucket)
/// instead of a single color. Bar height is still normalized against the
/// biggest total across all buckets, same as the original.
/// </summary>
public sealed class StackedHistogramControl
{
    private readonly VBoxContainer _root;
    private readonly HBoxContainer _bars;
    private readonly Label _emptyLabel;

    public Control Root => _root;

    public StackedHistogramControl()
    {
        _root = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 350f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        _emptyLabel = new Label
        {
            Text = "No matching simulations.",
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _root.AddChild(_emptyLabel);

        _bars = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin
        };

        _root.AddChild(_bars);
    }

    /// <param name="samples">One entry per simulation: its x-axis bucket
    /// value and which group it belongs to.</param>
    /// <param name="groupColors">Color to use for each group key.</param>
    /// <param name="groupOrder">Stacking order, bottom to top. Also
    /// determines legend order for callers that build one.</param>
    public void SetData(
        IReadOnlyList<(int Bucket, string Group)> samples,
        IReadOnlyDictionary<string, Color> groupColors,
        IReadOnlyList<string> groupOrder)
    {
        ClearChildren(_bars);

        _emptyLabel.Visible = samples.Count == 0;
        _bars.Visible = samples.Count > 0;

        if (samples.Count == 0)
            return;

        var countsByBucket = samples
            .GroupBy(sample => sample.Bucket)
            .OrderBy(group => group.Key)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(sample => sample.Group)
                    .ToDictionary(g => g.Key, g => g.Count()));

        var maxTotal = countsByBucket.Values
            .Max(countsByGroup => countsByGroup.Values.Sum());

        foreach (var (bucket, countsByGroup) in countsByBucket)
        {
            _bars.AddChild(BuildStackedBar(
                bucket,
                countsByGroup,
                groupOrder,
                groupColors,
                maxTotal));
        }
    }

    private static Control BuildStackedBar(
        int bucket,
        IReadOnlyDictionary<string, int> countsByGroup,
        IReadOnlyList<string> groupOrder,
        IReadOnlyDictionary<string, Color> groupColors,
        int maxTotal)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        int total = countsByGroup.Values.Sum();

        var countLabel = new Label
        {
            Text = total.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        column.AddChild(countLabel);

        // Same fixed vertical area + bottom-up growth trick as
        // HistogramControl, except now multiple segments stack within it.
        var barArea = new Control
        {
            CustomMinimumSize = new Vector2(20f, 250f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        column.AddChild(barArea);

        float cumulativeFraction = 0f;

        foreach (var group in groupOrder)
        {
            if (!countsByGroup.TryGetValue(group, out var count) ||
                count == 0)
            {
                continue;
            }

            float segmentFraction = (float)count / maxTotal;

            var segment = new ColorRect
            {
                Color = groupColors.TryGetValue(group, out var color)
                    ? color
                    : new Color(0.6f, 0.6f, 0.6f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            // Fill horizontally, same inset as HistogramControl's bars.
            segment.AnchorLeft = 0.1f;
            segment.AnchorRight = 0.9f;

            // Stack upward from the bottom, in groupOrder.
            segment.AnchorBottom = 1f - cumulativeFraction;
            cumulativeFraction += segmentFraction;
            segment.AnchorTop = 1f - cumulativeFraction;

            segment.OffsetLeft = 0f;
            segment.OffsetRight = 0f;
            segment.OffsetTop = 0f;
            segment.OffsetBottom = 0f;

            barArea.AddChild(segment);
        }

        var bucketLabel = new Label
        {
            Text = bucket.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        column.AddChild(bucketLabel);

        return column;
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
            child.QueueFree();
    }
}