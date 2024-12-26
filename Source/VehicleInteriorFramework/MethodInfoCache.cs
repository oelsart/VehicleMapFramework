using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VehicleInteriors.VIF_HarmonyPatches;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class MethodInfoCache
    {
        public static readonly MethodInfo g_FocusedVehicle = AccessTools.PropertyGetter(typeof(Command_FocusVehicleMap), nameof(Command_FocusVehicleMap.FocusedVehicle));

        public static readonly MethodInfo g_Find_CurrentMap = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));

        public static readonly MethodInfo g_VehicleMapUtility_CurrentMap = AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CurrentMap));

        public static readonly MethodInfo m_IsVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsVehicleMapOf));

        public static readonly MethodInfo m_IsOnVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsOnVehicleMapOf));

        public static readonly MethodInfo m_IsOnNonFocusedVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsOnNonFocusedVehicleMapOf));

        public static readonly MethodInfo m_OrigToVehicleMap1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.OrigToVehicleMap), new Type[] { typeof(Vector3) });

        public static readonly MethodInfo m_OrigToVehicleMap2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.OrigToVehicleMap), new Type[] { typeof(Vector3), typeof(VehiclePawnWithMap) });

        public static readonly MethodInfo m_VehicleMapToOrig1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.VehicleMapToOrig), new Type[] { typeof(Vector3) });

        public static readonly MethodInfo m_VehicleMapToOrig2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.VehicleMapToOrig), new Type[] { typeof(Vector3), typeof(VehiclePawnWithMap) });

        public static readonly MethodInfo g_Thing_Map = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));

        public static readonly MethodInfo m_BaseMap_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), new Type[] { typeof(Thing) });

        public static readonly MethodInfo g_Zone_Map = AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.Map));

        public static readonly MethodInfo m_BaseMap_Zone = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), new Type[] { typeof(Zone) });

        public static readonly MethodInfo g_Thing_MapHeld = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MapHeld));

        public static readonly MethodInfo m_MapHeldBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.MapHeldBaseMap));

        public static readonly MethodInfo g_Thing_Position = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));

        public static readonly MethodInfo m_PositionOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionOnBaseMap), new Type[] { typeof(Thing) });

        public static readonly MethodInfo g_LocalTargetInfo_Cell = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.Cell));

        public static readonly MethodInfo m_CellOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap));

        public static readonly MethodInfo m_OccupiedRect = AccessTools.Method(typeof(GenAdj), nameof(GenAdj.OccupiedRect), new Type[] { typeof(Thing) });

        public static readonly MethodInfo m_MovedOccupiedRect = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.MovedOccupiedRect));

        public static readonly MethodInfo m_ToTargetInfo = AccessTools.Method(typeof(LocalTargetInfo), nameof(LocalTargetInfo.ToTargetInfo));

        public static readonly MethodInfo m_ToBaseMapTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapTargetInfo));

        public static readonly MethodInfo g_FullRotation = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.FullRotation));

        public static readonly MethodInfo m_BaseFullRotation_Thing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotation), new Type[] { typeof(Thing) });

        public static readonly MethodInfo m_BaseFullRotation_Vehicle = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotation), new Type[] { typeof(VehiclePawn) });

        public static readonly MethodInfo g_Angle = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.Angle));

        public static readonly MethodInfo g_AsAngleRot8 = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsAngle));

        public static readonly MethodInfo m_BaseMap_Map = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap), new Type[] { typeof(Map) });

        public static readonly MethodInfo m_RotatePoint = AccessTools.Method(typeof(Ext_Math), nameof(Ext_Math.RotatePoint));

        public static readonly MethodInfo g_Thing_Spawned = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Spawned));

        public static readonly MethodInfo g_Rot4_AsQuat = AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsQuat));

        public static readonly MethodInfo m_Rot8_AsQuat = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsQuat), new Type[] { typeof(Rot8) });

        public static readonly MethodInfo m_Rot8_AsQuatRef = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsQuat), new Type[] { typeof(Rot8).MakeByRefType() });

        public static readonly MethodInfo m_Rot4_Rotate = AccessTools.Method(typeof(Rot4), nameof(Rot4.Rotate));

        public static readonly MethodInfo m_Rot8_Rotate = AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.Rotate));

        public static readonly MethodInfo o_Quaternion_Multiply = AccessTools.Method(typeof(Quaternion), "op_Multiply", new Type[] { typeof(Quaternion), typeof(Quaternion) });

        public static readonly MethodInfo m_GenDraw_DrawFieldEdges = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawFieldEdges), new Type[] { typeof(List<IntVec3>) });

        public static readonly MethodInfo m_GenDrawOnVehicle_DrawFieldEdges = AccessTools.Method(typeof(GenDrawOnVehicle), nameof(GenDrawOnVehicle.DrawFieldEdges), new Type[] { typeof(List<IntVec3>), typeof(Map) });

        public static readonly MethodInfo g_Designator_Map = AccessTools.PropertyGetter(typeof(Designator), nameof(Designator.Map));

        public static readonly MethodInfo g_Thing_Rotation = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Rotation));

        public static readonly MethodInfo m_Thing_RotationOrig = AccessTools.Method(typeof(Patch_Thing_Rotation), nameof(Patch_Thing_Rotation.Rotation));

        public static readonly MethodInfo g_Thing_DrawPos = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.DrawPos));

        public static readonly MethodInfo m_DrawPosOrig = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.DrawPosOrig));

        public static readonly MethodInfo m_GenThing_TrueCenter = AccessTools.Method(typeof(GenThing), nameof(GenThing.TrueCenter), new Type[] { typeof(Thing) });

        public static readonly MethodInfo m_TrueCenterOrig = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.TrueCenterOrig));

        public static readonly MethodInfo m_RotateForPrintNegate = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.RotateForPrintNegate));

        public static readonly MethodInfo m_ShouldLinkWith = AccessTools.Method(typeof(Graphic_Linked), nameof(Graphic_Linked.ShouldLinkWith));

        public static readonly MethodInfo m_ShouldLinkWithOrig = AccessTools.Method(typeof(Patch_Graphic_Linked_ShouldLinkWith), nameof(Patch_Graphic_Linked_ShouldLinkWith.ShouldLinkWith));

        public static readonly MethodInfo m_ForbidUtility_IsForbidden = AccessTools.Method(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), new Type[] { typeof(Thing), typeof(Pawn) });

        public static readonly MethodInfo m_ReservationAcrossMapsUtility_IsForbidden = AccessTools.Method(typeof(ReservationAcrossMapsUtility), nameof(ReservationAcrossMapsUtility.IsForbidden));

        public static readonly MethodInfo m_GenSight_LineOfSightToThing = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSightToThing));

        public static readonly MethodInfo m_GenSightOnVehicle_LineOfSightToThing = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSightToThing));

        public static readonly MethodInfo m_GenSight_LineOfSight1 = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSight), new Type[] { typeof(IntVec3), typeof(IntVec3), typeof(Map) });

        public static readonly MethodInfo m_GenSight_LineOfSight2 = AccessTools.Method(typeof(GenSight), nameof(GenSight.LineOfSight), new Type[] { typeof(IntVec3), typeof(IntVec3), typeof(Map), typeof(bool), typeof(Func<IntVec3, bool>), typeof(int), typeof(int) });

        public static readonly MethodInfo m_GenSightOnVehicle_LineOfSight1 = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSight), new Type[] { typeof(IntVec3), typeof(IntVec3), typeof(Map) });

        public static readonly MethodInfo m_GenSightOnVehicle_LineOfSight2 = AccessTools.Method(typeof(GenSightOnVehicle), nameof(GenSightOnVehicle.LineOfSight), new Type[] { typeof(IntVec3), typeof(IntVec3), typeof(Map), typeof(bool), typeof(Func<IntVec3, bool>), typeof(int), typeof(int) });

        public static readonly MethodInfo m_GenUI_TargetsAtMouse = AccessTools.Method(typeof(GenUI), nameof(GenUI.TargetsAtMouse));

        public static readonly MethodInfo m_GenUIOnVehicle_TargetsAtMouse = AccessTools.Method(typeof(GenUIOnVehicle), nameof(GenUIOnVehicle.TargetsAtMouse));
    }
}
