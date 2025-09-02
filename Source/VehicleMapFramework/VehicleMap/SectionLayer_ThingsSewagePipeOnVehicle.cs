using RimWorld;
using Verse;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework;

public class SectionLayer_ThingsSewagePipeOnVehicle : SectionLayer_ThingsOnVehicle
{
    public SectionLayer_ThingsSewagePipeOnVehicle(Section section) : base(section)
    {
        relevantChangeTypes = MapMeshFlagDefOf.Buildings;
    }

    public override void Regenerate()
    {
        if (!DubsBadHygiene.Active || DubsBadHygiene.LiteMode) return;
        base.Regenerate();
    }

    protected override void TakePrintFrom(Thing t)
    {
        if (DubsBadHygiene.Building_Pipe.IsAssignableFrom(t.GetType()))
        {
            DubsBadHygiene.PrintForGrid(t, this);
        }
    }
}