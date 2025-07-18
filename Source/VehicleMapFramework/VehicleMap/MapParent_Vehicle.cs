using RimWorld;
using RimWorld.Planet;
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

    //public override void FinalizeLoading()
    //{
    //    base.FinalizeLoading();
    //    Delay.AfterNSeconds(0.1f, () =>
    //    {
    //        LongEventHandler.ExecuteWhenFinished(() =>
    //        {
    //            vehicle?.VehicleMap.mapDrawer?.RegenerateEverythingNow();
    //        });
    //    });
    //}

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref vehicle, "vehicle");
    }
}