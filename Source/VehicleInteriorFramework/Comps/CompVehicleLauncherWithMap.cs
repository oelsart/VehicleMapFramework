using RimWorld;
using System.Collections.Generic;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class CompVehicleLauncherWithMap : CompVehicleLauncher
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (this.launchProtocol == null)
            {
                Log.ErrorOnce(string.Format("No launch protocols for {0}. At least 1 must be included in order to initiate takeoff.", base.Vehicle), base.Vehicle.thingIDNumber);
                yield break;
            }
            Command_ActionHighlighter launchCommand = this.launchProtocol.LaunchCommand;
            if (!this.CanLaunchWithCargoCapacityWithMap(out string reason))
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
            if (SettingsCache.TryGetValue<VehiclePermissions>(base.Vehicle.VehicleDef, typeof(VehicleDef), "vehicleMovementPermissions", base.Vehicle.VehicleDef.vehicleMovementPermissions) > VehiclePermissions.NotAllowed)
            {
                if (!base.Vehicle.CanMoveFinal || base.Vehicle.Angle != 0f)
                {
                    disableReason = "VF_CannotLaunchImmobile".Translate(base.Vehicle.LabelShort);
                }
            }
            else
            {
                float statValue = base.Vehicle.GetStatValue(VMF_DefOf.MaximumPayload);
                var mass = MassUtility.InventoryMass(base.Vehicle);
                if (base.Vehicle is VehiclePawnWithMap vehicleWithMap)
                {
                    mass += VehicleMapUtility.VehicleMapMass(vehicleWithMap);
                }
                if (mass > statValue)
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
            else if (this.FlightSpeed <= 0f)
            {
                disableReason = "VF_NoFlightSpeed".Translate();
            }
            if (!this.launchProtocol.CanLaunchNow)
            {
                disableReason = this.launchProtocol.FailLaunchMessage;
            }
            return disableReason.NullOrEmpty();
        }
    }
}
