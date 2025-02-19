using HarmonyLib;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_DeepStorage
    {
        static Patches_DeepStorage()
        {
            if (ModsConfig.IsActive("LWM.DeepStorage"))
            {
                var original = AccessTools.Method(typeof(HaulAIAcrossMapsUtility), nameof(HaulAIAcrossMapsUtility.HaulToCellStorageJob));
                var patch = AccessTools.Method("LWM.DeepStorage.Patch_HaulAIUtility_HaulToCellStorageJob:Transpiler");
                VMF_Harmony.Instance.Patch(original, transpiler: patch);
                original = AccessTools.Method(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.IsGoodStoreCell));
                patch = AccessTools.Method("LWM.DeepStorage.Patch_IsGoodStoreCell:Postfix");
                VMF_Harmony.Instance.Patch(original, postfix: patch);
                original = AccessTools.Method(typeof(StoreAcrossMapsUtility), "NoStorageBlockersIn");
                patch = AccessTools.Method("LWM.DeepStorage.Patch_NoStorageBlockersIn:Prefix");
                VMF_Harmony.Instance.Patch(original, prefix: patch);
            }
        }
    }
}
