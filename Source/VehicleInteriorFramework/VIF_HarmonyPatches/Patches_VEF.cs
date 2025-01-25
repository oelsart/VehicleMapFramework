using HarmonyLib;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class Patches_VEF
    {
        static Patches_VEF()
        {
            if (ModsConfig.IsActive("VanillaExpanded.VFEArchitect"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_VFE_Architect");
            }
            if (ModsConfig.IsActive("VanillaExpanded.VFESecurity"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_VFE_Security");
            }
            if (ModsConfig.IsActive("OskarPotocki.VanillaVehiclesExpanded"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_VVE");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Architect")]
    [HarmonyPatch("VFEArchitect.Building_DoorSingle", "DrawAt")]
    public static class Patch_Building_DoorSingle_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_Building_MultiTileDoor_DrawAt.Transpiler(Patch_Building_Door_DrawMovers.Transpiler(instructions));
        }
    }

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
            return instructions.MethodReplacer(MethodInfoCache.m_GetThingList, MethodInfoCache.m_GetThingListAcrossMaps);
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
            return instructions.MethodReplacer(MethodInfoCache.m_GetThingList, MethodInfoCache.m_GetThingListAcrossMaps);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
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
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_ToBaseMapCoord2),
            });
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
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

    [HarmonyPatchCategory("VMF_Patches_VFE_Security")]
    [HarmonyPatch("VFESecurity.Building_Shield", "UpdateCache")]
    public static class Patch_Building_Shield_UpdateCache
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VVE")]
    [HarmonyPatch("VanillaVehiclesExpanded.GarageDoor", "DrawAt")]
    public static class Patch_GarageDoor_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            //Graphics.DrawMesh(MeshPool.GridPlane(size), drawPos, base.Rotation.AsQuat, this.def.graphicData.GraphicColoredFor(this).MatAt(base.Rotation, this), 0);
            //this.Graphic.ShadowGraphic?.DrawWorker(drawPos, base.Rotation, this.def, this, 0f);
            //↓
            //Graphics.DrawMesh(MeshPool.GridPlane(size), RotateOffset(drawPos, this), this.BaseFullRotation().AsQuat(), this.def.graphicData.GraphicColoredFor(this).MatAt(this.BaseRotation(), this), 0);
            //this.Graphic.ShadowGraphic?.DrawWorker(drawPos, this.BaseFullRotation(), this.def, this, 0f);
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Rot4_AsQuat));
            codes[pos].operand = MethodInfoCache.m_Rot8_AsQuatRef;
            pos = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Thing_Rotation));
            codes[pos].operand = MethodInfoCache.m_BaseFullRotation_Thing;
            pos = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Ldarg_0);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.Call(typeof(Patch_GarageDoor_DrawAt), nameof(Patch_GarageDoor_DrawAt.RotateOffset))
            });
            pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Thing_Rotation));
            codes[pos].operand = MethodInfoCache.m_BaseRotation;
            pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Thing_Rotation));
            codes[pos].operand = MethodInfoCache.m_BaseFullRotation_Thing;
            return codes;
        }

        private static Vector3 RotateOffset(Vector3 point, Building garageDoor)
        {
            if (garageDoor.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                return Ext_Math.RotatePoint(point, garageDoor.DrawPos, -vehicle.FullRotation.AsAngle);
            }
            return point;
        }
    }
}
