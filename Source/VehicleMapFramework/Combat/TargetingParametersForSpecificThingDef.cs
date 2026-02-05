using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class TargetingParametersForSpecificThingDef : TargetingParameters
{
    public ThingDef thingDef;
    
    public void PostLoad()
    {
        validator = targetInfo => targetInfo.Thing?.def == thingDef;
    }
}