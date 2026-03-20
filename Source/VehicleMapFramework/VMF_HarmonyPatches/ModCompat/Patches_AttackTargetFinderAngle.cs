using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_AttackTargetFinderAngle
{
    public delegate IAttackTarget FuncBestAttackTarget(IAttackTargetSearcher searcher, TargetScanFlags flags,
        Vector3 angle, Predicate<Thing> validator, float minDist, float maxDist, IntVec3 locus,
        float maxTravelRadiusFromLocus, bool canTakeTargetsCloserThanEffectiveMinRange);
    
    public static readonly FuncBestAttackTarget BestAttackTarget;
    
    static Patches_AttackTargetFinderAngle()
    {
        var type = AccessTools.TypeByName("AttackTargetFinderAngle");
        if (type is null) return;
        var method = AccessTools.Method(type, "BestAttackTarget");
        if (method is null) return;
        
        BestAttackTarget = AccessTools.MethodDelegate<FuncBestAttackTarget>(method);
        if (BestAttackTarget is null) return;
        
        VMF_Harmony.PatchCategory(PatchCategories.AttackTargetFinderAngle);
    }
}

[HarmonyPatchCategory(PatchCategories.AttackTargetFinderAngle)]
[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_AttackTargetFinderAngle_BestAttackTarget
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return from type in GenTypes.AllTypes
            where type.Name == "AttackTargetFinderAngle"
            select AccessTools.Method(type, "BestAttackTarget");
    }
    
    private static bool working;
    
    public static void Postfix(IAttackTargetSearcher searcher, TargetScanFlags flags, Vector3 angle,
        Predicate<Thing> validator, float minDist, float maxDist, IntVec3 locus,
        float maxTravelRadiusFromLocus, bool canTakeTargetsCloserThanEffectiveMinRange, ref IAttackTarget __result)
    {
        if (working) return;

        var map = searcher.Thing.Map;
        var pawn = searcher.Thing as Pawn;
        pawn?.DepartMap = map;
        var pos = searcher.Thing.Position;
        var basePos = searcher.Thing.PositionOnBaseMap;
        foreach (var map2 in map.BaseMapAndVehicleMaps(false))
        {
            IAttackTarget target = null;
            try
            {
                working = true;
                searcher.Thing.VirtualMapTransfer(map2, map2.IsVehicleMapOf(out var vehicle) ? basePos.ToVehicleMapCoord(vehicle) : basePos);
                target = Patches_AttackTargetFinderAngle.BestAttackTarget(searcher, flags, angle, validator, minDist, maxDist, locus, maxTravelRadiusFromLocus, canTakeTargetsCloserThanEffectiveMinRange);
            }
            finally
            {
                working = false;
                searcher.Thing.VirtualMapTransfer(map, pos);
                pawn?.RemoveDepartMap();
                __result = AttackTargetFinderOnVehicle.CompareTarget(__result, target, searcher);
            }
        }
    }
}