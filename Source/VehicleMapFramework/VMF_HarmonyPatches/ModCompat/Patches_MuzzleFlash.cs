using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_MuzzleFlash
{
    static Patches_MuzzleFlash()
    {
        if (ModCompat.MuzzleFlash)
        {
            VMF_Harmony.PatchCategory(PatchCategories.MuzzleFlash);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.MuzzleFlash)]
[HarmonyPatch("MuzzleFlash.MapComponent_MuzzleFlashManager", "MapComponentUpdate")]
[PatchLevel(Level.Sensitive)]
public static class Patch_MapComponent_MuzzleFlashManager_MapComponentUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var f_map = AccessTools.Field(typeof(MapComponent), nameof(MapComponent.map));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldfld && instruction.OperandIs(f_map))
            {
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Map);
            }
        }
    }
}
