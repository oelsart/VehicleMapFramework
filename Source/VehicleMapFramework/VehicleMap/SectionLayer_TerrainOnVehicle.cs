using System.Collections.Generic;
using System.Linq;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class SectionLayer_TerrainOnVehicle(Section section) : SectionLayer_Terrain(section)
{
    public void DrawLayer(Rot8 rot, Vector3 drawPos, float extraRotation)
    {
        if (!Visible)
        {
            return;
        }
        var angle = Ext_Math.RotateAngle(rot.AsAngle, extraRotation);
        foreach (var layerSubMesh in subMeshes.Where(layerSubMesh => layerSubMesh.finalized &&
                                                                     !layerSubMesh.disabled &&
                                                                     layerSubMesh.material != MatBases.ShadowMask))
        {
            Graphics.DrawMesh(layerSubMesh.mesh, drawPos, Quaternion.AngleAxis(angle, Vector3.up), layerSubMesh.material, 0);
        }
    }

    //drawPlanetがオフでVehicleMapにフォーカスした時しか呼ばれないよ
    public override void DrawLayer()
    {
        if (!Map.IsVehicleMapOf(out var vehicle))
        {
            //VMF_Log.Error("Do not use SectionLayer_TerrainOnVehicle except for vehicle maps.");
            return;
        }
        var mapSize = new Vector3(vehicle.VehicleMap.Size.x, 0f, vehicle.VehicleMap.Size.z);
        Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mapSize / 2f, Quaternion.identity, mapSize), baseTerrainMat, 0);
    }

    public override Material GetMaterialFor(CellTerrain cellTerrain)
    {
        var mat = base.GetMaterialFor(cellTerrain);
        if (!terrainMatCache.TryGetValue(mat, out var mat2))
        {
            var newMat = new Material(mat)
            {
                shader = VMF_DefOf.VMF_TerrainHardWithZ.Shader
            };
            mat2 = terrainMatCache[mat] = newMat;
        }
        return mat2;
    }

    public override void Regenerate()
    {
        if (!Map.IsVehicleMapOf(out var vehicle))
            return;
        
        baseTerrainMat = SolidColorMaterials.NewSolidColorMaterial(vehicle.DrawColor, ShaderDatabase.TerrainHard);
        base.Regenerate();
    }

    private Material baseTerrainMat;

    private readonly Dictionary<Material, Material> terrainMatCache = [];
}
