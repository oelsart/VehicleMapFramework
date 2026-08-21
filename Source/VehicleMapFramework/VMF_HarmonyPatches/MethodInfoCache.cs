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
// ReSharper disable InvokeAsExtensionMember

namespace VehicleMapFramework;

public class MethodInfoCache
{
    private static readonly System.WeakReference<MethodInfoCache> cacheInt = new(null);

    public static MethodInfoCache CachedMethodInfo
    {
      get
      {
        if (!cacheInt.TryGetTarget(out var target))
        {
          cacheInt.SetTarget(target = new MethodInfoCache());
          // List.AnyはGenCollectionによって解決されるためCIランで失敗する
          var hasNullField = false;
          foreach (var f in AccessTools.GetDeclaredFields(typeof(MethodInfoCache)))
          {
            if (f.IsStatic) continue;
            if (f.GetValue(target) is null)
            {
              hasNullField = true;
              break;
            }
          }
          if (hasNullField)
          {
            VMF_Log.Error(
              "MethodInfoCache failed to cache all MethodInfos. This may cause errors in some Harmony patches.");
          }
        }
        
        return target;
      }
    }

    public readonly MethodInfo g_FocusedVehicle = AccessTools.PropertyGetter(typeof(Command_FocusVehicleMap), nameof(Command_FocusVehicleMap.FocusedVehicle));

    public readonly MethodInfo m_FocusedOnVehicleMap = ((Delegate)VehicleMapUtility.FocusedOnVehicleMap).Method;

    public readonly MethodInfo g_Find_CurrentMap = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));

    public readonly MethodInfo g_VehicleMapUtility_CurrentMap = AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CurrentMap));

    public readonly MethodInfo m_IsVehicleMapOf = ((Delegate)VehicleMapUtility.IsVehicleMapOf).Method;

    public readonly MethodInfo m_IsNonFocusedVehicleMapOf = ((Delegate)VehicleMapUtility.IsNonFocusedVehicleMapOf).Method;

    public readonly MethodInfo m_IsOnVehicleMapOf = ((Delegate)VehicleMapUtility.IsOnVehicleMapOf).Method;

    public readonly MethodInfo m_IsOnNonFocusedVehicleMapOf = ((Delegate)VehicleMapUtility.IsOnNonFocusedVehicleMapOf).Method;

    public readonly MethodInfo m_YOffsetFull = ((Func<float, VehiclePawnWithMap, float>)VehicleMapUtility.YOffsetFull).Method;

    public readonly MethodInfo m_ToBaseMapCoord1 = ((Func<Vector3, Vector3>)VehicleMapUtility.ToBaseMapCoord).Method;

    public readonly MethodInfo m_ToBaseMapCoord2 = ((Func<Vector3, VehiclePawnWithMap, Vector3>)VehicleMapUtility.ToBaseMapCoord).Method;

    public readonly MethodInfo m_ToBaseMapCoord3 = ((Func<Vector3, Map, Vector3>)VehicleMapUtility.ToBaseMapCoord).Method;
    
    public readonly MethodInfo m_ToBaseMapCoordCell = ((Func<IntVec3, VehiclePawnWithMap, IntVec3>)VehicleMapUtility.ToBaseMapCoord).Method;

    public readonly MethodInfo m_ToThingMapCoord = ((Delegate)VehicleMapUtility.ToThingMapCoord).Method;

    public readonly MethodInfo m_ToNonFocusedThingMapCoord = ((Delegate)VehicleMapUtility.ToNonFocusedThingMapCoord).Method;

    public readonly MethodInfo m_ToThingBaseMapCoord = ((Func<Vector3, Thing, Vector3>)VehicleMapUtility.ToThingBaseMapCoord).Method;

    public readonly MethodInfo m_ToVehicleMapCoord = ((Func<Vector3, Vector3>)VehicleMapUtility.ToVehicleMapCoord).Method;

    public readonly MethodInfo g_Thing_Map = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));
    
    public readonly MethodBase g_GlobalTargetInfo_Map = AccessTools.PropertyGetter(typeof(GlobalTargetInfo), nameof(GlobalTargetInfo.Map));

    public readonly MethodInfo m_BaseMap_Map = ((Func<Map, Map>)VehicleMapUtility.BaseMap).Method;
    
    public readonly MethodInfo m_BaseMapOrCaravan_Map = ((Func<Map, object>)VehicleMapUtility.get_BaseMapOrCaravan).Method;
    
    public readonly MethodInfo m_BaseMap_Thing = ((Func<Thing, Map>)VehicleMapUtility.BaseMap).Method;
    
    public readonly MethodInfo m_BaseMapOrCaravan_Thing = ((Func<Thing, object>)VehicleMapUtility.get_BaseMapOrCaravan).Method;
    
    public readonly MethodInfo m_BaseMap_GlobalTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), [typeof(GlobalTargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_TargetMapOrMap = ((Delegate)TargetMapUtility.TargetMapOrMap).Method;

    public readonly MethodInfo m_TargetMapOrThingMap = ((Delegate)TargetMapUtility.get_TargetMapOrThingMap).Method;

    public readonly MethodInfo m_TargetMapOrPawnMap = ((Delegate)TargetMapUtility.get_TargetMapOrPawnMap).Method;
    
    public readonly MethodInfo m_LordMapOrMapHeld = ((Delegate)VehicleMapUtility.get_LordMapOrMapHeld).Method;

    public readonly MethodInfo g_Zone_Map = AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.Map));

    public readonly MethodInfo g_Thing_MapHeld = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MapHeld));

    public readonly MethodInfo m_MapHeldBaseMap = ((Delegate)VehicleMapUtility.MapHeldBaseMap).Method;
    
    public readonly MethodInfo m_MapHeldBaseMapOrCaravan = ((Delegate)VehicleMapUtility.get_MapHeldBaseMapOrCaravan).Method;
    
    public readonly MethodInfo m_DepartMapOrPawnMap = ((Delegate)CrossMapReachabilityUtility.get_DepartMapOrPawnMap).Method;
    
    public readonly MethodInfo m_DepartMapOrPawnMapHeld = ((Delegate)CrossMapReachabilityUtility.get_DepartMapOrPawnMapHeld).Method;

    public readonly MethodInfo g_Thing_Position = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));

    public readonly MethodInfo m_PositionOnBaseMap = ((Delegate)VehicleMapUtility.get_PositionOnBaseMap).Method;

    public readonly MethodInfo m_PositionOnBaseMapSpawned = ((Delegate)VehicleMapUtility.get_PositionOnBaseMapSpawned).Method;

    public readonly MethodInfo g_Thing_PositionHeld = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.PositionHeld));

    public readonly MethodInfo m_PositionHeldOnBaseMap = ((Delegate)VehicleMapUtility.get_PositionHeldOnBaseMap).Method;

    public readonly MethodInfo m_PositionHeldOnBaseMapSpawned = ((Delegate)VehicleMapUtility.get_PositionHeldOnBaseMapSpawned).Method;

    public readonly MethodInfo m_PositionOnAnotherThingMap = ((Delegate)VehicleMapUtility.PositionOnAnotherThingMap).Method;

    public readonly MethodInfo g_LocalTargetInfo_Cell = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.Cell));
    
    public readonly MethodInfo g_TargetInfo_Cell = AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.Cell));
    
    public readonly MethodInfo g_GlobalTargetInfo_Cell = AccessTools.PropertyGetter(typeof(GlobalTargetInfo), nameof(GlobalTargetInfo.Cell));
    
    public readonly MethodInfo m_CellOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), [typeof(LocalTargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_CellOnBaseMapSpawned = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMapSpawned), [typeof(LocalTargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_CellOnBaseMap_TargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), [typeof(TargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_CellOnBaseMapSpawned_TargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMapSpawned), [typeof(TargetInfo).MakeByRefType()]);
    
    public readonly MethodInfo m_CellOnBaseMap_GlobalTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), [typeof(GlobalTargetInfo).MakeByRefType()]);

    public readonly MethodInfo m_CellOnBaseMapSpawned_GlobalTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMapSpawned), [typeof(GlobalTargetInfo).MakeByRefType()]);
    
    public readonly MethodInfo m_OccupiedRect = ((Func<Thing, CellRect>)GenAdj.OccupiedRect).Method;

    public readonly MethodInfo m_MovedOccupiedRect = ((Delegate)VehicleMapUtility.MovedOccupiedRect).Method;

    public readonly MethodInfo m_ToTargetInfo = AccessTools.Method(typeof(LocalTargetInfo), nameof(LocalTargetInfo.ToTargetInfo));

    public readonly MethodInfo m_ToBaseMapTargetInfo = ((Delegate)VehicleMapUtility.ToBaseMapTargetInfo).Method;

    public readonly MethodInfo m_BaseRotation = ((Delegate)VehicleMapUtility.BaseRotation).Method;
    
    public readonly MethodInfo m_BaseRotationSpawned = ((Delegate)VehicleMapUtility.BaseRotationSpawned).Method;

    public readonly MethodInfo m_BaseRotationVehicleDraw = ((Delegate)VehicleMapUtility.BaseRotationVehicleDraw).Method;

    public readonly MethodInfo m_BaseFullRotation_Thing = ((Func<Thing, Rot8>)VehicleMapUtility.BaseFullRotation).Method;

    public readonly MethodInfo m_BaseFullRotationSpawned_Thing = ((Func<Thing, Rot8>)VehicleMapUtility.BaseFullRotationSpawned).Method;

    public readonly MethodInfo m_BaseFullRotationAsRot4 = ((Func<Thing, Rot4>)VehicleMapUtility.BaseFullRotationAsRot4).Method;

    public readonly MethodInfo g_Angle = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.Angle));

    public readonly MethodInfo g_Rot4_AsAngle = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsAngle));

    public readonly MethodInfo g_Rot8_AsAngle = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsAngle));

    public readonly MethodInfo m_FullAngle = ((Delegate)VehicleMapUtility.get_FullAngle).Method;

    public readonly MethodInfo m_FullAngleQuat = ((Delegate)VehicleMapUtility.get_FullAngleQuat).Method;
    
    public readonly MethodInfo m_ExtraAngle = ((Delegate)VehicleMapUtility.get_ExtraAngle).Method;

    public readonly MethodInfo m_FlipAngle = ((Delegate)VehicleMapUtility.FlipAngle).Method;

    public readonly MethodInfo m_RotatePoint = ((Delegate)Ext_Math.RotatePoint).Method;

    public readonly MethodInfo g_Rot4_AsQuat = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsQuat));

    public readonly MethodInfo m_Rot8_AsQuatRef = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsQuat), [typeof(Rot8).MakeByRefType()]);

    public readonly MethodInfo m_Rot4_Rotate = AccessTools.Method(typeof(Rot4), nameof(Rot4.Rotate));

    public readonly MethodInfo m_Rot8_Rotate = ((Delegate)Rot8Utility.Rotate).Method;

    public readonly MethodInfo g_Quaternion_identity = AccessTools.PropertyGetter(typeof(Quaternion), nameof(Quaternion.identity));

    public readonly MethodInfo o_Quaternion_Multiply = AccessTools.Method(typeof(Quaternion), "op_Multiply", [typeof(Quaternion), typeof(Quaternion)]);

    public readonly MethodInfo m_GenDraw_DrawFieldEdges1 = ((Action<List<IntVec3>, int>)GenDraw.DrawFieldEdges).Method;
    
    public readonly MethodInfo m_GenDraw_DrawFieldEdges2 = ((Action<List<IntVec3>, Color, float?, HashSet<IntVec3>, int>)GenDraw.DrawFieldEdges).Method;

    public readonly MethodInfo m_GenDrawOnVehicle_DrawFieldEdges1 = ((Action<List<IntVec3>, int, Map>)GenDrawOnVehicle.DrawFieldEdges).Method;

    public readonly MethodInfo m_GenDrawOnVehicle_DrawFieldEdges2 = ((Action<List<IntVec3>, Color, float?, HashSet<IntVec3>, int, Map>)GenDrawOnVehicle.DrawFieldEdges).Method;

    public readonly MethodInfo g_Designator_Map = AccessTools.PropertyGetter(typeof(Designator), nameof(Designator.Map));

    public readonly MethodInfo g_Thing_Rotation = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Rotation));

    public readonly MethodInfo m_RotationForPrint = ((Delegate)VehicleMapUtility.RotationForPrint).Method;

    public readonly MethodInfo g_Thing_DrawPos = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.DrawPos));

    public readonly MethodInfo m_GenThing_TrueCenter1 = ((Func<Thing, Vector3>)GenThing.TrueCenter).Method;

    public readonly MethodInfo m_GenThing_TrueCenter2 = ((Func<IntVec3, Rot4, IntVec2, float, Vector3>)GenThing.TrueCenter).Method;

    public readonly MethodInfo m_RotateForPrintNegate = ((Delegate)VehicleMapUtility.RotateForPrintNegate).Method;

    public readonly MethodInfo m_ShouldLinkWith = AccessTools.Method(typeof(Graphic_Linked), nameof(Graphic_Linked.ShouldLinkWith));

    public readonly MethodInfo m_ShouldLinkWithOrig = ((Delegate)Patch_Graphic_Linked_ShouldLinkWith.ShouldLinkWith).Method;

    public readonly MethodInfo m_GenSight_LineOfSightToThing = ((Delegate)GenSight.LineOfSightToThing).Method;

    public readonly MethodInfo m_GenSightOnVehicle_LineOfSightToThing = ((Func<IntVec3, Thing, Map, bool, Func<IntVec3, bool>, bool>)GenSightOnVehicle.LineOfSightToThing).Method;

    public readonly MethodInfo m_GenSight_LineOfSight1 = ((Func<IntVec3, IntVec3, Map, bool>)GenSight.LineOfSight).Method;

    public readonly MethodInfo m_GenSight_LineOfSight2 = ((Func<IntVec3, IntVec3, Map, bool, Func<IntVec3, bool>, int, int, bool>)GenSight.LineOfSight).Method;

    public readonly MethodInfo m_GenSightOnVehicle_LineOfSight1 = ((Func<IntVec3, IntVec3, Map, bool>)GenSightOnVehicle.LineOfSight).Method;

    public readonly MethodInfo m_GenSightOnVehicle_LineOfSight2 = ((Func<IntVec3, IntVec3, Map, bool, Func<IntVec3, bool>, int, int, bool>)GenSightOnVehicle.LineOfSight).Method;

    public readonly MethodInfo m_GenSight_LineOfSightToEdges = ((Delegate)GenSight.LineOfSightToEdges).Method;

    public readonly MethodInfo m_GenSightOnVehicle_LineOfSightToEdges = ((Func<IntVec3, IntVec3, Map, bool, Func<IntVec3, bool>, bool>)GenSightOnVehicle.LineOfSightToEdges).Method;

    public readonly MethodInfo m_GenUI_TargetsAtMouse = ((Delegate)GenUI.TargetsAtMouse).Method;

    public readonly MethodInfo m_GenUIOnVehicle_TargetsAtMouse = ((Delegate)GenUIOnVehicle.TargetsAtMouse).Method;

    public readonly MethodInfo m_Matrix4x4_SetTRS = AccessTools.Method(typeof(Matrix4x4), nameof(Matrix4x4.SetTRS));

    public readonly MethodInfo m_SetTRSOnVehicle = ((Delegate)VehicleMapUtility.SetTRSOnVehicle).Method;

    public readonly MethodInfo m_CanBeSeenOverFast = ((Delegate)GenGrid.CanBeSeenOverFast).Method;

    public readonly MethodInfo m_CanBeSeenOverOnVehicleFast = ((Func<IntVec3, Map, bool>)GenSightOnVehicle.CanBeSeenOverOnVehicleFast).Method;

    public readonly MethodInfo g_Rot4_FacingCell = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.FacingCell));

    public readonly MethodInfo g_Rot8_FacingCell = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.FacingCell));

    public readonly MethodInfo g_Rot4_RighthandCell = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.RighthandCell));

    public readonly MethodInfo m_Rot8Utility_RighthandCell = ((Delegate)Rot8Utility.RighthandCell).Method;

    public readonly MethodInfo m_ToIntVec3 = ((Delegate)IntVec3Utility.ToIntVec3).Method;

    public readonly MethodInfo m_IntVec3_ToVector3 = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3));

    public readonly MethodInfo m_IntVec3_ToVector3Shifted = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3Shifted));

    public readonly MethodInfo m_IntVec3_ToVector3ShiftedWithAltitude = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), [typeof(float)]);
    
    public readonly MethodInfo m_Altitudes_AltitudeFor = ((Func<AltitudeLayer, float>)Altitudes.AltitudeFor).Method;

    public readonly MethodInfo m_Rot8Utility_ToFundVector3 = ((Delegate)Rot8Utility.ToFundVector3).Method;

    public readonly MethodInfo m_CellRect_ClipInsideMap = AccessTools.Method(typeof(CellRect), nameof(CellRect.ClipInsideMap));

    public readonly MethodInfo m_ClipInsideVehicleMap = ((Delegate)VehicleMapUtility.ClipInsideVehicleMap).Method;

    public readonly MethodInfo m_FocusedDrawPosOffset = ((Delegate)VehicleMapUtility.FocusedDrawPosOffset).Method;

    public readonly MethodInfo m_SelectedDrawPosOffset = ((Delegate)VehicleMapUtility.SelectedDrawPosOffset).Method;

    public readonly MethodInfo m_FocusedOrSelectedDrawPosOffset = ((Delegate)VehicleMapUtility.FocusedOrSelectedDrawPosOffset).Method;

    public readonly MethodInfo g_Rot4_AsVector2 = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsVector2));

    public readonly MethodInfo m_AsFundVector2 = ((Delegate)Rot8Utility.AsFundVector2).Method;

    public readonly MethodInfo m_Roofed = AccessTools.Method(typeof(RoofGrid), nameof(RoofGrid.Roofed), [typeof(IntVec3)]);

    public readonly MethodInfo m_RoofedAcrossMaps = ((Func<RoofGrid, IntVec3, bool>)VehicleMapUtility.RoofedAcrossMaps).Method;

    public readonly MethodInfo m_GetThingList = ((Delegate)GridsUtility.GetThingList).Method;

    public readonly MethodInfo m_GetThingListAcrossMaps = ((Delegate)VehicleMapUtility.GetThingListAcrossMaps).Method;

    public readonly MethodInfo m_AddColonistBuildingList = ((Delegate)VehicleMapUtility.AddColonistBuildingList).Method;

    public readonly MethodInfo m_PrintExtraRotation = ((Delegate)VehicleMapUtility.PrintExtraRotation).Method;

    public readonly MethodInfo m_Vector3Utility_WithY = ((Delegate)Vector3Utility.WithY).Method;

    public readonly MethodInfo m_TargetCellOnBaseMap = ((Delegate)TargetMapUtility.TargetCellOnBaseMap).Method;

    public readonly MethodInfo m_PositionOnTargetMap = ((Delegate)TargetMapUtility.get_PositionOnTargetMap).Method;

    public readonly MethodInfo m_BreadthFirstTraverse = ((Action<Region, RegionEntryPredicate, RegionProcessor, int, RegionType>)RegionTraverser.BreadthFirstTraverse).Method;

    public readonly MethodInfo m_BreadthFirstTraverseAcrossMaps = ((Action<Region, RegionEntryPredicate, RegionProcessor, int, RegionType>)RegionTraverserAcrossMaps.BreadthFirstTraverse).Method;

    public readonly MethodInfo m_IsForbidden = ((Func<IntVec3, Pawn, bool>)ForbidUtility.IsForbidden).Method;

    public readonly MethodInfo m_CrossMapIsForbidden1 = ((Func<IntVec3, Pawn, Thing, bool>)CrossMapForbidUtility.IsForbidden).Method;
    
    public readonly MethodInfo m_CrossMapIsForbidden2 = ((Func<IntVec3, Pawn, Map, bool>)CrossMapForbidUtility.IsForbidden).Method;

    public readonly MethodInfo m_AllInventoryItems = ((Delegate)CaravanInventoryUtility.AllInventoryItems).Method;

    public readonly MethodInfo m_AllInventoryItems_Original = ((Delegate)Patch_CaravanInventoryUtility_AllInventoryItems.AllInventoryItems).Method;

    public readonly MethodInfo m_RotatedBy = ((Func<Vector3, float, Vector3>)Vector3Utility.RotatedBy).Method;
    
    public readonly MethodInfo g_AllPawnsSpawned = AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawnsSpawned));
    
    public readonly MethodInfo m_AllPawnsSpawned_Reverse = ((Delegate)Patch_MapPawns_AllPawnsSpawned.AllPawnsSpawned).Method;
    
    public readonly MethodInfo g_AllPawns = AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawns));

    public readonly MethodInfo m_AllPawns_Reverse = ((Delegate)Patch_MapPawns_AllPawns.AllPawns).Method;

    public readonly MethodInfo g_Vector3_up = AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.up));
    
    public readonly MethodInfo m_Quaternion_AngleAxis = ((Delegate)Quaternion.AngleAxis).Method;
}
