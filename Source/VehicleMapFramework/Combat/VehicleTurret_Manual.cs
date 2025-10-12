using System.Linq;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleTurret_Manual : VehicleTurret
{
    public override void RecacheMannedStatus()
    {
        if (VehicleMod.settings.debug.debugShootAnyTurret)
        {
            IsManned = true;
            return;
        }
        var matchHandlers = vehicle.handlers.FindAll(h => h.role.HandlingTypes.HasFlag(HandlingType.Turret) && (h.role.TurretIds.Contains(key) || h.role.TurretIds.Contains(groupKey)));
        if (matchHandlers.Empty())
        {
            IsManned = false;
            return;
        }

        IsManned = matchHandlers.All(h => h.RoleFulfilled);
        return;
    }
}