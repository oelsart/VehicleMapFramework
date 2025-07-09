using RimWorld;
using System.Linq;
using Verse;

namespace VehicleMapFramework;

public class CompZipline : CompVehicleEnterSpot
{
    public Verb_LaunchZipline LaunchVerb
    {
        get
        {
            if (cachedLaunchVerb == null)
            {
                if (parent is Building_Turret building_Turret)
                {
                    cachedLaunchVerb = building_Turret.AttackVerb as Verb_LaunchZipline;
                }
                else if (parent is ZiplineEnd ziplineEnd)
                {
                    cachedLaunchVerb = ziplineEnd.launchVerb;
                }
                else if (parent is Pawn pawn)
                {
                    cachedLaunchVerb = pawn.VerbTracker.AllVerbs.OfType<Verb_LaunchZipline>().FirstOrDefault();
                }
                else if (parent.def.IsWeapon)
                {
                    cachedLaunchVerb = parent.TryGetComp<CompEquippable>()?.PrimaryVerb as Verb_LaunchZipline;
                }
            }
            return cachedLaunchVerb;
        }
    }

    public Thing Pair => cachedIsZiplineEnd ? LaunchVerb?.caster : LaunchVerb?.ZiplineEnd;

    public bool IsZiplineEnd => cachedIsZiplineEnd;

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

    private Verb_LaunchZipline cachedLaunchVerb;

    private bool cachedIsZiplineEnd;
}
