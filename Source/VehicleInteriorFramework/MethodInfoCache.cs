﻿using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VehicleInteriors.VMF_HarmonyPatches;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class MethodInfoCache
    {
        public static readonly MethodInfo g_VehicleMap = AccessTools.PropertyGetter(typeof(VehiclePawnWithMap), nameof(VehiclePawnWithMap.VehicleMap));

        public static readonly MethodInfo g_FocusedVehicle = AccessTools.PropertyGetter(typeof(Command_FocusVehicleMap), nameof(Command_FocusVehicleMap.FocusedVehicle));

        public static readonly MethodInfo m_FocusedOnVehicleMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedOnVehicleMap));

        public static readonly MethodInfo g_Find_CurrentMap = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));

        public static readonly MethodInfo g_VehicleMapUtility_CurrentMap = AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CurrentMap));

        public static readonly MethodInfo m_IsVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsVehicleMapOf));

        public static readonly MethodInfo m_IsNonFocusedVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsNonFocusedVehicleMapOf));

        public static readonly MethodInfo m_IsOnVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsOnVehicleMapOf));

        public static readonly MethodInfo m_IsOnNonFocusedVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsOnNonFocusedVehicleMapOf));

        public static readonly MethodInfo m_ToBaseMapCoord1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapCoord), new Type[] { typeof(Vector3) });

        public static readonly MethodInfo m_ToBaseMapCoord2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapCoord), new Type[] { typeof(Vector3), typeof(VehiclePawnWithMap) });

        public static readonly MethodInfo m_ToThingBaseMapCoord1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToThingBaseMapCoord), new Type[] { typeof(IntVec3), typeof(Thing) });

        public static readonly MethodInfo m_ToThingBaseMapCoord2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToThingBaseMapCoord), new Type[] { typeof(Vector3), typeof(Thing) });

        public static readonly MethodInfo m_ToVehicleMapCoord1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToVehicleMapCoord), new Type[] { typeof(Vector3) });

        public static readonly MethodInfo m_ToVehicleMapCoord2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToVehicleMapCoord), new Type[] { typeof(Vector3), typeof(VehiclePawnWithMap) });

        public static readonly MethodInfo g_Thing_Map = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));

        public static readonly MethodInfo m_BaseMap_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), new Type[] { typeof(Thing) });

        public static readonly MethodInfo g_Zone_Map = AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.Map));

        public static readonly MethodInfo m_BaseMap_Zone = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), new Type[] { typeof(Zone) });

        public static readonly MethodInfo g_Thing_MapHeld = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MapHeld));

        public static readonly MethodInfo m_MapHeldBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.MapHeldBaseMap));

        public static readonly MethodInfo g_Thing_Position = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));

        public static readonly MethodInfo m_PositionOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionOnBaseMap), new Type[] { typeof(Thing) });

        public static readonly MethodInfo g_Thing_PositionHeld = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.PositionHeld));

        public static readonly MethodInfo m_PositionHeldOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionHeldOnBaseMap));

        public static readonly MethodInfo m_PositionOnAnotherThingMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionOnAnotherThingMap));
        
        public static readonly MethodInfo g_LocalTargetInfo_Cell = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.Cell));

        public static readonly MethodInfo m_CellOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), new Type[] { typeof(LocalTargetInfo).MakeByRefType()} );

        public static readonly MethodInfo m_CellOnBaseMap_TargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap), new Type[] { typeof(TargetInfo).MakeByRefType() });

        public static readonly MethodInfo m_OccupiedRect = AccessTools.Method(typeof(GenAdj), nameof(GenAdj.OccupiedRect), new Type[] { typeof(Thing) });

        public static readonly MethodInfo m_MovedOccupiedRect = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.MovedOccupiedRect));

        public static readonly MethodInfo m_ToTargetInfo = AccessTools.Method(typeof(LocalTargetInfo), nameof(LocalTargetInfo.ToTargetInfo));

        public static readonly MethodInfo m_ToBaseMapTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapTargetInfo));

        public static readonly MethodInfo g_FullRotation = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.FullRotation));

        public static readonly MethodInfo m_BaseRotation = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseRotation));

        public static readonly MethodInfo m_BaseRotationVehicleDraw = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseRotationVehicleDraw));

        public static readonly MethodInfo m_BaseFullRotation_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotation), new Type[] { typeof(Thing) });

        public static readonly MethodInfo m_BaseFullRotationAsRot4 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotationAsRot4), new Type[] { typeof(Thing) });

        public static readonly MethodInfo m_BaseFullRotation_Vehicle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotation), new Type[] { typeof(VehiclePawn) });

        public static readonly MethodInfo g_Angle = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.Angle));

        public static readonly MethodInfo g_Rot4_AsAngle = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsAngle));

        public static readonly MethodInfo g_Rot8_AsAngle = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsAngle));

        public static readonly MethodInfo m_BaseMap_Map = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), new Type[] { typeof(Map) });

        public static readonly MethodInfo m_RotatePoint = AccessTools.Method(typeof(Ext_Math), nameof(Ext_Math.RotatePoint));

        public static readonly MethodInfo g_Thing_Spawned = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Spawned));

        public static readonly MethodInfo g_Rot4_AsQuat = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsQuat));

        public static readonly MethodInfo m_Rot8_AsQuat = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsQuat), new Type[] { typeof(Rot8) });

        public static readonly MethodInfo m_Rot8_AsQuatRef = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsQuat), new Type[] { typeof(Rot8).MakeByRefType() });

        public static readonly MethodInfo m_Rot4_Rotate = AccessTools.Method(typeof(Rot4), nameof(Rot4.Rotate));

        public static readonly MethodInfo m_Rot8_Rotate = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.Rotate));

        public static readonly MethodInfo g_Quaternion_identity = AccessTools.PropertyGetter(typeof(Quaternion), nameof(Quaternion.identity));

        public static readonly MethodInfo o_Quaternion_Multiply = AccessTools.Method(typeof(Quaternion), "op_Multiply", new Type[] { typeof(Quaternion), typeof(Quaternion) });

        public static readonly MethodInfo m_GenDraw_DrawFieldEdges = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawFieldEdges), new Type[] { typeof(List<IntVec3>), typeof(int) });

        public static readonly MethodInfo m_GenDrawOnVehicle_DrawFieldEdges = AccessTools.Method(typeof(GenDrawOnVehicle), nameof(GenDrawOnVehicle.DrawFieldEdges), new Type[] { typeof(List<IntVec3>), typeof(Map) });

        public static readonly MethodInfo g_Designator_Map = AccessTools.PropertyGetter(typeof(Designator), nameof(Designator.Map));

        public static readonly MethodInfo g_Thing_Rotation = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Rotation));

        public static readonly MethodInfo m_RotationForPrint = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.RotationForPrint));

        public static readonly MethodInfo g_Thing_DrawPos = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.DrawPos));

        public static readonly MethodInfo m_GenThing_TrueCenter = AccessTools.Method(typeof(GenThing), nameof(GenThing.TrueCenter), new Type[] { typeof(Thing) });

        public static readonly MethodInfo m_RotateForPrintNegate = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.RotateForPrintNegate));

        public static readonly MethodInfo m_ShouldLinkWith = AccessTools.Method(typeof(Graphic_Linked), nameof(Graphic_Linked.ShouldLinkWith));

        public static readonly MethodInfo m_ShouldLinkWithOrig = AccessTools.Method(typeof(Patch_Graphic_Linked_ShouldLinkWith), nameof(Patch_Graphic_Linked_ShouldLinkWith.ShouldLinkWith));

        public static readonly MethodInfo m_GenSight_LineOfSightToThing = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSightToThing));

        public static readonly MethodInfo m_GenSightOnVehicle_LineOfSightToThingVehicle = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSightToThingVehicle));

        public static readonly MethodInfo m_GenSightOnVehicle_LineOfSightToThing = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSightToThing));

        public static readonly MethodInfo m_GenSight_LineOfSight1 = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSight), new Type[] { typeof(IntVec3), typeof(IntVec3), typeof(Map) });

        public static readonly MethodInfo m_GenSight_LineOfSight2 = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSight), new Type[] { typeof(IntVec3), typeof(IntVec3), typeof(Map), typeof(bool), typeof(Func<IntVec3, bool>), typeof(int), typeof(int) });

        public static readonly MethodInfo m_GenSightOnVehicle_LineOfSight1 = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSight), new Type[] { typeof(IntVec3), typeof(IntVec3), typeof(Map) });

        public static readonly MethodInfo m_GenSightOnVehicle_LineOfSight2 = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSight), new Type[] { typeof(IntVec3), typeof(IntVec3), typeof(Map), typeof(bool), typeof(Func<IntVec3, bool>), typeof(int), typeof(int) });

        public static readonly MethodInfo m_GenUI_TargetsAtMouse = AccessTools.Method(typeof(GenUI), nameof(GenUI.TargetsAtMouse));

        public static readonly MethodInfo m_GenUIOnVehicle_TargetsAtMouse = AccessTools.Method(typeof(GenUIOnVehicle), nameof(GenUIOnVehicle.TargetsAtMouse));

        public static readonly MethodInfo m_Matrix4x4_SetTRS = AccessTools.Method(typeof(Matrix4x4), nameof(Matrix4x4.SetTRS));

        public static readonly MethodInfo m_SetTRSOnVehicle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.SetTRSOnVehicle));

        public static readonly MethodInfo m_Verb_TryFindShootLineFromTo = AccessTools.Method(typeof(Verb), nameof(Verb.TryFindShootLineFromTo));

        public static readonly MethodInfo m_TryFindShootLineFromToOnVehicle = AccessTools.Method(typeof(VerbOnVehicleUtility), nameof(VerbOnVehicleUtility.TryFindShootLineFromToOnVehicle));

        public static readonly MethodInfo m_CanBeSeenOverFast = AccessTools.Method(typeof(GenGrid), nameof(GenGrid.CanBeSeenOverFast));

        public static readonly MethodInfo m_CanBeSeenOverOnVehicle = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.CanBeSeenOverOnVehicle));

        public static readonly MethodInfo m_ReachabilityUtility_CanReach = AccessTools.Method(typeof(ReachabilityUtility), nameof(ReachabilityUtility.CanReach), new Type[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(bool), typeof(bool), typeof(TraverseMode) });

        public static readonly MethodInfo m_ReachabilityUtilityOnVehicle_CanReach = AccessTools.Method(typeof(ReachabilityUtilityOnVehicle), nameof(ReachabilityUtilityOnVehicle.CanReach), new Type[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(bool), typeof(bool), typeof(TraverseMode) });

        public static readonly MethodInfo m_Reachability_CanReach1 = AccessTools.Method(typeof(Reachability), nameof(Reachability.CanReach), new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseMode), typeof(Danger) });

        public static readonly MethodInfo m_Reachability_CanReach2 = AccessTools.Method(typeof(Reachability), nameof(Reachability.CanReach), new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms) });

        public static readonly MethodInfo m_CanReachReaplaceable1 = AccessTools.Method(typeof(ReachabilityUtilityOnVehicle), nameof(ReachabilityUtilityOnVehicle.CanReachReplaceable), new Type[] { typeof(Reachability), typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseMode), typeof(Danger) });

        public static readonly MethodInfo m_CanReachReaplaceable2 = AccessTools.Method(typeof(ReachabilityUtilityOnVehicle), nameof(ReachabilityUtilityOnVehicle.CanReachReplaceable), new Type[] { typeof(Reachability), typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms) });

        public static readonly MethodInfo g_Rot4_FacingCell = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.FacingCell));

        public static readonly MethodInfo g_Rot8_FacingCell = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.FacingCell));

        public static readonly MethodInfo g_Rot4_RighthandCell = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.RighthandCell));

        public static readonly MethodInfo m_Rot8Utility_RighthandCell = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.RighthandCell));

        public static readonly MethodInfo m_IntVec3_ToVector3 = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3));

        public static readonly MethodInfo m_IntVec3_ToVector3Shifted = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3Shifted));

        public static readonly MethodInfo m_IntVec3_ToVector3ShiftedWithAltitude1 = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), new[] { typeof(AltitudeLayer) });

        public static readonly MethodInfo m_IntVec3_ToVector3ShiftedWithAltitude2 = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), new[] { typeof(float) });

        public static readonly MethodInfo m_Rot8Utility_ToFundVector3 = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.ToFundVector3));

        public static readonly MethodInfo m_CellRect_ClipInsideMap = AccessTools.Method(typeof(CellRect), nameof(CellRect.ClipInsideMap));

        public static readonly MethodInfo m_ClipInsideVehicleMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ClipInsideVehicleMap));

        public static readonly MethodInfo m_FocusedDrawPosOffset = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedDrawPosOffset));

        public static readonly MethodInfo m_SelectedDrawPosOffset = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.SelectedDrawPosOffset));

        public static readonly MethodInfo g_Rot4_AsVector2 = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsVector2));

        public static readonly MethodInfo g_Rot8_AsVector2 = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsVector2));

        public static readonly MethodInfo m_AsFundVector2 = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsFundVector2));

        public static readonly MethodInfo m_GetThingList = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetThingList));

        public static readonly MethodInfo m_GetThingListAcrossMaps = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.GetThingListAcrossMaps));

        public static readonly MethodInfo m_UI_MouseCell = AccessTools.Method(typeof(UI), nameof(UI.MouseCell));

        public static readonly MethodInfo m_Stub_MouseCell = AccessTools.Method(typeof(Patch_UI_MouseCell), nameof(Patch_UI_MouseCell.MouseCell));

        public static readonly MethodInfo m_PrintExtraRotation = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PrintExtraRotation));

        public static readonly MethodInfo m_Vector3Utility_WithY = AccessTools.Method(typeof(Vector3Utility), nameof(Vector3Utility.WithY));

        public static readonly MethodInfo m_HaulToStorageJob = AccessTools.Method(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToStorageJob));

        public static readonly MethodInfo m_HaulToStorageJobReplace = AccessTools.Method(typeof(HaulAIAcrossMapsUtility), nameof(HaulAIAcrossMapsUtility.HaulToStorageJobReplace));

        public static readonly MethodInfo m_PawnCanAutomaticallyHaulFast = AccessTools.Method(typeof(HaulAIUtility), nameof(HaulAIUtility.PawnCanAutomaticallyHaulFast));

        public static readonly MethodInfo m_PawnCanAutomaticallyHaulFastReplace = AccessTools.Method(typeof(HaulAIAcrossMapsUtility), nameof(HaulAIAcrossMapsUtility.PawnCanAutomaticallyHaulFastReplace));

        public static readonly MethodInfo m_TryFindBestBetterStorageFor = AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStorageFor));

        public static readonly MethodInfo m_TryFindBestBetterStorageForReplace = AccessTools.Method(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.TryFindBestBetterStorageForReplace));

        public static readonly MethodInfo m_IsGoodStoreCell = AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell));

        public static readonly MethodInfo m_IsGoodStoreCellReplace = AccessTools.Method(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.IsGoodStoreCellReplace));

        public static readonly MethodInfo m_TryFindCastPosition = AccessTools.Method(typeof(CastPositionFinder), nameof(CastPositionFinder.TryFindCastPosition));

        public static readonly MethodInfo m_TryFindCastPositionOnVehicle = AccessTools.Method(typeof(CastPositionFinderOnVehicle), nameof(CastPositionFinderOnVehicle.TryFindCastPosition));

        public static readonly MethodInfo m_TargetCellOnBaseMap = AccessTools.Method(typeof(TargetMapManager), nameof(TargetMapManager.TargetCellOnBaseMap));

        public static readonly MethodInfo m_BreadthFirstTraverse = AccessTools.Method(typeof(RegionTraverser), nameof(RegionTraverser.BreadthFirstTraverse), new[] { typeof(Region), typeof(RegionEntryPredicate), typeof(RegionProcessor), typeof(int), typeof(RegionType) });

        public static readonly MethodInfo m_BreadthFirstTraverseAcrossMaps = AccessTools.Method(typeof(RegionTraverserAcrossMaps), nameof(RegionTraverserAcrossMaps.BreadthFirstTraverse), new[] { typeof(Region), typeof(RegionEntryPredicate), typeof(RegionProcessor), typeof(int), typeof(RegionType) });
    }
}
