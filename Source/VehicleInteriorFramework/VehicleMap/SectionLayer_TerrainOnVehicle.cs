using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class SectionLayer_TerrainOnVehicle : SectionLayer
    {
        public override bool Visible
        {
            get
            {
                return DebugViewSettings.drawTerrain;
            }
        }

        public SectionLayer_TerrainOnVehicle(Section section) : base(section)
        {
            this.relevantChangeTypes = MapMeshFlagDefOf.Terrain;
            if (!base.Map.IsVehicleMapOf(out var vehicle))
            {
                Log.Error("[VehicleInteriors] Do not use SectionLayer_TerrainOnVehicle except for vehicle maps.");
                return;
            }
            this.baseTerrainMat = SolidColorMaterials.NewSolidColorMaterial(vehicle.DrawColor, ShaderDatabase.TerrainHard);
        }

        //drawPlanetがオフでVehicleMapにフォーカスした時しか呼ばれないよ
        public override void DrawLayer()
        {
            if (!base.Map.IsVehicleMapOf(out var vehicle))
            {
                Log.Error("[VehicleInteriors] Do not use SectionLayer_TerrainOnVehicle except for vehicle maps.");
                return;
            }
            var mapSize = new Vector3(vehicle.VehicleMap.Size.x, 0f, vehicle.VehicleMap.Size.z);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mapSize / 2f, Quaternion.identity, mapSize), this.baseTerrainMat, 0);
        }

        public virtual Material GetMaterialFor(CellTerrain cellTerrain)
        {
            var def = cellTerrain.def;
            var color = cellTerrain.color;
            bool polluted = cellTerrain.polluted && cellTerrain.snowCoverage < 0.4f && cellTerrain.def.graphicPolluted != BaseContent.BadGraphic;
            ValueTuple<TerrainDef, bool, ColorDef> key = new ValueTuple<TerrainDef, bool, ColorDef>(def, polluted, color);
            if (!this.terrainMatCache.ContainsKey(key))
            {
                Graphic graphic = polluted ? def.graphicPolluted.GetCopy(def.graphicPolluted.drawSize, VMF_Shaders.terrainHardWithZ) : def.graphic.GetCopy(def.graphic.drawSize, VMF_Shaders.terrainHardWithZ);
                if (color != null)
                {
                    this.terrainMatCache[key] = graphic.GetColoredVersion(VMF_Shaders.terrainHardWithZ, color.color, Color.white).MatSingle;
                }
                else
                {
                    this.terrainMatCache[key] = graphic.MatSingle;
                }
            }
            return this.terrainMatCache[key];
        }

        public bool AllowRenderingFor(TerrainDef terrain)
        {
            return DebugViewSettings.drawTerrainWater || !terrain.HasTag("Water");
        }

        public override void Regenerate()
        {
            base.ClearSubMeshes(MeshParts.All);
            TerrainGrid terrainGrid = base.Map.terrainGrid;
            CellRect cellRect = this.section.CellRect;
            CellTerrain[] array = new CellTerrain[8];
            HashSet<CellTerrain> hashSet = new HashSet<CellTerrain>();
            bool[] array2 = new bool[8];
            foreach (IntVec3 intVec in cellRect)
            {
                hashSet.Clear();
                CellTerrain cellTerrain = new CellTerrain(terrainGrid.TerrainAt(intVec), intVec.IsPolluted(base.Map), base.Map.snowGrid.GetDepth(intVec), terrainGrid.ColorAt(intVec));

                if (cellTerrain.def == VMF_DefOf.VMF_VehicleFloor) continue; //デフォルトのVehicleFloorの場合は描画しない

                LayerSubMesh subMesh = base.GetSubMesh(this.GetMaterialFor(cellTerrain));
                if (subMesh != null && this.AllowRenderingFor(cellTerrain.def))
                {
                    int count = subMesh.verts.Count;
                    subMesh.verts.Add(new Vector3((float)intVec.x, 0f, (float)intVec.z));
                    subMesh.verts.Add(new Vector3((float)intVec.x, 0f, (float)(intVec.z + 1)));
                    subMesh.verts.Add(new Vector3((float)(intVec.x + 1), 0f, (float)(intVec.z + 1)));
                    subMesh.verts.Add(new Vector3((float)(intVec.x + 1), 0f, (float)intVec.z));
                    subMesh.colors.Add(SectionLayer_TerrainOnVehicle.ColorWhite);
                    subMesh.colors.Add(SectionLayer_TerrainOnVehicle.ColorWhite);
                    subMesh.colors.Add(SectionLayer_TerrainOnVehicle.ColorWhite);
                    subMesh.colors.Add(SectionLayer_TerrainOnVehicle.ColorWhite);
                    subMesh.tris.Add(count);
                    subMesh.tris.Add(count + 1);
                    subMesh.tris.Add(count + 2);
                    subMesh.tris.Add(count);
                    subMesh.tris.Add(count + 2);
                    subMesh.tris.Add(count + 3);
                }
                for (int i = 0; i < 8; i++)
                {
                    IntVec3 c = intVec + GenAdj.AdjacentCellsAroundBottom[i];
                    if (!c.InBounds(base.Map))
                    {
                        array[i] = cellTerrain;
                    }
                    else
                    {
                        CellTerrain cellTerrain2 = new CellTerrain(terrainGrid.TerrainAt(c), c.IsPolluted(base.Map), base.Map.snowGrid.GetDepth(c), terrainGrid.ColorAt(c));
                        Thing edifice = c.GetEdifice(base.Map);
                        if (edifice != null && edifice.def.coversFloor)
                        {
                            cellTerrain2.def = TerrainDefOf.Underwall;
                        }
                        array[i] = cellTerrain2;
                        if (!cellTerrain2.Equals(cellTerrain) && cellTerrain2.def.edgeType != TerrainDef.TerrainEdgeType.Hard && cellTerrain2.def.renderPrecedence >= cellTerrain.def.renderPrecedence && !hashSet.Contains(cellTerrain2))
                        {
                            hashSet.Add(cellTerrain2);
                        }
                    }
                }
                foreach (CellTerrain cellTerrain3 in hashSet)
                {
                    LayerSubMesh subMesh2 = base.GetSubMesh(this.GetMaterialFor(cellTerrain3));
                    if (subMesh2 != null && this.AllowRenderingFor(cellTerrain3.def))
                    {
                        int count = subMesh2.verts.Count;
                        subMesh2.verts.Add(new Vector3((float)intVec.x + 0.5f, 0f, (float)intVec.z));
                        subMesh2.verts.Add(new Vector3((float)intVec.x, 0f, (float)intVec.z));
                        subMesh2.verts.Add(new Vector3((float)intVec.x, 0f, (float)intVec.z + 0.5f));
                        subMesh2.verts.Add(new Vector3((float)intVec.x, 0f, (float)(intVec.z + 1)));
                        subMesh2.verts.Add(new Vector3((float)intVec.x + 0.5f, 0f, (float)(intVec.z + 1)));
                        subMesh2.verts.Add(new Vector3((float)(intVec.x + 1), 0f, (float)(intVec.z + 1)));
                        subMesh2.verts.Add(new Vector3((float)(intVec.x + 1), 0f, (float)intVec.z + 0.5f));
                        subMesh2.verts.Add(new Vector3((float)(intVec.x + 1), 0f, (float)intVec.z));
                        subMesh2.verts.Add(new Vector3((float)intVec.x + 0.5f, 0f, (float)intVec.z + 0.5f));
                        for (int j = 0; j < 8; j++)
                        {
                            array2[j] = false;
                        }
                        for (int k = 0; k < 8; k++)
                        {
                            if (k % 2 == 0)
                            {
                                if (array[k].Equals(cellTerrain3))
                                {
                                    array2[(k - 1 + 8) % 8] = true;
                                    array2[k] = true;
                                    array2[(k + 1) % 8] = true;
                                }
                            }
                            else if (array[k].Equals(cellTerrain3))
                            {
                                array2[k] = true;
                            }
                        }
                        for (int l = 0; l < 8; l++)
                        {
                            if (array2[l])
                            {
                                subMesh2.colors.Add(SectionLayer_TerrainOnVehicle.ColorWhite);
                            }
                            else
                            {
                                subMesh2.colors.Add(SectionLayer_TerrainOnVehicle.ColorClear);
                            }
                        }
                        subMesh2.colors.Add(SectionLayer_TerrainOnVehicle.ColorClear);
                        for (int m = 0; m < 8; m++)
                        {
                            subMesh2.tris.Add(count + m);
                            subMesh2.tris.Add(count + (m + 1) % 8);
                            subMesh2.tris.Add(count + 8);
                        }
                    }
                }
            }
            base.FinalizeMesh(MeshParts.All);
        }

        private readonly Material baseTerrainMat;

        private static readonly Color32 ColorWhite = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

        private static readonly Color32 ColorClear = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0);

        private readonly Dictionary<ValueTuple<TerrainDef, bool, ColorDef>, Material> terrainMatCache = new Dictionary<ValueTuple<TerrainDef, bool, ColorDef>, Material>();

        public const float MaxSnowCoverageForVisualPollution = 0.4f;
    }
}
