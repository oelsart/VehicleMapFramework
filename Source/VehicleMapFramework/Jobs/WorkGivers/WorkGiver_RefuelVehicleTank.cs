using RimWorld;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
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
        if (pawn.IsOnVehicleMapOf(out var vehicle))
        {
            if (!vehicle.Spawned || vehicle.Map.GetCachedMapComponent<VehicleReservationManager>().VehicleListers("Refuel").Contains(vehicle))
            {
                foreach (var comp in vehicle.FuelTankComps)
                {
                    yield return comp.parent;
                }
            }
        }
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return CanRefuelTank(pawn, t, forced) && t.IsOnVehicleMapOf(out var vehicle) && vehicle.CompFueledTravel != null && CanRefuelVehicle(pawn, vehicle, forced);
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!t.IsOnVehicleMapOf(out var vehicle))
        {
            return null;
        }
        Thing fuel = vehicle.CompFueledTravel.ClosestFuelAvailable(pawn);
        if (fuel == null)
        {
            return null;
        }
        return JobMaker.MakeJob(VMF_DefOf.VMF_RefuelVehicleTank, t, fuel);
    }

    public static bool CanRefuelVehicle(Pawn pawn, VehiclePawn vehicle, bool forced)
    {
        CompFueledTravel compFueledTravel = vehicle?.CompFueledTravel;
        if (compFueledTravel == null || compFueledTravel.FullTank || compFueledTravel.FuelLeaking)
        {
            return false;
        }
        bool ShouldAutoRefuelNow()
        {
            return FuelPercentOfTarget() <= compFueledTravel.Props.autoRefuelPercent && !compFueledTravel.FullTank && compFueledTravel.TargetFuelLevel > 0f && ShouldAutoRefuelNowIgnoringFuelPct();
        }
        bool ShouldAutoRefuelNowIgnoringFuelPct()
        {
            return compFueledTravel.allowAutoRefuel && (!vehicle.Spawned || (/*!vehicle.ignition.Drafted && */!vehicle.IsBurning() && vehicle.Map.designationManager.DesignationOn(vehicle, DesignationDefOf_Vehicles.DisassembleVehicle) == null));
        }
        float FuelPercentOfTarget()
        {
            if (compFueledTravel.TargetFuelLevel != 0f)
            {
                return compFueledTravel.Fuel / compFueledTravel.TargetFuelLevel;
            }
            return 0f;
        }
        if (!forced && !ShouldAutoRefuelNow())
        {
            return false;
        }
        if (compFueledTravel.ClosestFuelAvailable(pawn) == null)
        {
            JobFailReason.Is("NoFuelToRefuel".Translate(compFueledTravel.Props.fuelType));
            return false;
        }
        if (vehicle.Faction != pawn.Faction)
        {
            return false;
        }
        return true;
    }

    public static bool CanRefuelTank(Pawn pawn, Thing t, bool forced = false)
    {
        if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced, t.MapHeld))
        {
            return false;
        }
        if (t.Faction != pawn.Faction)
        {
            return false;
        }
        return true;
    }
}
