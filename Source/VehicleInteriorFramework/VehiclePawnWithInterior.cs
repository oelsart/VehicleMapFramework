using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class VehiclePawnWithInterior : VehiclePawn
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            var mapParent = (MapParent_Vehicle)WorldObjectMaker.MakeWorldObject(VIF_DefOf.VIF_VehicleMap);
            mapParent.vehicle = this;
            new GenStepWithParams();
            this.interiorMap = VehicleMapGenerator.GenerateMap(new IntVec3(3, 1, 2), mapParent, VIF_DefOf.VIF_InteriorGenerator, null, null, true);
        }

        public override void Tick()
        {
            base.Tick();
            this.interiorMap.MapUpdate();
        }

        public Map interiorMap;
    }
}