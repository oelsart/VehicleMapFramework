using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Vehicles;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework;

public class VehicleCaravanIncidentUtility
{
  public static int CalculateIncidentMapSize(List<VehiclePawn> caravanVehicles, List<VehiclePawnWithMap> enemyVehicles)
  {
    var allVehicles = caravanVehicles.ConcatIfNotNull(enemyVehicles).ToList();
    var maxSize = allVehicles.Select(v => v.def.size.x).Concat(allVehicles.Select(v => v.def.size.z)).Max();
    return Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(Mathf.RoundToInt(allVehicles.Count * 300 * maxSize))), 75, 200);
  }

  public static Map SetupCaravanAttackMap(VehicleCaravan caravan, List<Pawn> enemies,
    bool sendLetterIfRelatedPawns, WorldObjectDef mapParent, CaravanEnterMode enterMode = CaravanEnterMode.Edge)
  {
    var first = caravan.Vehicles.FirstOrDefault();
    if (first is null) return null;
    var num = CalculateIncidentMapSize(caravan.VehiclesListForReading, null);
    var map = CaravanIncidentUtility.GetOrGenerateMapForIncident(caravan, new IntVec3(num, 1, num), mapParent);
    if (map is null) return null;

    // キャラバンスポーン
    EnterMapUtilityVehicles.EnterMap(caravan, map,
      new EnterMapUtilityVehicles.SpawnParams(enterMode)
      {
        draftColonists = true
      });

    // 敵側スポーン
    var root = enterMode == CaravanEnterMode.Edge ? map.Center : CellFinder.RandomEdgeCell(map);
    for (var i = 0; i < enemies.Count; i++)
    {
      var intVec2 = CellFinder.RandomSpawnCellForPawnNear(root, map);
      GenSpawn.Spawn(enemies[i], intVec2, map, Rot4.Random);
    }
    if (sendLetterIfRelatedPawns)
    {
      PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(enemies,
        "LetterRelatedPawnsGroupGeneric".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, true);
    }

    return map;
  }

  public static Map SetupCaravanAttackMap(VehicleCaravan caravan, List<VehiclePawnWithMap> vehicles, List<Pawn> enemies,
    bool sendLetterIfRelatedPawns, WorldObjectDef mapParent, CaravanEnterMode enterMode = CaravanEnterMode.Edge)
  {
    try
    {
      var first = caravan.Vehicles.FirstOrDefault();
      if (first is null) return null;
      var num = CalculateIncidentMapSize(caravan.VehiclesListForReading, vehicles);
      var map = CaravanIncidentUtility.GetOrGenerateMapForIncident(caravan, new IntVec3(num, 1, num), mapParent);
      if (map is null) return null;

      // キャラバンスポーン
      EnterMapUtilityVehicles.EnterMap(caravan, map,
        new EnterMapUtilityVehicles.SpawnParams(enterMode)
        {
          draftColonists = true
        });

      // 敵側スポーン
      SpawnEnemies(map, vehicles, enemies, CellRect.WholeMap(map).GetClosestEdge(first.Position).Opposite);

      if (sendLetterIfRelatedPawns)
      {
        PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(enemies,
          "LetterRelatedPawnsGroupGeneric".Translate(Faction.OfPlayer.def.pawnsPlural),
          LetterDefOf.NeutralEvent,
          true);
      }

      return map;
    }
    catch(Exception ex)
    {
      vehicles.ForEach(v => v.Destroy());
      VMF_Log.Error($"Error within SetupCaravanAttackMap: {ex}");
      return null;
    }
  }

  public static void SpawnEnemies(Map map, List<VehiclePawnWithMap> vehicles, List<Pawn> enemies, Rot4? edge = null)
  {
    vehicles.SortBy(v => v.CompNpcVehicleMap?.Props.pawnCountWeight ?? 0f);
    var pawnCounts = PawnAllocation();
    var index = 0;
    var mapping = map.GetCachedMapComponent<VehiclePathingSystem>();
    for (var i = 0; i < vehicles.Count; i++)
    {
      var vehicle = vehicles[i];
      var count = pawnCounts[i];
      var vehicleMap = vehicle.VehicleMap;
      if (vehicle.CompNpcVehicleMap is not null)
      {
        vehicle.CompNpcVehicleMap.SetParams(count);
        var prefab = vehicle.CompNpcVehicleMap.Params.prefabDef;
        // MapExpanderによってサイズが変わる車両用に先にPrefabをスポーンさせる
        PrefabUtility.SpawnPrefab(prefab, vehicleMap, vehicleMap.Center, Rot4.North, vehicle.Faction,
          onSpawned: thing =>
          {
            if (thing is ThingWithComps thingWithComps)
            {
              if (thingWithComps.TryGetComp<CompPowerBattery>(out var comp))
                comp.SetStoredEnergyPct(1f);
              if (thingWithComps.TryGetComp<CompDrawAdditionalGraphicsOpacity>(out var comp2))
                comp2.Opacity = 0.5f;
            }
          });
        vehicle.Resize();
      }

      if (!SpawnVehicle(vehicle)) continue;

      // UpgradeBuildableをExecuteWhenFinishedで呼んでいるため
      LongEventHandler.ExecuteWhenFinished(() =>
      {
        var allVehiclesOnMap = vehicle.VehicleMap.GetDetachedMapComponent<VehiclePositionManager>().AllClaimants;
        for (var j = index; index < j + count; index++)
        {
          var pawn = enemies[index];

          // マップ車両に乗り込む
          if (vehicle.SeatsAvailable > 0)
          {
            if (!vehicle.TryAddPawn(pawn))
              VMF_Log.Error($"Unable to add {pawn} to {vehicle} during raid generation.");
            else continue;
          }

          // 車両マップ上の車両に乗り込む
          var vehicle2 = allVehiclesOnMap.FirstOrDefault(v => v.SeatsAvailable > 0);
          if (vehicle2 != null)
          {
            if (!vehicle2.TryAddPawn(pawn))
              VMF_Log.Error($"Unable to add {pawn} to {vehicle} during raid generation.");
            else
            {
              if (vehicle2 is { CompVehicleTurrets: { CanDeploy: true, Deployed: false } })
                vehicle2.CompVehicleTurrets.ToggleDeployment();
              continue;
            }
          }

          var pos = CellFinderExtended.RandomSpawnCellForPawnNear(vehicleMap.Center, vehicleMap, pawn,
            _ => true);
          GenSpawn.Spawn(pawn, pos, vehicleMap, Rot4.South);
        }
      });
    }

    return;

    int[] PawnAllocation()
    {
      var counts = new int[vehicles.Count];
      if (vehicles.Count == 0) return counts;
      var pawnCount = enemies.Count;
      var num = pawnCount;
      var weightSum = vehicles.Sum(v => v.CompNpcVehicleMap?.Props.pawnCountWeight ?? 0f);
      if (weightSum == 0f) weightSum = 1f;
      for (var i = 0; i < vehicles.Count; i++)
      {
        var vehicle = vehicles[i];
        var weight = vehicle.CompNpcVehicleMap?.Props.pawnCountWeight ?? 0f;
        var num2 = Mathf.FloorToInt(weight / weightSum * pawnCount);
        counts[i] = num2;
        num -= num2;
      }

      for (var i = 0; i < num; i++)
        counts[i % vehicles.Count]++;
      return counts;
    }

    bool SpawnVehicle(VehiclePawnWithMap vehicle)
    {
      var rot = edge ?? Rot4.Random;
      vehicle.Rotation = rot;
      var pathData = mapping[vehicle.VehicleDef];
      if (!pathData.VehiclePathGrid.Enabled) pathData.VehiclePathGrid.RecalculateAllPerceivedPathCosts();
      if (!pathData.VehicleRegionAndRoomUpdater.Enabled) pathData.VehicleRegionAndRoomUpdater.Init();
      
      if (!TryFindNearEdgeCell(map, vehicle.VehicleDef, rot, out var cell))
      {
        VMF_Log.Error($"Unable to find spawn position for vehicle {vehicle}");
        vehicle.Destroy();
        return false;
      }

      var result = GenSpawn.Spawn(vehicle, cell, map, rot) is not null;
      return result;
    }
  }
  
  private static bool TryFindNearEdgeCell(Map map, VehicleDef vehicleDef, Rot4 rot, out IntVec3 root)
  {
    if (CellFinderExtended.TryFindRandomEdgeCellWith(Validator, map, rot, vehicleDef, 
          CellFinder.EdgeRoadChance_Hostile, out root))
    {
      root = CellFinderExtended.RandomClosewalkCellNear(root, map, vehicleDef, 5);
      return true;
    }
    return false;

    bool Validator(IntVec3 cell)
    {
      return cell.Standable(vehicleDef, map) && !cell.Fogged(map);
    }
  }

  public static bool ValidThreatVehicle(VehicleDef vehicleDef, VehicleCategory category,
    PawnsArrivalModeDef arrivalModeDef, Faction faction, float points)
  {
    return vehicleDef.thingClass.SameOrSubclassOf<VehiclePawnWithMap>() && vehicleDef.HasComp<CompNpcVehicleMap>() &&
           RaidInjectionHelper.ValidRaiderVehicle(vehicleDef, category, arrivalModeDef, faction, points) &&
           vehicleDef.GetModExtension<VehicleMapProps_Unique>() is null or { baseDef: null } &&
           UniqueVehicleUtility.AllowGenerate(vehicleDef);
  }

  public static bool ValidSeaThreatVehicle(VehicleDef vehicleDef, VehicleCategory category,
    PawnsArrivalModeDef arrivalModeDef, Faction faction, float points)
  {
    return vehicleDef.thingClass.SameOrSubclassOf<VehiclePawnWithMap>() && vehicleDef.HasComp<CompNpcVehicleMap>() &&
           vehicleDef.GetModExtension<VehicleMapProps_Unique>() is null or { baseDef: null } &&
           UniqueVehicleUtility.AllowGenerate(vehicleDef) &&
           vehicleDef.type == VehicleType.Sea && (vehicleDef.vehicleCategory & category) == category &&
           vehicleDef.combatPower <= points && faction.def.techLevel >= vehicleDef.techLevel &&
           (vehicleDef.enabled & VehicleEnabled.For.Raiders) != VehicleEnabled.For.None &&
           vehicleDef.npcProperties != null && (vehicleDef.npcProperties.raidParams == null ||
                                                vehicleDef.npcProperties.raidParams.Allows(faction,
                                                  arrivalModeDef));
  }
}