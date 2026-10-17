using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Vehicles.Rendering;
using Verse;
using Object = UnityEngine.Object;

namespace VehicleMapFramework;

public class Command_SelectVehicleMap(VehiclePawnWithMap vehicle) : Command_ToggleWithIcon
{
  public VehiclePortrait portrait;

  public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
  {
    var min = Mathf.Min(rect.width, rect.height);
    var rect2 = rect.ContractedBy((min - min * 0.95f) / 2f);
    Widgets.BeginGroup(rect2);
    
    var request = BlitRequest.For(vehicle);
    var parentRect = rect2.AtZero();
    var mapRect = vehicle.VehicleMapBlitter.GetRenderRect(parentRect, request, true);
    var zoom = Mathf.Min(parentRect.width / mapRect.width, parentRect.width / mapRect.height);
    var drawRect = new Rect(parentRect.center - mapRect.center * zoom, parentRect.size * zoom);
    portrait.Draw(drawRect, in request);
    Widgets.EndGroup();
  }

  [DebugOutput(VehicleMapFramework.CategoryName, true, name = "Write Command_SelectVehicleMap texture")]
  private static void Write()
  {
    if (Find.Selector.SingleSelectedObject is not VehiclePawnWithMap vehicle)
      return;
    
    var g_RenderTexture = AccessTools.PropertyGetter(typeof(VehiclePortrait), "RenderTexture");
    var renderTexture = (RenderTexture)g_RenderTexture.Invoke(vehicle.VehicleMapGizmo.portrait, []);
    if (renderTexture is null)
      return;
    
    var active = RenderTexture.active;
    var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);

    RenderTexture.active = renderTexture;
    texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
    texture.Apply();

    var fullPath = Path.Combine(Application.persistentDataPath, $"{vehicle.ThingID}");
    File.WriteAllBytes(fullPath, texture.EncodeToPNG());
    Messages.Message($"Saved render texture to {fullPath}", MessageTypeDefOf.NeutralEvent, false);
    Object.Destroy(texture);
    RenderTexture.active = active;
  }
}