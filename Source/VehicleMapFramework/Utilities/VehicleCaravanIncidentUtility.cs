using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Vehicles;
using Vehicles.World;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class VehicleCaravanIncidentUtility
{
    public static int CalculateIncidentMapSize(List<VehiclePawn> caravanVehicles, List<VehiclePawnWithMap> enemyVehicles)
    {
        var allVehicles = caravanVehicles.Concat(enemyVehicles).ToList();
        var maxSize = allVehicles.Select(v => v.def.size.x).Concat(allVehicles.Select(v => v.def.size.z)).Max();
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(Mathf.RoundToInt(allVehicles.Count * 300 * maxSize))), 75, 200);
    }
    
    public static Map SetupCaravanAttackMap(VehicleCaravan caravan, List<VehiclePawnWithMap> vehicles, List<Pawn> enemies, bool sendLetterIfRelatedPawns, WorldObjectDef mapParent)
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
                new EnterMapUtilityVehicles.SpawnParams(CaravanEnterMode.Edge)
                {
                    draftColonists = true
                });

            // 敵側スポーン
            SpawnEnemies(map, vehicles, enemies, CellRect.WholeMap(map).GetClosestEdge(first.Position).Opposite, first);

            if (sendLetterIfRelatedPawns)
            {
                PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(enemies,
                    "LetterRelatedPawnsGroupGeneric".Translate(Faction.OfPlayer.def.pawnsPlural),
                    LetterDefOf.NeutralEvent,
                    true);
            }

            return map;
        }
        catch
        {
            vehicles.ForEach(v => v.Destroy());
            return null;
        }
    }

    public static void SpawnEnemies(Map map, List<VehiclePawnWithMap> vehicles, List<Pawn> enemies, Rot4 edge, VehiclePawn playerVehicle = null)
    {
        var opposite = edge.Opposite;
        vehicles.SortBy(v => v.CompNpcVehicleMap?.Props.pawnCountWeight ?? 0f);
        var pawnCounts = PawnAllocation();
        var index = 0;
        var mapping = map.GetCachedMapComponent<VehiclePathingSystem>();
        for (var i = 0; i < vehicles.Count; i++)
        {
            var vehicle = vehicles[i];
            var count = pawnCounts[i];
            var vehicleMap = vehicle.VehicleMap;
            if (vehicle.CompNpcVehicleMap != null)
            {
                vehicle.CompNpcVehicleMap.SetParams(count);
                var prefab = vehicle.CompNpcVehicleMap.Params.prefabDef;
                // MapExpanderによってサイズが変わる車両用に先にPrefabをスポーンさせる
                PrefabUtility.SpawnPrefab(prefab, vehicleMap, vehicleMap.Center, Rot4.North, vehicle.Faction, onSpawned: thing =>
                {
                    if (thing is ThingWithComps thingWithComps)
                    {
                        if (thingWithComps.TryGetComp<CompPowerBattery>(out var comp))
                            comp.SetStoredEnergyPct(1f);
                        if (thingWithComps.TryGetComp<CompDrawAdditionalGraphicsOpacity>(out var comp2))
                            comp2.Opacity = 0.5f;
                    }
                });
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
            var pawnCount = enemies.Count;
            var num = pawnCount;
            var weightSum = vehicles.Sum(v => v.CompNpcVehicleMap?.Props.pawnCountWeight ?? 0f);
            for (var i = 0; i < vehicles.Count; i++)
            {
                var vehicle = vehicles[i];
                var weight = vehicle.CompNpcVehicleMap?.Props.pawnCountWeight ?? 0f;
                var num2 = Mathf.FloorToInt(weight / weightSum * pawnCount);
                counts[i] = num2;
                num -= num2;
            }
            for (var i = 0; i < num; i++)
                counts[i]++;
            return counts;
        }

        bool SpawnVehicle(VehiclePawnWithMap vehicle)
        {
            Log.Message($"Spawn vehicle: {vehicle}");
            var cell = CellFinder.RandomEdgeCell(edge, map);
            vehicle.Rotation = opposite;
            var pathData = mapping[vehicle.VehicleDef];
            if (!pathData.VehiclePathGrid.Enabled) pathData.VehiclePathGrid.RecalculateAllPerceivedPathCosts();
            if (!pathData.VehicleRegionAndRoomUpdater.Enabled) pathData.VehicleRegionAndRoomUpdater.Init();

            var reachability = map.GetCachedMapComponent<VehiclePathingSystem>()[vehicle.VehicleDef].VehicleReachability;
            var pos2 = CellFinderExtended.RandomSpawnCellForPawnNear(cell, map, vehicle,
                c => vehicle.DrivableRectOnCell(c, true, map) &&
                     reachability.CanReachBase(c, vehicle.VehicleDef) &&
                    (playerVehicle is null || playerVehicle.CanReachVehicle(c, PathEndMode.Touch, Danger.Deadly)),
                vehicle.VehicleDef.type == VehicleType.Sea);
            if (!pos2.IsValid)
            {
                VMF_Log.Error($"Unable to find spawn position for vehicle {vehicle}");
                vehicle.Destroy();
                return false;
            }
            var result = GenSpawn.Spawn(vehicle, pos2, map, opposite) != null;
            return result;
        }
    }
}