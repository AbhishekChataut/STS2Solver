using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SpireSolver.Simulation;

/// <summary>
/// CombatHistoryEntry records which turn number each player was on when the
/// entry occurred, but only exposes that via HappenedThisTurn(state) /
/// HappenedLastPlayerTurn(player) — both of which compare against a *live*
/// ICombatState. SimulationResult.Actions holds entries from combats that
/// have already finished (and each simulation restarts from the same saved
/// state with its own fresh Player/Creature instances), so there's no live
/// state to compare against.
///
/// The actual turn number lives in a private field, `_playerTurnNumbers`.
/// This reads it via reflection, keyed off the entry's own Actor, so it
/// never needs an external Player reference to match against — sidestepping
/// the fact that every simulation has different Player object instances.
///
/// If a future game update renames or removes the field, GetPlayerTurnNumber
/// degrades to returning null (and turn-sequence analysis just stops working)
/// instead of throwing.
/// </summary>
internal static class CombatHistoryEntryReflection
{
    private static readonly FieldInfo? PlayerTurnNumbersField = ResolveField();

    private static FieldInfo? ResolveField()
    {
        try
        {
            return AccessTools.Field(
                typeof(CombatHistoryEntry),
                "_playerTurnNumbers");
        }
        catch (Exception e)
        {
            GD.PrintErr(
                "[SpireSolver] Could not resolve " +
                $"CombatHistoryEntry._playerTurnNumbers: {e.Message}");

            return null;
        }
    }

    public static int? GetPlayerTurnNumber(this CombatHistoryEntry entry)
    {
        var player = entry.Actor?.Player;

        if (player == null || PlayerTurnNumbersField == null)
            return null;

        if (PlayerTurnNumbersField.GetValue(entry) is not
            Dictionary<ulong, int> turnNumbersByNetId)
        {
            return null;
        }

        // NOTE: assumes Player.NetId is accessible from this assembly. If it
        // turns out to be internal, fetch it via reflection the same way as
        // the field above.
        return turnNumbersByNetId.TryGetValue(player.NetId, out var turn)
            ? turn
            : null;
    }
}

public static class TurnSequenceExtensions
{
    /// <summary>
    /// Every card played, grouped by turn and re-indexed so the earliest
    /// turn recorded in this simulation is always "1" — since every
    /// simulation restarts from the same saved combat state, this makes
    /// turn numbers directly comparable across every SimulationResult
    /// regardless of what the actual in-game turn counter said.
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<string>>
        GetCardSequencesByTurn(this SimulationResult result)
    {
        var cardPlays = result.Actions
            .OfType<CardPlayFinishedEntry>()
            .ToList();

        if (cardPlays.Count == 0)
            return new Dictionary<int, IReadOnlyList<string>>();

        var withTurns = cardPlays
            .Select(entry => (Entry: entry, Turn: entry.GetPlayerTurnNumber()))
            .Where(x => x.Turn.HasValue)
            .Select(x => (x.Entry, Turn: x.Turn!.Value))
            .ToList();

        if (withTurns.Count == 0)
            return new Dictionary<int, IReadOnlyList<string>>();

        int firstTurn = withTurns.Min(x => x.Turn);

        return withTurns
            .GroupBy(x => x.Turn - firstTurn + 1)
            .OrderBy(group => group.Key)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(x => x.Entry.CardPlay.Card.Id.Entry)
                    .ToList());
    }

    /// <summary>
    /// Convenience wrapper for just the first turn — equivalent to
    /// GetCardSequencesByTurn().GetValueOrDefault(1), or empty if nothing
    /// was played that turn.
    /// </summary>
    public static IReadOnlyList<string> GetFirstTurnCardSequence(
        this SimulationResult result)
    {
        return result.GetCardSequencesByTurn().TryGetValue(1, out var sequence)
            ? sequence
            : Array.Empty<string>();
    }
}