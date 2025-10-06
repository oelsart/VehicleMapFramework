using System.Linq;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CompZipline : CompVehicleEnterSpot
{
    public Verb_LaunchZipline LaunchVerb
    {
        get
        {
            if (field == null)
            {
                if (parent is Building_Turret building_Turret)
                {
                    field = building_Turret.AttackVerb as Verb_LaunchZipline;
                }
                else if (parent is ZiplineEnd ziplineEnd)
                {
                    field = ziplineEnd.launchVerb;
                }
                else if (parent is Pawn pawn)
                {
                    field = pawn.VerbTracker.AllVerbs.OfType<Verb_LaunchZipline>().FirstOrDefault();
                }
                else if (parent.def.IsWeapon)
                {
                    field = parent.TryGetComp<CompEquippable>()?.PrimaryVerb as Verb_LaunchZipline;
                }
            }
            return field;
        }
    }

    public Thing Pair => cachedIsZiplineEnd ? LaunchVerb?.caster : LaunchVerb?.ZiplineEnd;

    public bool IsZiplineEnd => cachedIsZiplineEnd;

    public override bool Available => Pair?.Spawned ?? false;

    public override IntVec3 EnterVehiclePosition => Pair?.Position ?? IntVec3.Invalid;

    public override float DistanceSquared(IntVec3 root)
    {
        return (Pair?.PositionOnBaseMap() - root)?.LengthHorizontalSquared ?? float.MaxValue;
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        cachedIsZiplineEnd = parent is ZiplineEnd;
    }

    public override void PostDraw()
    {
        var ziplineEndThing = LaunchVerb?.ZiplineEnd;
        if (!IsZiplineEnd && ziplineEndThing is IZiplineEnd ziplineEnd)
        {
            ziplineEnd.DrawZipline(ziplineEndThing.DrawPos);
        }
    }

    private bool cachedIsZiplineEnd;
}
