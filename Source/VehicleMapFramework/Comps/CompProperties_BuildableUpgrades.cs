using System.Collections.Generic;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompProperties_BuildableUpgrades : CompProperties
{
    public List<Upgrade> upgrades;
    
    public CompProperties_BuildableUpgrades()
    {
        compClass = typeof(CompBuildableUpgrades);
    }
}
