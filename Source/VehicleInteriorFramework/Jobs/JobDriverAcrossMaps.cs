using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public abstract class JobDriverAcrossMaps : JobDriver
    {
        public bool ShouldEnterTargetAMap => this.exitSpot1.HasThing || this.enterSpot1.HasThing;

        public bool ShouldEnterTargetBMap => this.exitSpot2.HasThing || this.enterSpot2.HasThing;

        public Map DestMap
        {
            get
            {
                if (this.destMap != null) return this.destMap;
                if (this.enterSpot2.HasThing) return this.enterSpot2.Thing.Map;
                if (this.exitSpot2.HasThing) return this.exitSpot2.Thing.BaseMap();
                if (this.enterSpot1.HasThing) return this.enterSpot1.Thing.Map;
                if (this.exitSpot1.HasThing) return this.exitSpot1.Thing.BaseMap();
                return base.Map;
            }
        }

        public Map TargetAMap
        {
            get
            {
                if (this.targetAMap != null) return this.targetAMap;
                if (this.enterSpot1.HasThing) return this.enterSpot1.Thing.Map;
                if (this.exitSpot1.HasThing) return this.exitSpot1.Thing.BaseMap();
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
            this.exitSpot1 = exitSpot1.HasValue ? exitSpot1.Value : LocalTargetInfo.Invalid;
            this.enterSpot1 = enterSpot1.HasValue ? enterSpot1.Value : LocalTargetInfo.Invalid;
            this.exitSpot2 = exitSpot2.HasValue ? exitSpot2.Value : LocalTargetInfo.Invalid;
            this.enterSpot2 = enterSpot2.HasValue ? enterSpot2.Value : LocalTargetInfo.Invalid;
            this.targetAMap = this.TargetAMap;
            this.destMap = this.DestMap;
        }

        public IEnumerable<Toil> GotoTargetMap(TargetIndex ind)
        {
            if (ind == TargetIndex.A)
            {
                var exitSpot = this.exitSpot1;
                var enterSpot = this.enterSpot1;
                this.exitSpot1 = LocalTargetInfo.Invalid;
                this.enterSpot1 = LocalTargetInfo.Invalid;
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot, enterSpot);
            }
            if (ind == TargetIndex.B)
            {
                var exitSpot = this.exitSpot2;
                var enterSpot = this.enterSpot2;
                this.exitSpot2 = LocalTargetInfo.Invalid;
                this.enterSpot2 = LocalTargetInfo.Invalid;
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot, enterSpot);
            }
            Log.Error("[VehicleInteriors] GotoTargetMap() does not support TargetIndex.C.");
            return null;
        }

        public override void ExposeData()
        {
            var exitSpot1 = this.exitSpot1.Thing;
            Scribe_References.Look(ref exitSpot1, "exitSpot1");
            this.exitSpot1 = exitSpot1;
            var enterSpot1 = this.enterSpot1.Thing;
            Scribe_References.Look(ref enterSpot1, "enterSpot1");
            this.enterSpot1 = enterSpot1;
            var exitSpot2 = this.exitSpot2.Thing;
            Scribe_References.Look(ref exitSpot2, "exitSpot2");
            this.exitSpot2 = exitSpot2;
            var enterSpot2 = this.enterSpot2.Thing;
            Scribe_References.Look(ref enterSpot2, "enterSpot2");
            this.enterSpot2 = enterSpot2;
            Scribe_Values.Look(ref this.drawOffset, "drawOffset");
            Scribe_References.Look(ref this.targetAMap, "targetAMap");
            Scribe_References.Look(ref this.destMap, "destMap");
            base.ExposeData();
        }

        private LocalTargetInfo exitSpot1;

        private LocalTargetInfo enterSpot1;

        private LocalTargetInfo exitSpot2;

        private LocalTargetInfo enterSpot2;

        public Vector3 drawOffset;

        private Map targetAMap;

        private Map destMap;
    }
}