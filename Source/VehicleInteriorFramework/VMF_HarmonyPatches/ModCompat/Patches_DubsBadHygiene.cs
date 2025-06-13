using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public class Patches_DubsBadHygiene
    {
        static Patches_DubsBadHygiene()
        {
            if (ModCompat.DubsBadHygiene.Active)
            {
                VMF_Harmony.PatchCategory("VMF_Patches_DubsBadGygiene");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DubsBadGygiene")]
    [HarmonyPatch("DubsBadHygiene.SectionLayer_PipeOverlay", "DrawAllTileOverlays")]
    public static class Patch_SectionLayer_PipeOverlay_DrawAllTileOverlays
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Beq);
            var label = codes[pos].operand;
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            codes.InsertRange(pos + 1, new[]
            {
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brtrue_S, label)
            });
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DubsBadGygiene")]
    [HarmonyPatch("DubsBadHygiene.Graphic_LinkedPipe", "ShouldLinkWith")]
    public static class Patch_Graphic_LinkedPipeDBH_ShouldLinkWith
    {
        public static void Prefix(IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
    }

    [HarmonyPatchCategory("VMF_Patches_DubsBadGygiene")]
    [HarmonyPatch("DubsBadHygiene.Building_AssignableFixture", "Print")]
    public static class Patch_Building_AssignableFixture_Print
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 0f);

            codes.Replace(codes[pos], new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation));
            codes.Insert(pos, CodeInstruction.LoadArgument(0));
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DubsBadGygiene")]
    [HarmonyPatch("DubsBadHygiene.Building_StallDoor", "DrawAt")]
    public static class Patch_Building_StallDoor_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = Patch_Building_Door_DrawMovers.Transpiler(instructions).ToList();
            var f_Vector3_y = AccessTools.Field(typeof(Vector3), nameof(Vector3.y));
            var label = generator.DefineLabel();
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stfld && c.OperandIs(f_Vector3_y));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldc_R4, VehicleMapUtility.altitudeOffsetFull),
                new CodeInstruction(OpCodes.Add)
            });
            return codes;
        }
    }
}
