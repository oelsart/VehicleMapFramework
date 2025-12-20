using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class CompProperties_OpacityOverlay : CompProperties
{
    public CompProperties_OpacityOverlay()
    {
        compClass = typeof(CompOpacityOverlay);
    }

    public string identifier = "";

    public string label;
}