using SmashTools;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class SectionLayer_TerrainOnVehicle : SectionLayer_Terrain
{
    public SectionLayer_TerrainOnVehicle(Section section) : base(section)
    {
        if (Map.Parent is MapParent_Vehicle parentVehicle)
        {
            baseTerrainMat = SolidColorMaterials.NewSolidColorMaterial(parentVehicle.vehicle.DrawColor, ShaderDatabase.TerrainHard);
        }
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
        //if (!Map.IsVehicleMapOf(out var vehicle))
        //{
        //    //VMF_Log.Error("Do not use SectionLayer_TerrainOnVehicle except for vehicle maps.");
        //    return;
        //}
        //var mapSize = new Vector3(vehicle.VehicleMap.Size.x, 0f, vehicle.VehicleMap.Size.z);
        //Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mapSize / 2f, Quaternion.identity, mapSize), baseTerrainMat, 0);
    }

    public override Material GetMaterialFor(CellTerrain cellTerrain)
    {
        var mat = base.GetMaterialFor(cellTerrain);
        if (!terrainMatCache.TryGetValue(mat, out var mat2))
        {
            var newMat = new Material(mat);
            newMat.shader = VMF_DefOf.VMF_TerrainHardWithZ.Shader;
            mat2 = terrainMatCache[mat] = newMat;
        }
        return mat2;
    }

    public override void Regenerate()
    {
        if (!Map.IsVehicleMapOf(out _))
        {
            return;
        }
        base.Regenerate();
    }

    private readonly Material baseTerrainMat;

    private readonly Dictionary<Material, Material> terrainMatCache = [];
}
