using Verse;

namespace VehicleMapFramework
{
  public class VehicleMapProps_Gravship : VehicleMapProps_Unique, IExposable
  {
    // Will be removed
    public string defName;

    public void ExposeData()
    {
      Scribe_Defs.Look(ref baseDef, "baseDef");
      Scribe_Values.Look(ref offset, "offset");
      Scribe_Values.Look(ref size, "size");
      Scribe_Collections.Look(ref outOfBoundsCells, "outOfBoundsCells");

      if (Scribe.mode == LoadSaveMode.LoadingVars && baseDef != null)
      {
        Scribe_Values.Look(ref defName, "defName");
      }
    }
  }
}