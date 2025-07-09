using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static VehicleMapFramework.MethodInfoCache;
using static VehicleMapFramework.ModCompat.VFEMechanoid;

namespace VehicleMapFramework.VMF_HarmonyPatches
{
    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "GetStartingEndCranePosition")]
    public static class Patch_Building_Autocrane_GetStartingEndCranePosition
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.m_OccupiedRect, CachedMethodInfo.m_MovedOccupiedRect);
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
            var pos = instructions.FirstIndexOf(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Thing_Map)) - 1;
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
            var pos = instructions.FirstIndexOf(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Thing_Map)) - 1;
            instructions.ElementAt(pos).opcode = OpCodes.Ldarg_1;
            return instructions;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFEMech.Building_Autocrane", "TryMoveTo")]
    public static class Patch_Building_Autocrane_TryMoveTo
    {
        public static bool Prefix(Building __instance, Frame ___curFrameTarget, Building ___curBuildingTarget, LocalTargetInfo target, ref float ___curCraneSize, float ___distanceRate, ref IntVec3 ___endCranePosition, float ___craneErectionSpeed, ref bool __result)
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
            instructions = instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
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
            return (float)Math.Sqrt((double)((num * num) + (num2 * num2)));
        }

        public static bool QuiteApproximately(float a, float b)
        {
            return Mathf.Abs(b - a) < 0.1f;
        }
    }

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

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFE.Mechanoids.PlaceWorkers.PlaceWorker_AutoPlant", "GetCells")]
    public static class Patch_PlaceWorker_AutoPlant_GetCells
    {
        public static void Postfix(Thing thing, List<IntVec3> __result)
        {
            if ((thing.IsOnNonFocusedVehicleMapOf(out var vehicle) && vehicle.Spawned) || (thing == null && (vehicle = Command_FocusVehicleMap.FocusedVehicle) != null && vehicle.Spawned))
            {
                for (var i = 0; i < __result.Count; i++)
                {
                    __result[i] = __result[i].ToBaseMapCoord(vehicle);
                    if (!__result[i].InBounds(vehicle.Map))
                    {
                        __result.RemoveAt(i);
                    }
                }
                foreach (var c in CellRect.FromLimits(__result.Min(c => c.x), __result.Min(c => c.z), __result.Max(c => c.x), __result.Max(c => c.z)))
                {
                    if (c.AdjacentCellsCardinal(vehicle.Map).All(c2 => __result.Contains(c2)))
                    {
                        __result.Add(c);
                    }
                }
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFE.Mechanoids.Buildings.Building_AutoPlant", "DoWorkOnCells")]
    public static class Patch_Building_AutoPlant_DoWorkOnCells
    {
        public static bool Prefix(Building __instance, float ___offset)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                foreach (var c in Patch_Building_AutoPlant_DoWorkOnCells.GetCells(__instance, vehicle, ___offset))
                {
                    DoWorkOnCell(__instance, c);
                }
                return false;
            }
            return true;
        }

        public static IEnumerable<IntVec3> GetCells(Building building, VehiclePawnWithMap vehicle, float offset)
        {
            var cell1 = new IntVec3(-3, 0, Mathf.FloorToInt(offset)).RotatedBy(building.Rotation) + building.Position;
            var cell2 = new IntVec3(3, 0, Mathf.FloorToInt(offset)).RotatedBy(building.Rotation) + building.Position;
            return GenSight.PointsOnLineOfSight(cell1.ToBaseMapCoord(vehicle), cell2.ToBaseMapCoord(vehicle));
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFE.Mechanoids.Buildings.Building_AutoPlant", "CheckCellsClear")]
    public static class Patch_Building_AutoPlant_CheckCellsClear
    {
        public static bool Prefix(Building __instance, float ___offset, bool ___blockedByTree, ref bool __result)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                __result = Patch_Building_AutoPlant_DoWorkOnCells.GetCells(__instance, vehicle, ___offset).All(c =>
                {
                    return CheckCell(c, __instance, vehicle, ___blockedByTree);
                });
                return false;
            }
            return true;
        }

        private static bool CheckCell(IntVec3 cell, Building __instance, VehiclePawnWithMap vehicle, bool ___blockedByTree)
        {
            foreach (Thing thing in cell.GetThingList(vehicle.Map))
            {
                if (thing != __instance && thing != vehicle && thing.def.passability != Traversability.Standable && (thing.def.plant == null || ___blockedByTree))
                {
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFE.Mechanoids.Buildings.Building_AutoPlant", "DrawPos", MethodType.Getter)]
    public static class Patch_Building_AutoPlant_DrawPos
    {
        public static bool Prefix(Building __instance, ref Vector3 __result)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                __result = GenThing.TrueCenter(__instance.Position, __instance.Rotation, __instance.def.size, __instance.def.Altitude).ToBaseMapCoord(vehicle);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFE.Mechanoids.Buildings.Building_AutoPlant", "DrawAt")]
    public static class Patch_Building_AutoPlant_DrawAt
    {
        public static bool Prefix(Building __instance, Vector3 drawLoc, bool flip, Graphic ___baseGraphic, float ___offset)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out _))
            {
                if (__instance.def.drawerType == DrawerType.RealtimeOnly || !__instance.Spawned)
                {
                    __instance.Graphic.Draw(drawLoc + new Vector3(0f, 1f, ___offset).RotatedBy(__instance.BaseFullRotation().AsAngle), flip ? __instance.Rotation.Opposite : __instance.Rotation, __instance, 0f);
                }
                SilhouetteUtility.DrawGraphicSilhouette(__instance, drawLoc);
                if (__instance.AllComps != null)
                {
                    int count = __instance.AllComps.Count;
                    for (int i = 0; i < count; i++)
                    {
                        __instance.AllComps[i].PostDraw();
                    }
                }
                ___baseGraphic.Draw(drawLoc, __instance.Rotation, __instance, 0f);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFE.Mechanoids.Buildings.Building_AutoSower", "DoWorkOnCell")]
    public static class Patch_Building_AutoSower_DoWorkOnCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFE.Mechanoids.Buildings.Building_AutoHarvester", "DoWorkOnCell")]
    public static class Patch_Building_AutoHarvester_DoWorkOnCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Mechanoid")]
    [HarmonyPatch("VFE.Mechanoids.Buildings.Building_AutoHarvester", "PlantCollected")]
    public static class Patch_Building_AutoHarvester_PlantCollected
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
        }
    }
}
