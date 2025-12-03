using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using SmashTools;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_PUAH
{
    static Patches_PUAH()
    {
        if (ModCompat.PickUpAndHaul)
        {
            VMF_Harmony.PatchCategory(PatchCategories.PickUpAndHaul);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.PickUpAndHaul)]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "PotentialWorkThingsGlobal")]
[PatchLevel(Level.Sensitive)]
public static class Patch_WorkGiver_HaulToInventory_PotentialWorkThingsGlobal
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        var f_rootCell = AccessTools.Field("PickUpAndHaul.WorkGiver_HaulToInventory+ThingPositionComparer:rootCell");
        codes.MatchStartForward(CodeMatch.StoresField(f_rootCell));
        codes.Insert(
            CodeInstruction.LoadArgument(1),
            CodeInstruction.Call(typeof(Patch_WorkGiver_HaulToInventory_PotentialWorkThingsGlobal), nameof(ToBaseMapCoord)));
        return codes.Instructions();
    }

    public static IntVec3 ToBaseMapCoord(IntVec3 c, Pawn pawn)
    {
        return c.ToBaseMapCoord(pawn.DepartMap ?? pawn.Map);
    }
}

[HarmonyPatchCategory(PatchCategories.PickUpAndHaul)]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory+ThingPositionComparer", "Compare")]
[PatchLevel(Level.Cautious)]
public static class Patch_ThingPositionComparer_Compare
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.PickUpAndHaul)]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "JobOnThing")]
[PatchLevel(Level.Sensitive)]
public static class Patch_WorkGiver_HaulToInventory_JobOnThing
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator);

        ////pawn.Map -> thing.MapHeld ?? pawn.Map
        //codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map));
        //codes.CreateLabelWithOffsets(1, out var label);
        //codes.MatchStartBackwards(new CodeMatch(OpCodes.Ldloc_0));
        //codes.Insert(
        //    CodeInstruction.LoadArgument(1),
        //    new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_MapHeld),
        //    new CodeInstruction(OpCodes.Dup),
        //    new CodeInstruction(OpCodes.Brtrue_S, label),
        //    new CodeInstruction(OpCodes.Pop));

        //HaulToHopperJob(thing, intVec, map) -> HaulToHopperJob(thing, intVec, TargetMapManager.TargetMapOrMap(map, pawn))
        var m_HaulToHopperJob = AccessTools.Method("PickUpAndHaul.WorkGiver_HaulToInventory:HaulToHopperJob");
        codes.MatchStartForward(CodeMatch.Calls(m_HaulToHopperJob));
        codes.Insert(
            CodeInstruction.LoadArgument(1),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_TargetMapOrMap));

        //CapacityAt(thing, storeTarget.cell, map) -> CapacityAt(thing, storeTarget.cell, TargetMapManager.TargetMapOrMap(map, pawn))
        var m_CapacityAt = AccessTools.Method("PickUpAndHaul.WorkGiver_HaulToInventory:CapacityAt");
        codes.MatchStartForward(CodeMatch.Calls(m_CapacityAt));
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
        if (pawn.TryGetTargetMap(out var map) && __result.def?.defName == "HaulToInventory" && __result.targetB.IsValid)
        {
            __result.globalTarget = __result.targetB.ToGlobalTargetInfo(map);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.PickUpAndHaul)]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "AllocateThingAtCell")]
public static class Patch_WorkGiver_HaulToInventory_AllocateThingAtCell
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(Pawn pawn, Thing nextThing)
    {
        if (pawn.TryGetTargetMap(out var map))
        {
            nextThing.TargetMap = map;
        }
    }

    [PatchLevel(Level.Safe)]
    public static void Finaliner(Thing nextThing)
    {
        nextThing.RemoveTargetInfo();
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
    }
}

[HarmonyPatchCategory(PatchCategories.PickUpAndHaul)]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "Stackable")]
[PatchLevel(Level.Cautious)]
public static class Patch_WorkGiver_HaulToInventory_Stackable
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}

[HarmonyPatchCategory(PatchCategories.PickUpAndHaul)]
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