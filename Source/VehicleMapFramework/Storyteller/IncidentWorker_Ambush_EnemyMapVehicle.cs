using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI.Group;

namespace VehicleMapFramework;

public class IncidentWorker_Ambush_EnemyMapVehicle : IncidentWorker_AmbushMapVehicle
{
  public static LinearCurve VehicleCountByPointsCurve { get; } =
  [
    new CurvePoint(0f, 1f),
    new CurvePoint(1000f, 1f),
    new CurvePoint(3000f, 1f),
    new CurvePoint(5000f, 3f),
    new CurvePoint(20000f, 5f)
  ];

  protected override WorldObjectDef MapParentDef => WorldObjectDefOf.Ambush;

  protected override bool CanFireNowSub(IncidentParms parms)
  {
    return base.CanFireNowSub(parms) && PawnGroupMakerUtility.TryGetRandomFactionForCombatPawnGroup(parms.points, out _);
  }

  protected override List<Pawn> GeneratePawns(IncidentParms parms)
  {
    var defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat, parms);
    defaultPawnGroupMakerParms.generateFightersOnly = true;
    defaultPawnGroupMakerParms.dontUseSingleUseRocketLaunchers = true;
    return PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
  }

  protected override List<VehiclePawnWithMap> GenerateVehicles(IncidentParms parms)
  {
    var category = RaidInjectionHelper.GetResolvedCategory(parms);
    var availableDefs = DefDatabase<VehicleDef>.AllDefs
      .Where(vehicleDef => ValidRaiderVehicle(vehicleDef, category, null, parms.faction, parms.points))
      .ToList();
    var list = MapVehicleGroupMakerUtility.GenerateVehicles(parms.faction,
      parms.points,
      VehicleCountByPointsCurve,
      availableDefs).ToList();
    parms.points = Mathf.Max(parms.points - list.Sum(v => v.VehicleDef.combatPower), parms.faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));
    return list;
  }

  protected virtual bool ValidRaiderVehicle(VehicleDef vehicleDef, VehicleCategory category,
    PawnsArrivalModeDef arrivalModeDef, Faction faction, float points)
  {
    return VehicleCaravanIncidentUtility.ValidThreatVehicle(vehicleDef, category, arrivalModeDef, faction, points);
  }

  protected override LordJob CreateLordJob(List<Pawn> generatedPawns, IncidentParms parms)
  {
    return new LordJob_AssaultColony(parms.faction,
      canTimeoutOrFlee: false,
      useAvoidGridSmart: true,
      canPickUpOpportunisticWeapons: true);
  }

  protected override LordJob CreateLordJob(List<VehiclePawnWithMap> generatedVehicles, IncidentParms parms)
  {
    return new LordJob_ArmoredAssault(parms.faction, LordJob_ArmoredAssault.RaiderPermissions.All);
  }

  protected override string GetLetterText(Pawn anyPawn, IncidentParms parms)
  {
    return def.letterText
      .Formatted(parms.target is Caravan caravan ? caravan.Name : "yourCaravan".TranslateSimple(),
        parms.faction.def.pawnsPlural,
        parms.faction.NameColored).Resolve().CapitalizeFirst();
  }
}
