using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace VehicleMapFramework;

public class VehicleMapParentsComponent : WorldComponent
{
    public static Dictionary<Map, MapParent_Vehicle> CachedMapParentVehicle => cachedMapParentVehicle;

    public VehicleMapParentsComponent(World world) : base(world)
    {
        Command_FocusVehicleMap.FocuseLockedVehicle = null;
        Command_FocusVehicleMap.FocusedVehicle = null;
    }

    public override void FinalizeInit(bool fromLoad)
    {
        cachedMapParentVehicle.Clear();
    }

    private static Dictionary<Map, MapParent_Vehicle> cachedMapParentVehicle = [];
}