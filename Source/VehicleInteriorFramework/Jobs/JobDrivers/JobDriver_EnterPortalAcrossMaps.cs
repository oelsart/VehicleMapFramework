using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleInteriors
{
    public class JobDriver_EnterPortalAcrossMaps : JobDriverAcrossMaps
    {
        public MapPortal MapPortal
        {
            get
            {
                return base.TargetThingA as MapPortal;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(this.PortalInd);
            this.FailOn(delegate ()
            {
                string text;
                return !(base.TargetThingA as MapPortal).IsEnterable(out text);
            });
            if (this.ShouldEnterTargetAMap)
            {
                foreach(var toil4 in this.GotoTargetMap(this.PortalInd)) yield return toil4;
            }
            yield return Toils_Goto.GotoThing(this.PortalInd, PathEndMode.Touch, false);
            Toil toil = Toils_General.Wait(90, TargetIndex.None).FailOnCannotTouch(this.PortalInd, PathEndMode.Touch).WithProgressBarToilDelay(this.PortalInd, true, -0.5f);
            Toil toil2 = toil;
            toil2.tickAction = (Action)Delegate.Combine(toil2.tickAction, new Action(delegate ()
            {
                this.pawn.rotationTracker.FaceTarget(base.TargetA);
            }));
            toil.handlingFacing = true;
            yield return toil;
            Toil toil3 = ToilMaker.MakeToil("MakeNewToils");
            toil3.initAction = delegate ()
            {
                MapPortal mapPortal = base.TargetThingA as MapPortal;
                Map otherMap = mapPortal.GetOtherMap();
                IntVec3 intVec = mapPortal.GetDestinationLocation();
                if (!intVec.Standable(otherMap))
                {
                    intVec = CellFinder.StandableCellNear(intVec, otherMap, 5f, null);
                }
                if (intVec == IntVec3.Invalid)
                {
                    Messages.Message("UnableToEnterPortal".Translate(base.TargetThingA.Label), base.TargetThingA, MessageTypeDefOf.NegativeEvent, true);
                    return;
                }
                bool drafted = this.pawn.Drafted;
                this.pawn.DeSpawnOrDeselect(DestroyMode.Vanish);
                GenSpawn.Spawn(this.pawn, intVec, otherMap, Rot4.Random, WipeMode.Vanish, false, false);
                mapPortal.OnEntered(this.pawn);
                if (!otherMap.IsPocketMap)
                {
                    this.pawn.inventory.UnloadEverything = true;
                }
                if (drafted || mapPortal.AutoDraftOnEnter)
                {
                    this.pawn.drafter.Drafted = true;
                }
                if (this.pawn.carryTracker.CarriedThing != null && !this.pawn.Drafted)
                {
                    Thing thing;
                    this.pawn.carryTracker.TryDropCarriedThing(this.pawn.Position, ThingPlaceMode.Direct, out thing, null);
                }
                Lord lord = this.pawn.GetLord();
                if (lord != null)
                {
                    lord.Notify_PawnLost(this.pawn, PawnLostCondition.ExitedMap, null);
                }
            };
            yield return toil3;
        }

        private TargetIndex PortalInd = TargetIndex.A;

        private const int EnterDelay = 90;
    }
}
