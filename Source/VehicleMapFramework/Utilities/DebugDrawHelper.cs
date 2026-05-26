using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

internal static class DebugDrawHelper
{
    private static readonly AccessTools.FieldRef<DebugCellDrawer, IList> debugCells
    = AccessTools.FieldRefAccess<DebugCellDrawer, IList>("debugCells");
    
    private static readonly Type t_DebugCell = GenTypes.GetTypeInAnyAssembly("Verse.DebugCell", "Verse");

    private static readonly Dictionary<string, DebugCellField> _fields = [];
    
    private static int lastCameraUpdateFrame = -1;

    private static Bounds bounds;

    public static void DebugDraw(DebugCellDrawer drawer, Map map)
    {
        var cells = debugCells(drawer);
        for (var i = 0; i < cells.Count; i++)
        {
            Draw(cells[i], map);
        }       
    }

    public static void DebugOnGUI(DebugCellDrawer drawer, Map map)
    {
        if (Find.CameraDriver.CurrentZoom == CameraZoomRange.Closest)
        {
            var cells = debugCells(drawer);
            if (cells.Count == 0) return;
            
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            for (var i = 0; i < cells.Count; i++)
            {
                OnGUI(cells[i], map);
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }

    private static void Draw(object debugCell, Map map)
    {
        var c = debugCell.GetFieldValue<IntVec3>("c");
        if (debugCell.GetFieldValue<Material>("customMat") is { } customMat)
        {
            RenderCell(c, customMat, map);
            return;
        }
        RenderCell(c, debugCell.GetFieldValue<float>("colorPct"), map);
    }
    
    private static void OnGUI(object debugCell, Map map)
    {
        if (debugCell.GetFieldValue<string>("displayString") is not { } displayString)
            return;
        
        var c = debugCell.GetFieldValue<IntVec3>("c");
        var vector = c.ToVector3Shifted();
        if (map.IsNonFocusedVehicleMapOf(out var vehicle))
            vector = vector.ToBaseMapCoord(vehicle);
        var vector2 = vector.MapToUIPosition();
        var rect = new Rect(vector2.x - 20f, vector2.y - 20f, 40f, 40f);
        if (new Rect(0f, 0f, UI.screenWidth, UI.screenHeight).Overlaps(rect))
        {
            Widgets.Label(rect, displayString);
        }
    }
    
    private static void InitFrame()
    {
        if (Time.frameCount != lastCameraUpdateFrame)
        {
            bounds = Find.CameraDriver.CurrentViewRect.ToBounds();
            lastCameraUpdateFrame = Time.frameCount;
        }
    }
    
    private static Material MatFromColorPct(float colorPct, bool transparent)
    {
        return DebugMatsSpectrum.Mat(GenMath.PositiveMod(Mathf.RoundToInt(colorPct * 100f), 100), transparent);
    }

    public static void RenderCell(IntVec3 c, float colorPct, Map map)
    {
        RenderCell(c, MatFromColorPct(colorPct, true), map);
    }
    
    public static void RenderCell(IntVec3 c, Material mat, Map map)
    {
        InitFrame();
        var vector = c.ToVector3Shifted();
        if (map.IsNonFocusedVehicleMapOf(out var vehicle))
            vector = vector.ToBaseMapCoord(vehicle);
        if (!bounds.Contains(vector.Yto0()))
            return;
        
        Graphics.DrawMesh(MeshPool.plane10, vector.SetToAltitude(AltitudeLayer.MetaOverlays), Quaternion.AngleAxis(vehicle.ExtraAngle, Vector3.up), mat, 0);
    }

    private static T GetFieldValue<T>(this object debugCell, string name)
    {
        if (!_fields.TryGetValue(name, out var field))
        {
            _fields[name] = field = DebugCellField.Create<T>(name);       
        }
        return ((DebugCellField<T>)field).Accessor(debugCell);
    }
    
    private abstract class DebugCellField
    {
        public static DebugCellField Create<T>(string name) => new DebugCellField<T>(name);
    }

    private class DebugCellField<T>(string name) : DebugCellField
    {
        public readonly AccessTools.FieldRef<object, T> Accessor = AccessTools.FieldRefAccess<T>(t_DebugCell, name);
    }
}