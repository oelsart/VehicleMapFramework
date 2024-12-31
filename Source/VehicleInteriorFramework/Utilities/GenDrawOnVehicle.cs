using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public static class GenDrawOnVehicle
    {
        public static void DrawFieldEdges(List<IntVec3> cells, Map map)
        {
            GenDrawOnVehicle.DrawFieldEdges(cells, Color.white, null, map);
        }

        public static void DrawFieldEdges(List<IntVec3> cells, Color color, float? altOffset, Map map)
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
                BaseTexPath = "UI/Overlays/TargetHighlight_Side"
            });
            material.GetTexture("_MainTex").wrapMode = TextureWrapMode.Clamp;
            if (GenDrawOnVehicle.fieldGrid == null)
            {
                GenDrawOnVehicle.fieldGrid = new BoolGrid(map);
            }
            else
            {
                GenDrawOnVehicle.fieldGrid.ClearAndResizeTo(map);
            }
            int x = map.Size.x;
            int z = map.Size.z;
            int count = cells.Count;
            float y = altOffset ?? (Rand.ValueSeeded(color.ToOpaque().GetHashCode()) * 0.03846154f / 10f);
            for (int i = 0; i < count; i++)
            {
                if (cells[i].InBounds(map))
                {
                    GenDrawOnVehicle.fieldGrid[cells[i].x, cells[i].z] = true;
                }
            }
            var vehicleMap = map.IsVehicleMapOf(out var vehicle);
            for (int j = 0; j < count; j++)
            {
                IntVec3 intVec = cells[j];
                if (intVec.InBounds(map))
                {
                    GenDrawOnVehicle.rotNeeded[0] = (intVec.z < z - 1 && !GenDrawOnVehicle.fieldGrid[intVec.x, intVec.z + 1]) || !(intVec + IntVec3.North).InBounds(map);
                    GenDrawOnVehicle.rotNeeded[1] = (intVec.x < x - 1 && !GenDrawOnVehicle.fieldGrid[intVec.x + 1, intVec.z]) || !(intVec + IntVec3.East).InBounds(map);
                    GenDrawOnVehicle.rotNeeded[2] = (intVec.z > 0 && !GenDrawOnVehicle.fieldGrid[intVec.x, intVec.z - 1]) || !(intVec + IntVec3.South).InBounds(map);
                    GenDrawOnVehicle.rotNeeded[3] = (intVec.x > 0 && !GenDrawOnVehicle.fieldGrid[intVec.x - 1, intVec.z]) || !(intVec + IntVec3.West).InBounds(map);
                    for (int k = 0; k < 4; k++)
                    {
                        if (GenDrawOnVehicle.rotNeeded[k])
                        {
                            if (vehicleMap)
                            {
                                Graphics.DrawMesh(MeshPool.plane10, intVec.ToVector3Shifted().OrigToVehicleMap(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor()).WithYOffset(y), new Rot4(k).AsQuat * vehicle.FullRotation.AsQuat(), material, 0);
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
