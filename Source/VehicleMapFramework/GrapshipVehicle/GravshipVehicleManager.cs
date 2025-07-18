using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleMapFramework
{
#pragma warning disable CS9113 // パラメーターが未読です。
    public class GravshipVehicleManager(Game game) : GameComponent
#pragma warning restore CS9113 // パラメーターが未読です。
    {
        public override void ExposeData()
        {
            HashSet<VehicleMapProps_Gravship> hashSet = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                hashSet = [];
                var allGravshipVehicles = Find.Maps.SelectMany(m => m.mapPawns.AllPawns);
                allGravshipVehicles = allGravshipVehicles.Concat(Find.WorldPawns.AllPawnsAliveOrDead);
                allGravshipVehicles = allGravshipVehicles.Concat(Find.Maps.SelectMany(m => m.listerThings.AllThings.OfType<VehicleSkyfaller>().Select(v => (Pawn)v.vehicle)));
                allGravshipVehicles = allGravshipVehicles.ToList();

                hashSet.AddRange(DefDatabase<VehicleDef>.AllDefs
                    .Where(d => d.HasModExtension<VehicleMapProps_Gravship>())
                    .Where(d => allGravshipVehicles.Any(p => p.def == d))
                    .Select(d => d.GetModExtension<VehicleMapProps_Gravship>()));
            }
            Scribe_Collections.Look(ref hashSet, "GravshipVehicleMapProps", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                foreach (var props in hashSet)
                {
                    VMF_Log.Debug($"Loading VehicleDef: {props.DefName}");
                    if (DefDatabase<VehicleDef>.GetNamedSilentFail(props.DefName) == null)
                    {
                        GravshipVehicleUtility.GenerateGravshipVehicleDef(props);
                    }
                }
            }
        }
    }
}
