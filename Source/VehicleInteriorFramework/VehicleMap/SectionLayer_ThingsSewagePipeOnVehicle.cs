using RimWorld;
using Verse;

namespace VehicleInteriors;

public class SectionLayer_ThingsSewagePipeOnVehicle : SectionLayer_ThingsOnVehicle
{
    public SectionLayer_ThingsSewagePipeOnVehicle(Section section) : base(section)
    {
        relevantChangeTypes = MapMeshFlagDefOf.Buildings;
    }

    protected override void TakePrintFrom(Thing t)
    {
        if (ModCompat.DubsBadHygiene.Building_Pipe.IsAssignableFrom(t.GetType()))
        {
            ModCompat.DubsBadHygiene.PrintForGrid(t, this);
        }
    }
}