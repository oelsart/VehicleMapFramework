using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class MethodInfoCache
{
    private static readonly Verse.WeakReference<MethodInfoCache> cacheInt = new(null);
    
    public static MethodInfoCache CachedMethodInfo
    {
        get
        {
            cacheInt.Target ??= new MethodInfoCache();
            return cacheInt.Target;
        }
    }
    
    public readonly MethodInfo g_FocusedVehicle = AccessTools.PropertyGetter(typeof(Command_FocusVehicleMap), nameof(Command_FocusVehicleMap.FocusedVehicle));

    public readonly MethodInfo m_FocusedOnVehicleMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedOnVehicleMap));

    public readonly MethodInfo g_Find_CurrentMap = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));

    public readonly MethodInfo g_VehicleMapUtility_CurrentMap = AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CurrentMap));

    public readonly MethodInfo m_IsVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsVehicleMapOf));

    public readonly MethodInfo m_IsNonFocusedVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsNonFocusedVehicleMapOf));

    public readonly MethodInfo m_IsOnVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsOnVehicleMapOf));

    public readonly MethodInfo m_IsOnNonFocusedVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsOnNonFocusedVehicleMapOf));

    public readonly MethodInfo m_YOffsetFull = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.YOffsetFull), [typeof(float), typeof(VehiclePawnWithMap)]);

    public readonly MethodInfo m_ToBaseMapCoord1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapCoord), [typeof(Vector3)]);

    public readonly MethodInfo m_ToBaseMapCoord2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapCoord), [typeof(Vector3), typeof(VehiclePawnWithMap)]);
    
    public readonly MethodInfo m_ToBaseMapCoord3 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapCoord), [typeof(IntVec3), typeof(VehiclePawnWithMap)]);

    public readonly MethodInfo m_ToThingMapCoord = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToThingMapCoord));

    public readonly MethodInfo m_ToNonFocusedThingMapCoord = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToNonFocusedThingMapCoord));

    public readonly MethodInfo m_ToThingBaseMapCoord = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToThingBaseMapCoord), [typeof(Vector3), typeof(Thing)]);

    public readonly MethodInfo m_ToVehicleMapCoord = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToVehicleMapCoord), [typeof(Vector3)]);

    public readonly MethodInfo g_Thing_Map = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));
    
    public readonly MethodBase g_GlobalTargetInfo_Map = AccessTools.PropertyGetter(typeof(GlobalTargetInfo), nameof(GlobalTargetInfo.Map));

    public readonly MethodInfo m_BaseMap_Map = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), [typeof(Map)]);
    
    public readonly MethodInfo m_BaseMapOrCaravan_Map = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_BaseMapOrCaravan), [typeof(Map)]);
    
    public readonly MethodInfo m_BaseMap_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), [typeof(Thing)]);
    
    public readonly MethodInfo m_BaseMapOrCaravan_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_BaseMapOrCaravan), [typeof(Thing)]);
    
    public readonly MethodInfo m_BaseMap_GlobalTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), [typeof(GlobalTargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_TargetMapOrMap = AccessTools.Method(typeof(TargetMapUtility), nameof(TargetMapUtility.TargetMapOrMap));

    public readonly MethodInfo m_TargetMapOrThingMap = AccessTools.Method(typeof(TargetMapUtility), nameof(TargetMapUtility.get_TargetMapOrThingMap));

    public readonly MethodInfo m_TargetMapOrPawnMap = AccessTools.Method(typeof(TargetMapUtility), nameof(TargetMapUtility.get_TargetMapOrPawnMap));
    
    public readonly MethodInfo m_LordMapOrMapHeld = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_LordMapOrMapHeld));

    public readonly MethodInfo g_Zone_Map = AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.Map));

    public readonly MethodInfo g_Thing_MapHeld = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MapHeld));

    public readonly MethodInfo m_MapHeldBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.MapHeldBaseMap));
    
    public readonly MethodInfo m_MapHeldBaseMapOrCaravan = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_MapHeldBaseMapOrCaravan));
    
    public readonly MethodInfo m_DepartMapOrPawnMap = AccessTools.Method(typeof(CrossMapReachabilityUtility), nameof(CrossMapReachabilityUtility.get_DepartMapOrPawnMap));
    
    public readonly MethodInfo m_DepartMapOrPawnMapHeld = AccessTools.Method(typeof(CrossMapReachabilityUtility), nameof(CrossMapReachabilityUtility.get_DepartMapOrPawnMapHeld));

    public readonly MethodInfo g_Thing_Position = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));

    public readonly MethodInfo m_PositionOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_PositionOnBaseMap), [typeof(Thing)]);

    public readonly MethodInfo m_PositionOnBaseMapSpawned = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_PositionOnBaseMapSpawned), [typeof(Thing)]);

    public readonly MethodInfo g_Thing_PositionHeld = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.PositionHeld));

    public readonly MethodInfo m_PositionHeldOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_PositionHeldOnBaseMap));

    public readonly MethodInfo m_PositionHeldOnBaseMapSpawned = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_PositionHeldOnBaseMapSpawned));

    public readonly MethodInfo m_PositionOnAnotherThingMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionOnAnotherThingMap));

    public readonly MethodInfo g_LocalTargetInfo_Cell = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.Cell));
    
    public readonly MethodInfo g_TargetInfo_Cell = AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.Cell));
    
    public readonly MethodInfo g_GlobalTargetInfo_Cell = AccessTools.PropertyGetter(typeof(GlobalTargetInfo), nameof(GlobalTargetInfo.Cell));

    public readonly MethodInfo m_CellOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), [typeof(LocalTargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_CellOnBaseMapSpawned = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMapSpawned), [typeof(LocalTargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_CellOnBaseMap_TargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), [typeof(TargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_CellOnBaseMapSpawned_TargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMapSpawned), [typeof(TargetInfo).MakeByRefType()]);
    
    public readonly MethodInfo m_CellOnBaseMap_GlobalTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), [typeof(GlobalTargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_CellOnBaseMapSpawned_GlobalTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMapSpawned), [typeof(GlobalTargetInfo).MakeByRefType()]);
    
    public readonly MethodInfo m_OccupiedRect = AccessTools.Method(typeof(GenAdj), nameof(GenAdj.OccupiedRect), [typeof(Thing)]);

    public readonly MethodInfo m_MovedOccupiedRect = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.MovedOccupiedRect));

    public readonly MethodInfo m_ToTargetInfo = AccessTools.Method(typeof(LocalTargetInfo), nameof(LocalTargetInfo.ToTargetInfo));

    public readonly MethodInfo m_ToBaseMapTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapTargetInfo));

    public readonly MethodInfo m_BaseRotation = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseRotation));
    
    public readonly MethodInfo m_BaseRotationSpawned = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseRotationSpawned));

    public readonly MethodInfo m_BaseRotationVehicleDraw = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseRotationVehicleDraw));

    public readonly MethodInfo m_BaseFullRotation_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotation), [typeof(Thing)]);

    public readonly MethodInfo m_BaseFullRotationSpawned_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotationSpawned), [typeof(Thing)]);

    public readonly MethodInfo m_BaseFullRotationAsRot4 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotationAsRot4), [typeof(Thing)]);

    public readonly MethodInfo g_Angle = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.Angle));

    public readonly MethodInfo g_Rot4_AsAngle = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsAngle));

    public readonly MethodInfo g_Rot8_AsAngle = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsAngle));

    public readonly MethodInfo m_FullAngle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_FullAngle));

    public readonly MethodInfo m_FullAngleQuat = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_FullAngleQuat));
    
    public readonly MethodInfo m_ExtraAngle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.get_ExtraAngle));

    public readonly MethodInfo m_FlipAngle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FlipAngle));

    public readonly MethodInfo m_RotatePoint = AccessTools.Method(typeof(Ext_Math), nameof(Ext_Math.RotatePoint));

    public readonly MethodInfo g_Rot4_AsQuat = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsQuat));

    public readonly MethodInfo m_Rot8_AsQuatRef = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsQuat), [typeof(Rot8).MakeByRefType()]);

    public readonly MethodInfo m_Rot4_Rotate = AccessTools.Method(typeof(Rot4), nameof(Rot4.Rotate));

    public readonly MethodInfo m_Rot8_Rotate = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.Rotate));

    public readonly MethodInfo g_Quaternion_identity = AccessTools.PropertyGetter(typeof(Quaternion), nameof(Quaternion.identity));

    public readonly MethodInfo o_Quaternion_Multiply = AccessTools.Method(typeof(Quaternion), "op_Multiply", [typeof(Quaternion), typeof(Quaternion)]);

    public readonly MethodInfo m_GenDraw_DrawFieldEdges1 = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawFieldEdges), [typeof(List<IntVec3>), typeof(int)]);
    
    public readonly MethodInfo m_GenDraw_DrawFieldEdges2 = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawFieldEdges), [typeof(List<IntVec3>), typeof(Color), typeof(float?), typeof(HashSet<IntVec3>), typeof(int)]);

    public readonly MethodInfo m_GenDrawOnVehicle_DrawFieldEdges1 = AccessTools.Method(typeof(GenDrawOnVehicle), nameof(GenDrawOnVehicle.DrawFieldEdges), [typeof(List<IntVec3>), typeof(int), typeof(Map)]);

    public readonly MethodInfo m_GenDrawOnVehicle_DrawFieldEdges2 = AccessTools.Method(typeof(GenDrawOnVehicle), nameof(GenDrawOnVehicle.DrawFieldEdges), [typeof(List<IntVec3>), typeof(Color), typeof(float?), typeof(HashSet<IntVec3>), typeof(int), typeof(Map)]);

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

    public readonly MethodInfo m_GenSight_LineOfSightToEdges = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSightToEdges));

    public readonly MethodInfo m_GenSightOnVehicle_LineOfSightToEdges = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSightToEdges));

    public readonly MethodInfo m_GenUI_TargetsAtMouse = AccessTools.Method(typeof(GenUI), nameof(GenUI.TargetsAtMouse));

    public readonly MethodInfo m_GenUIOnVehicle_TargetsAtMouse = AccessTools.Method(typeof(GenUIOnVehicle), nameof(GenUIOnVehicle.TargetsAtMouse));

    public readonly MethodInfo m_Matrix4x4_SetTRS = AccessTools.Method(typeof(Matrix4x4), nameof(Matrix4x4.SetTRS));

    public readonly MethodInfo m_SetTRSOnVehicle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.SetTRSOnVehicle));

    public readonly MethodInfo m_CanBeSeenOverFast = AccessTools.Method(typeof(GenGrid), nameof(GenGrid.CanBeSeenOverFast));

    public readonly MethodInfo m_CanBeSeenOverOnVehicleFast = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.CanBeSeenOverOnVehicleFast));

    public readonly MethodInfo g_Rot4_FacingCell = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.FacingCell));

    public readonly MethodInfo g_Rot8_FacingCell = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.FacingCell));

    public readonly MethodInfo g_Rot4_RighthandCell = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.RighthandCell));

    public readonly MethodInfo m_Rot8Utility_RighthandCell = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.RighthandCell));

    public readonly MethodInfo m_IntVec3_ToVector3 = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3));

    public readonly MethodInfo m_IntVec3_ToVector3Shifted = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3Shifted));

    public readonly MethodInfo m_IntVec3_ToVector3ShiftedWithAltitude = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), [typeof(float)]);
    
    public readonly MethodInfo m_Altitudes_AltitudeFor = AccessTools.Method(typeof(Altitudes), nameof(Altitudes.AltitudeFor), [typeof(AltitudeLayer)]);

    public readonly MethodInfo m_Rot8Utility_ToFundVector3 = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.ToFundVector3));

    public readonly MethodInfo m_CellRect_ClipInsideMap = AccessTools.Method(typeof(CellRect), nameof(CellRect.ClipInsideMap));

    public readonly MethodInfo m_ClipInsideVehicleMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ClipInsideVehicleMap));

    public readonly MethodInfo m_FocusedDrawPosOffset = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedDrawPosOffset));

    public readonly MethodInfo m_SelectedDrawPosOffset = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.SelectedDrawPosOffset));

    public readonly MethodInfo m_FocusedOrSelectedDrawPosOffset = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedOrSelectedDrawPosOffset));

    public readonly MethodInfo g_Rot4_AsVector2 = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsVector2));

    public readonly MethodInfo m_AsFundVector2 = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsFundVector2));

    public readonly MethodInfo m_GetThingList = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetThingList));

    public readonly MethodInfo m_GetThingListAcrossMaps = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.GetThingListAcrossMaps));

    public readonly MethodInfo m_PrintExtraRotation = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PrintExtraRotation));

    public readonly MethodInfo m_Vector3Utility_WithY = AccessTools.Method(typeof(Vector3Utility), nameof(Vector3Utility.WithY));

    public readonly MethodInfo m_TargetCellOnBaseMap = AccessTools.Method(typeof(TargetMapUtility), nameof(TargetMapUtility.TargetCellOnBaseMap));

    public readonly MethodInfo m_PositionOnTargetMap = AccessTools.Method(typeof(TargetMapUtility), nameof(TargetMapUtility.get_PositionOnTargetMap));

    public readonly MethodInfo m_BreadthFirstTraverse = AccessTools.Method(typeof(RegionTraverser), nameof(RegionTraverser.BreadthFirstTraverse), [typeof(Region), typeof(RegionEntryPredicate), typeof(RegionProcessor), typeof(int), typeof(RegionType)]);

    public readonly MethodInfo m_BreadthFirstTraverseAcrossMaps = AccessTools.Method(typeof(RegionTraverserAcrossMaps), nameof(RegionTraverserAcrossMaps.BreadthFirstTraverse), [typeof(Region), typeof(RegionEntryPredicate), typeof(RegionProcessor), typeof(int), typeof(RegionType)]);

    public readonly MethodInfo m_IsForbidden = AccessTools.Method(typeof(ForbidUtility),
        nameof(ForbidUtility.IsForbidden), [typeof(IntVec3), typeof(Pawn)]);

    public readonly MethodInfo m_CrossMapIsForbidden1 = AccessTools.Method(typeof(CrossMapForbidUtility),
        nameof(CrossMapForbidUtility.IsForbidden), [typeof(IntVec3), typeof(Pawn), typeof(Thing)]);
    
    public readonly MethodInfo m_CrossMapIsForbidden2 = AccessTools.Method(typeof(CrossMapForbidUtility),
        nameof(CrossMapForbidUtility.IsForbidden), [typeof(IntVec3), typeof(Pawn), typeof(Map)]);

    public readonly MethodInfo m_AllInventoryItems = AccessTools.Method(typeof(CaravanInventoryUtility),
        nameof(CaravanInventoryUtility.AllInventoryItems));

    public readonly MethodInfo m_AllInventoryItems_Original = AccessTools.Method(
        typeof(Patch_CaravanInventoryUtility_AllInventoryItems),
        nameof(Patch_CaravanInventoryUtility_AllInventoryItems.AllInventoryItems));

    public readonly MethodInfo m_RotatedBy = AccessTools.Method(typeof(Vector3Utility),
        nameof(Vector3Utility.RotatedBy), [typeof(Vector3), typeof(float)]);
}
