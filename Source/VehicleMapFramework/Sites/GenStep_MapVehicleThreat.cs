using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class GenStep_MapVehicleThreat : GenStep
{
  public override int SeedPart => 167961163;

  protected virtual bool ValidRaiderVehicle(VehicleDef vehicleDef, VehicleCategory category, PawnsArrivalModeDef arrivalModeDef,
    Faction faction, float points)
  {
    return VehicleCaravanIncidentUtility.ValidThreatVehicle(vehicleDef, category, arrivalModeDef, faction, points);
  }

  protected virtual List<VehiclePawnWithMap> GenerateVehicles(Faction faction, SitePart sitePart)
  {
    var minPoints = faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat);
    var points = Mathf.Max(sitePart.parms.points, minPoints);
    const VehicleCategory category = VehicleCategory.Combat;
    var availableDefs = DefDatabase<VehicleDef>.AllDefs
      .Where(vehicleDef => ValidRaiderVehicle(vehicleDef, category, null, faction, points))
      .ToList();
    var list = MapVehicleGroupMakerUtility.GenerateVehicles(faction,
      points,
      IncidentWorker_Ambush_EnemyMapVehicle.VehicleCountByPointsCurve,
      availableDefs).ToList();
    points = Mathf.Max(points - list.Sum(v => v.VehicleDef.combatPower), minPoints);
    return list;
  }

  protected virtual List<Pawn> GeneratePawns(Faction faction, SitePart sitePart)
  {
    return PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
    {
      groupKind = PawnGroupKindDefOf.Combat, tile = sitePart.site.Tile, faction = faction, points = Mathf.Max(sitePart.parms.points, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat))
    }).ToList();
  }

  public override void Generate(Map map, GenStepParams parms)
  {
    var faction = parms.sitePart.site.Faction is { IsPlayer: false }
      ? parms.sitePart.site.Faction
      : Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
    VehicleCaravanIncidentUtility.SpawnEnemies(map,
      GenerateVehicles(faction, parms.sitePart),
      GeneratePawns(faction, parms.sitePart));
  }
}
