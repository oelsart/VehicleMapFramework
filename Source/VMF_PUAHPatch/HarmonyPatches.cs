using HarmonyLib;
using PickUpAndHaul;
using RimWorld;
using Verse;
using VMF_PUAHPatch;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.VeryLow)]
    public class Patches_PUAH
    {
        static Patches_PUAH()
        {
            //var original = AccessTools.Method(typeof(JobDriver_HaulToCellAcrossMaps), "MakeNewToils");
            //var patch = AccessTools.Method("PickUpAndHaul.HarmonyPatches:JobDriver_HaulToCell_PostFix");
            //VMF_Harmony.Instance.Patch(original, postfix: patch);

            VMF_Harmony.PatchCategory("VMF_Patches_PUAH");

            var workGiver_HaulToInventoryAcrossMaps = new WorkGiver_HaulToInventoryAcrossMaps();
            Patches_AllowTool.JobOnThingDelegate = (Pawn pawn, Thing t, bool forced) =>
            {
                if (workGiver_HaulToInventoryAcrossMaps.ShouldSkip(pawn, forced))
                {
                    return null;
                }
                return workGiver_HaulToInventoryAcrossMaps.JobOnThing(pawn, t, forced);
            };
        }
    }

    [HarmonyPatchCategory("VMF_Patches_PUAH")]
    [HarmonyPatch(typeof(JobAcrossMapsUtility), nameof(JobAcrossMapsUtility.NoNeedVirtualMapTransfer))]
    public static class Patch_JobAcrossMapsUtility_NoNeedVirtualMapTransfer
    {
        public static void Postfix(WorkGiver_Scanner scanner, ref bool __result)
        {
            __result = __result || scanner is WorkGiver_HaulToInventory;
        }
    }
}
