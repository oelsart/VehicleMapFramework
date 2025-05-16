using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public class Patches_EnterHere
    {
        static Patches_EnterHere()
        {
            if (ModCompat.EnterHere)
            {
                //VMF_Harmony.Instance.PatchCategory("VMF_Patches_EnterHere");

                VMF_Harmony.Instance.Patch(AccessTools.FindIncludingInnerTypes<MethodBase>(AccessTools.TypeByName("EnterHere.VehicleCaravanFormingUtility_StartFormingCaravan"),
                t => t.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("<Prefix>b__0"))), prefix: AccessTools.Method(typeof(Patch_VehicleCaravanFormingUtility_StartFormingCaravan_Prefix_Func), nameof(Patch_VehicleCaravanFormingUtility_StartFormingCaravan_Prefix_Func.Prefix)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("EnterHere.VehicleCaravanFormingUtility_StartFormingCaravan:Prefix"), transpiler: AccessTools.Method(typeof(Patch_VehicleCaravanFormingUtility_StartFormingCaravan_Prefix), nameof(Patch_VehicleCaravanFormingUtility_StartFormingCaravan_Prefix.Transpiler)));
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_EnterHere")]
    [HarmonyPatch]
    public static class Patch_VehicleCaravanFormingUtility_StartFormingCaravan_Prefix_Func
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.FindIncludingInnerTypes<MethodBase>(AccessTools.TypeByName("EnterHere.VehicleCaravanFormingUtility_StartFormingCaravan"),
                t => t.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("<Prefix>b__0")));
        }

        public static bool Prefix(Pawn pawnObject, Type ___vehiclePawnType, ref bool __result)
        {
            __result = ___vehiclePawnType.IsAssignableFrom(pawnObject.GetType());
            return false;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_EnterHere")]
    [HarmonyPatch("EnterHere.VehicleCaravanFormingUtility_StartFormingCaravan", "Prefix")]
    public static class Patch_VehicleCaravanFormingUtility_StartFormingCaravan_Prefix
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var m_ChangeType = AccessTools.Method(typeof(Convert), nameof(Convert.ChangeType), new[] { typeof(object), typeof(Type) });
            var pos = codes.FindIndex(c => c.Calls(m_ChangeType)) - 2;
            codes.RemoveRange(pos, 3);
            return codes;
        }
    }
}
