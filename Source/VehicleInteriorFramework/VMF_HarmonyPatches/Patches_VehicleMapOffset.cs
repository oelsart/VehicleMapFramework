using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Vehicles.Rendering;
using Verse;
using Verse.AI;
using static VehicleInteriors.MethodInfoCache;

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
            codes.Insert(pos, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToVehicleMapCoord1));
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
            return !t.TryGetDrawPos(ref __result);
        }
    }

    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    public static class Patch_Pawn_DrawTracker_DrawPos
    {
        public static bool Prefix(Pawn ___pawn, ref Vector3 __result)
        {
            return !___pawn.TryGetDrawPos(ref __result);
        }

        public static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            __result.y += ___pawn.jobs?.curDriver is JobDriverAcrossMaps driver ? driver.ForcedBodyOffset.y : 0f;
        }
    }

    [HarmonyPatch(typeof(VehicleDrawTracker), nameof(VehicleDrawTracker.DrawPos), MethodType.Getter)]
    public static class Patch_VehiclePawn_DrawPos
    {
        public static bool Prefix(VehiclePawn ___vehicle, ref Vector3 __result, out bool __state)
        {
            __state = !___vehicle.TryGetDrawPos(ref __result);
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
            if (__instance.link1.Target.HasThing) return true;

            return !__instance.TryGetDrawPos(ref __result);
        }
    }

    [HarmonyPatch(typeof(VehicleSkyfaller), "RootPos", MethodType.Getter)]
    public static class Patch_VehicleSkyfaller_RootPos
    {
        public static void Postfix(VehicleSkyfaller __instance, ref Vector3 __result)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                __result = __result.ToBaseMapCoord(vehicle);
            }
        }
    }

    [HarmonyPatch(typeof(FleckSystemBase<FleckStatic>), nameof(FleckSystemBase<FleckStatic>.CreateFleck))]
    public static class Patch_FleckSystemBase_FleckStatic_CreateFleck
    {
        public static void Prefix(FleckSystemBase<FleckStatic> __instance, ref FleckCreationData creationData)
        {
            if (__instance.parent.parent.IsNonFocusedVehicleMapOf(out var vehicle))
            {
                creationData.Offset(vehicle);
            }
        }

        public static void Offset(this ref FleckCreationData creationData, VehiclePawnWithMap vehicle)
        {
            if (creationData.link.Target.HasThing || Patch_GenView_ShouldSpawnMotesAt.offset)
            {
                creationData.spawnPosition.y += vehicle.DrawPos.y;
                Patch_GenView_ShouldSpawnMotesAt.offset = false;
            }
            else
            {
                creationData.spawnPosition = creationData.spawnPosition.ToBaseMapCoord(vehicle);
            }
        }
    }

    [HarmonyPatch(typeof(FleckSystemBase<FleckThrown>), nameof(FleckSystemBase<FleckThrown>.CreateFleck))]
    public static class Patch_FleckSystemBase_FleckThrown_CreateFleck
    {
        public static void Prefix(FleckSystemBase<FleckThrown> __instance, ref FleckCreationData creationData)
        {
            if (__instance.parent.parent.IsNonFocusedVehicleMapOf(out var vehicle))
            {
                creationData.Offset(vehicle);
            }
        }
    }

    [HarmonyPatch(typeof(FleckSystemBase<FleckSplash>), nameof(FleckSystemBase<FleckSplash>.CreateFleck))]
    public static class Patch_FleckSystemBase_FleckSplash_CreateFleck
    {
        public static void Prefix(FleckSystemBase<FleckSplash> __instance, ref FleckCreationData creationData)
        {
            if (__instance.parent.parent.IsNonFocusedVehicleMapOf(out var vehicle))
            {
                creationData.Offset(vehicle);
            }
        }
    }

    [HarmonyPatch(typeof(FleckStatic), nameof(FleckStatic.Draw), new[] { typeof(float), typeof(DrawBatch) })]
    public static class Patch_FleckStatic_Draw
    {
        public static void Prefix(FleckStatic __instance, ref float altitude)
        {
            altitude = __instance.DrawPos.y;
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
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldc_R4, VehicleMapUtility.altitudeOffsetFull),
                new CodeInstruction(OpCodes.Add)
            });
            return codes;
        }
    }

    //thingがIsOnVehicleMapだった場合回転の初期値num3にベースvehicleのAngleを与え、posはRotatePointで回転
    [HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionBracketFor))]
    [HarmonyAfter("owlchemist.smartfarming")]
    public static class Patch_SelectionDrawer_DrawSelectionBracketFor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 9);
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            var rot = generator.DeclareLocal(typeof(Rot8));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_FullRotation),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Rot8_AsAngle),
                new CodeInstruction(OpCodes.Conv_I4),
                new CodeInstruction(OpCodes.Add),
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 17);
            var label2 = generator.DefineLabel();
            var g_DrawPos = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.DrawPos));

            codes[pos2].labels.Add(label2);
            codes.InsertRange(pos2, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label2),
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Callvirt, g_DrawPos),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Rot8_AsAngle),
                new CodeInstruction(OpCodes.Neg),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotatePoint)
            });

            var smartFarmingActive = ModsConfig.IsActive("Owlchemist.SmartFarming");
            var pos3 = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(smartFarmingActive ? AccessTools.Method("SmartFarming.MapComponent_SmartFarming:DrawFieldEdges") :CachedMethodInfo.m_GenDraw_DrawFieldEdges));
            codes[pos3].operand = smartFarmingActive ? AccessTools.Method("VMF_SmartFarmingPatch.GenDrawOnVehicleSF:DrawFieldEdges") : CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges;
            codes.InsertRange(pos3, new[]
            {
                CodeInstruction.LoadLocal(0),
                new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Zone_Map)
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
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Position));
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
                    var driver = pawn.jobs.AllJobs()?.FirstOrDefault()?.GetCachedDriver(pawn);
                    if (TargetMapManager.HasTargetMap(pawn, out var map) && pawn.stances.curStance is Stance_Busy)
                    {
                        return targ.Cell.ToVector3Shifted().ToBaseMapCoord(map);
                    }
                    else if (driver is JobDriverAcrossMaps driverAcrossMaps)
                    {
                        var destMap = driverAcrossMaps.DestMap;
                        if (destMap.IsNonFocusedVehicleMapOf(out var vehicle))
                        {
                            return targ.Cell.ToVector3Shifted().ToBaseMapCoord(vehicle);
                        }
                    }
                    else if (pawn.IsOnNonFocusedVehicleMapOf(out var vehicle) && !(pawn.stances.curStance is Stance_Busy busy && (busy.verb is Verb_Jump || busy.verb is Verb_CastAbilityJump)))
                    {
                        return targ.Cell.ToVector3Shifted().ToBaseMapCoord(vehicle);
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
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Position));
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
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
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
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2),
            });
            var pos3 = codes.FindIndex(pos, c => c.opcode == OpCodes.Stloc_3);
            var label3 = generator.DefineLabel();
            codes[pos3].labels.Add(label3);
            codes.InsertRange(pos3, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label3),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2),
            });
            var pos4 = codes.FindIndex(pos, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 6);
            var label4 = generator.DefineLabel();
            codes[pos4].labels.Add(label4);
            codes.InsertRange(pos4, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label4),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2),
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

                var rot2 = rot;
                var baseRotInt = vehicle.FullRotation.RotForVehicleDraw().AsInt;
                bool SameMaterialByRot()
                {
                    var graphic = def.graphic;
                    var rotation = new Rot4(rot2.AsInt + baseRotInt);
                    return graphic != null && graphic.MatAt(rot2, thing) == graphic.MatAt(rotation, thing) && graphic.DrawOffset(rot2) == graphic.DrawOffset(rotation);
                }

                if (thing.Isnt<Building_Bookcase>() || thing.Graphic == __instance)
                {
                    if (def.size.x != def.size.z || ((def.graphicData?.drawRotated ?? false) && (!def.graphicData?.Linked ?? true) || def.rotatable) && !SameMaterialByRot())
                    {
                        rot.AsInt += baseRotInt;
                    }
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
            else if (!thing.Spawned && thing.SpawnedParentOrMe.IsOnNonFocusedVehicleMapOf(out vehicle))
            {
                extraRotation += vehicle.FullRotation.AsAngle;
            }
        }
    }

    [HarmonyPatch(typeof(Graphic), nameof(Graphic.DrawFromDef))]
    public static class Patch_Graphic_DrawFromDef
    {
        public static void Prefix(ref Vector3 loc, ref Rot4 rot, ThingDef thingDef, ref float extraRotation, Graphic __instance)
        {
            if (VehicleMapUtility.FocusedOnVehicleMap(out var vehicle) && thingDef != null)
            {
                var def = thingDef.IsBlueprint ? thingDef.entityDefToBuild as ThingDef : thingDef;
                var compProperties = def.GetCompProperties<CompProperties_FireOverlay>();
                var flag = __instance is Graphic_Flicker && compProperties != null;

                if (flag)
                {
                    loc -= (def.graphicData?.DrawOffsetForRot(rot) ?? Vector3.zero) + compProperties.DrawOffsetForRot(rot);
                }

                var angle = vehicle.Angle;
                var loc2 = loc.ToBaseMapCoord(vehicle);
                loc = loc2.WithY(Mathf.Min(loc2.y, AltitudeLayer.MetaOverlays.AltitudeFor()));
                var rot2 = rot;
                var baseRotInt = vehicle.FullRotation.RotForVehicleDraw().AsInt;
                bool SameMaterialByRot()
                {
                    var graphic = def.graphic;
                    var rotation = new Rot4(rot2.AsInt + baseRotInt);
                    return graphic != null && graphic.MatAt(rot2, null) == graphic.MatAt(rotation, null) && graphic.DrawOffset(rot2) == graphic.DrawOffset(rotation);
                }

                if (def.size.x != def.size.z || ((def.graphicData?.drawRotated ?? false) && (!def.graphicData?.Linked ?? true) || def.rotatable) && !SameMaterialByRot())
                {
                     rot.AsInt += baseRotInt;
                }
                var flag2 = def.ShouldRotatedOnVehicle();
                if (flag2)
                {
                    extraRotation -= angle;
                }
                Vector3 offset = def.graphicData?.DrawOffsetForRot(rot) ?? Vector3.zero;
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
                    __result = __result.ToBaseMapCoord(vehicle).WithY(__result.y);
                }
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing)
                .MethodReplacer(CachedMethodInfo.g_Rot4_AsVector2, CachedMethodInfo.m_AsFundVector2);
        }
    }

    [HarmonyPatch(typeof(OverlayDrawer), "RenderPulsingOverlay", typeof(Thing), typeof(Material), typeof(int), typeof(Mesh), typeof(bool))]
    public static class Patch_OverlayDrawer_RenderPulsingOverlay
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing)
                .MethodReplacer(CachedMethodInfo.g_Rot4_AsVector2, CachedMethodInfo.m_AsFundVector2);
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.DrawRadiusRing))]
    public static class Patch_VerbProperties_DrawRadiusRing
    {
        public static void Prefix(ref IntVec3 center, Verb verb)
        {
            if (verb?.caster.IsOnNonFocusedVehicleMapOf(out var vehicle) ?? false)
            {
                center = center.ToBaseMapCoord(vehicle);
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
                    center = center.ToBaseMapCoord(vehicle);
                }
            }
            else if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                center = center.ToBaseMapCoord(Command_FocusVehicleMap.FocusedVehicle);
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
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_SelectedDrawPosOffset)
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Quaternion_identity));
            codes.InsertRange(pos2, new[]
            {
                CodeInstruction.LoadArgument(2),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FocusedDrawPosOffset)
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
            codes.Insert(pos, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord1));
            return codes;
        }

        [HarmonyPatch(new Type[] { typeof(Vector3), typeof(AltitudeLayer) })]
        public static void Prefix(ref Vector3 c)
        {
            c = c.ToBaseMapCoord();
        }
    }

    //v, v2にToBaseMapCoordをしてDrawBoxRotatedにFocusedVehicle.FullRotation.AsAngleを渡す
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
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord1),
                new CodeInstruction(OpCodes.Ldc_R4, 0f),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_Vector3Utility_WithY),
                new CodeInstruction(OpCodes.Stloc_2)
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Newobj && c.OperandIs(c_Vector3)) + 1;
            codes.InsertRange(pos2, new[]
            {
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord1),
                new CodeInstruction(OpCodes.Ldc_R4, 0f),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_Vector3Utility_WithY),
            });

            var m_Widgets_DrawBox = AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawBox));
            var pos3 = codes.FindIndex(pos2, c => c.Calls(m_Widgets_DrawBox));
            var m_DrawBoxRotated = AccessTools.Method(typeof(VMF_Widgets), nameof(VMF_Widgets.DrawBoxRotated));
            var label = generator.DefineLabel();
            var label2 = generator.DefineLabel();

            codes[pos3].operand = m_DrawBoxRotated;
            codes[pos3].labels.Add(label2);
            codes.InsertRange(pos3, new[]
            {
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Angle),
                new CodeInstruction(OpCodes.Br_S, label2),
                new CodeInstruction(OpCodes.Ldc_R4, 0f).WithLabels(label),
            });

            var m_Widgets_DrawNumberOnMap = AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawNumberOnMap));
            var m_ConvertToVehicleMap = AccessTools.Method(typeof(Patch_DesignationDragger_DraggerOnGUI), nameof(ConvertToVehicleMap));
            var pos4 = codes.FindIndex(pos3, c => c.Calls(m_Widgets_DrawNumberOnMap)) - 3;
            codes.Insert(pos4, new CodeInstruction(OpCodes.Call, m_ConvertToVehicleMap));
            
            var pos5 = codes.FindIndex(pos4 + 5, c => c.Calls(m_Widgets_DrawNumberOnMap)) - 3;
            codes.Insert(pos5, new CodeInstruction(OpCodes.Call, m_ConvertToVehicleMap));

            var pos6 = codes.FindIndex(pos5 + 5, c => c.Calls(m_Widgets_DrawNumberOnMap)) - 4;
            codes.Insert(pos6, new CodeInstruction(OpCodes.Call, m_ConvertToVehicleMap));

            return codes;
        }

        private static Vector2 ConvertToVehicleMap(Vector2 screenPos)
        {
            screenPos.y = UI.screenHeight - screenPos.y;
            return UI.UIToMapPosition(screenPos).ToBaseMapCoord().WithY(0f).MapToUIPosition();
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
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_GenThing_TrueCenter)) - 1;
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(ArgumentNum),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FocusedDrawPosOffset)
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
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_GenDraw_DrawFieldEdges));
            var label = generator.DefineLabel();
            codes[pos].operand = CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges;
            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Ldnull),
                CodeInstruction.LoadArgument(5),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Pop),
                CodeInstruction.LoadArgument(5),
                new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map),
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
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_IntVec3_ToVector3Shifted)) + 1;
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(MoteAttachLink), "targetInt", true),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.Map))),
                new CodeInstruction(OpCodes.Ldloca, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2)
            });
            return codes;
        }
    }
}