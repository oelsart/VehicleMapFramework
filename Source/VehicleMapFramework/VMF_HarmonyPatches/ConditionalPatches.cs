using HarmonyLib;
using SmashTools;
using System.Linq;
using System.Reflection;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class ConditionalPatches
{
    static ConditionalPatches()
    {
        // This class is just a placeholder for conditional patches.
        var fullName = "Vehicles.WorkGiver_RefuelVehicleTurret:JobOnThing";
        var method = AccessTools.Method(fullName);
        if (method != null)
        {
            VMF_Harmony.Instance.Patch(method, AccessTools.Method(typeof(Patch_WorkGiver_RefuelVehicleTurret_JobOnThing), nameof(Patch_WorkGiver_RefuelVehicleTurret_JobOnThing.Prefix)));
        }
        else
        {
            DebugError(fullName);
        }
    }

    internal static void DebugError(string methodName)
    {
        VMF_Log.DebugError($"The method {methodName} targeted for patching was not found. This should mean the removal of the stubs targeted for patching.");
    }
}

//WorkGiver_RefuelVehicleTurretでVehicleが海上に居た場合Regionがnullでエラーを吐いていた問題の修正
//[HarmonyPatch(typeof(WorkGiver_RefuelVehicleTurret), nameof(WorkGiver_RefuelVehicleTurret.JobOnThing))]
//[PatchLevel(Level.Safe)]
//TODO: VFサイドで修正済み。アップデートに合わせて削除予定。
public static class Patch_WorkGiver_RefuelVehicleTurret_JobOnThing
{
    public static bool Prefix(Thing thing)
    {
        return thing.Position.GetRegion(thing.Map) != null;
    }
}

//車両マップ上からLoadVehicleをしようとした時など
//TODO: VFサイドのリファクタリングによりメソッド名変更。VFアップデートからスタブが削除されるまでに通常パッチに変更予定。
[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_JobDriver_LoadVehicle_FailJob
{
    private static MethodBase TargetMethod()
    {
        var method = AccessTools.Method(typeof(JobDriver_LoadVehicle), "FailJob");
        if (method is null)
        {
            ConditionalPatches.DebugError("Vehicles.JobDriver_LoadVehicle:FailJob");
            return AccessTools.Method(typeof(JobDriver_LoadVehicle), "ShouldFailJob");
        }
        return method;
    }

    public static void Postfix(JobDriver_LoadVehicle __instance, ref bool __result)
    {
        if (__result)
        {
            var map = __instance.pawn.MapHeld;
            var maps = map.BaseMapAndVehicleMaps().Except(map);
            var vehicle = __instance.job.GetTarget(TargetIndex.B).Thing as VehiclePawn;
            if (maps.Any(m => MapComponentCache<VehicleReservationManager>.GetComponent(m).VehicleListed(vehicle, ReservationType.LoadVehicle)))
            {
                __result = false;
            }
        }
    }
}