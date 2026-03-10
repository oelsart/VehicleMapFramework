using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

public record struct TraverseSpots(TargetInfo exitSpot, TargetInfo enterSpot)
{
    public TargetInfo exitSpot = exitSpot;
    public TargetInfo enterSpot = enterSpot;
}

public class TraverseSpotsSaveLoader(TraverseSpots spots) : IExposable
{
    public TraverseSpots spots = spots;

    [UsedImplicitly]
    public TraverseSpotsSaveLoader() : this(new TraverseSpots(TargetInfo.Invalid, TargetInfo.Invalid))
    {
    }
    
    public void ExposeData()
    {
        Scribe_TargetInfo.Look(ref spots.exitSpot, nameof(spots.exitSpot));
        Scribe_TargetInfo.Look(ref spots.enterSpot, nameof(spots.enterSpot));     
    }
}