using LudeonTK;
using Verse;
using Waylith.Injection;

namespace VehicleMapFramework;

public static class TargetMapUtility
{
  internal static bool logging;

  extension(Thing thing)
  {
    internal extern ref TargetInfo TargetInfoField { [InjectedField(Initializer = nameof(get_InvalidTargetInfo))] get; }
    
    private static TargetInfo InvalidTargetInfo => TargetInfo.Invalid;
    
    public TargetInfo TargetInfo
    {
      get => thing.TargetInfoField;
      set
      {
        if (logging) VMF_Log.Message($"Set TargetInfo ({value}) to {thing}");
        thing.TargetInfoField = value;
      }
    }

    public Map TargetMap
    {
      get => thing.TargetInfoField.Map;
      set
      {
        if (logging) VMF_Log.Message($"Set TargetMap ({value}) to {thing}");
        thing.TargetInfoField = new TargetInfo(IntVec3.Invalid, value);
      }
    }

    public void RemoveTargetInfo()
    {
      if (thing is null) return;
      if (logging) VMF_Log.Message($"Remove TargetInfo ({thing.TargetInfoField}) from {thing}");
      thing.TargetInfoField = TargetInfo.Invalid;
    }

    public bool TryGetTargetInfo(out TargetInfo target)
    {
      target = thing.TargetInfoField;
      return target.IsValid;
    }

    public bool IsTargeting(LocalTargetInfo localTarget, out TargetInfo target)
    {
      target = TargetInfo.Invalid;
      if (thing is null) return false;
      return thing.TryGetTargetInfo(out target) && (LocalTargetInfo)target == localTarget;
    }

    public bool TryGetTargetMap(out Map map)
    {
      map = null;
      if (thing is null) return false;
      map = thing.TargetInfoField.Map;
      return map is not null;
    }

    public Map TargetMapOrMap => thing.TargetMap ?? thing.Map;

    public Map PawnOrThingTargetMap => thing is Pawn pawn ? pawn.PawnTargetMap : thing.TargetMap;

    public IntVec3 PositionOnTargetMap
    {
      get
      {
        if (thing.TryGetTargetMap(out var map))
        {
          if (map == thing.Map)
          {
            return thing.Position;
          }

          var pos = thing.PositionOnBaseMap;
          if (map.IsNonFocusedVehicleMapOf(out var vehicle))
          {
            pos = pos.ToVehicleMapCoord(vehicle);
          }

          return pos;
        }

        return thing.Position;
      }
    }
  }

  extension(Pawn pawn)
  {
    public Map PawnTargetMap => pawn.TargetMap ?? pawn.CurJob?.globalTarget.Map;

    public Map PawnTargetMapOrMap => pawn.PawnTargetMap ?? pawn.Map;
  }

  public static IntVec3 TargetCellOnBaseMap(this ref LocalTargetInfo targ, Thing thing)
  {
    return targ.HasThing
      ? targ.Thing.PositionOnBaseMap
      : targ.Cell.ToBaseMapCoord(thing.PawnOrThingTargetMap);
  }

  public static Map TargetMapOrMap(Map map, Thing thing)
  {
    return thing.TargetMap ?? map;
  }

  [DebugAction(VehicleMapFramework.CategoryName, "Toggle Logging TargetMapManager")]
  private static void ToggleLogging()
  {
    logging = !logging;
  }
}