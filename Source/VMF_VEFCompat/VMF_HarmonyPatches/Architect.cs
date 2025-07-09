using HarmonyLib;
using System.Collections.Generic;

namespace VehicleMapFramework.VMF_HarmonyPatches
{
    [HarmonyPatchCategory("VMF_Patches_VFE_Architect")]
    [HarmonyPatch("VFEArchitect.Building_DoorSingle", "DrawAt")]
    public static class Patch_Building_DoorSingle_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_Building_MultiTileDoor_DrawAt.Transpiler(Patch_Building_Door_DrawMovers.Transpiler(instructions));
        }
    }
}