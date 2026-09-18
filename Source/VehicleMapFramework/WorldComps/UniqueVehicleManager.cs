using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class UniqueVehicleManager(Game game) : GameComponent
{
  private readonly Game game = game;
  private Dictionary<VehicleDef, List<string>> claimedDefNames = [];
  private static bool logging;

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
        if (logging) VMF_Log.Message($"Claim unique vehicle def: {vehicleDef}");
        if (vehicleDef.GetModExtension<VehicleMapProps_Unique>() is not { } props)
        {
          VMF_Log.Warning("Could not get the props required for the def being claimed.");
          vehicleDef.modExtensions ??= [];
          vehicleDef.modExtensions.Add(props = new VehicleMapProps_Unique
          {
            baseDef = parentDef
          });
        }
        if (props.baseDef is null)
        {
          VMF_Log.Warning("The parent is not set in the props of the def being claimed.");
          props.baseDef = parentDef;
        }
        return vehicleDef;
      }
    }

    VMF_Log.Error($"Failed to claim unique vehicle def for {parentDef}. Using parent def instead.");
    return parentDef;
  }

  public void ReleaseUniqueVehicleDef(VehicleDef def)
  {
    if (logging) VMF_Log.Message($"Release unique vehicle def: {def}");
    foreach (var hashSet in claimedDefNames.Values)
    {
      hashSet.Remove(def.defName);
    }
  }

  public int ClaimedCount(VehicleDef vehicleDef)
  {
    return claimedDefNames.GetValueOrDefault(vehicleDef)?.Count ?? 0;
  }

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
          Scribe_Collections.Look(ref claimed, $"{nameof(claimedDefNames)}_{parentDef.defName}", LookMode.Value);
      }
    }
    Scribe_Collections.Look(ref hashSet, "GravshipVehicleMapProps", LookMode.Deep);
    hashSet ??= [];

    if (Scribe.mode == LoadSaveMode.LoadingVars)
    {
      foreach (var props in hashSet)
      {
        GravshipVehicleUtility.GenerateGravshipVehicleDef(props, this);
      }
    }

    if (Scribe.mode is LoadSaveMode.LoadingVars or LoadSaveMode.ResolvingCrossRefs or LoadSaveMode.PostLoadInit)
    {
      claimedDefNames ??= [];
      foreach (var parentDef in PlaceholderDefs.Keys)
      {
        var claimed = claimedDefNames.GetValueOrDefault(parentDef);
        Scribe_Collections.Look(ref claimed, $"{nameof(claimedDefNames)}_{parentDef.defName}", LookMode.Value);
        claimedDefNames[parentDef] = claimed;
      }
    }

    if (Scribe.mode is LoadSaveMode.PostLoadInit)
    {
      claimedDefNames.RemoveAll(l => l.Value is null);
      foreach (var parentDef in PlaceholderDefs.Keys)
      {
        foreach (var placeholder in PlaceholderDefs[parentDef])
        {
          placeholder.size = parentDef.size;
          placeholder.uiIconScale = parentDef.uiIconScale;
          UniqueVehicleUtility.ReinitializeComponents(placeholder);
        }
      }
    }
  }

  [DebugAction(VehicleMapFramework.CategoryName, "Toggle logging UniqueVehicleManager",
    allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.PlayingOnMap)]
  private static void ToggleLogging() => logging = !logging;

  [DebugOutput(VehicleMapFramework.CategoryName, name = "UniqueVehicleManager")]
  private static void OutputState()
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine("PlaceholderDefs count");
    foreach (var (vehicleDef, list) in PlaceholderDefs)
    {
      stringBuilder.AppendLine($"{vehicleDef.defName}: {list.Count}");
    }

    if (Current.Game?.GetComponent<UniqueVehicleManager>() is { } component)
    {
      foreach (var (vehicleDef, list) in PlaceholderDefs)
      {
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(vehicleDef.defName);
        if (!component.claimedDefNames.TryGetValue(vehicleDef, out var list2))
        {
          stringBuilder.AppendLine("Nothing is claimed");
          continue;
        }

        for (var i = 0; i < list.Count; i++)
        {
          var vehicleDef2 = list[i];
          stringBuilder.Append($"{vehicleDef2.defName}: ");
          stringBuilder.Append(list2.Contains(vehicleDef2.defName) ? "Claimed" : "       ");
          stringBuilder.Append(i % 2 == 0 ? "   " : "\n");
        }
      }
    }
    
    Log.Message(stringBuilder);
  }
}
