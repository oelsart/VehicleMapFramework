using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SmashTools;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class CompAllowDangerTerrains : ThingComp
{
  public static readonly ConditionalWeakTable<Pawn, List<TerrainDef>> AllowedTerrains = [];

  static CompAllowDangerTerrains()
  {
    GameEvent.OnGameDisposed += AllowedTerrains.Clear;
  }
  
  public CompProperties_AllowDangerTerrains Props => (CompProperties_AllowDangerTerrains)props;

  public override void Notify_Equipped(Pawn pawn)
  {
    AllowedTerrains.AddOrUpdate(pawn, Props.allowedDangerTerrains);
  }

  public override void Notify_Unequipped(Pawn pawn)
  {
    AllowedTerrains.Remove(pawn);
  }
}