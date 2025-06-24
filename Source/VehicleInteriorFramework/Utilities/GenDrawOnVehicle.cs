using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public static class GenDrawOnVehicle
    {
        public static void DrawFieldEdges(List<IntVec3> cells, int renderQueue = 2900, Map map = null)
        {
            DrawFieldEdges(cells, Color.white, null, null, renderQueue, map);
        }

        public static void DrawFieldEdges(List<IntVec3> cells, Color color, float? altOffset, HashSet<IntVec3> ignoreBorderCells = null, int renderQueue = 2900, Map map = null)
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

            Material material = MaterialPool.MatFrom(new MaterialRequest
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
            int x = map.Size.x;
            int z = map.Size.z;
            int count = cells.Count;
            float y = altOffset ?? (Rand.ValueSeeded(color.ToOpaque().GetHashCode()) * 0.03846154f / 10f);
            for (int i = 0; i < count; i++)
            {
                if (cells[i].InBounds(map))
                {
                    fieldGrid[cells[i].x, cells[i].z] = true;
                }
            }
            var vehicleMap = map.IsVehicleMapOf(out var vehicle);
            for (int j = 0; j < count; j++)
            {
                IntVec3 intVec = cells[j];
                if (intVec.InBounds(map))
                {
                    rotNeeded[0] = intVec.z < z - 1 && !fieldGrid[intVec.x, intVec.z + 1] && !(ignoreBorderCells?.Contains(intVec + IntVec3.North) ?? false);
                    rotNeeded[1] = intVec.x < x - 1 && !fieldGrid[intVec.x + 1, intVec.z] && !(ignoreBorderCells?.Contains(intVec + IntVec3.East) ?? false);
                    rotNeeded[2] = intVec.z > 0 && !fieldGrid[intVec.x, intVec.z - 1] && !(ignoreBorderCells?.Contains(intVec + IntVec3.South) ?? false);
                    rotNeeded[3] = intVec.x > 0 && !fieldGrid[intVec.x - 1, intVec.z] && !(ignoreBorderCells?.Contains(intVec + IntVec3.West) ?? false);
                    for (int k = 0; k < 4; k++)
                    {
                        if (rotNeeded[k])
                        {
                            if (vehicleMap)
                            {
                                Graphics.DrawMesh(MeshPool.plane10, intVec.ToVector3Shifted().ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor()).WithYOffset(y), new Rot4(k).AsQuat * vehicle.FullRotation.AsQuat(), material, 0);
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

        private static BoolGrid fieldGrid;

        private static bool[] rotNeeded = new bool[4];
    }
}
