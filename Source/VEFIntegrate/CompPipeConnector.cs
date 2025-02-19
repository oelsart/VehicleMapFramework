using PipeSystem;
using Verse;

namespace VehicleInteriors
{
    public class CompPipeConnector : ThingComp
    {
        public CompProperties_PipeConnector Props => (CompProperties_PipeConnector)this.props;

        public override void CompTick()
        {
            base.CompTick();
            if (Find.TickManager.TicksGame % ticksInterval != 0) return;

            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {

            }
            else
            {
                foreach (var c in this.parent.OccupiedRect().ExpandedBy(1).Cells)
                {
                    if (!c.InBounds(this.parent.Map)) continue;

                    if (c.ToVector3Shifted().TryGetVehicleMap(this.parent.Map, out vehicle, false))
                    {
                        var c2 = c.ToVehicleMapCoord(vehicle);
                        if (!c2.InBounds(vehicle.VehicleMap)) continue;

                        var receiver = c2.GetFirstThingWithComp<CompPipeConnector>(vehicle.VehicleMap);
                        if (receiver != null)
                        {
                            var compConnector = receiver.GetComp<CompPipeConnector>();
                            if (compConnector.pipeNetDef == this.pipeNetDef)
                            {
                                compConnector.connectReq = true;
                            }
                        }
                    }
                }
            }
        }

        private PipeNetDef pipeNetDef;

        public const int ticksInterval = 30;
    }
}