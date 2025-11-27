using RimWorld;
using Vehicles;
using Verse.AI;

namespace VehicleMapFramework;

public class LordToil_AssaultColonyArmoredSea : LordToil_AssaultColonyArmored
{
    public override void UpdateAllDuties()
    {
        foreach (var pawn in this.lord.ownedPawns)
        {
            if (pawn is VehiclePawn vehicle)
                vehicle.mindState.duty = new PawnDuty(VMF_DefOf.VMF_RangedBoatAggressive);
            else
                pawn.mindState.duty = new PawnDuty(DutyDefOf.Follow);
        }
    }
}