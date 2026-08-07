using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using SpireSolver.SpireSolverCode.Relics;

namespace SpireSolver.SpireSolverCode.Relics;

[Pool(typeof(SharedRelicPool))]
public class MiloRelic() : SpireSolverRelic
{
    public override RelicRarity Rarity =>
        RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(3M)];
    /*{
        get
        {
            return (IEnumerable<DynamicVar>) new \u003C\u003Ez__ReadOnlySingleElementList<DynamicVar>((DynamicVar) new PowerVar<StrengthPower>(1M));
        }
    }*/


    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        MiloRelic miloRelic = this;
        if (!(room is CombatRoom))
            return;
        miloRelic.Flash();
        StrengthPower strengthPower = await PowerCmd.Apply<StrengthPower>((PlayerChoiceContext) new ThrowingPlayerChoiceContext(), miloRelic.Owner.Creature, miloRelic.DynamicVars.Strength.BaseValue, miloRelic.Owner.Creature, (CardModel) null);
    }
    
}