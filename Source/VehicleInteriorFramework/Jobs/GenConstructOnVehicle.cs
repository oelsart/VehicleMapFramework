using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class GenConstructOnVehicle
    {
        public const float ConstructionSpeedGlobalFactor = 1.7f;

        private static string SkillTooLowTrans = "SkillTooLowForConstruction".Translate();

        private static string IncapableOfDeconstruction = "IncapableOfDeconstruction".Translate();

        //private static string IncapableOfMining = "IncapableOfMining".Translate();

        private static string TreeMarkedForExtraction = "TreeMarkedForExtraction".Translate();

        private static readonly List<string> tmpIdeoMemberNames = new List<string>();

        public static bool CanConstruct(Thing t, Pawn pawn, WorkTypeDef workType, bool forced, JobDef jobForReservation, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            if (!forced && !pawn.workSettings.WorkIsActive(workType))
            {
                JobFailReason.Is("NotAssignedToWorkType".Translate(workType.gerundLabel).CapitalizeFirst());
                exitSpot = TargetInfo.Invalid;
                enterSpot = TargetInfo.Invalid;
                return false;
            }

            return CanConstruct(t, pawn, workType == WorkTypeDefOf.Construction, forced, jobForReservation, out exitSpot, out enterSpot);
        }

        public static bool CanConstruct(Thing t, Pawn p, bool checkSkills, bool forced, JobDef jobForReservation, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            GenConstructOnVehicle.tmpIdeoMemberNames.Clear();
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            if (GenConstruct.FirstBlockingThing(t, p) != null)
            {
                return false;
            }

            if (jobForReservation != null)
            {
                if (!p.Spawned)
                {
                    return false;
                }

                if (!t.Map.reservationManager.OnlyReservationsForJobDef(t, jobForReservation))
                {
                    return false;
                }

                if (!p.CanReach(t, PathEndMode.Touch, forced ? Danger.Deadly : p.NormalMaxDanger(), false, false, TraverseMode.ByPawn, t.Map, out exitSpot, out enterSpot))
                {
                    JobFailReason.Is("NoPath".Translate());
                    return false;
                }
            }
            else if (!p.CanReserveAndReach(t.Map, t, PathEndMode.Touch, forced ? Danger.Deadly : p.NormalMaxDanger(), 1, -1, null, forced, out exitSpot, out enterSpot))
            {
                return false;
            }

            if (t.IsBurning())
            {
                return false;
            }

            if (checkSkills)
            {
                if (p.skills != null)
                {
                    if (p.skills.GetSkill(SkillDefOf.Construction).Level < t.def.constructionSkillPrerequisite)
                    {
                        JobFailReason.Is(SkillTooLowTrans.Formatted(SkillDefOf.Construction.LabelCap));
                        return false;
                    }

                    if (p.skills.GetSkill(SkillDefOf.Artistic).Level < t.def.artisticSkillPrerequisite)
                    {
                        JobFailReason.Is(SkillTooLowTrans.Formatted(SkillDefOf.Artistic.LabelCap));
                        return false;
                    }
                }

                if (p.IsColonyMech)
                {
                    if (p.RaceProps.mechFixedSkillLevel < t.def.constructionSkillPrerequisite)
                    {
                        JobFailReason.Is(SkillTooLowTrans.Formatted(SkillDefOf.Construction.LabelCap));
                        return false;
                    }

                    if (p.RaceProps.mechFixedSkillLevel < t.def.artisticSkillPrerequisite)
                    {
                        JobFailReason.Is(SkillTooLowTrans.Formatted(SkillDefOf.Artistic.LabelCap));
                        return false;
                    }
                }
            }

            bool flag = t is Blueprint_Install;
            if (p.Ideo != null && !p.Ideo.MembersCanBuild(t) && !flag)
            {
                foreach (Ideo item in Find.IdeoManager.IdeosListForReading)
                {
                    if (item.MembersCanBuild(t))
                    {
                        tmpIdeoMemberNames.Add(item.memberName);
                    }
                }

                if (tmpIdeoMemberNames.Any())
                {
                    JobFailReason.Is("OnlyMembersCanBuild".Translate(tmpIdeoMemberNames.ToCommaList(useAnd: true)));
                }

                return false;
            }

            if ((t.def.IsBlueprint || t.def.IsFrame) && t.def.entityDefToBuild is ThingDef thingDef)
            {
                if (thingDef.building != null && thingDef.building.isAttachment)
                {
                    Thing wallAttachedTo = GenConstruct.GetWallAttachedTo(t);
                    if (wallAttachedTo == null || wallAttachedTo.def.IsBlueprint || wallAttachedTo.def.IsFrame)
                    {
                        return false;
                    }
                }

                NamedArgument arg = p.Named(HistoryEventArgsNames.Doer);
                if (!new HistoryEvent(HistoryEventDefOf.BuildSpecificDef, arg, NamedArgumentUtility.Named(thingDef, HistoryEventArgsNames.Building)).Notify_PawnAboutToDo_Job())
                {
                    return false;
                }

                if (thingDef.building != null && thingDef.building.IsTurret && !thingDef.HasComp(typeof(CompMannable)) && !new HistoryEvent(HistoryEventDefOf.BuiltAutomatedTurret, arg).Notify_PawnAboutToDo_Job())
                {
                    return false;
                }
            }

            return true;
        }

        public static Job HandleBlockingThingJob(Thing constructible, Pawn worker, bool forced = false)
        {
            Thing thing = GenConstruct.FirstBlockingThing(constructible, worker);
            if (thing == null)
            {
                return null;
            }

            if (thing.def.category == ThingCategory.Plant)
            {
                if (!PlantUtility.PawnWillingToCutPlant_Job(thing, worker))
                {
                    return null;
                }

                if (PlantUtility.TreeMarkedForExtraction(thing))
                {
                    JobFailReason.Is(TreeMarkedForExtraction);
                    return null;
                }

                if (worker.CanReserveAndReach(thing.Map, thing, PathEndMode.ClosestTouch, worker.NormalMaxDanger(), 1, -1, null, forced, out var exitSpot, out var enterSpot))
                {
                    return JobAcrossMapsUtility.GotoDestMapJob(worker, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.CutPlant, thing));
                }
            }
            else if (thing.def.category == ThingCategory.Item)
            {
                if (thing.def.EverHaulable)
                {
                    return HaulAIAcrossMapsUtility.HaulAsideJobFor(worker, thing);
                }

                Log.ErrorOnce(string.Concat("Never haulable ", thing, " blocking ", constructible.ToStringSafe(), " at ", constructible.Position), 6429262);
            }
            else if (thing.def.category == ThingCategory.Building)
            {
                if (((Building)thing).DeconstructibleBy(worker.Faction))
                {
                    if (worker.WorkTypeIsDisabled(WorkTypeDefOf.Construction) || (worker.workSettings != null && !worker.workSettings.WorkIsActive(WorkTypeDefOf.Construction)))
                    {
                        JobFailReason.Is(IncapableOfDeconstruction);
                        return null;
                    }

                    if (worker.CanReserveAndReach(thing.Map, thing, PathEndMode.Touch, worker.NormalMaxDanger(), 1, -1, null, forced, out var exitSpot, out var enterSpot))
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Deconstruct, thing);
                        job.ignoreDesignations = true;
                        return JobAcrossMapsUtility.GotoDestMapJob(worker, exitSpot, enterSpot, job);
                    }
                }

                if (thing.def.mineable && worker.CanReserveAndReach(thing.Map, thing, PathEndMode.Touch, worker.NormalMaxDanger(), 1, -1, null, forced, out var exitSpot2, out var enterSpot2))
                {
                    Job job2 = JobMaker.MakeJob(JobDefOf.Mine, thing);
                    job2.ignoreDesignations = true;
                    return JobAcrossMapsUtility.GotoDestMapJob(worker, exitSpot2, enterSpot2, job2);
                }
            }

            return null;
        }
    }
}
