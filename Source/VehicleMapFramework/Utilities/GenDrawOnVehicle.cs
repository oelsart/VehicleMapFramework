using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using static VehicleMapFramework.ModCompat.SmartFarming;

namespace VehicleMapFramework;

public static class GenDrawOnVehicle
{
    private static BoolGrid fieldGrid;

    private static readonly bool[] rotNeeded = new bool[4];

    private static readonly List<Matrix4x4> matrixList = [];

    public static void DrawFieldEdges(List<IntVec3> cells, int renderQueue = 2900, Map map = null)
    {
        DrawFieldEdges(cells, Color.white, null, null, renderQueue, map);
    }

    public static void DrawFieldEdges(List<IntVec3> cells, Color color, float? altOffset = null, HashSet<IntVec3> ignoreBorderCells = null, int renderQueue = 2900, Map map = null)
    {
        const int x = 200;
        const int z = 200;
        
        if (map is null)
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

        if (!map.IsNonFocusedVehicleMapOf(out var vehicle))
        {
            GenDraw.DrawFieldEdges(cells, color, altOffset);
            return;
        }

        var material = MaterialPool.MatFrom(new MaterialRequest
        {
            shader = ShaderDatabase.Transparent,
            color = color,
            BaseTexPath = "UI/Overlays/TargetHighlight_Side",
            renderQueue = renderQueue
        });
        material.GetTexture(AdditionalShaderPropertyIDs.MainTex).wrapMode = TextureWrapMode.Clamp;
        if (fieldGrid == null)
        {
            fieldGrid = new BoolGrid(x, z);
        }
        else
        {
            fieldGrid.ClearAndResizeTo(x, z);
        }
        var count = cells.Count;
        var y = altOffset ?? (Rand.ValueSeeded(color.ToOpaque().GetHashCode()) * 0.03846154f / 10f);
        var offset = new IntVec3(x / 2, 0, z / 2);
        for (var i = 0; i < count; i++)
        {
            var intVec = cells[i] + offset;
            if (InBounds(intVec))
            {
                fieldGrid[intVec.x, intVec.z] = true;
            }
        }
        for (var j = 0; j < count; j++)
        {
            var intVec = cells[j] + offset;
            if (InBounds(intVec))
            {
                rotNeeded[0] = intVec.z < z - 1 && !fieldGrid[intVec.x, intVec.z + 1] && !(ignoreBorderCells?.Contains(intVec + IntVec3.North) ?? false);
                rotNeeded[1] = intVec.x < x - 1 && !fieldGrid[intVec.x + 1, intVec.z] && !(ignoreBorderCells?.Contains(intVec + IntVec3.East) ?? false);
                rotNeeded[2] = intVec.z > 0 && !fieldGrid[intVec.x, intVec.z - 1] && !(ignoreBorderCells?.Contains(intVec + IntVec3.South) ?? false);
                rotNeeded[3] = intVec.x > 0 && !fieldGrid[intVec.x - 1, intVec.z] && !(ignoreBorderCells?.Contains(intVec + IntVec3.West) ?? false);
                for (var k = 0; k < 4; k++)
                {
                    if (rotNeeded[k])
                    {
                        Graphics.DrawMesh(MeshPool.plane10, (intVec - offset).ToVector3Shifted().ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor()).WithYOffset(y), new Rot4(k).AsQuat * vehicle.FullAngleQuat, material, 0);
                    }
                }
            }
        }
        return;

        bool InBounds(IntVec3 c) => (ulong)c.x < 200 && (ulong)c.z < 200;
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
    
    public static void DrawLineBetweenInstanced(Vector3 A, Vector3 B, Material mat, float lineWidth = 0.2f)
    {
        if (Mathf.Abs(A.x - B.x) < 0.01f && Mathf.Abs(A.z - B.z) < 0.01f)
        {
            return;
        }

        if (!mat.enableInstancing)
        {
            GenDraw.DrawLineBetween(A, B, mat, lineWidth);
            return;
        }

        A.y = B.y;
        var distance = (B - A).MagnitudeHorizontal();
        var matCount = Mathf.CeilToInt(distance / lineWidth);
        var scale = new Vector3(lineWidth, 1f, distance / matCount);
        var offset = (B - A) / matCount;
        var firstPosition = A + offset * 0.5f;
        var quaternion = Quaternion.LookRotation(B - A);
        
        matrixList.Clear();
        for (var i = 0; i < matCount; i++)
        {
            matrixList.Add(Matrix4x4.TRS(firstPosition + offset * i, quaternion, scale));
        }

        Graphics.DrawMeshInstanced(MeshPool.plane10, 0, mat, matrixList);
    }
}
