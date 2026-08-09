using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace SpireSolver.SpireSolverCode.Nodes;

/// <summary>
/// Static injection point for the recommendation HUD. Deliberately thin:
/// its only job is attaching (and, after a RestartRoom rebuild,
/// re-attaching) <see cref="RecommendationPanel"/> to the scene tree,
/// the same way <see cref="SpireSolverScreen"/> does for the main
/// window. All UI construction and refresh logic lives on the panel
/// itself.
/// </summary>
public static class ModelRecommendationOverlay
{
    private static RecommendationPanel? _panel;

    public static void Inject(Node parent)
    {
        // RestartRoom() tears down and rebuilds the run's scene tree,
        // destroying the previous NTopBar (and our old root with it) and
        // triggering a fresh Inject() call with a brand new parent. The
        // name check below is against that *new* parent, so it correctly
        // returns false and we build a fresh panel - no need to track
        // staleness of the old root ourselves.
        if (parent.HasNode(RecommendationPanel.RootName))
            return;

        _panel = new RecommendationPanel();
        parent.CallDeferred("add_child", _panel.Root);
    }

    public static void SetContext(Player player, IRunState runState)
    {
        // RestartRoom creates fresh combat/player objects each time, so
        // this always replaces rather than merges with prior context.
        _panel?.SetContext(player, runState);
    }
}