using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class MethodInfoCache
{
    private readonly static Verse.WeakReference<MethodInfoCache> cacheInt = new(null);

    public static MethodInfoCache CachedMethodInfo
    {
        get
        {
            cacheInt.Target ??= new MethodInfoCache();
            return cacheInt.Target;
        }
    }

    public readonly MethodInfo g_VehicleMap = AccessTools.PropertyGetter(typeof(VehiclePawnWithMap), nameof(VehiclePawnWithMap.VehicleMap));

    public readonly MethodInfo g_FocusedVehicle = AccessTools.PropertyGetter(typeof(Command_FocusVehicleMap), nameof(Command_FocusVehicleMap.FocusedVehicle));

    public readonly MethodInfo m_FocusedOnVehicleMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedOnVehicleMap));

    public readonly MethodInfo g_Find_CurrentMap = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));

    public readonly MethodInfo g_VehicleMapUtility_CurrentMap = AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CurrentMap));

    public readonly MethodInfo m_IsVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsVehicleMapOf));

    public readonly MethodInfo m_IsNonFocusedVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsNonFocusedVehicleMapOf));

    public readonly MethodInfo m_IsOnVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsOnVehicleMapOf));

    public readonly MethodInfo m_IsOnNonFocusedVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsOnNonFocusedVehicleMapOf));

    public readonly MethodInfo m_YOffsetFull1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.YOffsetFull), [typeof(float)]);

    public readonly MethodInfo m_YOffsetFull2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.YOffsetFull), [typeof(float), typeof(VehiclePawnWithMap)]);

    public readonly MethodInfo m_ToBaseMapCoord1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapCoord), [typeof(Vector3)]);

    public readonly MethodInfo m_ToBaseMapCoord2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapCoord), [typeof(Vector3), typeof(VehiclePawnWithMap)]);

    public readonly MethodInfo m_ToThingMapCoord = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToThingMapCoord));

    public readonly MethodInfo m_ToThingBaseMapCoord1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToThingBaseMapCoord), [typeof(IntVec3), typeof(Thing)]);

    public readonly MethodInfo m_ToThingBaseMapCoord2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToThingBaseMapCoord), [typeof(Vector3), typeof(Thing)]);

    public readonly MethodInfo m_ToVehicleMapCoord1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToVehicleMapCoord), [typeof(Vector3)]);

    public readonly MethodInfo m_ToVehicleMapCoord2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToVehicleMapCoord), [typeof(Vector3), typeof(VehiclePawnWithMap)]);

    public readonly MethodInfo g_Thing_Map = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));

    public readonly MethodInfo m_BaseMap_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), [typeof(Thing)]);

    public readonly MethodInfo m_TargetMapOrMap = AccessTools.Method(typeof(TargetMapManager), nameof(TargetMapManager.TargetMapOrMap));

    public readonly MethodInfo m_TargetMapOrPawnMap = AccessTools.Method(typeof(TargetMapManager), nameof(TargetMapManager.TargetMapOrPawnMap));

    public readonly MethodInfo g_Zone_Map = AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.Map));

    public readonly MethodInfo m_BaseMap_Zone = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), [typeof(Zone)]);

    public readonly MethodInfo g_Thing_MapHeld = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MapHeld));

    public readonly MethodInfo m_MapHeldBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.MapHeldBaseMap));

    public readonly MethodInfo g_Thing_Position = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));

    public readonly MethodInfo m_PositionOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionOnBaseMap), [typeof(Thing)]);

    public readonly MethodInfo g_Thing_PositionHeld = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.PositionHeld));

    public readonly MethodInfo m_PositionHeldOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionHeldOnBaseMap));

    public readonly MethodInfo m_PositionOnAnotherThingMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionOnAnotherThingMap));

    public readonly MethodInfo g_LocalTargetInfo_Cell = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.Cell));

    public readonly MethodInfo m_CellOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), [typeof(LocalTargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_CellOnBaseMap_TargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), [typeof(TargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_OccupiedRect = AccessTools.Method(typeof(GenAdj), nameof(GenAdj.OccupiedRect), [typeof(Thing)]);

    public readonly MethodInfo m_MovedOccupiedRect = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.MovedOccupiedRect));

    public readonly MethodInfo m_ToTargetInfo = AccessTools.Method(typeof(LocalTargetInfo), nameof(LocalTargetInfo.ToTargetInfo));

    public readonly MethodInfo m_ToBaseMapTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapTargetInfo));

    public readonly MethodInfo g_FullRotation = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.FullRotation));

    public readonly MethodInfo m_BaseRotation = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseRotation));

    public readonly MethodInfo m_BaseRotationVehicleDraw = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseRotationVehicleDraw));

    public readonly MethodInfo m_BaseFullRotation_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotation), [typeof(Thing)]);

    public readonly MethodInfo m_BaseFullRotationAsRot4 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotationAsRot4), [typeof(Thing)]);

    public readonly MethodInfo m_BaseFullRotation_Vehicle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotation), [typeof(VehiclePawn)]);

    public readonly MethodInfo g_Angle = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.Angle));

    public readonly MethodInfo g_Rot4_AsAngle = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsAngle));

    public readonly MethodInfo g_Rot8_AsAngle = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsAngle));

    public readonly MethodInfo m_FullAngle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FullAngle));

    public readonly MethodInfo m_BaseMap_Map = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), [typeof(Map)]);

    public readonly MethodInfo m_RotatePoint = AccessTools.Method(typeof(Ext_Math), nameof(Ext_Math.RotatePoint));

    public readonly MethodInfo g_Thing_Spawned = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Spawned));

    public readonly MethodInfo g_Rot4_AsQuat = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsQuat));

    public readonly MethodInfo m_Rot8_AsQuat = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsQuat), [typeof(Rot8)]);

    public readonly MethodInfo m_Rot8_AsQuatRef = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsQuat), [typeof(Rot8).MakeByRefType()]);

    public readonly MethodInfo m_Rot4_Rotate = AccessTools.Method(typeof(Rot4), nameof(Rot4.Rotate));

    public readonly MethodInfo m_Rot8_Rotate = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.Rotate));

    public readonly MethodInfo g_Quaternion_identity = AccessTools.PropertyGetter(typeof(Quaternion), nameof(Quaternion.identity));

    public readonly MethodInfo o_Quaternion_Multiply = AccessTools.Method(typeof(Quaternion), "op_Multiply", [typeof(Quaternion), typeof(Quaternion)]);

    public readonly MethodInfo m_GenDraw_DrawFieldEdges = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawFieldEdges), [typeof(List<IntVec3>), typeof(int)]);

    public readonly MethodInfo m_GenDrawOnVehicle_DrawFieldEdges = AccessTools.Method(typeof(GenDrawOnVehicle), nameof(GenDrawOnVehicle.DrawFieldEdges), [typeof(List<IntVec3>), typeof(int), typeof(Map)]);

    public readonly MethodInfo g_Designator_Map = AccessTools.PropertyGetter(typeof(Designator), nameof(Designator.Map));

    public readonly MethodInfo g_Thing_Rotation = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Rotation));

    public readonly MethodInfo m_RotationForPrint = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.RotationForPrint));

    public readonly MethodInfo g_Thing_DrawPos = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.DrawPos));

    public readonly MethodInfo m_GenThing_TrueCenter1 = AccessTools.Method(typeof(GenThing), nameof(GenThing.TrueCenter), [typeof(Thing)]);

    public readonly MethodInfo m_GenThing_TrueCenter2 = AccessTools.Method(typeof(GenThing), nameof(GenThing.TrueCenter), [typeof(IntVec3), typeof(Rot4), typeof(IntVec2), typeof(float)]);

    public readonly MethodInfo m_RotateForPrintNegate = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.RotateForPrintNegate));

    public readonly MethodInfo m_ShouldLinkWith = AccessTools.Method(typeof(Graphic_Linked), nameof(Graphic_Linked.ShouldLinkWith));

    public readonly MethodInfo m_ShouldLinkWithOrig = AccessTools.Method(typeof(Patch_Graphic_Linked_ShouldLinkWith), nameof(Patch_Graphic_Linked_ShouldLinkWith.ShouldLinkWith));

    public readonly MethodInfo m_GenSight_LineOfSightToThing = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSightToThing));

    public readonly MethodInfo m_GenSightOnVehicle_LineOfSightToThing = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSightToThing));

    public readonly MethodInfo m_GenSight_LineOfSight1 = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSight), [typeof(IntVec3), typeof(IntVec3), typeof(Map)]);

    public readonly MethodInfo m_GenSight_LineOfSight2 = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSight), [typeof(IntVec3), typeof(IntVec3), typeof(Map), typeof(bool), typeof(Func<IntVec3, bool>), typeof(int), typeof(int)]);

    public readonly MethodInfo m_GenSightOnVehicle_LineOfSight1 = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSight), [typeof(IntVec3), typeof(IntVec3), typeof(Map)]);

    public readonly MethodInfo m_GenSightOnVehicle_LineOfSight2 = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSight), [typeof(IntVec3), typeof(IntVec3), typeof(Map), typeof(bool), typeof(Func<IntVec3, bool>), typeof(int), typeof(int)]);

    public readonly MethodInfo m_GenUI_TargetsAtMouse = AccessTools.Method(typeof(GenUI), nameof(GenUI.TargetsAtMouse));

    public readonly MethodInfo m_GenUIOnVehicle_TargetsAtMouse = AccessTools.Method(typeof(GenUIOnVehicle), nameof(GenUIOnVehicle.TargetsAtMouse));

    public readonly MethodInfo m_Matrix4x4_SetTRS = AccessTools.Method(typeof(Matrix4x4), nameof(Matrix4x4.SetTRS));

    public readonly MethodInfo m_SetTRSOnVehicle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.SetTRSOnVehicle));

    public readonly MethodInfo m_Verb_TryFindShootLineFromTo = AccessTools.Method(typeof(Verb), nameof(Verb.TryFindShootLineFromTo));

    public readonly MethodInfo m_TryFindShootLineFromToOnVehicle = AccessTools.Method(typeof(VerbOnVehicleUtility), nameof(VerbOnVehicleUtility.TryFindShootLineFromToOnVehicle));

    public readonly MethodInfo m_CanBeSeenOverFast = AccessTools.Method(typeof(GenGrid), nameof(GenGrid.CanBeSeenOverFast));

    public readonly MethodInfo m_CanBeSeenOverOnVehicle = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.CanBeSeenOverOnVehicle));

    public readonly MethodInfo g_Rot4_FacingCell = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.FacingCell));

    public readonly MethodInfo g_Rot8_FacingCell = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.FacingCell));

    public readonly MethodInfo g_Rot4_RighthandCell = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.RighthandCell));

    public readonly MethodInfo m_Rot8Utility_RighthandCell = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.RighthandCell));

    public readonly MethodInfo m_IntVec3_ToVector3 = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3));

    public readonly MethodInfo m_IntVec3_ToVector3Shifted = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3Shifted));

    public readonly MethodInfo m_IntVec3_ToVector3ShiftedWithAltitude1 = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), [typeof(AltitudeLayer)]);

    public readonly MethodInfo m_IntVec3_ToVector3ShiftedWithAltitude2 = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), [typeof(float)]);

    public readonly MethodInfo m_Rot8Utility_ToFundVector3 = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.ToFundVector3));

    public readonly MethodInfo m_CellRect_ClipInsideMap = AccessTools.Method(typeof(CellRect), nameof(CellRect.ClipInsideMap));

    public readonly MethodInfo m_ClipInsideVehicleMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ClipInsideVehicleMap));

    public readonly MethodInfo m_FocusedDrawPosOffset = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedDrawPosOffset));

    public readonly MethodInfo m_SelectedDrawPosOffset = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.SelectedDrawPosOffset));

    public readonly MethodInfo g_Rot4_AsVector2 = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsVector2));

    public readonly MethodInfo g_Rot8_AsVector2 = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsVector2));

    public readonly MethodInfo m_AsFundVector2 = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsFundVector2));

    public readonly MethodInfo m_GetThingList = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetThingList));

    public readonly MethodInfo m_GetThingListAcrossMaps = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.GetThingListAcrossMaps));

    public readonly MethodInfo m_PrintExtraRotation = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PrintExtraRotation));

    public readonly MethodInfo m_Vector3Utility_WithY = AccessTools.Method(typeof(Vector3Utility), nameof(Vector3Utility.WithY));

    public readonly MethodInfo m_TargetCellOnBaseMap = AccessTools.Method(typeof(TargetMapManager), nameof(TargetMapManager.TargetCellOnBaseMap));

    public readonly MethodInfo m_PositionOnTargetMap = AccessTools.Method(typeof(TargetMapManager), nameof(TargetMapManager.PositionOnTargetMap));

    public readonly MethodInfo m_BreadthFirstTraverse = AccessTools.Method(typeof(RegionTraverser), nameof(RegionTraverser.BreadthFirstTraverse), [typeof(Region), typeof(RegionEntryPredicate), typeof(RegionProcessor), typeof(int), typeof(RegionType)]);

    public readonly MethodInfo m_BreadthFirstTraverseAcrossMaps = AccessTools.Method(typeof(RegionTraverserAcrossMaps), nameof(RegionTraverserAcrossMaps.BreadthFirstTraverse), [typeof(Region), typeof(RegionEntryPredicate), typeof(RegionProcessor), typeof(int), typeof(RegionType)]);
}
