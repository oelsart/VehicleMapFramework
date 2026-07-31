using Verse;

namespace VehicleMapFramework;

public class CompGroundToVehicleAccess : CompVehicleEnterSpot
{
  protected override bool Available
  {
    get
    {
      if (!parent.IsOnVehicleMapOf(out var vehicle))
      {
        return false;
      }
      var opposite = parent.Position + parent.Rotation.Opposite.AsIntVec3;
      return vehicle.CachedOutOfBoundsCells.Contains(opposite) ||
             vehicle.CachedExpandableCells.Contains(opposite) && vehicle.CachedImpassableCells.Contains(opposite);
    }
  }

  protected override TargetInfo AccessSpot
  {
    get
    {
      var pos = CrossMapReachabilityUtility.EnterVehiclePosition(parent);
      return pos.IsValid ? new TargetInfo(pos, parent.GroundMap) : TargetInfo.Invalid;
    }
  }

  public override float MovePerTick(Pawn pawn) => 0.3f / pawn.TicksPerMoveCardinal;
}