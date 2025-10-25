using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class UniqueVehicleManager(Game game) : GameComponent
{
    private readonly Game game = game;

    public override void ExposeData()
    {
        HashSet<VehicleMapProps_Unique> hashSet = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            hashSet = [];
            var allGravshipVehicles = Find.Maps.SelectMany(m => m.mapPawns.AllPawns);
            allGravshipVehicles = allGravshipVehicles.Concat(Find.WorldPawns.AllPawnsAliveOrDead);
            allGravshipVehicles = allGravshipVehicles.Concat(Find.Maps.SelectMany(m => m.listerThings.AllThings.OfType<VehicleSkyfaller>().Select(v => (Pawn)v.vehicle)));
            allGravshipVehicles = [.. allGravshipVehicles];

            hashSet.AddRange(DefDatabase<VehicleDef>.AllDefs
                .Where(d => d.HasModExtension<VehicleMapProps_Unique>())
                .Where(d => allGravshipVehicles.Any(p => p.def == d))
                .Select(d => d.GetModExtension<VehicleMapProps_Unique>()));
        }
        Scribe_Collections.Look(ref hashSet, "GravshipVehicleMapProps", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            foreach (var props in hashSet)
            {
                VMF_Log.DebugMessage($"Loading VehicleDef: {props.defName}");
                var vehicleDef = DefDatabase<VehicleDef>.GetNamedSilentFail(props.defName);
                if (props is VehicleMapProps_Gravship gravshipProps)
                {
                    vehicleDef ??= GravshipVehicleUtility.GenerateGravshipVehicleDef(gravshipProps);
                    vehicleDef.size = gravshipProps.size;
                }
                else
                {
                    vehicleDef ??= UniqueVehicleUtility.GenerateUniqueVehicleDef(props);
                    vehicleDef.size = props.baseDef.size;
                }

                vehicleDef.components?.ForEach(component =>
                {
                    component.hitbox.Hitbox.Clear();
                    component.hitbox.Initialize(vehicleDef);
                });
            }
        }
    }
}