using Godot;
using System.Collections.Generic;

namespace SpireSolver.SpireSolverCode.Nodes.Controls;

/// <summary>
/// Assigns each card name a color the first time it's seen, then always
/// returns that same color afterward. Deliberately static (not tied to any
/// panel instance) so a card's color stays stable across every UI rebuild —
/// tab switches, RestartRoom tearing down and rebuilding the whole screen,
/// running more simulations, all of it — for as long as the game is running.
/// </summary>
public static class CardColorPalette
{
    private static readonly Color[] Palette =
    {
        new(0.35f, 0.70f, 0.90f),
        new(0.90f, 0.55f, 0.30f),
        new(0.55f, 0.85f, 0.45f),
        new(0.85f, 0.40f, 0.75f),
        new(0.90f, 0.85f, 0.35f),
        new(0.55f, 0.55f, 0.90f),
        new(0.85f, 0.35f, 0.35f),
        new(0.40f, 0.85f, 0.80f),
        new(0.75f, 0.60f, 0.35f),
        new(0.60f, 0.75f, 0.35f),
        new(0.35f, 0.60f, 0.75f),
        new(0.75f, 0.35f, 0.60f)
    };

    private static readonly Dictionary<string, Color> Assigned = new();
    private static int _nextIndex;

    /// <summary>
    /// If the palette is exhausted (more than Palette.Length distinct cards
    /// have been seen), colors start repeating rather than throwing —
    /// slightly ambiguous is better than crashing the panel.
    /// </summary>
    public static Color GetColor(string cardName)
    {
        if (Assigned.TryGetValue(cardName, out var color))
            return color;

        color = Palette[_nextIndex % Palette.Length];
        _nextIndex++;

        Assigned[cardName] = color;
        return color;
    }
}