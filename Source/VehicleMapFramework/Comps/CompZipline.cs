using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class CompZipline : CompVehicleEnterSpot
{
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

    public Thing Pair => IsZiplineEnd ? LaunchVerb?.caster : LaunchVerb?.ZiplineEnd;

    public bool IsZiplineEnd { get; private set; }

    public override bool Available => Pair is { Spawned: true };

    public override IntVec3 EnterVehiclePosition => Pair?.Position ?? IntVec3.Invalid;

    public override float DistanceSquared(IntVec3 root)
    {
        return (Pair?.PositionOnBaseMap - root)?.LengthHorizontalSquared ?? float.MaxValue;
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        IsZiplineEnd = parent is ZiplineEnd;
    }

    public override void PostDraw()
    {
        if (!IsZiplineEnd)
        {
            var ziplineEndThing = LaunchVerb?.ZiplineEnd;
            switch (ziplineEndThing)
            {
                case IZiplineEnd ziplineEnd:
                    ziplineEnd.DrawZipline(ziplineEndThing.DrawPos);
                    break;
                case null when Props.standbyGraphic != null:
                    Graphics.DrawMesh(MeshPool.plane10, parent.DrawPos,
                        Quaternion.AngleAxis((parent as Building_TurretGun)?.Top?.CurRotation ?? 0f, Vector3.up),
                        Props.standbyGraphic.Graphic.MatSingleFor(parent), 0);
                    break;
            }
        }
    }
}
