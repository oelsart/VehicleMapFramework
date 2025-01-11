using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class Patches_VEF
    {
        static Patches_VEF()
        {
            if (ModsConfig.IsActive("VanillaExpanded.VFESecurity"))
            {
                VIF_Harmony.Instance.PatchCategory("VIF_Patches_VEF_Security");
            }
        }
    }

    [HarmonyPatchCategory("VIF_Patches_VEF_Security")]
    [HarmonyPatch]
    public static class Patch_Building_Shield_ThingsWithinRadius
    {
        private static MethodInfo TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("VFESecurity.Building_Shield").FirstInner(t => t.Name.Contains("ThingsWithinRadius")), "MoveNext");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_GetThingList, MethodInfoCache.m_GetThingListAcrossMaps);
        }
    }

    [HarmonyPatchCategory("VIF_Patches_VEF_Security")]
    [HarmonyPatch]
    public static class Patch_Building_Shield_ThingsWithinScanArea
    {
        private static MethodInfo TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("VFESecurity.Building_Shield").FirstInner(t => t.Name.Contains("ThingsWithinScanArea")), "MoveNext");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_GetThingList, MethodInfoCache.m_GetThingListAcrossMaps);
        }
    }

    [HarmonyPatchCategory("VIF_Patches_VEF_Security")]
    [HarmonyPatch("VFESecurity.Building_Shield", "AbsorbDamage")]
    [HarmonyPatch(new Type[] { typeof(float), typeof(DamageDef), typeof(float) })]
    public static class Patch_Building_Shield_AbsorbDamage
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var last = instructions.Last(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Thing_Map));
            return instructions.Manipulator(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Thing_Map) && c != last, c =>
            {
                c.operand = MethodInfoCache.m_BaseMap_Thing;
            }).MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VIF_Patches_VEF_Security")]
    [HarmonyPatch("VFESecurity.Building_Shield", "DrawAt")]
    public static class Patch_Building_Shield_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_1);
            var label = generator.DefineLabel();
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap2),
            });
            return codes;
        }
    }

    [HarmonyPatchCategory("VIF_Patches_VEF_Security")]
    [HarmonyPatch("VFESecurity.Building_Shield", "EnergyShieldTick")]
    public static class Patch_Building_Shield_EnergyShieldTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return instructions.Select((c, i) =>
            {
                if (c.OperandIs(MethodInfoCache.g_Thing_Position) && instructions.ElementAt(i - 1).opcode == OpCodes.Ldarg_0)
                {
                    c.opcode = OpCodes.Call;
                    c.operand = MethodInfoCache.m_PositionOnBaseMap;
                }
                return c;
            });
        }
    }

    [HarmonyPatchCategory("VIF_Patches_VEF_Security")]
    [HarmonyPatch("VFESecurity.Building_Shield", "UpdateCache")]
    public static class Patch_Building_Shield_UpdateCache
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap);
        }
    }
}
