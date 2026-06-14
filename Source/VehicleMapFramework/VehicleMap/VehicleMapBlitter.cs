using System.Collections.Generic;
using SmashTools.Rendering;
using UnityEngine;
using Vehicles.Rendering;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class VehicleMapBlitter(VehiclePawnWithMap vehicle) : IBlitTarget
{
  private static Material defaultMat;
  private static readonly int MainTex = Shader.PropertyToID("_MainTex");
  
  static VehicleMapBlitter()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      defaultMat = new Material(ShaderDatabase.Transparent);
    });
  }
  
  (int width, int height) IBlitTarget.TextureSize(in BlitRequest request)
  {
    const int SizePerCell = 64;
    if (vehicle.VehicleMap is null)
      return (0, 0);

    var size = vehicle.VehicleMap.Size;
    return (size.x * SizePerCell, size.z * SizePerCell);
  }

  IEnumerable<RenderData> IBlitTarget.GetRenderData(Rect rect, BlitRequest request)
  {
    Vector2? drawSize = null;
    Vector3? drawOffset = null;
    if (!vehicle.def.HasModExtension<VehicleMapProps_Gravship>())
    {
      drawSize = vehicle.DrawSize;
      drawOffset = VehicleMapUtility.OffsetFor(vehicle, request.rot);
    }
    var texture = VehicleMapUIRenderer.GetVehicleMapTexture(vehicle,
      request.rot,
      new Vector2Int((int)rect.size.x, (int)rect.size.y),
      drawSize,
      drawOffset);
    defaultMat.mainTexture = texture;
    yield return new RenderData(rect, texture, defaultMat, null);
  }
}