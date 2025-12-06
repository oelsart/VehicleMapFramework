using HarmonyLib;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_DeepStorage
{
    static Patches_DeepStorage()
    {
        if (DeepStorage)
        {
            var original = AccessTools.Method(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.IsGoodStoreCell));
            var patch = AccessTools.Method("LWM.DeepStorage.Patch_IsGoodStoreCell:Postfix");
            VMF_Harmony.Instance.Patch(original, postfix: patch);
        }
    }
}
