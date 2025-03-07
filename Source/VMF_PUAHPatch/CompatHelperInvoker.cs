using HarmonyLib;

namespace VMF_PUAHPatch
{
    public static class CompatHelperInvoker
    {
        public static readonly FastInvokeHandler CeOverweight = MethodInvoker.GetHandler(AccessTools.Method("PickUpAndHaul.CompatHelper:CeOverweight"));

        public static readonly FastInvokeHandler CanFitInInventory = MethodInvoker.GetHandler(AccessTools.Method("PickUpAndHaul.CompatHelper:CanFitInInventory"));

        public static readonly FastInvokeHandler UpdateInventory = MethodInvoker.GetHandler(AccessTools.Method("PickUpAndHaul.CompatHelper:UpdateInventory"));
    }
}
