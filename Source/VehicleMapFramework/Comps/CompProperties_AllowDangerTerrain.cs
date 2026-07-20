using System.Collections.Generic;
using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class CompProperties_AllowDangerTerrains : CompProperties
{
  public List<TerrainDef> allowedDangerTerrains;
  
  public CompProperties_AllowDangerTerrains()
  {
    compClass = typeof(CompAllowDangerTerrains);
  }

  public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
  {
    if (allowedDangerTerrains.NullOrEmpty())
    {
      yield return "allowedDangerTerrains is null or empty";
      yield break;
    }
    foreach (var terrain in allowedDangerTerrains)
    {
      if (!terrain.dangerous)
      {
        yield return $"terrain {terrain.defName} is not dangerous";
      }
    }
  }
}