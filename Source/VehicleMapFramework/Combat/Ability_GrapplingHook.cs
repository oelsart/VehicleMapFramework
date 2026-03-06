using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class Ability_GrapplingHook : Ability_MapTraverse
{
    public Ability_GrapplingHook()
    {
    }

    public Ability_GrapplingHook(Pawn pawn) : base(pawn)
    {
    }

    public Ability_GrapplingHook(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept)
    {
    }

    public Ability_GrapplingHook(Pawn pawn, AbilityDef def) : base(pawn, def)
    {
    }

    public Ability_GrapplingHook(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def)
    {
    }

    public override AcceptanceReport CanCast
    {
        get
        {
            var canCast = base.CanCast;
            if (!canCast.Accepted || verb is not Verb_LaunchZipline launchVerb)
                return canCast;
            return launchVerb.ziplineEnd is not null
                ? "VMF_GrapplingHookAlreadyLaunched".Translate()
                : AcceptanceReport.WasAccepted;
        }
    }

    public virtual void OnHit(ZiplineEnd ziplineEnd)
    {
        if (pawn.TargetMap != ziplineEnd.Map)
            pawn.TargetMap = ziplineEnd.Map;
        if (!JumpUtility.DoJump(pawn, ziplineEnd, null, verb.verbProps, this, ziplineEnd,
                VMF_DefOf.VMF_GrapplingHookFlyer) || pawn.ParentHolder is not PawnFlyer_PersistentJob pawnFlyer) return;
        pawnFlyer.OnLanded += OnLanded;
    }

    protected virtual void OnLanded()
    {
        if (verb is Verb_LaunchZipline launchVerb)
            launchVerb.ziplineEnd = null;
    }
}