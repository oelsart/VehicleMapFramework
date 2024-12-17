using Verse;

namespace VehicleInteriors
{
    public class CompVehicleEnterSpot : ThingComp
    {
        public CompProperties_VehicleEnterSpot Props => (CompProperties_VehicleEnterSpot)this.props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                vehicle.InteractionCells.Add(this.parent.Position);
            }
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            if (map.IsVehicleMapOf(out var vehicle))
            {
                vehicle.InteractionCells.Remove(this.parent.Position);
            }
        }
    }
}
