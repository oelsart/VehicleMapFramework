using RimWorld;
using System;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

[Obsolete]
public static class LoadTransportersJobOnVehicleUtility
{
    public static ThingCount FindThingToLoad(Pawn p, CompTransporter transporter, bool gatherFromBaseMap)
    {
        if (gatherFromBaseMap)
        {
            return Patch_LoadTransportersJobUtility_FindThingToLoad.FindThingToLoad(p, transporter);
        }
        return LoadTransportersJobUtility.FindThingToLoad(p, transporter);
    }

    public static Job JobOnTransporter(Pawn p, CompTransporter transporter)
    {
        return LoadTransportersJobUtility.JobOnTransporter(p, transporter);
    }

    public static bool HasJobOnTransporter(Pawn pawn, CompTransporter transporter)
    {
        if (transporter.parent.IsForbidden(pawn))
        {
            return false;
        }

        if (!transporter.AnythingLeftToLoad)
        {
            return false;
        }

        if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
        {
            return false;
        }

        if (!pawn.CanReach(transporter.parent, PathEndMode.Touch, pawn.NormalMaxDanger(), false, false, TraverseMode.ByPawn, transporter.parent.Map, out _, out _))
        {
            return false;
        }

        if (LoadTransportersJobOnVehicleUtility.FindThingToLoad(pawn, transporter, transporter is not CompBuildableContainer container || container.GatherFromBaseMap).Thing == null)
        {
            return false;
        }

        return true;
    }
}
