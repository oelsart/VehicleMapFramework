using HarmonyLib;
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

            this.adaptiveStorageActive = ModsConfig.IsActive("adaptive.storage.framework");
            if (this.adaptiveStorageActive)
            {
                this.t_Adaptive_ThingClass = AccessTools.TypeByName("AdaptiveStorage.ThingClass");
            }
        }

        protected override void TakePrintFrom(Thing t)
        {
            try
            {
                if (!this.adaptiveStorageActive || this.Prefix(t))
                {
                    t.Print(this);
                }
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

        private bool Prefix(Thing t)
        {
            if (t.def.category == ThingCategory.Item)
            {
                var storingThing = t.StoringThing();
                if (storingThing != null && this.t_Adaptive_ThingClass.IsAssignableFrom(storingThing.GetType()))
                {
                    return t == storingThing;
                }
            }
            return true;
        }

        private bool adaptiveStorageActive;

        private Type t_Adaptive_ThingClass;
    }
}
