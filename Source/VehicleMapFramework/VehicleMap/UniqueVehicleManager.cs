using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleMapFramework
{
#pragma warning disable CS9113 // パラメーターが未読です。
    public class UniqueVehicleManager(Game game) : GameComponent
#pragma warning restore CS9113 // パラメーターが未読です。
    {
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
                    if (DefDatabase<VehicleDef>.GetNamedSilentFail(props.defName) == null)
                    {
                        if (props is VehicleMapProps_Gravship gravshipProps)
                        {
                            GravshipVehicleUtility.GenerateGravshipVehicleDef(gravshipProps);
                        }
                        else
                        {
                            UniqueVehicleUtility.GenerateUniqueVehicleDef(props);
                        }
                    }
                }
            }
        }
    }
}
