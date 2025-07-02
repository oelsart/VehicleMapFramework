using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_MiscRobots
{
    static Patches_MiscRobots()
    {
        if (ModCompat.MiscRobots)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_MiscRobots");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_MiscRobots")]
[HarmonyPatch("X2_JobGiver_Return2BaseRoom", "TryIssueJobPackage")]
public static class Patch_X2_JobGiver_Return2BaseRoom_TryIssueJobPackage
{
    public static bool Prefix(ThinkNode __instance, Pawn pawn, ref ThinkResult __result)
    {
        if (!t_X2_AIRobot?.IsAssignableFrom(pawn.GetType()) ?? true) return true;

        var rechargeStation = Patch_X2_JobGiver_Return2BaseRoom_TryIssueJobPackage.rechargeStation(pawn);
        if (pawn.Map == rechargeStation?.Map) return true;

        if (pawn.DestroyedOrNull())
        {
            __result = ThinkResult.NoJob;
        }
        else
        {
            if (!pawn.Spawned)
            {
                __result = ThinkResult.NoJob;
            }
            else
            {
                if (rechargeStation.DestroyedOrNull())
                {
                    __result = ThinkResult.NoJob;
                }
                else
                {
                    if (!rechargeStation.Spawned)
                    {
                        __result = ThinkResult.NoJob;
                    }
                    else
                    {
                        Room roomRecharge = rechargeStation.Position.GetRoom(rechargeStation.Map);
                        Room roomRobot = pawn.Position.GetRoom(pawn.Map);
                        bool flag5 = roomRecharge == roomRobot;
                        if (flag5)
                        {
                            __result = ThinkResult.NoJob;
                        }
                        else
                        {
                            Map mapRecharge = rechargeStation.Map;
                            IntVec3 posRecharge = rechargeStation.Position;
                            TargetInfo exitSpot = TargetInfo.Invalid;
                            TargetInfo enterSpot = TargetInfo.Invalid;
                            IntVec3 cell = (from c in roomRecharge.Cells
                                            where c.Standable(mapRecharge) && !c.IsForbidden(pawn) && c.InHorDistOf(posRecharge, 5f) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Some, false, false, TraverseMode.ByPawn, rechargeStation.Map, out exitSpot, out enterSpot)
                                            select c).FirstOrDefault<IntVec3>();
                            if (cell == IntVec3.Invalid)
                            {
                                __result = ThinkResult.NoJob;
                            }
                            else
                            {
                                var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps, cell);
                                job.locomotionUrgency = LocomotionUrgency.Amble;
                                job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot);
                                __result = new ThinkResult(job, __instance, new JobTag?(JobTag.Misc), false);
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    private static Type t_X2_AIRobot = AccessTools.TypeByName("AIRobot.X2_AIRobot");

    private static AccessTools.FieldRef<Pawn, Building> rechargeStation = AccessTools.FieldRefAccess<Building>("AIRobot.X2_AIRobot:rechargeStation");
}
