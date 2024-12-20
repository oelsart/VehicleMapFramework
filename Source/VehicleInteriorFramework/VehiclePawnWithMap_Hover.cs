using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class VehiclePawnWithMap_Hover : VehiclePawnWithMap
    {
        public override Vector3 DrawPos
        {
            get
            {
                var drawPos = base.DrawPos;
                drawPos.z += this.drawOffset;
                return drawPos;
            }
        }

        public override void Tick()
        {
            if (base.ignition.Drafted)
            {
                if (!this.ignitionComplete)
                {
                    if (this.ignitionTick == null)
                    {
                        this.ignitionTick = Find.TickManager.TicksGame;
                    }
                    else
                    {
                        var offsetFactor = Mathf.Min((Find.TickManager.TicksGame - this.ignitionTick.Value) / ignitionDuration, 1f);
                        if (offsetFactor == 1f)
                        {
                            ignitionComplete = true;
                        }
                        this.drawOffset = this.offsetDrafted * offsetFactor;
                    }
                }
                else
                {
                    this.drawOffset = this.offsetDrafted;
                    this.drawOffset += Mathf.Sin(Find.TickManager.TicksGame * 0.075f) * 0.035f;
                }
            }
            else if (this.ignitionTick != null)
            {
                this.ignitionTick = null;
                this.ignitionComplete = false;
                this.landingComplete = false;
            }

            if (!this.landingComplete)
            {
                this.drawOffset = Mathf.Max(0f, this.drawOffset - 0.004f);
                if (this.drawOffset == 0f)
                {
                    this.landingComplete = true;
                }
            }

            base.Tick();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.drawOffset, "floatingOffset");
            Scribe_Values.Look(ref this.ignitionTick, "ignitionTick");
            Scribe_Values.Look(ref this.ignitionComplete, "ignitionComplete");
            Scribe_Values.Look(ref this.landingComplete, "landingComplete");
        }

        private float drawOffset = 0f;

        private int? ignitionTick;

        private bool ignitionComplete;

        private bool landingComplete = true;

        private readonly float offsetDrafted = 0.25f;

        private const float ignitionDuration = 100f;
    }
}
