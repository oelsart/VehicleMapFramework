using System.Collections.Generic;
using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class ScenPart_StartingMapVehicle : ScenPart_StartingVehicle
{
  private PrefabDef prefabDef;

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_Defs.Look(ref prefabDef, nameof(prefabDef));
  }

  public override void DoEditInterface(Listing_ScenEdit listing)
  {
    base.DoEditInterface(listing);
    var rect = listing.GetScenPartRect(this, RowHeight);
    if (Widgets.ButtonText(rect, prefabDef?.defName ?? "VMF_SelectPrefab".Translate().CapitalizeFirst()))
    {
      var options = new List<FloatMenuOption> { new(" ", () => prefabDef = null) };
      foreach (var prefab in DefDatabase<PrefabDef>.AllDefsListForReading)
      {
        options.Add(new FloatMenuOption(prefab.defName, () => prefabDef = prefab));
      }
      Find.WindowStack.Add(new FloatMenu(options));
    }
  }

  public override void Randomize()
  {
    base.Randomize();
    prefabDef = null;
  }

  public override IEnumerable<Thing> PlayerStartingThings()
  {
    foreach (var thing in base.PlayerStartingThings())
    {
      if (prefabDef is not null && thing is VehiclePawnWithMap vehicle)
      {
        _ = vehicle.VehicleMap;
        LongEventHandler.ExecuteWhenFinished(() =>
        {
          PrefabUtility.SpawnPrefab(prefabDef, vehicle.VehicleMap, vehicle.VehicleMap.Center, Rot4.North, Faction.OfPlayer);
        });
      }

      yield return thing;
    }
  }
}