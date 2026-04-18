using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class UniqueVehicleManager(Game game) : GameComponent
{
    private readonly Game game = game;
    private Dictionary<VehicleDef, List<string>> claimedDefNames = [];
    public static Dictionary<VehicleDef, List<VehicleDef>> PlaceholderDefs { get; } = [];

    public VehicleDef ClaimUniqueVehicleDef(VehicleDef parentDef)
    {
        if (!PlaceholderDefs.TryGetValue(parentDef, out var placeholderDefs))
        {
            VMF_Log.Error($"Missing PlaceholderDefs for {parentDef}. Using parent def instead.");
            return parentDef;
        }
        
        if (!claimedDefNames.TryGetValue(parentDef, out var list))
            list = claimedDefNames[parentDef] = [];
        
        foreach (var vehicleDef in placeholderDefs)
        {
            if (!list.Contains(vehicleDef.defName))
            {
                list.Add(vehicleDef.defName);
                vehicleDef.size = parentDef.size;
                UniqueVehicleUtility.ReinitializeComponents(vehicleDef);
                VMF_Log.DebugMessage($"Claim unique vehicle def: {vehicleDef}");
                return vehicleDef;
            }
        }
        
        VMF_Log.Error($"Failed to claim unique vehicle def for {parentDef}. Using parent def instead.");
        return parentDef;
    }

    public void ReleaseUniqueVehicleDef(VehicleDef def)
    {
        VMF_Log.DebugMessage($"Release unique vehicle def: {def}");
        foreach (var hashSet in claimedDefNames.Values) hashSet.Remove(def.defName);
    }

    public int ClaimedCount(VehicleDef vehicleDef) => claimedDefNames.GetValueOrDefault(vehicleDef)?.Count ?? 0;

    public override void ExposeData()
    {
        HashSet<VehicleMapProps_Gravship> hashSet = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            hashSet = [];
            var allGravshipVehicles = Find.Maps.SelectMany(m => m.mapPawns.AllPawns);
            allGravshipVehicles = allGravshipVehicles.Concat(Find.WorldPawns.AllPawnsAliveOrDead);
            allGravshipVehicles = allGravshipVehicles.Concat(Find.Maps.SelectMany(m => m.listerThings.AllThings.OfType<VehicleSkyfaller>().Select(Pawn (v) => v.vehicle)));
            allGravshipVehicles = [.. allGravshipVehicles];

            hashSet.AddRange(DefDatabase<VehicleDef>.AllDefs
                .Where(d => d.HasModExtension<VehicleMapProps_Gravship>())
                .Where(d => allGravshipVehicles.Any(p => p.def == d))
                .Select(d => d.GetModExtension<VehicleMapProps_Gravship>()));
            
            foreach (var parentDef in PlaceholderDefs.Keys)
            {
                if (claimedDefNames.TryGetValue(parentDef, out var claimed))
                    Scribe_Collections.Look(ref claimed, $"{claimedDefNames}_{parentDef.defName}", LookMode.Value);
            }
        }
        Scribe_Collections.Look(ref hashSet, "GravshipVehicleMapProps", LookMode.Deep);
        hashSet ??= [];

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            foreach (var props in hashSet)
            {
                VMF_Log.DebugMessage($"Loading VehicleDef: {props.defName}");
                var vehicleDef = DefDatabase<VehicleDef>.GetNamedSilentFail(props.defName);
                vehicleDef ??= GravshipVehicleUtility.GenerateGravshipVehicleDef(props);
                vehicleDef.size = props.size;
                UniqueVehicleUtility.ReinitializeComponents(vehicleDef);
            }

            claimedDefNames ??= [];
            foreach (var parentDef in PlaceholderDefs.Keys)
            {
                List<string> claimed = null;
                Scribe_Collections.Look(ref claimed, $"{claimedDefNames}_{parentDef.defName}", LookMode.Value);
                if (claimed is not null)
                    claimedDefNames[parentDef] = claimed;

                foreach (var placeholder in PlaceholderDefs[parentDef])
                {
                    placeholder.size = parentDef.size;
                    UniqueVehicleUtility.ReinitializeComponents(placeholder);
                }
            }
        }
    }
}