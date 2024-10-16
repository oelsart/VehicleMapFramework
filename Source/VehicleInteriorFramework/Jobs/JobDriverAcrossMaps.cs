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

        public Map TargetAMap
        {
            get
            {
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

        public void SetSpots(LocalTargetInfo? exitSpot1 = null, LocalTargetInfo? enterSpot1 = null, LocalTargetInfo? exitSpot2 = null, LocalTargetInfo? enterSpot2 = null)
        {
            this.ExitSpot1 = exitSpot1.HasValue ? exitSpot1.Value : LocalTargetInfo.Invalid;
            this.EnterSpot1 = enterSpot1.HasValue ? enterSpot1.Value : LocalTargetInfo.Invalid;
            this.ExitSpot2 = exitSpot2.HasValue ? exitSpot2.Value : LocalTargetInfo.Invalid;
            this.EnterSpot2 = enterSpot2.HasValue ? enterSpot2.Value : LocalTargetInfo.Invalid;
        }

        public IEnumerable<Toil> GotoTargetMap(TargetIndex ind)
        {
            if (ind == TargetIndex.A)
            {
                var exitSpot = this.ExitSpot1;
                var enterSpot = this.EnterSpot1;
                this.ExitSpot1 = LocalTargetInfo.Invalid;
                this.EnterSpot1 = LocalTargetInfo.Invalid;
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot, enterSpot);
            }
            if (ind == TargetIndex.B)
            {
                var exitSpot = this.ExitSpot2;
                var enterSpot = this.EnterSpot2;
                this.ExitSpot2 = LocalTargetInfo.Invalid;
                this.EnterSpot2 = LocalTargetInfo.Invalid;
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot, enterSpot);
            }
            Log.Error("[VehicleInteriors] GotoTargetMap() does not support TargetIndex.C.");
            return null;
        }

        public Vector3 drawOffset;
    }
}