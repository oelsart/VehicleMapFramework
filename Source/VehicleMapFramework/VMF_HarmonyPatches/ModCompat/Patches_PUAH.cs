﻿using HarmonyLib;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_PUAH
{
    public const string Category = "VMF_Patches_PUAH";

    static Patches_PUAH()
    {
        if (ModCompat.PickUpAndHaul)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_PUAH.Category)]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "PotentialWorkThingsGlobal")]
[PatchLevel(Level.Sensitive)]
public static class Patch_WorkGiver_HaulToInventory_PotentialWorkThingsGlobal
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var f_rootCell = AccessTools.Field("PickUpAndHaul.WorkGiver_HaulToInventory+ThingPositionComparer:rootCell");
        var pos = codes.FindIndex(c => c.StoresField(f_rootCell));
        codes.Insert(pos, CodeInstruction.Call(typeof(Patch_WorkGiver_HaulToInventory_PotentialWorkThingsGlobal), nameof(ToBaseMapCoord)));
        return codes;
    }

    public static IntVec3 ToBaseMapCoord(IntVec3 c)
    {
        return c.ToBaseMapCoord(CrossMapReachabilityUtility.DepartMap);
    }
}

[HarmonyPatchCategory(Patches_PUAH.Category)]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory+ThingPositionComparer", "Compare")]
[PatchLevel(Level.Cautious)]
public static class Patch_ThingPositionComparer_Compare
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_PUAH.Category)]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "JobOnThing")]
[PatchLevel(Level.Sensitive)]
public static class Patch_WorkGiver_HaulToInventory_JobOnThing
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator);

        //pawn.Map -> thing.MapHeld ?? pawn.Map
        codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map));
        codes.CreateLabelWithOffsets(1, out var label);
        codes.MatchStartBackwards(new CodeMatch(OpCodes.Ldloc_0));
        codes.Insert(
            CodeInstruction.LoadArgument(1),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_MapHeld),
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Brtrue_S, label),
            new CodeInstruction(OpCodes.Pop));

        //HaulToHopperJob(thing, intVec, map) -> HaulToHopperJob(thing, intVec, TargetMapManager.TargetMapOrMap(map, pawn))
        var m_HaulToHopperJob = AccessTools.Method("PickUpAndHaul.WorkGiver_HaulToInventory:HaulToHopperJob");
        codes.MatchStartForward(CodeMatch.Calls(m_HaulToHopperJob));
        codes.Insert(
            CodeInstruction.LoadArgument(1),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_TargetMapOrMap));

        //storeTarget.Position -> storeTarget.Position.ToBaseMapCoord(TargetMapManager.TargetMapOrThingMap(pawn))
        var g_Position = AccessTools.PropertyGetter("PickUpAndHaul.WorkGiver_HaulToInventory+StoreTarget:Position");
        codes.MatchStartForward(CodeMatch.Calls(g_Position));
        codes.InsertAfter(
            CodeInstruction.LoadArgument(1),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_TargetMapOrPawnMap),
            CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapCoord), [typeof(IntVec3), typeof(Map)]));

        var num = 0;
        return codes.Instructions().Manipulator(c => c.Calls(CachedMethodInfo.g_Thing_Position), c =>
        {
            num++;
            if (num <= 2)
            {
                c.opcode = OpCodes.Call;
                c.operand = CachedMethodInfo.m_PositionOnBaseMap;
            }
        });
    }

    public static void Postfix(Pawn pawn, Job __result)
    {
        if (__result is null) return;
        if (TargetMapManager.HasTargetMap(pawn, out var map) && __result.def?.defName == "HaulToInventory" && __result.targetB.IsValid)
        {
            __result.globalTarget = __result.targetB.ToGlobalTargetInfo(map);
        }
    }
}

[HarmonyPatchCategory(Patches_PUAH.Category)]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "AllocateThingAtCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_WorkGiver_HaulToInventory_AllocateThingAtCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
    }
}

[HarmonyPatchCategory(Patches_PUAH.Category)]
[HarmonyPatch("PickUpAndHaul.JobDriver_HaulToInventory", "TryMakePreToilReservations")]
[PatchLevel(Level.Safe)]
public static class Patch_JobDriver_HaulToInventory_TryMakePreToilReservations
{
    public static bool Prefix(Job ___job, Pawn ___pawn, ref bool __result)
    {
        if (___job.targetQueueA.NotNullAndAny()) return true;
            ___pawn.ReserveAsManyAsPossible(___job.targetQueueA, ___job);
            ___pawn.ReserveAsManyAsPossible(___job.targetQueueB, ___job);
            __result = ___pawn.Reserve(___job.targetB, ___job);
        return false;
    }
}