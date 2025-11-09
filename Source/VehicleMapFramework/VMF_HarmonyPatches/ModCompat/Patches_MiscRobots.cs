using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_MiscRobots
{
    static Patches_MiscRobots()
    {
        if (MiscRobots.Active)
        {
            VMF_Harmony.PatchCategory(PatchCategories.MiscRobots);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.MiscRobots)]
[HarmonyPatch("AIRobot.X2_JobGiver_Return2BaseRoom", "TryIssueJobPackage")]
[PatchLevel(Level.Safe)]
public static class Patch_X2_JobGiver_Return2BaseRoom_TryIssueJobPackage
{

    public static bool Prefix(ThinkNode __instance, Pawn pawn, ref ThinkResult __result)
    {
        if (!MiscRobots.X2_AIRobot?.IsAssignableFrom(pawn.GetType()) ?? true) return true;

        var rechargeStation = MiscRobots.rechargeStation(pawn);
        if (pawn.Map == rechargeStation?.Map) return true;

        if (pawn.DestroyedOrNull())
        {
            __result = ThinkResult.NoJob;
            return false;
        }
        if (!pawn.Spawned)
        {
            __result = ThinkResult.NoJob;
            return false;
        }
        if (rechargeStation.DestroyedOrNull())
        {
            __result = ThinkResult.NoJob;
            return false;
        }
        if (!rechargeStation.Spawned)
        {
            __result = ThinkResult.NoJob;
            return false;
        }
        var roomRecharge = rechargeStation.Position.GetRoom(rechargeStation.Map);
        var roomRobot = pawn.Position.GetRoom(pawn.Map);
        if (roomRecharge == roomRobot)
        {
            __result = ThinkResult.NoJob;
            return false;
        }
        var mapRecharge = rechargeStation.Map;
        var posRecharge = rechargeStation.Position;
        var exitSpot = TargetInfo.Invalid;
        var enterSpot = TargetInfo.Invalid;
        var cell = (from c in roomRecharge.Cells
                        where c.Standable(mapRecharge) && !c.IsForbidden(pawn) && c.InHorDistOf(posRecharge, 5f) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Some, false, false, TraverseMode.ByPawn, rechargeStation.Map, out exitSpot, out enterSpot)
                        select c).FirstOrDefault();
        if (cell == IntVec3.Invalid)
        {
            __result = ThinkResult.NoJob;
            return false;
        }
        var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps, cell);
        job.locomotionUrgency = LocomotionUrgency.Amble;
        job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot);
        __result = new ThinkResult(job, __instance, JobTag.Misc);
        return false;
    }
}

[HarmonyPatchCategory(PatchCategories.MiscRobots)]
[HarmonyPatch("AIRobot.X2_JobGiver_Work", "TryIssueJobPackage")]
[PatchLevel(Level.Sensitive)]
public static class Patch_X2_JobGiver_Work_TryIssueJobPackage
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
    {
        var codes = new CodeMatcher(instructions, generator);
        //scanner変数をローカルに保存しておく
        codes.MatchStartForward(new CodeMatch(c => c.opcode == OpCodes.Isinst && c.operand.Equals(typeof(WorkGiver_Scanner))));
        codes.DeclareLocal(typeof(WorkGiver_Scanner), out var scanner);
        codes.InsertAfterAndAdvance(
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Stloc_S, scanner));

        object local = null;
        var matchStloc = new CodeMatch(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalType == typeof(IEnumerable<Thing>) && c.operand != local);
        var addedCodes = new[]
        {
           CodeInstruction.LoadArgument(1),
           new CodeInstruction(OpCodes.Ldloc_S, scanner),
           CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(Patch_JobGiver_Work_TryIssueJobPackage.AddSearchSet))
       };
        codes.MatchStartForward(matchStloc);
        local = codes.Operand;

        //サーチセットに複数マップのthingリストを足す
        codes.MatchStartForward(matchStloc);
        local = codes.Operand;
        codes.InsertAndAdvance(addedCodes);

        codes.MatchStartForward(matchStloc);
        codes.InsertAndAdvance(addedCodes);

        //複数マップのセルをスキャンする
        //codes.MatchStartForward(new CodeMatch(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalType == typeof(IEnumerable<IntVec3>)));
        //var locals = original.GetMethodBody().LocalVariables;
        //var innerTypeIndex = locals.FirstIndexOf(l => l.LocalType.GetCustomAttribute<CompilerGeneratedAttribute>() != null); //たぶん0のはずだけど一応
        //var innerStructIndex = locals.FirstIndexOf(l => l.LocalType.GetCustomAttribute<CompilerGeneratedAttribute>() != null && l.LocalType.IsStruct());
        //codes.InsertAfterAndAdvance(
        //    new CodeInstruction(OpCodes.Ldloc_S, scanner),
        //    CodeInstruction.LoadLocal(innerTypeIndex, true),
        //    CodeInstruction.LoadLocal(innerStructIndex, true),
        //    CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(Patch_JobGiver_Work_TryIssueJobPackage.ScanCellsAcrossMaps)));

        //var g_TargetInfo_Cell = AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.Cell));
        //codes.MatchStartForward(CodeMatch.Calls(g_TargetInfo_Cell));
        //codes.RemoveInstruction();

        //var m_JobOnCell = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.JobOnCell));
        //codes.MatchStartForward(CodeMatch.Calls(m_JobOnCell));
        //codes.SetInstruction(CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(Patch_JobGiver_Work_TryIssueJobPackage.JobOnCellMap)));


        //GenClosestの各メソッドを自作のものに置き換える
        //PotentialWorkThingsGlobalの各マップの結果を合計
        var m_GenClosest_ClosestThing_Global = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global));
        var m_GenClosestCrossMap_ClosestThing_Global = AccessTools.Method(typeof(GenClosestCrossMap), nameof(GenClosestCrossMap.ClosestThing_Global),
            [typeof(IntVec3), typeof(IEnumerable<>), typeof(float), typeof(Predicate<Thing>), typeof(Func<Thing, float>), typeof(bool)]);
        var m_GenClosest_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable));
        var m_GenClosestCrossMap_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosestCrossMap), nameof(GenClosestCrossMap.ClosestThing_Global_Reachable),
            [typeof(IntVec3), typeof(Map), typeof(IEnumerable<Thing>), typeof(PathEndMode), typeof(TraverseParms), typeof(float), typeof(Predicate<Thing>), typeof(Func<Thing, float>), typeof(bool)]);
        var m_Scanner_PotentialWorkThingsGlobal = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal));
        var m_PotentialWorkThingsGlobalAll = AccessTools.Method(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(Patch_JobGiver_Work_TryIssueJobPackage.PotentialWorkThingsGlobalAll));
        var m_Scanner_JobOnThing = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.JobOnThing));
        var m_JobOnThingMap = AccessTools.Method(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(Patch_JobGiver_Work_TryIssueJobPackage.JobOnThingMap));
        return codes.Instructions().MethodReplacer(m_GenClosest_ClosestThing_Global, m_GenClosestCrossMap_ClosestThing_Global)
            .MethodReplacer(m_GenClosest_ClosestThing_Global_Reachable, m_GenClosestCrossMap_ClosestThing_Global_Reachable)
            .MethodReplacer(m_Scanner_PotentialWorkThingsGlobal, m_PotentialWorkThingsGlobalAll)
            .MethodReplacer(m_Scanner_JobOnThing, m_JobOnThingMap);
    }
}