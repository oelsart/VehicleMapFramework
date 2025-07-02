using Verse;

namespace VehicleInteriors;

public class CompWirelessReceiver : CompToggleLitGraphic, IThingGlower
{
    new public CompProperties_WirelessCharger Props => (CompProperties_WirelessCharger)props;

    public override void CompTick()
    {
        base.CompTick();
        if (Find.TickManager.TicksGame % CompWirelessTransmitter.ticksInterval != 0) return;

        if (PowerOutput != 0f && !shouldBeLitNow)
        {
            PowerOutput = 0f;
            if (parent.TryGetComp<CompGlower>(out var comp))
            {
                comp.UpdateLit(parent.Map);
            }
        }
        shouldBeLitNow = false;
    }

    new public bool ShouldBeLitNow()
    {
        return PowerOutput != 0f;
    }
}
