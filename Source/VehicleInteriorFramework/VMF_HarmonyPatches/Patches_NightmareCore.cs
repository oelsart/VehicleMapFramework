using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public class Patches_NightmareCore
    {
        static Patches_NightmareCore()
        {
            if (ModCompat.NightmareCore)
            {
                //VMF_Harmony.Instance.PatchCategory("VMF_Patches_NightmareCore");

                VMF_Harmony.Instance.Patch(AccessTools.Method("NightmareCore.DiagonalAtlasGraphics.Graphic_LinkedStitched:Print"), transpiler: AccessTools.Method(typeof(Patch_Graphic_LinkedStitched_Print), nameof(Patch_Graphic_LinkedStitched_Print.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("NightmareCore.ThingComp_AdditionalGraphics:PostPrintOnto"), transpiler: AccessTools.Method(typeof(Patch_ThingComp_AdditionalGraphics_PostPrintOnto), nameof(Patch_ThingComp_AdditionalGraphics_PostPrintOnto.Transpiler)));
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_NightmareCore")]
    [HarmonyPatch("NightmareCore.DiagonalAtlasGraphics.Graphic_LinkedStitched", "Print")]
    public static class Patch_Graphic_LinkedStitched_Print
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_RotationForPrint).ToList();
            var pos = codes.FindLastIndex(c => c.Calls(AccessTools.Method(typeof(Vector3), "op_Addition")));
            codes.Insert(pos, CodeInstruction.Call(typeof(Patch_Graphic_LinkedStitched_Print), nameof(RotateVector)));

            return codes;
        }

        private static Vector3 RotateVector(Vector3 vector)
        {
            var rot = VehicleMapUtility.rotForPrint;
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

            codes.Replace(codes[pos], new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PrintExtraRotation));
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent))
            });
            return codes;
        }
    }
}
