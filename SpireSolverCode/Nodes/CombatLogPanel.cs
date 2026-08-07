using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpireSolver.SpireSolverCode.Nodes;

public sealed class CombatLogPanel
{
    private static readonly HashSet<Type> AllowedLogEntries = new()
    {
        typeof(CardPlayFinishedEntry),
        // typeof(CardDrawnEntry),
        typeof(CardDiscardedEntry),
        typeof(DamageReceivedEntry),
        typeof(BlockGainedEntry),
        typeof(EnergySpentEntry),
        typeof(PotionUsedEntry),
        typeof(PowerReceivedEntry)
    };

    private Player? _player;
    private IRunState? _runState;

    private readonly VBoxContainer _thisTurnContent;
    private readonly VBoxContainer _fullLogContent;

    public Control ThisTurnRoot { get; }
    public Control FullLogRoot { get; }

    public CombatLogPanel()
    {
        (ThisTurnRoot, _thisTurnContent) =
            BuildLogPanel("ThisTurnContent");

        (FullLogRoot, _fullLogContent) =
            BuildLogPanel("FullLogContent");
    }

    public void SetContext(
        Player player,
        IRunState runState)
    {
        _player = player;
        _runState = runState;
    }

    public void Populate()
    {
        ClearChildren(_thisTurnContent);
        ClearChildren(_fullLogContent);

        if (_player == null)
        {
            GD.PrintErr(
                "[SpireSolver] No player context — call SetContext first");

            AddSectionHeader(
                _thisTurnContent,
                "No player context");

            AddSectionHeader(
                _fullLogContent,
                "No player context");

            return;
        }

        var combatState = GetCombatState();

        if (combatState == null)
        {
            AddSectionHeader(
                _thisTurnContent,
                "Not currently in combat");

            AddSectionHeader(
                _fullLogContent,
                "Not currently in combat");

            return;
        }

        var history = GetCombatHistory();

        if (history == null)
        {
            AddSectionHeader(
                _thisTurnContent,
                "No combat history available");

            AddSectionHeader(
                _fullLogContent,
                "No combat history available");

            return;
        }

        PopulateLogs(
            combatState,
            history);
    }

    private void PopulateLogs(
        ICombatState combatState,
        CombatHistory history)
    {
        // Player entries only.
        //
        // AllowedLogEntries acts as a whitelist, so entries not explicitly
        // listed above will not appear in either tab.
        //
        // Newest entries are displayed first.
        var playerEntries = history.Entries
            .Where(entry =>
                entry.Actor?.Player == _player &&
                AllowedLogEntries.Contains(entry.GetType()))
            .Reverse()
            .ToList();

        var thisTurnEntries = playerEntries
            .Where(entry => entry.HappenedThisTurn(combatState))
            .ToList();

        PopulateThisTurn(thisTurnEntries);
        PopulateFullLog(playerEntries);
    }

    private void PopulateThisTurn(
        IReadOnlyCollection<CombatHistoryEntry> entries)
    {
        AddSectionHeader(
            _thisTurnContent,
            $"This Turn  ({entries.Count})");

        if (entries.Count == 0)
        {
            AddRow(
                _thisTurnContent,
                "—",
                "No actions yet this turn");

            return;
        }

        foreach (var entry in entries)
            AddLogRow(_thisTurnContent, entry);
    }

    private void PopulateFullLog(
        IReadOnlyCollection<CombatHistoryEntry> entries)
    {
        AddSectionHeader(
            _fullLogContent,
            $"Full Combat Log  ({entries.Count})");

        if (entries.Count == 0)
        {
            AddRow(
                _fullLogContent,
                "—",
                "No actions logged yet");

            return;
        }

        foreach (var entry in entries)
            AddLogRow(_fullLogContent, entry);
    }

    private static ICombatState? GetCombatState()
    {
        if (!CombatManager.Instance.IsInProgress)
            return null;

        return CombatManager.Instance.DebugOnlyGetState();
    }

    private static CombatHistory? GetCombatHistory()
    {
        if (!CombatManager.Instance.IsInProgress)
            return null;

        return CombatManager.Instance.History;
    }

    private static (
        Control Root,
        VBoxContainer Content)
        BuildLogPanel(string contentName)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        var content = new VBoxContainer
        {
            Name = contentName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        scroll.AddChild(content);

        return (scroll, content);
    }

    private static void AddSectionHeader(
        Control parent,
        string text)
    {
        var label = new Label
        {
            Text = text
        };

        label.AddThemeFontSizeOverride(
            "font_size",
            18);

        label.AddThemeColorOverride(
            "font_color",
            new Color(0.9f, 0.8f, 0.4f));

        parent.AddChild(label);
    }

    private static void AddRow(
        Control parent,
        string label,
        string value)
    {
        var row = new HBoxContainer();

        var labelControl = new Label
        {
            Text = label,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        labelControl.AddThemeColorOverride(
            "font_color",
            new Color(0.75f, 0.75f, 0.75f));

        row.AddChild(labelControl);

        var valueControl = new Label
        {
            Text = value
        };

        row.AddChild(valueControl);

        parent.AddChild(row);
    }

    private static void AddLogRow(
        Control parent,
        CombatHistoryEntry entry)
    {
        var label = new Label
        {
            Text = $"{IconFor(entry)}  {entry.HumanReadableString}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        parent.AddChild(label);
    }

    private static string IconFor(
        CombatHistoryEntry entry)
    {
        return entry.GetType().Name switch
        {
            "CardPlayFinishedEntry"     => "🃏",
            "CardPlayStartedEntry"      => "▶",
            "CardDrawnEntry"            => "📥",
            "CardDiscardedEntry"        => "🗑",
            "CardExhaustedEntry"        => "💀",
            "CardAfflictedEntry"        => "☠",
            "CardGeneratedEntry"        => "✚",
            "CreatureAttackedEntry"     => "⚔",
            "DamageReceivedEntry"       => "💥",
            "BlockGainedEntry"          => "🛡",
            "EnergySpentEntry"          => "⚡",
            "MonsterPerformedMoveEntry" => "👹",
            "OrbChanneledEntry"         => "🔮",
            "PotionUsedEntry"           => "🧪",
            "PowerReceivedEntry"        => "✨",
            "StarsModifiedEntry"        => "⭐",
            "SummonedEntry"             => "🌀",
            _                           => "•"
        };
    }

    private static void ClearChildren(
        Control parent)
    {
        foreach (Node child in parent.GetChildren())
            child.QueueFree();
    }
}