using SpireSolver.SpireSolverCode.Simulation.Actions;

namespace SpireSolver.SpireSolverCode.Simulation.Learning;

public sealed class BasicActionEncoder : IActionEncoder
{
    public float[] Encode(CombatAction action)
    {
        return action switch
        {
            CombatAction.PlayCard playCard => EncodePlayCard(playCard),
            CombatAction.EndTurn => EncodeEndTurn(),

            _ => throw new System.ArgumentOutOfRangeException(
                nameof(action),
                action,
                "Unknown combat action."
            )
        };
    }

    private static float[] EncodePlayCard(
        CombatAction.PlayCard action)
    {
        /*
         * Minimal action representation:
         *
         * [0] Is PlayCard
         * [1] Is EndTurn
         * [2] Card cost
         * [3] Has target
         *
         * We intentionally do NOT encode card identity yet.
         */
        return
        [
            1.0f,
            0.0f,
            action.Card.EnergyCost.Canonical,
            action.Target != null ? 1.0f : 0.0f
        ];
    }

    private static float[] EncodeEndTurn()
    {
        return
        [
            0.0f,
            1.0f,
            0.0f,
            0.0f
        ];
    }
}