using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Vehicles;
using Vehicles.World;
using Verse;
using Verse.AI.Group;

namespace VehicleMapFramework;

public abstract class IncidentWorker_AmbushMapVehicle : IncidentWorker
{
  protected abstract WorldObjectDef MapParentDef { get; }

  protected abstract List<VehiclePawnWithMap> GenerateVehicles(IncidentParms parms);

  protected abstract List<Pawn> GeneratePawns(IncidentParms parms);

  protected virtual void PostProcessGeneratedPawnsAfterSpawning(List<Pawn> generatedPawns) { }

  protected virtual void PostProcessGeneratedVehiclesAfterSpawning(List<VehiclePawnWithMap> generatedVehicles) { }

  protected virtual LordJob CreateLordJob(List<Pawn> generatedPawns, IncidentParms parms)
  {
    return null;
  }

  protected virtual LordJob CreateLordJob(List<VehiclePawnWithMap> generatedVehicles, IncidentParms parms)
  {
    return null;
  }

  protected override bool CanFireNowSub(IncidentParms parms)
  {
    return parms.target is Map map
      ? CellFinder.TryFindRandomEdgeCellWith(x => x.Standable(map) && map.reachability.CanReachColony(x), map, CellFinder.EdgeRoadChance_Hostile, out _)
      : parms.target is VehicleCaravan && CaravanIncidentUtility.CanFireIncidentWhichWantsToGenerateMapAt(parms.target.Tile);
  }

  private static void CleanUpGeneratedVehicles(List<VehiclePawnWithMap> generatedVehicles)
  {
    foreach (var vehicle in generatedVehicles)
    {
      if (!vehicle.Destroyed)
        vehicle.Destroy();
    }
  }

  protected override bool TryExecuteWorker(IncidentParms parms)
  {
    if (!PawnGroupMakerUtility.TryGetRandomFactionForCombatPawnGroup(parms.points, out parms.faction))
    {
      Log.Error($"Could not find any valid faction for {def} incident.");
      return false;
    }
    var map = parms.target as Map;
    var existingMapEdgeCell = IntVec3.Invalid;

    var generatedVehicles = GenerateVehicles(parms);
    if (generatedVehicles.Empty())
    {
      VMF_Log.DebugWarning($"{GetType()}: generatedVehicles empty");
      return false;
    }

    var largestVehicle = generatedVehicles.MaxBy(v => v.def.size.Area);

    if (map != null && !TryFindCellEdgeCell(Rot4.North) && !TryFindCellEdgeCell(Rot4.South) &&
        !TryFindCellEdgeCell(Rot4.East) && !TryFindCellEdgeCell(Rot4.West))
    {
      CleanUpGeneratedVehicles(generatedVehicles);
      VMF_Log.DebugWarning($"{GetType()}: could not find edge cell");
      return false;
    }
    var generatedEnemies = GeneratePawns(parms);
    if (generatedEnemies.Empty())
    {
      VMF_Log.DebugWarning($"{GetType()} generatedEnemies empty");
      return false;
    }

    if (map != null)
    {
      if (!DoExecute(parms, generatedVehicles, generatedEnemies, existingMapEdgeCell))
      {
        CleanUpGeneratedVehicles(generatedVehicles);
        VMF_Log.DebugWarning($"{GetType()} fail to execute");
        return false;
      }
      return true;
    }
    LongEventHandler.QueueLongEvent(() =>
      {
        if (!DoExecute(parms, generatedVehicles, generatedEnemies, existingMapEdgeCell))
          CleanUpGeneratedVehicles(generatedVehicles);
      },
      "GeneratingMapForNewEncounter",
      false,
      null);
    return true;

    bool TryFindCellEdgeCell(Rot4 rot)
    {
      return CellFinderExtended.TryFindRandomEdgeCellWith(c => largestVehicle.CellRectStandable(map, c),
        map,
        rot,
        largestVehicle.VehicleDef,
        CellFinder.EdgeRoadChance_Hostile,
        out existingMapEdgeCell);
    }
  }

  private bool DoExecute(IncidentParms parms, List<VehiclePawnWithMap> generatedVehicles, List<Pawn> generatedEnemies, IntVec3 existingMapEdgeCell)
  {
    var flag = false;
    if (parms.target is Map map)
    {
      var edge = CellRect.WholeMap(map).GetClosestEdge(existingMapEdgeCell);
      VehicleCaravanIncidentUtility.SpawnEnemies(map, generatedVehicles, generatedEnemies, edge);
    }
    else if (parms.target is VehicleCaravan vehicleCaravan)
    {
      map = VehicleCaravanIncidentUtility.SetupCaravanAttackMap(vehicleCaravan, generatedVehicles, generatedEnemies, false, MapParentDef);
      flag = true;
    }
    else
    {
      return false;
    }

    if (map is null) return false;

    PostProcessGeneratedPawnsAfterSpawning(generatedEnemies);
    PostProcessGeneratedVehiclesAfterSpawning(generatedVehicles);

    var lordJob = CreateLordJob(generatedEnemies, parms);
    if (lordJob != null)
      LordMaker.MakeNewLord(parms.faction, lordJob, map, generatedEnemies);
    var lordJob2 = CreateLordJob(generatedVehicles, parms);
    if (lordJob2 != null)
      LordMaker.MakeNewLord(parms.faction, lordJob2, map, generatedVehicles);

    TaggedString taggedString = GetLetterLabel(generatedEnemies[0], parms);
    TaggedString taggedString2 = GetLetterText(generatedEnemies[0], parms);
    PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter(generatedEnemies,
      ref taggedString,
      ref taggedString2,
      GetRelatedPawnsInfoLetterText(parms),
      true);
    SendStandardLetter(taggedString, taggedString2, GetLetterDef(generatedEnemies[0], parms), parms, generatedEnemies[0]);
    if (flag)
    {
      Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
    }
    return true;
  }

  protected virtual string GetLetterLabel(Pawn anyPawn, IncidentParms parms)
  {
    return def.letterLabel;
  }

  protected virtual string GetLetterText(Pawn anyPawn, IncidentParms parms)
  {
    return def.letterText;
  }

  protected virtual LetterDef GetLetterDef(Pawn anyPawn, IncidentParms parms)
  {
    return def.letterDef;
  }

  protected virtual string GetRelatedPawnsInfoLetterText(IncidentParms parms)
  {
    return "LetterRelatedPawnsGroupGeneric".Translate(Faction.OfPlayer.def.pawnsPlural);
  }
}
