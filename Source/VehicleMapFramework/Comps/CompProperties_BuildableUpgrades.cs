using System.Collections.Generic;
using JetBrains.Annotations;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class CompProperties_BuildableUpgrades : CompProperties
{
    public List<Upgrade> upgrades;
    
    public CompProperties_BuildableUpgrades()
    {
        compClass = typeof(CompBuildableUpgrades);
    }
}
