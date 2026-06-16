using SmashTools;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles.Rendering;
using Verse;
#if DEV
using SmashTools.Rendering;
#endif

namespace VehicleMapFramework;

public class Command_SelectVehicleMap(VehiclePawnWithMap vehicle) : Command_ToggleWithIcon
{
  public static bool Available { get; } =
    new VfVersionalPatchAttribute("1.6.2380", ComparisonType.GreaterThanOrEqual).Available;
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
    
    var request = new BlitRequest(vehicle);
    request.blitTargets.Add(vehicle.VehicleDef);
    if (vehicle.CompVehicleTurrets is { } compTurrets &&
        !compTurrets.Turrets.NullOrEmpty())
    {
      foreach (var turret in compTurrets.Turrets)
      {
        if (!turret.NoGraphic)
          request.blitTargets.Add(turret);
      }
    }

    var mapBlitter = vehicle.VehicleMapBlitter;
    request.blitTargets.Add(mapBlitter);
    var parentRect = rect2.AtZero();
    var mapRect = mapBlitter.GetRenderRect(parentRect, request);
    var zoom = Mathf.Max(Mathf.Max(mapRect.width, mapRect.height) / parentRect.width, 1f);
    var drawRect = new Rect(parentRect.center - mapRect.center * zoom, parentRect.size * zoom);
    portrait.Draw(drawRect, in request);
    Widgets.EndGroup();
  }
}