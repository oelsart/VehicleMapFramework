using System;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework;

public readonly struct VirtualTeleporter : IDisposable
{
    public static readonly AccessTools.FieldRef<Thing, sbyte> mapIndexOrState = AccessTools.FieldRefAccess<Thing, sbyte>("mapIndexOrState");
    
    private readonly Thing _thing;

    private readonly Map _map;

    private readonly IntVec3 _pos = IntVec3.Invalid;
        
    public VirtualTeleporter(Thing thing, Map map, IntVec3? c = null)
    {
        _thing = thing;
        if (thing is null or { Spawned: false })
            return;
        _map = thing.Map;
        _pos = thing.Position;
        if (map is not null)
            mapIndexOrState(thing) = (sbyte)map.Index;
        if (c.HasValue) thing.SetPositionDirect(c.Value);
    }

    public void Dispose()
    {
        if (_thing is not null)
        {
            if (_map is not null)
                mapIndexOrState(_thing) = (sbyte)_map.Index;
            if (_pos.IsValid)
                _thing.SetPositionDirect(_pos);
        }
    }
}