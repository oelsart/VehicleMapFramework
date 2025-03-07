using HarmonyLib;
using PickUpAndHaul;
using RimWorld;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public class Patches_PUAH
    {
        static Patches_PUAH()
        {
            //var original = AccessTools.Method(typeof(JobDriver_HaulToCellAcrossMaps), "MakeNewToils");
            //var patch = AccessTools.Method("PickUpAndHaul.HarmonyPatches:JobDriver_HaulToCell_PostFix");
            //VMF_Harmony.Instance.Patch(original, postfix: patch);

            VMF_Harmony.Instance.PatchCategory("VMF_Patches_PUAH");
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
