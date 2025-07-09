using HarmonyLib;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using static VehicleMapFramework.MethodInfoCache;
using static VehicleMapFramework.ModCompat.VFESecurity;

namespace VehicleMapFramework.VMF_HarmonyPatches
{
    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
    [HarmonyPatch]
    public static class Patch_Building_Shield_ThingsWithinRadius
    {
        private static MethodInfo TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("VFESecurity.Building_Shield").FirstInner(t => t.Name.Contains("ThingsWithinRadius")), "MoveNext");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
    [HarmonyPatch]
    public static class Patch_Building_Shield_ThingsWithinScanArea
    {
        private static MethodInfo TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("VFESecurity.Building_Shield").FirstInner(t => t.Name.Contains("ThingsWithinScanArea")), "MoveNext");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
    [HarmonyPatch("VFESecurity.Building_Shield", "AbsorbDamage")]
    [HarmonyPatch([typeof(float), typeof(DamageDef), typeof(float)])]
    public static class Patch_Building_Shield_AbsorbDamage
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var last = instructions.Last(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Thing_Map));
            return instructions.Manipulator(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Thing_Map) && c != last, c =>
            {
                c.operand = CachedMethodInfo.m_BaseMap_Thing;
            }).MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
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
            codes.InsertRange(pos,
            [
                CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2),
        ]);
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
    [HarmonyPatch("VFESecurity.Building_Shield", "EnergyShieldTick")]
    public static class Patch_Building_Shield_EnergyShieldTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.Select((c, i) =>
            {
                if (c.OperandIs(CachedMethodInfo.g_Thing_Position) && instructions.ElementAt(i - 1).opcode == OpCodes.Ldarg_0)
                {
                    c.opcode = OpCodes.Call;
                    c.operand = CachedMethodInfo.m_PositionOnBaseMap;
                }
                return c;
            });
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
    [HarmonyPatch("VFESecurity.Building_Shield", "UpdateCache")]
    public static class Patch_Building_Shield_UpdateCache
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
                .MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
    [HarmonyPatch("VFESecurity.CompLongRangeArtillery", "CompTick")]
    public static class Patch_CompLongRangeArtillery_CompTick
    {
        public static void Postfix(ThingComp __instance)
        {
            GlobalTargetInfo target;
            if (__instance.parent.IsOnVehicleMapOf(out var vehicle) && __instance.parent.IsHashIntervalTick(60) && (target = targetedTile(__instance)).IsValid)
            {
                if (Find.WorldGrid.TraversalDistanceBetween(vehicle.Tile, target.Tile, true, 2147483647) < worldTileRange(__instance.props)) return;

                targetedTile(__instance) = GlobalTargetInfo.Invalid;
            }
        }
    }
}
