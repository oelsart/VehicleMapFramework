using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
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

    //なぜかdefがselectableじゃなくても選択できてしまう気がする
    public override bool SelectableNow => false;

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
    {
        if (caravan.PawnsListForReading.Any(p => p is VehiclePawnWithMap))
        {
            return [];
        }
        return base.GetFloatMenuOptions(caravan);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref vehicle, "vehicle");
    }
}