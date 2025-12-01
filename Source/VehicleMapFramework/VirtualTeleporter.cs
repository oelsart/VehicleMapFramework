using System;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework;

public readonly struct VirtualTeleporter : IDisposable
{
    public static readonly AccessTools.FieldRef<Thing, sbyte> mapIndexOrState = AccessTools.FieldRefAccess<Thing, sbyte>("mapIndexOrState");
    
    private readonly Thing _thing;

    private readonly Map _map;

    private readonly IntVec3 _pos;
        
    public VirtualTeleporter(Thing thing, Map map, IntVec3? c = null)
    {
        _thing = thing;
        _map = thing.Map;
        _pos = thing.Position;
        mapIndexOrState(thing) = (sbyte)map.Index;
        if (c.HasValue) thing.SetPositionDirect(c.Value);
    }

    public void Dispose()
    {
        mapIndexOrState(_thing) = (sbyte)_map.Index;
        _thing.SetPositionDirect(_pos);
    }
}