using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class SectionLayer_ThingsPipeOnVehicle : SectionLayer_ThingsOnVehicle
{
    public SectionLayer_ThingsPipeOnVehicle(Section section) : base(section)
    {
        relevantChangeTypes = MapMeshFlagDefOf.Buildings;
    }

    protected override void TakePrintFrom(Thing t)
    {
        if (ModCompat.Rimefeller.Building_Pipe.IsAssignableFrom(t.GetType()))
        {
            ModCompat.Rimefeller.PrintForGrid(t, this);
        }
    }
}
