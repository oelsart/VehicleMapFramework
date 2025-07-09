using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class SectionLayer_ThingsPowerGridOnVehicle : SectionLayer_ThingsOnVehicle
{
    public SectionLayer_ThingsPowerGridOnVehicle(Section section) : base(section)
    {
        requireAddToMapMesh = false;
        relevantChangeTypes = MapMeshFlagDefOf.PowerGrid;
    }

    public override void DrawLayer()
    {
        if (OverlayDrawHandler.ShouldDrawPowerGrid)
        {
            base.DrawLayer();
        }
    }

    protected override void TakePrintFrom(Thing t)
    {
        if (t.Faction != null && t.Faction != Faction.OfPlayer)
        {
            return;
        }
        if (t is Building building)
        {
            building.PrintForPowerGrid(this);
        }
    }
}
