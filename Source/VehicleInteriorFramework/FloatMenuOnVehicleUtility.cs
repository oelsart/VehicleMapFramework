﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace VehicleInteriors
{
    public static class FloatMenuOnVehicleUtility
    {
        public static Action GetRangedAttackAction(Pawn pawn, LocalTargetInfo target, out string failStr)
        {
            failStr = "";
            if (pawn.equipment.Primary == null)
            {
                return null;
            }
            Verb primaryVerb = pawn.equipment.PrimaryEq.PrimaryVerb;
            if (primaryVerb.verbProps.IsMeleeAttack)
            {
                return null;
            }
            Pawn target2;
            Pawn victim;
            if (!pawn.Drafted)
            {
                failStr = "IsNotDraftedLower".Translate(pawn.LabelShort, pawn);
            }
            else if (!pawn.IsColonistPlayerControlled && !pawn.IsColonyMech && !pawn.IsColonyMutantPlayerControlled)
            {
                failStr = "CannotOrderNonControlledLower".Translate();
            }
            else if (pawn.IsColonyMechPlayerControlled && target.IsValid && !MechanitorUtility.InMechanitorCommandRange(pawn, target))
            {
                failStr = "OutOfCommandRange".Translate();
            }
            else if (target.IsValid && !pawn.equipment.PrimaryEq.PrimaryVerb.CanHitTarget(target))
            {
                if (!pawn.PositionOnBaseMap().InHorDistOf(target.CellOnBaseMap(), primaryVerb.verbProps.range))
                {
                    failStr = "OutOfRange".Translate();
                }
                else
                {
                    float num = primaryVerb.verbProps.EffectiveMinRange(target, pawn);
                    if ((float)pawn.PositionOnBaseMap().DistanceToSquared(target.CellOnBaseMap()) < num * num)
                    {
                        failStr = "TooClose".Translate();
                    }
                    else
                    {
                        failStr = "CannotHitTarget".Translate();
                    }
                }
            }
            else if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                failStr = "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn);
            }
            else if (pawn == target.Thing)
            {
                failStr = "CannotAttackSelf".Translate();
            }
            else if ((target2 = (target.Thing as Pawn)) != null && (pawn.InSameExtraFaction(target2, ExtraFactionType.HomeFaction, null) || pawn.InSameExtraFaction(target2, ExtraFactionType.MiniFaction, null)))
            {
                failStr = "CannotAttackSameFactionMember".Translate();
            }
            else if ((victim = (target.Thing as Pawn)) != null && HistoryEventUtility.IsKillingInnocentAnimal(pawn, victim) && !new HistoryEvent(HistoryEventDefOf.KilledInnocentAnimal, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                failStr = "IdeoligionForbids".Translate();
            }
            else
            {
                Pawn pawn2;
                if ((pawn2 = (target.Thing as Pawn)) == null || pawn.Ideo == null || !pawn.Ideo.IsVeneratedAnimal(pawn2) || new HistoryEvent(HistoryEventDefOf.HuntedVeneratedAnimal, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                {
                    return delegate ()
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                        pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
                    };
                }
                failStr = "IdeoligionForbids".Translate();
            }
            failStr = failStr.CapitalizeFirst();
            return null;
        }

        public static Action GetMeleeAttackAction(Pawn pawn, LocalTargetInfo target, out string failStr)
        {
            failStr = "";
            Pawn target2;
            var map = target.HasThing ? target.Thing.Map : pawn.BaseMapOfThing();
            var exitSpot = LocalTargetInfo.Invalid;
            var enterSpot = LocalTargetInfo.Invalid;
            if (!pawn.Drafted)
            {
                failStr = "IsNotDraftedLower".Translate(pawn.LabelShort, pawn);
            }
            else if (!pawn.IsColonistPlayerControlled && !pawn.IsColonyMech && !pawn.IsColonyMutantPlayerControlled)
            {
                failStr = "CannotOrderNonControlledLower".Translate();
            }
            else if (pawn.IsColonyMechPlayerControlled && target.IsValid && !MechanitorUtility.InMechanitorCommandRange(pawn, target))
            {
                failStr = "OutOfCommandRange".Translate();
            }
            else if (target.IsValid && !pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out exitSpot, out enterSpot))
            {
                failStr = "NoPath".Translate();
            }
            else if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                failStr = "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn);
            }
            else if (pawn.meleeVerbs.TryGetMeleeVerb(target.Thing) == null)
            {
                failStr = "Incapable".Translate();
            }
            else if (pawn == target.Thing)
            {
                failStr = "CannotAttackSelf".Translate();
            }
            else if ((target2 = (target.Thing as Pawn)) != null && (pawn.InSameExtraFaction(target2, ExtraFactionType.HomeFaction, null) || pawn.InSameExtraFaction(target2, ExtraFactionType.MiniFaction, null)))
            {
                failStr = "CannotAttackSameFactionMember".Translate();
            }
            else
            {
                Pawn pawn2;
                if ((pawn2 = (target.Thing as Pawn)) == null || !pawn2.RaceProps.Animal || !HistoryEventUtility.IsKillingInnocentAnimal(pawn, pawn2) || new HistoryEvent(HistoryEventDefOf.KilledInnocentAnimal, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                {
                    return delegate ()
                    {
                        Job job = JobMaker.MakeJob(VIF_DefOf.VIF_AttackMeleeAcrossMaps, exitSpot, enterSpot, target);
                        Pawn pawn3 = target.Thing as Pawn;
                        if (pawn3 != null)
                        {
                            job.killIncappedTarget = pawn3.Downed;
                        }
                        pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
                    };
                }
                failStr = "IdeoligionForbids".Translate();
            }
            failStr = failStr.CapitalizeFirst();
            return null;
        }
    }
}