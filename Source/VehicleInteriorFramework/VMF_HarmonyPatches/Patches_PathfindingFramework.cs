using HarmonyLib;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_PathfindingFramework
    {
        static Patches_PathfindingFramework()
        {
            if (ModsConfig.IsActive("pathfinding.framework"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_PathfindingFramework");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_PathfindingFramework")]
    [HarmonyPatch("PathfindingFramework.LocationFinding", "IsPassableRegion")]
    public static class Patch_LocationFinding_IsPassableRegion
    {
        public static void Prefix(Region region, ref Map map)
        {
            var regionMap = region.Map;
            if (regionMap != map)
            {
                map = regionMap;
            }
        }
    }

    //PathfindingFrameworkに書き換えられたparms.pawn.MovementContext().PathingContextのマップが別マップだった場合、結局元メソッドのPathingContextに差し替える
    [HarmonyPatchCategory("VMF_Patches_PathfindingFramework")]
    [HarmonyPatch(typeof(Pathing), nameof(Pathing.For), typeof(TraverseParms))]
    public static class Patch_Pathing_For_TraverseParms
    {
        public static void Postfix(Pathing __instance, TraverseParms parms,  ref PathingContext __result)
        {
            if (__result.map != __instance.Normal.map)
            {
                if (parms.fenceBlocked && !parms.canBashFences)
                {
                    __result = __instance.FenceBlocked;
                }
                else
                {
                    __result = __instance.Normal;
                }

            }
        }
    }

    //PathfindingFrameworkに書き換えられたparms.pawn.MovementContext().PathingContextのマップが別マップだった場合、結局元メソッドのPathingContextに差し替える
    [HarmonyPatchCategory("VMF_Patches_PathfindingFramework")]
    [HarmonyPatch(typeof(Pathing), nameof(Pathing.For), typeof(Pawn))]
    public static class Patch_Pathing_For_Pawn
    {
        public static void Postfix(Pathing __instance, Pawn pawn, ref PathingContext __result)
        {
            if (__result.map != __instance.Normal.map)
            {
                if (pawn != null && pawn.ShouldAvoidFences && (pawn.CurJob == null || !pawn.CurJob.canBashFences))
                {
                    __result = __instance.FenceBlocked;
                }
                __result = __instance.Normal;
            }
        }
    }
}
