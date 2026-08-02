using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class CompGangplank : CompVehicleEnterSpot
{
  private const int RateTicks = 30;
  private int ticks;
  private Vector3 pairDrawPos;
  
  public new CompProperties_Gangplank Props => (CompProperties_Gangplank)props;
  
  protected override bool Available => Pair is not null;

  protected Thing Pair { get; set; }

  protected override TargetInfo AccessSpot => Pair ?? TargetInfo.Invalid;

  public override float MovePerTick(Pawn pawn) => 0.5f / pawn.TicksPerMoveCardinal;

  public override void CompTickInterval(int delta)
  {
    ticks += delta;
    if (ticks < RateTicks)
      return;

    ticks = 0;
    if (!parent.IsOnVehicleMapOf(out var vehicle))
      return;
    
    if (Pair is null)
    {
      var origin = parent.DrawPos;
      var dir = parent.Rotation.AsVector2.ToVector3().RotatedBy(vehicle.FullAngle);
      var map = parent.GroundMap;
      for (var i = 1; i <= Props.length; i++)
      {
        var pos = origin + dir * i;
        if (pos.TryGetVehicleMap(map, out var vehicle2, VehicleMapFlag.None) &&
            vehicle != vehicle2)
        {
          var anchor =
            GenSpawn.Spawn(VMF_DefOf.VMF_GangplankAnchor, pos.ToVehicleMapCoord(vehicle2).ToIntVec3(), vehicle2.VehicleMap);
          Pair = anchor;
          anchor.TryGetComp<CompGangplank>()?.Pair = parent;
          pairDrawPos = anchor.DrawPos;
          return;
        }
      }
    }
    else
    {
      if ((Pair.DrawPos - pairDrawPos).MagnitudeHorizontalSquared() > 1f)
      {
        Pair.Destroy();
        Pair = null;
      }
    }
  }
}