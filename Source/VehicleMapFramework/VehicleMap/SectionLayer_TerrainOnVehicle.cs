using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class SectionLayer_TerrainOnVehicle(Section section) : SectionLayer_Terrain(section)
{

  private readonly Dictionary<Material, Material> terrainMatCache = [];

  private Material baseTerrainMat;

  public void DrawLayer(Vector3 drawPos)
  {
    if (!Visible || !Map.IsVehicleMapOf(out var vehicle))
      return;

    var rot = Quaternion.AngleAxis(vehicle.FullAngle, Vector3.up);
    for (var i = 0; i < subMeshes.Count; i++)
    {
      var subMesh = subMeshes[i];
      if (subMesh.finalized && !subMesh.disabled && subMesh.material != MatBases.ShadowMask)
      {
        Graphics.DrawMesh(subMesh.mesh, drawPos, rot, subMesh.material, subMesh.renderLayer);
      }
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
}
