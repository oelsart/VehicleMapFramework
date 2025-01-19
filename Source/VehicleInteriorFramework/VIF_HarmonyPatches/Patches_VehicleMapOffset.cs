using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VMF_HarmonyPatches
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

    [HarmonyPatch(typeof(VehicleSkyfaller), "RootPos", MethodType.Getter)]
    public static class Patch_VehicleSkyfaller_RootPos
    {
        public static void Postfix(VehicleSkyfaller __instance, ref Vector3 __result)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                __result = __result.OrigToVehicleMap(vehicle);
            }
        }
    }


    //VehicleSkyfallerのyが上書きされてたので車上のVehicleSkyfallerはy足しときなね
    [HarmonyPatch(typeof(LaunchProtocol), nameof(LaunchProtocol.Draw))]
    public static class Patch_LaunchProtocol_Draw
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var m_AltitudeFor = AccessTools.Method(typeof(Altitudes), nameof(Altitudes.AltitudeFor), new Type[] { typeof(AltitudeLayer) });
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_AltitudeFor)) + 1;
            var label = generator.DefineLabel();
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(LaunchProtocol), "map"),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldc_R4, VehicleMapUtility.altitudeOffsetFull),
                new CodeInstruction(OpCodes.Add)
            });
            return codes;
        }
    }

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
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_Rot8_AsAngle),
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
                //new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                //new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Spawned),
                //new CodeInstruction(OpCodes.Brfalse_S, label2),
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Callvirt, g_DrawPos),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_Rot8_AsAngle),
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
                    var driver = pawn.jobs.AllJobs()?.First().GetCachedDriver(pawn);
                    if (driver is JobDriverAcrossMaps driverAcrossMaps)
                    {
                        var destMap = driverAcrossMaps.DestMap;
                        if (destMap.IsVehicleMapOf(out var vehicle) && (Find.CurrentMap != destMap || Find.CurrentMap.IsVehicleMapOf(out _)))
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
                    code.operand = m_CenterVector3VehicleOffset;
                }
                yield return code;
            }
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
            if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) && thing.def.drawerType == DrawerType.RealtimeOnly && thing.def.category != ThingCategory.Item)
            {
                var def = thing.def.IsBlueprint ? thing.def.entityDefToBuild as ThingDef : thing.def;
                if ((def.rotatable || def.graphic is Graphic_Multi) && (!def.graphicData?.Linked ?? true))
                {
                    var fullRot = vehicle.FullRotation;
                    rot.AsInt += fullRot.RotForVehicleDraw().AsInt;
                }
                if (def.ShouldRotatedOnVehicle())
                {
                    var angle = vehicle.Angle;
                    extraRotation -= angle;
                    var offset = thing.Graphic.DrawOffset(rot);
                    if (__instance is Graphic_Flicker && !(thing.Graphic is Graphic_Single) && thing.TryGetComp<CompFireOverlay>(out var comp))
                    {
                        offset += comp.Props.DrawOffsetForRot(rot);
                    }
                    var offset2 = offset.RotatedBy(-angle);
                    loc += new Vector3(offset2.x - offset.x, 0f, offset2.z - offset.z);
                }

                ////はしごとかのマップ端オフセット
                //VehicleMapProps mapProps;
                //if (thing.HasComp<CompVehicleEnterSpot>() && (mapProps = vehicle.VehicleDef.GetModExtension<VehicleMapProps>()) != null)
                //{
                //    var baseRot = thing.BaseFullRotation().Opposite;
                //    loc += baseRot.Opposite.AsVector2.ToVector3() * mapProps.EdgeSpaceValue(vehicle.FullRotation, thing.Rotation.Opposite);
                //}
            }
        }
    }

    [HarmonyPatch(typeof(Graphic), nameof(Graphic.DrawFromDef))]
    public static class Patch_Graphic_DrawFromDef
    {
        public static void Prefix(ref Vector3 loc, ref Rot4 rot, ThingDef thingDef, ref float extraRotation, Graphic __instance)
        {
            var vehicle = Command_FocusVehicleMap.FocusedVehicle;
            if (vehicle == null && VehicleInteriors.settings.drawPlanet)
            {
                Find.CurrentMap.IsVehicleMapOf(out vehicle);
            }
            if (vehicle != null)
            {
                var def = thingDef.IsBlueprint ? thingDef.entityDefToBuild as ThingDef : thingDef;
                var compProperties = def.GetCompProperties<CompProperties_FireOverlay>();
                var flag = __instance is Graphic_Flicker && compProperties != null;

                if (flag)
                {
                    loc -= def.graphicData.DrawOffsetForRot(rot) + compProperties.DrawOffsetForRot(rot);
                }

                var angle = vehicle.Angle;
                loc = loc.OrigToVehicleMap(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor());
                var rot2 = rot;
                if ((def.rotatable || def.graphic is Graphic_Multi) && (!def.graphicData?.Linked ?? true))
                {
                    var fullRot = vehicle.FullRotation;
                    rot.AsInt += fullRot.RotForVehicleDraw().AsInt;
                }
                var flag2 = def.ShouldRotatedOnVehicle();
                if (flag2)
                {
                    extraRotation -= angle;
                }
                Vector3 offset = def.graphicData.DrawOffsetForRot(rot);
                if (flag)
                {
                    var offset2 = compProperties.DrawOffsetForRot(rot);
                    loc += (offset + offset2).RotatedBy(flag2 ? -angle : 0f);
                }
                else
                {
                    var offset2 = offset.RotatedBy(flag2 ? -angle : 0f);
                    loc += new Vector3(offset2.x - offset.x, 0f, offset2.z - offset.z);
                }

                //はしごとかのマップ端オフセット
                VehicleMapProps mapProps;
                if (thingDef.HasComp<CompVehicleEnterSpot>() && (mapProps = vehicle.VehicleDef.GetModExtension<VehicleMapProps>()) != null)
                {
                    var baseRot = new Rot8(Rot8.FromIntClockwise((vehicle.FullRotation.AsIntClockwise + new Rot8(rot2).AsIntClockwise) % 8));
                    loc += baseRot.Opposite.AsVector2.ToVector3() * mapProps.EdgeSpaceValue(vehicle.FullRotation, rot2.Opposite);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Designation), nameof(Designation.DrawLoc))]
    public static class Patch_Designation_DrawLoc
    {
        public static void Postfix(ref Vector3 __result, DesignationManager ___designationManager, LocalTargetInfo ___target)
        {
            if (___designationManager.map.IsVehicleMapOf(out var vehicle))
            {
                if (!___target.HasThing)
                {
                    __result = __result.OrigToVehicleMap(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor());
                }
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotation_Thing)
                .MethodReplacer(MethodInfoCache.g_Rot4_AsVector2, MethodInfoCache.m_AsFundVector2);
        }
    }

    [HarmonyPatch(typeof(OverlayDrawer), "RenderPulsingOverlay", typeof(Thing), typeof(Material), typeof(int), typeof(Mesh), typeof(bool))]
    public static class Patch_OverlayDrawer_RenderPulsingOverlay
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotation_Thing)
                .MethodReplacer(MethodInfoCache.g_Rot4_AsVector2, MethodInfoCache.m_AsFundVector2);
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
            Thing thing;
            if ((thing = Find.Selector.SelectedObjects.OfType<Thing>().FirstOrDefault(t => t.Position == tmp)) != null)
            {
                if (thing.IsOnNonFocusedVehicleMapOf(out vehicle))
                {
                    center = center.OrigToVehicleMap(vehicle);
                }
            }
            else if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                center = center.OrigToVehicleMap(Command_FocusVehicleMap.FocusedVehicle);
            }
        }
    }

    //tDef.interactionCellGraphic.DrawFromDef(vector, rot, tDef.interactionCellIcon, 0f) ->
    //tDef.interactionCellGraphic.DrawFromDef(vector, rot, tDef.interactionCellIcon, 0f)
    //Graphics.DrawMesh(MeshPool.plane10, SelectedDrawPosOffset(vector, center), Quaternion.identity, GenDraw.InteractionCellMaterial, 0) ->
    //Graphics.DrawMesh(MeshPool.plane10, FocusedDrawPosOffset(vector, center), Quaternion.identity, GenDraw.InteractionCellMaterial, 0)
    [HarmonyPatch(typeof(GenDraw), "DrawInteractionCell")]
    public static class Patch_GenDraw_DrawInteractionCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldloc_S && (c.operand as LocalBuilder).LocalIndex == 4);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(2),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_SelectedDrawPosOffset)
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Quaternion_identity));
            codes.InsertRange(pos2, new[]
            {
                CodeInstruction.LoadArgument(2),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_FocusedDrawPosOffset)
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawTargetHighlightWithLayer))]
    public static class Patch_GenDraw_DrawTargetHighlightWithLayer
    {
        //Vector3 position = c.ToVector3ShiftedWithAltitude(layer); ->
        //Vector3 position = c.ToVector3ShiftedWithAltitude(layer).OrigToVehicleMap();
        [HarmonyPatch(new Type[] { typeof(IntVec3), typeof(AltitudeLayer), typeof(Material) })]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);
            codes.Insert(pos, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap1));
            return codes;
        }

        [HarmonyPatch(new Type[] { typeof(Vector3), typeof(AltitudeLayer) })]
        public static void Prefix(ref Vector3 c)
        {
            c = c.OrigToVehicleMap();
        }
    }

    //v, v2にOrigToVehicleをしてDrawBoxRotatedにFocusedVehicle.FullRotation.AsAngleを渡す
    //Widgets.DrawNumberOnMap(screenPos, intVec.x, Color.white) ->
    //Widgets.DrawNumberOnMap(ConvertToVehicleMap(screenPos), intVec.x, Color.white)を3回
    [HarmonyPatch(typeof(DesignationDragger), nameof(DesignationDragger.DraggerOnGUI))]
    public static class Patch_DesignationDragger_DraggerOnGUI
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var c_Vector3 = AccessTools.Constructor(typeof(Vector3), new Type[] { typeof(float), typeof(float), typeof(float) });
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(c_Vector3)) + 1;
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap1),
                new CodeInstruction(OpCodes.Stloc_2)
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Newobj && c.OperandIs(c_Vector3)) + 1;
            codes.Insert(pos2, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap1));

            var m_Widgets_DrawBox = AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawBox));
            var pos3 = codes.FindIndex(pos2, c => c.opcode == OpCodes.Call && c.OperandIs(m_Widgets_DrawBox));
            var m_DrawBoxRotated = AccessTools.Method(typeof(VMF_Widgets), nameof(VMF_Widgets.DrawBoxRotated));
            var label = generator.DefineLabel();
            var label2 = generator.DefineLabel();

            codes[pos3].operand = m_DrawBoxRotated;
            codes[pos3].labels.Add(label2);
            codes.InsertRange(pos3, new[]
            {
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Angle),
                new CodeInstruction(OpCodes.Br_S, label2),
                new CodeInstruction(OpCodes.Ldc_R4, 0f).WithLabels(label),
            });

            var m_Widgets_DrawNumberOnMap = AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawNumberOnMap));
            var m_ConvertToVehicleMap = AccessTools.Method(typeof(Patch_DesignationDragger_DraggerOnGUI), nameof(Patch_DesignationDragger_DraggerOnGUI.ConvertToVehicleMap));
            var pos4 = codes.FindIndex(pos3, c => c.opcode == OpCodes.Call && c.OperandIs(m_Widgets_DrawNumberOnMap)) - 3;
            codes.Insert(pos4, new CodeInstruction(OpCodes.Call, m_ConvertToVehicleMap));
            
            var pos5 = codes.FindIndex(pos4 + 5, c => c.opcode == OpCodes.Call && c.OperandIs(m_Widgets_DrawNumberOnMap)) - 3;
            codes.Insert(pos5, new CodeInstruction(OpCodes.Call, m_ConvertToVehicleMap));

            var pos6 = codes.FindIndex(pos5 + 5, c => c.opcode == OpCodes.Call && c.OperandIs(m_Widgets_DrawNumberOnMap)) - 3;
            codes.Insert(pos6, new CodeInstruction(OpCodes.Call, m_ConvertToVehicleMap));

            return codes;
        }

        private static Vector2 ConvertToVehicleMap(Vector2 screenPos)
        {
            screenPos.y = UI.screenHeight - screenPos.y;
            return UI.UIToMapPosition(screenPos).OrigToVehicleMap().MapToUIPosition();
        }
    }

    //GenDraw.DrawLineBetween(GenThing.TrueCenter(pos, Rot4.North, def.size, def.Altitude), t.TrueCenter(), SimpleColor.Red, 0.2f) ->
    //GenDraw.DrawLineBetween(FocusedDrawPosOffset(GenThing.TrueCenter(pos, Rot4.North, def.size, def.Altitude), pos), t.TrueCenter(), SimpleColor.Red, 0.2f)
    [HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.DrawArtificialBuildingOverlay))]
    public static class Patch_MeditationUtility_DrawArtificialBuildingOverlay
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_MeditationUtility_DrawArtificialBuildingOverlay.TranspilerCommon(instructions);

        public static IEnumerable<CodeInstruction> TranspilerCommon(IEnumerable<CodeInstruction> instructions, int ArgumentNum = 0)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.m_GenThing_TrueCenter)) - 1;
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(ArgumentNum),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_FocusedDrawPosOffset)
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.DrawMeditationSpotOverlay))]
    public static class Patch_MeditationUtility_DrawMeditationSpotOverlay
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_MeditationUtility_DrawArtificialBuildingOverlay.TranspilerCommon(instructions);
    }

    [HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.DrawMeditationFociAffectedByBuildingOverlay))]
    public static class Patch_MeditationUtility_DrawMeditationFociAffectedByBuildingOverlay
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_MeditationUtility_DrawArtificialBuildingOverlay.TranspilerCommon(instructions, 3);
    }

    [HarmonyPatch(typeof(PlaceWorker_ShowTradeBeaconRadius), nameof(PlaceWorker_ShowTradeBeaconRadius.DrawGhost))]
    public static class Patch_PlaceWorker_ShowTradeBeaconRadius_DrawGhost
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.m_GenDraw_DrawFieldEdges));
            var label = generator.DefineLabel();
            codes[pos].operand = MethodInfoCache.m_GenDrawOnVehicle_DrawFieldEdges;
            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Ldnull),
                CodeInstruction.LoadArgument(5),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Pop),
                CodeInstruction.LoadArgument(5),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Map),
            });
            return codes;
        }
    }

    //CellがターゲットのMoteにオフセットをかける
    [HarmonyPatch(typeof(MoteAttachLink), nameof(MoteAttachLink.UpdateDrawPos))]
    public static class Patch_MoteAttachLink_UpdateDrawPos
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.m_IntVec3_ToVector3Shifted)) + 1;
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(MoteAttachLink), "targetInt", true),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.Map))),
                new CodeInstruction(OpCodes.Ldloca, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap2)
            });
            return codes;
        }
    }
}