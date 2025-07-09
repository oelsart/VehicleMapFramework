﻿using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

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
        relevantChangeTypes = MapMeshFlagDefOf.Terrain;
        if (base.Map.Parent is not MapParent_Vehicle parentVehicle)
        {
            VMF_Log.Error("Do not use SectionLayer_TerrainOnVehicle except for vehicle maps.");
            return;
        }
        baseTerrainMat = SolidColorMaterials.NewSolidColorMaterial(parentVehicle.vehicle.DrawColor, ShaderDatabase.TerrainHard);
    }

    public void DrawLayer(Rot8 rot, Vector3 drawPos, float extraRotation)
    {
        if (!Visible)
        {
            return;
        }
        var angle = Ext_Math.RotateAngle(rot.AsAngle, extraRotation);
        foreach (var layerSubMesh in subMeshes)
        {
            if (layerSubMesh.finalized && !layerSubMesh.disabled)
            {
                Graphics.DrawMesh(layerSubMesh.mesh, drawPos, Quaternion.AngleAxis(angle, Vector3.up), layerSubMesh.material, 0);
            }
        }
    }

    //drawPlanetがオフでVehicleMapにフォーカスした時しか呼ばれないよ
    public override void DrawLayer()
    {
        if (!base.Map.IsVehicleMapOf(out var vehicle))
        {
            VMF_Log.Error("Do not use SectionLayer_TerrainOnVehicle except for vehicle maps.");
            return;
        }
        var mapSize = new Vector3(vehicle.VehicleMap.Size.x, 0f, vehicle.VehicleMap.Size.z);
        Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mapSize / 2f, Quaternion.identity, mapSize), baseTerrainMat, 0);
    }

    public virtual Material GetMaterialFor(CellTerrain cellTerrain)
    {
        var def = cellTerrain.def;
        var color = cellTerrain.color;
        bool polluted = cellTerrain.polluted && cellTerrain.snowCoverage < 0.4f && cellTerrain.def.graphicPolluted != BaseContent.BadGraphic;
        ValueTuple<TerrainDef, bool, ColorDef> key = new(def, polluted, color);
        if (!terrainMatCache.ContainsKey(key))
        {
            Graphic graphic = polluted ? def.graphicPolluted.GetCopy(def.graphicPolluted.drawSize, VMF_DefOf.VMF_TerrainHardWithZ.Shader) : def.graphic.GetCopy(def.graphic.drawSize, VMF_DefOf.VMF_TerrainHardWithZ.Shader);
            if (color != null)
            {
                terrainMatCache[key] = new Material(graphic.GetColoredVersion(VMF_DefOf.VMF_TerrainHardWithZ.Shader, color.color, Color.white).MatSingle);
            }
            else
            {
                terrainMatCache[key] = new Material(graphic.MatSingle);
            }
        }
        return terrainMatCache[key];
    }

    public bool AllowRenderingFor(TerrainDef terrain)
    {
        return DebugViewSettings.drawTerrainWater || !terrain.HasTag("Water");
    }

    public override void Regenerate()
    {
        base.ClearSubMeshes(MeshParts.All);
        TerrainGrid terrainGrid = base.Map.terrainGrid;
        CellRect cellRect = section.CellRect;
        CellTerrain[] array = new CellTerrain[8];
        HashSet<CellTerrain> hashSet = [];
        bool[] array2 = new bool[8];
        foreach (IntVec3 intVec in cellRect)
        {
            hashSet.Clear();
            CellTerrain cellTerrain = new(terrainGrid.TerrainAt(intVec), intVec.IsPolluted(base.Map), base.Map.snowGrid.GetDepth(intVec), intVec.GetSandDepth(Map), terrainGrid.ColorAt(intVec));

            if (cellTerrain.def == VMF_DefOf.VMF_VehicleFloor) continue; //デフォルトのVehicleFloorの場合は描画しない

            LayerSubMesh subMesh = base.GetSubMesh(GetMaterialFor(cellTerrain));
            if (subMesh != null && AllowRenderingFor(cellTerrain.def))
            {
                int count = subMesh.verts.Count;
                subMesh.verts.Add(new Vector3(intVec.x, 0f, intVec.z));
                subMesh.verts.Add(new Vector3(intVec.x, 0f, intVec.z + 1));
                subMesh.verts.Add(new Vector3(intVec.x + 1, 0f, intVec.z + 1));
                subMesh.verts.Add(new Vector3(intVec.x + 1, 0f, intVec.z));
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
                    CellTerrain cellTerrain2 = new(terrainGrid.TerrainAt(c), c.IsPolluted(base.Map), base.Map.snowGrid.GetDepth(c), c.GetSandDepth(Map), terrainGrid.ColorAt(c));
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
                LayerSubMesh subMesh2 = base.GetSubMesh(GetMaterialFor(cellTerrain3));
                if (subMesh2 != null && AllowRenderingFor(cellTerrain3.def))
                {
                    int count = subMesh2.verts.Count;
                    subMesh2.verts.Add(new Vector3(intVec.x + 0.5f, 0f, intVec.z));
                    subMesh2.verts.Add(new Vector3(intVec.x, 0f, intVec.z));
                    subMesh2.verts.Add(new Vector3(intVec.x, 0f, intVec.z + 0.5f));
                    subMesh2.verts.Add(new Vector3(intVec.x, 0f, intVec.z + 1));
                    subMesh2.verts.Add(new Vector3(intVec.x + 0.5f, 0f, intVec.z + 1));
                    subMesh2.verts.Add(new Vector3(intVec.x + 1, 0f, intVec.z + 1));
                    subMesh2.verts.Add(new Vector3(intVec.x + 1, 0f, intVec.z + 0.5f));
                    subMesh2.verts.Add(new Vector3(intVec.x + 1, 0f, intVec.z));
                    subMesh2.verts.Add(new Vector3(intVec.x + 0.5f, 0f, intVec.z + 0.5f));
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
                        subMesh2.tris.Add(count + ((m + 1) % 8));
                        subMesh2.tris.Add(count + 8);
                    }
                }
            }
        }
        base.FinalizeMesh(MeshParts.All);
    }

    private readonly Material baseTerrainMat;

    private static readonly Color32 ColorWhite = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    private static readonly Color32 ColorClear = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0);

    private readonly Dictionary<ValueTuple<TerrainDef, bool, ColorDef>, Material> terrainMatCache = [];

    public const float MaxSnowCoverageForVisualPollution = 0.4f;
}
