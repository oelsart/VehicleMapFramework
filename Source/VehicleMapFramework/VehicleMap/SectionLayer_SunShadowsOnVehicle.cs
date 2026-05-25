using System;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

/// <summary>
/// バッチ処理によりSunShadowシェーダーのGlobalVectorの扱いが変わるのを防ぐため予めVertsだけを回転しておくSectionLayer_SunShadowsのラッパー
/// </summary>
public class SectionLayer_SunShadowsOnVehicle : SectionLayer
{

  internal static readonly Type t_SectionLayer_SunShadows =
    GenTypes.GetTypeInAnyAssembly("Verse.SectionLayer_SunShadows", "Verse");

  private static readonly Color32 LowVertexColor = new(0, 0, 0, 0);

  private readonly SectionLayer subLayer;

  public SectionLayer_SunShadowsOnVehicle(Section section) : base(section)
  {
    subLayer = (SectionLayer)Activator.CreateInstance(t_SectionLayer_SunShadows, section);
    relevantChangeTypes = subLayer.relevantChangeTypes;
  }

  public void DrawLayer(Vector3 drawPos, float extraRotation)
  {
    if (!Visible)
    {
      return;
    }

    var rot = Quaternion.AngleAxis(extraRotation, Vector3.up);
    for (var i = 0; i < subLayer.subMeshes.Count; i++)
    {
      var layerSubMesh = subLayer.subMeshes[i];
      if (layerSubMesh.finalized && !layerSubMesh.disabled)
      {
        Graphics.DrawMesh(layerSubMesh.mesh, drawPos, rot, layerSubMesh.material, layerSubMesh.renderLayer);
      }
    }
  }

  public override void Regenerate()
  {
    if (!Map.IsVehicleMapOf(out _))
      return;

    subLayer.Regenerate();
    var subMesh = subLayer.GetSubMesh(MatBases.SunShadow);
    if (VehicleSectionLayerManager.RotForPrint != Rot4.North)
    {
      CastShadowNorth(subMesh);
      for (var i = 0; i < subMesh.verts.Count; i++)
      {
        subMesh.verts[i] = subMesh.verts[i].RotatedBy(VehicleSectionLayerManager.RotForPrint);
      }
    }
    VehicleSectionLayerManager.FinalizeShadowVerts(subLayer);
    subMesh.mesh.SetTriangles(subMesh.tris, 0);
    subMesh.mesh.SetColors(subMesh.colors);
  }

  private void CastShadowNorth(LayerSubMesh subMesh)
  {
    if (!MatBases.SunShadow.shader.isSupported)
      return;
    var innerArray = Map.edificeGrid.InnerArray;
    var num = AltitudeLayer.Shadows.AltitudeFor();
    var cellRect = section.CellRect;
    var cellIndices = Map.cellIndices;
    for (var i = cellRect.minX; i <= cellRect.maxX; i++)
    {
      for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
      {
        var building = innerArray[cellIndices.CellToIndex(i, j)];
        if (building?.def.staticSunShadowHeight > 0f)
        {
          var staticSunShadowHeight = building.def.staticSunShadowHeight;
          var color = new Color32(0, 0, 0, (byte)(255f * staticSunShadowHeight));
          if (j < Map.Size.z - 1)
          {
            building = innerArray[cellIndices.CellToIndex(i, j + 1)];
            if (building is null || building.def.staticSunShadowHeight < staticSunShadowHeight)
            {
              var count = subMesh.verts.Count;
              subMesh.verts.Add(new Vector3(i, num, j + 1));
              subMesh.verts.Add(new Vector3(i + 1, num, j + 1));
              subMesh.verts.Add(new Vector3(i, num, j + 1));
              subMesh.verts.Add(new Vector3(i + 1, num, j + 1));
              subMesh.colors.Add(LowVertexColor);
              subMesh.colors.Add(LowVertexColor);
              subMesh.colors.Add(color);
              subMesh.colors.Add(color);
              subMesh.tris.Add(count);
              subMesh.tris.Add(count + 2);
              subMesh.tris.Add(count + 1);

              subMesh.tris.Add(count + 1);
              subMesh.tris.Add(count + 2);
              subMesh.tris.Add(count + 3);
            }
          }
        }
      }
    }
  }
}
