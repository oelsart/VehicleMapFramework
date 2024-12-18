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
                if (this.enterSpot2.Map != null) return this.enterSpot2.Map;
                if (this.exitSpot2.Map != null) return this.exitSpot2.Map.BaseMap();
                if (this.enterSpot1.Map != null) return this.enterSpot1.Map;
                if (this.exitSpot1.Map != null) return this.exitSpot1.Map.BaseMap();
                return base.Map;
            }
        }

        public Map TargetAMap
        {
            get
            {
                if (this.targetAMap != null) return this.targetAMap;
                if (this.enterSpot1.Map != null) return this.enterSpot1.Map;
                if (this.exitSpot1.Map != null) return this.exitSpot1.Map.BaseMap();
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
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var exitSpot1Thing = this.exitSpot1.Thing;
                var exitSpot1Cell = this.exitSpot1.Cell;
                var exitSpot1Map = this.exitSpot1.Map;
                Scribe_References.Look(ref exitSpot1Thing, "exitSpot1Thing");
                Scribe_Values.Look(ref exitSpot1Cell, "exitSpot1Cell");
                Scribe_References.Look(ref exitSpot1Map, "exitSpot1Map");
                var exitSpot2Thing = this.exitSpot2.Thing;
                var exitSpot2Cell = this.exitSpot2.Cell;
                var exitSpot2Map = this.exitSpot2.Map;
                Scribe_References.Look(ref exitSpot2Thing, "exitSpot2Thing");
                Scribe_Values.Look(ref exitSpot2Cell, "exitSpot2Cell");
                Scribe_References.Look(ref exitSpot2Map, "exitSpot2Map");
                var enterSpot1Thing = this.enterSpot1.Thing;
                var enterSpot1Cell = this.enterSpot1.Cell;
                var enterSpot1Map = this.enterSpot1.Map;
                Scribe_References.Look(ref enterSpot1Thing, "enterSpot1Thing");
                Scribe_Values.Look(ref enterSpot1Cell, "enterSpot1Cell");
                Scribe_References.Look(ref enterSpot1Map, "enterSpot1Map");
                var enterSpot2Thing = this.enterSpot2.Thing;
                var enterSpot2Cell = this.enterSpot2.Cell;
                var enterSpot2Map = this.enterSpot2.Map;
                Scribe_References.Look(ref enterSpot2Thing, "enterSpot2Thing");
                Scribe_Values.Look(ref enterSpot2Cell, "enterSpot2Cell");
                Scribe_References.Look(ref enterSpot2Map, "enterSpot2Map");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Thing exitSpot1Thing = null;
                Scribe_References.Look(ref exitSpot1Thing, "exitSpot1Thing");
                if (exitSpot1Thing != null)
                {
                    this.exitSpot1 = new TargetInfo(exitSpot1Thing);
                }
                else
                {
                    IntVec3 exitSpot1Cell = IntVec3.Invalid;
                    Map exitSpot1Map = null;
                    Scribe_Values.Look(ref exitSpot1Cell, "exitSpot1Cell");
                    Scribe_References.Look(ref exitSpot1Map, "exitSpot1Map");
                    this.exitSpot1 = new TargetInfo(exitSpot1Cell, exitSpot1Map);
                }
                Thing exitSpot2Thing = null;
                Scribe_References.Look(ref exitSpot2Thing, "exitSpot2Thing");
                if (exitSpot2Thing != null)
                {
                    this.exitSpot2 = new TargetInfo(exitSpot2Thing);
                }
                else
                {
                    IntVec3 exitSpot2Cell = IntVec3.Invalid;
                    Map exitSpot2Map = null;
                    Scribe_Values.Look(ref exitSpot2Cell, "exitSpot1Cell");
                    Scribe_References.Look(ref exitSpot2Map, "exitSpot1Map");
                    this.exitSpot2 = new TargetInfo(exitSpot2Cell, exitSpot2Map);
                }
                Thing enterSpot1Thing = null;
                Scribe_References.Look(ref enterSpot1Thing, "enterSpot1Thing");
                if (enterSpot1Thing != null)
                {
                    this.enterSpot1 = new TargetInfo(enterSpot1Thing);
                }
                else
                {
                    IntVec3 enterSpot1Cell = IntVec3.Invalid;
                    Map enterSpot1Map = null;
                    Scribe_Values.Look(ref enterSpot1Cell, "enterSpot1Cell");
                    Scribe_References.Look(ref enterSpot1Map, "enterSpot1Map");
                    this.enterSpot1 = new TargetInfo(enterSpot1Cell, enterSpot1Map);
                }
                Thing enterSpot2Thing = null;
                Scribe_References.Look(ref enterSpot2Thing, "enterSpot2Thing");
                if (enterSpot2Thing != null)
                {
                    this.enterSpot2 = new TargetInfo(enterSpot2Thing);
                }
                else
                {
                    IntVec3 enterSpot2Cell = IntVec3.Invalid;
                    Map enterSpot2Map = null;
                    Scribe_Values.Look(ref enterSpot2Cell, "enterSpot2Cell");
                    Scribe_References.Look(ref enterSpot2Map, "enterSpot2Map");
                    this.enterSpot2 = new TargetInfo(enterSpot2Cell, enterSpot2Map);
                }
            }
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