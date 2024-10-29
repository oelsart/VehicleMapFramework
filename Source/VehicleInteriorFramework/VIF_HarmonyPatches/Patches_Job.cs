using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Patch_Pawn_JobTracker_StartJob
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_MakeDriver = AccessTools.Method(typeof(Job), nameof(Job.MakeDriver));
            var m_GetCachedDriver = AccessTools.Method(typeof(Job), nameof(Job.GetCachedDriver));
            return instructions.MethodReplacer(m_MakeDriver, m_GetCachedDriver);
        }
    }

    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    public static class Patch_JobGiver_Work_TryIssueJobPackage
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 17);

            var pos2 = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 9);
            var ldfld_scanner = codes[pos2 + 1];

            var pos3 = codes.FindLastIndex(pos2 - 1, c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 9);
            var ldfld_innerClass = codes[pos3 + 1];
            var ldfld_pawn = codes[pos3 + 2];

            var addedCodes = new[]
            {
                CodeInstruction.LoadLocal(9),
                ldfld_innerClass,
                ldfld_pawn,
                CodeInstruction.LoadLocal(9),
                ldfld_scanner,
                CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(Patch_JobGiver_Work_TryIssueJobPackage.AddSearchSet))
            };
            codes.InsertRange(pos, addedCodes);

            var pos4 = codes.FindIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Position));
            codes[pos4] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PositionOnBaseMap);

            var pos5 = codes.FindIndex(pos4, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 19);
            codes.InsertRange(pos5, addedCodes);

            var pos6 = codes.FindIndex(pos5, c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Position));
            codes[pos6] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PositionOnBaseMap);

            var m_GenClosest_ClosestThing_Global = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global));
            var m_GenClosestOnVehicle_ClosestThing_Global = AccessTools.Method(typeof(GenClosestOnVehicle), nameof(GenClosestOnVehicle.ClosestThing_Global),
                new[] { typeof(IntVec3), typeof(IEnumerable<>), typeof(float), typeof(Predicate<Thing>), typeof(Func<Thing, float>) } );
            var m_GenClosest_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable));
            var m_GenClosestOnVehicle_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosestOnVehicle), nameof(GenClosestOnVehicle.ClosestThing_Global_Reachable),
                new[] { typeof(IntVec3), typeof(Map), typeof(IEnumerable<Thing>), typeof(PathEndMode), typeof(TraverseParms), typeof(float), typeof(Predicate<Thing>), typeof(Func<Thing, float>) });
            var m_GenClosest_ClosestThingReachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable));
            var m_GenClosestOnVehicle_ClosestThingReachable = AccessTools.Method(typeof(GenClosestOnVehicle), nameof(GenClosestOnVehicle.ClosestThingReachable),
                new[] { typeof(IntVec3), typeof(Map), typeof(ThingRequest), typeof(PathEndMode), typeof(TraverseParms), typeof(float), typeof(Predicate<Thing>), typeof(IEnumerable<Thing>), typeof(int), typeof(int), typeof(bool), typeof(RegionType), typeof(bool)});
            return codes.MethodReplacer(m_GenClosest_ClosestThing_Global, m_GenClosestOnVehicle_ClosestThing_Global)
                .MethodReplacer(m_GenClosest_ClosestThing_Global_Reachable, m_GenClosestOnVehicle_ClosestThing_Global_Reachable)
                .MethodReplacer(m_GenClosest_ClosestThingReachable, m_GenClosestOnVehicle_ClosestThingReachable);
        }

        private static IEnumerable<Thing> AddSearchSet(List<Thing> list, Pawn pawn, WorkGiver_Scanner scanner)
        {
            var searchSet = new List<Thing>(list);
            var baseMap = pawn.BaseMap();
            if (baseMap != pawn.Map)
            {
                searchSet.AddRange(baseMap.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest));
            }
            foreach(var vehicle in VehiclePawnWithMapCache.allVehicles[baseMap])
            {
                if (pawn.Map != vehicle.interiorMap)
                {
                    searchSet.AddRange(vehicle.interiorMap.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest));
                }
            }
            return searchSet;
        }
    }
}
