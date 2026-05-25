using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class CompLocalThruster : ThingComp
{
  [field: Unsaved]
  public CompGravshipFacility CompGravshipFacility
  {
    get
    {
      field ??= parent.GetComp<CompGravshipFacility>();
      return field;
    }
  }

  public override IEnumerable<Gizmo> CompGetGizmosExtra()
  {
    if (!parent.Tile.LayerDef?.isSpace ?? true) yield break;

    if (!parent.IsOnVehicleMapOf(out var vehicle))
    {
      yield return new Command_Toggle
      {
        defaultLabel = "VMF_VehicleMode".Translate(), icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/GravshipVehicleMode"), toggleAction = GenerateGravshipVehicle, isActive = () => false
      };
    }
    else if (vehicle.Spawned && vehicle.def.HasModExtension<VehicleMapProps_Gravship>())
    {
      yield return new Command_Toggle
      {
        defaultLabel = "VMF_VehicleMode".Translate(), icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/GravshipVehicleMode"), toggleAction = () => PlaceGravship(vehicle), isActive = () => true
      };
    }
  }

  public void GenerateGravshipVehicle()
  {
    var report = GravshipVehicleUtility.GenerateGravshipVehicle(CompGravshipFacility?.engine, VMF_DefOf.VMF_GravshipVehicleBaseSpace, false);
    if (!report.Accepted)
    {
      Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
    }
  }

  public void PlaceGravship(VehiclePawnWithMap vehicle)
  {
    var report = GravshipVehicleUtility.PlaceGravshipVehicle(CompGravshipFacility?.engine, vehicle);
    if (!report.Accepted)
    {
      Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
    }
  }
}
