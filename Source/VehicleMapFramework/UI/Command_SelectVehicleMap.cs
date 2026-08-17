using SmashTools;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles.Rendering;
using Verse;
#if DEV
#endif

namespace VehicleMapFramework;

public class Command_SelectVehicleMap(VehiclePawnWithMap vehicle) : Command_ToggleWithIcon
{
  public static bool Available { get; } =
    new VFVersionalPatchAttribute("1.6.2380", ComparisonType.GreaterThanOrEqual).Available;
  public VehiclePortrait portrait;

  public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
  {
    if (!Available)
    {
      icon = VehicleMapUIRenderer.GetVehicleMapTexture(vehicle, Rot4.East, (256, 256));
      base.DrawIcon(rect, buttonMat, parms);
      return;
    }
    
    var min = Mathf.Min(rect.width, rect.height);
    var rect2 = rect.ContractedBy((min - min * 0.95f) / 2f);
    Widgets.BeginGroup(rect2);
    
    var request = BlitRequest.For(vehicle);
    var parentRect = rect2.AtZero();
    var mapRect = vehicle.VehicleMapBlitter.GetRenderRect(parentRect, request, true);
    var zoom = parentRect.width / mapRect.width;
    var drawRect = new Rect(parentRect.position - mapRect.position * zoom, parentRect.size * zoom);
    portrait.Draw(drawRect, in request);
    Widgets.EndGroup();
  }
}