using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_ExosuitFramework
    {
        static Patches_ExosuitFramework()
        {
            if (ModCompat.ExosuitFramework)
            {
                VMF_Harmony.PatchCategory("VMF_Patches_ExosuitFramework");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_ExosuitFramework")]
    [HarmonyPatch("WalkerGear.CompBuildingExtraRenderer", "PostPrintOnto")]
    public static class Patch_CompBuildingExtraRenderer_PostPrintOnto
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 0f);

            codes.Replace(codes[pos], new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PrintExtraRotation));
            codes.Insert(pos, new CodeInstruction(OpCodes.Dup));
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_ExosuitFramework")]
    [HarmonyPatch("WalkerGear.WG_AbilityVerb_QuickJump", "DoJump")]
    [HarmonyPatch(new Type[] { typeof(Pawn), typeof(Map), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool) })]
    public static class Patch_WG_AbilityVerb_QuickJump_DoJump
    {
        public static void Prefix(Pawn pawn, Map targetMap, ref LocalTargetInfo currentTarget)
        {
            if (pawn.IsOnNonFocusedVehicleMapOf(out _))
            {
                var positionOnBaseMap = pawn.PositionOnBaseMap();
                currentTarget = new IntVec3(positionOnBaseMap.x, positionOnBaseMap.y, Math.Min(positionOnBaseMap.z + 25, CellRect.WholeMap(targetMap).maxZ));
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_ExosuitFramework")]
    [HarmonyPatch("WalkerGear.WG_PawnFlyer", "RespawnPawn")]
    public static class Patch_WG_PawnFlyer_RespawnPawn
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_ExosuitFramework")]
    [HarmonyPatch("WalkerGear.WG_PawnFlyer", "SpawnSetup")]
    public static class Patch_WG_PawnFlyer_SpawnSetup
    {
        public static void Postfix(ref Thing ___eBay, Map map)
        {
            if (___eBay == null)
            {
                var maps = map.BaseMapAndVehicleMaps().Except(map);
                ___eBay = maps.SelectMany(m => m.listerBuildings.allBuildingsColonist).FirstOrDefault(b => b.GetType() == t_Building_EjectorBay);
            }
        }

        private static Type t_Building_EjectorBay = AccessTools.TypeByName("WalkerGear.Building_EjectorBay");
    }

    [HarmonyPatchCategory("VMF_Patches_ExosuitFramework")]
    [HarmonyPatch("WalkerGear.Building_EjectorBay", "DynamicDrawPhaseAt")]
    public static class Patch_Building_EjectorBay_DynamicDrawPhaseAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseRotation);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_ExosuitFramework")]
    [HarmonyPatch("WalkerGear.Building_MaintenanceBay", "DynamicDrawPhaseAt")]
    public static class Patch_Building_MaintenanceBay_DynamicDrawPhaseAt
    {
        public static void Prefix(Building __instance, Pawn ___cachePawn)
        {
            if (___cachePawn != null)
            {
                ___cachePawn.Rotation = __instance.BaseRotation().Opposite;
            }
        }
    }
}
