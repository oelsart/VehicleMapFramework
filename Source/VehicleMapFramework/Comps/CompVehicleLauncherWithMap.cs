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
            if (gizmo is Command_ActionHighlighter takeoffCommand)
            {
                takeoffCommand.Disabled = false;
                if (!CanLaunchWithCargoCapacityWithMap(out string reason))
                {
                    takeoffCommand.Disable(reason);
                }
            }
            yield return gizmo;
        }
    }

    public bool CanLaunchWithCargoCapacityWithMap(out string disableReason)
    {
        disableReason = null;
        if (Vehicle.Spawned)
        {
            if (Vehicle.vehiclePather.Moving)
            {
                disableReason = "VF_CannotLaunchWhileMoving".Translate(Vehicle.LabelShort);
            }
            else if (Ext_Vehicles.IsRoofed(Vehicle.Position, Vehicle.Map))
            {
                disableReason = "CommandLaunchGroupFailUnderRoof".Translate();
            }
        }
        if (Vehicle.MovementPermissions.HasFlag(VehiclePermissions.Mobile))
        {
            if (!Vehicle.CanMoveFinal)
            {
                disableReason = "VF_CannotLaunchImmobile".Translate(Vehicle.LabelShort);
            }
            else if (Vehicle.Angle != 0f)
            {
                disableReason = "VF_CannotLaunchRotated".Translate(Vehicle.LabelShort);
            }
        }
        else
        {
            float cargoCapacity = Vehicle.GetStatValue(VehicleStatDefOf.CargoCapacity);
            var mass = MassUtility.InventoryMass(Vehicle);
            var flag = true;
            if (Vehicle is VehiclePawnWithMap vehicleWithMap)
            {
                float maximumPayload = Vehicle.GetStatValue(VMF_DefOf.MaximumPayload);
                var mass2 = CollectionsMassCalculator.MassUsage(vehicleWithMap.VehicleMap.listerThings.AllThings, IgnorePawnsInventoryMode.DontIgnore, true);
                if (mass2 > maximumPayload)
                {
                    flag = false;
                }
            }
            if (mass > cargoCapacity && flag)
            {
                disableReason = "VF_CannotLaunchOverEncumbered".Translate(Vehicle.LabelShort);
            }
        }
        if (!VehicleMod.settings.debug.debugDraftAnyVehicle && !Vehicle.CanMoveWithOperators)
        {
            disableReason = "VF_NotEnoughToOperate".Translate();
        }
        else if (Vehicle.CompFueledTravel != null && Vehicle.CompFueledTravel.EmptyTank)
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
