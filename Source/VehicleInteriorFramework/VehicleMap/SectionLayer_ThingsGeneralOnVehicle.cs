using RimWorld;
using System;
using Verse;
using static VehicleInteriors.ModCompat;

namespace VehicleInteriors;

public class SectionLayer_ThingsGeneralOnVehicle : SectionLayer_ThingsOnVehicle
{
    public SectionLayer_ThingsGeneralOnVehicle(Section section) : base(section)
    {
        relevantChangeTypes = MapMeshFlagDefOf.Things;
        requireAddToMapMesh = true;
    }

    protected override void TakePrintFrom(Thing t)
    {
        try
        {
            if (!AdaptiveStorage.Active || AllowPrint(t))
            {
                if (AdaptiveStorage.Active && AdaptiveStorage.ThingClass.IsAssignableFrom(t.GetType()))
                {
                    var renderer = AdaptiveStorage.Renderer(t);
                    if (renderer != null)
                    {
                        AdaptiveStorage.SetAllPrintDatasDirty(renderer);
                    }
                }
                t.Print(this);
            }
        }
        catch (Exception ex)
        {
            Log.Error(string.Concat(
            [
                "Exception printing ",
                t,
                " at ",
                t.Position,
                ": ",
                ex
            ]));
        }
    }

    private bool AllowPrint(Thing t)
    {
        if (t.def.category == ThingCategory.Item)
        {
            var storingThing = t.StoringThing();
            if (storingThing != null && AdaptiveStorage.ThingClass.IsAssignableFrom(storingThing.GetType()))
            {
                return t == storingThing;
            }
        }
        return true;
    }
}
