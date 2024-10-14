using HarmonyLib;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Reflection;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class MethodInfoCache
    {
        public static readonly MethodInfo g_FocusedVehicle = AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedVehicle));

        public static readonly MethodInfo g_Find_CurrentMap = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));

        public static readonly MethodInfo g_VehicleMapUtility_CurrentMap = AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CurrentMap));

        public static readonly MethodInfo m_IsVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsVehicleMapOf));

        public static readonly MethodInfo m_IsOnVehicleMapOf = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.IsOnVehicleMapOf));

        public static readonly MethodInfo m_OrigToVehicleMap1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.OrigToVehicleMap), new Type[] { typeof(Vector3) });

        public static readonly MethodInfo m_OrigToVehicleMap2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.OrigToVehicleMap), new Type[] { typeof(Vector3), typeof(VehiclePawnWithInterior) });

        public static readonly MethodInfo m_VehicleMapToOrig1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.VehicleMapToOrig), new Type[] { typeof(Vector3) });

        public static readonly MethodInfo m_VehicleMapToOrig2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.VehicleMapToOrig), new Type[] { typeof(Vector3), typeof(VehiclePawnWithInterior) });

        public static readonly MethodInfo g_Thing_Map = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));

        public static readonly MethodInfo m_BaseMapOfThing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMapOfThing));

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

        public static readonly MethodInfo m_BaseFullRotationOfThing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotationOfThing));

        public static readonly MethodInfo g_Angle = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.Angle));

        public static readonly MethodInfo g_AsAngleRot8 = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsAngle));

        public static readonly MethodInfo m_BaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMap));
    }
}
