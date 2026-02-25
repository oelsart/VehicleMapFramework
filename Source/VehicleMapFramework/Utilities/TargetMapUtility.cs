using Verse;

namespace VehicleMapFramework;

public static class TargetMapUtility
{
    public static TargetMapManager manager;

    extension(Thing thing)
    {
        public TargetInfo TargetInfo
        {
            get => manager?.TargetInfoTable.TryGetValue(thing, out var box) ?? false ? box.Value : TargetInfo.Invalid;
            set => manager?.GetOrCreateTargetInfo(thing)?.Value = value;
        }

        public Map TargetMap
        {
            get => manager?.TargetInfoTable.TryGetValue(thing, out var box) ?? false ? box.Value.Map : null;
            set => manager?.GetOrCreateTargetInfo(thing)?.Value = new TargetInfo(IntVec3.Invalid, value);
        }

        public void RemoveTargetInfo()
        {
            if (thing is null || manager is null) return;
            if (manager.TargetInfoTable.TryGetValue(thing, out var box))
            {
                VMF_Log.DebugMessage($"Remove TargetInfo: {thing}");
                box.Value = TargetInfo.Invalid;
            }
        }

        public bool TryGetTargetInfo(out TargetInfo target)
        {
            target = TargetInfo.Invalid;
            if (thing is null || manager is null) return false;
            var result = manager.TargetInfoTable.TryGetValue(thing, out var box);
            if (result)
                target = box.Value;
            return result && target.IsValid;
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
            if (manager?.TargetInfoTable is not { } table) return false;
            var result = table.TryGetValue(thing, out var box);
            if (result)
                map = box.Value.Map;
            return result && map != null;
        }
        
        public Map TargetMapOrThingMap => thing.TargetMap ?? thing.Map;

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
        public Map TargetMapOrPawnMap => pawn.TargetMap ?? pawn.CurJob?.globalTarget.Map ?? pawn.Map;
    }

    public static IntVec3 TargetCellOnBaseMap(this ref LocalTargetInfo targ, Thing thing)
    {
        return targ.HasThing ? targ.Thing.PositionOnBaseMap : thing.TryGetTargetMap(out var map) ? targ.Cell.ToBaseMapCoord(map) : targ.Cell;
    }
    
    public static Map TargetMapOrMap(Map map, Thing thing)
    {
        return thing.TargetMap ?? map;
    }
}