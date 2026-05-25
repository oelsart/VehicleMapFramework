using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class Graphic_VehicleOpacity : Graphic_Vehicle
{
  public static readonly int OpacityID = Shader.PropertyToID("_Opacity");

  private float opacityInt = 1f;

  public float Opacity
  {
    get => opacityInt;
    set
    {
      opacityInt = value;
      Notify_OpacityChanged();
    }
  }

  public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
  {
    Log.Warning(
      $"Retrieving {GetType()} Colored Graphic from vanilla GraphicDatabase which will result in redundant graphic creation.");
    return GraphicDatabase.Get<Graphic_VehicleOpacity>(path, newShader, drawSize, newColor, newColorTwo, DataRgb);
  }

  private void Notify_OpacityChanged()
  {
    if (materials.NullOrEmpty()) return;
    foreach (var mat in materials)
    {
      mat?.SetFloat(OpacityID, opacityInt);
    }
  }
}
