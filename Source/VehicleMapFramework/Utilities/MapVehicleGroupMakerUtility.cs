using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public static class MapVehicleGroupMakerUtility
{
  public const float MinPointsToGenerateVehicles = 50f;

  public static IEnumerable<VehiclePawnWithMap> GenerateVehicles(Faction faction, float points, LinearCurve vehicleCountCurve, List<VehicleDef> availableDefs)
  {
    var raiderModExtension = faction.def.GetModExtension<VehicleRaiderDefModExtension>();
    var vehicleBudget = (raiderModExtension?.pointMultiplier ?? 1f) * points / 2f;
    if (vehicleBudget <= 0f)
    {
      VMF_Log.DebugWarning("vehicleBudget <= 0f");
      yield break;
    }
    vehicleBudget = Mathf.Max(vehicleBudget, MinPointsToGenerateVehicles);

    var vehicleCount = Mathf.FloorToInt(vehicleCountCurve.Evaluate(points));
    if (vehicleCount <= 0)
    {
      VMF_Log.DebugWarning("vehicleCount <= 0");
      yield break;
    }

    if (availableDefs.Count > 0)
    {
      for (var i = 0; i < vehicleCount; i++)
      {
        var budget = vehicleBudget;
        if (!availableDefs.Where(vehicleDef => vehicleDef.combatPower <= budget)
              .TryRandomElementByWeight(vehicleDef => vehicleDef.combatPower, out var vehicleDef2))
          continue;

        vehicleBudget -= vehicleDef2.combatPower;
        points = Mathf.Max(points - vehicleDef2.combatPower, 10f);
        yield return (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(vehicleDef2, faction);
      }
    }
  }
}
