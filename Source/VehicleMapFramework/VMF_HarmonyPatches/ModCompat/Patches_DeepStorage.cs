using HarmonyLib;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_DeepStorage
{
    static Patches_DeepStorage()
    {
        if (ModCompat.DeepStorage.Active)
        {
            var original = AccessTools.Method(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.IsGoodStoreCell));
            var patch = AccessTools.Method("LWM.DeepStorage.Patch_IsGoodStoreCell:Postfix");
            VMF_Harmony.Instance.Patch(original, postfix: patch);
            original = AccessTools.Method(typeof(StoreAcrossMapsUtility), "NoStorageBlockersIn");
            patch = AccessTools.Method("LWM.DeepStorage.Patch_NoStorageBlockersIn:Prefix");
            VMF_Harmony.Instance.Patch(original, prefix: patch);
        }
    }
}
