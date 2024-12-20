using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public abstract class JobDriverAcrossMaps : JobDriver
    {
        public bool ShouldEnterTargetAMap => this.exitSpot1.IsValid || this.enterSpot1.IsValid;

        public bool ShouldEnterTargetBMap => this.exitSpot2.IsValid || this.enterSpot2.IsValid;

        public Map DestMap
        {
            get
            {
                if (this.destMap != null) return this.destMap;
                if (this.enterSpot2.IsValid) return this.enterSpot2.Map;
                if (this.exitSpot2.IsValid) return this.exitSpot2.Map.BaseMap();
                if (this.enterSpot1.IsValid) return this.enterSpot1.Map;
                if (this.exitSpot1.IsValid) return this.exitSpot1.Map.BaseMap();
                return base.Map;
            }
        }

        public Map TargetAMap
        {
            get
            {
                if (this.targetAMap != null) return this.targetAMap;
                if (this.enterSpot1.IsValid) return this.enterSpot1.Map;
                if (this.exitSpot1.IsValid) return this.exitSpot1.Map.BaseMap();
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

        public void SetSpots(TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, TargetInfo? exitSpot2 = null, TargetInfo? enterSpot2 = null)
        {
            this.exitSpot1 = exitSpot1 ?? TargetInfo.Invalid;
            this.enterSpot1 = enterSpot1 ?? TargetInfo.Invalid;
            this.exitSpot2 = exitSpot2 ?? TargetInfo.Invalid;
            this.enterSpot2 = enterSpot2 ?? TargetInfo.Invalid;
            this.targetAMap = this.TargetAMap;
            this.destMap = this.DestMap;
        }

        public IEnumerable<Toil> GotoTargetMap(TargetIndex ind)
        {
            if (ind == TargetIndex.A)
            {
                var exitSpot = this.exitSpot1;
                var enterSpot = this.enterSpot1;
                this.exitSpot1 = TargetInfo.Invalid;
                this.enterSpot1 = TargetInfo.Invalid;
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot, enterSpot);
            }
            if (ind == TargetIndex.B)
            {
                var exitSpot = this.exitSpot2;
                var enterSpot = this.enterSpot2;
                this.exitSpot2 = TargetInfo.Invalid;
                this.enterSpot2 = TargetInfo.Invalid;
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot, enterSpot);
            }
            Log.Error("[VehicleInteriors] GotoTargetMap() does not support TargetIndex.C.");
            return null;
        }

        public override void ExposeData()
        {
            Scribe_TargetInfo.Look(ref this.exitSpot1, "exitSpot1");
            Scribe_TargetInfo.Look(ref this.enterSpot1, "enterSpot1");
            Scribe_TargetInfo.Look(ref this.exitSpot2, "exitSpot2");
            Scribe_TargetInfo.Look(ref this.enterSpot2, "enterSpot2");
            Scribe_Values.Look(ref this.drawOffset, "drawOffset");
            Scribe_References.Look(ref this.targetAMap, "targetAMap");
            Scribe_References.Look(ref this.destMap, "destMap");
            base.ExposeData();
        }

        private TargetInfo exitSpot1;

        private TargetInfo enterSpot1;

        private TargetInfo exitSpot2;

        private TargetInfo enterSpot2;

        public Vector3 drawOffset;

        private Map targetAMap;

        private Map destMap;
    }
}