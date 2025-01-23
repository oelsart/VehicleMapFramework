using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class CompVehicleSeat : CompBuildableUpgrades
    {
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (this.parent.IsOnVehicleMapOf(out var vehicle) && selPawn.CanReach(this.parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, this.parent.Map, out var exitSpot, out var enterSpot))
            {
                foreach (var handler in vehicle.handlers)
                {
                    if (handler.AreSlotsAvailable && this.handlerUniqueIDs.Any(h => h.id == handler.uniqueID))
                    {
                        VehicleReservationManager cachedMapComponent = vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>();
                        string key = "VF_EnterVehicle";
                        NamedArgument arg = vehicle.LabelShort;
                        NamedArgument arg2 = handler.role.label;
                        int slots = handler.role.Slots;
                        int count = handler.handlers.Count;
                        VehicleHandlerReservation reservation = cachedMapComponent?.GetReservation<VehicleHandlerReservation>(vehicle);
                        FloatMenuOption floatMenuOption2 = new FloatMenuOption(key.Translate(arg, arg2, (slots - (count + ((reservation != null) ? new int?(reservation.ClaimantsOnHandler(handler)) : null)).GetValueOrDefault()).ToString()), delegate ()
                        {
                            if (handler == null)
                            {
                                Messages.Message("VF_HandlerNotEnoughRoom".Translate(selPawn, vehicle), MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            Job job = new Job(VMF_DefOf.VMF_BoardAcrossMaps, this.parent).SetSpotsToJobAcrossMaps(selPawn, exitSpot, enterSpot);
                            vehicle.GiveLoadJob(selPawn, handler);
                            selPawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.DraftedOrder), false);
                            if (!selPawn.Spawned)
                            {
                                return;
                            }
                            vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>().Reserve<VehicleHandler, VehicleHandlerReservation>(vehicle, selPawn, selPawn.CurJob, handler);
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
                foreach (var keyIDPair in this.handlerUniqueIDs)
                {
                    var handler = vehicle.handlers.FirstOrDefault(h => h.uniqueID == keyIDPair.id);
                    if (handler != null)
                    {
                        foreach (var pawn in handler.handlers)
                        {
                            if (vehicle.Drafted && handler.role.HandlingTypes.HasFlag(HandlingTypeFlags.Movement)) continue;

                            Command_Action_PawnDrawer command_Action_PawnDrawer = new Command_Action_PawnDrawer
                            {
                                defaultLabel = "VF_DisembarkSinglePawn".Translate(pawn.LabelShort),
                                groupable = false,
                                pawn = pawn,
                                action = delegate ()
                                {
                                    var caravan = pawn.GetCaravan();
                                    caravan?.RemovePawn(pawn);
                                    if (Find.WorldPawns.Contains(pawn))
                                    {
                                        Find.WorldPawns.RemovePawn(pawn);
                                    }
                                    vehicle.DisembarkPawn(pawn);
                                }
                            };
                            if (exitBlocked)
                            {
                                command_Action_PawnDrawer.Disable("VF_DisembarkNoExit".Translate());
                            }
                            yield return command_Action_PawnDrawer;
                        }
                    }
                }

                foreach (var gizmo in vehicle.AllComps.Where(c => c is CompTogglableOverlays).SelectMany(c => c.CompGetGizmosExtra()))
                {
                    yield return gizmo;
                }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                vehicle.CompVehicleTurrets?.RecacheTurretPermissions();
                vehicle.RecachePawnCount();
            }
        }
    }
}
