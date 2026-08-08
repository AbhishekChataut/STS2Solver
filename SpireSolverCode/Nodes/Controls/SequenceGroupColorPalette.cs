using Godot;
using System.Collections.Generic;

namespace SpireSolver.SpireSolverCode.Nodes.Controls;

/// <summary>
/// Same pattern as CardColorPalette, but keyed on a saved comparison
/// group's label instead of a card name — kept as a separate palette/
/// dictionary so group colors and individual card-chip colors don't fight
/// over the same color slots. Static, so a group's color survives every UI
/// rebuild for as long as the game session runs (assuming the group is
/// re-saved with the same label — colors aren't persisted to disk).
/// </summary>
public static class SequenceGroupColorPalette
{
    private static readonly Color[] Palette =
    {
        new(0.35f, 0.70f, 0.90f),
        new(0.85f, 0.40f, 0.75f),
        new(0.55f, 0.85f, 0.45f),
        new(0.90f, 0.55f, 0.30f),
        new(0.55f, 0.55f, 0.90f),
        new(0.40f, 0.85f, 0.80f),
        new(0.85f, 0.35f, 0.35f),
        new(0.75f, 0.60f, 0.35f)
    };

    private static readonly Dictionary<string, Color> Assigned = new();
    private static int _nextIndex;

    public static Color GetColor(string groupLabel)
    {
        if (Assigned.TryGetValue(groupLabel, out var color))
            return color;

        color = Palette[_nextIndex % Palette.Length];
        _nextIndex++;

        Assigned[groupLabel] = color;
        return color;
    }
}