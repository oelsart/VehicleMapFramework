using System.Collections.Generic;
using RimWorld;
using Vehicles;
using Verse;
using Verse.AI.Group;

namespace VehicleMapFramework;

public class IncidentWorker_Ambush_EnemyBoats : IncidentWorker_Ambush_EnemyMapVehicle
{
    protected override WorldObjectDef MapParentDef => VMF_DefOf.VMF_AmbushSea;

    protected override bool ValidRaiderVehicle(VehicleDef vehicleDef, VehicleCategory category, PawnsArrivalModeDef arrivalModeDef,
        Faction faction, float points)
    {
        return vehicleDef.thingClass.SameOrSubclassOf<VehiclePawnWithMap>() && vehicleDef.HasComp<CompNpcVehicleMap>() &&
               vehicleDef.type == VehicleType.Sea && (vehicleDef.vehicleCategory & category) == category &&
               vehicleDef.combatPower <= points && faction.def.techLevel >= vehicleDef.techLevel &&
               (vehicleDef.enabled & VehicleEnabled.For.Raiders) != VehicleEnabled.For.None &&
               vehicleDef.npcProperties != null && (vehicleDef.npcProperties.raidParams == null ||
                                                    vehicleDef.npcProperties.raidParams.Allows(faction,
                                                        arrivalModeDef));
    }

    protected override LordJob CreateLordJob(List<VehiclePawnWithMap> generatedVehicles, IncidentParms parms)
    {
        return new LordJob_ArmoredAssaultSea(parms.faction, LordJob_ArmoredAssault.RaiderPermissions.All);
    }
}