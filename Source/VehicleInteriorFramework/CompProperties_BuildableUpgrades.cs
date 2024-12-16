using System.Collections.Generic;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class CompProperties_BuildableUpgrades : CompProperties
    {
        public CompProperties_BuildableUpgrades()
        {
            this.compClass = typeof(CompBuildableUpgrades);
        }

        public List<Upgrade> upgrades;
    }
}
