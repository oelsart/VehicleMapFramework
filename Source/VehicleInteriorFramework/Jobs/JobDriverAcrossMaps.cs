using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public abstract class JobDriverAcrossMaps : JobDriver
    {
        public LocalTargetInfo ExitSpot1 { get; set; }

        public LocalTargetInfo EnterSpot1 { get; set; }

        public LocalTargetInfo ExitSpot2 { get; set; }

        public LocalTargetInfo EnterSpot2 { get; set; }

        public bool ShouldEnterTargetAMap => this.ExitSpot1.HasThing || this.EnterSpot1.HasThing;

        public bool ShouldEnterTargetBMap => this.ExitSpot2.HasThing || this.EnterSpot2.HasThing;

        public Map DestMap
        {
            get
            {
                if (this.EnterSpot2.HasThing) return this.EnterSpot2.Thing.Map;
                if (this.ExitSpot2.HasThing) return this.ExitSpot2.Thing.BaseMapOfThing();
                if (this.EnterSpot1.HasThing) return this.EnterSpot1.Thing.Map;
                if (this.ExitSpot1.HasThing) return this.ExitSpot1.Thing.BaseMapOfThing();
                return base.Map;
            }
        }

        public override Vector3 ForcedBodyOffset
        {
            get
            {
                return this.drawOffset;
            }
        }

        public void SetSpots(LocalTargetInfo exitSpot1 = default, LocalTargetInfo enterSpot1 = default, LocalTargetInfo exitSpot2 = default, LocalTargetInfo enterSpot2 = default)
        {
            this.ExitSpot1 = exitSpot1;
            this.EnterSpot1 = enterSpot1;
            this.ExitSpot2 = exitSpot2;
            this.EnterSpot2 = enterSpot2;
        }

        public IEnumerable<Toil> GotoTargetMap(TargetIndex ind)
        {
            if (ind == TargetIndex.A)
            {
                var exitSpot = this.ExitSpot1;
                var enterSpot = this.EnterSpot1;
                this.ExitSpot1 = default;
                this.EnterSpot1 = default;
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot, enterSpot);
            }
            if (ind == TargetIndex.B)
            {
                var exitSpot = this.ExitSpot2;
                var enterSpot = this.EnterSpot2;
                this.ExitSpot2 = default;
                this.EnterSpot2 = default;
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot, enterSpot);
            }
            Log.Error("[VehicleInteriors] GotoTargetMap() does not support TargetIndex.C.");
            return null;
        }

        public Vector3 drawOffset;
    }
}