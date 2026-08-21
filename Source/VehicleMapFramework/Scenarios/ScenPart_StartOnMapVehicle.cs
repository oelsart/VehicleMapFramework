using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class ScenPart_StartOnMapVehicle : ScenPart
{
  public override void GenerateIntoMap(Map map)
  {
    if (Find.GameInitData == null)
      return;
    
    var list = new List<Thing>();
    foreach (var scenPart in Find.Scenario.AllParts)
    {
      list.AddRange(scenPart.PlayerStartingThings());
    }
    foreach (var pawn in Find.GameInitData.startingAndOptionalPawns)
    {
      foreach (var thingDefCount in Find.GameInitData.startingPossessions[pawn])
      {
        list.Add(StartingPawnUtility.GenerateStartingPossession(thingDefCount));
      }
    }

    var vehicle = list.OfType<VehiclePawnWithMap>().FirstOrDefault();
    if (vehicle is null)
      return;

    list.Remove(vehicle);
    vehicle.SetFactionDirect(Faction.OfPlayer);
    var cell = CellFinderExtended.RandomSpawnCellForPawnNear(map.Center, map, vehicle, c => vehicle.CellRectStandable(map, c));
    GenSpawn.Spawn(vehicle, cell, map);
    
    var list2 = new List<List<Thing>>();
    foreach (var pawn in Find.GameInitData.startingAndOptionalPawns)
    {
      list2.Add([pawn]);
    }
    var num = 0;
    foreach (var thing in list)
    {
      if (thing.def.CanHaveFaction)
      {
        thing.SetFactionDirect(Faction.OfPlayer);
      }
      list2[num].Add(thing);
      num++;
      if (num >= list2.Count)
      {
        num = 0;
      }
    }
    
    // VehicleMap生成を待つためExecuteWhenFinishedでスポーンさせる
    _ = vehicle.VehicleMap;
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      DropPodUtility.DropThingGroupsNear(vehicle.VehicleMap.Center, vehicle.VehicleMap, list2, 110, true, true, true, true, false);
    });
  }
}