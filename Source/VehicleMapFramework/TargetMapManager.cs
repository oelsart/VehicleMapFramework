﻿using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VehicleMapFramework;

public class TargetMapManager(World world) : WorldComponent(world)
{
    private static Dictionary<Thing, TargetInfo> TargetInfoDic
    {
        get
        {
            var component = Find.World?.GetComponent<TargetMapManager>();
            if (component == null) return null;

            component.targetInfoDic ??= [];
            return component.targetInfoDic;
        }
    }

    public static void SetTargetInfo(Thing thing, TargetInfo target)
    {
        if (thing is null) return;
        VMF_Log.Debug($"[TargetMapManager] Set target info for {thing}");
        TargetInfoDic[thing] = target;
    }

    public static void SetTargetMap(Thing thing, Map map)
    {
        if (thing is null) return;
        VMF_Log.Debug($"[TargetMapManager] Set target map for {thing}");
        TargetInfoDic[thing] = new TargetInfo(IntVec3.Invalid, map);
    }

    public static bool RemoveTargetInfo(Thing thing)
    {
        var result = TargetInfoDic.Remove(thing);
        if (result)
        {
            VMF_Log.Debug($"[TargetMapManager] Remove target map for {thing}");
        }
        return result;
    }

    public static bool HasTargetInfo(Thing thing, out TargetInfo target)
    {
        if (thing == null)
        {
            target = TargetInfo.Invalid;
            return false;
        }
        return TargetInfoDic.TryGetValue(thing, out target) && target.IsValid && target.Map != null;
    }

    public static bool HasTargetMap(Thing thing, out Map map)
    {
        if (thing == null)
        {
            map = null;
            return false;
        }
        var flag = TargetInfoDic.TryGetValue(thing, out var target);
        map = target.Map;
        return flag && map != null;
    }

    public static Map TargetMapOrMap(Map map, Thing thing)
    {
        if (HasTargetMap(thing, out var targetMap))
        {
            return targetMap;
        }
        return map;
    }

    public static Map TargetMapOrPawnMap(Pawn pawn)
    {
        if (HasTargetMap(pawn, out var map) || (map = pawn.CurJob?.globalTarget.Map) != null)
        {
            return map;
        }
        return pawn.Map;
    }

    public static IntVec3 TargetCellOnBaseMap(ref LocalTargetInfo targ, Thing thing)
    {
        return targ.HasThing ? targ.CellOnBaseMap() : HasTargetMap(thing, out var map) ? targ.Cell.ToBaseMapCoord(map) : targ.Cell;
    }

    public static IntVec3 PositionOnTargetMap(Thing thing)
    {
        if (HasTargetMap(thing, out var map))
        {
            if (map == thing.Map)
            {
                return thing.Position;
            }
            var pos = thing.PositionOnBaseMap();
            if (map.IsNonFocusedVehicleMapOf(out var vehicle))
            {
                pos = pos.ToVehicleMapCoord(vehicle);
            }
            return pos;
        }
        return thing.Position;
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref targetInfoDic, "TargetInfo", LookMode.Reference, LookMode.TargetInfo, ref tmpKeys, ref tmpValues, false, true, true);
    }

    private Dictionary<Thing, TargetInfo> targetInfoDic;

    private List<Thing> tmpKeys = [];

    private List<TargetInfo> tmpValues = [];
}
