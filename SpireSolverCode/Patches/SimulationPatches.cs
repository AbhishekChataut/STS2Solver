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


// ============================================================
// Prevent combat from ending while we're running the simulation
// ============================================================

[HarmonyPatch]
public static class CombatSimulationPatch
{
    [HarmonyPatch(
        typeof(Hook),
        nameof(Hook.ShouldStopCombatFromEnding)
    )]
    [HarmonyPostfix]
    public static void ShouldStopCombatFromEndingPostfix(
        ICombatState combatState,
        ref bool __result)
    {
        if (SimulationState.IsSimulating)
            __result = true;
    }
}


// ============================================================
// Disable card pile movement/tweens
// ============================================================

[HarmonyPatch]
public static class FastCardPileVisualsPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CardPileCmd),
            "GetTweenForCardsChangingPiles",
            new[]
            {
                typeof(IEnumerable<CardPileAddResult>)
            }
        );
    }

    static bool Prefix(
        ref ValueTuple<Tween?, bool> __result)
    {
        if (!SimulationState.IsSimulating)
            return true;

        __result = (null, false);
        return false;
    }
}


// ============================================================
// Don't create the visual combat room
// ============================================================

[HarmonyPatch]
public static class NoCombatRoomPatch
{
    [HarmonyPatch(
        typeof(NCombatRoom),
        nameof(NCombatRoom.Create)
    )]
    [HarmonyPrefix]
    public static bool CreatePrefix(
        ref NCombatRoom? __result)
    {
        if (!SimulationState.IsSimulating)
            return true;

        __result = null;
        return false;
    }
}


// ============================================================
// Disable CreatureCmd animations.
//
// This catches animations outside AttackCommand too, e.g.
//
// await CreatureCmd.TriggerAnim(
//     creature,
//     "Inhale",
//     0.6f
// );
//
// ============================================================

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


// ============================================================
// Disable screen shake
// ============================================================

[HarmonyPatch]
public static class NoScreenShakePatch
{
    [HarmonyPatch(
        typeof(NScreenShake),
        nameof(NScreenShake.Shake)
    )]
    [HarmonyPrefix]
    public static bool ShakePrefix()
    {
        return !SimulationState.IsSimulating;
    }
}


// ============================================================
// Disable AttackCommand attacker animations.
//
// Do this on Execute rather than WithAttackerAnim because
// FromCard/FromMonster can configure animations without ever
// calling WithAttackerAnim explicitly.
// ============================================================

[HarmonyPatch]
public static class NoAttackerAnimationsPatch
{
    [HarmonyPatch(
        typeof(AttackCommand),
        nameof(AttackCommand.Execute)
    )]
    [HarmonyPrefix]
    public static void ExecutePrefix(
        AttackCommand __instance)
    {
        if (!SimulationState.IsSimulating)
            return;

        __instance.WithNoAttackerAnim();
    }
}


// ============================================================
// Disable SFX.
//
// IMPORTANT:
// SfxCmd.Play has multiple overloads, so:
//
// [HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play))]
//
// is ambiguous.
//
// TargetMethods patches every static Play overload.
// ============================================================

[HarmonyPatch]
public static class NoSimulationSfxPatch
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(SfxCmd)
            .GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static
            )
            .Where(method =>
                method.Name == nameof(SfxCmd.Play) &&
                method.ReturnType == typeof(void)
            );
    }

    static bool Prefix()
    {
        return !SimulationState.IsSimulating;
    }
}


// ============================================================
// Disable VFX.
//
// Only patch void Play* methods for now. This avoids breaking
// methods where the caller expects a return value.
// ============================================================

[HarmonyPatch]
public static class NoSimulationVfxPatch
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(VfxCmd)
            .GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static
            )
            .Where(method =>
                method.Name.StartsWith("Play") &&
                method.ReturnType == typeof(void)
            );
    }

    static bool Prefix()
    {
        return !SimulationState.IsSimulating;
    }
}