using RimWorld;
using System;
using Verse;

namespace VehicleInteriors
{
    public class SectionLayer_ThingsGeneralOnVehicle : SectionLayer_ThingsOnVehicle
    {
        public SectionLayer_ThingsGeneralOnVehicle(Section section) : base(section)
        {
            this.relevantChangeTypes = MapMeshFlagDefOf.Things;
            this.requireAddToMapMesh = true;
        }

        protected override void TakePrintFrom(Thing t)
        {
            try
            {
                t.Print(this);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat(new object[]
                {
                    "Exception printing ",
                    t,
                    " at ",
                    t.Position,
                    ": ",
                    ex
                }));
            }
        }
    }
}
