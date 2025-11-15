using System.Linq;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleTurret_Manual : VehicleTurret
{
    /// <summary>
    /// Init from CompProperties
    /// </summary>
    public VehicleTurret_Manual()
    {
    }

    /// <summary>
    /// Init from save file
    /// </summary>
    public VehicleTurret_Manual(VehiclePawn vehicle) : base(vehicle)
    {
    }

    /// <summary>
    /// Newly Spawned
    /// </summary>
    /// <param name="vehicle"></param>
    /// <param name="reference">VehicleTurret as defined in xml</param>
    public VehicleTurret_Manual(VehiclePawn vehicle, VehicleTurret reference) : base(vehicle, reference)
    {
    }

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
    }
}