using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace VehicleInteriors
{
    public class PlaceWorker_ForbidOnHumanPoweredVehicle : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!map.IsVehicleMapOf(out var vehicle) && !vehicle.VehicleDef.HasModExtension<VehicleHumanPowered>())
            {
                return true;
            }
            else
            {
                return "VIF_ForbidOnVehicle".Translate();
            }
        }

    }
}
