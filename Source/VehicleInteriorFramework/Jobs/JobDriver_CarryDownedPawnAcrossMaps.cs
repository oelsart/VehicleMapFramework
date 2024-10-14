using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace VehicleInteriors
{
    public class JobDriver_CarryDownedPawnAcrossMaps : JobDriverAcrossMaps
    {
        protected Pawn Takee
        {
            get
            {
                return (Pawn)this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Takee.Map, this.Takee, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnAggroMentalStateAndHostile(TargetIndex.A);
            if (this.ShouldEnterTargetAMap)
            {
                foreach(var toil2 in this.GotoTargetMap(TakeeIndex)) yield return toil2;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, false).FailOnDespawnedNullOrForbidden(TargetIndex.A).FailOn(() => !this.Takee.Downed).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            Toil toil = Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true, false);
            toil.AddPreInitAction(new Action(this.CheckMakeTakeeGuest));
            yield return toil;
            yield break;
        }

        private void CheckMakeTakeeGuest()
        {
            if (!this.job.def.makeTargetPrisoner && !this.Takee.HostileTo(Faction.OfPlayer) && this.Takee.Faction != Faction.OfPlayer && this.Takee.HostFaction != Faction.OfPlayer && this.Takee.guest != null && !this.Takee.IsWildMan())
            {
                this.Takee.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
            }
        }

        private const TargetIndex TakeeIndex = TargetIndex.A;
    }
}
