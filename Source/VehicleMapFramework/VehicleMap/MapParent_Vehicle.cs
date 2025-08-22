using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class MapParent_Vehicle : PocketMapParent
{
    public VehiclePawnWithMap vehicle;

    public override string Label
    {
        get
        {
            return $"{vehicle.Label}{"VMF_VehicleMap".Translate()}";
        }
    }

    public override Material Material => BaseContent.ClearMat;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref vehicle, "vehicle");
    }
}