using RimWorld;
using System.Collections.Generic;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompVehicleLauncherWithMap : CompVehicleLauncher
{
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }
        if (launchProtocol == null)
        {
            Log.ErrorOnce(string.Format("No launch protocols for {0}. At least 1 must be included in order to initiate takeoff.", base.Vehicle), base.Vehicle.thingIDNumber);
            yield break;
        }
        Command_ActionHighlighter launchCommand = launchProtocol.LaunchCommand;
        if (!CanLaunchWithCargoCapacityWithMap(out string reason))
        {
            launchCommand.Disable(reason);
        }
        yield return launchCommand;
    }

    public bool CanLaunchWithCargoCapacityWithMap(out string disableReason)
    {
        disableReason = null;
        if (base.Vehicle.Spawned)
        {
            if (base.Vehicle.vehiclePather.Moving)
            {
                disableReason = "VF_CannotLaunchWhileMoving".Translate(base.Vehicle.LabelShort);
            }
            else if (Ext_Vehicles.IsRoofed(base.Vehicle.Position, base.Vehicle.Map))
            {
                disableReason = "CommandLaunchGroupFailUnderRoof".Translate();
            }
        }
        if (base.Vehicle.MovementPermissions.HasFlag(VehiclePermissions.Mobile))
        {
            if (!base.Vehicle.CanMoveFinal)
            {
                disableReason = "VF_CannotLaunchImmobile".Translate(base.Vehicle.LabelShort);
            }
            else if (base.Vehicle.Angle != 0f)
            {
                disableReason = "VF_CannotLaunchRotated".Translate(base.Vehicle.LabelShort);
            }
        }
        else
        {
            float cargoCapacity = base.Vehicle.GetStatValue(VehicleStatDefOf.CargoCapacity);
            var mass = MassUtility.InventoryMass(base.Vehicle);
            var flag = true;
            if (base.Vehicle is VehiclePawnWithMap vehicleWithMap)
            {
                float maximumPayload = base.Vehicle.GetStatValue(VMF_DefOf.MaximumPayload);
                var mass2 = CollectionsMassCalculator.MassUsage(vehicleWithMap.VehicleMap.listerThings.AllThings, IgnorePawnsInventoryMode.DontIgnore, true);
                if (mass2 > maximumPayload)
                {
                    flag = false;
                }
            }
            if (mass > cargoCapacity && flag)
            {
                disableReason = "VF_CannotLaunchOverEncumbered".Translate(base.Vehicle.LabelShort);
            }
        }
        if (!VehicleMod.settings.debug.debugDraftAnyVehicle && !base.Vehicle.CanMoveWithOperators)
        {
            disableReason = "VF_NotEnoughToOperate".Translate();
        }
        else if (base.Vehicle.CompFueledTravel != null && base.Vehicle.CompFueledTravel.EmptyTank)
        {
            disableReason = "VF_LaunchOutOfFuel".Translate();
        }
        else if (FlightSpeed <= 0f)
        {
            disableReason = "VF_NoFlightSpeed".Translate();
        }
        if (!launchProtocol.CanLaunchNow)
        {
            disableReason = launchProtocol.FailLaunchMessage;
        }
        return disableReason.NullOrEmpty();
    }
}
