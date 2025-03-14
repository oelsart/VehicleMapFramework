using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace VehicleInteriors
{
    public class SectionLayer_ThingsSewagePipeOnVehicle : SectionLayer_ThingsOnVehicle
    {
		public SectionLayer_ThingsSewagePipeOnVehicle(Section section) : base(section)
        {
            this.relevantChangeTypes = MapMeshFlagDefOf.Buildings;
        }

        protected override void TakePrintFrom(Thing t)
        {
            if (t_Building_Pipe.IsAssignableFrom(t.GetType()))
            {
                PrintForGrid(t, this);
            }
        }

        private static Type t_Building_Pipe = AccessTools.TypeByName("DubsBadHygiene.Building_Pipe");

        private static FastInvokeHandler PrintForGrid = MethodInvoker.GetHandler(AccessTools.Method(t_Building_Pipe, "PrintForGrid"));
    }
}