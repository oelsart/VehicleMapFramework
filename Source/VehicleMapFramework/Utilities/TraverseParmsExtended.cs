using System;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public struct TraverseParmsExtended : IEquatable<TraverseParmsExtended>
{
  public TraverseParms traverseParms;

  public AbilityDef ability;

  public static implicit operator TraverseParmsExtended(TraverseParms m)
  {
    return new TraverseParmsExtended
    {
      traverseParms = m
    };
  }

  public static bool operator ==(TraverseParmsExtended a, TraverseParmsExtended b)
  {
    return a.traverseParms == b.traverseParms && a.ability == b.ability;
  }

  public static bool operator !=(TraverseParmsExtended a, TraverseParmsExtended b)
  {
    return !(a == b);
  }

  public override bool Equals(object obj)
  {
    return obj is TraverseParmsExtended other && Equals(other);
  }

  public bool Equals(TraverseParmsExtended other)
  {
    return traverseParms.Equals(other.traverseParms) && ability == other.ability;
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(traverseParms, ability);
  }
}
