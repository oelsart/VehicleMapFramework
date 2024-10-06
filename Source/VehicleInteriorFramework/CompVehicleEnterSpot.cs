using Verse;

namespace VehicleInteriors
{
    public class CompVehicleEnterSpot : ThingComp
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (this.parent.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                parentVehicle.vehicle.InteractionCells.Add(this.parent.Position);
            }
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            if (map.Parent is MapParent_Vehicle parentVehicle)
            {
                parentVehicle.vehicle.InteractionCells.Remove(this.parent.Position);
            }
        }
    }
}
