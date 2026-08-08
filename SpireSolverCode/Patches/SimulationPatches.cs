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
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;
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

[HarmonyPatch]
public static class SimulationInstantModePatch
{
    [HarmonyPatch(
        typeof(PrefsSave),
        nameof(PrefsSave.FastMode),
        MethodType.Getter
    )]
    [HarmonyPostfix]
    public static void FastModeGetterPostfix(
        ref FastModeType __result)
    {
        if (SimulationState.IsSimulating)
            __result = FastModeType.Instant;
    }
}

[HarmonyPatch]
public static class NoDamageNumberVfxPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(NDamageNumVfx),
            nameof(NDamageNumVfx.Create),
            new[]
            {
                typeof(Creature),
                typeof(DamageResult)
            }
        );
    }

    static bool Prefix(ref NDamageNumVfx? __result)
    {
        if (!SimulationState.IsSimulating)
            return true;

        __result = null;
        return false;
    }
}

[HarmonyPatch]
public static class NoHitSparkVfxPatch
{
    [HarmonyPatch(
        typeof(NHitSparkVfx),
        nameof(NHitSparkVfx.Create)
    )]
    [HarmonyPrefix]
    public static bool Prefix(ref NHitSparkVfx? __result)
    {
        if (!SimulationState.IsSimulating)
            return true;

        __result = null;
        return false;
    }
}

[HarmonyPatch]
public static class NoPlayerHurtVignettePatch
{
    [HarmonyPatch(
        typeof(PlayerHurtVignetteHelper),
        nameof(PlayerHurtVignetteHelper.Play)
    )]
    [HarmonyPrefix]
    public static bool Prefix()
    {
        return !SimulationState.IsSimulating;
    }
}

[HarmonyPatch]
public static class SimulationNoCardPreviewPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CardCmd),
            "PreviewInternal"
        );
    }

    static bool Prefix(ref TaskCompletionSource? __result)
    {
        if (!SimulationState.IsSimulating)
            return true;

        // Replicate PreviewInternal's:
        // if (TestMode.IsOn)
        //     return null;
        __result = null;
        return false;
    }
}

[HarmonyPatch]
public static class SimulationPlayerDeathPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(CreatureCmd),
            nameof(CreatureCmd.Kill),
            new[]
            {
                typeof(IReadOnlyCollection<Creature>),
                typeof(bool)
            }
        );
    }

    static bool Prefix(
        IReadOnlyCollection<Creature> creatures,
        ref Task __result)
    {
        if (!SimulationState.IsSimulating)
            return true;

        if (!creatures.Any(c => c.IsPlayer))
            return true;

        // We know the player was about to die.
        SimulationState.Lose();

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
public static class SimulationTransformVfxPatch
{
    static MethodBase TargetMethod()
    {
        var transform = AccessTools.Method(
            typeof(CardCmd),
            nameof(CardCmd.Transform),
            new[]
            {
                typeof(IEnumerable<CardTransformation>),
                typeof(Rng),
                typeof(CardPreviewStyle)
            }
        );

        // Transform is async; its actual body lives in MoveNext().
        return AccessTools.AsyncMoveNext(transform);
    }

    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var isOn = AccessTools.PropertyGetter(
            typeof(TestMode),
            nameof(TestMode.IsOn));

        var isOff = AccessTools.PropertyGetter(
            typeof(TestMode),
            nameof(TestMode.IsOff));

        var simIsOn = AccessTools.Method(
            typeof(SimulationTransformVfxPatch),
            nameof(IsTestOrSimulation));

        var simIsOff = AccessTools.Method(
            typeof(SimulationTransformVfxPatch),
            nameof(IsNormalGame));

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(isOn))
            {
                instruction.operand = simIsOn;
            }
            else if (instruction.Calls(isOff))
            {
                instruction.operand = simIsOff;
            }

            yield return instruction;
        }
    }

    public static bool IsTestOrSimulation()
    {
        return TestMode.IsOn || SimulationState.IsSimulating;
    }

    public static bool IsNormalGame()
    {
        return TestMode.IsOff && !SimulationState.IsSimulating;
    }
}

