using RimWorld;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class CompVehicleSeat : CompBuildableUpgrade
    {
        new CompProperties_BuildableUpgrade Props => (CompProperties_BuildableUpgrade)this.props;

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (this.parent.IsOnVehicleMapOf(out var vehicle) && selPawn.CanReach(this.parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, this.parent.Map, out var exitSpot, out var enterSpot))
            {
                foreach (var handler in vehicle.handlers)
                {
                    if (this.Props.upgrades.Any(u =>
                    {
                        if (!(u is VehicleUpgrade vehicleUpgrade)) return false;
                        return vehicleUpgrade.roles.Any(r => r.key == handler.role.key);
                    }))
                    {
                        VehicleReservationManager cachedMapComponent = vehicle.Map.GetCachedMapComponent<VehicleReservationManager>();
                        string key = "VF_EnterVehicle";
                        NamedArgument arg = vehicle.LabelShort;
                        NamedArgument arg2 = handler.role.label;
                        int slots = handler.role.Slots;
                        int count = handler.handlers.Count;
                        VehicleHandlerReservation reservation = cachedMapComponent.GetReservation<VehicleHandlerReservation>(vehicle);
                        FloatMenuOption floatMenuOption2 = new FloatMenuOption(key.Translate(arg, arg2, (slots - (count + ((reservation != null) ? new int?(reservation.ClaimantsOnHandler(handler)) : null)).GetValueOrDefault()).ToString()), delegate ()
                        {
                            if (handler == null)
                            {
                                Messages.Message("VF_HandlerNotEnoughRoom".Translate(selPawn, vehicle), MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            Job job = new Job(VIF_DefOf.VIF_BoardAcrossMaps, this.parent);
                            vehicle.GiveLoadJob(selPawn, handler);
                            job.SetSpotsToJobAcrossMaps(selPawn, exitSpot, enterSpot);
                            selPawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.DraftedOrder), false);
                            if (!selPawn.Spawned)
                            {
                                return;
                            }
                            vehicle.Map.GetCachedMapComponent<VehicleReservationManager>().Reserve<VehicleHandler, VehicleHandlerReservation>(vehicle, selPawn, selPawn.CurJob, handler);
                        }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        yield return floatMenuOption2;
                    }
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            
            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                bool exitBlocked = !this.parent.OccupiedRect().ExpandedBy(1).EdgeCells.NotNullAndAny((IntVec3 cell) => cell.Walkable(this.parent.Map));
                foreach (var keyIDPair in base.handlerUniqueIDs)
                {
                    var handler = vehicle.handlers.FirstOrDefault(h => h.uniqueID == keyIDPair.id);
                    if (handler != null)
                    {
                        foreach (var pawn in handler.handlers)
                        {
                            Command_Action_PawnDrawer command_Action_PawnDrawer = new Command_Action_PawnDrawer();
                            command_Action_PawnDrawer.defaultLabel = "VF_DisembarkSinglePawn".Translate(pawn.LabelShort);
                            command_Action_PawnDrawer.groupable = false;
                            command_Action_PawnDrawer.pawn = pawn;
                            command_Action_PawnDrawer.action = delegate ()
                            {
                                vehicle.DisembarkPawn(pawn);
                            };
                            if (exitBlocked)
                            {
                                command_Action_PawnDrawer.Disable("VF_DisembarkNoExit".Translate());
                            }
                            yield return command_Action_PawnDrawer;
                        }
                    }
                }
            }
        }
    }
}
