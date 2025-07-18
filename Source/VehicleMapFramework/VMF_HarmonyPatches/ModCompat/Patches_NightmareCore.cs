using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_NightmareCore
{
    static Patches_NightmareCore()
    {
        if (ModCompat.NightmareCore)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_NightmareCore");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_NightmareCore")]
[HarmonyPatch("NightmareCore.DiagonalAtlasGraphics.Graphic_LinkedStitched", "Print")]
public static class Patch_Graphic_LinkedStitched_Print
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_RotationForPrint).ToList();
        var pos = codes.FindLastIndex(c => c.Calls(AccessTools.Method(typeof(Vector3), "op_Addition")));
        codes.Insert(pos, CodeInstruction.Call(typeof(Patch_Graphic_LinkedStitched_Print), nameof(RotateVector)));

        return codes;
    }

    private static Vector3 RotateVector(Vector3 vector)
    {
        var rot = VehicleMapUtility.RotForPrint;
        if (rot.IsHorizontal) rot = rot.Opposite;
        return vector.RotatedBy(rot);
    }
}

[HarmonyPatchCategory("VMF_Patches_NightmareCore")]
[HarmonyPatch("NightmareCore.ThingComp_AdditionalGraphics", "PostPrintOnto")]
public static class Patch_ThingComp_AdditionalGraphics_PostPrintOnto
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 0f);

        codes.Replace(codes[pos], new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation));
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent))
        ]);
        return codes;
    }
}
