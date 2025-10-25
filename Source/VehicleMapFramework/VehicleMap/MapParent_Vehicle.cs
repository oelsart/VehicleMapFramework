using System.Collections.Generic;
using JetBrains.Annotations;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly]
public class MapParent_Vehicle : PocketMapParent
{
    public VehiclePawnWithMap vehicle;

    public override string Label => $"{vehicle.Label}{"VMF_VehicleMap".Translate()}";

    public override Material Material => BaseContent.ClearMat;

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
    {
        return caravan.PawnsListForReading.Any(p => p is VehiclePawnWithMap) ? [] : base.GetFloatMenuOptions(caravan);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref vehicle, "vehicle");
    }
}