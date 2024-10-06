using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using VehicleInteriors.VIF_HarmonyPatches;

using RimWorld.Planet;

namespace VehicleInteriors
{
    public static class FloatMenuMakerOnVehicle
    {
        public static FloatMenuOption GotoLocationOption(IntVec3 clickCell, Pawn pawn, bool suppressAutoTakeableGoto)
        {
            if (suppressAutoTakeableGoto)
            {
                return null;
            }
            IntVec3 curLoc = ReachabilityUtilityOnVehicle.StandableCellNear(clickCell, pawn.Map, 2.9f, null, out var map);
            if (!curLoc.IsValid || !(curLoc != pawn.Position))
            {
                return null;
            }
            if (ModsConfig.BiotechActive && pawn.IsColonyMech && !MechanitorUtility.InMechanitorCommandRange(pawn, curLoc))
            {
                return new FloatMenuOption("CannotGoOutOfRange".Translate() + ": " + "OutOfCommandRange".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
            }
            if (!pawn.CanReach(curLoc, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var dest1, out var dest2))
            {
                return new FloatMenuOption("CannotGoNoPath".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
            }
            Action action = delegate ()
            {
                var cell = ReachabilityUtilityOnVehicle.BestOrderedGotoDestNear(curLoc, pawn, (IntVec3 c) => c.InBounds(map), map);
                FloatMenuMakerOnVehicle.PawnGotoAction(clickCell, pawn, dest1, dest2, new LocalTargetInfo(cell));
            };
            return new FloatMenuOption("GoHere".Translate(), action, MenuOptionPriority.GoHere, null, null, 0f, null, null, true, 0)
            {
                autoTakeable = true,
                autoTakeablePriority = 10f
            };
        }

        public static void PawnGotoAction(IntVec3 clickCell, Pawn pawn, LocalTargetInfo dest1, LocalTargetInfo dest2, LocalTargetInfo dest3)
        {
            bool flag;
            if ((!dest1.IsValid && !dest2.IsValid && pawn.Position == dest3.Cell) || (pawn.CurJobDef == VIF_DefOf.VIF_GotoAcrossMaps && pawn.CurJob.targetA == dest1 && pawn.CurJob.targetB == dest2 && pawn.CurJob.targetC == dest3))
            {
                flag = true;
            }
            else
            {
                var baseMap = pawn.BaseMapOfThing();
                Job job = JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps, dest1, dest2, dest3);
                if (baseMap.exitMapGrid.IsExitCell(clickCell))
                {
                    job.exitMapOnArrival = !pawn.IsColonyMech;
                }
                else if (!baseMap.IsPlayerHome && !baseMap.exitMapGrid.MapUsesExitGrid && CellRect.WholeMap(baseMap).IsOnEdge(clickCell, 3) && baseMap.Parent.GetComponent<FormCaravanComp>() != null && MessagesRepeatAvoider.MessageShowAllowed("MessagePlayerTriedToLeaveMapViaExitGrid-" + baseMap.uniqueID, 60f))
                {
                    if (baseMap.Parent.GetComponent<FormCaravanComp>().CanFormOrReformCaravanNow)
                    {
                        Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CanReform".Translate(), baseMap.Parent, MessageTypeDefOf.RejectInput, false);
                    }
                    else
                    {
                        Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CantReform".Translate(), baseMap.Parent, MessageTypeDefOf.RejectInput, false);
                    }
                }
                flag = pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
            }
            if (flag)
            {
                FleckMaker.Static(dest3.Cell, pawn.Map, FleckDefOf.FeedbackGoto, 1f);
            }
        }
    }
}
