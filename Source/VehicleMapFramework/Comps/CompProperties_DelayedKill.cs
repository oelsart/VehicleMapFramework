using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class CompProperties_DelayedKill : CompProperties
{
    public int delayTicks = 480;

    public EffecterDef effecterDef;

    [MustTranslate]
    public string message;
    
    public CompProperties_DelayedKill()
    {
        compClass = typeof(CompDelayedKill);
    }
}