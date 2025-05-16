using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_PRF
    {
        static Patches_PRF()
        {
            if (ModCompat.ProjectRimFactory)
            {
                //VMF_Harmony.Instance.PatchCategory("VMF_Patches_PRF");

                VMF_Harmony.Instance.Patch(AccessTools.Method("ProjectRimFactory.Common.HarmonyPatches.Patch_CanReserve_SAL:Postfix"), transpiler: AccessTools.Method(typeof(Patch_Patch_CanReserve_SAL_Postfix), nameof(Patch_Patch_CanReserve_SAL_Postfix.Transpiler)));
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_PRF")]
    [HarmonyPatch("ProjectRimFactory.Common.HarmonyPatches.Patch_CanReserve_SAL", "Postfix")]
    public static class Patch_Patch_CanReserve_SAL_Postfix
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map)) - 1;
            codes[pos].opcode = OpCodes.Ldloc_2;
            return codes;
        }
    }
}
