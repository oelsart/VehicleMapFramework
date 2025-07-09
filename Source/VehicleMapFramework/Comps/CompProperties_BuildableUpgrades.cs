using System.Collections.Generic;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompProperties_BuildableUpgrades : CompProperties
{
    public CompProperties_BuildableUpgrades()
    {
        compClass = typeof(CompBuildableUpgrades);
    }

    public List<Upgrade> upgrades;
}
