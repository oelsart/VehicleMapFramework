using RimWorld;
using SmashTools;
using Verse;
using PawnOverlayRenderer = Vehicles.PawnOverlayRenderer;

namespace VehicleMapFramework;

public static class PawnOverlayRendererUtility
{
  public static void SetDrawOffsets(this PawnOverlayRenderer renderer, VehiclePawnWithMap vehicle, VehicleRoleBuildable role)
  {
    var thing = role.upgradeComp.parent;
    var cacheMode = VehicleSectionLayerManager.CacheMode;
    VehicleSectionLayerManager.CacheMode = true;
    var position = GenThing.TrueCenter(thing.Position, thing.Rotation, thing.def.Size, 0f);
    VehicleSectionLayerManager.CacheMode = cacheMode;
    var vehiclePos = vehicle.cachedDrawPos;
    var rot = thing.Rotation;
    Rot8 rot8 = rot;
    var data = vehicle.VehicleDef.graphicData;
    var renderer2 = role.sourceRenderer;
    
    renderer.drawOffset = position.ToBaseMapCoord(vehicle, Rot8.North) - vehiclePos +
                          data.DrawOffsetForRot(Rot4.North) +
                          renderer2.drawOffset;
    
    renderer.drawOffsetNorth = position.ToBaseMapCoord(vehicle, Rot8.North) - vehiclePos +
                               data.DrawOffsetForRot(Rot4.North) +
                               renderer2.DrawOffsetFor(new Rot4(Rot4.NorthInt + rot.AsInt));
    
    renderer.drawOffsetSouth = position.ToBaseMapCoord(vehicle, Rot8.South) - vehiclePos +
                               data.DrawOffsetForRot(Rot4.South) +
                               renderer2.DrawOffsetFor(new Rot4(Rot4.SouthInt + rot.AsInt));
    
    renderer.drawOffsetEast = position.ToBaseMapCoord(vehicle, Rot8.East) - vehiclePos +
                              data.DrawOffsetForRot(Rot4.East) +
                              renderer2.DrawOffsetFor(new Rot4(Rot4.EastInt + rot.AsInt));
    
    renderer.drawOffsetWest = position.ToBaseMapCoord(vehicle, Rot8.West) - vehiclePos +
                              data.DrawOffsetForRot(Rot4.West) +
                              renderer2.DrawOffsetFor(new Rot4(Rot4.WestInt + rot.AsInt));
    
    renderer.drawOffsetNorthEast = position.ToBaseMapCoord(vehicle, Rot8.NorthEast) - vehiclePos +
                                   data.DrawOffsetForRot(Rot4.North).RotatedBy(45f) + (rot.IsHorizontal
                                     ? renderer2.DrawOffsetFor(rot).RotatedBy(45f)
                                     : renderer2.DrawOffsetFor(rot8.Rotated(Rot8.NorthEast)));
    
    renderer.drawOffsetNorthWest = position.ToBaseMapCoord(vehicle, Rot8.NorthWest) - vehiclePos +
                                   data.DrawOffsetForRot(Rot4.North).RotatedBy(-45f) + (rot.IsHorizontal
                                     ? renderer2.DrawOffsetFor(rot).RotatedBy(-45f)
                                     : renderer2.DrawOffsetFor(rot8.Rotated(Rot8.NorthWest)));
    
    renderer.drawOffsetSouthEast = position.ToBaseMapCoord(vehicle, Rot8.SouthEast) - vehiclePos +
                                   data.DrawOffsetForRot(Rot4.South).RotatedBy(-45f) + (rot.IsHorizontal
                                     ? renderer2.DrawOffsetFor(rot.Opposite).RotatedBy(-45f)
                                     : renderer2.DrawOffsetFor(rot8.Rotated(Rot8.SouthEast)));
    
    renderer.drawOffsetSouthWest = position.ToBaseMapCoord(vehicle, Rot8.SouthWest) - vehiclePos +
                                   data.DrawOffsetForRot(Rot4.South).RotatedBy(45f) + (rot.IsHorizontal
                                     ? renderer2.DrawOffsetFor(rot.Opposite).RotatedBy(45f)
                                     : renderer2.DrawOffsetFor(rot8.Rotated(Rot8.SouthWest)));
  }
}