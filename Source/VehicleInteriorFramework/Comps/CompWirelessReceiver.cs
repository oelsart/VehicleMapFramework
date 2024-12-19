using Verse;

namespace VehicleInteriors
{
    public class CompWirelessReceiver : CompToggleLitGraphic, IThingGlower
    {
        new public CompProperties_WirelessCharger Props => (CompProperties_WirelessCharger)this.props;

        public override void CompTick()
        {
            base.CompTick();
            if (this.PowerOutput != 0f && !this.shouldBeLitNow)
            {
                this.PowerOutput = 0f;
                if (this.parent.TryGetComp<CompGlower>(out var comp))
                {
                    comp.UpdateLit(this.parent.Map);
                }
            }
            this.shouldBeLitNow = false;
        }

        new public bool ShouldBeLitNow()
        {
            return this.shouldBeLitNow;
        }
    }
}
