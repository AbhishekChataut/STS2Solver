using Godot;
using SpireSolver.Simulation;
using SpireSolver.SpireSolverCode.Nodes.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpireSolver.SpireSolverCode.Nodes;

/// <summary>
/// Replaces the old flat "This Turn" combat log. Lets the user build a
/// multi-turn "plan" — a per-turn chain of cards (Turn 1: Strike, Defend;
/// Turn 2: Bash; etc.), choose whether each turn must match exactly in
/// order or just be contained (any order, but respecting repeated cards),
/// and pin any number of these as named comparison groups. Every pinned
/// group — plus whatever's currently being drafted — gets highlighted
/// simultaneously in one always-full-scale histogram of final HP, so you
/// can compare e.g. "Turn 1 Strike→Defend, Turn 2 Bash" head to head
/// against "Turn 1 Defend→Defend" without ever losing the rest of the
/// distribution for context.
/// </summary>
public sealed class TurnDecisionPanel
{
    private enum SequenceMatchMode
    {
        ExactOrder,
        ContainsAnyOrder
    }

    private sealed class SequenceGroup
    {
        public string Label { get; }
        public Dictionary<int, List<string>> CardsByTurn { get; }
        public SequenceMatchMode Mode { get; }

        public SequenceGroup(
            string label,
            Dictionary<int, List<string>> cardsByTurn,
            SequenceMatchMode mode)
        {
            Label = label;
            CardsByTurn = cardsByTurn;
            Mode = mode;
        }
    }

    private const string AllGroupKey = "All simulations";
    private const string OtherGroupKey = "Everything else";
    private const int MaxTurn = 50;

    // Used both for the "nothing defined yet, plain distribution" view and
    // for whatever's currently being drafted (before it's saved).
    private static readonly Color AccentColor = new(0.95f, 0.75f, 0.25f);
    private static readonly Color OtherColor = new(0.40f, 0.40f, 0.45f);

    // Draft state: which turn cards are currently being added to, and the
    // cards accumulated so far for each turn number that has any.
    private readonly Dictionary<int, List<string>> _draftCardsByTurn = new();
    private SequenceMatchMode _draftMode = SequenceMatchMode.ExactOrder;

    private readonly List<SequenceGroup> _savedGroups = new();
    private List<string> _knownCardNames = new();

    private readonly VBoxContainer _root;
    private readonly SpinBox _turnSelector;
    private readonly OptionButton _cardPicker;
    private readonly Button _addButton;
    private readonly Button _exactModeButton;
    private readonly Button _containsModeButton;
    private readonly Button _saveGroupButton;
    private readonly VBoxContainer _turnsContainer;
    private readonly Label _chainEmptyLabel;
    private readonly VBoxContainer _savedGroupsContent;
    private readonly Label _savedGroupsEmptyLabel;
    private readonly Label _statusLabel;
    private readonly StackedHistogramControl _histogram;
    private readonly VBoxContainer _legendContent;

    public Control Root => _root;

    public TurnDecisionPanel()
    {
        _root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        // "Add card" row.
        var addRow = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 32f)
        };
        addRow.AddThemeConstantOverride("separation", 8);

        addRow.AddChild(new Label
        {
            Text = "Add to turn:",
            VerticalAlignment = VerticalAlignment.Center
        });

        _turnSelector = new SpinBox
        {
            MinValue = 1,
            MaxValue = MaxTurn,
            Step = 1,
            Value = 1,
            CustomMinimumSize = new Vector2(70f, 30f)
        };
        addRow.AddChild(_turnSelector);

        addRow.AddChild(new Label
        {
            Text = "card:",
            VerticalAlignment = VerticalAlignment.Center
        });

        _cardPicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(160f, 30f),
            Disabled = true
        };
        addRow.AddChild(_cardPicker);

        _addButton = new Button
        {
            Text = "+ Add",
            CustomMinimumSize = new Vector2(70f, 30f),
            Disabled = true
        };
        _addButton.Pressed += OnAddPressed;
        addRow.AddChild(_addButton);

        var clearButton = new Button
        {
            Text = "Clear All",
            CustomMinimumSize = new Vector2(90f, 30f)
        };
        clearButton.Pressed += OnClearPressed;
        addRow.AddChild(clearButton);

        _root.AddChild(addRow);

        // Match-mode toggle + Save Group row.
        var modeRow = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 32f)
        };
        modeRow.AddThemeConstantOverride("separation", 8);

        modeRow.AddChild(new Label
        {
            Text = "Match:",
            VerticalAlignment = VerticalAlignment.Center
        });

        var modeGroup = new ButtonGroup();

        _exactModeButton = new Button
        {
            Text = "Exact order",
            ToggleMode = true,
            ButtonPressed = true,
            ButtonGroup = modeGroup,
            CustomMinimumSize = new Vector2(110f, 28f),
            TooltipText = "Within each turn, cards must appear in this " +
                          "exact order, starting from the first card " +
                          "played that turn"
        };
        _exactModeButton.Pressed +=
            () => SetDraftMode(SequenceMatchMode.ExactOrder);
        modeRow.AddChild(_exactModeButton);

        _containsModeButton = new Button
        {
            Text = "Contains (any order)",
            ToggleMode = true,
            ButtonGroup = modeGroup,
            CustomMinimumSize = new Vector2(160f, 28f),
            TooltipText = "Within each turn, it must include at least " +
                          "this many of each card, in any order or " +
                          "position that turn"
        };
        _containsModeButton.Pressed +=
            () => SetDraftMode(SequenceMatchMode.ContainsAnyOrder);
        modeRow.AddChild(_containsModeButton);

        _saveGroupButton = new Button
        {
            Text = "💾 Save Group",
            CustomMinimumSize = new Vector2(120f, 28f),
            Disabled = true,
            TooltipText = "Pin this multi-turn plan as a permanent " +
                          "comparison group and start drafting a new one"
        };
        _saveGroupButton.Pressed += OnSaveGroupPressed;
        modeRow.AddChild(_saveGroupButton);

        _root.AddChild(modeRow);

        // Reorderable per-turn chains of chips.
        _turnsContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _root.AddChild(_turnsContainer);

        _chainEmptyLabel = new Label
        {
            Text = "No cards added — showing the full distribution.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _root.AddChild(_chainEmptyLabel);

        // Saved comparison groups.
        var comparingHeaderRow = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 28f)
        };
        comparingHeaderRow.AddThemeConstantOverride("separation", 8);

        comparingHeaderRow.AddChild(new Label
        {
            Text = "Comparing:",
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });

        var clearGroupsButton = new Button
        {
            Text = "Clear comparisons",
            CustomMinimumSize = new Vector2(140f, 26f)
        };
        clearGroupsButton.Pressed += OnClearGroupsPressed;
        comparingHeaderRow.AddChild(clearGroupsButton);

        _root.AddChild(comparingHeaderRow);

        _savedGroupsContent = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _root.AddChild(_savedGroupsContent);

        _savedGroupsEmptyLabel = new Label
        {
            Text = "No saved comparisons yet — build a plan above and " +
                   "click Save Group.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _root.AddChild(_savedGroupsEmptyLabel);

        _root.AddChild(new HSeparator());

        _statusLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _root.AddChild(_statusLabel);

        // Body: histogram on the left, summary legend on the right.
        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        _histogram = new StackedHistogramControl();
        _histogram.Root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _histogram.Root.SizeFlagsStretchRatio = 3f;
        body.AddChild(_histogram.Root);

        var legendScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(280f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        _legendContent = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        legendScroll.AddChild(_legendContent);

        body.AddChild(legendScroll);

        _root.AddChild(body);
    }

    public void Populate()
    {
        var allResults = SimulationState.Simulations;

        RefreshCardPickerOptions(allResults);
        RebuildTurnsContainer();
        RebuildSavedGroupsList();

        if (allResults.Count == 0)
        {
            ShowStatus(
                "No simulations run yet — use Play Combat to generate data.");

            return;
        }

        ShowChart();

        // Priority order for which group claims a given simulation: saved
        // groups first, in the order they were saved (so pinned comparisons
        // stay stable regardless of what you're currently drafting), then
        // the unsaved draft (if it has any cards), then a final catch-all
        // for anything matching none of them.
        var activeGroups = new List<SequenceGroup>(_savedGroups);

        var draftCards = CopyNonEmptyTurns(_draftCardsByTurn);

        SequenceGroup? draftGroup = null;
        if (draftCards.Count > 0)
        {
            draftGroup = new SequenceGroup(
                "(draft) " + BuildGroupLabel(draftCards, _draftMode),
                draftCards,
                _draftMode);

            activeGroups.Add(draftGroup);
        }

        List<string> groupOrder;
        Dictionary<string, Color> groupColors;
        List<(SimulationResult Result, string Group)> grouped;

        if (activeGroups.Count == 0)
        {
            // Nothing saved or drafted — show the plain, unhighlighted
            // distribution instead of a meaningless single-color split.
            groupOrder = new List<string> { AllGroupKey };
            groupColors = new Dictionary<string, Color>
            {
                [AllGroupKey] = AccentColor
            };

            grouped = allResults
                .Select(result => (Result: result, Group: AllGroupKey))
                .ToList();
        }
        else
        {
            groupOrder = activeGroups.Select(g => g.Label).ToList();
            groupOrder.Add(OtherGroupKey);

            groupColors = _savedGroups.ToDictionary(
                g => g.Label,
                g => SequenceGroupColorPalette.GetColor(g.Label));

            if (draftGroup != null)
                groupColors[draftGroup.Label] = AccentColor;

            groupColors[OtherGroupKey] = OtherColor;

            grouped = allResults
                .Select(result =>
                {
                    var sequencesByTurn = result.GetCardSequencesByTurn();

                    var matched = activeGroups
                        .FirstOrDefault(g => Matches(g, sequencesByTurn));

                    return (
                        Result: result,
                        Group: matched?.Label ?? OtherGroupKey);
                })
                .ToList();
        }

        var samples = grouped
            .Select(entry => (Bucket: entry.Result.HpRemaining, entry.Group))
            .ToList();

        _histogram.SetData(samples, groupColors, groupOrder);

        RebuildLegend(grouped, groupOrder, groupColors);
    }

    private static Dictionary<int, List<string>> CopyNonEmptyTurns(
        Dictionary<int, List<string>> source)
    {
        return source
            .Where(kv => kv.Value.Count > 0)
            .ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value));
    }

    /// <summary>
    /// A group matches a simulation only if every turn it specifies is
    /// satisfied (AND across turns) — a turn the simulation never reached
    /// (e.g. combat ended early) simply fails to satisfy any non-empty
    /// requirement for that turn.
    /// </summary>
    private static bool Matches(
        SequenceGroup group,
        IReadOnlyDictionary<int, IReadOnlyList<string>> sequencesByTurn)
    {
        foreach (var (turn, requiredCards) in group.CardsByTurn)
        {
            var actualCards = sequencesByTurn.TryGetValue(turn, out var cards)
                ? cards
                : Array.Empty<string>();

            bool turnMatches = group.Mode == SequenceMatchMode.ExactOrder
                ? MatchesExactPrefix(requiredCards, actualCards)
                : MatchesContainsAnyOrder(requiredCards, actualCards);

            if (!turnMatches)
                return false;
        }

        return true;
    }

    /// <summary>Positional prefix match — order matters.</summary>
    private static bool MatchesExactPrefix(
        IReadOnlyList<string> required,
        IReadOnlyList<string> actual)
    {
        if (required.Count > actual.Count)
            return false;

        for (int i = 0; i < required.Count; i++)
        {
            if (actual[i] != required[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Multiset containment — order doesn't matter, but repeats do (asking
    /// for two Strikes requires that turn to have played at least two,
    /// anywhere within it).
    /// </summary>
    private static bool MatchesContainsAnyOrder(
        IReadOnlyList<string> required,
        IReadOnlyList<string> actual)
    {
        if (required.Count == 0)
            return true;

        var requiredCounts = required
            .GroupBy(card => card)
            .ToDictionary(g => g.Key, g => g.Count());

        var actualCounts = actual
            .GroupBy(card => card)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (card, requiredCount) in requiredCounts)
        {
            if (!actualCounts.TryGetValue(card, out var actualCount) ||
                actualCount < requiredCount)
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildGroupLabel(
        IReadOnlyDictionary<int, List<string>> cardsByTurn,
        SequenceMatchMode mode)
    {
        var parts = cardsByTurn
            .OrderBy(kv => kv.Key)
            .Select(kv => mode == SequenceMatchMode.ExactOrder
                ? $"T{kv.Key}: {string.Join(" → ", kv.Value)}"
                : $"T{kv.Key} contains: {string.Join(", ", kv.Value)}");

        return string.Join("  |  ", parts);
    }

    private void SetDraftMode(SequenceMatchMode mode)
    {
        if (_draftMode == mode)
            return;

        _draftMode = mode;
        Populate();
    }

    private void RefreshCardPickerOptions(
        IReadOnlyCollection<SimulationResult> results)
    {
        var cardNames = results
            .SelectMany(result => result
                .GetCardSequencesByTurn()
                .Values
                .SelectMany(cards => cards))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (cardNames.SequenceEqual(_knownCardNames))
            return;

        // Try to keep whatever was selected in the dropdown if it's still
        // in the (possibly larger) list of known cards, rather than
        // silently resetting the picker every time a new card shows up.
        string? previousSelection =
            _cardPicker.ItemCount > 0 && _cardPicker.Selected >= 0
                ? _cardPicker.GetItemText(_cardPicker.Selected)
                : null;

        _knownCardNames = cardNames;

        _cardPicker.Clear();

        foreach (var name in cardNames)
            _cardPicker.AddItem(name);

        if (previousSelection != null)
        {
            int restoredIndex = cardNames.IndexOf(previousSelection);
            if (restoredIndex >= 0)
                _cardPicker.Selected = restoredIndex;
        }

        bool hasCards = cardNames.Count > 0;
        _cardPicker.Disabled = !hasCards;
        _addButton.Disabled = !hasCards;
    }

    private void RebuildTurnsContainer()
    {
        ClearChildren(_turnsContainer);

        var turnsWithCards = _draftCardsByTurn
            .Where(kv => kv.Value.Count > 0)
            .Select(kv => kv.Key)
            .OrderBy(turn => turn)
            .ToList();

        bool hasAny = turnsWithCards.Count > 0;

        _chainEmptyLabel.Visible = !hasAny;
        _turnsContainer.Visible = hasAny;
        _saveGroupButton.Disabled = !hasAny;

        foreach (var turn in turnsWithCards)
            _turnsContainer.AddChild(BuildTurnRow(turn));
    }

    private Control BuildTurnRow(int turn)
    {
        var panel = new PanelContainer();
        var column = new VBoxContainer();

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);

        header.AddChild(new Label
        {
            Text = $"Turn {turn}:",
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });

        var removeTurnButton = new Button
        {
            Text = "Remove turn",
            CustomMinimumSize = new Vector2(100f, 24f)
        };
        removeTurnButton.Pressed += () => RemoveTurn(turn);
        header.AddChild(removeTurnButton);

        column.AddChild(header);

        var chipFlow = new HFlowContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var cards = _draftCardsByTurn[turn];
        for (int i = 0; i < cards.Count; i++)
            chipFlow.AddChild(BuildChip(turn, i, cards[i]));

        column.AddChild(chipFlow);

        panel.AddChild(column);

        return panel;
    }

    private Control BuildChip(int turn, int index, string cardName)
    {
        var panel = new PanelContainer();

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var swatch = new ColorRect
        {
            Color = CardColorPalette.GetColor(cardName),
            CustomMinimumSize = new Vector2(14f, 14f)
        };
        row.AddChild(swatch);

        row.AddChild(new Label
        {
            Text = $"{index + 1}. {cardName}",
            VerticalAlignment = VerticalAlignment.Center
        });

        var cardsInTurn = _draftCardsByTurn[turn];

        var upButton = new Button
        {
            Text = "↑",
            Disabled = index == 0,
            CustomMinimumSize = new Vector2(26f, 26f),
            TooltipText = "Move earlier in this turn"
        };
        upButton.Pressed += () => MoveChip(turn, index, index - 1);
        row.AddChild(upButton);

        var downButton = new Button
        {
            Text = "↓",
            Disabled = index == cardsInTurn.Count - 1,
            CustomMinimumSize = new Vector2(26f, 26f),
            TooltipText = "Move later in this turn"
        };
        downButton.Pressed += () => MoveChip(turn, index, index + 1);
        row.AddChild(downButton);

        var removeButton = new Button
        {
            Text = "✕",
            CustomMinimumSize = new Vector2(26f, 26f),
            TooltipText = "Remove"
        };
        removeButton.Pressed += () => RemoveChip(turn, index);
        row.AddChild(removeButton);

        panel.AddChild(row);

        return panel;
    }

    private void RebuildSavedGroupsList()
    {
        ClearChildren(_savedGroupsContent);

        _savedGroupsEmptyLabel.Visible = _savedGroups.Count == 0;
        _savedGroupsContent.Visible = _savedGroups.Count > 0;

        for (int i = 0; i < _savedGroups.Count; i++)
            _savedGroupsContent.AddChild(BuildSavedGroupRow(i, _savedGroups[i]));
    }

    private Control BuildSavedGroupRow(int index, SequenceGroup group)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 32f)
        };
        row.AddThemeConstantOverride("separation", 8);

        var swatch = new ColorRect
        {
            Color = SequenceGroupColorPalette.GetColor(group.Label),
            CustomMinimumSize = new Vector2(16f, 16f)
        };
        row.AddChild(swatch);

        row.AddChild(new Label
        {
            Text = group.Label,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        });

        var removeButton = new Button
        {
            Text = "✕",
            CustomMinimumSize = new Vector2(26f, 26f),
            TooltipText = "Remove from comparison"
        };
        removeButton.Pressed += () => RemoveSavedGroup(index);
        row.AddChild(removeButton);

        return row;
    }

    private void RebuildLegend(
        List<(SimulationResult Result, string Group)> grouped,
        List<string> groupOrder,
        Dictionary<string, Color> groupColors)
    {
        ClearChildren(_legendContent);

        foreach (var group in groupOrder)
        {
            var groupResults = grouped
                .Where(entry => entry.Group == group)
                .Select(entry => entry.Result)
                .ToList();

            int count = groupResults.Count;
            double averageHp = count == 0
                ? 0
                : groupResults.Average(r => r.HpRemaining);
            double winRate = count == 0
                ? 0
                : groupResults.Count(r => r.Won) / (double)count;

            double averageTurns = count == 0
                ? 0
                : groupResults.Average(r => r.TurnsTaken);

            _legendContent.AddChild(BuildLegendRow(
                group,
                groupColors[group],
                count,
                averageHp,
                averageTurns,
                winRate));
        }
    }

    private static Control BuildLegendRow(
        string label,
        Color color,
        int count,
        double averageHp,
        double averageTurns,
        double winRate)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 44f)
        };
        row.AddThemeConstantOverride("separation", 8);

        var swatch = new ColorRect
        {
            Color = color,
            CustomMinimumSize = new Vector2(16f, 16f)
        };
        row.AddChild(swatch);

        var text = new Label
        {
            Text = count == 0
                ? $"{label}\nNo simulations"
                : $"{label}\n{count} sims • avg HP {averageHp:F1} • " +
                  $" avg Turns {averageTurns:F1} • " +
                  $"{winRate:P0} won",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddChild(text);

        return row;
    }

    private void OnAddPressed()
    {
        if (_cardPicker.ItemCount == 0 || _cardPicker.Selected < 0)
            return;

        var cardName = _cardPicker.GetItemText(_cardPicker.Selected);
        var turn = (int)_turnSelector.Value;

        if (!_draftCardsByTurn.TryGetValue(turn, out var cards))
        {
            cards = new List<string>();
            _draftCardsByTurn[turn] = cards;
        }

        cards.Add(cardName);
        Populate();
    }

    private void OnClearPressed()
    {
        if (_draftCardsByTurn.Count == 0)
            return;

        _draftCardsByTurn.Clear();
        Populate();
    }

    private void OnSaveGroupPressed()
    {
        var cardsByTurn = CopyNonEmptyTurns(_draftCardsByTurn);

        if (cardsByTurn.Count == 0)
            return;

        var label = BuildGroupLabel(cardsByTurn, _draftMode);

        // Avoid piling up exact duplicates if the user clicks Save twice
        // on the same plan.
        if (_savedGroups.Any(g => g.Label == label))
            return;

        _savedGroups.Add(new SequenceGroup(label, cardsByTurn, _draftMode));

        _draftCardsByTurn.Clear();
        _draftMode = SequenceMatchMode.ExactOrder;
        _exactModeButton.ButtonPressed = true;
        _turnSelector.Value = 1;

        Populate();
    }

    private void OnClearGroupsPressed()
    {
        if (_savedGroups.Count == 0)
            return;

        _savedGroups.Clear();
        Populate();
    }

    private void MoveChip(int turn, int fromIndex, int toIndex)
    {
        var cards = _draftCardsByTurn[turn];

        if (toIndex < 0 || toIndex >= cards.Count)
            return;

        (cards[fromIndex], cards[toIndex]) = (cards[toIndex], cards[fromIndex]);

        Populate();
    }

    private void RemoveChip(int turn, int index)
    {
        var cards = _draftCardsByTurn[turn];
        cards.RemoveAt(index);

        if (cards.Count == 0)
            _draftCardsByTurn.Remove(turn);

        Populate();
    }

    private void RemoveTurn(int turn)
    {
        _draftCardsByTurn.Remove(turn);
        Populate();
    }

    private void RemoveSavedGroup(int index)
    {
        _savedGroups.RemoveAt(index);
        Populate();
    }

    private void ShowStatus(string message)
    {
        _statusLabel.Text = message;
        _statusLabel.Visible = true;

        _histogram.Root.Visible = false;
        ClearChildren(_legendContent);
    }

    private void ShowChart()
    {
        _statusLabel.Visible = false;
        _histogram.Root.Visible = true;
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
            child.QueueFree();
    }
}