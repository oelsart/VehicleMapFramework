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

        private static readonly string SkillTooLowTrans;

        private static readonly List<string> tmpIdeoMemberNames = new List<string>();

        public static bool CanConstruct(Thing t, Pawn pawn, WorkTypeDef workType, bool forced = false, JobDef jobForReservation = null)
        {
            if (!forced && !pawn.workSettings.WorkIsActive(workType))
            {
                JobFailReason.Is("NotAssignedToWorkType".Translate(workType.gerundLabel).CapitalizeFirst());
                return false;
            }

            return CanConstruct(t, pawn, workType == WorkTypeDefOf.Construction, forced, jobForReservation);
        }

        public static bool CanConstruct(Thing t, Pawn p, bool checkSkills = true, bool forced = false, JobDef jobForReservation = null)
        {
            GenConstructOnVehicle.tmpIdeoMemberNames.Clear();
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

                if (!p.CanReach(t, PathEndMode.Touch, forced ? Danger.Deadly : p.NormalMaxDanger(), false, false, TraverseMode.ByPawn, t.Map, out _, out _))
                {
                    JobFailReason.Is("NoPath".Translate());
                    return false;
                }
            }
            else if (!p.CanReserveAndReach(t.Map, t, PathEndMode.Touch, forced ? Danger.Deadly : p.NormalMaxDanger(), 1, -1, null, forced, out _, out _))
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
    }
}
