using HarmonyLib;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.VeryLow)]
public class Patches_PUAH
{
    static Patches_PUAH()
    {
        if (ModCompat.PickUpAndHaul)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_PUAH");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_PUAH")]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "PotentialWorkThingsGlobal")]
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

[HarmonyPatchCategory("VMF_Patches_PUAH")]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory+ThingPositionComparer", "Compare")]
public static class Patch_ThingPositionComparer_Compare
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_PUAH")]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "JobOnThing")]
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
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_TargetMapOrThingMap),
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
}

[HarmonyPatchCategory("VMF_Patches_PUAH")]
[HarmonyPatch("PickUpAndHaul.WorkGiver_HaulToInventory", "AllocateThingAtCell")]
public static class Patch_WorkGiver_HaulToInventory_AllocateThingAtCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_PUAH")]
[HarmonyPatch("PickUpAndHaul.JobDriver_HaulToInventory", "TryMakePreToilReservations")]
public static class Patch_JobDriver_HaulToInventory_TryMakePreToilReservations
{
    public static bool Prefix(Job ___job, Pawn ___pawn)
    {
        if (___job.targetQueueB.NotNullAndAny()) return true;

        var message = $"{___pawn} starting HaulToInventory job: {___job.targetQueueA.ToStringSafeEnumerable()}:{___job.countQueue.ToStringSafeEnumerable()}";
        if (Message != null)
        {
            Message(message);
        }
        else
        {
            Log.Message(message);
        }
        ___pawn.ReserveAsManyAsPossible(___job.targetQueueB, ___job);
        return ___pawn.Reserve(___job.targetB, ___job);
    }

    private static Action<string> Message = (Action<string>)AccessTools.Method("PickUpAndHaul.Log:Message")?.CreateDelegate(typeof(Action<string>));
}