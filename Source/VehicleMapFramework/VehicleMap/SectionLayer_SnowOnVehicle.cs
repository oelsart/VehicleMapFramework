using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class SectionLayer_SnowOnVehicle(Section section) : SectionLayer_Snow(section)
{
	private readonly float[] adjValuesTmp = new float[9];
	private static readonly List<float> opacityListTmp = [];
	private static readonly CachedTexture PollutedSnowTex = new("Other/SnowPolluted");
  private static Material SnowMat;
  private static readonly Func<SnowGrid, NativeArray<float>> DepthGrid_Unsafe =
    AccessTools.MethodDelegate<Func<SnowGrid, NativeArray<float>>>(
      AccessTools.PropertyGetter(typeof(SnowGrid), "DepthGrid_Unsafe"));

  static SectionLayer_SnowOnVehicle()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      SnowMat = MaterialPool.MatFrom("Other/Snow", VMF_DefOf.VMF_SnowWithZ.Shader);
      SnowMat.SetTexture(Shader.PropertyToID("_MacroTex"), ContentFinder<Texture2D>.Get("Other/SnowMacro"));
      SnowMat.SetTexture(Shader.PropertyToID("_AlphaAddTex"), TexGame.AlphaAddTex);
    });
  }
  
  public void DrawLayer(Vector3 drawPos)
  {
    if (!Visible || !Map.IsVehicleMapOf(out var vehicle))
      return;
        
    var rot = Quaternion.AngleAxis(vehicle.FullAngle, Vector3.up);
    for (var i = 0; i < subMeshes.Count; i++)
    {
      var subMesh = subMeshes[i];
      if (subMesh.finalized && !subMesh.disabled)
      {
        Graphics.DrawMesh(subMesh.mesh, drawPos, rot, subMesh.material, subMesh.renderLayer);
      }
    }
  }

  //drawPlanetがオフでVehicleMapにフォーカスした時しか呼ばれないよ
  public override void DrawLayer()
  {
  }

  private bool Filled(int index)
	{
		var building = Map.edificeGrid[index];
		if (building is not null)
		{
			return building.def.Fillage == FillCategory.Full;
		}
		return false;
	}

	public override void Regenerate()
	{
    if (!Map.IsVehicleMap)
      return;
    
		var subMesh = GetSubMesh(SnowMat);
		if (ModsConfig.BiotechActive)
		{
			subMesh.material.SetTexture(ShaderPropertyIDs.PollutedTex, PollutedSnowTex.Texture);
		}
		if (subMesh.mesh.vertexCount == 0)
		{
			SectionLayerGeometryMaker_Solid.MakeBaseGeometry(section, subMesh, AltitudeLayer.Terrain);
      VehicleSectionLayerManager.FinalizeVerts(this);
		}
		opacityListTmp.Clear();
		subMesh.Clear(MeshParts.Colors);
		var depthGrid_Unsafe = DepthGrid_Unsafe(Map.snowGrid);
		var cellRect = section.CellRect;
		var flag = false;
		var cellIndices = Map.cellIndices;
		for (var i = cellRect.minX; i <= cellRect.maxX; i++)
		{
			for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
			{
				opacityListTmp.Clear();
				var num = depthGrid_Unsafe[cellIndices.CellToIndex(i, j)];
				for (var k = 0; k < 9; k++)
				{
					var c = new IntVec3(i, 0, j) + GenAdj.AdjacentCellsAndInsideForUV[k];
					adjValuesTmp[k] = (c.InBounds(Map) ? depthGrid_Unsafe[cellIndices.CellToIndex(c)] : num);
				}
				for (var l = 0; l < 9; l++)
				{
					var num2 = 0f;
					for (var m = 0; m < vertexWeights[l].Count; m++)
					{
						num2 += adjValuesTmp[vertexWeights[l][m]];
					}
					num2 /= vertexWeights[l].Count;
					if (num2 > 0.01f)
					{
						flag = true;
					}
					opacityListTmp.Add(num2);
				}
				for (var n = 0; n < 9; n++)
        {
          adjValuesTmp[n] = Map.pollutionGrid.IsPolluted(new IntVec3(i, 0, j) + GenAdj.AdjacentCellsAndInsideForUV[n])
            ? 1f
            : 0f;
        }
				for (var num3 = 0; num3 < 9; num3++)
				{
					var num4 = 0f;
					for (var num5 = 0; num5 < vertexWeights[num3].Count; num5++)
					{
						num4 += adjValuesTmp[vertexWeights[num3][num5]];
					}
					num4 /= vertexWeights[num3].Count;
					var num6 = opacityListTmp[num3];
					subMesh.colors.Add(new Color32(Convert.ToByte(num4 * 255f), byte.MaxValue, byte.MaxValue, Convert.ToByte(num6 * 255f)));
				}
			}
		}
		if (flag)
		{
			subMesh.disabled = false;
			subMesh.FinalizeMesh(MeshParts.Colors);
		}
		else
		{
			subMesh.disabled = true;
		}
	}
}