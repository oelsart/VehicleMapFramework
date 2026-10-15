using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Command_SelectVehicleMap(VehiclePawnWithMap vehicle) : Command_ToggleWithIcon
{
  public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
  {
    icon = VehicleMapUIRenderer.GetVehicleMapTexture(vehicle, Rot4.East, (256, 256));
    base.DrawIcon(rect, buttonMat, parms);
  }
}