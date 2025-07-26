using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace VehicleMapFramework.VMF_HarmonyPatches
{
    [HarmonyPatchCategory(Patches_VEF.CategoryArchitect)]
    [HarmonyPatch("VFEArchitect.Building_DoorSingle", "DrawAt")]
    [PatchLevel(Level.Sensitive)]
    public static class Patch_Building_DoorSingle_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return Patch_Building_SupportedDoor_DrawAt.Transpiler(Patch_Building_Door_DrawMovers.Transpiler(instructions, generator), generator);
        }
    }
}