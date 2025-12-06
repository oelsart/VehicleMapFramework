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
    protected virtual LinearCurve VehicleCountByPointsCurve { get; } =
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
        var raiderModExtension = parms.faction.def.GetModExtension<VehicleRaiderDefModExtension>();
        var vehicleBudget = (raiderModExtension?.pointMultiplier ?? 1f) * parms.points / 2f;
        if (vehicleBudget <= 0f) return [];
        
        var budgetSpent = 0f;
        var vehicleCount = Mathf.FloorToInt(VehicleCountByPointsCurve.Evaluate(parms.points));
        if (vehicleCount <= 0) return [];
        
        var category = RaidInjectionHelper.GetResolvedCategory(parms);
        var availableDefs = DefDatabase<VehicleDef>.AllDefsListForReading
            .Where(vehicleDef => ValidRaiderVehicle(vehicleDef, category, null, parms.faction, vehicleBudget))
            .ToList();
        
        var result = new List<VehiclePawnWithMap>();
        if (availableDefs.Count > 0)
        {
            for (var i = 0; i < vehicleCount; i++)
            {
                var budget = vehicleBudget;
                if (!availableDefs.Where(vehicleDef => vehicleDef.combatPower <= budget)
                        .TryRandomElementByWeight(vehicleDef => vehicleDef.combatPower, out var vehicleDef2))
                    continue;
                
                result.Add((VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(vehicleDef2, parms.faction));
                vehicleBudget -= vehicleDef2.combatPower;
                budgetSpent += vehicleDef2.combatPower;
            }

            parms.points = Mathf.Max(parms.points - budgetSpent, 10f);
        }

        return result;
    }

    protected virtual bool ValidRaiderVehicle(VehicleDef vehicleDef, VehicleCategory category,
        PawnsArrivalModeDef arrivalModeDef, Faction faction, float points)
    {
        return vehicleDef.thingClass.SameOrSubclassOf<VehiclePawnWithMap>() && vehicleDef.HasComp<CompNpcVehicleMap>() &&
               RaidInjectionHelper.ValidRaiderVehicle(vehicleDef, category, arrivalModeDef, faction, points);
    }

    protected override LordJob CreateLordJob(List<Pawn> generatedPawns, IncidentParms parms)
    {
        return new LordJob_AssaultColony(parms.faction, true, false);
    }

    protected override LordJob CreateLordJob(List<VehiclePawnWithMap> generatedVehicles, IncidentParms parms)
    {
        return new LordJob_ArmoredAssault(parms.faction, LordJob_ArmoredAssault.RaiderPermissions.All);
    }

    protected override string GetLetterText(Pawn anyPawn, IncidentParms parms)
    {
        return this.def.letterText
            .Formatted(parms.target is Caravan caravan ? caravan.Name : "yourCaravan".TranslateSimple(),
                parms.faction.def.pawnsPlural, parms.faction.NameColored).Resolve().CapitalizeFirst();
    }
}