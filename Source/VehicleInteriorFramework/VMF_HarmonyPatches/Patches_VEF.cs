using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
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
            if (ModsConfig.IsActive("OskarPotocki.VFE.Pirates"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_VFE_Pirates");
            }
            if (ModsConfig.IsActive("OskarPotocki.VFE.Mechanoid"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_VFE_Mechanoid");
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

    [HarmonyPatchCategory("VMF_Patches_VFE_Pirates")]
    [HarmonyPatch("VFEPirates.Verb_ShootCone", "DrawLines")]
    public static class Patch_Verb_ShootCone_DrawLines
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotationAsRot4)
                .MethodReplacer(MethodInfoCache.g_Rot4_AsQuat, MethodInfoCache.m_Rot8_AsQuatRef);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Pirates")]
    [HarmonyPatch("VFEPirates.Verb_ShootCone", "DrawConeRounded")]
    public static class Patch_Verb_ShootCone_DrawConeRounded
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotationAsRot4);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Pirates")]
    [HarmonyPatch("VFEPirates.Verb_ShootCone", "CanHitTarget")]
    public static class Patch_Verb_ShootCone_CanHitTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotation_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Pirates")]
    [HarmonyPatch("VFEPirates.Verb_ShootCone", "InCone")]
    public static class Patch_Verb_ShootCone_InCone
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Rot4_AsAngle, MethodInfoCache.g_Rot8_AsAngle);
        }
    }

    //[HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    //[HarmonyPatch("VFEMech.Building_Autocrane", "SpawnSetup")]
    //public static class Patch_Building_Autocrane_SpawnSetup
    //{
    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //    {
    //        foreach (var instruction in instructions)
    //        {
    //            if (instruction.opcode == OpCodes.Call && instruction.OperandIs(MethodInfoCache.m_IntVec3_ToVector3Shifted))
    //            {
    //                yield return CodeInstruction.LoadArgument(0);
    //                yield return CodeInstruction.Call(typeof(Patch_Building_Autocrane_SpawnSetup), nameof(ToVector3Shifted));
    //            }
    //            else
    //            {
    //                yield return instruction;
    //            }
    //        }
    //    }

    //    private static Vector3 ToVector3Shifted(ref IntVec3 c, Building b)
    //    {
    //        var vector = c.ToVector3Shifted();
    //        if (b.IsOnNonFocusedVehicleMapOf(out var vehicle))
    //        {
    //            vector = vector.ToBaseMapCoord(vehicle);
    //        }
    //        return vector;
    //    }
    //}

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "GetStartingEndCranePosition")]
    public static class Patch_Building_Autocrane_GetStartingEndCranePosition
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_OccupiedRect, MethodInfoCache.m_MovedOccupiedRect);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "CurRotation")]
    public static class Patch_Building_Autocrane_CurRotation
    {
        [HarmonyPatch(MethodType.Setter)]
        public static void Prefix(Building __instance, ref float value)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                value = Ext_Math.RotateAngle(value, -vehicle.FullRotation.AsAngle);
            }
        }

        [HarmonyPatch(MethodType.Getter)]
        public static void Postfix(Building __instance, ref float __result)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                __result = Ext_Math.RotateAngle(__result, vehicle.FullRotation.AsAngle);
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "CraneDrawPos", MethodType.Getter)]
    public static class Patch_Building_Autocrane_CraneDrawPos
    {
        public static void Postfix(Building __instance, ref Vector3 __result)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                __result = Ext_Math.RotatePoint(__result, __instance.DrawPos, vehicle.Angle);
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "NextFrameTarget")]
    public static class Patch_Building_Autocrane_NextFrameTarget
    {
        public static void Postfix(Building __instance, IntVec3 ___endCranePosition, ref Frame __result)
        {
            if (__result == null)
            {
                var things = __instance.Map.BaseMapAndVehicleMaps().Except(__instance.Map).SelectMany(m =>
                {
                    var pos = m.IsVehicleMapOf(out var vehicle) ? __instance.PositionOnBaseMap().ToVehicleMapCoord(vehicle) : __instance.PositionOnBaseMap();
                    return GenRadial.RadialDistinctThingsAround(pos, m, 20f, true);
                });

                __result = (from x in things.OfType<Frame>()
                            where x.IsCompleted() && Patch_Building_Autocrane_NextFrameTarget.Validator(x, __instance)
                            orderby x.PositionOnBaseMap().DistanceTo(___endCranePosition)
                            select x).FirstOrDefault();
            }
        }

        public static bool Validator(Thing x, Building b)
        {
            return x.Faction == b.Faction && !x.IsBurning() && x.PositionOnBaseMap().DistanceTo(b.PositionOnBaseMap()) >= 6f && !x.Map.reservationManager.IsReservedByAnyoneOf(x, b.Faction);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "NextDamagedBuildingTarget")]
    public static class Patch_Building_Autocrane_NextDamagedBuildingTarget
    {
        public static void Postfix(Building __instance, IntVec3 ___endCranePosition, ref Building __result)
        {
            if (__result == null)
            {
                var things = __instance.Map.BaseMapAndVehicleMaps().Except(__instance.Map).SelectMany(m =>
                {
                    var pos = m.IsVehicleMapOf(out var vehicle) ? __instance.PositionOnBaseMap().ToVehicleMapCoord(vehicle) : __instance.PositionOnBaseMap();
                    return GenRadial.RadialDistinctThingsAround(pos, m, 20f, true);
                });

                __result = (from x in things.OfType<Building>()
                            where x.def.useHitPoints && x.MaxHitPoints > 0 && x.HitPoints < x.MaxHitPoints && Patch_Building_Autocrane_NextFrameTarget.Validator(x, __instance)
                            orderby x.PositionOnBaseMap().DistanceTo(___endCranePosition)
                            select x).FirstOrDefault();
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "DoConstruction")]
    public static class Patch_Building_Autocrane_DoConstruction
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var pos = instructions.FirstIndexOf(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Thing_Map)) - 1;
            instructions.ElementAt(pos).opcode = OpCodes.Ldarg_1;
            return instructions;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "DoRepairing")]
    public static class Patch_Building_Autocrane_DoRepairing
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var pos = instructions.FirstIndexOf(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Thing_Map)) - 1;
            instructions.ElementAt(pos).opcode = OpCodes.Ldarg_1;
            return instructions;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "TryMoveTo")]
    public static class Patch_Building_Autocrane_TryMoveTo
    {
        public static bool Prefix(Building __instance, Frame ___curFrameTarget, Building ___curBuildingTarget, LocalTargetInfo target, Vector3 offset, ref float ___curCraneSize,float ___distanceRate, ref IntVec3 ___endCranePosition, float ___craneErectionSpeed, ref bool __result)
        {
            if (__instance.IsOnVehicleMapOf(out _) && ___curFrameTarget == null && ___curBuildingTarget == null)
            {
                __result = true;
                float num3 = 5.5f;
                float num4 = num3 / ___distanceRate;
                bool flag4 = num4 > ___curCraneSize + ___craneErectionSpeed;
                if (flag4)
                {
                    ___curCraneSize += ___craneErectionSpeed;
                }
                else
                {
                    bool flag5 = num4 <= ___curCraneSize - ___craneErectionSpeed;
                    if (flag5)
                    {
                        ___curCraneSize -= ___craneErectionSpeed;
                    }
                    else
                    {
                        float num5 = Mathf.Abs(num4 - ___curCraneSize);
                        bool flag6 = num5 > 0f && num5 < ___craneErectionSpeed;
                        if (!flag6)
                        {
                            ___endCranePosition = target.Cell;
                            __result = false;
                        }
                        ___curCraneSize = num4;
                    }
                }
                return false;
            }
            return true;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_Distance = AccessTools.Method(typeof(Vector3), nameof(Vector3.Distance));
            var m_DistanceFlat = AccessTools.Method(typeof(Patch_Building_Autocrane_TryMoveTo), nameof(DistanceFlat));
            instructions = instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(m_Distance, m_DistanceFlat);
            instructions.First(c => c.opcode == OpCodes.Ldc_R4).operand = 0.1f;
            var instruction = instructions.First(c => c.opcode == OpCodes.Ceq);
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(Patch_Building_Autocrane_TryMoveTo), nameof(QuiteApproximately));
            return instructions;
        }

        public static float DistanceFlat(Vector3 a, Vector3 b)
        {
            float num = a.x - b.x;
            float num2 = a.z - b.z;
            return (float)Math.Sqrt((double)(num * num + num2 * num2));
        }

        public static bool QuiteApproximately(float a, float b)
        {
            return Mathf.Abs(b - a) < 0.1f;
        }
    }

    //[HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    //[HarmonyPatch("VFEMech.Building_Autocrane", "Tick")]
    //public static class Patch_Building_Autocrane_Tick
    //{
    //    public static void Prefix(Building __instance, Frame ___curFrameTarget, Building ___curBuildingTarget, ref IntVec3 ___curCellTarget)
    //    {
    //        if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle) && ___curCellTarget.IsValid && ___curFrameTarget == null && ___curBuildingTarget == null && (vehicle.vehiclePather?.Moving ?? false))
    //        {
    //            ___curCellTarget = (IntVec3)GetStartingEndCranePosition(__instance);
    //        }
    //    }

    //    private static FastInvokeHandler GetStartingEndCranePosition = MethodInvoker.GetHandler(AccessTools.Method("VFEMech.Building_Autocrane:GetStartingEndCranePosition"));
    //}

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFE.Mechanoids.PlaceWorkers.PlaceWorker_AutoCrane", "DrawGhost")]
    public static class Patch_PlaceWorker_AutoCrane_DrawGhost
    {
        public static bool Prefix(IntVec3 center, Thing thing)
        {
            if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) || (vehicle = Command_FocusVehicleMap.FocusedVehicle) != null)
            {
                GenDraw.DrawRadiusRing(center, 6f, Color.red);
                GenDraw.DrawRadiusRing(center, 20, Color.white);
                return false;
            }
            return true;
        }
    }
}
