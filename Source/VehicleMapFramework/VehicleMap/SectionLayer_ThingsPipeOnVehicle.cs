using RimWorld;
using Verse;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework;

public class SectionLayer_ThingsPipeOnVehicle : SectionLayer_ThingsOnVehicle
{
    public SectionLayer_ThingsPipeOnVehicle(Section section) : base(section)
    {
        relevantChangeTypes = MapMeshFlagDefOf.Buildings;
    }

    public override void Regenerate()
    {
        if (!Rimefeller.Active) return;
        base.Regenerate();
    }

    protected override void TakePrintFrom(Thing t)
    {
        if (Rimefeller.Building_Pipe.IsAssignableFrom(t.GetType()))
        {
            Rimefeller.PrintForGrid(t, this);
        }
    }
}
