using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static VehicleMapFramework.ModCompat.SmartFarming;

namespace VehicleMapFramework;

public static class GenDrawOnVehicle
{
    private static BoolGrid fieldGrid;

    private static readonly bool[] rotNeeded = new bool[4];

    public static void DrawFieldEdges(List<IntVec3> cells, int renderQueue = 2900, Map map = null)
    {
        DrawFieldEdges(cells, Color.white, null, null, renderQueue, map);
    }

    public static void DrawFieldEdges(List<IntVec3> cells, Color color, float? altOffset = null, HashSet<IntVec3> ignoreBorderCells = null, int renderQueue = 2900, Map map = null)
    {
        if (map == null)
        {
            if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                map = Command_FocusVehicleMap.FocusedVehicle.VehicleMap;
            }
            else
            {
                GenDraw.DrawFieldEdges(cells, color, altOffset);
                return;
            }
        }

        var material = MaterialPool.MatFrom(new MaterialRequest
        {
            shader = ShaderDatabase.Transparent,
            color = color,
            BaseTexPath = "UI/Overlays/TargetHighlight_Side",
            renderQueue = renderQueue
        });
        material.GetTexture("_MainTex").wrapMode = TextureWrapMode.Clamp;
        if (fieldGrid == null)
        {
            fieldGrid = new BoolGrid(map);
        }
        else
        {
            fieldGrid.ClearAndResizeTo(map);
        }
        var x = map.Size.x;
        var z = map.Size.z;
        var count = cells.Count;
        var y = altOffset ?? (Rand.ValueSeeded(color.ToOpaque().GetHashCode()) * 0.03846154f / 10f);
        for (var i = 0; i < count; i++)
        {
            if (cells[i].InBounds(map))
            {
                fieldGrid[cells[i].x, cells[i].z] = true;
            }
        }
        var vehicleMap = map.IsVehicleMapOf(out var vehicle);
        for (var j = 0; j < count; j++)
        {
            var intVec = cells[j];
            if (intVec.InBounds(map))
            {
                rotNeeded[0] = intVec.z < z - 1 && !fieldGrid[intVec.x, intVec.z + 1] && !(ignoreBorderCells?.Contains(intVec + IntVec3.North) ?? false);
                rotNeeded[1] = intVec.x < x - 1 && !fieldGrid[intVec.x + 1, intVec.z] && !(ignoreBorderCells?.Contains(intVec + IntVec3.East) ?? false);
                rotNeeded[2] = intVec.z > 0 && !fieldGrid[intVec.x, intVec.z - 1] && !(ignoreBorderCells?.Contains(intVec + IntVec3.South) ?? false);
                rotNeeded[3] = intVec.x > 0 && !fieldGrid[intVec.x - 1, intVec.z] && !(ignoreBorderCells?.Contains(intVec + IntVec3.West) ?? false);
                for (var k = 0; k < 4; k++)
                {
                    if (rotNeeded[k])
                    {
                        if (vehicleMap)
                        {
                            Graphics.DrawMesh(MeshPool.plane10, intVec.ToVector3Shifted().ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor()).WithYOffset(y), new Rot4(k).AsQuat * vehicle.FullAngleQuat(), material, 0);
                        }
                        else
                        {
                            Graphics.DrawMesh(MeshPool.plane10, intVec.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays).WithYOffset(y), new Rot4(k).AsQuat, material, 0);
                        }
                    }
                }
            }
        }
    }

    public static void DrawFieldEdgesSF(List<IntVec3> cells, Zone zone, Map map)
    {
        if (zone is Zone_Growing gZone)
        {
            var component = map.GetComponent(MapComponent_SmartFarming);
            if (component is null)
            {
                DrawFieldEdges(cells, map: map);
                return;
            }
            var dict = growZoneRegistry(component);
            if (dict is null)
            {
                DrawFieldEdges(cells, map: map);
                return;
            }
            if (dict.Contains(gZone.ID))
            {
                DrawFieldEdges(cells, priority(dict[gZone.ID]) switch
                {
                    1 => Color.grey,
                    3 => Color.green,
                    4 => Color.yellow,
                    5 => Color.red,
                    _ => Color.white,
                }, map: map);
            }
        }
        DrawFieldEdges(cells, map: map);
    }

    public static void DrawFieldEdgesRG(List<IntVec3> cells, int renderQueue, Zone zone, Map map)
    {
        if (zone is Zone_Growing gZone)
        {
            var component = map.GetComponent(MapComponent_SmartFarming);
            if (component is null)
            {
                DrawFieldEdges(cells, renderQueue, map);
                return;
            }
            var dict = growZoneRegistry(component);
            if (dict is null)
            {
                DrawFieldEdges(cells, renderQueue, map);
                return;
            }
            if (dict.Contains(gZone.ID))
            {
                DrawFieldEdges(cells, priority(dict[gZone.ID]) switch
                {
                    1 => Color.grey,
                    3 => Color.green,
                    4 => Color.yellow,
                    5 => Color.red,
                    _ => Color.white,
                }, renderQueue: renderQueue, map: map);
            }
        }
        DrawFieldEdges(cells, renderQueue, map);
    }
}
