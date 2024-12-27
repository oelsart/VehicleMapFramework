using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(UI), nameof(UI.MouseCell))]
    public static class Patch_UI_MouseCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var toIntVec3 = AccessTools.Method(typeof(IntVec3Utility), nameof(IntVec3Utility.ToIntVec3));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(toIntVec3));
            codes.Insert(pos, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_VehicleMapToOrig1));
            return codes;
        }

        public static IntVec3 MouseCell()
        {
            return UI.UIToMapPosition(UI.MousePositionOnUI).ToIntVec3();
        }
    }

    [HarmonyBefore("SmashPhil.VehicleFramework")]
    [HarmonyPatch(typeof(GenThing), nameof(GenThing.TrueCenter), typeof(Thing))]
    public static class Patch_GenThing_TrueCenter
    {
        public static bool Prefix(Thing t, ref Vector3 __result)
        {
            return !t.TryGetOnVehicleDrawPos(ref __result);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_Thing_RotationOrig);
        }
    }

    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    public static class Patch_Pawn_DrawTracker_DrawPos
    {
        public static bool Prefix(Pawn ___pawn, ref Vector3 __result)
        {
            return !___pawn.TryGetOnVehicleDrawPos(ref __result);
        }

        public static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            __result.y += ___pawn.jobs?.curDriver is JobDriverAcrossMaps driver ? driver.ForcedBodyOffset.y : 0f;
        }
    }

    [HarmonyPatch(typeof(Vehicle_DrawTracker), nameof(Vehicle_DrawTracker.DrawPos), MethodType.Getter)]
    public static class Patch_VehiclePawn_DrawPos
    {
        public static bool Prefix(VehiclePawn ___vehicle, ref Vector3 __result, out bool __state)
        {
            __state = !___vehicle.TryGetOnVehicleDrawPos(ref __result);
            return __state;
        }

        public static void Postfix(VehiclePawn ___vehicle, ref Vector3 __result, bool __state)
        {
            if (__state)
            {
                __result += ___vehicle.jobs?.curDriver is JobDriverAcrossMaps driver ? driver.ForcedBodyOffset : Vector3.zero;
            }
        }
    }

    [HarmonyPatch(typeof(Mote), nameof(Mote.DrawPos), MethodType.Getter)]
    public static class Patch_Mote_DrawPos
    {
        public static bool Prefix(Mote __instance, ref Vector3 __result)
        {
            return !__instance.TryGetOnVehicleDrawPos(ref __result);
        }
    }

    //[HarmonyPatch(typeof(FleckStatic), nameof(FleckStatic.DrawPos), MethodType.Getter)]
    //public static class Patch_FleckStatic_DrawPos
    //{
    //    public static void Postfix(Map ___map, ref Vector3 __result)
    //    {
    //        if (___map?.Parent is MapParent_Vehicle parentVehicle)
    //        {
    //            __result = __result.OrigToVehicleMap(parentVehicle.vehicle);
    //        }
    //    }
    //}

    ////描画位置をOrigToVehicleMapで調整
    //[HarmonyPatch(typeof(GhostDrawer), nameof(GhostDrawer.DrawGhostThing))]
    //public static class Patch_GhostDrawer_DrawGhostThing
    //{
    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    //    {
    //        var codes = instructions.ToList();
    //        var getTrueCenter = AccessTools.Method(typeof(GenThing), nameof(GenThing.TrueCenter), new Type[] { typeof(IntVec3), typeof(Rot4), typeof(IntVec2), typeof(float) });
    //        var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(getTrueCenter)) + 1;
    //        codes.Insert(pos, CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.OrigToVehicleMap), new Type[] { typeof(Vector3) }));

    //    //    var label = generator.DefineLabel();
    //    //    var drawFromDef = AccessTools.Method(typeof(Graphic), nameof(Graphic.DrawFromDef));
    //    //    var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(drawFromDef));
    //    //    codes[pos2].labels.Add(label);
    //    //    codes.InsertRange(pos2, new[]
    //    //    {
    //    //        new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
    //    //        new CodeInstruction(OpCodes.Brfalse_S, label),
    //    //        new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
    //    //        new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Angle),
    //    //        new CodeInstruction(OpCodes.Neg),
    //    //        new CodeInstruction(OpCodes.Add)
    //    //    });
    //        return codes;
    //    }
    //}

    //thingがIsOnVehicleMapだった場合回転の初期値num3にベースvehicleのAngleを与え、posはRotatePointで回転
    [HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionBracketFor))]
    public static class Patch_SelectionDrawer_DrawSelectionBracketFor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 8);
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            var rot = generator.DeclareLocal(typeof(Rot8));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Spawned),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_AsAngleRot8),
                new CodeInstruction(OpCodes.Conv_I4),
                new CodeInstruction(OpCodes.Add),
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 16);
            var label2 = generator.DefineLabel();
            var g_DrawPos = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.DrawPos));

            codes[pos2].labels.Add(label2);
            codes.InsertRange(pos2, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label2),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Spawned),
                new CodeInstruction(OpCodes.Brfalse_S, label2),
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Callvirt, g_DrawPos),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_AsAngleRot8),
                new CodeInstruction(OpCodes.Neg),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_RotatePoint)
            });

            var pos3 = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.m_GenDraw_DrawFieldEdges));
            codes[pos3].operand = MethodInfoCache.m_GenDrawOnVehicle_DrawFieldEdges;
            codes.InsertRange(pos3, new[]
            {
                CodeInstruction.LoadLocal(0),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Zone_Map)
            });
            return codes;
        }
    }

    //[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetSleepingSlotPos))]
    //public static class Patch_Building_Bed_GetSleepingSlotPos
    //{
    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //    {
    //        return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
    //    }
    //}

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.DrawLinesBetweenTargets))]
    public static class Patch_Pawn_JobTracker_DrawLinesBetweenTargets
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Position));
            codes.RemoveRange(pos, 4);
            var g_Pawn_DrawPos = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.DrawPos));
            codes.Insert(pos, new CodeInstruction(OpCodes.Callvirt, g_Pawn_DrawPos));

            var g_CenterVector3 = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.CenterVector3));
            var m_CenterVector3VehicleOffset = AccessTools.Method(typeof(Patch_Pawn_JobTracker_DrawLinesBetweenTargets), nameof(Patch_Pawn_JobTracker_DrawLinesBetweenTargets.CenterVector3VehicleOffset));
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.OperandIs(g_CenterVector3))
                {
                    yield return CodeInstruction.LoadArgument(0);
                    yield return CodeInstruction.LoadField(typeof(Pawn_JobTracker), "pawn");
                    code.operand = m_CenterVector3VehicleOffset;
                }
                yield return code;
            }
        }

        public static Vector3 CenterVector3VehicleOffset(ref LocalTargetInfo targ, Pawn pawn)
        {
            if (targ.HasThing)
            {
                if (targ.Thing.Spawned)
                {
                    return targ.Thing.DrawPos;
                }
                if (targ.Thing.SpawnedOrAnyParentSpawned)
                {
                    return targ.Thing.SpawnedParentOrMe.DrawPos;
                }
                return targ.Thing.Position.ToVector3Shifted();
            }
            else
            {
                if (targ.Cell.IsValid)
                {
                    var driver = pawn.jobs.AllJobs()?.First()?.GetCachedDriver(pawn);
                    if (driver is JobDriverAcrossMaps driverAcrossMaps)
                    {
                        if (driverAcrossMaps.DestMap.IsVehicleMapOf(out var vehicle))
                        {
                            return targ.Cell.ToVector3Shifted().OrigToVehicleMap(vehicle);
                        }
                    }
                    else
                    {
                        if (pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
                        {
                            return targ.Cell.ToVector3Shifted().OrigToVehicleMap(vehicle);
                        }
                    }
                    return targ.Cell.ToVector3Shifted();
                }
                return default;
            }
        }
    }

    [HarmonyPatch(typeof(RenderHelper), nameof(RenderHelper.DrawLinesBetweenTargets))]
    public static class Patch_RenderHelper_DrawLinesBetweenTargets
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_Pawn_JobTracker_DrawLinesBetweenTargets.Transpiler(instructions);
        }
    }

    [HarmonyPatch(typeof(PawnPath), nameof(PawnPath.DrawPath))]
    public static class Patch_PawnPath_DrawPath
    {
        public static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(1),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldc_R4, VehicleMapUtility.altitudeOffsetFull),
                new CodeInstruction(OpCodes.Add)
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Stloc_2);
            var label2 = generator.DefineLabel();
            codes[pos2].labels.Add(label2);
            codes.InsertRange(pos2, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label2),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap2),
            });
            var pos3 = codes.FindIndex(pos, c => c.opcode == OpCodes.Stloc_3);
            var label3 = generator.DefineLabel();
            codes[pos3].labels.Add(label3);
            codes.InsertRange(pos3, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label3),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap2),
            });
            var pos4 = codes.FindIndex(pos, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 6);
            var label4 = generator.DefineLabel();
            codes[pos4].labels.Add(label4);
            codes.InsertRange(pos4, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label4),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap2),
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(Graphic), nameof(Graphic.Draw))]
    public static class Patch_Graphic_Draw
    {
        public static void Prefix(ref Vector3 loc, ref Rot4 rot, Thing thing, ref float extraRotation, Graphic __instance)
        {
            if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) && thing.def.drawerType == DrawerType.RealtimeOnly)
            {
                var fullRot = vehicle.FullRotation;
                rot.AsInt += fullRot.RotForVehicleDraw().AsInt;
                var angle = vehicle.Angle;
                if (thing.def.Size != IntVec2.One || !(thing.Graphic is Graphic_Single))
                {
                    extraRotation -= angle;
                    var offset = thing.Graphic.DrawOffset(rot);
                    if (__instance is Graphic_Flicker && thing.TryGetComp<CompFireOverlay>(out var comp))
                    {
                        offset += comp.Props.DrawOffsetForRot(rot);
                    }
                    var offset2 = offset.RotatedBy(-angle);
                    loc += new Vector3(offset2.x - offset.x, 0f, offset2.z - offset.z);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Graphic), nameof(Graphic.DrawFromDef))]
    public static class Patch_Graphic_DrawFromDef
    {
        public static void Prefix(ref Vector3 loc, ref Rot4 rot, ThingDef thingDef, ref float extraRotation, Graphic __instance)
        {
            var vehicle = Command_FocusVehicleMap.FocusedVehicle;
            if (vehicle != null)
            {
                loc = loc.OrigToVehicleMap(vehicle);
                var fullRot = vehicle.FullRotation;
                rot.AsInt += fullRot.RotForVehicleDraw().AsInt;
                var angle = vehicle.Angle;
                if (thingDef.Size != IntVec2.One || !(thingDef.graphic is Graphic_Single))
                {
                    extraRotation -= angle;
                    var offset = __instance.DrawOffset(rot);
                    var offset2 = offset.RotatedBy(-angle);
                    loc += new Vector3(offset2.x - offset.x, 0f, offset2.z - offset.z);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Designation), nameof(Designation.DrawLoc))]
    public static class Patch_Designation_DrawLoc
    {
        public static void Postfix(ref Vector3 __result, DesignationManager ___designationManager, LocalTargetInfo ___target)
        {
            if (!___target.HasThing && ___designationManager.map.IsVehicleMapOf(out var vehicle))
            {
                __result = __result.OrigToVehicleMap(vehicle);
            }
        }
    }

    [HarmonyPatch(typeof(Building_Door), "DrawMovers")]
    public static class Patch_Building_Door_DrawMovers
    {
        public static void Prefix(ref float altitude, Building_Door __instance)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out _))
            {
                altitude += VehicleMapUtility.altitudeOffsetFull;
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotation_Thing)
                .MethodReplacer(MethodInfoCache.g_Rot4_AsQuat, MethodInfoCache.m_Rot8_AsQuatRef)
                .MethodReplacer(MethodInfoCache.m_Rot4_Rotate, MethodInfoCache.m_Rot8_Rotate);
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.DrawRadiusRing_NewTemp))]
    public static class Patch_VerbProperties_DrawRadiusRing_NewTemp
    {
        public static void Prefix(ref IntVec3 center, Verb verb)
        {
            if (verb?.caster.IsOnNonFocusedVehicleMapOf(out var vehicle) ?? false)
            {
                center = center.OrigToVehicleMap(vehicle);
            }
        }
    }

    [HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawRadiusRing), typeof(IntVec3), typeof(float), typeof(Color), typeof(Func<IntVec3, bool>))]
    public static class Patch_GenDraw_DrawRadiusRing
    {
        public static void Prefix(ref IntVec3 center)
        {
            var tmp = center;
            VehiclePawnWithMap vehicle = null;
            if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                center = center.OrigToVehicleMap(Command_FocusVehicleMap.FocusedVehicle);
            }
            else if (Find.Selector.SelectedObjects.Any(o => o is Thing thing && thing.Position == tmp && thing.IsOnNonFocusedVehicleMapOf(out vehicle)))
            {
                center = center.OrigToVehicleMap(vehicle);
            }
        }
    }

    //c.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays) -> c.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays).OrigToVehicleMap();
    [HarmonyPatch(typeof(GenDraw), "DrawInteractionCell")]
    public static class Patch_GenDraw_DrawInteractionCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_1);
            codes.Insert(pos, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap1));
            return codes;
        }
    }
}