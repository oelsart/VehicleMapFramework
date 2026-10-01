using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class CompZipline : CompVehicleEnterSpot
{
  private Region regionA;
  private Region regionB;
  
  public new CompProperties_Zipline Props => (CompProperties_Zipline)props;

  public Verb_LaunchZipline LaunchVerb
  {
    get
    {
      if (field == null)
      {
        switch (parent)
        {
          case Building_Turret building_Turret:
            field = building_Turret.AttackVerb as Verb_LaunchZipline;
            break;
          case ZiplineEnd ziplineEnd:
            field = ziplineEnd.launchVerb;
            break;
          case Pawn pawn:
            field = pawn.VerbTracker.AllVerbs.OfType<Verb_LaunchZipline>().FirstOrDefault();
            break;
          default:
          {
            if (parent.def.IsWeapon)
            {
              field = parent.TryGetComp<CompEquippable>()?.PrimaryVerb as Verb_LaunchZipline;
            }

            break;
          }
        }
      }
      return field;
    }
  }

  public Thing Pair => IsZiplineEnd ? LaunchVerb?.caster : LaunchVerb?.ziplineEnd;

  public bool IsZiplineEnd { get; private set; }

  protected override bool Available => Pair is { Spawned: true };

  public override bool ShouldOffsetOnEdge => false;

  protected override TargetInfo AccessSpot => Pair ?? TargetInfo.Invalid;

  public override float MovePerTick(Pawn pawn) => (IsZiplineEnd ? 0.5f : 1f) / pawn.TicksPerMoveCardinal;

  public override void PostSpawnSetup(bool respawningAfterLoad)
  {
    base.PostSpawnSetup(respawningAfterLoad);
    IsZiplineEnd = parent is ZiplineEnd;
    if (!IsZiplineEnd) return;
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      if (Find.World.GetComponent<CrossMapRegionLinks>() is not { } component ||
          Pair is not { Spawned: true })
        return;
      
      regionA = parent.GetRegion();
      regionB = Pair.GetRegion();
      component.AddLink(regionA, regionB);
    });
  }

  public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
  {
    base.PostDeSpawn(map, mode);
    Find.World.GetComponent<CrossMapRegionLinks>()?.RemoveLink(regionA, regionB);
  }

  public override void PostDraw()
  {
    if (!IsZiplineEnd)
    {
      var ziplineEndThing = LaunchVerb?.ziplineEnd;
      switch (ziplineEndThing)
      {
        case IZiplineEnd ziplineEnd:
          ziplineEnd.DrawZipline(ziplineEndThing.DrawPos);
          break;
        case null when Props.standbyGraphic != null:
          Graphics.DrawMesh(MeshPool.plane10,
            parent.DrawPos,
            Quaternion.AngleAxis((parent as Building_TurretGun)?.Top?.CurRotation ?? 0f, Vector3.up),
            Props.standbyGraphic.Graphic.MatSingleFor(parent),
            0);
          break;
      }
    }
  }
}
