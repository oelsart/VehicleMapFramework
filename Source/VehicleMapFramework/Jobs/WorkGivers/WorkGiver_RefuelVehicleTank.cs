using RimWorld;
using System.Collections.Generic;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class WorkGiver_RefuelVehicleTank : WorkGiver_RefuelVehicle
{
    public override JobDef JobStandard => VMF_DefOf.VMF_RefuelVehicleTank;

    //public override JobDef JobAtomic => VMF_DefOf.VMF_RefuelVehicleTankAtomic;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.listerThings.GetAllThings(t => t.HasComp<CompFuelTank>());
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var compFuelTank = t.TryGetComp<CompFuelTank>();
        var vehiclePawn = compFuelTank.Vehicle;
        return vehiclePawn != null && vehiclePawn.CompFueledTravel != null && CanRefuel(pawn, t, forced);
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        Job result = null;
        var compFuelTank = t.TryGetComp<CompFuelTank>();
        VehiclePawn vehiclePawn = compFuelTank.Vehicle;
        if (vehiclePawn == null || vehiclePawn.CompFueledTravel == null)
        {
            return result;
        }
        Thing thing = vehiclePawn.CompFueledTravel.ClosestFuelAvailable(pawn);
        if (thing == null)
        {
            return null;
        }
        return JobMaker.MakeJob(VMF_DefOf.VMF_RefuelVehicleTank, t, thing);
    }

    public static bool CanRefuel(Pawn pawn, Thing t, bool forced = false)
    {
        var compFuelTank = t.TryGetComp<CompFuelTank>();
        var vehicle = compFuelTank.Vehicle;
        CompFueledTravel compFueledTravel = vehicle?.CompFueledTravel;
        if (compFueledTravel == null || compFueledTravel.FullTank || compFueledTravel.FuelLeaking)
        {
            return false;
        }

        if (!forced && !compFueledTravel.ShouldAutoRefuelNow)
        {
            return false;
        }

        if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
        {
            return false;
        }

        if (vehicle.Faction != pawn.Faction || t.Faction != pawn.Faction)
        {
            return false;
        }

        if (compFueledTravel.ClosestFuelAvailable(pawn) == null)
        {
            JobFailReason.Is("NoFuelToRefuel".Translate(compFueledTravel.Props.fuelType));
            return false;
        }

        return true;
    }
}
