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
    private readonly bool _setDepartMap;
    private readonly Map _departMap;
        
    public VirtualTeleporter(Thing thing, Map map, IntVec3? c = null, bool setDepartMap = false)
    {
        _thing = thing;
        if (thing is not { Spawned: true }) return;

        _map = thing.Map;
        _pos = thing.Position;
        if (map is not null)
            mapIndexOrState(thing) = (sbyte)map.Index;
        if (c.HasValue) thing.SetPositionDirect(c.Value);
        _setDepartMap = setDepartMap;
        if (_setDepartMap && thing is Pawn pawn)
        {
          _departMap = pawn.DepartMap;
          pawn.DepartMap = _map;
        }
    }

    public void Dispose()
    {
        if (_thing is not null)
        {
            if (_map is not null)
                mapIndexOrState(_thing) = (sbyte)_map.Index;
            if (_pos.IsValid)
                _thing.SetPositionDirect(_pos);
            if (_setDepartMap && _thing is Pawn pawn)
            {
              pawn.DepartMap = _departMap;
            }
        }
    }
}