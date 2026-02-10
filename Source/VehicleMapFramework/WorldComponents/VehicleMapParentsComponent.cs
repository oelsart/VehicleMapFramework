using System;
using System.Runtime.CompilerServices;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

public class VehicleMapParentsComponent : WorldComponent
{
    private static MapParent_Vehicle[] cachedMapParentVehicle = new MapParent_Vehicle[32];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MapParent_Vehicle GetCachedVehicle(Map map)
    {
        if (map is null) return null;
        
        var index = map.Index;
        if (index >= 0 && index < cachedMapParentVehicle.Length)
        {
            return cachedMapParentVehicle[index];
        }
        return null;
    }

    public static void SetCachedVehicle(Map map, MapParent_Vehicle parent)
    {
        var index = map.Index;
        if (index < 0) return;
        if (index >= cachedMapParentVehicle.Length)
        {
            var newSize = cachedMapParentVehicle.Length;
            while (index >= newSize)
            {
                newSize *= 2;
            }
            Array.Resize(ref cachedMapParentVehicle, newSize);
        }
        cachedMapParentVehicle[index] = parent;
    }

    public VehicleMapParentsComponent(World world) : base(world)
    {
        Command_FocusVehicleMap.FocusLockedVehicle = null;
        Command_FocusVehicleMap.FocusedVehicle = null;
    }

    public override void FinalizeInit(bool fromLoad)
    {
        Array.Clear(cachedMapParentVehicle, 0, cachedMapParentVehicle.Length);
    }
}