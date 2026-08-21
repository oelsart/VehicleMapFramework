using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompVehicleDrawOffset : VehicleComp
{
  public Vector3 drawOffset;
  public Vector3? drawOffsetNorth;
  public Vector3? drawOffsetEast;
  public Vector3? drawOffsetSouth;
  public Vector3? drawOffsetWest;
  
  private bool eastDiagonalRotated;
  private bool westDiagonalRotated;
  
  public override bool TickByRequest => true;

  public override void PostLoad()
  {
    Init();
  }

  public override void PostGeneration()
  {
    Init();
  }

  private void Init()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      var vehicleGraphic = Vehicle.VehicleGraphic;
      eastDiagonalRotated = vehicleGraphic.EastDiagonalRotated;
      westDiagonalRotated = vehicleGraphic.WestDiagonalRotated;
    });
  }

  /// <summary>
  /// 車両が実際に描画される時にかかる描画オフセット。車両のDrawPosは車両全体の中心を指すためこれに影響されない
  /// </summary>
  public Vector3 DrawOffsetFull(Rot8 rot)
  {
    if (!rot.IsDiagonal)
    {
      return DrawOffset(rot);
    }
    if (eastDiagonalRotated)
    {
      if (rot == Rot8.NorthEast)
      {
        return DrawOffset(Rot4.North);
      }
      if (rot == Rot8.SouthEast)
      {
        return DrawOffset(Rot4.South);
      }
    }
    if (westDiagonalRotated)
    {
      if (rot == Rot8.NorthWest)
      {
        return DrawOffset(Rot4.North);
      }
      if (rot == Rot8.SouthWest)
      {
        return DrawOffset(Rot4.South);
      }
    }
    return DrawOffset(rot);
  }

  private Vector3 DrawOffset(Rot4 rot)
  {
    return rot.AsInt switch
    {
      Rot4.NorthInt => drawOffsetNorth ?? drawOffset,
      Rot4.EastInt => drawOffsetEast ?? drawOffset,
      Rot4.SouthInt => drawOffsetSouth ?? drawOffset,
      Rot4.WestInt => drawOffsetWest ?? drawOffset,
      _ => drawOffset
    };
  }
}