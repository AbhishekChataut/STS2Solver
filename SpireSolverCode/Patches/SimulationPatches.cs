using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using SpireSolver.Simulation;

[HarmonyPatch]
public static class CombatSimulationPatch
{
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldStopCombatFromEnding))]
    [HarmonyPostfix]
    public static void ShouldStopCombatFromEndingPostfix(
        ICombatState combatState,
        ref bool __result)
    {
        if (SimulationState.IsSimulating)
            __result = true;
    }
}

// TODO: remove character animations, remove turn title cards
// TODO: fix restart to not remove history
// TODO: add simulation state management
[HarmonyPatch]
public static class FastCardPileVisualsPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CardPileCmd),
            "GetTweenForCardsChangingPiles",
            new[] { typeof(IEnumerable<CardPileAddResult>) }
        );
    }

    static bool Prefix(ref ValueTuple<Tween?, bool> __result)
    {
        if (!SimulationState.IsSimulating)
            return true;

        __result = (null, false);
        return false;
    }
}

// PRETTY PRETTY GOOD!
[HarmonyPatch]
public static class NoCombatRoomPatch
{
    [HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.Create))]
    [HarmonyPrefix]
    public static bool CreatePrefix(ref NCombatRoom? __result)
    {
        if (!SimulationState.IsSimulating)
            return true;

        __result = null;
        return false;
    }
}

[HarmonyPatch]
public static class NoCreatureAnimationsPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CreatureCmd),
            nameof(CreatureCmd.TriggerAnim),
            new[]
            {
                typeof(Creature),
                typeof(string),
                typeof(float)
            }
        );
    }

    static bool Prefix(ref Task __result)
    {
        if (!SimulationState.IsSimulating)
            return true;

        __result = Task.CompletedTask;
        return false;
    }
}


// NCombatRoom normally provides the screen shake target.
//
// Without the room, every damage event attempts to shake a target that
// doesn't exist and Godot prints "Missing screenShake target!".
//
// Screen shake has no effect on combat state, so skip it entirely.
[HarmonyPatch]
public static class NoScreenShakePatch
{
    [HarmonyPatch(typeof(NScreenShake), nameof(NScreenShake.Shake))]
    [HarmonyPrefix]
    public static bool ShakePrefix()
    {
        return !SimulationState.IsSimulating;
    }
}

[HarmonyPatch]
public static class NoAttackerAnimationsPatch
{
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.WithAttackerAnim))]
    [HarmonyPostfix]
    public static void WithAttackerAnimPostfix(AttackCommand __result)
    {
        if (SimulationState.IsSimulating)
            __result.WithNoAttackerAnim();
    }
}