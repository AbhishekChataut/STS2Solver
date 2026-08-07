using Godot;

namespace SpireSolver.SpireSolverCode.Nodes.Controls;

public sealed class HistogramControl
{
    private readonly VBoxContainer _root;
    private readonly HBoxContainer _bars;
    private readonly Label _emptyLabel;

    public Control Root => _root;

    public HistogramControl()
    {
        _root = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 350f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        _emptyLabel = new Label
        {
            Text = "No simulation results available.",
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

    public void SetValues(IEnumerable<int> values)
    {
        ClearChildren(_bars);

        var hpValues = values.ToArray();

        _emptyLabel.Visible = hpValues.Length == 0;
        _bars.Visible = hpValues.Length > 0;

        if (hpValues.Length == 0)
            return;

        var counts = hpValues
            .GroupBy(hp => hp)
            .OrderBy(group => group.Key)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var maxCount = counts.Values.Max();

        foreach (var (hp, count) in counts)
            _bars.AddChild(BuildBar(hp, count, maxCount));
    }

    private static Control BuildBar(
        int hp,
        int count,
        int maxCount)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        var countLabel = new Label
        {
            Text = count.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        column.AddChild(countLabel);

        // This container gives us a fixed vertical area in which
        // the histogram bar can grow from the bottom.
        var barArea = new Control
        {
            CustomMinimumSize = new Vector2(20f, 250f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        column.AddChild(barArea);

        var normalizedHeight = (float)count / maxCount;

        var bar = new ColorRect
        {
            Color = new Color(0.35f, 0.7f, 0.9f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        // Fill horizontally.
        bar.AnchorLeft = 0.1f;
        bar.AnchorRight = 0.9f;

        // Grow upward from the bottom.
        bar.AnchorTop = 1f - normalizedHeight;
        bar.AnchorBottom = 1f;

        bar.OffsetLeft = 0f;
        bar.OffsetRight = 0f;
        bar.OffsetTop = 0f;
        bar.OffsetBottom = 0f;

        barArea.AddChild(bar);

        var hpLabel = new Label
        {
            Text = hp.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        column.AddChild(hpLabel);

        return column;
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
            child.QueueFree();
    }
}